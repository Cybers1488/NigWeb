using Robust.Shared.GameStates;

namespace Content.Shared.Temperature;

[RegisterComponent, NetworkedComponent]
public sealed partial class ColdShiverComponent : Component
{
    [DataField]
    public TimeSpan NextCheck = TimeSpan.Zero;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(40);

    [DataField]
    public float ShiverChance = 0.30f;
}
