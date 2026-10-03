# -*- coding: utf-8 -*-
with open('Resources/Prototypes/_NigWeb/Teeth/teeth_surgery.yml', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('Objects/Materials/materials.rsi', '_NigWeb/Teeth/teeth.rsi')
content = content.replace('state: wood', 'state: tooth1')
content = content.replace('state: steel', 'state: gold_teeth1')

with open('Resources/Prototypes/_NigWeb/Teeth/teeth_surgery.yml', 'w', encoding='utf-8') as f:
    f.write(content)
