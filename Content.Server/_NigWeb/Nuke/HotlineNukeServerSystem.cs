// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._NigWeb.Nuke;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._NigWeb.Nuke;

public sealed class HotlineNukeServerSystem : EntitySystem
{
    public void BroadcastDetonation(NetCoordinates coords)
    {
        RaiseNetworkEvent(new NukeDetonationCinematicEvent(coords), Filter.Broadcast());
    }
}
