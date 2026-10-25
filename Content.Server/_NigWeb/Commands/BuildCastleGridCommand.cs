using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server._NigWeb.Commands;

[AdminCommand(AdminFlags.Mapping)]
public sealed class BuildCastleGridCommand : IConsoleCommand
{
    public string Command => "build_castle_grid";
    public string Description => "Generates an authentic Lifeweb Ravenheart Castle grid with all sectors, gates, dungeons, and keep.";
    public string Help => "build_castle_grid [save] [mapId]";

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var log = _logManager.GetSawmill("castle_grid");
        log.Info("[BuildCastleGridCommand] Initiating castle grid generation...");

        bool autoSave = false;
        MapId? targetMapId = null;

        foreach (var arg in args)
        {
            if (arg.Equals("save", StringComparison.OrdinalIgnoreCase))
            {
                autoSave = true;
            }
            else if (int.TryParse(arg, out var parsedMapId))
            {
                targetMapId = new MapId(parsedMapId);
            }
        }

        var mapSystem = _entityManager.System<SharedMapSystem>();
        var xformSystem = _entityManager.System<SharedTransformSystem>();

        MapId mapId;
        Vector2 position = Vector2.Zero;

        if (targetMapId != null)
        {
            mapId = targetMapId.Value;
            if (!mapSystem.MapExists(mapId))
            {
                shell.WriteError($"Map {mapId} does not exist. Creating map {mapId}...");
                mapSystem.CreateMap(mapId);
            }
        }
        else if (shell.Player?.AttachedEntity != null &&
                 _entityManager.TryGetComponent<TransformComponent>(shell.Player.AttachedEntity.Value, out var playerXform))
        {
            mapId = playerXform.MapID;
            position = xformSystem.GetWorldPosition(playerXform);
        }
        else
        {
            mapId = _mapManager.CreateMap();
            shell.WriteLine($"Created new map {mapId}.");
        }

        // Create the castle grid
        var grid = _mapManager.CreateGrid(mapId);
        var gridUid = grid.Owner;
        xformSystem.SetWorldPosition(gridUid, position);
        log.Info($"[BuildCastleGridCommand] Created grid {gridUid} on Map {mapId} at {position}.");

        // Resolve tile definitions safely
        var tileStone = GetTile("NigWebFloorStone25", "FloorSteel");
        var tileNewbar = GetTile("NigWebFloorNewbar", "FloorWood");
        var tileSpooky = GetTile("NigWebFloorSpooky", "FloorDarkStone");
        var tileDarkStone = GetTile("FloorDarkStone", "FloorSteel");
        var tileWood = GetTile("FloorWood", "FloorSteel");
        var tileSteel = GetTile("FloorSteel", "FloorSteel");
        var tileRock = GetTile("NigWebFloorPlanetRock", "FloorSteel");

        var tilesToSet = new List<(Vector2i, Tile)>(3600);
        var entitySpawns = new List<(Vector2i, string, float)>();

