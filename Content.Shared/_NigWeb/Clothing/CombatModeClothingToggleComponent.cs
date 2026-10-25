// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Clothing;

/// <summary>
/// When worn, dynamically swaps the EquippedPrefix of the clothing item based on wearer's combat mode state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CombatModeClothingToggleComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? NormalPrefix = null;

    [DataField, AutoNetworkedField]
    public string? CombatPrefix = "combat";
}
