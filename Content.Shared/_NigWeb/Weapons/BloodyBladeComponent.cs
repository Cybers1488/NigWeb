// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodyBladeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int HitsToBloody = 3;

    [DataField, AutoNetworkedField]
    public int CurrentHits = 0;

    [DataField, AutoNetworkedField]
    public bool IsBloody = false;

    [DataField]
    public string HeldPrefix = "blood";
}

[Serializable, NetSerializable]
public enum BloodyBladeVisuals : byte
{
    Bloody
}
