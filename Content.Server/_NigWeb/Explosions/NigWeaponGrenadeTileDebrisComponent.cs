// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._NigWeb.Explosions;

/// <summary>
/// Attached to grenades and explosives to slice and blast physical tile debris pixels
/// from the floor upon detonation.
/// </summary>
[RegisterComponent]
public sealed partial class NigWeaponGrenadeTileDebrisComponent : Component
{
    /// <summary>
    /// Trigger keys that activate the debris blast. Defaults to ["timer"] so it ONLY fires on detonation.
    /// </summary>
    [DataField("keysIn")]
    public HashSet<string> KeysIn = new() { "timer" };

    /// <summary>
    /// Total number of physical ground tile debris fragments ejected into the air.
    /// </summary>
    [DataField("count")]
    public int DebrisCount = 650;

    /// <summary>
    /// Maximum blast radius for physical debris dispersion (meters).
    /// </summary>
    [DataField("radius")]
    public float Radius = 7.0f;
}
