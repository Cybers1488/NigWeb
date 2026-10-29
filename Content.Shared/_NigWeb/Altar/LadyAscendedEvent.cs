// SPDX-License-Identifier: MIT
using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Altar;

/// <summary>
/// Broadcast across the network when the Lady of the Chalices achieves Stage 5 ascension.
/// Clients receive this to play the dramatic world-altering audio and screen pulse.
/// </summary>
[Serializable, NetSerializable]
public sealed class LadyAscendedEvent : EntityEventArgs
{
    public NetEntity Altar;

    public LadyAscendedEvent(NetEntity altar)
    {
        Altar = altar;
    }
}
