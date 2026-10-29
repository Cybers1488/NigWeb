// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Server.Beam;
using Content.Shared._NigWeb.Enemies.Lightbeam;
using Content.Shared._NigWeb.Religion;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._NigWeb.Enemies.Lightbeam;

public sealed class LightbeamSystem : EntitySystem
{
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[LightbeamSystem] Initiating Lightbeam Server System...");

        SubscribeLocalEvent<LightbeamComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LightbeamComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<LightbeamComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(EntityUid uid, LightbeamComponent comp, MapInitEvent args)
    {
        Log.Debug($"[LightbeamSystem] Initializing Lightbeam entity {uid} on map init.");
        comp.State = LightbeamState.Idle;
        comp.CooldownTimer = 1.0f;

        _pointLight.EnsureLight(uid);
        _pointLight.SetColor(uid, comp.AttackLightColor);
        _pointLight.SetRadius(uid, comp.AttackLightRadius * 0.5f);
        _pointLight.SetEnergy(uid, 0.8f);
        _pointLight.SetEnabled(uid, false);

        if (comp.SoundWakeup != null)
            _audio.PlayPvs(comp.SoundWakeup, uid, AudioParams.Default.WithVolume(1.0f));

        UpdateAppearance(uid, comp);
        Dirty(uid, comp);
    }

    private void OnAttemptMelee(EntityUid uid, LightbeamComponent comp, ref AttemptMeleeEvent args)
    {
        // Lightbeam attacks exclusively via divine holy laser beams
        args.Cancelled = true;
    }

