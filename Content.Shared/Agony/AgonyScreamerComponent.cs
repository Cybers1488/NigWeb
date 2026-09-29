using Robust.Shared.GameStates;

namespace Content.Shared.Agony;

[RegisterComponent]
public sealed partial class AgonyScreamerComponent : Component
{
    [DataField]
    public TimeSpan NextScream;

    [DataField]
    public TimeSpan ScreamCooldown = TimeSpan.FromSeconds(5);
}
