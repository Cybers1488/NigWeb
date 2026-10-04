using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Stunnable;
using Content.Shared.Standing;
using Content.Shared.Movement.Components;
using Content.Shared.Alert;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared._Shitmed.DoAfter;
using Content.Shared.Rejuvenate;
using Content.Shared.Body.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Content.Shared.Item;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Network;
using System;
using System.Linq;

namespace Content.Shared._NigWeb.Nerves;

public sealed class NerveSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[NerveSystem] Initializing NerveSystem...");
        
        SubscribeLocalEvent<BodyPartComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<SeveredNerveComponent, BodyPartRelayedEvent<GetDoAfterDelayMultiplierEvent>>(OnGetDoAfterDelayMultiplier);
        SubscribeLocalEvent<BodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<MobStateComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<MovementSpeedModifierComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<MovementSpeedModifierComponent, StandUpAttemptEvent>(OnStandUpAttempt);
        
        SubscribeLocalEvent<SeveredNerveComponent, ComponentStartup>(OnNerveSevered);
        SubscribeLocalEvent<SeveredNerveComponent, ComponentRemove>(OnNerveHealed);

        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
    }

    private void OnDamageChanged(Entity<BodyPartComponent> ent, ref DamageChangedEvent args)
    {
        if (!_net.IsServer) return;
        
        if (args.DamageDelta == null)
            return;
            
        if (ent.Comp.PartType == BodyPartType.Head)
            return;
            
        var slash = args.DamageDelta.DamageDict.GetValueOrDefault("Slash", 0);
        var piercing = args.DamageDelta.DamageDict.GetValueOrDefault("Piercing", 0);
        
        var totalCut = slash + piercing;
        
        if (totalCut > 15 && !HasComp<SeveredNerveComponent>(ent))
        {
            if (_random.Prob(0.15f))
            {
                AddComp<SeveredNerveComponent>(ent);
                Log.Info($"[NerveSystem] Nerve severed on body part {ent.Owner} ({ent.Comp.PartType})");
                
                if (ent.Comp.Body.HasValue)
                {
                    var msg = "";
                    if (ent.Comp.PartType == BodyPartType.Arm)
                        msg = "Вы чувствуете острую боль, ваша рука немеет!";
                    else if (ent.Comp.PartType == BodyPartType.Leg)
                        msg = "Ваша нога подкашивается от невыносимой боли!";
                    else if (ent.Comp.PartType == BodyPartType.Chest)
                        msg = "Вы чувствуете, как онемела спина...";

                    if (!string.IsNullOrEmpty(msg))
                        _popup.PopupEntity(msg, ent.Comp.Body.Value, ent.Comp.Body.Value, PopupType.LargeCaution);
                }
            }
        }
    }

    private void OnNerveSevered(Entity<SeveredNerveComponent> ent, ref ComponentStartup args)
    {
        Log.Debug($"[NerveSystem] Processing OnNerveSevered for {ent.Owner}");
        if (_net.IsServer)
        {
            ent.Comp.NextDropTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(10, 30));
            ent.Comp.NextTripTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(5, 20));
        }

        if (TryComp<BodyPartComponent>(ent, out var part) && part.Body.HasValue)
        {
            _movementSpeed.RefreshMovementSpeedModifiers(part.Body.Value);
        }
    }

    private void OnNerveHealed(Entity<SeveredNerveComponent> ent, ref ComponentRemove args)
    {
        Log.Debug($"[NerveSystem] Processing OnNerveHealed for {ent.Owner}");
        if (TryComp<BodyPartComponent>(ent, out var part) && part.Body.HasValue)
        {
            _movementSpeed.RefreshMovementSpeedModifiers(part.Body.Value);
        }
    }

    private void OnGetDoAfterDelayMultiplier(Entity<SeveredNerveComponent> ent, ref BodyPartRelayedEvent<GetDoAfterDelayMultiplierEvent> args)
    {
        if (TryComp<BodyPartComponent>(ent, out var part) && part.PartType == BodyPartType.Arm)
        {
            args.Args.Multiplier *= 2.5f;
        }
    }

    private void OnRefreshMovementSpeed(Entity<BodyComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var speedMod = 1f;
        var query = EntityQueryEnumerator<SeveredNerveComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var nerve, out var part))
        {
            if (part.Body != ent.Owner) continue;

            if (part.PartType == BodyPartType.Leg)
            {
                speedMod *= 0.5f; // 50% slower per severed leg nerve
            }
        }
        
        args.ModifySpeed(speedMod, speedMod);
    }

    private bool IsLowerBodyParalyzed(EntityUid bodyUid, out string reason, out SeveredNerveComponent? alertNerve, out EntityUid alertUid)
    {
        reason = string.Empty;
        alertNerve = null;
        alertUid = EntityUid.Invalid;

        int severedLegs = 0;
        SeveredNerveComponent? legNerveComp = null;
        EntityUid legNerveUid = EntityUid.Invalid;

        var query = EntityQueryEnumerator<SeveredNerveComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var nerve, out var part))
        {
            if (part.Body != bodyUid)
                continue;

            if (part.PartType == BodyPartType.Chest)
            {
                reason = "Вы пытаетесь встать, но ваш спинной мозг поврежден!";
                alertNerve = nerve;
                alertUid = uid;
                return true;
            }

            if (part.PartType == BodyPartType.Leg)
            {
                severedLegs++;
                legNerveComp = nerve;
                legNerveUid = uid;
            }
        }

        // Count how many total legs this body currently has
        int totalLegs = 0;
        foreach (var _ in _body.GetBodyChildrenOfType(bodyUid, BodyPartType.Leg))
        {
            totalLegs++;
        }

        // If both legs have severed nerves, or all remaining legs have severed nerves (e.g. 1 leg remaining and severed)
        if (severedLegs >= 2 || (totalLegs > 0 && severedLegs >= totalLegs))
        {
            reason = "Вы пытаетесь встать, но ваши ноги парализованы из-за повреждения нервов!";
            alertNerve = legNerveComp;
            alertUid = legNerveUid;
            return true;
        }

        return false;
    }

    private void OnStandAttempt(Entity<MovementSpeedModifierComponent> ent, ref StandAttemptEvent args)
    {
        if (IsLowerBodyParalyzed(ent.Owner, out var reason, out var nerve, out var uid))
        {
            args.Cancel();
            if (_net.IsServer && nerve != null && _timing.CurTime > nerve.NextPopupTime)
            {
                nerve.NextPopupTime = _timing.CurTime + TimeSpan.FromSeconds(3);
                Dirty(uid, nerve);
                _popup.PopupEntity(reason, ent.Owner, ent.Owner, PopupType.LargeCaution);
            }
        }
    }

    private void OnStandUpAttempt(Entity<MovementSpeedModifierComponent> ent, ref StandUpAttemptEvent args)
    {
        if (IsLowerBodyParalyzed(ent.Owner, out var reason, out var nerve, out var uid))
        {
            args.Cancelled = true;
            args.Autostand = false;
            if (_net.IsServer && nerve != null && _timing.CurTime > nerve.NextPopupTime)
            {
                nerve.NextPopupTime = _timing.CurTime + TimeSpan.FromSeconds(3);
                Dirty(uid, nerve);
                _popup.PopupEntity(reason, ent.Owner, ent.Owner, PopupType.LargeCaution);
            }
        }
    }

    private void OnRejuvenate(Entity<MobStateComponent> ent, ref RejuvenateEvent args)
    {
        Log.Info($"[NerveSystem] Healing severed nerves on {ent.Owner} due to Rejuvenate");
        var query = EntityQueryEnumerator<SeveredNerveComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var nerve, out var part))
        {
            if (part.Body == ent.Owner)
            {
                RemComp<SeveredNerveComponent>(uid);
            }
        }
    }

    private void OnGunShot(Entity<GunComponent> ent, ref GunShotEvent args)
    {
        if (!_net.IsServer)
            return;

        var user = args.User;
        if (!user.IsValid() || EntityManager.IsQueuedForDeletion(user))
            return;

        Log.Debug($"[NerveSystem] Processing GunShot for gun {ent.Owner}, user {user}");

        bool isHeavy = HasComp<GunRequiresWieldComponent>(ent.Owner)
                    || HasComp<WieldableComponent>(ent.Owner)
                    || (TryComp<ItemComponent>(ent.Owner, out var item) && (item.Size == "Large" || item.Size == "Huge" || item.Size == "Ginormous"));

        if (!isHeavy)
            return;

        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        if (!_hands.IsHolding(user, ent.Owner, out var handName))
            return;

        var isWielded = TryComp<WieldableComponent>(ent.Owner, out var wieldable) && wieldable.Wielded;
        var holdingHand = hands.Hands.GetValueOrDefault(handName);

        bool dropGun = false;

        var query = EntityQueryEnumerator<SeveredNerveComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var nerve, out var part))
        {
            if (part.Body != user || part.PartType != BodyPartType.Arm)
                continue;

            // If the heavy weapon is wielded with two hands, any damaged arm can't withstand the recoil
            if (isWielded)
            {
                dropGun = true;
                break;
            }

            // If held in one hand, check if that specific arm's nerve is severed
            if (holdingHand != null)
            {
                if (part.Symmetry == BodyPartSymmetry.Left && holdingHand.Location == HandLocation.Left)
                    dropGun = true;
                else if (part.Symmetry == BodyPartSymmetry.Right && holdingHand.Location == HandLocation.Right)
                    dropGun = true;
                else if (part.Symmetry == BodyPartSymmetry.None)
                    dropGun = true;

                if (dropGun)
                    break;
            }
        }

        if (dropGun)
        {
            Log.Info($"[NerveSystem] Heavy weapon recoil knocked gun {ent.Owner} out of user {user}'s hands due to severed arm nerve");
            if (_hands.TryDrop(user, ent.Owner))
            {
                _popup.PopupEntity("Из-за поврежденных нервов в руке отдача выбивает оружие из ваших рук!", user, user, PopupType.LargeCaution);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer) return;

        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<SeveredNerveComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var nerve, out var part))
        {
            if (!part.Body.HasValue)
                continue;

            var bodyUid = part.Body.Value;

            if (part.PartType == BodyPartType.Arm && curTime >= nerve.NextDropTime)
            {
                nerve.NextDropTime = curTime + TimeSpan.FromSeconds(_random.NextFloat(15, 45));
                
                if (TryComp<HandsComponent>(bodyUid, out var hands))
                {
                    foreach (var (handId, hand) in hands.Hands)
                    {
                        bool matches = false;
                        if (part.Symmetry == BodyPartSymmetry.Left && hand.Location == HandLocation.Left)
                            matches = true;
                        else if (part.Symmetry == BodyPartSymmetry.Right && hand.Location == HandLocation.Right)
                            matches = true;
                        else if (part.Symmetry == BodyPartSymmetry.None)
                            matches = true;

                        if (matches)
                        {
                            var heldItem = _hands.GetHeldItem(bodyUid, handId);
                            if (heldItem != null)
                            {
                                if (_hands.TryDrop(bodyUid, handId))
                                {
                                    var msg = part.Symmetry == BodyPartSymmetry.Left ? "Ваша левая рука непроизвольно разжимается!" : "Ваша правая рука непроизвольно разжимается!";
                                    _popup.PopupEntity(msg, bodyUid, bodyUid, PopupType.SmallCaution);
                                }
                            }
                        }
                    }
                }
            }

            if (part.PartType == BodyPartType.Leg)
            {
                if (curTime >= nerve.NextTripTime)
                {
                    nerve.NextTripTime = curTime + TimeSpan.FromSeconds(_random.NextFloat(10, 30));
                    
                    if (TryComp<PhysicsComponent>(bodyUid, out var phys) && phys.LinearVelocity.Length() > 0.5f)
                    {
                        _stun.TryKnockdown(bodyUid, TimeSpan.FromSeconds(2), true);
                        _popup.PopupEntity("Ваша нога вас не слушается, и вы падаете!", bodyUid, bodyUid, PopupType.MediumCaution);
                    }
                }

                // If lower body is paralyzed (e.g. both legs damaged), force crawling
                if (IsLowerBodyParalyzed(bodyUid, out _, out _, out _))
                {
                    if (!_standing.IsDown(bodyUid))
                    {
                        _stun.TryCrawling(bodyUid, refresh: true, autoStand: false, drop: false, force: true);
                    }
                }
            }

            if (part.PartType == BodyPartType.Chest)
            {
                // Continuous knockdown for spine damage
                if (!_standing.IsDown(bodyUid))
                {
                    _stun.TryCrawling(bodyUid, refresh: true, autoStand: false, drop: false, force: true);
                }
            }
        }
    }
}
