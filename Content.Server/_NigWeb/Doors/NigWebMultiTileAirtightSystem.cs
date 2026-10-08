using System.Numerics;
using Content.Server._NigWeb.Doors.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;

namespace Content.Server._NigWeb.Doors.Systems;

/// <summary>
///     РќСѓР¶РЅР° С‡С‚РѕР±С‹ РјСѓР»СЊС‚РёС‚Р°Р№Р»РѕРІС‹Рµ РґРІРѕР№РЅС‹Рµ РёР»Рё С‚СЂРѕР№РЅС‹Рµ С€Р»СЋР·С‹ РЅРѕСЂРјР°Р»СЊРЅРѕ РЅРµ РїСЂРѕРїСѓСЃРєР°Р»Рё РіР°Р·С‹
///     РЎРїР°РІРЅРёС‚ Р±Р»РѕРєРµСЂС‹ РЅР° СЃРѕСЃРµРґРЅРёС… С‚Р°Р№Р»Р°С… Рё СЂРµРіСѓР»РёСЂСѓРµС‚ РєРѕРіРґР° Р±Р»РѕРєРµСЂС‹ РЅРµ РїСЂРѕРїСѓСЃРєР°СЋС‚ РіР°Р·, РєРѕРіРґР° РїСЂРѕРїСѓСЃРєР°СЋС‚. Р’ Р·Р°РІРёСЃРёРјРѕСЃС‚Рё РѕС‚ СЃРѕСЃС‚РѕСЏРЅРёСЏ С€Р»СЋР·Р°
/// </summary>
public sealed partial class NigWebMultiTileAirtightSystem : EntitySystem
{
    private const string BlockerPrototype = "NigWebMultiTileAirtightBlocker";

    [Dependency] private AirtightSystem _airtight = default!;
    [Dependency] private TransformSystem _transform = default!;

