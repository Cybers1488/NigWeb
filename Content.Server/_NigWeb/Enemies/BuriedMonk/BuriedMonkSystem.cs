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
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._NigWeb.Enemies.BuriedMonk;

public sealed class BuriedMonkSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MeleeBloodHitEffectSystem _bloodEffect = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[BuriedMonkSystem] Initiating Buried Monk Server System with pure walking and instant turning...");

        SubscribeLocalEvent<BuriedMonkComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BuriedMonkComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<BuriedMonkComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(EntityUid uid, BuriedMonkComponent comp, MapInitEvent args)
    {
        Log.Info($"[BuriedMonkSystem] MapInit for Buried Monk {ToPrettyString(uid)}");

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetCanCollide(uid, true, body: physics);
        }

        _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);
        comp.State = BuriedMonkState.Idle;

        if (_pointLight.TryGetLight(uid, out var light))
        {
            _pointLight.SetEnabled(uid, false, light);
        }

        UpdateAppearance(uid, comp);
    }

    private void OnAttemptMelee(EntityUid uid, BuriedMonkComponent comp, ref AttemptMeleeEvent args)
    {
        // EN18 Buried Monk does not use standard melee strikes; he attacks strictly via his scream attack.
        args.Cancelled = true;
    }

    private void OnMobStateChanged(EntityUid uid, BuriedMonkComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (comp.State == BuriedMonkState.Dying)
            return;

        Log.Info($"[BuriedMonkSystem] Entity {ToPrettyString(uid)} reached 0 HP. Initiating death dissolution...");

        comp.State = BuriedMonkState.Dying;
        comp.StateTimer = comp.DeathDuration;
        Dirty(uid, comp);

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetCanCollide(uid, false, body: physics);
        }

        _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);

        if (_pointLight.TryGetLight(uid, out var light))
        {
            _pointLight.SetEnabled(uid, false, light);
        }

        if (comp.SoundDeath != null)
            _audio.PlayPvs(comp.SoundDeath, uid, AudioParams.Default.WithVolume(2.5f));

        if (comp.SoundDeathVoice != null)
            _audio.PlayPvs(comp.SoundDeathVoice, uid, AudioParams.Default.WithVolume(2.5f));

        UpdateAppearance(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BuriedMonkComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            UpdateFacing(uid, comp, xform);

            switch (comp.State)
            {
                case BuriedMonkState.Dying:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        Log.Info($"[BuriedMonkSystem] Death dissolution complete for {ToPrettyString(uid)}. Entity vanishes into dust.");
                        QueueDel(uid);
                    }
                    break;
                }

                case BuriedMonkState.Anticipating:
                {
                    comp.StateTimer -= frameTime;

                    // Dynamically build up mystical aura light glow
                    var antProgress = Math.Clamp(1.0f - (comp.StateTimer / Math.Max(0.01f, comp.AnticipationDuration)), 0f, 1f);
                    _pointLight.SetRadius(uid, MathHelper.Lerp(comp.AttackLightRadius * 0.4f, comp.AttackLightRadius * 0.8f, antProgress));
                    _pointLight.SetEnergy(uid, MathHelper.Lerp(0.6f, 1.8f, antProgress));

                    if (comp.StateTimer <= 0f)
                    {
                        Log.Info($"[BuriedMonkSystem] {ToPrettyString(uid)} unleashing StandingScream attack!");

                        comp.State = BuriedMonkState.Screaming;
                        comp.StateTimer = comp.ScreamDuration;
                        Dirty(uid, comp);

                        // Unleash peak mystical blue aura light burst illuminating the entire area
                        _pointLight.EnsureLight(uid);
                        _pointLight.SetColor(uid, comp.AttackLightColor);
                        _pointLight.SetRadius(uid, comp.AttackLightRadius);
                        _pointLight.SetEnergy(uid, comp.AttackLightEnergy);
                        _pointLight.SetEnabled(uid, true);
                        Log.Info($"[BuriedMonkSystem] Aura light burst at peak on {ToPrettyString(uid)}: color={comp.AttackLightColor}, radius={comp.AttackLightRadius}, energy={comp.AttackLightEnergy}");

                        if (comp.SoundAttack != null)
                            _audio.PlayPvs(comp.SoundAttack, uid, AudioParams.Default.WithVolume(3.2f));

                        var myPos = _transform.GetMapCoordinates(uid).Position;
                        // Apply damage, stun, and impulse to entities caught in scream area
                        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, comp.ScreamRadius))
                        {
                            var targetUid = target.Owner;
                            if (targetUid == uid || _faction.IsEntityFriendly(uid, targetUid) ||
                                HasComp<LadyFaithfulComponent>(targetUid) ||
                                HasComp<BuriedMonkComponent>(targetUid) ||
                                HasComp<BigGunnerComponent>(targetUid) ||
                                HasComp<SpiritSpawnerComponent>(targetUid) ||
                                HasComp<FlyingFlowerComponent>(targetUid) ||
                                HasComp<LightbeamComponent>(targetUid) ||
                                HasComp<ShockwaveComponent>(targetUid) ||
                                target.Comp.CurrentState == MobState.Dead)
                                continue;

                            // Line of sight check: do not scream damage through walls
                            if (!_interaction.InRangeUnobstructed(uid, targetUid, range: comp.ScreamRadius))
                                continue;

                            var tPos = _transform.GetMapCoordinates(targetUid).Position;
                            var diff = tPos - myPos;
                            var dir = diff.LengthSquared() > 0.01f ? Vector2.Normalize(diff) : new Vector2(1, 0);

                            _physics.ApplyLinearImpulse(targetUid, dir * comp.ScreamKnockback);
                            _stun.TryKnockdown(targetUid, TimeSpan.FromSeconds(1.5), true);

                            var dmg = new DamageSpecifier();
                            dmg.DamageDict["Blunt"] = 16;
                            dmg.DamageDict["Piercing"] = 8;
                            _damageable.TryChangeDamage(targetUid, dmg);

                            _audio.PlayPvs(new SoundCollectionSpecifier("NigWebBloodSplatter"), targetUid, AudioParams.Default.WithVolume(3.0f));
                            _audio.PlayPvs(new SoundCollectionSpecifier("NigWebVisceralFleshHit"), targetUid, AudioParams.Default.WithVolume(3.0f));

                            _bloodEffect.EmitBlood(targetUid, dir);
                        }

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case BuriedMonkState.Screaming:
                {
                    comp.StateTimer -= frameTime;

                    // Maintain bright aura light during scream shockwave and smoothly taper
                    var screamProgress = Math.Clamp(comp.StateTimer / Math.Max(0.01f, comp.ScreamDuration), 0f, 1f);
                    _pointLight.SetEnergy(uid, MathHelper.Lerp(1.0f, comp.AttackLightEnergy, screamProgress));

                    if (comp.StateTimer <= 0f)
                    {
                        comp.State = BuriedMonkState.Recovery;
                        comp.StateTimer = comp.RecoveryDuration;

                        if (_pointLight.TryGetLight(uid, out var light))
                            _pointLight.SetEnabled(uid, false, light);

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case BuriedMonkState.Recovery:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.AttackCooldownTimer = comp.AttackCooldown;
                        comp.State = BuriedMonkState.Idle;
                        _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);

                        if (_pointLight.TryGetLight(uid, out var light))
                            _pointLight.SetEnabled(uid, false, light);

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case BuriedMonkState.Idle:
                case BuriedMonkState.Walking:
                {
                    if (comp.AttackCooldownTimer > 0f)
                        comp.AttackCooldownTimer -= frameTime;

                    // Target validation - must be alive, on same map, unobstructed, and not friendly
                    EntityUid? validTarget = null;
                    if (comp.CurrentTarget != null && Exists(comp.CurrentTarget.Value) && !Deleted(comp.CurrentTarget.Value))
                    {
                        var tUid = comp.CurrentTarget.Value;
                        if (tUid != uid && !_faction.IsEntityFriendly(uid, tUid) &&
                            !HasComp<LadyFaithfulComponent>(tUid) &&
                            !HasComp<BuriedMonkComponent>(tUid) &&
                            !HasComp<BigGunnerComponent>(tUid) &&
                            !HasComp<SpiritSpawnerComponent>(tUid) &&
                            !HasComp<FlyingFlowerComponent>(tUid) &&
                            !HasComp<LightbeamComponent>(tUid) &&
                            !HasComp<ShockwaveComponent>(tUid) &&
                            TryComp<MobStateComponent>(tUid, out var mobState) && mobState.CurrentState != MobState.Dead)
                        {
                            var targetMap = _transform.GetMapCoordinates(tUid);
                            var myMap = _transform.GetMapCoordinates(uid);
                            if (targetMap.MapId == myMap.MapId && _interaction.InRangeUnobstructed(uid, tUid, range: comp.AttackRange + 2.0f))
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
                                HasComp<BuriedMonkComponent>(cUid) ||
                                HasComp<BigGunnerComponent>(cUid) ||
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

                    // If still no target: stand in place breathing
                    if (validTarget == null)
                    {
                        if (comp.State != BuriedMonkState.Idle)
                        {
                            comp.State = BuriedMonkState.Idle;
                            _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);
                            UpdateAppearance(uid, comp);
                            Dirty(uid, comp);
                        }
                        break;
                    }

                    var targetPos = _transform.GetMapCoordinates(validTarget.Value).Position;
                    var myPos = _transform.GetMapCoordinates(uid).Position;
                    var diff = targetPos - myPos;
                    var dist = diff.Length();

                    // Instant horizontal facing flip without turning pauses
                    if (diff.X < -0.1f && !comp.FacingLeft)
                    {
                        comp.FacingLeft = true;
                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    else if (diff.X > 0.1f && comp.FacingLeft)
                    {
                        comp.FacingLeft = false;
                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }

                    // Attack trigger check: approaching within AttackRange and unobstructed
                    if (dist <= comp.AttackRange && comp.AttackCooldownTimer <= 0f && _interaction.InRangeUnobstructed(uid, validTarget.Value, range: comp.AttackRange))
                    {
                        Log.Info($"[BuriedMonkSystem] Target within {dist:F1}m <= {comp.AttackRange}m. {ToPrettyString(uid)} begins Anticipation preparation!");

                        comp.State = BuriedMonkState.Anticipating;
                        comp.StateTimer = comp.AnticipationDuration;
                        _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);

                        _pointLight.EnsureLight(uid);
                        _pointLight.SetColor(uid, comp.AttackLightColor);
                        _pointLight.SetRadius(uid, comp.AttackLightRadius * 0.4f);
                        _pointLight.SetEnergy(uid, 0.6f);
                        _pointLight.SetEnabled(uid, true);
                        Log.Debug($"[BuriedMonkSystem] Aura light building on {ToPrettyString(uid)}: radius={comp.AttackLightRadius * 0.4f}, energy=0.6");

                        if (comp.SoundAnticipation != null)
                            _audio.PlayPvs(comp.SoundAnticipation, uid, AudioParams.Default.WithVolume(2.8f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                        break;
                    }

                    // Movement / Idle state check
                    if (dist <= comp.AttackRange)
                    {
                        // Within attack range but attack is on cooldown: stand menacingly facing target (Idle)
                        if (comp.State != BuriedMonkState.Idle)
                        {
                            comp.State = BuriedMonkState.Idle;
                            _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);
                            UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                            Dirty(uid, comp);
                        }
                    }
                    else
                    {
                        // Outside attack range: slowly walk towards target (Walking)
                        if (comp.State != BuriedMonkState.Walking)
                        {
                            comp.State = BuriedMonkState.Walking;
                            _movementSpeed.ChangeBaseSpeed(uid, comp.WalkSpeed, comp.SprintSpeed, 20f);
                            UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                            Dirty(uid, comp);
                        }
                    }
                    break;
                }
            }
        }
    }

    private void UpdateAppearance(EntityUid uid, BuriedMonkComponent comp)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, BuriedMonkVisuals.FacingLeft, comp.FacingLeft, appearance);
        _appearance.SetData(uid, BuriedMonkVisuals.State, comp.State, appearance);
        Log.Debug($"[BuriedMonkSystem] Updated visual state on {ToPrettyString(uid)}: State={comp.State}, FacingLeft={comp.FacingLeft}");
    }

    private void UpdateFacing(EntityUid uid, BuriedMonkComponent comp, TransformComponent xform)
    {
        if (comp.State == BuriedMonkState.Dying)
            return;

        var myPos = _transform.GetMapCoordinates(uid).Position;
        float diffX = 0f;
        bool hasDiff = false;

        if (comp.CurrentTarget != null && Exists(comp.CurrentTarget.Value) && !Deleted(comp.CurrentTarget.Value))
        {
            var targetPos = _transform.GetMapCoordinates(comp.CurrentTarget.Value).Position;
            diffX = targetPos.X - myPos.X;
            hasDiff = true;
        }

        if (!hasDiff && TryComp<PhysicsComponent>(uid, out var physics))
        {
            diffX = physics.LinearVelocity.X;
            if (MathF.Abs(diffX) > 0.1f)
                hasDiff = true;
        }

        if (!hasDiff)
            return;

        if (diffX < -0.1f && !comp.FacingLeft)
        {
            comp.FacingLeft = true;
            UpdateAppearance(uid, comp);
            Dirty(uid, comp);
            Log.Debug($"[BuriedMonkSystem] {ToPrettyString(uid)} flipped facing to Left");
        }
        else if (diffX > 0.1f && comp.FacingLeft)
        {
            comp.FacingLeft = false;
            UpdateAppearance(uid, comp);
            Dirty(uid, comp);
            Log.Debug($"[BuriedMonkSystem] {ToPrettyString(uid)} flipped facing to Right");
        }
    }
}
