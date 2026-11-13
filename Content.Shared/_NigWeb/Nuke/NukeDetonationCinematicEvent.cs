// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Nuke;

/// <summary>
/// Broadcast when a nuclear warhead detonates to trigger the Hotline Miami nuclear explosion cinematic.
/// </summary>
[Serializable, NetSerializable]
public sealed class NukeDetonationCinematicEvent : EntityEventArgs
{
    public NetCoordinates Coordinates;

    public NukeDetonationCinematicEvent(NetCoordinates coordinates)
    {
        Coordinates = coordinates;
    }
}
