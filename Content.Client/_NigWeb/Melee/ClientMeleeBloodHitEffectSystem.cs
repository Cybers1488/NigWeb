// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._NigWeb.Melee;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Client._NigWeb.Melee;

/// <summary>
/// Receives <see cref="MeleeBloodHitEffectEvent"/> from the server.
/// Spawns visceral, enlarged directional body slash spurts and directional spray particles at the victim's body,
/// combined with physical GPU pixel-art blood fountains gushing in the exact strike/stump direction onto the floor via <see cref="BloodPixelOverlay"/>.
/// </summary>
public sealed class ClientMeleeBloodHitEffectSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly AnimationPlayerSystem _animPlayer = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private BloodPixelOverlay _overlay = default!;

    private const string BloodHitEffectProto = "EffectBloodHit";
    private const string SpurtAnimKey = "blood_spurt_anim";

    // Rich visceral arterial crimson (deep authentic blood red, never cartoonish, never pitch black)
    private static readonly Color VisceralSpurtBlood = Color.FromHex("#8b0000");
    private static readonly Color VisceralFlankBlood = Color.FromHex("#7a0000");

    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[ClientMeleeBloodHitEffectSystem] Initiating combined directional body slash spurt and GPU blood pixel overlay...");

        _overlay = new BloodPixelOverlay(_random);
        _overlayMan.AddOverlay(_overlay);

        SubscribeNetworkEvent<MeleeBloodHitEffectEvent>(OnBloodHitEffect);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Log.Info("[ClientMeleeBloodHitEffectSystem] Shutting down blood pixel overlay...");
        _overlayMan.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _overlay.FrameUpdate(frameTime);
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

            var mapCoords = _transform.ToMapCoordinates(coords);
            if (mapCoords.MapId == MapId.Nullspace)
            {
                Log.Debug("[ClientMeleeBloodHitEffectSystem] Nullspace coordinates — skipping");
                continue;
            }

            var baseDir = hit.Direction.LengthSquared() > 0.001f
                ? Vector2.Normalize(hit.Direction)
                : Vector2.UnitX;

            // 1. Primary arterial spurt with natural slight angular drift (-12 deg to +12 deg)
            float primaryAngle = _random.NextFloat(-12f, 12f);
            SpawnDirectionalSpurt(coords, baseDir, primaryAngle, 0.40f, 0.85f, 1.10f, 1.55f, 0.28f, VisceralSpurtBlood);

            // 2. Wide flank directional spurt fanning out naturally (+-22 deg to +-45 deg)
            float flankAngle1 = _random.Prob(0.5f) ? _random.NextFloat(22f, 45f) : _random.NextFloat(-45f, -22f);
            SpawnDirectionalSpurt(coords, baseDir, flankAngle1, 0.30f, 0.70f, 0.90f, 1.30f, 0.26f, VisceralFlankBlood);

            // 3. Companion flank or chaotic rebound spurt (breaks cone template)
            float flankAngle2 = _random.Prob(0.35f)
                ? _random.NextFloat(-160f, 160f)
                : (-flankAngle1 * _random.NextFloat(1.1f, 1.6f));
            SpawnDirectionalSpurt(coords, baseDir, flankAngle2, 0.20f, 0.55f, 0.75f, 1.10f, 0.24f, VisceralFlankBlood);

            // 4. Physical pixel-art blood burst with organic wide cone, side-splatter, and 18s floor persistence
            _overlay.EmitBloodBurst(mapCoords.MapId, mapCoords.Position, baseDir);
        }
    }

    /// <summary>
    /// Spawns an enlarged, directional arterial blood spray that moves outward along <paramref name="baseDir"/>.
    /// </summary>
    private void SpawnDirectionalSpurt(
        EntityCoordinates coords,
        Vector2 baseDir,
        float angleOffsetDeg,
        float minDistance,
        float maxDistance,
        float startScale,
        float endScale,
        float lifetime,
        Color tint)
    {
        var particle = Spawn(BloodHitEffectProto, coords);
        if (!TryComp<SpriteComponent>(particle, out var sprite))
            return;

        var dir = Angle.FromDegrees(angleOffsetDeg).RotateVec(baseDir);
        var dirIdx = GetDirIndex(dir);

        // Pure directional arterial squirt jet from Lifeweb blood sheet
        string state = $"squirt_{dirIdx}_0";

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
                        new AnimationTrackProperty.KeyFrame(tint, 0f),
                        new AnimationTrackProperty.KeyFrame(tint, lifetime * 0.35f),
                        new AnimationTrackProperty.KeyFrame(tint.WithAlpha(0f), lifetime, Easings.OutQuad),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(driftOffset, lifetime, Easings.OutQuad),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(new Vector2(startScale, startScale), 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(endScale, endScale), lifetime),
                    }
                }
            }
        };

        var player = EnsureComp<AnimationPlayerComponent>(particle);
        _animPlayer.Play((particle, player), anim, SpurtAnimKey);
    }

    /// <summary>
    /// Correctly maps 2D direction vectors to Lifeweb squirt sprite states.
    /// Index 0 = North (+Y)
    /// Index 1 = South (-Y)
    /// Index 2 = West (-X)
    /// Index 3 = East (+X)
    /// Index 4 = NorthWest (-X, +Y)
    /// Index 5 = NorthEast (+X, +Y)
    /// Index 6 = SouthWest (-X, -Y)
    /// Index 7 = SouthEast (+X, -Y)
    /// </summary>
    private static int GetDirIndex(Vector2 dir)
    {
        if (dir.LengthSquared() < 0.001f)
            return 0; // Default North (UP)

        var norm = Vector2.Normalize(dir);

        // Check diagonals first (when both components are significant)
        if (MathF.Abs(norm.X) > 0.38f && MathF.Abs(norm.Y) > 0.38f)
        {
            if (norm.Y > 0)
                return norm.X > 0 ? 5 : 4; // 5 = NE, 4 = NW
            else
                return norm.X > 0 ? 7 : 6; // 7 = SE, 6 = SW
        }

        // Cardinal directions
        if (MathF.Abs(norm.Y) >= MathF.Abs(norm.X))
        {
            return norm.Y >= 0 ? 0 : 1; // 0 = North (UP), 1 = South (DOWN)
        }
        else
        {
            return norm.X >= 0 ? 3 : 2; // 3 = East (RIGHT), 2 = West (LEFT)
        }
    }
}
