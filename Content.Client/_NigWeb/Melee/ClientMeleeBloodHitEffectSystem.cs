// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._NigWeb.Melee;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Client._NigWeb.Melee;

/// <summary>
/// Spawns dynamic, multi-directional blood spray and splatter VFX particles at hit locations
/// when <see cref="MeleeBloodHitEffectEvent"/> is received from the server.
/// </summary>
public sealed class ClientMeleeBloodHitEffectSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AnimationPlayerSystem _animPlayer = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string BloodHitEffectProto = "EffectBloodHit";
    private const string BloodAnimationKey = "blood_hit_anim";

    private static readonly Color LifewebDarkBlood = Color.FromHex("#780000");
    private static readonly Color LifewebDropletBlood = Color.FromHex("#6b0000");

    private static readonly string[] GenericSplatters =
    {
        "splatter1_0_0",
        "splatter1_1_0",
        "splatter1_2_0",
        "splatter1_3_0",
        "splatter2_0_0",
        "splatter2_1_0",
        "splatter2_2_0",
        "splatter2_3_0",
        "splatter3_0_0",
        "splatter3_1_0",
        "splatter3_2_0",
        "splatter3_3_0",
        "splatter4_0_0",
        "splatter4_1_0",
        "splatter5_0_0",
        "splatter6_0_0",
    };

    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[ClientMeleeBloodHitEffectSystem] Initiating multi-particle blood splatter system...");

        SubscribeNetworkEvent<MeleeBloodHitEffectEvent>(OnBloodHitEffect);
    }

    private void OnBloodHitEffect(MeleeBloodHitEffectEvent ev)
    {
        Log.Debug($"[ClientMeleeBloodHitEffectSystem] Received blood VFX for {ev.Hits.Count} hits");

        foreach (var hit in ev.Hits)
        {
            var coords = GetCoordinates(hit.Coordinates);

            if (!coords.IsValid(EntityManager))
            {
                Log.Debug("[ClientMeleeBloodHitEffectSystem] Invalid coordinates — skipping");
                continue;
            }

            var baseDir = hit.Direction.LengthSquared() > 0.001f
                ? Vector2.Normalize(hit.Direction)
                : Vector2.UnitX;

            // Spawn a rich cone of blood spray and splatter particles
            // 1. Core arterial jets (high speed, directly forward)
            SpawnBloodParticle(coords, baseDir, 0f, 0.55f, 0.85f, 1.0f, 1.45f, 0.38f, true);
            SpawnBloodParticle(coords, baseDir, _random.NextFloat(-8f, 8f), 0.45f, 0.75f, 0.95f, 1.35f, 0.35f, true);

            // 2. Left flank sprays (fanning outward +16° to +34°)
            SpawnBloodParticle(coords, baseDir, _random.NextFloat(16f, 34f), 0.35f, 0.60f, 0.9f, 1.25f, 0.32f, false);
            SpawnBloodParticle(coords, baseDir, _random.NextFloat(28f, 45f), 0.25f, 0.48f, 0.85f, 1.15f, 0.28f, false);

            // 3. Right flank sprays (fanning outward -16° to -34°)
            SpawnBloodParticle(coords, baseDir, _random.NextFloat(-34f, -16f), 0.35f, 0.60f, 0.9f, 1.25f, 0.32f, false);
            SpawnBloodParticle(coords, baseDir, _random.NextFloat(-45f, -28f), 0.25f, 0.48f, 0.85f, 1.15f, 0.28f, false);

            // 4. Fine impact droplet mist (scattered around impact center and slight back-splash)
            SpawnDropletParticle(coords, baseDir, _random.NextFloat(-55f, 55f), 0.18f, 0.42f, 0.26f);
            SpawnDropletParticle(coords, baseDir, _random.NextFloat(-70f, 70f), 0.14f, 0.32f, 0.22f);
            SpawnDropletParticle(coords, baseDir, _random.NextFloat(-90f, 90f), 0.10f, 0.28f, 0.22f);

            // Backward blade splash droplets
            if (_random.Prob(0.65f))
                SpawnDropletParticle(coords, -baseDir, _random.NextFloat(-35f, 35f), 0.12f, 0.30f, 0.20f);

            Log.Debug($"[ClientMeleeBloodHitEffectSystem] Spawned multi-particle blood spray cone along {baseDir} at {coords}");
        }
    }

    private void SpawnBloodParticle(EntityCoordinates coords, Vector2 baseDir, float angleOffsetDeg, float minDistance, float maxDistance, float startScale, float endScale, float lifetime, bool isCore)
    {
        var particle = Spawn(BloodHitEffectProto, coords);

        if (!TryComp<SpriteComponent>(particle, out var sprite))
        {
            Log.Error($"[ClientMeleeBloodHitEffectSystem] Spawned effect {particle} has no SpriteComponent");
            return;
        }

        var dir = Angle.FromDegrees(angleOffsetDeg).RotateVec(baseDir);
        var dirIdx = GetDirIndex(dir);

        string state;
        if (isCore || _random.Prob(0.6f))
        {
            state = $"squirt_{dirIdx}_0";
        }
        else
        {
            var set = _random.Prob(0.5f) ? 1 : 2;
            state = $"dir_splatter_{set}_{dirIdx}_0";
        }

        _sprite.LayerSetRsiState((particle, sprite), 0, state);

        var distance = _random.NextFloat(minDistance, maxDistance);
        var driftOffset = dir * distance;

        var anim = new Animation
        {
            Length = TimeSpan.FromSeconds(lifetime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(LifewebDarkBlood, 0f),
                        new AnimationTrackProperty.KeyFrame(LifewebDarkBlood, lifetime * 0.35f),
                        new AnimationTrackProperty.KeyFrame(LifewebDarkBlood.WithAlpha(0f), lifetime, Easings.OutQuad),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(driftOffset, lifetime, Easings.OutCubic),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(new Vector2(startScale, startScale), 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(endScale, endScale), lifetime * 0.40f, Easings.OutQuad),
                        new AnimationTrackProperty.KeyFrame(new Vector2(endScale * 1.08f, endScale * 1.08f), lifetime),
                    }
                }
            }
        };

        var player = EnsureComp<AnimationPlayerComponent>(particle);
        _animPlayer.Play((particle, player), anim, BloodAnimationKey);
    }

    private void SpawnDropletParticle(EntityCoordinates coords, Vector2 baseDir, float angleOffsetDeg, float minDistance, float maxDistance, float lifetime)
    {
        var particle = Spawn(BloodHitEffectProto, coords);

        if (!TryComp<SpriteComponent>(particle, out var sprite))
            return;

        var state = _random.Pick(GenericSplatters);
        _sprite.LayerSetRsiState((particle, sprite), 0, state);

        var dir = Angle.FromDegrees(angleOffsetDeg).RotateVec(baseDir);
        var distance = _random.NextFloat(minDistance, maxDistance);
        var driftOffset = dir * distance;

        var startScale = _random.NextFloat(0.7f, 0.9f);
        var endScale = startScale * _random.NextFloat(1.2f, 1.4f);

        var anim = new Animation
        {
            Length = TimeSpan.FromSeconds(lifetime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(LifewebDropletBlood, 0f),
                        new AnimationTrackProperty.KeyFrame(LifewebDropletBlood, lifetime * 0.30f),
                        new AnimationTrackProperty.KeyFrame(LifewebDropletBlood.WithAlpha(0f), lifetime, Easings.OutQuad),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(driftOffset, lifetime, Easings.OutCubic),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(new Vector2(startScale, startScale), 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(endScale, endScale), lifetime * 0.40f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(endScale, endScale), lifetime),
                    }
                }
            }
        };

        var player = EnsureComp<AnimationPlayerComponent>(particle);
        _animPlayer.Play((particle, player), anim, BloodAnimationKey);
    }

    private static int GetDirIndex(Vector2 dir)
    {
        if (dir.LengthSquared() < 0.001f)
            return 0;

        var angle = dir.ToWorldAngle();
        var cardinal = angle.GetDir();
        return cardinal switch
        {
            Direction.South => 0,
            Direction.North => 1,
            Direction.East => 2,
            Direction.West => 3,
            Direction.SouthEast => 4,
            Direction.SouthWest => 5,
            Direction.NorthEast => 6,
            Direction.NorthWest => 7,
            _ => 0,
        };
    }
}
