// SPDX-License-Identifier: MIT
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._NigWeb.Cherub;

[RegisterComponent, NetworkedComponent]
public sealed partial class CherubFamiliarComponent : Component
{
    [DataField("owner")]
    public EntityUid? Owner = null;

    [DataField("orbitPhase")]
    public float OrbitPhase = 0f;

    [DataField("orbitSpeed")]
    public float OrbitSpeed = 2.0f;

    [DataField("orbitRadiusX")]
    public float OrbitRadiusX = 0.85f;

    [DataField("orbitRadiusY")]
    public float OrbitRadiusY = 0.35f;

    [DataField("orbitBaseHeight")]
    public float OrbitBaseHeight = 0.9f;
}