    [Dependency] private EntityQuery<AirtightComponent> _airtightQuery = default!;
    [Dependency] private EntityQuery<DoorComponent> _doorQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NigWebMultiTileAirtightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NigWebMultiTileAirtightComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<NigWebMultiTileAirtightComponent, AirtightChanged>(OnAirtightChanged);
        SubscribeLocalEvent<NigWebMultiTileAirtightComponent, DoorStateChangedEvent>(OnDoorStateChanged);
        SubscribeLocalEvent<NigWebMultiTileAirtightComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<NigWebMultiTileAirtightComponent, ReAnchorEvent>(OnReAnchor);
        SubscribeLocalEvent<NigWebMultiTileAirtightComponent, MoveEvent>(OnMoved);
    }

    private void OnMapInit(Entity<NigWebMultiTileAirtightComponent> ent, ref MapInitEvent args)
    {
        RefreshGeometry(ent);
        RefreshAirblock(ent);
    }

    private void OnShutdown(Entity<NigWebMultiTileAirtightComponent> ent, ref ComponentShutdown args)
    {
        DeleteBlockers(ent);
    }

    private void OnDoorStateChanged(Entity<NigWebMultiTileAirtightComponent> ent, ref DoorStateChangedEvent args)
    {
        RefreshAirblock(ent);
    }

    private void OnAirtightChanged(Entity<NigWebMultiTileAirtightComponent> ent, ref AirtightChanged args)
    {
        if (!args.AirBlockedChanged)
            return;

        RefreshAirblock(ent);
    }

    private void OnAnchorChanged(Entity<NigWebMultiTileAirtightComponent> ent, ref AnchorStateChangedEvent args)
    {
        RefreshGeometry(ent);
        RefreshAirblock(ent);
    }

    private void OnReAnchor(Entity<NigWebMultiTileAirtightComponent> ent, ref ReAnchorEvent args)
    {
        RefreshGeometry(ent);
        RefreshAirblock(ent);
    }

    private void OnMoved(Entity<NigWebMultiTileAirtightComponent> ent, ref MoveEvent args)
    {
        RefreshGeometry(ent);
        RefreshAirblock(ent);
    }

    /// <summary>
    ///     РџРµСЂРµСЃРѕР·РґР°РµС‚ Р±Р»РѕРєРµСЂС‹ РЅР° РґРѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹С… С‚Р°Р№Р»Р°С…
    ///     ExtraTiles Р·Р°РґР°СЋС‚СЃСЏ РІ Р»РѕРєР°Р»СЊРЅС‹С… РєРѕРѕСЂРґРёРЅР°С‚Р°С… РґРІРµСЂРё, РїРѕСЌС‚РѕРјСѓ РѕС„С„СЃРµС‚ РЅР°РґРѕ РїРѕРІРµСЂРЅСѓС‚СЊ РїРѕ РЅР°РїСЂР°РІР»РµРЅРёСЋ РґРІРµСЂРё
    ///     РџРѕСЃР»Рµ РїРѕРІРѕСЂРѕС‚Р° РѕРєСЂСѓРіСЏРµРј СЃСЂР°Р·Сѓ РґРѕ С†РµР»С‹С… С‚Р°Р№Р»РѕРІ, РїРѕС‚РѕРјСѓ С‡С‚Рѕ РїРѕРІРѕСЂРѕС‚ РёРґРµС‚ С‡РµСЂРµР· float
    /// </summary>
    private void RefreshGeometry(Entity<NigWebMultiTileAirtightComponent> ent)
    {
        DeleteBlockers(ent);

        if (!_xformQuery.TryGetComponent(ent.Owner, out var xform))
            return;

        if (!xform.Anchored || xform.GridUid is not { } gridUid || !_gridQuery.TryGetComponent(gridUid, out var grid))
            return;

        var baseTile = _transform.GetGridTilePositionOrDefault((ent, xform), grid);
        var rotation = xform.LocalRotation.RoundToCardinalAngle();

        foreach (var local in ent.Comp.ExtraTiles)
        {
            var rotated = rotation.RotateVec(new Vector2(local.X, local.Y));
            var offset = new Vector2i((int)MathF.Round(rotated.X), (int)MathF.Round(rotated.Y));
            var tile = baseTile + offset;

            var coords = GetTileCenter(gridUid, grid, tile);
            var blocker = Spawn(BlockerPrototype, coords);
            var blockerXform = _xformQuery.GetComponent(blocker);

            // РћР±СЏР·Р°С‚РµР»СЊРЅРѕ Р°РЅРєРѕСЂРёРј РЅР° РіСЂРёРґ Рё РєРѕРЅРєСЂРµС‚РЅС‹Р№ С‚Р°Р№Р», РёР±Рѕ airtight Р±СѓРґРµС‚ РЅРµ РЅР° С‚РѕРј РјРµСЃС‚Рµ Р±СѓРґРµС‚ Рё Р±СѓРґРµС‚ Р°РґСЃРєРѕРµ С€РѕСѓ
            if (!_transform.AnchorEntity((blocker, blockerXform), (gridUid, grid), tile))
            {
                Del(blocker);
                continue;
            }

            ent.Comp.Blockers.Add(blocker);
        }
    }

    /// <summary>
    ///     РЎРёРЅС…СЂРѕРЅРёР·РёСЂСѓРµС‚ Airtight.AirBlocked Сѓ РІСЃРµС… Р±Р»РѕРєРµСЂРѕРІ Сѓ РґРІРµСЂРё
    ///     Р•СЃР»Рё Сѓ РґРІРµСЂРё РµСЃС‚СЊ AirtightComponent С‚Рѕ Р±РµСЂРµРј РµРіРѕ AirBlocked
    /// </summary>
    private void RefreshAirblock(Entity<NigWebMultiTileAirtightComponent> ent)
    {
        bool blocked;

        if (_airtightQuery.TryGetComponent(ent.Owner, out var doorAirtight))
            blocked = doorAirtight.AirBlocked;
        else
        {
            if (!_doorQuery.TryGetComponent(ent.Owner, out var door))
                return;

            blocked = door.State is DoorState.Closed or DoorState.Welded;
        }

        foreach (var blocker in ent.Comp.Blockers)
        {
            if (!_airtightQuery.TryGetComponent(blocker, out var airtight))
                continue;

            _airtight.SetAirblocked((blocker, airtight), blocked);
        }
    }

    private void DeleteBlockers(Entity<NigWebMultiTileAirtightComponent> ent)
    {
        foreach (var blocker in ent.Comp.Blockers)
        {
            if (!TerminatingOrDeleted(blocker))
                Del(blocker);
        }

        ent.Comp.Blockers.Clear();
    }

    private static EntityCoordinates GetTileCenter(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        // +0.5f С‡С‚РѕР±С‹ РїРѕР»СѓС‡РёС‚СЊ С†РµРЅС‚СЂ С‚Р°Р№Р»Р°, Р° С‚Рѕ Р±РµСЂРµС‚ С‚Рѕ РїСЂР°РІС‹Р№ СѓРіРѕР», С‚Рѕ Р»РµРІС‹Р№
        var pos = new Vector2(tile.X + 0.5f, tile.Y + 0.5f) * grid.TileSize;
        return new EntityCoordinates(gridUid, pos);
    }
}

