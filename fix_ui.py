# -*- coding: utf-8 -*-
import re
with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Remove the custom injection
injection_regex = r'// NigWeb: Teeth UI Custom Injection.*?HSeparator \{ Margin = new Robust\.Shared\.Maths\.Thickness\(0, 0, 0, 5\) \}\);\s*\}\s*\}'
content = re.sub(injection_regex, '', content, flags=re.DOTALL)

# 2. Modify the name dynamically
old_name_code = '''            var name = _entities.GetComponent<MetaDataComponent>(surgery).EntityName;
            surgeries.Add(((surgery, surgeryComp), surgeryId, name));'''
new_name_code = '''            var name = _entities.GetComponent<MetaDataComponent>(surgery).EntityName;
            
            // NigWeb: Dynamic teeth count in surgery name
            if (surgeryId.Id == "SurgeryMouth" && _entities.TryGetComponent(Owner, out Content.Shared._NigWeb.Teeth.TeethComponent? teethComp))
            {
                name = $"Рот (Осталось зубов: {teethComp.CurrentTeeth})";
            }

            surgeries.Add(((surgery, surgeryComp), surgeryId, name));'''

content = content.replace(old_name_code, new_name_code)

with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'w', encoding='utf-8') as f:
    f.write(content)
