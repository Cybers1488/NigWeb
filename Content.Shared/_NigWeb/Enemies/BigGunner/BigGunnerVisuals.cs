// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Enemies.BigGunner;

[Serializable, NetSerializable]
public enum BigGunnerVisuals : byte
{
    State,
    FacingLeft
}

[Serializable, NetSerializable]
public enum BigGunnerState : byte
{
    Idle,
    RangedAnticipating,
    RangedAttacking,
    MeleeAnticipating,
    MeleeAttacking,
    Stunned,
    Dying
}
