// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._NigWeb.Melee;
using Content.Shared.Body.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._NigWeb.Melee;

/// <summary>
/// Detects melee hits on entities, spawns transient blood spray VFX,
/// and plays visceral bone-crunching flesh slashes or heavy metallic clangs.
/// </summary>
public sealed class MeleeBloodHitEffectSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private static readonly SoundCollectionSpecifier FleshHitSound = new("NigWebVisceralFleshHit");
    private static readonly SoundCollectionSpecifier ArmorClangSound = new("NigWebVisceralArmorClang");

    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[MeleeBloodHitEffectSystem] Initiating visceral combat audio and blood splatter system...");

        // MeleeHitEvent is broadcast by SharedMeleeWeaponSystem on light and wide attacks.
        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(MeleeHitEvent args)
    {
        Log.Debug($"[MeleeBloodHitEffectSystem] OnMeleeHit — {args.HitEntities.Count} hit entities from user {args.User}");

        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var hits = new List<BloodHitData>(args.HitEntities.Count);

        Vector2 fallbackDir = Vector2.UnitX;
        var hasUserXform = TryComp<TransformComponent>(args.User, out var userXform);

        foreach (var hit in args.HitEntities)
        {
            if (!TryComp<TransformComponent>(hit, out var hitXform))
            {
                Log.Error($"[MeleeBloodHitEffectSystem] Failed to find TransformComponent for {hit}");
                continue;
            }

            if (!HasComp<BloodstreamComponent>(hit))
            {
                Log.Debug($"[MeleeBloodHitEffectSystem] Entity {hit} has no BloodstreamComponent — metallic clang deflection");
                try
                {
                    _audio.PlayPvs(ArmorClangSound, hit, AudioParams.Default.WithVolume(0.5f).WithVariation(0.08f));
                }
                catch (Exception ex)
                {
                    Log.Error($"[MeleeBloodHitEffectSystem] Failed playing armor clang audio for {hit}: {ex.Message}");
                }
                continue;
            }

            // Visceral flesh cut & bone-crunch impact audio
            try
            {
                _audio.PlayPvs(FleshHitSound, hit, AudioParams.Default.WithVolume(1.8f).WithVariation(0.12f));
            }
            catch (Exception ex)
            {
                Log.Error($"[MeleeBloodHitEffectSystem] Failed playing flesh hit audio for {hit}: {ex.Message}");
            }

            // Determine directional vector for the blood spray
            Vector2 sprayDir;
            if (args.Direction.HasValue && args.Direction.Value.LengthSquared() > 0.001f)
            {
                sprayDir = Vector2.Normalize(args.Direction.Value);
            }
            else if (hasUserXform && userXform != null)
            {
                var delta = _transform.GetWorldPosition(hitXform) - _transform.GetWorldPosition(userXform);
                sprayDir = delta.LengthSquared() > 0.001f ? Vector2.Normalize(delta) : fallbackDir;
            }
            else
            {
                sprayDir = fallbackDir;
            }

            // Offset impact coordinates towards the attacker so blood erupts at the entry surface
            var impactOffset = -sprayDir * 0.22f;
            var netCoords = GetNetCoordinates(new EntityCoordinates(hit, impactOffset));
            hits.Add(new BloodHitData(netCoords, sprayDir));
            Log.Debug($"[MeleeBloodHitEffectSystem] Queued blood VFX for entity {hit} with direction {sprayDir} offset {impactOffset}");
        }

        if (hits.Count == 0)
            return;

        var ev = new MeleeBloodHitEffectEvent(hits);
        RaiseNetworkEvent(ev, Filter.Pvs(args.User));
        Log.Debug($"[MeleeBloodHitEffectSystem] Dispatched MeleeBloodHitEffectEvent with {hits.Count} hits");
    }
}
