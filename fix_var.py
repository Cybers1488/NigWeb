# -*- coding: utf-8 -*-
with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('var surgeryComp = _entities.GetComponent<SurgeryComponent>(_surgery.Value.Ent);', 'var surgeryComponent = _entities.GetComponent<SurgeryComponent>(_surgery.Value.Ent);')
content = content.replace('if (!surgeryComp.RequireSequential)', 'if (!surgeryComponent.RequireSequential)')

with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'w', encoding='utf-8') as f:
    f.write(content)
