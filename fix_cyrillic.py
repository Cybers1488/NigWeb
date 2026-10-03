# -*- coding: utf-8 -*-
import re
with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = re.sub(r'name = \$\"Р.*?\"\;', 'name = $"Рот (Осталось зубов: {teethComp.CurrentTeeth})";', content)

with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'w', encoding='utf-8') as f:
    f.write(content)
