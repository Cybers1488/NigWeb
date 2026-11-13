// SPDX-License-Identifier: MIT
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._NigWeb.Cherub;

[RegisterComponent]
public sealed partial class FreedCherubComponent : Component
{
    [DataField("ascentSpeed")]
    public float AscentSpeed = 1.0f;

    [DataField("lifetime")]
    public float Lifetime = 4.0f;

    public float Elapsed = 0.0f;
}
