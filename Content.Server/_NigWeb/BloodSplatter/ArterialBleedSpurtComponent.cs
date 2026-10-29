// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Body.Part;

namespace Content.Server._NigWeb.BloodSplatter;

[RegisterComponent]
public sealed partial class ArterialBleedSpurtComponent : Component
{
    [DataField("partType")]
    public BodyPartType PartType = BodyPartType.Other;

    [DataField("symmetry")]
    public BodyPartSymmetry Symmetry = BodyPartSymmetry.None;

    [DataField("spurtsRemaining")]
    public int SpurtsRemaining = 5;

    [DataField("totalSpurts")]
    public int TotalSpurts = 5;

    [DataField("interval")]
    public float Interval = 0.20f;

    [DataField("timer")]
    public float Timer = 0.05f;

    [DataField("isFountain")]
    public bool IsFountain;

    [DataField("hasCustomGeometry")]
    public bool HasCustomGeometry;

    [DataField("customLocalOffset")]
    public Vector2 CustomLocalOffset = Vector2.Zero;

    [DataField("customLocalDir")]
    public Vector2 CustomLocalDir = Vector2.UnitY;
}