    private void OnMobStateChanged(EntityUid uid, LightbeamComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead || args.NewMobState == MobState.Critical)
        {
            Log.Info($"[LightbeamSystem] Entity {uid} entered Dead/Critical state, initiating shattering death...");
            comp.State = LightbeamState.Dying;
            comp.StateTimer = comp.DeathDuration;

            if (_pointLight.TryGetLight(uid, out var light))
                _pointLight.SetEnabled(uid, false, light);

            if (comp.SoundDeath != null)
                _audio.PlayPvs(comp.SoundDeath, uid, AudioParams.Default.WithVolume(2.0f));

            UpdateAppearance(uid, comp);
            Dirty(uid, comp);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LightbeamComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            // Dying state handling
            if (comp.State == LightbeamState.Dying)
            {
                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                {
                    Log.Info($"[LightbeamSystem] Entity {uid} death animation completed, queueing deletion.");
                    QueueDel(uid);
                }
                continue;
            }

            // Target validation and acquisition
            ValidateTarget(uid, comp, xform);

            switch (comp.State)
            {
                case LightbeamState.Idle:
                {
                    if (comp.CooldownTimer > 0f)
                        comp.CooldownTimer -= frameTime;

                    if (comp.CurrentTarget != null && comp.CooldownTimer <= 0f && _interaction.InRangeUnobstructed(uid, comp.CurrentTarget.Value, range: comp.Range))
                    {
                        Log.Info($"[LightbeamSystem] Entity {uid} acquired target {comp.CurrentTarget.Value}, starting activation telegraph.");
                        comp.State = LightbeamState.Activating;
                        comp.StateTimer = comp.ChargeDuration;
                        comp.TickTimer = 0f;

                        // Point initial aim towards target
                        UpdateAimAndFacing(uid, comp, xform, frameTime, instant: true);

                        _pointLight.EnsureLight(uid);
                        _pointLight.SetColor(uid, comp.AttackLightColor);
                        _pointLight.SetRadius(uid, comp.AttackLightRadius * 0.4f);
                        _pointLight.SetEnergy(uid, 1.0f);
                        _pointLight.SetEnabled(uid, true);

                        if (comp.SoundActivate != null)
                            _audio.PlayPvs(comp.SoundActivate, uid, AudioParams.Default.WithVolume(1.5f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case LightbeamState.Activating:
                {
                    comp.StateTimer -= frameTime;
                    comp.TickTimer -= frameTime;

                    // Smoothly track target while charging
                    UpdateAimAndFacing(uid, comp, xform, frameTime, instant: false);

                    // Dynamic light charging up
                    var chargeProgress = Math.Clamp(1f - (comp.StateTimer / Math.Max(0.01f, comp.ChargeDuration)), 0f, 1f);
                    _pointLight.SetRadius(uid, MathHelper.Lerp(comp.AttackLightRadius * 0.4f, comp.AttackLightRadius * 0.8f, chargeProgress));
                    _pointLight.SetEnergy(uid, MathHelper.Lerp(1.0f, comp.AttackLightEnergy * 0.7f, chargeProgress));

                    // Emit telegraph targeting guide ray
                    if (comp.TickTimer <= 0f && comp.CurrentTarget != null)
                    {
                        comp.TickTimer = 0.2f;
                        FireBeamSegment(uid, comp, comp.TelegraphPrototype, "telegraph");
                    }

                    if (comp.StateTimer <= 0f)
                    {
                        Log.Info($"[LightbeamSystem] Entity {uid} charge complete, UNLEASHING HOLY BEAM!");
                        comp.State = LightbeamState.Firing;
                        comp.StateTimer = comp.FiringDuration;
                        comp.TickTimer = 0f;

                        _pointLight.SetRadius(uid, comp.AttackLightRadius);
                        _pointLight.SetEnergy(uid, comp.AttackLightEnergy);

                        if (comp.SoundFiring != null)
                            _audio.PlayPvs(comp.SoundFiring, uid, AudioParams.Default.WithVolume(3.0f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case LightbeamState.Firing:
                {
                    comp.StateTimer -= frameTime;
                    comp.TickTimer -= frameTime;

                    // Smoothly track target during laser emission
                    UpdateAimAndFacing(uid, comp, xform, frameTime, instant: false);

                    // Process laser damage and beam updates
                    if (comp.TickTimer <= 0f)
                    {
                        comp.TickTimer = comp.TickInterval;
                        FireBeamSegment(uid, comp, comp.BeamPrototype, "beam");
                        ProcessLaserDamage(uid, comp, xform);
                    }

                    if (comp.StateTimer <= 0f)
                    {
                        Log.Info($"[LightbeamSystem] Entity {uid} finished holy beam firing, entering recovery.");
                        comp.State = LightbeamState.Recovery;
                        comp.StateTimer = comp.RecoveryDuration;

                        _pointLight.SetRadius(uid, comp.AttackLightRadius * 0.5f);
                        _pointLight.SetEnergy(uid, 0.8f);

                        if (comp.SoundRecover != null)
                            _audio.PlayPvs(comp.SoundRecover, uid, AudioParams.Default.WithVolume(2.0f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case LightbeamState.Recovery:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        Log.Debug($"[LightbeamSystem] Entity {uid} recovery finished, returning to Idle.");
                        comp.State = LightbeamState.Idle;
                        comp.CooldownTimer = comp.AttackCooldown;

                        if (_pointLight.TryGetLight(uid, out var light))
                            _pointLight.SetEnabled(uid, false, light);

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }
            }
        }
    }

    private void ValidateTarget(EntityUid uid, LightbeamComponent comp, TransformComponent xform)
    {
        EntityUid? validTarget = null;
        var myMapPos = _transform.GetMapCoordinates(uid, xform);

        if (comp.CurrentTarget != null && Exists(comp.CurrentTarget.Value) && !Deleted(comp.CurrentTarget.Value))
        {
            var targetUid = comp.CurrentTarget.Value;
            if (!HasComp<LadyFaithfulComponent>(targetUid) &&
                !_faction.IsEntityFriendly(uid, targetUid) &&
                TryComp<MobStateComponent>(targetUid, out var targetMobState) && targetMobState.CurrentState != MobState.Dead)
            {
                var targetMapPos = _transform.GetMapCoordinates(targetUid);
                if (targetMapPos.MapId == myMapPos.MapId)
                {
                    var dist = (targetMapPos.Position - myMapPos.Position).Length();
                    if (dist <= comp.Range * 1.3f && _interaction.InRangeUnobstructed(uid, targetUid, range: comp.Range * 1.3f))
                    {
                        validTarget = targetUid;
                    }
                }
            }
        }

        // If target lost or invalid, find closest valid target in line of sight
        if (validTarget == null && comp.State == LightbeamState.Idle)
        {
            float closestDist = float.MaxValue;
            var myCoords = xform.Coordinates;

            foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(myCoords, comp.Range))
            {
                var candidateUid = candidate.Owner;
                if (candidateUid == uid ||
                    _faction.IsEntityFriendly(uid, candidateUid) ||
                    HasComp<LadyFaithfulComponent>(candidateUid) ||
                    HasComp<LightbeamComponent>(candidateUid) ||
                    candidate.Comp.CurrentState == MobState.Dead)
                    continue;

                // Line of sight check: do not target through walls
                if (!_interaction.InRangeUnobstructed(uid, candidateUid, range: comp.Range))
                    continue;

                var candidatePos = _transform.GetMapCoordinates(candidateUid).Position;
                var distSq = (candidatePos - myMapPos.Position).LengthSquared();
                if (distSq < closestDist)
                {
                    closestDist = distSq;
                    validTarget = candidateUid;
                }
            }
        }

        comp.CurrentTarget = validTarget;
    }

    private void UpdateAimAndFacing(EntityUid uid, LightbeamComponent comp, TransformComponent xform, float frameTime, bool instant)
    {
        if (comp.CurrentTarget == null || !Exists(comp.CurrentTarget.Value) || Deleted(comp.CurrentTarget.Value))
            return;

        var myPos = _transform.GetMapCoordinates(uid, xform).Position;
        var targetPos = _transform.GetMapCoordinates(comp.CurrentTarget.Value).Position;
        var diff = targetPos - myPos;

        if (diff.LengthSquared() < 0.001f)
            return;

        var desiredDir = diff.Normalized();

        if (instant)
        {
            comp.AimAngle = new Angle(desiredDir);
        }
        else
        {
            var currentDir = comp.AimAngle.ToVec();
            var step = Math.Clamp(comp.TrackingSpeed * frameTime, 0f, 1f);
            var lerped = Vector2.Normalize(Vector2.Lerp(currentDir, desiredDir, step));
            comp.AimAngle = new Angle(lerped);
        }

        var newFacingLeft = comp.AimAngle.ToVec().X < 0f;
        if (newFacingLeft != comp.FacingLeft)
        {
            comp.FacingLeft = newFacingLeft;

            if (comp.SoundTurn != null && comp.State == LightbeamState.Idle)
                _audio.PlayPvs(comp.SoundTurn, uid, AudioParams.Default.WithVolume(1.0f));

            UpdateAppearance(uid, comp);
            Dirty(uid, comp);
        }
    }

    private void FireBeamSegment(EntityUid uid, LightbeamComponent comp, string prototype, string state)
    {
        if (comp.CurrentTarget == null || !Exists(comp.CurrentTarget.Value) || Deleted(comp.CurrentTarget.Value))
            return;

        var targetUid = comp.CurrentTarget.Value;
        var userMapPos = _transform.GetMapCoordinates(uid);
        var targetMapPos = _transform.GetMapCoordinates(targetUid);

        var direction = targetMapPos.Position - userMapPos.Position;
        var length = direction.Length();
        if (length < 0.1f)
            return;

        // Block beam against solid walls
        var ray = new CollisionRay(userMapPos.Position, direction.Normalized(), (int) CollisionGroup.Opaque);
        EntityUid? blocker = null;
        foreach (var result in _physics.IntersectRay(userMapPos.MapId, ray, length, uid, false))
        {
            blocker = result.HitEntity;
            break;
        }

        if (blocker != null)
            targetUid = blocker.Value;

        _beam.TryCreateBeam(uid, targetUid, prototype, state, shader: "unshaded");
    }

    private void ProcessLaserDamage(EntityUid uid, LightbeamComponent comp, TransformComponent xform)
    {
        var myMapPos = _transform.GetMapCoordinates(uid, xform);
        var beamDir = comp.AimAngle.ToVec().Normalized();
        var ray = new CollisionRay(myMapPos.Position, beamDir, (int) (CollisionGroup.MobMask | CollisionGroup.Opaque));
        var hits = _physics.IntersectRay(myMapPos.MapId, ray, comp.Range, uid, false);

        foreach (var hit in hits)
        {
            var hitUid = hit.HitEntity;
            if (hitUid == uid || HasComp<LightbeamComponent>(hitUid))
                continue;

            // Stop penetrating beyond opaque walls
            if (TryComp<PhysicsComponent>(hitUid, out var physics) &&
                (physics.CollisionLayer & (int) CollisionGroup.Opaque) != 0 &&
                !HasComp<MobStateComponent>(hitUid))
            {
                break;
            }

            if (TryComp<DamageableComponent>(hitUid, out var damageable))
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Heat"] = comp.DamageHeat;
                dmg.DamageDict["Shock"] = comp.DamageShock;
                _damageable.TryChangeDamage(hitUid, dmg);

                if (physics != null)
                {
                    _physics.ApplyLinearImpulse(hitUid, beamDir * comp.Knockback, body: physics);
                }

                if (comp.SoundHit != null)
                    _audio.PlayPvs(comp.SoundHit, hitUid, AudioParams.Default.WithVolume(-3.0f));

                Log.Debug($"[LightbeamSystem] Laser struck {ToPrettyString(hitUid)}, dealt {comp.DamageHeat} Heat + {comp.DamageShock} Shock.");
            }
        }
    }

    private void UpdateAppearance(EntityUid uid, LightbeamComponent comp)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, LightbeamVisuals.FacingLeft, comp.FacingLeft, appearance);
        _appearance.SetData(uid, LightbeamVisuals.State, comp.State, appearance);
        Log.Debug($"[LightbeamSystem] Updated visual appearance on {uid}: State={comp.State}, FacingLeft={comp.FacingLeft}");
    }
}
