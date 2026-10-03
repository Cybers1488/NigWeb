# -*- coding: utf-8 -*-
with open('Content.Shared/_NigWeb/Teeth/TeethComponent.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('[RegisterComponent, NetworkedComponent]', '[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]')
content = content.replace('[DataField("currentTeeth")]', '[DataField("currentTeeth"), AutoNetworkedField]')
content = content.replace('[DataField]\n    public int Implants', '[DataField, AutoNetworkedField]\n    public int Implants')

with open('Content.Shared/_NigWeb/Teeth/TeethComponent.cs', 'w', encoding='utf-8') as f:
    f.write(content)
