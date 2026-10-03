using Content.Shared._Shitmed.Medical.Surgery;
using System;
using System.Text.RegularExpressions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Damage;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body.Part;
using Content.Shared.Speech;
using Content.Shared.Throwing;
using Content.Shared.Damage.Events;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Physics.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Utility;

namespace Content.Shared._NigWeb.Teeth;

public sealed class TeethSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<TeethComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<TeethComponent, AccentGetEvent>(OnAccentGet);
        SubscribeLocalEvent<TeethComponent, DamageExamineEvent>(OnDamageExamine);
        SubscribeLocalEvent<TeethComponent, AttemptIngestEvent>(OnAttemptIngestTeeth, before: new[] { typeof(IngestionSystem) });

        
        SubscribeLocalEvent<SurgeryStepTeethRemoveComponent, SurgeryStepEvent>(OnTeethRemove);
        SubscribeLocalEvent<SurgeryStepTeethInsertComponent, SurgeryStepEvent>(OnTeethInsert);
        SubscribeLocalEvent<SurgeryStepTeethImplantComponent, SurgeryStepEvent>(OnTeethImplant);

        SubscribeLocalEvent<SurgeryStepTeethRemoveComponent, SurgeryStepCompleteCheckEvent>(OnTeethRemoveCheck);
        SubscribeLocalEvent<SurgeryStepTeethInsertComponent, SurgeryStepCompleteCheckEvent>(OnTeethInsertCheck);
        SubscribeLocalEvent<SurgeryStepTeethImplantComponent, SurgeryStepCompleteCheckEvent>(OnTeethImplantCheck);
        
    }

    
    private void OnAttemptIngestTeeth(EntityUid uid, TeethComponent component, ref AttemptIngestEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EdibleComponent>(args.Ingested, out var edible))
            return;

        // If it's a drink or a pill, let it pass
        if (edible.Edible.Id == "Drink" || edible.Edible.Id == "Pill")
            return;

        // Check if they have a broken jaw
        if (HasComp<BrokenJawComponent>(uid))
        {
            if (args.Ingest && args.User == uid)
                _popup.PopupClient("Вам больно жевать со сломанной челюстью!", uid, uid);
            else if (args.Ingest)
                _popup.PopupClient("Пациент не может жевать со сломанной челюстью!", uid, args.User);
            
            args.Handled = true; 
            return;
        }

        // Check if they have teeth (if they have less than 10 teeth and less than 2 implants)
        if (component.Implants < 2 && component.CurrentTeeth < 10)
        {
            if (args.Ingest && args.User == uid)
                _popup.PopupClient("Вам нечем жевать эту еду!", uid, uid);
            else if (args.Ingest)
                _popup.PopupClient("У пациента недостаточно зубов, чтобы жевать!", uid, args.User);
            
            args.Handled = true;
            return;
        }
    }

    private void OnDamageExamine(EntityUid uid, TeethComponent component, ref DamageExamineEvent args)
    {
        var msg = new FormattedMessage();
        msg.PushNewline();
        
        if (HasComp<BrokenJawComponent>(uid))
        {
            msg.AddMarkup("[color=red]Челюсть: Сломана[/color]");
        }
        else
        {
            msg.AddMarkup("[color=green]Челюсть: Цела[/color]");
        }
        
        args.Message.AddMessage(msg);
    }

    private void OnDamageChanged(EntityUid uid, TeethComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;
            
        // Look for blunt damage specifically
        if (!args.DamageDelta.DamageDict.TryGetValue("Blunt", out var bluntDamage) || bluntDamage <= 0)
            return;

        // Ensure we only knock out teeth/break jaw if the attacker targeted the head
        if (args.Origin != null && TryComp<Content.Shared._Shitmed.Targeting.TargetingComponent>(args.Origin, out var targeting))
        {
            if (targeting.Target != Content.Shared._Shitmed.Targeting.TargetBodyPart.Head)
                return;
        }

        if (_net.IsServer)
        {
            float damage = bluntDamage.Float();
            int maxRolls = Math.Max(1, (int)(damage / 10f));
            float chance = Math.Min(damage * component.DropChancePerBlunt, 0.9f);
            
            for (int i = 0; i < maxRolls; i++)
            {
                if (component.CurrentTeeth > 0 && _random.Prob(chance))
                {
                    component.CurrentTeeth--;
                    Dirty(uid, component);

                    // Spawn tooth item
                    var tooth = Spawn(component.ToothPrototype, Transform(uid).Coordinates);
                    
                    // Throw it out randomly
                    var throwDir = _random.NextAngle().ToVec();
                    _throwing.TryThrow(tooth, throwDir, _random.NextFloat(2f, 4f), uid, 0f);
                }
                chance /= 2f; // Decrease chance for consecutive drops
            }
        }
    }

    private void OnAccentGet(EntityUid uid, TeethComponent component, ref AccentGetEvent args)
    {
        if (component.Implants >= 2)
            return;

        if (component.CurrentTeeth >= component.MaxTeeth - 4)
            return;

        args.Message = Accentuate(args.Message);
    }

    private string Accentuate(string message)
    {
        // Replace typical lisp characters
        var msg = message;
        msg = Regex.Replace(msg, "[сшщч]", "ф");
        msg = Regex.Replace(msg, "[СШЩЧ]", "Ф");
        msg = Regex.Replace(msg, "[зж]", "в");
        msg = Regex.Replace(msg, "[ЗЖ]", "В");
        msg = Regex.Replace(msg, "[р]", "л");
        msg = Regex.Replace(msg, "[Р]", "Л");
        
        // English equivalents just in case
        msg = Regex.Replace(msg, "[sS]", "th");
        msg = Regex.Replace(msg, "[rR]", "l");

        return msg;
    }
    private void OnTeethRemoveCheck(EntityUid uid, SurgeryStepTeethRemoveComponent comp, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp<TeethComponent>(args.Body, out var teeth) && teeth.CurrentTeeth > 0)
            args.Cancelled = true; // Not complete, so we can do it
    }

    private void OnTeethInsertCheck(EntityUid uid, SurgeryStepTeethInsertComponent comp, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp<TeethComponent>(args.Body, out var teeth) && teeth.CurrentTeeth < teeth.MaxTeeth && teeth.Implants == 0)
            args.Cancelled = true;
    }

    private void OnTeethImplantCheck(EntityUid uid, SurgeryStepTeethImplantComponent comp, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp<TeethComponent>(args.Body, out var teeth) && teeth.CurrentTeeth == 0 && teeth.Implants < 2)
            args.Cancelled = true;
    }


    private void OnTeethRemove(EntityUid uid, SurgeryStepTeethRemoveComponent comp, ref SurgeryStepEvent args)
    {
        if (!TryComp<TeethComponent>(args.Body, out var teeth) || teeth.CurrentTeeth <= 0)
            return;

        if (_net.IsServer)
        {
            teeth.CurrentTeeth--;
            Dirty(args.Body, teeth);
            var tooth = Spawn(teeth.ToothPrototype, Transform(args.Body).Coordinates);
            _popup.PopupEntity("Действие успешно завершено.", args.User, args.User);
        }
    }

    private void OnTeethInsert(EntityUid uid, SurgeryStepTeethInsertComponent comp, ref SurgeryStepEvent args)
    {
        if (!TryComp<TeethComponent>(args.Body, out var teeth) || teeth.CurrentTeeth >= teeth.MaxTeeth)
            return;

        var tool = _hands.GetActiveItemOrSelf(args.User);
        if (!HasComp<ToothItemComponent>(tool))
            return;

        if (_net.IsServer)
        {
            teeth.CurrentTeeth++;
            Dirty(args.Body, teeth);
            QueueDel(tool);
            _popup.PopupEntity("Действие успешно завершено.", args.User, args.User);
        }
    }

    private void OnTeethImplant(EntityUid uid, SurgeryStepTeethImplantComponent comp, ref SurgeryStepEvent args)
    {
        if (!TryComp<TeethComponent>(args.Body, out var teeth) || teeth.CurrentTeeth > 0)
            return;

        var tool = _hands.GetActiveItemOrSelf(args.User);
        if (!HasComp<ToothImplantComponent>(tool))
            return;

        if (_net.IsServer)
        {
            teeth.Implants++;
            Dirty(args.Body, teeth);
            QueueDel(tool);
            _popup.PopupEntity($"Вы успешно установили зубной имплант ({teeth.Implants}/2).", args.User, args.User);
        }
    }
}
