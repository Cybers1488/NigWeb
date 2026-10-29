// SPDX-License-Identifier: MIT
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Altar;

/// <summary>
/// Attached to maps when the Lady of the Chalices completes her Stage 5 ascension.
/// Indicates that reality on this map has collapsed into pure Black, White, and Crimson Red.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LadyAscensionMapComponent : Component
{
    /// <summary>
    /// When the skin was flayed and ascension started.
    /// Used for smooth intensity fade-in.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StartTime = TimeSpan.Zero;

    /// <summary>
    /// Whether the visual ascension effect is actively rendering.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = true;
}
