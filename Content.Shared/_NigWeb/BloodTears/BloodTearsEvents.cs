// SPDX-License-Identifier: MIT
using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.BloodTears;

/// <summary>
/// Sent from the server to all nearby clients to play the blood tears wipe emote
/// on the entity that triggered it.
/// </summary>
[Serializable, NetSerializable]
public sealed class BloodTearsEmoteEvent : EntityEventArgs
{
    public NetEntity Performer;

    public BloodTearsEmoteEvent(NetEntity performer)
    {
        Performer = performer;
    }
}
