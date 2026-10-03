# -*- coding: utf-8 -*-
with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'r', encoding='utf-8') as f:
    content = f.read()

import re

# Insert check: only if implants == 0
old_insert = r'if \(TryComp<TeethComponent>\(args\.Body, out var teeth\) && teeth\.CurrentTeeth < teeth\.MaxTeeth\)'
new_insert = r'if (TryComp<TeethComponent>(args.Body, out var teeth) && teeth.CurrentTeeth < teeth.MaxTeeth && teeth.Implants == 0)'
content = re.sub(old_insert, new_insert, content)

with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'w', encoding='utf-8') as f:
    f.write(content)

with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'r', encoding='utf-8') as f:
    bui = f.read()

old_bui = r'name = \$\"Рот \(Осталось зубов: \{teethComp\.CurrentTeeth\}\)\";'
new_bui = r'name = teethComp.Implants > 0 ? "Рот (Имплантирован)" : $"Рот (Осталось зубов: {teethComp.CurrentTeeth})";'
bui = re.sub(old_bui, new_bui, bui)

with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'w', encoding='utf-8') as f:
    f.write(bui)

print("Done!")
