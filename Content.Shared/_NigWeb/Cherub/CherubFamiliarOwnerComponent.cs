// SPDX-License-Identifier: MIT
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._NigWeb.Cherub;

[RegisterComponent, NetworkedComponent]
public sealed partial class CherubFamiliarOwnerComponent : Component
{
    [DataField("familiar")]
    public EntityUid? Familiar = null;
}
