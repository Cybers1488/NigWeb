import codecs
content = '''using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Stunnable;
using Content.Shared.Popups;
using Content.Shared._Shitmed.DoAfter;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Components;

namespace Content.Shared._NigWeb.Nerves;

public sealed class NerveSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<BodyPartComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BodyPartComponent, GetDoAfterDelayMultiplierEvent>(OnGetDoAfterDelayMultiplier);
        
        SubscribeLocalEvent<SeveredNerveComponent, ComponentStartup>(OnNerveSevered);
        SubscribeLocalEvent<SeveredNerveComponent, ComponentRemove>(OnNerveHealed);
    }

    private void OnDamageChanged(Entity<BodyPartComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;
            
        // No head nerves as requested
        if (ent.Comp.PartType == BodyPartType.Head)
            return;
            
        var slash = args.DamageDelta.DamageDict.GetValueOrDefault("Slash", 0);
        var piercing = args.DamageDelta.DamageDict.GetValueOrDefault("Piercing", 0);
        
        var totalCut = slash + piercing;
        
        if (totalCut > 15 && !HasComp<SeveredNerveComponent>(ent))
        {
            // 15% chance
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
        ent.Comp.NextDropTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(10, 30));
        ent.Comp.NextTripTime = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(5, 20));

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

    private void OnGetDoAfterDelayMultiplier(Entity<BodyPartComponent> ent, ref GetDoAfterDelayMultiplierEvent args)
    {
        if (HasComp<SeveredNerveComponent>(ent) && ent.Comp.PartType == BodyPartType.Arm)
        {
            // Actions take 2.5x longer with severed arm nerve
            args.Multiplier *= 2.5f;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

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
                    if (hands.ActiveHandId != null)
                    {
                        var held = _hands.GetActiveHand(bodyUid);
                        if (held != null)
                        {
                            _hands.TryDrop(bodyUid);
                            _popup.PopupEntity("Ваша рука непроизвольно разжимается!", bodyUid, bodyUid, PopupType.SmallCaution);
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
        }
    }
}
'''
with codecs.open('Content.Shared/_NigWeb/Nerves/NerveSystem.cs', 'w', 'utf-8') as f:
    f.write(content)
