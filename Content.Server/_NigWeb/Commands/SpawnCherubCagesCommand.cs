// SPDX-License-Identifier: MIT
using Content.Server.Administration;
using Content.Server._NigWeb.Cherub;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._NigWeb.Commands;

[AdminCommand(AdminFlags.Mapping | AdminFlags.Fun)]
public sealed class SpawnCherubCagesCommand : IConsoleCommand
{
    public string Command => "spawn_cherub_cages";
    public string Description => "Spawns breakable Cherub Cages randomly scattered around (0,0) on the target map.";
    public string Help => "Usage: spawn_cherub_cages [count] [range] [mapId]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var cherubSys = entMan.System<CherubCageSystem>();
        var mapMan = IoCManager.Resolve<IMapManager>();

        int count = 15;
        float range = 100f;
        MapId mapId = MapId.Nullspace;

        if (args.Length >= 1 && int.TryParse(args[0], out var c))
            count = c;

        if (args.Length >= 2 && float.TryParse(args[1], out var r))
            range = r;

        if (args.Length >= 3 && int.TryParse(args[2], out var m))
            mapId = new MapId(m);
        else if (shell.Player?.AttachedEntity is { } attached)
            mapId = entMan.GetComponent<TransformComponent>(attached).MapID;
        else
        {
            foreach (var id in mapMan.GetAllMapIds())
            {
                if (id != MapId.Nullspace)
                {
                    mapId = id;
                    break;
                }
            }
        }

        if (mapId == MapId.Nullspace)
        {
            shell.WriteLine("No valid MapId found to spawn cages.");
            return;
        }

        int spawned = cherubSys.SpawnCagesOnMap(mapId, count, range);
        shell.WriteLine($"Successfully scattered {spawned} Cherub Cages on Map {mapId} within radius {range}.");
    }
}
