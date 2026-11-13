// SPDX-License-Identifier: MIT
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._NigWeb.Cherub;

[RegisterComponent]
public sealed partial class CherubCageComponent : Component
{
    [DataField("broken")]
    public bool Broken = false;

    [DataField("cherubPrototype")]
    public string? CherubPrototype = null;

    [DataField("shatterPrototype")]
    public string ShatterPrototype = "NigWebCherubCageShatterEffect";

    public EntityUid? LastAttacker = null;
}
