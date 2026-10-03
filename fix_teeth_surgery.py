# -*- coding: utf-8 -*-
import re

with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'r', encoding='utf-8') as f:
    content = f.read()

usings = '''using Content.Shared.Hands.EntitySystems;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
'''
if 'using Content.Shared.Hands.EntitySystems;' not in content:
    content = content.replace('using Content.Shared.Damage;', usings + 'using Content.Shared.Damage;')

dep = '''    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;'''
if 'SharedHandsSystem _hands' not in content:
    content = content.replace('    [Dependency] private readonly SharedPopupSystem _popup = default!;', dep)

init = '''        SubscribeLocalEvent<TeethComponent, DamageExamineEvent>(OnDamageExamine);
        
        SubscribeLocalEvent<SurgeryStepTeethRemoveComponent, SurgeryStepEvent>(OnTeethRemove);
        SubscribeLocalEvent<SurgeryStepTeethInsertComponent, SurgeryStepEvent>(OnTeethInsert);
        SubscribeLocalEvent<SurgeryStepTeethImplantComponent, SurgeryStepEvent>(OnTeethImplant);

        SubscribeLocalEvent<SurgeryStepTeethRemoveComponent, SurgeryStepCompleteCheckEvent>(OnTeethRemoveCheck);
        SubscribeLocalEvent<SurgeryStepTeethInsertComponent, SurgeryStepCompleteCheckEvent>(OnTeethInsertCheck);
        SubscribeLocalEvent<SurgeryStepTeethImplantComponent, SurgeryStepCompleteCheckEvent>(OnTeethImplantCheck);'''
if 'SurgeryStepTeethRemoveComponent' not in content:
    content = content.replace('        SubscribeLocalEvent<TeethComponent, DamageExamineEvent>(OnDamageExamine);', init)

methods = '''
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
            _popup.PopupEntity("Вы успешно вырвали зуб.", args.User, args.User);
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
            _popup.PopupEntity("Вы успешно вставили зуб.", args.User, args.User);
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
'''
if 'OnTeethRemoveCheck' not in content:
    content = content.replace('    private void OnDamageExamine', methods.strip() + '\n\n    private void OnDamageExamine')

# Update AccentGet
accent = '''    private void OnAccentGet(EntityUid uid, TeethComponent component, ref AccentGetEvent args)
    {
        if (component.Implants >= 2)
            return;

        if (component.CurrentTeeth >= component.MaxTeeth - 4)
            return;

        args.Message = Accentuate(args.Message);
    }'''
content = re.sub(r'    private void OnAccentGet.*?    }', accent, content, flags=re.DOTALL)

with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'w', encoding='utf-8') as f:
    f.write(content)
