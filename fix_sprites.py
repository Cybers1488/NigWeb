# -*- coding: utf-8 -*-
import re

with open('Resources/Prototypes/_NigWeb/Entities/Objects/tooth.yml', 'r', encoding='utf-8') as f:
    content = f.read()
    
content = content.replace('_NigWeb/Objects/teeth.rsi', '_NigWeb/Teeth/teeth.rsi')

with open('Resources/Prototypes/_NigWeb/Entities/Objects/tooth.yml', 'w', encoding='utf-8') as f:
    f.write(content)

with open('Resources/Prototypes/_NigWeb/Teeth/teeth_surgery.yml', 'r', encoding='utf-8') as f:
    surg = f.read()

surg = re.sub(r'id: WoodenToothImplant.*?state: tooth1', r'id: WoodenToothImplant\n  parent: BaseItem\n  name: "Деревянный зубной имплант"\n  description: "Кусок дерева, выточенный под форму челюсти."\n  components:\n  - type: Sprite\n    sprite: _NigWeb/Teeth/teeth.rsi\n    state: teeth2', surg, flags=re.DOTALL)
surg = re.sub(r'id: SteelToothImplant.*?state: gold_teeth1', r'id: SteelToothImplant\n  parent: BaseItem\n  name: "Стальной зубной имплант"\n  description: "Прочный металлический протез для зубов."\n  components:\n  - type: Sprite\n    sprite: _NigWeb/Teeth/teeth.rsi\n    state: teeth3', surg, flags=re.DOTALL)

with open('Resources/Prototypes/_NigWeb/Teeth/teeth_surgery.yml', 'w', encoding='utf-8') as f:
    f.write(surg)

print("Sprites updated!")
