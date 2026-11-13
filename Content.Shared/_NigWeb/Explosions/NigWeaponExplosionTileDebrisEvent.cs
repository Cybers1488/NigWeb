// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Explosions;

/// <summary>
/// Broadcast from the server to clients in PVS range when an explosive grenade detonates,
/// carrying the epicenter coordinates and the underlying tile type for realistic pixel debris slicing and scattering.
/// </summary>
[Serializable, NetSerializable]
public sealed class NigWeaponExplosionTileDebrisEvent : EntityEventArgs
{
    public NetCoordinates Coordinates;
    public string TileId;
    public int Count;
    public float Radius;

    public NigWeaponExplosionTileDebrisEvent(NetCoordinates coordinates, string tileId, int count, float radius)
    {
        Coordinates = coordinates;
        TileId = tileId;
        Count = count;
        Radius = radius;
    }
}
