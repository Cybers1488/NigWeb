using System.IO;
using System.Linq;
using System.Collections.Generic;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;
using Content.Shared.Administration;

namespace Content.Server._NigWeb.Commands;

[AnyCommand]
public sealed class ImportDmmCommand : IConsoleCommand
{
    public string Command => "import_dmm";
    public string Description => "Imports a DMM file to a new grid on the current map";
    public string Help => "import_dmm <filepath> <z_level>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine("Need filepath and z_level");
            return;
        }

        var path = args[0];
        if (!int.TryParse(args[1], out var targetZ))
        {
            shell.WriteLine("Invalid Z level");
            return;
        }

        if (!File.Exists(path))
        {
            shell.WriteLine($"File not found: {path}");
            return;
        }

        var content = File.ReadAllLines(path);
        var defs = new Dictionary<string, string[]>();
        
        int i = 0;
        while (i < content.Length)
        {
            var line = content[i].Trim();
            if (line.StartsWith('\"'))
            {
                var parts = line.Split(" = (");
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim('\"');
                    var vals = parts[1].TrimEnd(')').Split(',');
                    for (int j = 0; j < vals.Length; j++)
                        vals[j] = vals[j].Trim();
                    defs[key] = vals;
                }
            }
            else if (line.StartsWith("(1,1," + targetZ + ") = {\""))
            {
                break;
            }
            i++;
        }

        if (i >= content.Length)
        {
            shell.WriteLine($"Could not find Z={targetZ}");
            return;
        }

        i++;
        if (content[i].Trim() == "(1,1," + targetZ + ") = {\"") i++; 

        var mapData = new List<string>();
        while (i < content.Length && !content[i].StartsWith("\"}"))
        {
            mapData.Add(content[i]);
            i++;
        }

        if (mapData.Count == 0)
        {
            shell.WriteLine("Map data empty");
            return;
        }

        int keyLen = defs.Keys.First().Length;
        int height = mapData.Count;
        int width = mapData[0].Length / keyLen;

        var entManager = IoCManager.Resolve<IEntityManager>();
        var mapManager = IoCManager.Resolve<IMapManager>();
        var tileDefManager = IoCManager.Resolve<ITileDefinitionManager>();
        
        if (shell.Player?.AttachedEntity == null)
        {
            shell.WriteLine("You must have an attached entity to spawn a map");
            return;
        }
        
        var transform = entManager.GetComponent<TransformComponent>(shell.Player.AttachedEntity.Value);
        var mapId = transform.MapID;
        var mapSystem = entManager.System<SharedMapSystem>();
        
        MapGridComponent grid;
        Vector2i offset = Vector2i.Zero;
        
        if (transform.GridUid != null && entManager.TryGetComponent<MapGridComponent>(transform.GridUid.Value, out var existingGrid))
        {
            grid = existingGrid;
            offset = mapSystem.LocalToTile(transform.GridUid.Value, grid, transform.Coordinates);
            shell.WriteLine($"Attaching fortress to existing planet grid {transform.GridUid.Value} at offset {offset}");
        }
        else
        {
            grid = mapManager.CreateGrid(mapId);
            var xformSystem = entManager.System<SharedTransformSystem>();
            xformSystem.SetWorldPosition(grid.Owner, xformSystem.GetWorldPosition(transform));
            shell.WriteLine($"Created new floating grid {grid.Owner}");
        }
        
        var tileRock = tileDefManager["NigWebFloorPlanetRock"];
        var tileWood = tileDefManager["FloorWood"];
        var tileSteel = tileDefManager["FloorSteel"]; // basic SS14 floor
        
        for (int y = 0; y < height; y++)
        {
            int ss14Y = height - 1 - y;
            
            string row = mapData[y];
            for (int x = 0; x < width; x++)
            {
                string key = row.Substring(x * keyLen, keyLen);
                if (!defs.TryGetValue(key, out var vals)) continue;

                // Check if this is just cave rock that we want to ignore
                bool isCave = false;
                foreach (var val in vals)
                {
                    string baseVal = val.Split('{')[0].Trim('\"');
                    if (baseVal.Contains("wall/r_wall/cave") || 
                        baseVal.Contains("floor/plating/dirt") ||
                        baseVal.Contains("/turf/space") ||
                        baseVal.Contains("/turf/simulated/floor/open"))
                    {
                        isCave = true;
                        break; // Skip rendering this tile entirely!
                    }
                }

                if (isCave) continue;

                var pos = new Vector2i(x, ss14Y) + offset;
                bool hasFloor = false;
                
                foreach (var val in vals)
                {
                    string baseVal = val.Split('{')[0].Trim('\"');
                    
                    // Floors
                    if (baseVal.Contains("/turf/simulated/floor/wood"))
                    {
                        grid.SetTile(pos, new Tile(tileWood.TileId));
                        hasFloor = true;
                    }
                    else if (baseVal.Contains("/turf/simulated/floor/lifeweb"))
                    {
                        // Use basic SS14 floor so it stands out inside the fortress!
                        grid.SetTile(pos, new Tile(tileSteel.TileId));
                        hasFloor = true;
                    }
                    else if (baseVal.Contains("/turf/simulated/floor") || baseVal.Contains("/turf/simulated/wall"))
                    {
                        if (!hasFloor)
                        {
                            grid.SetTile(pos, new Tile(tileRock.TileId)); // default floor for walls
                            hasFloor = true;
                        }
                    }
                    
                    // Walls
                    if (baseVal.Contains("/turf/simulated/wall/stone"))
                    {
                        entManager.SpawnEntity("WallBrick", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.Contains("/turf/simulated/wall/r_wall") && !baseVal.Contains("cave"))
                    {
                        entManager.SpawnEntity("WallReinforced", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.Contains("/turf/simulated/wall/wood") || baseVal.Contains("/turf/simulated/wall/woodwall"))
                    {
                        entManager.SpawnEntity("WallSolid", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.StartsWith("/turf/simulated/wall") && !baseVal.Contains("cave"))
                    {
                        // Any other generic wall
                        entManager.SpawnEntity("WallSolid", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.Contains("/obj/structure/window") || baseVal.Contains("/obj/structure/lfwindow"))
                    {
                        entManager.SpawnEntity("Window", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.Contains("/obj/machinery/door/window"))
                    {
                        entManager.SpawnEntity("WindoorSecure", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.Contains("/obj/machinery/door/airlock/security"))
                    {
                        entManager.SpawnEntity("AirlockSecurity", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.Contains("/obj/machinery/door/airlock"))
                    {
                        entManager.SpawnEntity("Airlock", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.Contains("/obj/structure/mineral_door/wood"))
                    {
                        entManager.SpawnEntity("WoodDoor", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.Contains("/obj/structure/mineral_door/metal"))
                    {
                        entManager.SpawnEntity("AirlockMaint", grid.GridTileToLocal(pos));
                    }
                    else if (baseVal.Contains("/obj/machinery/door/poddoor/shutters"))
                    {
                        entManager.SpawnEntity("Airlock", grid.GridTileToLocal(pos)); // just proxy
                    }
                }
            }
        }
        
        shell.WriteLine($"Imported DMM fortress!");
    }
}
