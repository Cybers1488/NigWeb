// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Melee;

/// <summary>
/// Data payload for a single melee blood splatter impact.
/// </summary>
[Serializable, NetSerializable]
public struct BloodHitData
{
    public NetCoordinates Coordinates;
    public Vector2 Direction;

    public BloodHitData(NetCoordinates coordinates, Vector2 direction)
    {
        Coordinates = coordinates;
        Direction = direction;
    }
}

/// <summary>
/// Sent from the server to clients in PVS range when a melee attack strikes
/// an entity with a bloodstream. Clients spawn transient blood spray VFX.
/// </summary>
[Serializable, NetSerializable]
public sealed class MeleeBloodHitEffectEvent : EntityEventArgs
{
    public List<BloodHitData> Hits;

    public MeleeBloodHitEffectEvent(List<BloodHitData> hits)
    {
        Hits = hits;
    }
}
