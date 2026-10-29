// SPDX-License-Identifier: MIT

using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._NigWeb.Bosses.EternalSummoner;

/// <summary>
/// Data component for the Summoner of Eternal Night monument.
/// Holds state, death threshold tracking, lighting colors, and retribution configuration.
/// Logic is strictly isolated in EternalSummonerSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EternalSummonerComponent : Component
{
    /// <summary>
    /// Total deaths recorded across the wasteland / map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TotalDeaths = 0;

    /// <summary>
    /// Number of deaths required to trigger awakening of the Mask.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DeathsRequiredForMask = 10;

    /// <summary>
    /// Number of deaths required to trigger Stage 3 (Skull awakening).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DeathsRequiredForSkull = 30;

    /// <summary>
    /// Current head state of the monument.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EternalHeadState CurrentHeadState = EternalHeadState.ElderIdle;

    /// <summary>
    /// Duration of the transition animation (21 frames * 0.09s = 1.89s).
    /// </summary>
    [DataField]
    public float TransitionDuration = 1.89f;

    /// <summary>
    /// Active timer during transition animation.
    /// </summary>
    [DataField]
    public float StateTimer = 0f;

    /// <summary>
    /// Flag indicating whether the monument is currently in the middle of a head transition.
    /// </summary>
    [DataField]
    public bool IsTransitioning = false;

    /// <summary>
    /// Ambient light color of the wasteland planet at sunset ("как бы закат").
    /// </summary>
    [DataField]
    public Color SunsetColor = Color.FromHex("#7a3820");

    /// <summary>
    /// Ambient light color after 10 kills ("тупо темно").
    /// </summary>
    [DataField]
    public Color DarknessColor = Color.FromHex("#030305");

    /// <summary>
    /// Monument's initial warm amber glow during sunset.
    /// </summary>
    [DataField]
    public Color InitialLightColor = Color.FromHex("#ffbb55");

    [DataField]
    public float InitialLightRadius = 7.5f;

    [DataField]
    public float InitialLightEnergy = 1.8f;

    /// <summary>
    /// Monument's bloody red light after 10 kills.
    /// </summary>
    [DataField]
    public Color MaskLightColor = Color.FromHex("#ff1111");

    [DataField]
    public float MaskLightRadius = 12.0f;

    [DataField]
    public float MaskLightEnergy = 2.5f;

    /// <summary>
    /// Ambient light color of the wasteland planet in Stage 3 (abyssal blood eclipse).
    /// </summary>
    [DataField]
    public Color BloodEclipseColor = Color.FromHex("#0d0208");

    /// <summary>
    /// Monument's apocalyptic crimson light in Stage 3 (Skull).
    /// </summary>
    [DataField]
    public Color SkullLightColor = Color.FromHex("#ff0044");

    [DataField]
    public float SkullLightRadius = 16.0f;

    [DataField]
    public float SkullLightEnergy = 3.2f;

    /// <summary>
    /// Sound played when the head shifts (sinister bone twist / chime).
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundHeadChange = new SoundPathSpecifier("/Audio/_NigWeb/Summoner/sinodo_head_change.ogg");

    /// <summary>
    /// Sound played when divine retribution smites an attacker.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundRetribution = new SoundCollectionSpecifier("SummonerRetributionSounds");

    /// <summary>
    /// Liturgical sound played when Stage 2 (Mask) awakens and darkness descends.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundStage2Transition = new SoundPathSpecifier("/Audio/_NigWeb/Summoner/stage2_transition.ogg");

    /// <summary>
    /// Cataclysmic sound played when Stage 3 (Skull) awakens and blood eclipse descends.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundStage3Transition = new SoundPathSpecifier("/Audio/_NigWeb/Summoner/stage3_transition.ogg");

    /// <summary>
    /// Global voice announcement played across the entire server when Stage 2 (Mask) awakens.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundMaskAwakeningGlobal = new SoundPathSpecifier("/Audio/_NigWeb/Summoner/mask_awakening_global.ogg");

    /// <summary>
    /// Global voice announcement played across the entire server when Stage 3 (Skull) awakens.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundSkullAwakeningGlobal = new SoundPathSpecifier("/Audio/_NigWeb/Summoner/skull_awakening_global.ogg");

    /// <summary>
    /// Cooldown between retribution strikes to prevent multi-hit audio/effect spam.
    /// </summary>
    [DataField]
    public TimeSpan RetributionCooldown = TimeSpan.FromSeconds(0.4);

    [DataField]
    public TimeSpan NextRetributionTime = TimeSpan.Zero;

    /// <summary>
    /// Weather prototype activated on Stage 3 (Death Skull awakening).
    /// </summary>
    [DataField]
    public ProtoId<WeatherPrototype>? Stage3Weather = "NigWebCorruptBloodStorm";

    /// <summary>
    /// Flag indicating if the map atmosphere has been initialized.
    /// </summary>
    [DataField]
    public bool LightingApplied = false;
}
