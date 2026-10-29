// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Enemies.Shockwave;

[Serializable, NetSerializable]
public enum ShockwaveVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum ShockwaveState : byte
{
    Spawn,
    Idle,
    Anticipating,
    Attacking,
    Recovery,
    Dying,
}
