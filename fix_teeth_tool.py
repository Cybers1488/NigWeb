# -*- coding: utf-8 -*-
with open('Content.Shared/_NigWeb/Teeth/TeethSurgeryComponents.cs', 'r', encoding='utf-8') as f:
    content = f.read()

import re

# Add ISurgeryToolComponent to ToothImplantComponent
impl = """[RegisterComponent, NetworkedComponent]
public sealed partial class ToothImplantComponent : Component, Content.Shared._Shitmed.Medical.Surgery.Tools.ISurgeryToolComponent
{
    public string ToolName => "зубной имплант";
    [DataField] public bool? Used { get; set; } = null;
    [DataField] public float Speed { get; set; } = 1f;
}"""
content = re.sub(r'\[RegisterComponent, NetworkedComponent\]\s*public sealed partial class ToothImplantComponent : Component \{\}', impl, content)

# Add ISurgeryToolComponent to ToothItemComponent
item = """[RegisterComponent, NetworkedComponent]
public sealed partial class ToothItemComponent : Component, Content.Shared._Shitmed.Medical.Surgery.Tools.ISurgeryToolComponent
{
    public string ToolName => "зуб";
    [DataField] public bool? Used { get; set; } = null;
    [DataField] public float Speed { get; set; } = 1f;
}"""
content = re.sub(r'\[RegisterComponent, NetworkedComponent\]\s*public sealed partial class ToothItemComponent : Component \{\}', item, content)

with open('Content.Shared/_NigWeb/Teeth/TeethSurgeryComponents.cs', 'w', encoding='utf-8') as f:
    f.write(content)
