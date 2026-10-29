// SPDX-License-Identifier: MIT
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.BloodTears;

/// <summary>
/// Tracks the cooldown for the blood tears emote.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodTearsCooldownComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan NextEmoteTime = TimeSpan.Zero;
}
