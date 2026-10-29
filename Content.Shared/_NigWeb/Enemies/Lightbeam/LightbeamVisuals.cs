// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Enemies.Lightbeam;

[Serializable, NetSerializable]
public enum LightbeamVisuals : byte
{
    State,
    FacingLeft
}

[Serializable, NetSerializable]
public enum LightbeamState : byte
{
    Idle,
    Activating,
    Firing,
    Recovery,
    Dying
}
