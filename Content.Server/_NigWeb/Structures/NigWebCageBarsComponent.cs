using Robust.Shared.Prototypes;

namespace Content.Server._NigWeb.Structures;

[RegisterComponent]
public sealed partial class NigWebCageBarsComponent : Component
{
    [DataField]
    public EntProtoId BrokenPrototype = "NigWebCageBarsBroken";
}
