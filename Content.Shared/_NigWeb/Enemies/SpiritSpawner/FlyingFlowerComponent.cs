// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Enemies.SpiritSpawner;

/// <summary>
/// Component for EN116 Flying Flower (Summoned Spirit) from Blasphemous 2.
/// Data ONLY per RobustToolbox ECS rules.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlyingFlowerComponent : Component
{
    [DataField, AutoNetworkedField]
    public FlyingFlowerState State = FlyingFlowerState.Idle;

    [DataField, AutoNetworkedField]
    public bool FacingLeft = false;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField, AutoNetworkedField]
    public EntityUid? Spawner;

    [DataField, AutoNetworkedField]
    public float StateTimer = 0f;

    /// <summary>
    /// Brief pause upon detaching from Spawner's crown before accelerating towards target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpawnPauseTimer = 0.35f;

    /// <summary>
    /// Flight hover speed towards target. Slower, smooth floating.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MoveSpeed = 2.0f;

    /// <summary>
    /// Distance at which flower begins blooming detonation anticipation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TriggerRadius = 1.8f;

    /// <summary>
    /// Duration of blooming charging animation (6 frames * 0.10s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AnticipationDur = 0.60f;

    /// <summary>
    /// Duration of explosion blast animation (5 frames * 0.09s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ExplosionDur = 0.45f;

    /// <summary>
    /// Duration of death animation (10 frames * 0.10s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeathDur = 1.00f;

    /// <summary>
    /// Blast damage radius upon detonation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ExplosionRadius = 2.0f;

    /// <summary>
    /// Heat damage dealt by bloom burst.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageHeat = 15.0f;

    /// <summary>
    /// Slash damage dealt by spirit petals.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageSlash = 10.0f;

    [DataField]
    public SoundSpecifier? SoundAnticipation = new SoundCollectionSpecifier("NigWebFlyingFlowerAnticipation");

    [DataField]
    public SoundSpecifier? SoundExplosion = new SoundCollectionSpecifier("NigWebFlyingFlowerExplosion");

    [DataField]
    public SoundSpecifier? SoundDeath = new SoundCollectionSpecifier("NigWebFlyingFlowerDeath");
}
