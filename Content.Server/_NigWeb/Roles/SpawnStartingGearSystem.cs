// SPDX-License-Identifier: MIT

using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._NigWeb.Roles;

/// <summary>
/// Equips starting gear on mobs spawned with SpawnStartingGearComponent (e.g. from F5 menu).
/// Logic only!
/// </summary>
public sealed class SpawnStartingGearSystem : EntitySystem
{
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[SpawnStartingGearSystem] Initializing SpawnStartingGearSystem...");
        SubscribeLocalEvent<SpawnStartingGearComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, SpawnStartingGearComponent component, MapInitEvent args)
    {
        Log.Info($"[SpawnStartingGearSystem] MapInit on mob {ToPrettyString(uid)}, equipping gear {component.Gear} for job {component.Job}");

        if (component.Gear != null)
        {
            _stationSpawning.EquipStartingGear(uid, component.Gear);
        }

        if (component.Job != null && _prototype.Resolve(component.Job, out JobPrototype? jobProto))
        {
            foreach (var jobSpecial in jobProto.Special)
            {
                jobSpecial.AfterEquip(uid);
            }
        }
    }
}
