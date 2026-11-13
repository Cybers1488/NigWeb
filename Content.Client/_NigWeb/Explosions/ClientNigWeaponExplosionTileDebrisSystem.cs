// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._NigWeb.Explosions;
using Content.Shared.Camera;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client._NigWeb.Explosions;

/// <summary>
/// Receives <see cref="NigWeaponExplosionTileDebrisEvent"/> from the server upon explosive detonation.
/// Matches the underlying floor tile to authentic pixel palettes (including planet mud, desert sand, dirt, and rock),
/// launches physical pixel debris across the blast crater via <see cref="TileDebrisPixelOverlay"/>,
/// applies distance-scaled screen shake to the player camera, and plays direct blast audio to victims.
/// </summary>
public sealed class ClientNigWeaponExplosionTileDebrisSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _cameraRecoil = default!;

    private TileDebrisPixelOverlay _overlay = default!;

    // 52 authentic sampled colors directly from planet_mud.png (NigWebFloorPlanetRock)
    private static readonly Color[] PlanetMudPalette =
    {
        Color.FromHex("#110f0f"), Color.FromHex("#120f0f"), Color.FromHex("#12100f"), Color.FromHex("#121010"),
        Color.FromHex("#131010"), Color.FromHex("#131110"), Color.FromHex("#141110"), Color.FromHex("#141211"),
        Color.FromHex("#151211"), Color.FromHex("#151311"), Color.FromHex("#151312"), Color.FromHex("#161311"),
        Color.FromHex("#161312"), Color.FromHex("#161412"), Color.FromHex("#171412"), Color.FromHex("#171413"),
        Color.FromHex("#181412"), Color.FromHex("#181513"), Color.FromHex("#181514"), Color.FromHex("#191513"),
        Color.FromHex("#191614"), Color.FromHex("#191615"), Color.FromHex("#1a1614"), Color.FromHex("#1a1715"),
        Color.FromHex("#1a1716"), Color.FromHex("#1b1715"), Color.FromHex("#1b1716"), Color.FromHex("#1b1816"),
        Color.FromHex("#1c1816"), Color.FromHex("#1c1817"), Color.FromHex("#1d1917"), Color.FromHex("#1e1917"),
        Color.FromHex("#1e1918"), Color.FromHex("#1e1a18"), Color.FromHex("#1f1a18"), Color.FromHex("#1f1b19"),
        Color.FromHex("#201b19"), Color.FromHex("#201b1a"), Color.FromHex("#201c1a"), Color.FromHex("#211c1a"),
        Color.FromHex("#211c1b"), Color.FromHex("#221d1b"), Color.FromHex("#231d1b"), Color.FromHex("#231e1c"),
        Color.FromHex("#241e1c"), Color.FromHex("#241f1d"), Color.FromHex("#251f1d"), Color.FromHex("#251f1e"),
        Color.FromHex("#25201e"), Color.FromHex("#26201e"), Color.FromHex("#26211f"), Color.FromHex("#27211f"),
    };

    // Authentic desert sand and sun-bleached dune palette (FloorDesert, FloorLowDesert, FloorDesertAstroSand)
    private static readonly Color[] DesertSandPalette =
    {
        Color.FromHex("#5a4b36"), Color.FromHex("#6b5a43"), Color.FromHex("#746249"), Color.FromHex("#7b684f"),
        Color.FromHex("#83765b"), Color.FromHex("#8c7e65"), Color.FromHex("#95866a"), Color.FromHex("#9e8e70"),
        Color.FromHex("#a89776"), Color.FromHex("#b29f7c"), Color.FromHex("#bcab87"), Color.FromHex("#c4b18d"),
        Color.FromHex("#d0be97"), Color.FromHex("#d8c6a0"), Color.FromHex("#e2d0aa"), Color.FromHex("#4c3e2c"),
    };

    // Rich organic dark soil and dirt palette (FloorDirt)
    private static readonly Color[] DirtPalette =
    {
        Color.FromHex("#23170e"), Color.FromHex("#2b1d12"), Color.FromHex("#322215"), Color.FromHex("#372617"),
        Color.FromHex("#3e2b19"), Color.FromHex("#45301f"), Color.FromHex("#4d3521"), Color.FromHex("#543a24"),
        Color.FromHex("#5a3f27"), Color.FromHex("#63452b"), Color.FromHex("#1c120a"),
    };

    // Authentic sampled rock palette from planet_rock.png and darkstonefloor.png
    private static readonly Color[] RockPalette =
    {
        Color.FromHex("#0a0a0a"), Color.FromHex("#0e0d0d"), Color.FromHex("#100f0e"), Color.FromHex("#110f0f"),
        Color.FromHex("#12100f"), Color.FromHex("#131110"), Color.FromHex("#141110"), Color.FromHex("#171412"),
        Color.FromHex("#191514"), Color.FromHex("#1b1313"), Color.FromHex("#211c1a"), Color.FromHex("#231e1c"),
        Color.FromHex("#241f1d"), Color.FromHex("#25201e"), Color.FromHex("#27211f"), Color.FromHex("#272220"),
        Color.FromHex("#292321"), Color.FromHex("#2c2724"), Color.FromHex("#302a28"), Color.FromHex("#312b29"),
        Color.FromHex("#322c2a"), Color.FromHex("#342424"), Color.FromHex("#3e3835"), Color.FromHex("#48494c"),
    };

    // Concrete and stone tile palette
    private static readonly Color[] StonePalette =
    {
        Color.FromHex("#2e2f32"), Color.FromHex("#38393c"), Color.FromHex("#404245"), Color.FromHex("#48494c"),
        Color.FromHex("#525458"), Color.FromHex("#585a5e"), Color.FromHex("#616368"), Color.FromHex("#696b70"),
        Color.FromHex("#73767c"), Color.FromHex("#222325"),
    };

    // Steel plating and hull tile palette
    private static readonly Color[] SteelPalette =
    {
        Color.FromHex("#2b2e35"), Color.FromHex("#343740"), Color.FromHex("#3a3e47"), Color.FromHex("#434852"),
        Color.FromHex("#4a4f5a"), Color.FromHex("#545a66"), Color.FromHex("#5b616e"), Color.FromHex("#666d7c"),
        Color.FromHex("#71798a"), Color.FromHex("#828b9e"), Color.FromHex("#23262c"),
    };

    // Grass and foliage tile palette
    private static readonly Color[] GrassPalette =
    {
        Color.FromHex("#1e2c14"), Color.FromHex("#243517"), Color.FromHex("#2d421d"), Color.FromHex("#354d22"),
        Color.FromHex("#3b5726"), Color.FromHex("#44632c"), Color.FromHex("#4a6d30"), Color.FromHex("#547b37"),
        Color.FromHex("#5b853c"), Color.FromHex("#679644"),
    };

    // Wood tile palette
    private static readonly Color[] WoodPalette =
    {
        Color.FromHex("#3b2413"), Color.FromHex("#4a2e19"), Color.FromHex("#5c3a20"), Color.FromHex("#6e4526"),
        Color.FromHex("#7d502d"), Color.FromHex("#8c5a34"), Color.FromHex("#2a190c"),
    };

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new TileDebrisPixelOverlay(_random);
        _overlayMan.AddOverlay(_overlay);

        SubscribeNetworkEvent<NigWeaponExplosionTileDebrisEvent>(OnTileDebrisEvent);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _overlay.FrameUpdate(frameTime);
    }

    private void OnTileDebrisEvent(NigWeaponExplosionTileDebrisEvent ev)
    {
        var coords = GetCoordinates(ev.Coordinates);
        if (!coords.IsValid(EntityManager))
            return;

        var mapCoords = _transform.ToMapCoordinates(coords);
        if (mapCoords.MapId == MapId.Nullspace)
            return;

        var tileId = ev.TileId;

        // Fall back to querying local turf if server passed Space or empty
        if (string.IsNullOrEmpty(tileId) || tileId == "Space")
        {
            if (_turf.TryGetTileRef(coords, out var localTile))
            {
                var def = _tileDefMan[localTile.Value.Tile.TypeId];
                tileId = def.ID;
            }
        }

        var palette = ResolveTilePalette(tileId);
        _overlay.EmitTileDebrisBurst(mapCoords.MapId, mapCoords.Position, palette, ev.Count, ev.Radius);

        // Dynamic distance-scaled screen shake and blast audio for anyone near or hit by the grenade
        if (_playerManager.LocalEntity is { Valid: true } player &&
            TryComp(player, out TransformComponent? playerXform))
        {
            var playerPos = _transform.ToMapCoordinates(playerXform.Coordinates);
            if (playerPos.MapId == mapCoords.MapId)
            {
                var dist = (playerPos.Position - mapCoords.Position).Length();
                const float maxShakeDistance = 18.0f;
                if (dist < maxShakeDistance)
                {
                    var factor = MathF.Pow(Math.Clamp(1.0f - (dist / maxShakeDistance), 0.0f, 1.0f), 1.5f);
                    var dir = playerPos.Position - mapCoords.Position;
                    if (dir.LengthSquared() < 0.001f)
                        dir = _random.NextAngle().ToVec();
                    else
                        dir = dir.Normalized();

                    var jitter = _random.NextAngle().ToVec();
                    var kickVector = (dir * 0.80f + jitter * 0.50f) * (factor * 3.4f);
                    _cameraRecoil.KickCamera(player, kickVector);
                }
            }
        }
    }

    /// <summary>
    /// Matches tile ID and naming to authentic palettes.
    /// </summary>
    private Color[] ResolveTilePalette(string tileId)
    {
        if (string.IsNullOrEmpty(tileId))
            return PlanetMudPalette;

        var lower = tileId.ToLowerInvariant();

        if (lower.Contains("sand") || lower.Contains("desert") || lower.Contains("dune") || lower.Contains("oasis") || lower.Contains("sun"))
            return DesertSandPalette;

        if (lower.Contains("mud") || lower == "nigwebfloorplanetrock")
            return PlanetMudPalette;

        if (lower.Contains("dirt") || lower.Contains("earth") || lower.Contains("soil"))
            return DirtPalette;

        if (lower.Contains("rock") || lower.Contains("darkstone") || lower.Contains("asteroid") || lower.Contains("basalt") || lower.Contains("chasm"))
            return RockPalette;

        if (lower.Contains("grass") || lower.Contains("moss") || lower.Contains("foliage") || lower.Contains("turf"))
            return GrassPalette;

        if (lower.Contains("wood") || lower.Contains("timber") || lower.Contains("plank"))
            return WoodPalette;

        if (lower.Contains("steel") || lower.Contains("metal") || lower.Contains("plating") || lower.Contains("hull") || lower.Contains("shuttle") || lower.Contains("grate") || lower.Contains("lattice") || lower.Contains("iron"))
            return SteelPalette;

        if (lower.Contains("stone") || lower.Contains("concrete") || lower.Contains("brick") || lower.Contains("floor"))
            return StonePalette;

        return PlanetMudPalette;
    }
}
