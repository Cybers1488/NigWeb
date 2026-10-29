// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Enemies.SpiritSpawner;

[Serializable, NetSerializable]
public enum SpiritSpawnerVisuals : byte
{
    State,
    FacingLeft
}

[Serializable, NetSerializable]
public enum SpiritSpawnerState : byte
{
    Idle,
    SpawnAnticipation,
    Spawning,
    SpawnRecovery,
    AttackAnticipation,
    Attacking,
    AttackRecovery,
    Stunned,
    Dying
}
