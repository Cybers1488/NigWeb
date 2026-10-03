import re

with open('Z:/fork/NigWeb/Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Add usings
usings = '''using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
'''
content = content.replace('using Content.Shared.Popups;', usings + 'using Content.Shared.Popups;')

# Add SubscribeLocalEvent
subs = '''
        SubscribeLocalEvent<TeethComponent, AttemptIngestEvent>(OnAttemptIngestTeeth, before: new[] { typeof(IngestionSystem) });
'''
content = content.replace('SubscribeLocalEvent<TeethComponent, DamageExamineEvent>(OnDamageExamine);', 'SubscribeLocalEvent<TeethComponent, DamageExamineEvent>(OnDamageExamine);' + subs)

# Add method
method = '''
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
'''
content = content.replace('private void OnDamageExamine(', method + '\n    private void OnDamageExamine(')

with open('Z:/fork/NigWeb/Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Updated TeethSystem.cs!')
