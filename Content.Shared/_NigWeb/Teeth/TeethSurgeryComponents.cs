using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Teeth;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepTeethRemoveComponent : Component {}

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepTeethInsertComponent : Component {}

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepTeethImplantComponent : Component {}

[RegisterComponent, NetworkedComponent]
public sealed partial class ToothImplantComponent : Component, Content.Shared._Shitmed.Medical.Surgery.Tools.ISurgeryToolComponent
{
    public string ToolName => "зубной имплант";
    [DataField] public bool? Used { get; set; } = null;
    [DataField] public float Speed { get; set; } = 1f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ToothItemComponent : Component, Content.Shared._Shitmed.Medical.Surgery.Tools.ISurgeryToolComponent
{
    public string ToolName => "зуб";
    [DataField] public bool? Used { get; set; } = null;
    [DataField] public float Speed { get; set; } = 1f;
}
