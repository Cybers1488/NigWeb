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
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Network;
using System;

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

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<BodyPartComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<SeveredNerveComponent, BodyPartRelayedEvent<GetDoAfterDelayMultiplierEvent>>(OnGetDoAfterDelayMultiplier);
        SubscribeLocalEvent<BodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<MobStateComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<MovementSpeedModifierComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<MovementSpeedModifierComponent, StandUpAttemptEvent>(OnStandUpAttempt);
        
        SubscribeLocalEvent<SeveredNerveComponent, ComponentStartup>(OnNerveSevered);
        SubscribeLocalEvent<SeveredNerveComponent, ComponentRemove>(OnNerveHealed);
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

    
    private void OnStandAttempt(Entity<MovementSpeedModifierComponent> ent, ref StandAttemptEvent args)
    {
        var query = EntityQueryEnumerator<SeveredNerveComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var nerve, out var part))
        {
            if (part.Body == ent.Owner && part.PartType == BodyPartType.Chest)
            {
                args.Cancel();
                if (_net.IsServer && _timing.CurTime > nerve.NextPopupTime)
                {
                    nerve.NextPopupTime = _timing.CurTime + TimeSpan.FromSeconds(3);
                    Dirty(uid, nerve);
                    _popup.PopupEntity("Вы пытаетесь встать, но ваш спинной мозг поврежден!", ent.Owner, ent.Owner, PopupType.LargeCaution);
                }
                return;
            }
        }
    }

    private void OnStandUpAttempt(Entity<MovementSpeedModifierComponent> ent, ref StandUpAttemptEvent args)
    {
        var query = EntityQueryEnumerator<SeveredNerveComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var nerve, out var part))
        {
            if (part.Body == ent.Owner && part.PartType == BodyPartType.Chest)
            {
                args.Cancelled = true;
                args.Autostand = false;
                if (_net.IsServer && _timing.CurTime > nerve.NextPopupTime)
                {
                    nerve.NextPopupTime = _timing.CurTime + TimeSpan.FromSeconds(3);
                    Dirty(uid, nerve);
                    _popup.PopupEntity("Вы пытаетесь встать, но ваш спинной мозг поврежден!", ent.Owner, ent.Owner, PopupType.LargeCaution);
                }
                return;
            }
        }
    }

    private void OnRejuvenate(Entity<MobStateComponent> ent, ref RejuvenateEvent args)
    {
        var query = EntityQueryEnumerator<SeveredNerveComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var nerve, out var part))
        {
            if (part.Body == ent.Owner)
            {
                RemComp<SeveredNerveComponent>(uid);
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

            if (part.PartType == BodyPartType.Leg && curTime >= nerve.NextTripTime)
            {
                nerve.NextTripTime = curTime + TimeSpan.FromSeconds(_random.NextFloat(10, 30));
                
                if (TryComp<PhysicsComponent>(bodyUid, out var phys) && phys.LinearVelocity.Length() > 0.5f)
                {
                    _stun.TryKnockdown(bodyUid, TimeSpan.FromSeconds(2), true);
                    _popup.PopupEntity("Ваша нога вас не слушается, и вы падаете!", bodyUid, bodyUid, PopupType.MediumCaution);
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
