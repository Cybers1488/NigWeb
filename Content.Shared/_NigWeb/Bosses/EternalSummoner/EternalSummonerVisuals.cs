// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Bosses.EternalSummoner;

[Serializable, NetSerializable]
public enum EternalHeadState : byte
{
    ElderIdle,
    ElderToMask,
    MaskIdle,
    MaskToElder,
    MaskToSkull,
    SkullIdle
}

[Serializable, NetSerializable]
public enum EternalSummonerVisuals : byte
{
    VisualState
}

[Serializable, NetSerializable]
public struct EternalSummonerVisualState
{
    public EternalHeadState HeadState;
    public bool IsDead;

    public EternalSummonerVisualState(EternalHeadState headState, bool isDead)
    {
        HeadState = headState;
        IsDead = isDead;
    }
}
