// SPDX-License-Identifier: MIT

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._NigWeb.Roles;

/// <summary>
/// When attached to a mob (like a pawn spawned via F5), automatically equips the specified starting gear
/// and runs job specials on MapInit. Data only!
/// </summary>
[RegisterComponent]
public sealed partial class SpawnStartingGearComponent : Component
{
    [DataField]
    public ProtoId<StartingGearPrototype>? Gear;

    [DataField]
    public ProtoId<JobPrototype>? Job;
}
