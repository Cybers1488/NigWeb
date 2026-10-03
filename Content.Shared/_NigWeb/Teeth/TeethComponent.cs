using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._NigWeb.Teeth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TeethComponent : Component
{
    [DataField("maxTeeth")]
    public int MaxTeeth = 32;

    [DataField("currentTeeth"), AutoNetworkedField]
    public int CurrentTeeth = 32;

    [DataField, AutoNetworkedField]
    public int Implants = 0;

    [DataField("dropChancePerBlunt")]
    public float DropChancePerBlunt = 0.02f; // 2% chance per 1 blunt damage. 10 blunt = 20% chance.
    
    [DataField("toothPrototype")]
    public EntProtoId ToothPrototype = "ItemTooth";
}
