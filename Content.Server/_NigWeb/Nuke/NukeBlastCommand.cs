// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._NigWeb.Nuke;

[AdminCommand(AdminFlags.Fun)]
public sealed class NukeBlastCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "nukeblast";
    public string Description => "Broadcasts the Hotline Miami nuclear explosion cinematic to all players.";
    public string Help => "nukeblast";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var netCoords = NetCoordinates.Invalid;
        if (shell.Player?.AttachedEntity != null)
        {
            var xform = _entManager.GetComponent<TransformComponent>(shell.Player.AttachedEntity.Value);
            netCoords = _entManager.GetNetCoordinates(xform.Coordinates);
        }

        var sys = _entManager.EntitySysManager.GetEntitySystem<HotlineNukeServerSystem>();
        sys.BroadcastDetonation(netCoords);
        shell.WriteLine("Broadcasted Hotline Miami nuclear blast to all players.");
    }
}
