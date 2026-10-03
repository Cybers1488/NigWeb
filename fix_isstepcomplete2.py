# -*- coding: utf-8 -*-
with open('Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.Steps.cs', 'r', encoding='utf-8') as f:
    content = f.read()
    
new_method = """    public bool IsStepComplete(EntityUid body, EntityUid part, EntityUid stepEnt, EntityUid surgery)
    {
        var ev = new SurgeryStepCompleteCheckEvent(body, part, surgery);
        RaiseLocalEvent(stepEnt, ref ev);
        return !ev.Cancelled;
    }

    public bool IsStepComplete(EntityUid body, EntityUid part, EntProtoId step, EntityUid surgery)"""

content = content.replace('    public bool IsStepComplete(EntityUid body, EntityUid part, EntProtoId step, EntityUid surgery)', new_method)

with open('Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.Steps.cs', 'w', encoding='utf-8') as f:
    f.write(content)
