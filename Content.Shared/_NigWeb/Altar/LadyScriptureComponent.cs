// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Altar;

/// <summary>
/// Component for sacred scripture of Our Lady of the Chalices (Ordo Carnis Truncatae).
/// Grants healing of brute/burn wounds and stopping bleeding when chanted by an ordained priest,
/// while burning unordained readers with holy retribution.
/// Data only!
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LadyScriptureComponent : Component
{
    /// <summary>
    /// Cult chant played when preaching/healing.
    /// </summary>
    [DataField]
    public SoundSpecifier ChantSound = new SoundCollectionSpecifier("CultistChantSounds");

    /// <summary>
    /// Sound played when the scripture burns an unordained user.
    /// </summary>
    [DataField]
    public SoundSpecifier BackfireSound = new SoundCollectionSpecifier("CultistPainSounds");

    /// <summary>
    /// Amount of damage healed per prayer.
    /// </summary>
    [DataField]
    public DamageSpecifier HealDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", -20 },
            { "Slash", -20 },
            { "Piercing", -20 },
            { "Heat", -15 }
        }
    };

    /// <summary>
    /// Backfire damage dealt to unordained readers.
    /// </summary>
    [DataField]
    public DamageSpecifier BackfireDamage = new()
    {
        DamageDict = new()
        {
            { "Heat", 15 }
        }
    };
}
