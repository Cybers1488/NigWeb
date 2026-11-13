// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Server._NigWeb.Execution;

/// <summary>
///     Attached to an executioner while they are actively decapitating a victim.
/// </summary>
[RegisterComponent]
public sealed partial class BeheadingExecutionComponent : Component
{
    [DataField]
    public EntityUid Victim;

    [DataField]
    public EntityUid Weapon;

    [DataField]
    public float Timer;

    [DataField]
    public float TotalDuration = 2.95f;

    [DataField]
    public bool FirstStrikeDone;

    [DataField]
    public bool SecondStrikeDone;
}

/// <summary>
///     Attached to a victim while they are actively being decapitated.
/// </summary>
[RegisterComponent]
public sealed partial class BeingBeheadedComponent : Component
{
    [DataField]
    public EntityUid Executioner;
}
