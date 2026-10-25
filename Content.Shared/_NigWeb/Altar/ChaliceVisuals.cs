// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Altar;

[Serializable, NetSerializable]
public enum ChaliceVisuals : byte
{
    FillState,
}

[Serializable, NetSerializable]
public enum ChaliceFillState : byte
{
    Empty,
    Quarter,
    Half,
    Full,
    Broken,
}
