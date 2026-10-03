# -*- coding: utf-8 -*-
with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('if (_system.IsStepComplete(Owner, _part.Value, stepButton.Step, _surgery.Value.Ent))', 'if (_system.IsStepComplete(Owner, _part.Value, surgeryComponent.Steps[i], _surgery.Value.Ent))')

with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'w', encoding='utf-8') as f:
    f.write(content)
