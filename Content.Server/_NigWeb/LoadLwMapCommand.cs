using System.Numerics;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server._NigWeb;

[AdminCommand(AdminFlags.Mapping)]
public sealed class LoadLwMapFullCommand : IConsoleCommand
{
    public string Command => "load_lw_map_full";
    public string Description => "Loads ALL Lifeweb Z-levels into one map horizontally offset.";
    public string Help => "load_lw_map_full <base_path_without_Z1.json>";

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine("Need base path. E.g. Z:/fork/NigWeb/");
            return;
        }

        var basePath = args[0];
        
        var mapId = _mapManager.CreateMap();
        
        var caveFloorTileId = _tileDefManager["FloorDarkStone"].TileId;
        var stationFloorTileId = _tileDefManager["FloorSteel"].TileId;
        
        var caveFloorTile = new Tile(caveFloorTileId);
        var stationFloorTile = new Tile(stationFloorTileId);

        for (int z = 1; z <= 6; z++)
        {
            string path = basePath + $"castle_Z{z}.json";
            if (!File.Exists(path))
            {
                shell.WriteLine($"Missing {path}");
                continue;
            }

            string jsonContent = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            var dataObj = root.GetProperty("data");

            var grid = _mapManager.CreateGrid(mapId);
            var gridUid = grid.Owner;

            float xOffset = (z - 1) * 1000f;
            _entityManager.GetComponent<TransformComponent>(gridUid).LocalPosition = new Vector2(xOffset, 0);

            var tilesToSet = new List<(Vector2i, Tile)>();
            var entitiesToSpawn = new List<(Vector2i, string)>();

            foreach (var property in dataObj.EnumerateObject())
            {
                var coords = property.Name.Split(',');
                int x = int.Parse(coords[0]);
                int y = int.Parse(coords[1]);
                var tileType = property.Value.GetString();

                var pos = new Vector2i(x, y);

                if (tileType == "CF") // Cave Floor
                {
                    tilesToSet.Add((pos, caveFloorTile));
                }
                else if (tileType == "SF") // Station Floor
                {
                    tilesToSet.Add((pos, stationFloorTile));
                }
                else if (tileType == "CW") // Cave Wall
                {
                    tilesToSet.Add((pos, caveFloorTile));
                    entitiesToSpawn.Add((pos, "WallCaveSmyak"));
                }
                else if (tileType == "MW1") // ClassicWallMetalic
                {
                    tilesToSet.Add((pos, stationFloorTile));
                    entitiesToSpawn.Add((pos, "ClassicWallMetalic"));
                }
                else if (tileType == "MW2") // ClassicWallMetal
                {
                    tilesToSet.Add((pos, stationFloorTile));
                    entitiesToSpawn.Add((pos, "ClassicWallMetal"));
                }
                else if (tileType == "STW") // Stone Wall
                {
                    tilesToSet.Add((pos, stationFloorTile));
                    entitiesToSpawn.Add((pos, "WallStone"));
                }
                else if (tileType == "OP") // Open Chasm
                {
                    entitiesToSpawn.Add((pos, "LifewebChasm"));
                }
                else if (tileType == "LAD") // Ladder
                {
                    tilesToSet.Add((pos, stationFloorTile));
                    entitiesToSpawn.Add((pos, "LifewebStairs"));
                }
            }

            grid.SetTiles(tilesToSet);
            foreach (var ent in entitiesToSpawn)
            {
                var coords = new EntityCoordinates(gridUid, ent.Item1.X + 0.5f, ent.Item1.Y + 0.5f);
                _entityManager.SpawnEntity(ent.Item2, coords);
            }
            shell.WriteLine($"Generated Z{z} at X-Offset {xOffset}.");
        }

        shell.WriteLine($"All 6 Z-levels loaded into MapId {mapId}.");
        shell.WriteLine($"Use 'tp 0 0 {mapId}' to visit Z1.");
    }
}

