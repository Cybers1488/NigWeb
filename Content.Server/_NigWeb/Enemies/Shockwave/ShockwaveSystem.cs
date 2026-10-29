// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Server._NigWeb.Melee;
using Content.Shared._NigWeb.Enemies.Shockwave;
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

namespace Content.Server._NigWeb.Enemies.Shockwave;

public sealed class ShockwaveSystem : EntitySystem
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
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[ShockwaveSystem] Initiating Shockwave Generator Server System...");

        SubscribeLocalEvent<ShockwaveComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShockwaveComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<ShockwaveComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(EntityUid uid, ShockwaveComponent comp, MapInitEvent args)
    {
        Log.Info($"[ShockwaveSystem] MapInit for Shockwave Generator {ToPrettyString(uid)}");

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetCanCollide(uid, true, body: physics);
        }

        _movementSpeed.ChangeBaseSpeed(uid, 0f, 0f, 0f);

        if (_pointLight.TryGetLight(uid, out var light))
        {
            _pointLight.SetEnabled(uid, false, light);
        }

        if (comp.PlaySpawnAnimation)
        {
            comp.State = ShockwaveState.Spawn;
            comp.StateTimer = comp.SpawnDuration;
            if (comp.SoundWakeUp != null)
                _audio.PlayPvs(comp.SoundWakeUp, uid, AudioParams.Default.WithVolume(2.5f));
        }
        else
        {
            comp.State = ShockwaveState.Idle;
        }

        UpdateAppearance(uid, comp);
    }

    private void OnAttemptMelee(EntityUid uid, ShockwaveComponent comp, ref AttemptMeleeEvent args)
    {
        // EN115 Shockwave attacks exclusively via its shockwave pulse blast
        args.Cancelled = true;
    }

    private void OnMobStateChanged(EntityUid uid, ShockwaveComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (comp.State == ShockwaveState.Dying)
            return;

        Log.Info($"[ShockwaveSystem] Entity {ToPrettyString(uid)} reached 0 HP. Initiating destruction explosion...");

        comp.State = ShockwaveState.Dying;
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
            _audio.PlayPvs(comp.SoundDeath, uid, AudioParams.Default.WithVolume(3.0f));

        UpdateAppearance(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShockwaveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            switch (comp.State)
            {
                case ShockwaveState.Dying:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        Log.Info($"[ShockwaveSystem] Destruction complete for {ToPrettyString(uid)}. Entity vanishes.");
                        QueueDel(uid);
                    }
                    break;
                }

                case ShockwaveState.Spawn:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.State = ShockwaveState.Idle;
                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case ShockwaveState.Anticipating:
                {
                    comp.StateTimer -= frameTime;

                    // Dynamically build up energy aura light glow
                    var antProgress = Math.Clamp(1.0f - (comp.StateTimer / Math.Max(0.01f, comp.AnticipationDuration)), 0f, 1f);
                    _pointLight.SetRadius(uid, MathHelper.Lerp(comp.AttackLightRadius * 0.4f, comp.AttackLightRadius * 0.8f, antProgress));
                    _pointLight.SetEnergy(uid, MathHelper.Lerp(0.8f, 2.0f, antProgress));

                    if (comp.StateTimer <= 0f)
                    {
                        Log.Info($"[ShockwaveSystem] {ToPrettyString(uid)} detonating shockwave blast!");

                        comp.State = ShockwaveState.Attacking;
                        comp.StateTimer = comp.AttackDuration;
                        Dirty(uid, comp);

                        // Unleash peak fiery crimson shockwave blast light burst
                        _pointLight.EnsureLight(uid);
                        _pointLight.SetColor(uid, comp.AttackLightColor);
                        _pointLight.SetRadius(uid, comp.AttackLightRadius);
                        _pointLight.SetEnergy(uid, comp.AttackLightEnergy);
                        _pointLight.SetEnabled(uid, true);
                        Log.Info($"[ShockwaveSystem] Shockwave blast light burst on {ToPrettyString(uid)}: color={comp.AttackLightColor}, radius={comp.AttackLightRadius}, energy={comp.AttackLightEnergy}");

                        if (comp.SoundAttack != null)
                            _audio.PlayPvs(comp.SoundAttack, uid, AudioParams.Default.WithVolume(3.2f));

                        var myPos = _transform.GetMapCoordinates(uid).Position;
                        // Apply damage, stun, and knockback impulse to entities in shockwave blast radius
                        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, comp.ShockwaveRadius))
                        {
                            var targetUid = target.Owner;
                            if (targetUid == uid ||
                                HasComp<ShockwaveComponent>(targetUid) ||
                                HasComp<LadyFaithfulComponent>(targetUid) ||
                                _faction.IsEntityFriendly(uid, targetUid) ||
                                target.Comp.CurrentState == MobState.Dead)
                                continue;

                            // Line of sight check: do not damage through walls
                            if (!_interaction.InRangeUnobstructed(uid, targetUid, range: comp.ShockwaveRadius))
                                continue;

                            var tPos = _transform.GetMapCoordinates(targetUid).Position;
                            var diff = tPos - myPos;
                            var dir = diff.LengthSquared() > 0.01f ? Vector2.Normalize(diff) : new Vector2(1, 0);

                            _physics.ApplyLinearImpulse(targetUid, dir * comp.ShockwaveKnockback);
                            _stun.TryKnockdown(targetUid, TimeSpan.FromSeconds(1.5), true);

                            var dmg = new DamageSpecifier();
                            dmg.DamageDict["Blunt"] = comp.DamageBlunt;
                            dmg.DamageDict["Shock"] = comp.DamageShock;
                            _damageable.TryChangeDamage(targetUid, dmg);

                            _audio.PlayPvs(new SoundCollectionSpecifier("NigWebVisceralFleshHit"), targetUid, AudioParams.Default.WithVolume(3.0f));
                            _audio.PlayPvs(new SoundCollectionSpecifier("MetalThud"), targetUid, AudioParams.Default.WithVolume(3.0f));

                            _bloodEffect.EmitBlood(targetUid, dir);
                        }

                        UpdateAppearance(uid, comp);
                    }
                    break;
                }

                case ShockwaveState.Attacking:
                {
                    comp.StateTimer -= frameTime;

                    // Maintain bright blast light during explosion and smoothly taper
                    var attackProgress = Math.Clamp(comp.StateTimer / Math.Max(0.01f, comp.AttackDuration), 0f, 1f);
                    _pointLight.SetEnergy(uid, MathHelper.Lerp(1.2f, comp.AttackLightEnergy, attackProgress));

                    if (comp.StateTimer <= 0f)
                    {
                        comp.State = ShockwaveState.Recovery;
                        comp.StateTimer = comp.RecoveryDuration;

                        if (_pointLight.TryGetLight(uid, out var light))
                            _pointLight.SetEnabled(uid, false, light);

                        if (comp.SoundRecover != null)
                            _audio.PlayPvs(comp.SoundRecover, uid, AudioParams.Default.WithVolume(2.5f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case ShockwaveState.Recovery:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.AttackCooldownTimer = comp.AttackCooldown;
                        comp.State = ShockwaveState.Idle;

                        if (_pointLight.TryGetLight(uid, out var light))
                            _pointLight.SetEnabled(uid, false, light);

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case ShockwaveState.Idle:
                {
                    if (comp.AttackCooldownTimer > 0f)
                        comp.AttackCooldownTimer -= frameTime;

                    // Target validation
                    EntityUid? validTarget = null;
                    if (comp.CurrentTarget != null && Exists(comp.CurrentTarget.Value) && !Deleted(comp.CurrentTarget.Value))
                    {
                        var tUid = comp.CurrentTarget.Value;
                        if (!HasComp<LadyFaithfulComponent>(tUid) &&
                            !_faction.IsEntityFriendly(uid, tUid) &&
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

                    // If target lost or dead, find closest hostile target in line of sight
                    if (validTarget == null)
                    {
                        float closestDist = float.MaxValue;
                        var myCoords = xform.Coordinates;
                        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(myCoords, 10.0f))
                        {
                            var cUid = candidate.Owner;
                            if (cUid == uid ||
                                HasComp<ShockwaveComponent>(cUid) ||
                                HasComp<LadyFaithfulComponent>(cUid) ||
                                _faction.IsEntityFriendly(uid, cUid) ||
                                candidate.Comp.CurrentState == MobState.Dead)
                                continue;

                            // Line of sight check: do not target through walls
                            if (!_interaction.InRangeUnobstructed(uid, cUid, range: 10.0f))
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
                        break;

                    var targetPos = _transform.GetMapCoordinates(validTarget.Value).Position;
                    var myPos = _transform.GetMapCoordinates(uid).Position;
                    var dist = (targetPos - myPos).Length();

                    // Attack trigger check: player enters detection range and is unobstructed
                    if (dist <= comp.AttackRange && comp.AttackCooldownTimer <= 0f && _interaction.InRangeUnobstructed(uid, validTarget.Value, range: comp.AttackRange))
                    {
                        Log.Info($"[ShockwaveSystem] Target within {dist:F1}m <= {comp.AttackRange}m. {ToPrettyString(uid)} starts Anticipation charge!");

                        comp.State = ShockwaveState.Anticipating;
                        comp.StateTimer = comp.AnticipationDuration;

                        _pointLight.EnsureLight(uid);
                        _pointLight.SetColor(uid, comp.AttackLightColor);
                        _pointLight.SetRadius(uid, comp.AttackLightRadius * 0.4f);
                        _pointLight.SetEnergy(uid, 0.8f);
                        _pointLight.SetEnabled(uid, true);
                        Log.Debug($"[ShockwaveSystem] Energy aura light building on {ToPrettyString(uid)}: radius={comp.AttackLightRadius * 0.4f}, energy=0.8");

                        if (comp.SoundAnticipation != null)
                            _audio.PlayPvs(comp.SoundAnticipation, uid, AudioParams.Default.WithVolume(2.8f));

                        if (comp.SoundSpin != null)
                            _audio.PlayPvs(comp.SoundSpin, uid, AudioParams.Default.WithVolume(2.5f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }
            }
        }
    }

    private void UpdateAppearance(EntityUid uid, ShockwaveComponent comp)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, ShockwaveVisuals.State, comp.State, appearance);
        Log.Debug($"[ShockwaveSystem] Updated visual state on {ToPrettyString(uid)}: State={comp.State}");
    }
}
