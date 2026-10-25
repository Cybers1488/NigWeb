// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._NigWeb.BloodSplatter;

/// <summary>
/// Attached to items and clothing that have been stained with blood.
/// Uses a procedural pixel-art shader that strictly masks blood to the item's visible pixels,
/// ensuring zero blood pixels ever spawn in empty or transparent areas.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BloodyItemComponent : Component
{
    /// <summary>
    /// Saturation level of the blood stain from 0.0 to 1.0.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BloodLevel = 0.0f;

    /// <summary>
    /// Random seed for unique splatter placement on each entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BloodSeed = 0.0f;

    /// <summary>
    /// Color of the blood on this item. Defaults to standard deep dark blood #780000.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color BloodColor = Color.FromHex("#780000");
}
