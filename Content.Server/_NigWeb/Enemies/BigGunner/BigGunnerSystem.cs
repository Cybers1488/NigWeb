// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Server._NigWeb.Melee;
using Content.Shared._NigWeb.Enemies.BigGunner;
using Content.Shared._NigWeb.Enemies.BuriedMonk;
using Content.Shared._NigWeb.Enemies.Lightbeam;
using Content.Shared._NigWeb.Enemies.Shockwave;
using Content.Shared._NigWeb.Enemies.SpiritSpawner;
using Content.Shared._NigWeb.Religion;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._NigWeb.Enemies.BigGunner;

public sealed class BigGunnerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly MeleeBloodHitEffectSystem _bloodEffect = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[BigGunnerSystem] Initiating Big Gunner Server System...");

        SubscribeLocalEvent<BigGunnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BigGunnerComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<BigGunnerComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(EntityUid uid, BigGunnerComponent comp, MapInitEvent args)
    {
        Log.Info($"[BigGunnerSystem] MapInit for Big Gunner {ToPrettyString(uid)}");

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetCanCollide(uid, true, body: physics);
        }

        _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);
        comp.State = BigGunnerState.Idle;
        UpdateAppearance(uid, comp);
    }

    private void OnAttemptMelee(EntityUid uid, BigGunnerComponent comp, ref AttemptMeleeEvent args)
    {
        // Big Gunner attacks strictly via his custom ranged cannon fire and melee slam state machine
        args.Cancelled = true;
    }

    private void OnMobStateChanged(EntityUid uid, BigGunnerComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (comp.State == BigGunnerState.Dying)
            return;

        Log.Info($"[BigGunnerSystem] Entity {ToPrettyString(uid)} reached 0 HP. Initiating death collapse...");

        comp.State = BigGunnerState.Dying;
        comp.StateTimer = comp.DeathDuration;
        Dirty(uid, comp);

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetCanCollide(uid, false, body: physics);
        }

        _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);

        if (comp.SoundDeath != null)
            _audio.PlayPvs(comp.SoundDeath, uid, AudioParams.Default.WithVolume(3.2f));

        UpdateAppearance(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BigGunnerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            UpdateFacing(uid, comp, xform);

            switch (comp.State)
            {
                case BigGunnerState.Dying:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        Log.Info($"[BigGunnerSystem] Death collapse complete for {ToPrettyString(uid)}. Entity vanishes.");
                        QueueDel(uid);
                    }
                    break;
                }

                case BigGunnerState.RangedAnticipating:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        Log.Info($"[BigGunnerSystem] {ToPrettyString(uid)} firing siege cannon!");

                        comp.State = BigGunnerState.RangedAttacking;
                        comp.StateTimer = comp.RangedAttackDuration;
                        Dirty(uid, comp);

                        if (comp.SoundGunBlast != null)
                            _audio.PlayPvs(comp.SoundGunBlast, uid, AudioParams.Default.WithVolume(5.0f));

                        // Spawn and fire cannonball projectile
                        var myCoords = xform.Coordinates;
                        var myPos = _transform.GetMapCoordinates(uid).Position;
                        var targetPos = myPos + (comp.FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0));

                        if (comp.CurrentTarget != null && Exists(comp.CurrentTarget.Value) && !Deleted(comp.CurrentTarget.Value))
                        {
                            targetPos = _transform.GetMapCoordinates(comp.CurrentTarget.Value).Position;
                        }

                        var diff = targetPos - myPos;
                        var dir = diff.LengthSquared() > 0.01f ? Vector2.Normalize(diff) : (comp.FacingLeft ? -Vector2.UnitX : Vector2.UnitX);

                        var proj = Spawn(comp.CannonballPrototype, myCoords);
                        _gun.ShootProjectile(proj, dir, Vector2.Zero, uid, uid, comp.CannonballSpeed);

                        UpdateAppearance(uid, comp);
                    }
                    break;
                }

                case BigGunnerState.RangedAttacking:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.RangedCooldownTimer = comp.RangedCooldown;
                        comp.State = BigGunnerState.Idle;
                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case BigGunnerState.MeleeAnticipating:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        Log.Info($"[BigGunnerSystem] {ToPrettyString(uid)} smashing cannon into the ground!");

                        comp.State = BigGunnerState.MeleeAttacking;
                        comp.StateTimer = comp.MeleeAttackDuration;
                        Dirty(uid, comp);

                        if (comp.SoundMeleeSlam != null)
                            _audio.PlayPvs(comp.SoundMeleeSlam, uid, AudioParams.Default.WithVolume(6.0f));

                        var myPos = _transform.GetMapCoordinates(uid).Position;
                        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, comp.MeleeRadius))
                        {
                            var targetUid = target.Owner;
                            if (targetUid == uid || _faction.IsEntityFriendly(uid, targetUid) ||
                                HasComp<LadyFaithfulComponent>(targetUid) ||
                                HasComp<BigGunnerComponent>(targetUid) ||
                                HasComp<BuriedMonkComponent>(targetUid) ||
                                HasComp<SpiritSpawnerComponent>(targetUid) ||
                                HasComp<FlyingFlowerComponent>(targetUid) ||
                                HasComp<LightbeamComponent>(targetUid) ||
                                HasComp<ShockwaveComponent>(targetUid) ||
                                target.Comp.CurrentState == MobState.Dead)
                                continue;

                            // Line of sight check: do not melee slam through walls
                            if (!_interaction.InRangeUnobstructed(uid, targetUid, range: comp.MeleeRadius))
                                continue;

                            var tPos = _transform.GetMapCoordinates(targetUid).Position;
                            var diff = tPos - myPos;
                            var dir = diff.LengthSquared() > 0.01f ? Vector2.Normalize(diff) : (comp.FacingLeft ? -Vector2.UnitX : Vector2.UnitX);

                            _physics.ApplyLinearImpulse(targetUid, dir * comp.MeleeKnockback);
                            _stun.TryKnockdown(targetUid, TimeSpan.FromSeconds(1.5), true);

                            var dmg = new DamageSpecifier();
                            dmg.DamageDict["Blunt"] = comp.MeleeDamage;
                            _damageable.TryChangeDamage(targetUid, dmg);

                            if (comp.SoundMeleeHit != null)
                                _audio.PlayPvs(comp.SoundMeleeHit, targetUid, AudioParams.Default.WithVolume(5.0f));
                            _audio.PlayPvs(new SoundCollectionSpecifier("NigWebVisceralFleshHit"), targetUid, AudioParams.Default.WithVolume(3.0f));

                            _bloodEffect.EmitBlood(targetUid, dir);
                        }

                        UpdateAppearance(uid, comp);
                    }
                    break;
                }

                case BigGunnerState.MeleeAttacking:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.MeleeCooldownTimer = comp.MeleeCooldown;
                        comp.State = BigGunnerState.Idle;
                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case BigGunnerState.Idle:
                {
                    if (comp.MeleeCooldownTimer > 0f)
                        comp.MeleeCooldownTimer -= frameTime;

                    if (comp.RangedCooldownTimer > 0f)
                        comp.RangedCooldownTimer -= frameTime;

                    // Ensure Big Gunner remains firmly stationary as siege artillery
                    _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);

                    // Target validation - must be alive, on same map, unobstructed, and not friendly
                    EntityUid? validTarget = null;
                    if (comp.CurrentTarget != null && Exists(comp.CurrentTarget.Value) && !Deleted(comp.CurrentTarget.Value))
                    {
                        var tUid = comp.CurrentTarget.Value;
                        if (tUid != uid && !_faction.IsEntityFriendly(uid, tUid) &&
                            !HasComp<LadyFaithfulComponent>(tUid) &&
                            !HasComp<BigGunnerComponent>(tUid) &&
                            !HasComp<BuriedMonkComponent>(tUid) &&
                            !HasComp<SpiritSpawnerComponent>(tUid) &&
                            !HasComp<FlyingFlowerComponent>(tUid) &&
                            !HasComp<LightbeamComponent>(tUid) &&
                            !HasComp<ShockwaveComponent>(tUid) &&
                            TryComp<MobStateComponent>(tUid, out var mobState) && mobState.CurrentState != MobState.Dead)
                        {
                            var targetMap = _transform.GetMapCoordinates(tUid);
                            var myMap = _transform.GetMapCoordinates(uid);
                            if (targetMap.MapId == myMap.MapId && _interaction.InRangeUnobstructed(uid, tUid, range: comp.RangedRange + 2.0f))
                            {
                                validTarget = tUid;
                            }
                        }
                    }

                    // If target lost or dead, acquire closest hostile target in line of sight
                    if (validTarget == null)
                    {
                        float closestDist = float.MaxValue;
                        var myCoords = xform.Coordinates;
                        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(myCoords, 14.0f))
                        {
                            var cUid = candidate.Owner;
                            if (cUid == uid ||
                                _faction.IsEntityFriendly(uid, cUid) ||
                                HasComp<LadyFaithfulComponent>(cUid) ||
                                HasComp<BigGunnerComponent>(cUid) ||
                                HasComp<BuriedMonkComponent>(cUid) ||
                                HasComp<SpiritSpawnerComponent>(cUid) ||
                                HasComp<FlyingFlowerComponent>(cUid) ||
                                HasComp<LightbeamComponent>(cUid) ||
                                HasComp<ShockwaveComponent>(cUid) ||
                                candidate.Comp.CurrentState == MobState.Dead)
                                continue;

                            // Line of sight check: do not target through walls
                            if (!_interaction.InRangeUnobstructed(uid, cUid, range: 14.0f))
                                continue;

                            var d = (_transform.GetMapCoordinates(cUid).Position - _transform.GetMapCoordinates(uid).Position).LengthSquared();
                            if (d < closestDist)
                            {
                                closestDist = d;
                                validTarget = cUid;
                            }
                        }

                        if (validTarget != null)
                        {
                            comp.CurrentTarget = validTarget;
                            Dirty(uid, comp);
                        }
                        else
                        {
                            comp.CurrentTarget = null;
                        }
                    }

                    if (validTarget == null)
                    {
                        break;
                    }

                    var targetPos = _transform.GetMapCoordinates(validTarget.Value).Position;
                    var myPos = _transform.GetMapCoordinates(uid).Position;
                    var diff = targetPos - myPos;
                    var dist = diff.Length();

                    // Combat decision: Melee slam vs Ranged cannon shot
                    if (dist <= comp.MeleeRange && comp.MeleeCooldownTimer <= 0f && _interaction.InRangeUnobstructed(uid, validTarget.Value, range: comp.MeleeRange))
                    {
                        Log.Info($"[BigGunnerSystem] Target in melee range: {dist:F1}m <= {comp.MeleeRange}m. {ToPrettyString(uid)} starts Melee Slam!");

                        comp.State = BigGunnerState.MeleeAnticipating;
                        comp.StateTimer = comp.MeleeAnticipationDuration;

                        if (comp.SoundMeleeAttack != null)
                            _audio.PlayPvs(comp.SoundMeleeAttack, uid, AudioParams.Default.WithVolume(3.0f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                        break;
                    }

                    if (dist > comp.MinRangedRange && dist <= comp.RangedRange && comp.RangedCooldownTimer <= 0f && _interaction.InRangeUnobstructed(uid, validTarget.Value, range: comp.RangedRange))
                    {
                        Log.Info($"[BigGunnerSystem] Target in firing range: {dist:F1}m. {ToPrettyString(uid)} aims siege cannon!");

                        comp.State = BigGunnerState.RangedAnticipating;
                        comp.StateTimer = comp.RangedAnticipationDuration;

                        if (comp.SoundGunAttack != null)
                            _audio.PlayPvs(comp.SoundGunAttack, uid, AudioParams.Default.WithVolume(3.0f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                        break;
                    }
                    break;
                }
            }
        }
    }

    private void UpdateFacing(EntityUid uid, BigGunnerComponent comp, TransformComponent xform)
    {
        if (comp.State == BigGunnerState.Dying)
            return;

        if (comp.CurrentTarget == null || !Exists(comp.CurrentTarget.Value) || Deleted(comp.CurrentTarget.Value))
            return;

        var myPos = _transform.GetMapCoordinates(uid).Position;
        var targetPos = _transform.GetMapCoordinates(comp.CurrentTarget.Value).Position;
        var diffX = targetPos.X - myPos.X;

        if (diffX < -0.1f && !comp.FacingLeft)
        {
            comp.FacingLeft = true;
            UpdateAppearance(uid, comp);
            Dirty(uid, comp);
            Log.Debug($"[BigGunnerSystem] {ToPrettyString(uid)} flipped facing to Left");
        }
        else if (diffX > 0.1f && comp.FacingLeft)
        {
            comp.FacingLeft = false;
            UpdateAppearance(uid, comp);
            Dirty(uid, comp);
            Log.Debug($"[BigGunnerSystem] {ToPrettyString(uid)} flipped facing to Right");
        }
    }

    private void UpdateAppearance(EntityUid uid, BigGunnerComponent comp)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, BigGunnerVisuals.FacingLeft, comp.FacingLeft, appearance);
        _appearance.SetData(uid, BigGunnerVisuals.State, comp.State, appearance);
        Log.Debug($"[BigGunnerSystem] Updated visual state on {ToPrettyString(uid)}: State={comp.State}, FacingLeft={comp.FacingLeft}");
    }
}
