// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Enemies.Shockwave;

/// <summary>
/// Component for EN115 Shockwave Generator from Blasphemous 2.
/// Data ONLY per RobustToolbox ECS rules.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShockwaveComponent : Component
{
    [DataField, AutoNetworkedField]
    public ShockwaveState State = ShockwaveState.Idle;

    /// <summary>
    /// Aura light color emitted during anticipation charge and blast detonation.
    /// Fiery crimson energy shockwave.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color AttackLightColor = Color.FromHex("#ff4d4d");

    /// <summary>
    /// Light radius for the shockwave blast.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackLightRadius = 5.5f;

    /// <summary>
    /// Maximum energy/intensity of the aura light during blast detonation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackLightEnergy = 3.5f;

    /// <summary>
    /// Duration of the initial spawn/unfolding animation.
    /// 16 frames * 0.08s = 1.28s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpawnDuration = 1.28f;

    /// <summary>
    /// Whether to play the spawn animation when spawned into the world.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PlaySpawnAnimation = false;

    /// <summary>
    /// Duration of the spin-up anticipation phase before blast detonation.
    /// 10 frames * 0.08s = 0.80s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AnticipationDuration = 0.80f;

    /// <summary>
    /// Duration of the shockwave blast explosion animation.
    /// 8 frames * 0.08s = 0.64s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackDuration = 0.64f;

    /// <summary>
    /// Duration of the mechanism recovery / stabilization phase.
    /// 10 frames * 0.08s = 0.80s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RecoveryDuration = 0.80f;

    /// <summary>
    /// Duration of the death burst / dissolution animation before deletion.
    /// 10 frames * 0.09s = 0.90s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeathDuration = 0.90f;

    /// <summary>
    /// Detection and trigger range in tiles at which the shockwave device begins charging.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackRange = 5.0f;

    /// <summary>
    /// Cooldown between shockwave attacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackCooldown = 3.5f;

    [DataField, AutoNetworkedField]
    public float AttackCooldownTimer = 1.0f;

    /// <summary>
    /// Radius of the shockwave blast wave.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ShockwaveRadius = 4.0f;

    /// <summary>
    /// Force of the linear impulse pushing targets away on blast detonation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ShockwaveKnockback = 8.5f;

    /// <summary>
    /// Blunt damage dealt by the shockwave blast.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageBlunt = 18.0f;

    /// <summary>
    /// Shock/electric damage dealt by the shockwave blast.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageShock = 12.0f;

    [DataField, AutoNetworkedField]
    public float StateTimer = 0f;

    /// <summary>
    /// Target entity the device is currently tracking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentTarget;

    [DataField]
    public SoundSpecifier? SoundAnticipation = new SoundCollectionSpecifier("NigWebShockwaveAnticipation");

    [DataField]
    public SoundSpecifier? SoundSpin = new SoundCollectionSpecifier("NigWebShockwaveSpin");

    [DataField]
    public SoundSpecifier? SoundSlowSpin = new SoundCollectionSpecifier("NigWebShockwaveSlowSpin");

    [DataField]
    public SoundSpecifier? SoundAttack = new SoundCollectionSpecifier("NigWebShockwaveAttack");

    [DataField]
    public SoundSpecifier? SoundRecover = new SoundCollectionSpecifier("NigWebShockwaveRecover");

    [DataField]
    public SoundSpecifier? SoundWakeUp = new SoundCollectionSpecifier("NigWebShockwaveWakeUp");

    [DataField]
    public SoundSpecifier? SoundDeath = new SoundCollectionSpecifier("NigWebShockwaveDeath");
}
