// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Religion;

/// <summary>
/// Marks an entity as a devout follower of the Flayed Lady and member of Ordo Carnis Truncatae.
/// Members do not mockingly laugh upon dismemberment, but rather offer prayer to the Lady.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LadyFaithfulComponent : Component
{
}
