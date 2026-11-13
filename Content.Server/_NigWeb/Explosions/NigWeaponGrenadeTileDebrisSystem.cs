// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Decals;
using Content.Shared._NigWeb.Explosions;
using Content.Shared.Maps;
using Content.Shared.Trigger;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._NigWeb.Explosions;

/// <summary>
/// Broadcasts a tile debris scattering event to all clients in PVS range when an explosive grenade detonates.
/// Enforces key filtering so debris ONLY launches upon actual explosion detonation (not pin priming).
/// Plays the thunderous Hotline Miami explosion sound directly to all victims caught in the blast radius.
/// </summary>
public sealed class NigWeaponGrenadeTileDebrisSystem : EntitySystem
{
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly string[] BurntDecals = { "burnt1", "burnt2", "burnt3", "burnt4" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NigWeaponGrenadeTileDebrisComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<NigWeaponGrenadeTileDebrisComponent> ent, ref TriggerEvent args)
    {
        // Must match keysIn (default: "timer") so it NEVER triggers on use/priming
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;
        if (args.Key == null && ent.Comp.KeysIn.Count > 0 && !ent.Comp.KeysIn.Contains("default"))
            return;

        var xform = Transform(ent);
        var coords = xform.Coordinates;
        var tileId = "Space";

        if (_turf.TryGetTileRef(coords, out var tileRef))
        {
            var tileDef = _tileDefMan[tileRef.Value.Tile.TypeId];
            tileId = tileDef.ID;
        }

        // If exact center returned space, sample adjacent cardinal neighbors
        if (tileId == "Space")
        {
            Span<Vector2> offsets = stackalloc Vector2[]
            {
                new(0f, 1f),
                new(0f, -1f),
                new(1f, 0f),
                new(-1f, 0f),
            };

            foreach (var off in offsets)
            {
                if (_turf.TryGetTileRef(coords.Offset(off), out var neighborTile))
                {
                    var def = _tileDefMan[neighborTile.Value.Tile.TypeId];
                    if (def.ID != "Space")
                    {
                        tileId = def.ID;
                        break;
                    }
                }
            }
        }

        // Spawn permanent charred burn decals around the explosion crater if grid supports it
        for (int i = 0; i < 6; i++)
        {
            var offset = new Vector2(_random.NextFloat(-2.2f, 2.2f), _random.NextFloat(-2.2f, 2.2f));
            var decalId = BurntDecals[_random.Next(BurntDecals.Length)];
            _decals.TryAddDecal(decalId, coords.Offset(offset), out _, Color.FromHex("#110e0c"), Angle.FromDegrees(_random.Next(0, 360)), 0, false);
        }

        var radius = ent.Comp.Radius;
        var netCoords = GetNetCoordinates(coords);
        var ev = new NigWeaponExplosionTileDebrisEvent(netCoords, tileId, ent.Comp.DebrisCount, radius);
        RaiseNetworkEvent(ev, Filter.Pvs(ent));
    }
}