        void FillTiles(int x1, int y1, int x2, int y2, Tile tile)
        {
            int minX = Math.Min(x1, x2);
            int maxX = Math.Max(x1, x2);
            int minY = Math.Min(y1, y2);
            int maxY = Math.Max(y1, y2);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    tilesToSet.Add((new Vector2i(x, y), tile));
                }
            }
        }

        void Spawn(int x, int y, string proto, float rotDeg = 0f)
        {
            entitySpawns.Add((new Vector2i(x, y), proto, rotDeg));
        }

        void BuildHWall(int x1, int x2, int y, string proto = "NigWebWallStone")
        {
            int minX = Math.Min(x1, x2);
            int maxX = Math.Max(x1, x2);
            for (int x = minX; x <= maxX; x++)
            {
                Spawn(x, y, proto);
            }
        }

        void BuildVWall(int x, int y1, int y2, string proto = "NigWebWallStone")
        {
            int minY = Math.Min(y1, y2);
            int maxY = Math.Max(y1, y2);
            for (int y = minY; y <= maxY; y++)
            {
                Spawn(x, y, proto);
            }
        }

        // ==========================================
        // 1. BASE FLOORING
        // ==========================================
        // 60x60 bedrock / planetary mud surround
        FillTiles(0, 0, 59, 59, tileRock);

        // Castle courtyard and general interior base (stone cobblestone)
        FillTiles(4, 4, 55, 55, tileStone);

        // Specific room floor overrides:
        // Dungeons & Torture
        FillTiles(5, 5, 20, 16, tileSpooky);
        // Garrison Barracks
        FillTiles(5, 18, 20, 25, tileWood);
        // Clinic & Hospital
        FillTiles(38, 5, 54, 15, tileDarkStone);
        // Church of the Creator
        FillTiles(38, 17, 54, 35, tileStone);
        // Tavern "Old Cock"
        FillTiles(5, 27, 20, 37, tileNewbar);
        // Blacksmith Workshop
        FillTiles(36, 27, 44, 37, tileDarkStone);
        // Great Throne Room
        FillTiles(23, 44, 35, 55, tileSpooky);
        // Baron's Bedchamber
        FillTiles(5, 44, 13, 55, tileWood);
        // Baron's Treasury Vault
        FillTiles(14, 45, 20, 55, tileSteel);
        // War Room / Council
        FillTiles(38, 44, 44, 55, tileDarkStone);
        // Meister's Study & Alchemy Lab
        FillTiles(46, 44, 54, 55, tileWood);

        // ==========================================
        // 2. OUTER CURTAIN WALLS & TOWERS
        // ==========================================
        string stoneWall = "NigWebWallStone";
        string darkWall = "NigWebWallDark";
        string woodWall = "NigWebWoodWall";

        // South Outer Wall (y = 4) with 3-tile gate gap at x: 28..30
        BuildHWall(4, 27, 4, stoneWall);
        BuildHWall(31, 55, 4, stoneWall);

        // North Outer Wall (y = 55)
        BuildHWall(4, 55, 55, stoneWall);

        // West Outer Wall (x = 4)
        BuildVWall(4, 4, 55, stoneWall);

        // East Outer Wall (x = 55)
        BuildVWall(55, 4, 55, stoneWall);

        // Corner Towers reinforcement
        // SW Tower (4..8, 4..8)
        BuildHWall(4, 8, 8, stoneWall);
        BuildVWall(8, 4, 8, stoneWall);
        // SE Tower (51..55, 4..8)
        BuildHWall(51, 55, 8, stoneWall);
        BuildVWall(51, 4, 8, stoneWall);
        // NW Tower (4..8, 51..55)
        BuildHWall(4, 8, 51, stoneWall);
        BuildVWall(8, 51, 55, stoneWall);
        // NE Tower (51..55, 51..55)
        BuildHWall(51, 55, 51, stoneWall);
        BuildVWall(51, 51, 55, stoneWall);

        // ==========================================
        // 3. GATEHOUSE & BARBICAN
        // ==========================================
        // Main South Citadel Gate (3x1 animated gate)
        Spawn(28, 4, "NigWebGateDoor");

        // West Gate Tower (x: 22..27, y: 4..10)
        BuildVWall(22, 4, 10, stoneWall);
        BuildHWall(22, 27, 10, stoneWall);
        BuildVWall(27, 4, 10, stoneWall);
        // Arrowslit windows & door
        Spawn(24, 4, "Window");
        Spawn(27, 8, "NigWebDoorWood1");
        Spawn(24, 8, "TableWood");
        Spawn(24, 7, "ChairWood");
        Spawn(25, 9, "TorchWall");

        // East Gate Tower (x: 31..36, y: 4..10)
        BuildVWall(36, 4, 10, stoneWall);
        BuildHWall(31, 36, 10, stoneWall);
        BuildVWall(31, 4, 10, stoneWall);
        // Arrowslit windows & door
        Spawn(34, 4, "Window");
        Spawn(31, 8, "NigWebDoorWood1");
        Spawn(34, 8, "TableWood");
        Spawn(34, 7, "ChairWood");
        Spawn(33, 9, "TorchWall");

        // Barbican Inner Portcullis (3x1 gate)
        Spawn(28, 10, "NigWebCastleGate3x1");
        Spawn(27, 6, "TorchWall");
        Spawn(31, 6, "TorchWall");

        // ==========================================
        // 4. LOWER COURTYARD
        // ==========================================
        // Central well / bonfire decorative center
        Spawn(29, 18, "TableStone");
        Spawn(30, 18, "TableStone");
        Spawn(29, 17, "TorchWall");
        Spawn(30, 17, "TorchWall");

        // Courtyard perimeter wall torches
        Spawn(22, 14, "TorchWall");
        Spawn(36, 14, "TorchWall");
        Spawn(22, 20, "TorchWall");
        Spawn(36, 20, "TorchWall");

        // ==========================================
        // 5. SOUTH-WEST WING: DUNGEON & GARRISON
        // ==========================================
        // East enclosing wall of SW wing
        BuildVWall(21, 4, 26, stoneWall);
        // Dividing wall between Dungeon (y<=17) and Barracks (y>=18)
        BuildHWall(4, 21, 17, stoneWall);

        // --- DUNGEON (x: 4..21, y: 4..17) ---
        // Dungeon Entrance from Courtyard
        Spawn(21, 12, "NigWebMineralDoorX");
        // Dungeon cell wall at y = 10
        BuildHWall(4, 21, 10, darkWall);
        // Cell dividers
        BuildVWall(9, 5, 10, darkWall);
        BuildVWall(14, 5, 10, darkWall);

        // Cell 1 (x: 5..8, y: 5..10)
        Spawn(7, 10, "NigWebCageBars");
        Spawn(8, 10, "NigWebCageBars");
        Spawn(6, 6, "Mattress");
        Spawn(8, 6, "CandleBlackInfinite");

        // Cell 2 (x: 10..13, y: 5..10)
        Spawn(11, 10, "NigWebCageBars");
        Spawn(12, 10, "NigWebCageBars");
        Spawn(11, 6, "Mattress");
        Spawn(12, 6, "CandleBlackInfinite");

        // Cell 3 (x: 15..20, y: 5..10)
        Spawn(16, 10, "NigWebCageBars");
        Spawn(17, 10, "NigWebCageBars");
        Spawn(16, 6, "Mattress");
        Spawn(18, 6, "CandleBlackInfinite");

        // Interrogation & Torture Room (x: 5..13, y: 11..16)
        BuildVWall(13, 11, 16, darkWall);
        Spawn(13, 14, "NigWebMineralDoorX");
        Spawn(9, 15, "TableStone");
        Spawn(9, 14, "ChairRitual");
        Spawn(8, 15, "CandleBlackInfinite");

        // Jailer's Post (x: 14..20, y: 11..16)
        Spawn(17, 15, "TableWood");
        Spawn(17, 14, "ChairWood");
        Spawn(19, 15, "CrateSecgear");
        Spawn(14, 12, "TorchWall");
        Spawn(19, 12, "TorchWall");

        // --- GARRISON BARRACKS (x: 4..21, y: 18..26) ---
        // Courtyard entrance
        Spawn(21, 22, "NigWebDoorWood1");
        // Office partition
        BuildVWall(14, 18, 26, stoneWall);
        Spawn(14, 22, "NigWebDoorWood1");

        // Sleeping bay (x: 5..13, y: 18..26)
        int[] bedYs = { 19, 21, 23, 25 };
        foreach (var by in bedYs)
        {
            Spawn(6, by, "Bed");
            Spawn(8, by, "Bed");
        }
        Spawn(11, 24, "CrateSecgear");
        Spawn(12, 24, "CrateLivestock");
        Spawn(5, 22, "TorchWall");
        Spawn(13, 22, "TorchWall");

        // Captain's Office (x: 15..20, y: 18..26)
        Spawn(17, 21, "TableWood");
        Spawn(17, 20, "ChairWood");
        Spawn(19, 24, "Bookshelf");
        Spawn(18, 25, "TorchWall");

        // ==========================================
        // 6. SOUTH-EAST WING: CLINIC & CHURCH
        // ==========================================
        // West enclosing wall of SE wing
        BuildVWall(37, 4, 36, stoneWall);
        // Dividing wall between Clinic and Church
        BuildHWall(37, 55, 16, stoneWall);

        // --- CLINIC & SURGERY (x: 37..55, y: 4..16) ---
        // Courtyard entrance
        Spawn(37, 10, "NigWebMedGlassAirlock");
        // Surgery & Morgue partition
        BuildVWall(46, 5, 15, stoneWall);
        Spawn(46, 8, "NigWebMedGlassAirlock");

        // Ward beds
        int[] wardYs = { 7, 10, 13 };
        foreach (var wy in wardYs)
        {
            Spawn(40, wy, "MedicalBed");
            Spawn(43, wy, "MedicalBed");
        }
        Spawn(38, 9, "TorchWall");
        Spawn(38, 14, "TorchWall");

        // Surgery Room (x: 47..54, y: 5..11)
        BuildHWall(46, 55, 11, stoneWall);
        Spawn(49, 11, "NigWebMineralDoorX");
        Spawn(50, 8, "OperatingTable");
        Spawn(52, 7, "TableStone");
        Spawn(52, 9, "TableStone");
        Spawn(48, 6, "TorchWall");

        // Morgue (x: 47..54, y: 12..15)
        Spawn(48, 14, "TableStone");
        Spawn(51, 14, "TableStone");
        Spawn(49, 14, "CandleInfinite");

        // --- CHURCH OF THE CREATOR (x: 37..55, y: 17..36) ---
        BuildHWall(37, 55, 36, stoneWall);
        // Grand church entrance (double church door)
        Spawn(37, 23, "NigWebChurchDoorDouble", 90f);

        // Priest's Vestry (x: 49..54, y: 17..24)
        BuildVWall(49, 17, 24, stoneWall);
        BuildHWall(49, 55, 24, stoneWall);
        Spawn(49, 21, "NigWebDoorWood1");
        Spawn(52, 23, "Bookshelf");
        Spawn(52, 20, "TableWood");
        Spawn(52, 19, "ChairWood");
        Spawn(51, 18, "TorchWall");

        // Church Nave Pews (Wooden Benches)
        int[] pewYs = { 21, 24, 27 };
        foreach (var py in pewYs)
        {
            Spawn(40, py, "WoodenBench");
            Spawn(41, py, "WoodenBench");
            Spawn(46, py, "WoodenBench");
            Spawn(47, py, "WoodenBench");
        }

        // Chancel & Holy Altar
        Spawn(44, 34, "NigWebLifewebAltar");
        Spawn(42, 34, "CandleInfinite");
        Spawn(46, 34, "CandleRedInfinite");
        Spawn(43, 35, "CandleInfinite");
        Spawn(45, 35, "CandleRedInfinite");

        // Nave Torches
        Spawn(38, 20, "TorchWall");
        Spawn(38, 28, "TorchWall");
        Spawn(54, 28, "TorchWall");
        Spawn(54, 34, "TorchWall");

        // ==========================================
        // 7. CENTER SECTOR: TAVERN, MARKET & SMITHY
        // ==========================================
        // --- TAVERN "THE OLD COCK" (x: 4..21, y: 27..38) ---
        BuildHWall(4, 21, 26, woodWall);
        BuildHWall(4, 21, 38, woodWall);
        BuildVWall(21, 26, 38, woodWall);
        Spawn(21, 31, "NigWebDoorWood1"); // Tavern Courtyard entrance

        // Kitchen partition
        BuildVWall(15, 33, 38, woodWall);
        Spawn(15, 35, "NigWebDoorWood1");
        Spawn(19, 36, "Sink");
        Spawn(17, 36, "TableWood");

        // Bar Counter & Stools
        for (int bx = 7; bx <= 12; bx++)
        {
            Spawn(bx, 35, "TableCounterWood");
            Spawn(bx, 34, "StoolBar");
        }
        Spawn(8, 37, "BarrelChemEmpty");
        Spawn(10, 37, "BarrelChemEmpty");
        Spawn(12, 37, "BarrelChemEmpty");

        // Dining Hall
        int[] tabXs = { 8, 12, 16 };
        foreach (var tx in tabXs)
        {
            Spawn(tx, 30, "TableWood");
            Spawn(tx, 29, "ChairWood");
            Spawn(tx, 31, "ChairWood");
        }
        Spawn(5, 30, "TorchWall");
        Spawn(14, 28, "TorchWall");

        // --- MARKET SQUARE (x: 23..33, y: 27..37) ---
        // Left Stall (x: 23..27, y: 33..37)
        BuildVWall(23, 33, 37, woodWall);
        BuildHWall(23, 27, 37, woodWall);
        BuildVWall(27, 33, 37, woodWall);
        Spawn(24, 33, "NigWebMerchantGate3x1");
        Spawn(24, 35, "TableCounterWood");
        Spawn(25, 36, "CrateLivestock");
        Spawn(26, 36, "CrateLivestock");

        // Right Stall (x: 29..33, y: 33..37)
        BuildVWall(29, 33, 37, woodWall);
        BuildHWall(29, 33, 37, woodWall);
        BuildVWall(33, 33, 37, woodWall);
        Spawn(30, 33, "NigWebMerchantGate3x1");
        Spawn(30, 35, "TableCounterWood");
        Spawn(31, 36, "BarrelChemEmpty");
        Spawn(32, 36, "BarrelChemEmpty");

        // --- BLACKSMITH WORKSHOP (x: 35..45, y: 27..38) ---
        BuildVWall(35, 27, 38, stoneWall);
        BuildHWall(35, 45, 27, stoneWall);
        BuildHWall(35, 45, 38, stoneWall);
        BuildVWall(45, 27, 38, stoneWall);
        Spawn(35, 30, "NigWebMineralDoorX"); // Workshop entrance
        Spawn(38, 34, "TableStone");
        Spawn(40, 34, "TableStone");
        Spawn(42, 34, "TableStone");
        Spawn(39, 36, "CrateGenericSteel");
        Spawn(41, 36, "CrateGenericSteel");
        Spawn(36, 32, "TorchWall");
        Spawn(44, 32, "TorchWall");

        // ==========================================
        // 8. NORTH SECTOR: THE HIGH KEEP / CITADEL
        // ==========================================
        // Inner Fortified Curtain Wall (y = 39)
        BuildHWall(4, 27, 39, stoneWall);
        BuildHWall(30, 55, 39, stoneWall);
        // Inner Keep Gates (2x1 Keep Doors)
        Spawn(28, 39, "NigWebKeepAirlock");

        // Keep Foyer (x: 24..34, y: 40..43)
        Spawn(26, 41, "TorchWall");
        Spawn(31, 41, "TorchWall");

        // --- GREAT THRONE ROOM (x: 22..36, y: 44..55) ---
        BuildVWall(22, 44, 55, stoneWall);
        BuildVWall(36, 44, 55, stoneWall);
        BuildHWall(22, 26, 44, stoneWall);
        BuildHWall(31, 36, 44, stoneWall);
        // Archway entrance at x: 27..30, y: 44

        // Banquet Table & Chairs
        for (int ty = 47; ty <= 50; ty++)
        {
            Spawn(28, ty, "TableWood");
            Spawn(29, ty, "TableWood");
            Spawn(27, ty, "ChairWood");
            Spawn(30, ty, "ChairWood");
        }
        Spawn(28, 47, "CandleInfinite");
        Spawn(29, 50, "CandleInfinite");

        // Baron's Raised Dais & Throne of Thorns
        Spawn(29, 53, "ChairRitual");
        Spawn(28, 53, "CandleRedInfinite");
        Spawn(30, 53, "CandleRedInfinite");

        // Throne Room Torches
        Spawn(23, 47, "TorchWall");
        Spawn(23, 51, "TorchWall");
        Spawn(35, 47, "TorchWall");
        Spawn(35, 51, "TorchWall");

        // --- BARON'S PRIVATE QUARTERS (x: 5..21, y: 40..55) ---
        BuildVWall(21, 40, 55, stoneWall);
        Spawn(21, 41, "NigWebDoorWood1"); // Door from Foyer
        BuildHWall(5, 21, 44, stoneWall);
        BuildVWall(14, 44, 55, stoneWall);

        // Baron's Bedchamber (x: 5..13, y: 44..55)
        Spawn(10, 44, "NigWebDoorWood1");
        Spawn(7, 52, "Bed");
        Spawn(10, 53, "Dresser");
        Spawn(7, 47, "TableWood");
        Spawn(7, 46, "ChairWood");
        Spawn(10, 47, "Bookshelf");
        Spawn(4, 48, "Window");
        Spawn(4, 51, "Window");
        Spawn(6, 50, "TorchWall");

        // Baron's Treasury Vault (x: 14..20, y: 44..55)
        Spawn(17, 44, "NigWebMineralDoorX");
        Spawn(16, 52, "CrateGenericSteel");
        Spawn(17, 52, "CrateGenericSteel");
        Spawn(18, 52, "CrateGenericSteel");
        Spawn(19, 48, "TorchWall");

        // --- COUNCIL ROOM & MEISTER'S STUDY (x: 37..54, y: 40..55) ---
        BuildVWall(37, 40, 55, stoneWall);
        Spawn(37, 41, "NigWebDoorWood1"); // Door from Foyer
        BuildHWall(37, 54, 44, stoneWall);
        BuildVWall(45, 44, 55, stoneWall);

        // War Room / Council (x: 37..44, y: 44..55)
        Spawn(41, 44, "NigWebDoorWood1");
        Spawn(40, 48, "TableWood");
        Spawn(40, 49, "TableWood");
        Spawn(41, 48, "TableWood");
        Spawn(41, 49, "TableWood");
        Spawn(39, 48, "ChairWood");
        Spawn(39, 49, "ChairWood");
        Spawn(42, 48, "ChairWood");
        Spawn(42, 49, "ChairWood");
        Spawn(40, 47, "ChairWood");
        Spawn(41, 47, "ChairWood");
        Spawn(40, 50, "ChairWood");
        Spawn(41, 50, "ChairWood");
        Spawn(38, 53, "Bookshelf");
        Spawn(39, 53, "Bookshelf");
        Spawn(38, 46, "TorchWall");
        Spawn(44, 46, "TorchWall");

        // Meister's Study & Alchemical Lab (x: 46..54, y: 44..55)
        Spawn(46, 47, "NigWebDoorWood1");
        Spawn(48, 54, "Bookshelf");
        Spawn(49, 54, "Bookshelf");
        Spawn(50, 54, "Bookshelf");
        Spawn(51, 54, "Bookshelf");
        Spawn(51, 48, "TableStone");
        Spawn(52, 48, "TableStone");
        Spawn(49, 46, "OperatingTable");
        Spawn(51, 49, "CandleInfinite");
        Spawn(55, 48, "Window");
        Spawn(55, 51, "Window");
        Spawn(48, 48, "TorchWall");

        // ==========================================
        // 9. COMMIT TILES & ENTITIES
        // ==========================================
        log.Info($"[BuildCastleGridCommand] Applying {tilesToSet.Count} tiles...");
        grid.SetTiles(tilesToSet);

        log.Info($"[BuildCastleGridCommand] Spawning {entitySpawns.Count} entities...");
        int spawnedCount = 0;
        foreach (var (tilePos, proto, rot) in entitySpawns)
        {
            var coords = new EntityCoordinates(gridUid, tilePos.X + 0.5f, tilePos.Y + 0.5f);
            try
            {
                var uid = _entityManager.SpawnEntity(proto, coords);
                if (rot != 0f)
                {
                    xformSystem.SetLocalRotation(uid, Angle.FromDegrees(rot));
                }
                spawnedCount++;
            }
            catch (Exception ex)
            {
                log.Error($"[BuildCastleGridCommand] Error spawning '{proto}' at ({tilePos.X}, {tilePos.Y}): {ex.Message}");
            }
        }

        shell.WriteLine($"[CastleGrid] Generated Ravenheart Fortress on Grid {gridUid} (Map: {mapId}).");
        shell.WriteLine($"[CastleGrid] Total Tiles: {tilesToSet.Count}, Total Entities: {spawnedCount}.");

        // ==========================================
        // 10. SAVE GRID ASSET (IF REQUESTED OR AUTO)
        // ==========================================
        if (autoSave)
        {
            try
            {
                var mapLoader = _entityManager.System<MapLoaderSystem>();
                var savePath = new ResPath("castle_grid.yml");

                shell.WriteLine($"[CastleGrid] Serializing grid to {savePath}...");
                bool saved = mapLoader.TrySaveGrid(gridUid, savePath);

                if (saved)
                {
                    shell.WriteLine($"[CastleGrid] Grid saved to user data: {savePath}");

                    // Also copy directly into Resources/Maps/_NigWeb/castle_grid.yml for Git repository
                    var repoDir = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Maps", "_NigWeb");
                    if (Directory.Exists(repoDir))
                    {
                        var repoFile = Path.Combine(repoDir, "castle_grid.yml");
                        if (_resourceManager.UserData.Exists(savePath))
                        {
                            using var inStream = _resourceManager.UserData.OpenRead(savePath);
                            using var outStream = File.Create(repoFile);
                            inStream.CopyTo(outStream);
                            shell.WriteLine($"[CastleGrid] Also exported to repository: {repoFile}");
                        }
                    }
                }
                else
                {
                    shell.WriteError("[CastleGrid] Failed to save grid via MapLoaderSystem.");
                }
            }
            catch (Exception ex)
            {
                log.Error($"[BuildCastleGridCommand] Exception while saving grid: {ex}");
                shell.WriteError($"[CastleGrid] Error saving grid: {ex.Message}");
            }
        }
    }

    private Tile GetTile(string name, string fallback = "FloorSteel")
    {
        if (_tileDefManager.TryGetDefinition(name, out var def))
            return new Tile(def.TileId);
        if (_tileDefManager.TryGetDefinition(fallback, out var fbDef))
            return new Tile(fbDef.TileId);
        return new Tile(0);
    }
}
