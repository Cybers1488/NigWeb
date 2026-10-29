// SPDX-License-Identifier: MIT

using System.Linq;
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
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._NigWeb.Enemies.SpiritSpawner;

public sealed class SpiritSpawnerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[SpiritSpawnerSystem] Initiating Spirit Spawner Server System...");

        SubscribeLocalEvent<SpiritSpawnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SpiritSpawnerComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<SpiritSpawnerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SpiritSpawnerComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnMapInit(EntityUid uid, SpiritSpawnerComponent comp, MapInitEvent args)
    {
        Log.Debug($"[SpiritSpawnerSystem] Initializing Spirit Spawner entity {uid} on map init.");
        comp.State = SpiritSpawnerState.Idle;
        comp.SpawnCooldownTimer = 1.5f;
        comp.AttackCooldownTimer = 2.5f;

        UpdateAppearance(uid, comp);
        Dirty(uid, comp);
    }

    private void OnAttemptMelee(EntityUid uid, SpiritSpawnerComponent comp, ref AttemptMeleeEvent args)
    {
        // Spawner attacks exclusively through spirit summons and spectral magic
        args.Cancelled = true;
    }

    private void OnMobStateChanged(EntityUid uid, SpiritSpawnerComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead || args.NewMobState == MobState.Critical)
        {
            if (comp.State == SpiritSpawnerState.Dying)
                return;

            Log.Info($"[SpiritSpawnerSystem] Entity {uid} entered Dead/Critical state, initiating death animation...");
            comp.State = SpiritSpawnerState.Dying;
            comp.StateTimer = comp.DeathDur;

            if (comp.SoundDeath != null)
                _audio.PlayPvs(comp.SoundDeath, uid, AudioParams.Default.WithVolume(1.8f));

            UpdateAppearance(uid, comp);
            Dirty(uid, comp);
        }
    }

    private void OnDamageChanged(EntityUid uid, SpiritSpawnerComponent comp, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || comp.State == SpiritSpawnerState.Dying || comp.State == SpiritSpawnerState.Stunned)
            return;

        var dmg = args.DamageDelta.GetTotal();
        comp.AccumulatedDamage += (float) dmg;
        Log.Debug($"[SpiritSpawnerSystem] Entity {uid} accumulated damage: {comp.AccumulatedDamage}/{comp.StunThreshold}");

        if (comp.AccumulatedDamage >= comp.StunThreshold)
        {
            Log.Info($"[SpiritSpawnerSystem] Entity {uid} reached stun threshold ({comp.AccumulatedDamage}), entering Stunned state.");
            comp.AccumulatedDamage = 0f;
            comp.State = SpiritSpawnerState.Stunned;
            comp.StateTimer = comp.StunDur;

            if (comp.SoundStun != null)
                _audio.PlayPvs(comp.SoundStun, uid, AudioParams.Default.WithVolume(1.5f));

            UpdateAppearance(uid, comp);
            Dirty(uid, comp);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SpiritSpawnerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            // Clean up dead/deleted minions
            comp.ActiveMinions.RemoveAll(m => Deleted(m) || !Exists(m) || (TryComp<MobStateComponent>(m, out var ms) && ms.CurrentState != MobState.Alive));

            if (comp.State == SpiritSpawnerState.Dying)
            {
                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                {
                    Log.Debug($"[SpiritSpawnerSystem] Entity {uid} death animation completed, despawning entity.");
                    QueueDel(uid);
                }
                continue;
            }

            if (comp.State == SpiritSpawnerState.Stunned)
            {
                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                {
                    Log.Debug($"[SpiritSpawnerSystem] Entity {uid} recovered from stun, returning to Idle.");
                    comp.State = SpiritSpawnerState.Idle;
                    UpdateAppearance(uid, comp);
                    Dirty(uid, comp);
                }
                continue;
            }

            comp.SpawnCooldownTimer -= frameTime;
            comp.AttackCooldownTimer -= frameTime;

            switch (comp.State)
            {
                case SpiritSpawnerState.SpawnAnticipation:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.State = SpiritSpawnerState.Spawning;
                        comp.StateTimer = comp.SpawningDur;

                        if (comp.SoundCast != null)
                            _audio.PlayPvs(comp.SoundCast, uid, AudioParams.Default.WithVolume(1.6f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case SpiritSpawnerState.Spawning:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.State = SpiritSpawnerState.SpawnRecovery;
                        comp.StateTimer = comp.SpawnRecoveryDur;

                        if (comp.SoundRecover != null)
                            _audio.PlayPvs(comp.SoundRecover, uid, AudioParams.Default.WithVolume(1.2f));

                        // Flower has fully formed in spawner's hands at frame 19.
                        // At the exact transition to recovery (frame 20), flower detaches from spawner's hands into living minion!
                        var myCoords = _transform.GetMapCoordinates(uid, xform);
                        var flowerOffset = new Vector2(0f, 1.80f);
                        var spawnPos = myCoords.Offset(flowerOffset);

                        var minion = Spawn(comp.MinionPrototype, spawnPos);
                        if (TryComp<FlyingFlowerComponent>(minion, out var flowerComp))
                        {
                            flowerComp.Spawner = uid;
                            flowerComp.Target = comp.CurrentTarget;
                            flowerComp.FacingLeft = comp.FacingLeft;

                            _appearance.SetData(minion, FlyingFlowerVisuals.FacingLeft, comp.FacingLeft);
                            _appearance.SetData(minion, FlyingFlowerVisuals.State, FlyingFlowerState.Idle);
                            Dirty(minion, flowerComp);
                        }
                        comp.ActiveMinions.Add(minion);
                        Log.Info($"[SpiritSpawnerSystem] Entity {uid} detached flower spirit {minion} at {spawnPos} (FacingLeft={comp.FacingLeft}). Active count: {comp.ActiveMinions.Count}/{comp.MaxMinions}");

                        comp.SpawnCooldownTimer = comp.SpawnCooldown;
                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case SpiritSpawnerState.SpawnRecovery:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.State = SpiritSpawnerState.Idle;
                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case SpiritSpawnerState.AttackAnticipation:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.State = SpiritSpawnerState.Attacking;
                        comp.StateTimer = comp.AttackingDur;

                        if (comp.SoundAttack != null)
                            _audio.PlayPvs(comp.SoundAttack, uid, AudioParams.Default.WithVolume(1.5f));

                        var myCoords = _transform.GetMapCoordinates(uid, xform);
                        Vector2 dir;
                        if (comp.CurrentTarget != null && Exists(comp.CurrentTarget.Value))
                        {
                            var targetPos = _transform.GetWorldPosition(comp.CurrentTarget.Value);
                            var diff = targetPos - myCoords.Position;
                            dir = diff.LengthSquared() > 0.01f ? Vector2.Normalize(diff) : (comp.FacingLeft ? -Vector2.UnitX : Vector2.UnitX);
                        }
                        else
                        {
                            dir = comp.FacingLeft ? -Vector2.UnitX : Vector2.UnitX;
                        }

                        var proj = Spawn(comp.ProjectilePrototype, myCoords);
                        _gun.ShootProjectile(proj, dir, Vector2.Zero, uid, uid, speed: 12f);
                        Log.Info($"[SpiritSpawnerSystem] Entity {uid} launched Temple of the Three Winds projectile {proj} in direction {dir}");

                        comp.AttackCooldownTimer = comp.AttackCooldown;
                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case SpiritSpawnerState.Attacking:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.State = SpiritSpawnerState.AttackRecovery;
                        comp.StateTimer = comp.AttackRecoveryDur;

                        if (comp.SoundRecover != null)
                            _audio.PlayPvs(comp.SoundRecover, uid, AudioParams.Default.WithVolume(1.2f));

                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case SpiritSpawnerState.AttackRecovery:
                {
                    comp.StateTimer -= frameTime;
                    if (comp.StateTimer <= 0f)
                    {
                        comp.State = SpiritSpawnerState.Idle;
                        UpdateAppearance(uid, comp);
                        Dirty(uid, comp);
                    }
                    break;
                }

                case SpiritSpawnerState.Idle:
                {
                    var myCoords = _transform.GetMapCoordinates(uid, xform);
                    EntityUid? bestTarget = null;
                    float bestDist = comp.Range;

                    foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(myCoords, comp.Range))
                    {
                        var targetUid = candidate.Owner;
                        if (targetUid == uid || _faction.IsEntityFriendly(uid, targetUid) ||
                            HasComp<LadyFaithfulComponent>(targetUid) ||
                            HasComp<SpiritSpawnerComponent>(targetUid) || HasComp<FlyingFlowerComponent>(targetUid) ||
                            HasComp<BuriedMonkComponent>(targetUid) || HasComp<BigGunnerComponent>(targetUid) ||
                            HasComp<LightbeamComponent>(targetUid) || HasComp<ShockwaveComponent>(targetUid) ||
                            !_mobState.IsAlive(targetUid))
                            continue;

                        // Line of sight check: do not target through walls
                        if (!_interaction.InRangeUnobstructed(uid, targetUid, range: comp.Range))
                            continue;

                        var dist = (_transform.GetWorldPosition(targetUid) - myCoords.Position).Length();
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestTarget = targetUid;
                        }
                    }

                    comp.CurrentTarget = bestTarget;

                    if (bestTarget != null)
                    {
                        var targetPos = _transform.GetWorldPosition(bestTarget.Value);
                        bool newFacing = targetPos.X < myCoords.Position.X;
                        if (newFacing != comp.FacingLeft)
                        {
                            comp.FacingLeft = newFacing;
                            if (comp.SoundTurn != null)
                                _audio.PlayPvs(comp.SoundTurn, uid, AudioParams.Default.WithVolume(0.9f));
                            UpdateAppearance(uid, comp);
                            Dirty(uid, comp);
                        }

                        // Priority 1: Summon Flying Flowers if under max minion count
                        if (comp.ActiveMinions.Count < comp.MaxMinions && comp.SpawnCooldownTimer <= 0f)
                        {
                            Log.Debug($"[SpiritSpawnerSystem] Entity {uid} initiating spirit summoning...");
                            comp.State = SpiritSpawnerState.SpawnAnticipation;
                            comp.StateTimer = comp.SpawnAnticipationDur;

                            if (comp.SoundAnticipation != null)
                                _audio.PlayPvs(comp.SoundAnticipation, uid, AudioParams.Default.WithVolume(1.4f));

                            UpdateAppearance(uid, comp);
                            Dirty(uid, comp);
                        }
                        // Priority 2: Cast spectral wind projectile at target
                        else if (comp.AttackCooldownTimer <= 0f)
                        {
                            Log.Debug($"[SpiritSpawnerSystem] Entity {uid} initiating spectral wind attack...");
                            comp.State = SpiritSpawnerState.AttackAnticipation;
                            comp.StateTimer = comp.AttackAnticipationDur;

                            if (comp.SoundAnticipation != null)
                                _audio.PlayPvs(comp.SoundAnticipation, uid, AudioParams.Default.WithVolume(1.4f));

                            UpdateAppearance(uid, comp);
                            Dirty(uid, comp);
                        }
                    }
                    break;
                }
            }
        }
    }

    private void UpdateAppearance(EntityUid uid, SpiritSpawnerComponent comp)
    {
        _appearance.SetData(uid, SpiritSpawnerVisuals.State, comp.State);
        _appearance.SetData(uid, SpiritSpawnerVisuals.FacingLeft, comp.FacingLeft);
    }
}
