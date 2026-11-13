// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Goobstation.Common.Grab;
using Content.Goobstation.Shared.GrabIntent;
using Content.Server.Effects;
using Content.Server.Weapons.Melee;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Execution;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Components;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._NigWeb.Execution;

/// <summary>
///     System governing brutal executions by decapitation on downed, hard-grabbed victims.
/// </summary>
public sealed class DecapitationExecutionSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly ColorFlashEffectSystem _color = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MeleeWeaponSystem _melee = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;

    private static readonly SoundPathSpecifier AxeSound = new("/Audio/_NigWeb/Combat/decapitate_axe.ogg");
    private static readonly SoundCollectionSpecifier BloodSound = new("NigWebBloodSplatter");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);

        SubscribeLocalEvent<BeheadingExecutionComponent, UpdateCanMoveEvent>(OnCanMoveExecutioner);
        SubscribeLocalEvent<BeingBeheadedComponent, UpdateCanMoveEvent>(OnCanMoveVictim);
        SubscribeLocalEvent<BeingBeheadedComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<BeingBeheadedComponent, GrabAttemptReleaseEvent>(OnGrabReleaseAttempt);

        SubscribeLocalEvent<BeheadingExecutionComponent, ComponentShutdown>(OnExecutionerShutdown);
        SubscribeLocalEvent<BeingBeheadedComponent, ComponentShutdown>(OnVictimShutdown);
    }

    private void OnCanMoveExecutioner(Entity<BeheadingExecutionComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnCanMoveVictim(Entity<BeingBeheadedComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnStandAttempt(Entity<BeingBeheadedComponent> ent, ref StandAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnGrabReleaseAttempt(Entity<BeingBeheadedComponent> ent, ref GrabAttemptReleaseEvent args)
    {
        args.Released = false;
    }

    private void OnExecutionerShutdown(Entity<BeheadingExecutionComponent> ent, ref ComponentShutdown args)
    {
        RemComp<BlockMovementComponent>(ent.Owner);
        _actionBlocker.UpdateCanMove(ent.Owner);
    }

    private void OnVictimShutdown(Entity<BeingBeheadedComponent> ent, ref ComponentShutdown args)
    {
        RemComp<BlockMovementComponent>(ent.Owner);
        _actionBlocker.UpdateCanMove(ent.Owner);
    }

    private void OnGetVerbs(Entity<BodyComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = ent.Owner;
        var user = args.User;

        if (target == user)
            return;

        if (!HasHead(target))
            return;

        if (!_standing.IsDown(target))
            return;

        if (!IsHardGrabbed(target))
            return;

        if (!TryGetHeldWeapon(user, out var weapon))
            return;

        if (HasComp<BeingBeheadedComponent>(target) || HasComp<BeheadingExecutionComponent>(user))
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("decapitate-verb-name"),
            Message = Loc.GetString("decapitate-verb-message"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Weapons/Melee/fireaxe.rsi"), "icon"),
            Priority = 10,
            Impact = LogImpact.High,
            Act = () => StartBeheading(user, target, weapon),
        });
    }

    public bool IsHardGrabbed(EntityUid target)
    {
        if (!TryComp<GrabbableComponent>(target, out var grabbable))
            return false;

        if (grabbable.GrabStage < GrabStage.Hard)
            return false;

        if (!TryComp<PullableComponent>(target, out var pullable) || !pullable.BeingPulled)
            return false;

        return true;
    }

    private bool HasHead(EntityUid uid)
    {
        return _body.GetBodyChildrenOfType(uid, BodyPartType.Head).Any();
    }

    private bool TryGetHeldWeapon(EntityUid user, out EntityUid weapon)
    {
        weapon = default;

        if (_hands.GetActiveItem(user) is { } activeItem && IsViableExecutionWeapon(activeItem))
        {
            weapon = activeItem;
            return true;
        }

        foreach (var item in _hands.EnumerateHeld(user))
        {
            if (IsViableExecutionWeapon(item))
            {
                weapon = item;
                return true;
            }
        }

        return false;
    }

    private bool IsViableExecutionWeapon(EntityUid item)
    {
        return HasComp<SharpComponent>(item)
            || HasComp<MeleeWeaponComponent>(item)
            || HasComp<ExecutionComponent>(item);
    }

    public void StartBeheading(EntityUid attacker, EntityUid victim, EntityUid weapon)
    {
        if (!HasHead(victim) || !_standing.IsDown(victim) || !IsHardGrabbed(victim))
            return;

        if (!TryGetHeldWeapon(attacker, out var currentWeapon))
            return;

        if (HasComp<BeingBeheadedComponent>(victim) || HasComp<BeheadingExecutionComponent>(attacker))
            return;

        var blockAttacker = EnsureComp<BlockMovementComponent>(attacker);
        blockAttacker.BlockInteraction = true;
        blockAttacker.BlockUse = true;

        var blockVictim = EnsureComp<BlockMovementComponent>(victim);
        blockVictim.BlockInteraction = true;
        blockVictim.BlockUse = true;

        _actionBlocker.UpdateCanMove(attacker);
        _actionBlocker.UpdateCanMove(victim);

        _standing.Down(victim);

        var execComp = EnsureComp<BeheadingExecutionComponent>(attacker);
        execComp.Victim = victim;
        execComp.Weapon = currentWeapon;
        execComp.Timer = 0f;
        execComp.TotalDuration = 2.95f;
        execComp.FirstStrikeDone = true;

        var beingBeheaded = EnsureComp<BeingBeheadedComponent>(victim);
        beingBeheaded.Executioner = attacker;

        _audio.PlayPvs(AxeSound, victim, AudioParams.Default.WithVolume(2.5f).WithMaxDistance(20f));

        ExecuteFirstStrike(attacker, victim, currentWeapon);
    }

    private void ExecuteFirstStrike(EntityUid attacker, EntityUid victim, EntityUid weapon)
    {
        if (TryComp<MeleeWeaponComponent>(weapon, out var melee))
        {
            _melee.DoLunge(attacker, weapon, Transform(attacker).LocalRotation, Vector2.UnitX, null, Angle.Zero, false);
            _color.RaiseEffect(Color.Red, new() { victim }, Filter.Pvs(victim, entityManager: EntityManager));
        }

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Slash", 35);
        _damageable.TryChangeDamage(victim, damage, origin: attacker);

        _audio.PlayPvs(BloodSound, victim, AudioParams.Default.WithVolume(0f));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BeheadingExecutionComponent>();
        var toComplete = new List<EntityUid>();
        var toCancel = new List<EntityUid>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!Exists(uid) || !Exists(comp.Victim) || Deleted(uid) || Deleted(comp.Victim))
            {
                toCancel.Add(uid);
                continue;
            }

            if (_mobState.IsIncapacitated(uid))
            {
                toCancel.Add(uid);
                continue;
            }

            comp.Timer += frameTime;

            _standing.Down(comp.Victim);

            if (comp.Timer >= comp.TotalDuration)
            {
                toComplete.Add(uid);
            }
        }

        foreach (var uid in toCancel)
        {
            CancelBeheading(uid);
        }

        foreach (var uid in toComplete)
        {
            CompleteBeheading(uid);
        }
    }

    private void CompleteBeheading(EntityUid attacker)
    {
        if (!TryComp<BeheadingExecutionComponent>(attacker, out var comp))
            return;

        var victim = comp.Victim;
        var weapon = comp.Weapon;

        if (Exists(weapon) && TryComp<MeleeWeaponComponent>(weapon, out var melee))
        {
            _melee.DoLunge(attacker, weapon, Transform(attacker).LocalRotation, Vector2.UnitX, null, Angle.Zero, false);
            _color.RaiseEffect(Color.Red, new() { victim }, Filter.Pvs(victim, entityManager: EntityManager));
        }

        TryDecapitate(victim);

        var lethal = new DamageSpecifier();
        lethal.DamageDict.Add("Slash", 350);
        _damageable.TryChangeDamage(victim, lethal, origin: attacker);

        CleanUpExecution(attacker, victim);
    }

    public bool TryDecapitate(EntityUid victim)
    {
        var head = _body.GetBodyChildrenOfType(victim, BodyPartType.Head).FirstOrDefault().Id;
        var chest = _body.GetBodyChildrenOfType(victim, BodyPartType.Chest).FirstOrDefault().Id;

        if (head == default || chest == default)
            return false;

        _wounds.AmputateWoundable(chest, head);
        return true;
    }

    private void CleanUpExecution(EntityUid attacker, EntityUid victim)
    {
        RemComp<BlockMovementComponent>(attacker);
        RemComp<BeheadingExecutionComponent>(attacker);
        _actionBlocker.UpdateCanMove(attacker);

        if (Exists(victim))
        {
            RemComp<BlockMovementComponent>(victim);
            RemComp<BeingBeheadedComponent>(victim);
            _actionBlocker.UpdateCanMove(victim);
        }
    }

    private void CancelBeheading(EntityUid attacker)
    {
        if (TryComp<BeheadingExecutionComponent>(attacker, out var comp))
        {
            CleanUpExecution(attacker, comp.Victim);
        }
        else
        {
            RemComp<BlockMovementComponent>(attacker);
            RemComp<BeheadingExecutionComponent>(attacker);
            _actionBlocker.UpdateCanMove(attacker);
        }
    }
}
