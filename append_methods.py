# -*- coding: utf-8 -*-
with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'r', encoding='utf-8') as f:
    content = f.read()

methods = """
    private void OnTeethRemoveCheck(EntityUid uid, SurgeryStepTeethRemoveComponent comp, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp<TeethComponent>(args.Body, out var teeth) || teeth.CurrentTeeth <= 0)
            args.Cancelled = true;
    }

    private void OnTeethInsertCheck(EntityUid uid, SurgeryStepTeethInsertComponent comp, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp<TeethComponent>(args.Body, out var teeth) || teeth.CurrentTeeth >= teeth.MaxTeeth)
            args.Cancelled = true;
    }

    private void OnTeethImplantCheck(EntityUid uid, SurgeryStepTeethImplantComponent comp, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp<TeethComponent>(args.Body, out var teeth) || teeth.CurrentTeeth > 0)
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
"""

if 'OnTeethRemoveCheck' not in content:
    content = content[:content.rfind('}')] + methods

with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'w', encoding='utf-8') as f:
    f.write(content)
