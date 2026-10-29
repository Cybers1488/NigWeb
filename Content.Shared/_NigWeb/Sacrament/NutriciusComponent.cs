// SPDX-License-Identifier: MIT
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Sacrament;

/// <summary>
/// Component for the Sorrow Patriarch (Вскормитель Скорби).
/// Cradles a wax infant and has a frozen grafted breast that requires hot sacrificial blood to thaw.
/// When thawed, weeping holy Seraphic Wax can be gathered into chalices, beakers, and bottles.
/// Has a total wax pool of 100 units regenerating over 10 minutes (600s),
/// and a cooldown between wax extractions.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NutriciusComponent : Component
{
    /// <summary>
    /// Whether the Feeder's breast is thawed and warm.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsWarm;

    /// <summary>
    /// How much warmth time remains in seconds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WarmthTimeLeft;

    /// <summary>
    /// Duration of warmth granted by a single sacrificial cut in seconds.
    /// </summary>
    [DataField]
    public float WarmthDuration = 75f;

    /// <summary>
    /// Current available wax pool units (regenerates up to MaxWaxPool).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentWaxPool = 100f;

    /// <summary>
    /// Maximum wax pool (100 units).
    /// </summary>
    [DataField]
    public float MaxWaxPool = 100f;

    /// <summary>
    /// Time in seconds required to regenerate the full wax pool (10 minutes = 600s).
    /// </summary>
    [DataField]
    public float WaxRegenTime = 600f;

    /// <summary>
    /// Cooldown between wax extractions in seconds.
    /// </summary>
    [DataField]
    public float GatherCooldown = 5f;

    /// <summary>
    /// Timestamp when wax can next be gathered.
    /// </summary>
    [DataField]
    public TimeSpan NextGatherTime = TimeSpan.Zero;

    [DataField]
    public SoundSpecifier WarmthSound = new SoundCollectionSpecifier("NigWebFatherSigh");

    [DataField]
    public SoundSpecifier WaxDripSound = new SoundPathSpecifier("/Audio/Effects/Fluids/slosh.ogg");

    [DataField]
    public SoundSpecifier SizzleSound = new SoundCollectionSpecifier("NigWebWaxSizzle");

    [DataField]
    public SoundSpecifier VoiceSound = new SoundCollectionSpecifier("NigWebPatriarchVoice");
}
