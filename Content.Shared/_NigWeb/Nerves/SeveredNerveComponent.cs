using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Nerves;

/// <summary>
/// Attached to a BodyPart (e.g., Arm, Leg, Torso) when its nerve is severed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SeveredNerveComponent : Component
{
    // The next time the player will drop an item from the arm
    public TimeSpan NextDropTime = TimeSpan.Zero;
    
    // The next time the player will trip if it's a leg
    public TimeSpan NextTripTime = TimeSpan.Zero;
}
