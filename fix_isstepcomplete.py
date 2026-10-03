# -*- coding: utf-8 -*-
with open('Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.Steps.cs', 'r', encoding='utf-8') as f:
    content = f.read()
    
content = content.replace('private bool IsStepComplete(', 'public bool IsStepComplete(')

with open('Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.Steps.cs', 'w', encoding='utf-8') as f:
    f.write(content)
