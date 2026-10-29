// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._NigWeb.Enemies.BigGunner;
using Content.Shared._NigWeb.Enemies.BuriedMonk;
using Content.Shared._NigWeb.Enemies.Lightbeam;
using Content.Shared._NigWeb.Enemies.Shockwave;
using Content.Shared._NigWeb.Enemies.SpiritSpawner;
using Content.Shared._NigWeb.Religion;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._NigWeb.Enemies.SpiritSpawner;

public sealed class FlyingFlowerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[FlyingFlowerSystem] Initiating Flying Flower Server System...");

        SubscribeLocalEvent<FlyingFlowerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FlyingFlowerComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<FlyingFlowerComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(EntityUid uid, FlyingFlowerComponent comp, MapInitEvent args)
    {
        Log.Debug($"[FlyingFlowerSystem] Initializing Flying Flower entity {uid} on map init.");
        comp.State = FlyingFlowerState.Idle;
        _movementSpeed.ChangeBaseSpeed(uid, comp.MoveSpeed, comp.MoveSpeed, 20f);

        UpdateAppearance(uid, comp);
        Dirty(uid, comp);
    }

    private void OnAttemptMelee(EntityUid uid, FlyingFlowerComponent comp, ref AttemptMeleeEvent args)
    {
        // Flying Flower attacks exclusively via explosive blooming burst
        args.Cancelled = true;
    }

    private void OnMobStateChanged(EntityUid uid, FlyingFlowerComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead || args.NewMobState == MobState.Critical)
        {
            if (comp.State == FlyingFlowerState.Dying || comp.State == FlyingFlowerState.Exploding)
                return;

            Log.Info($"[FlyingFlowerSystem] Entity {uid} killed, playing death animation...");
            comp.State = FlyingFlowerState.Dying;
            comp.StateTimer = comp.DeathDur;

            _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);

            if (comp.SoundDeath != null)
                _audio.PlayPvs(comp.SoundDeath, uid, AudioParams.Default.WithVolume(1.4f));

            UpdateAppearance(uid, comp);
            Dirty(uid, comp);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FlyingFlowerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State == FlyingFlowerState.Dying || comp.State == FlyingFlowerState.Exploding)
            {
                _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);
                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                {
                    Log.Debug($"[FlyingFlowerSystem] Entity {uid} animation finished, deleting entity.");
                    QueueDel(uid);
                }
                continue;
            }

            // Brief detachment pause over Spawner's hands before accelerating towards target
            if (comp.SpawnPauseTimer > 0f)
            {
                comp.SpawnPauseTimer -= frameTime;
                _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);
                UpdateAppearance(uid, comp);
                continue;
            }

            if (comp.State == FlyingFlowerState.Anticipating)
            {
                _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);

                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                {
                    Log.Info($"[FlyingFlowerSystem] Entity {uid} detonating explosive bloom burst!");
                    comp.State = FlyingFlowerState.Exploding;
                    comp.StateTimer = comp.ExplosionDur;

                    if (comp.SoundExplosion != null)
                        _audio.PlayPvs(comp.SoundExplosion, uid, AudioParams.Default.WithVolume(1.7f));

                    var myCoords = _transform.GetMapCoordinates(uid, xform);
                    var damage = new DamageSpecifier();
                    damage.DamageDict.Add("Heat", comp.DamageHeat);
                    damage.DamageDict.Add("Slash", comp.DamageSlash);

                    foreach (var ent in _lookup.GetEntitiesInRange(myCoords, comp.ExplosionRadius))
                    {
                        if (ent == uid || ent == comp.Spawner || _faction.IsEntityFriendly(uid, ent) ||
                            HasComp<LadyFaithfulComponent>(ent) ||
                            HasComp<FlyingFlowerComponent>(ent) || HasComp<SpiritSpawnerComponent>(ent) ||
                            HasComp<BuriedMonkComponent>(ent) || HasComp<BigGunnerComponent>(ent) ||
                            HasComp<LightbeamComponent>(ent) || HasComp<ShockwaveComponent>(ent))
                            continue;

                        // Line of sight check: do not blast through walls
                        if (!_interaction.InRangeUnobstructed(uid, ent, range: comp.ExplosionRadius))
                            continue;

                        if (_mobState.IsAlive(ent))
                        {
                            _damageable.TryChangeDamage(ent, damage, origin: uid);
                            Log.Debug($"[FlyingFlowerSystem] Damaged entity {ent} with {comp.DamageHeat} Heat + {comp.DamageSlash} Slash.");
                        }
                    }

                    UpdateAppearance(uid, comp);
                    Dirty(uid, comp);
                }
                continue;
            }

            // Idle State: Seek target and monitor distance
            if (comp.Target == null || Deleted(comp.Target.Value) || !_mobState.IsAlive(comp.Target.Value))
            {
                var myCoords = _transform.GetMapCoordinates(uid, xform);
                EntityUid? bestTarget = null;
                float bestDist = 14.0f;

                foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(myCoords, 14.0f))
                {
                    var targetUid = candidate.Owner;
                    if (targetUid == uid || targetUid == comp.Spawner || _faction.IsEntityFriendly(uid, targetUid) ||
                        HasComp<LadyFaithfulComponent>(targetUid) ||
                        HasComp<FlyingFlowerComponent>(targetUid) || HasComp<SpiritSpawnerComponent>(targetUid) ||
                        HasComp<BuriedMonkComponent>(targetUid) || HasComp<BigGunnerComponent>(targetUid) ||
                        HasComp<LightbeamComponent>(targetUid) || HasComp<ShockwaveComponent>(targetUid))
                        continue;

                    // Line of sight check: do not target through walls
                    if (!_interaction.InRangeUnobstructed(uid, targetUid, range: 14.0f))
                        continue;

                    if (!_mobState.IsAlive(targetUid))
                        continue;

                    var dist = (_transform.GetWorldPosition(targetUid) - myCoords.Position).Length();
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = targetUid;
                    }
                }
                comp.Target = bestTarget;
            }

            // Determine facing direction: ALWAYS face towards target/player!
            if (comp.Target != null && Exists(comp.Target.Value))
            {
                var myPos = _transform.GetWorldPosition(xform);
                var targetPos = _transform.GetWorldPosition(comp.Target.Value);
                bool newFacing = targetPos.X < myPos.X;

                if (newFacing != comp.FacingLeft)
                {
                    comp.FacingLeft = newFacing;
                    UpdateAppearance(uid, comp);
                    Dirty(uid, comp);
                }
            }

            if (comp.Target != null && Exists(comp.Target.Value))
            {
                var myPos = _transform.GetWorldPosition(xform);
                var targetPos = _transform.GetWorldPosition(comp.Target.Value);
                var dist = (targetPos - myPos).Length();

                if (dist <= comp.TriggerRadius)
                {
                    Log.Debug($"[FlyingFlowerSystem] Entity {uid} reached target proximity ({dist:F2}m <= {comp.TriggerRadius:F2}m). Initiating detonation anticipation.");
                    comp.State = FlyingFlowerState.Anticipating;
                    comp.StateTimer = comp.AnticipationDur;

                    _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);

                    if (comp.SoundAnticipation != null)
                        _audio.PlayPvs(comp.SoundAnticipation, uid, AudioParams.Default.WithVolume(1.3f));

                    UpdateAppearance(uid, comp);
                    Dirty(uid, comp);
                }
                else
                {
                    _movementSpeed.ChangeBaseSpeed(uid, comp.MoveSpeed, comp.MoveSpeed, 20f);
                }
            }
        }
    }

    private void UpdateAppearance(EntityUid uid, FlyingFlowerComponent comp)
    {
        _appearance.SetData(uid, FlyingFlowerVisuals.State, comp.State);
        _appearance.SetData(uid, FlyingFlowerVisuals.FacingLeft, comp.FacingLeft);
    }
}
