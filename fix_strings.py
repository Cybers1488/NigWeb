# -*- coding: utf-8 -*-
import re

with open('Content.Shared/_Shitmed/Surgery/Traumas/Systems/TraumaSystem.Bones.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = re.sub(r'_popup\.PopupEntity\(".*?\.", bodyComp\.Body\.Value, bodyComp\.Body\.Value, PopupType\.Small\);', 
                 '_popup.PopupEntity("Ваша челюсть восстановлена.", bodyComp.Body.Value, bodyComp.Body.Value, PopupType.Small);', content)

content = re.sub(r'_popup\.PopupEntity\(".*?\!", bodyComp\.Body\.Value, bodyComp\.Body\.Value, PopupType\.LargeCaution\);', 
                 '_popup.PopupEntity("Вам ломает челюсть!", bodyComp.Body.Value, bodyComp.Body.Value, PopupType.LargeCaution);', content)

with open('Content.Shared/_Shitmed/Surgery/Traumas/Systems/TraumaSystem.Bones.cs', 'w', encoding='utf-8') as f:
    f.write(content)

with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'r', encoding='utf-8') as f:
    bui = f.read()
bui = re.sub(r'\[bold\]\[color=orange\].*?\{teethComp\.CurrentTeeth\}\)\[/color\]\[/bold\]', 
             r'[bold][color=orange]Рот (Осталось зубов: {teethComp.CurrentTeeth})[/color][/bold]', bui)
with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'w', encoding='utf-8') as f:
    f.write(bui)

with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'r', encoding='utf-8') as f:
    sys = f.read()
sys = re.sub(r'_popup\.PopupEntity\(".*?\.", args\.User, args\.User\);', 
             r'_popup.PopupEntity("Действие успешно завершено.", args.User, args.User);', sys)
with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'w', encoding='utf-8') as f:
    f.write(sys)
