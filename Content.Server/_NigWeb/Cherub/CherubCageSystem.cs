// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using System.Numerics;
using Content.Server.Popups;
using Content.Shared._NigWeb.Cherub;
using Content.Shared._NigWeb.Religion;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._NigWeb.Cherub;

public sealed class CherubCageSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private readonly HashSet<MapId> _shuffledMaps = new();
    private int _shatteredCount = 0;
    private bool _buffGranted = false;

    // Pool of verified open dirt coordinates (NigWebFloorPlanetRock) on desert_sun with clearance >= 3.0 tiles from all walls
    private static readonly Vector2[] SafeDirtCoordinates =
    {
        new(-33.5f, 10.5f), new(98.5f, 47.5f), new(44.5f, -16.5f), new(99.5f, -8.5f),
        new(-3.5f, -28.5f), new(1.5f, 6.5f), new(91.5f, 19.5f), new(30.5f, 7.5f),
        new(17.5f, -13.5f), new(-19.5f, -11.5f), new(-20.5f, -30.5f), new(46.5f, 1.5f),
        new(88.5f, 3.5f), new(88.5f, -20.5f), new(-2.5f, -12.5f), new(-42.5f, -5.5f),
        new(22.5f, -25.5f), new(60.5f, -18.5f), new(75.5f, 2.5f), new(82.5f, 35.5f),
        new(-28.5f, -18.5f), new(10.5f, 20.5f), new(38.5f, -22.5f), new(55.5f, 8.5f),
        new(68.5f, -12.5f), new(85.5f, 22.5f), new(-12.5f, 15.5f), new(-38.5f, 5.5f),
        new(25.5f, -18.5f), new(50.5f, -10.5f), new(94.5f, -18.5f), new(95.5f, 12.5f),
        new(-7.5f, 8.5f), new(3.5f, -19.5f), new(14.5f, 24.5f), new(72.5f, -22.5f),
        new(80.5f, 8.5f), new(97.5f, 32.5f), new(40.5f, 12.5f), new(-15.5f, -28.5f)
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CherubCageComponent, DamageChangedEvent>(OnCageDamage);
        SubscribeLocalEvent<CherubCageComponent, DestructionEventArgs>(OnCageDestruction);
        SubscribeLocalEvent<SunShadowCycleComponent, ComponentStartup>(OnSunShadowStartup);
    }

    private void OnSunShadowStartup(EntityUid uid, SunShadowCycleComponent comp, ComponentStartup args)
    {
        var sunXform = Transform(uid);
        var mapId = sunXform.MapID;
        var gridUid = sunXform.GridUid;
        if (mapId == MapId.Nullspace || gridUid == null)
            return;

        if (_shuffledMaps.Contains(mapId))
            return;

        _shuffledMaps.Add(mapId);
        _shatteredCount = 0;
        _buffGranted = false;

        var cages = new List<EntityUid>();
        var query = EntityQueryEnumerator<CherubCageComponent, TransformComponent>();
        while (query.MoveNext(out var cageUid, out _, out var xform))
        {
            if (xform.MapID == mapId)
            {
                cages.Add(cageUid);
            }
        }

        var available = new List<Vector2>(SafeDirtCoordinates);
        _random.Shuffle(available);

        for (int i = 0; i < cages.Count && i < available.Count; i++)
        {
            _transform.SetCoordinates(cages[i], new EntityCoordinates(gridUid.Value, available[i]));
        }

        Log.Info($"[CherubCageSystem] Randomly placed {cages.Count} Cherub Cages across desert wasteland dirt.");
    }

    public int SpawnCagesOnMap(MapId mapId, int count = 15, float range = 100f)
    {
        int spawned = 0;
        var available = new List<Vector2>(SafeDirtCoordinates);
        _random.Shuffle(available);

        for (int i = 0; i < count && i < available.Count; i++)
        {
            var coords = new MapCoordinates(available[i], mapId);
            Spawn("NigWebCherubCage", coords);
            spawned++;
        }

        Log.Info($"[CherubCageSystem] Spawned {spawned} Cherub Cages across map {mapId}.");
        return spawned;
    }

    private void OnCageDamage(EntityUid uid, CherubCageComponent comp, DamageChangedEvent args)
    {
        if (comp.Broken)
            return;

        if (args.Origin != null)
            comp.LastAttacker = args.Origin;

        if (!args.DamageIncreased)
            return;

        var coords = Transform(uid).Coordinates;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Cherub/cherub_hit.ogg"), coords, AudioParams.Default.WithVolume(2f));
    }

    private void OnCageDestruction(EntityUid uid, CherubCageComponent comp, DestructionEventArgs args)
    {
        if (comp.Broken)
            return;

        comp.Broken = true;
        var xform = Transform(uid);
        var coords = xform.Coordinates;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Cherub/cherub_break.ogg"), coords, AudioParams.Default.WithVolume(4f));
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Cherub/cherub_free.ogg"), coords, AudioParams.Default.WithVolume(4f));

        if (!string.IsNullOrEmpty(comp.ShatterPrototype))
        {
            Spawn(comp.ShatterPrototype, coords);
        }

        if (!string.IsNullOrEmpty(comp.CherubPrototype))
        {
            Spawn(comp.CherubPrototype, coords);
        }

        _shatteredCount++;
        Log.Info($"[CherubCageSystem] Cherub Cage {uid} shattered at {coords}! Total shattered: {_shatteredCount}/15");

        if (_shatteredCount >= 15 && !_buffGranted)
        {
            _buffGranted = true;
            GrantCherubBuff(comp.LastAttacker);
        }
    }

    public void GrantCherubBuff(EntityUid? breaker)
    {
        var isChurch = breaker != null && HasComp<LadyFaithfulComponent>(breaker.Value);

        var count = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } playerEnt || !Exists(playerEnt))
                continue;

            if (!TryComp<MobStateComponent>(playerEnt, out var mobState) || mobState.CurrentState == MobState.Dead)
                continue;

            var playerIsChurch = HasComp<LadyFaithfulComponent>(playerEnt);

            bool isTeammate;
            if (breaker == null)
            {
                isTeammate = true;
            }
            else if (playerEnt == breaker.Value)
            {
                isTeammate = true;
            }
            else if (_faction.IsEntityFriendly(breaker.Value, playerEnt))
            {
                isTeammate = true;
            }
            else
            {
                isTeammate = (isChurch == playerIsChurch);
            }

            if (!isTeammate)
                continue;

            if (HasComp<CherubFamiliarOwnerComponent>(playerEnt))
                continue;

            var coords = Transform(playerEnt).Coordinates;
            var familiarUid = Spawn("NigWebCherubFamiliar", coords);
            var familiarComp = EnsureComp<CherubFamiliarComponent>(familiarUid);
            familiarComp.Owner = playerEnt;

            var ownerComp = EnsureComp<CherubFamiliarOwnerComponent>(playerEnt);
            ownerComp.Familiar = familiarUid;

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Cherub/cherub_free.ogg"), coords, AudioParams.Default.WithVolume(4f));
            _popup.PopupEntity("Небесный херувим взял вас под свою защиту!", playerEnt, playerEnt, PopupType.Large);
            count++;
        }

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/_NigWeb/Cherub/cherub_free.ogg"), Filter.Broadcast(), false, AudioParams.Default.WithVolume(5f));
        Log.Info($"[CherubCageSystem] All 15 Cherub Cages broken! Familiar protection granted to {count} players of the winning front.");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FreedCherubComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var cherub, out var xform))
        {
            cherub.Elapsed += frameTime;
            xform.LocalPosition += new Vector2(0f, cherub.AscentSpeed * frameTime);

            if (cherub.Elapsed >= cherub.Lifetime)
            {
                QueueDel(uid);
            }
        }
    }
}
