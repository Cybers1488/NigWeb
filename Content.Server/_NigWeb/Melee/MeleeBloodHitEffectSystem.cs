// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server._NigWeb.BloodSplatter;
using Content.Shared._NigWeb.Melee;
using Content.Shared.Body.Components;
using Content.Shared.Execution;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._NigWeb.Melee;

/// <summary>
/// Detects melee hits on entities, plays visceral bone-crunching flesh slashes
/// or heavy metallic clangs, and broadcasts high-performance blood particle VFX.
/// </summary>
public sealed class MeleeBloodHitEffectSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private static readonly SoundCollectionSpecifier FleshHitSound = new("NigWebVisceralFleshHit");
    private static readonly SoundCollectionSpecifier ArmorClangSound = new("NigWebVisceralArmorClang");

    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[MeleeBloodHitEffectSystem] Initiating visceral combat audio and blood VFX system...");

        // MeleeHitEvent is broadcast by SharedMeleeWeaponSystem on light and wide attacks.
        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
    }

    /// <summary>
    /// Broadcasts a blood spray burst from target entity in the given direction.
    /// Can be called directly by boss/enemy abilities when damaging living targets.
    /// </summary>
    public void EmitBlood(EntityUid target, Vector2 direction)
    {
        if (!TryComp<TransformComponent>(target, out var hitXform))
        {
            Log.Error($"[MeleeBloodHitEffectSystem] Failed to find TransformComponent for {target}");
            return;
        }

        var dir = direction.LengthSquared() > 0.001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var impactOffset = -dir * 0.22f;
        var netCoords = GetNetCoordinates(new EntityCoordinates(target, impactOffset));

        var ev = new MeleeBloodHitEffectEvent(new List<BloodHitData> { new(netCoords, dir) });
        RaiseNetworkEvent(ev, Filter.Pvs(target));
        Log.Debug($"[MeleeBloodHitEffectSystem] Emitted direct blood burst for {target} along {dir}");
    }

    private void OnMeleeHit(MeleeHitEvent args)
    {
        Log.Debug($"[MeleeBloodHitEffectSystem] OnMeleeHit — {args.HitEntities.Count} hit entities from user {args.User}");

        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        // Skip generic weapon forward spray if performing a decapitating execution (the severed neck fountain handles it)
        if (TryComp<ExecutionComponent>(args.Weapon, out var execComp) && execComp.Executing && execComp.Decapitation)
        {
            Log.Debug("[MeleeBloodHitEffectSystem] Execution decapitation blow — skipping generic swing spray in favor of neck stump fountain");
            return;
        }

        var hits = new List<BloodHitData>(args.HitEntities.Count);

        var hasUserXform = TryComp<TransformComponent>(args.User, out var userXform);
        var userPos = hasUserXform && userXform != null ? _transform.GetWorldPosition(userXform) : Vector2.Zero;
        var userFacing = hasUserXform && userXform != null ? _transform.GetWorldRotation(userXform).ToWorldVec() : Vector2.UnitX;

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
                    _audio.PlayPvs(ArmorClangSound, hit, AudioParams.Default.WithVolume(-4.5f).WithVariation(0.08f));
                }
                catch (Exception ex)
                {
                    Log.Error($"[MeleeBloodHitEffectSystem] Failed playing armor clang audio for {hit}: {ex.Message}");
                }
                continue;
            }

            if (TryComp<ArterialBleedSpurtComponent>(hit, out var activeSpurt) && activeSpurt.IsFountain)
            {
                Log.Debug($"[MeleeBloodHitEffectSystem] Entity {hit} has active arterial neck fountain — skipping generic swing spray");
                continue;
            }

            // Visceral flesh cut & bone-crunch impact audio
            try
            {
                _audio.PlayPvs(FleshHitSound, hit, AudioParams.Default.WithVolume(-5.5f).WithVariation(0.12f));
            }
            catch (Exception ex)
            {
                Log.Error($"[MeleeBloodHitEffectSystem] Failed playing flesh hit audio for {hit}: {ex.Message}");
            }

            // Calculate precise direction of the blade slice
            Vector2 sprayDir;
            if (args.Direction.HasValue && args.Direction.Value.LengthSquared() > 0.001f)
            {
                sprayDir = Vector2.Normalize(args.Direction.Value);
            }
            else if (hasUserXform && userXform != null)
            {
                var hitPos = _transform.GetWorldPosition(hitXform);
                var delta = hitPos - userPos;

                if (delta.LengthSquared() > 0.02f)
                {
                    sprayDir = Vector2.Normalize(delta);
                }
                else
                {
                    sprayDir = userFacing;
                }
            }
            else
            {
                sprayDir = userFacing;
            }

            // Offset impact coordinates towards the entry surface of the wound
            var impactOffset = -sprayDir * 0.18f;
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
