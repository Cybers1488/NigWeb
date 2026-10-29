// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Enemies.SpiritSpawner;

[Serializable, NetSerializable]
public enum FlyingFlowerVisuals : byte
{
    State,
    FacingLeft
}

[Serializable, NetSerializable]
public enum FlyingFlowerState : byte
{
    Idle,
    Anticipating,
    Exploding,
    Dying
}
