# -*- coding: utf-8 -*-
import re
with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'r', encoding='utf-8') as f:
    content = f.read()

def replacer(match):
    return """
    private void OnTeethRemoveCheck(EntityUid uid, SurgeryStepTeethRemoveComponent comp, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp<TeethComponent>(args.Body, out var teeth) && teeth.CurrentTeeth > 0)
            args.Cancelled = true; // Not complete, so we can do it
    }

    private void OnTeethInsertCheck(EntityUid uid, SurgeryStepTeethInsertComponent comp, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp<TeethComponent>(args.Body, out var teeth) && teeth.CurrentTeeth < teeth.MaxTeeth)
            args.Cancelled = true;
    }

    private void OnTeethImplantCheck(EntityUid uid, SurgeryStepTeethImplantComponent comp, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp<TeethComponent>(args.Body, out var teeth) && teeth.CurrentTeeth == 0 && teeth.Implants < 2)
            args.Cancelled = true;
    }
"""

content = re.sub(r'\s*private void OnTeethRemoveCheck.*?args\.Cancelled = true;\s*\}\s*private void OnTeethImplantCheck.*?args\.Cancelled = true;\s*\}', replacer, content, flags=re.DOTALL)

with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'w', encoding='utf-8') as f:
    f.write(content)
