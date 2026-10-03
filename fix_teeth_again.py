# -*- coding: utf-8 -*-
import re

# 1. Modify SurgeryComponent
with open('Content.Shared/_Shitmed/Surgery/SurgeryComponent.cs', 'r', encoding='utf-8') as f:
    comp = f.read()
if 'RequireSequential' not in comp:
    comp = comp.replace('public List<EntProtoId> Steps = new();', 'public List<EntProtoId> Steps = new();\n\n    [DataField, AutoNetworkedField]\n    public bool RequireSequential = true;')
    with open('Content.Shared/_Shitmed/Surgery/SurgeryComponent.cs', 'w', encoding='utf-8') as f:
        f.write(comp)

# 2. Modify PreviousStepsComplete
with open('Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.Steps.cs', 'r', encoding='utf-8') as f:
    sys = f.read()

orig_func = '''    private bool PreviousStepsComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId step, EntityUid user)
    {
        var ev = new SurgeryIgnorePreviousStepsEvent();
        RaiseLocalEvent(user, ev);
        if (ev.Handled)
            return true;'''

new_func = '''    private bool PreviousStepsComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId step, EntityUid user)
    {
        if (!surgery.Comp.RequireSequential)
            return true;

        var ev = new SurgeryIgnorePreviousStepsEvent();
        RaiseLocalEvent(user, ev);
        if (ev.Handled)
            return true;'''

sys = sys.replace(orig_func, new_func)

with open('Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.Steps.cs', 'w', encoding='utf-8') as f:
    f.write(sys)
