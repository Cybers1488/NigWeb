// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._NigWeb.Enemies.SpiritSpawner;

/// <summary>
/// Component for EN109 Spirit Spawner from Blasphemous 2.
/// Data ONLY per RobustToolbox ECS rules.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpiritSpawnerComponent : Component
{
    [DataField, AutoNetworkedField]
    public SpiritSpawnerState State = SpiritSpawnerState.Idle;

    [DataField, AutoNetworkedField]
    public bool FacingLeft = false;

    [DataField, AutoNetworkedField]
    public EntityUid? CurrentTarget;

    /// <summary>
    /// Maximum detection and engagement range.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 12.0f;

    /// <summary>
    /// Cooldown between minion spawn summon cycles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpawnCooldown = 6.0f;

    [DataField, AutoNetworkedField]
    public float SpawnCooldownTimer = 1.5f;

    /// <summary>
    /// Cooldown between spirit projectile attack casts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackCooldown = 4.0f;

    [DataField, AutoNetworkedField]
    public float AttackCooldownTimer = 2.5f;

    /// <summary>
    /// Maximum simultaneous active flying spirits summoned.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxMinions = 3;

    [DataField, AutoNetworkedField]
    public List<EntityUid> ActiveMinions = new();

    /// <summary>
    /// Current state progression timer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StateTimer = 0f;

    /// <summary>
    /// Spawn anticipation duration (8 frames * 0.08s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpawnAnticipationDur = 0.64f;

    /// <summary>
    /// Spawn summoning duration (11 frames * 0.08s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpawningDur = 0.88f;

    /// <summary>
    /// Spawn recovery duration (4 frames * 0.08s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpawnRecoveryDur = 0.32f;

    /// <summary>
    /// Attack cast anticipation duration (8 frames * 0.08s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackAnticipationDur = 0.64f;

    /// <summary>
    /// Attack projectile launch duration (5 frames * 0.08s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackingDur = 0.40f;

    /// <summary>
    /// Attack recovery duration (7 frames * 0.08s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackRecoveryDur = 0.56f;

    /// <summary>
    /// Stun duration under heavy damage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StunDur = 1.8f;

    /// <summary>
    /// Death animation duration (24 frames * 0.08s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeathDur = 1.92f;

    /// <summary>
    /// Damage accumulation required to trigger stun state.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StunThreshold = 45.0f;

    [DataField, AutoNetworkedField]
    public float AccumulatedDamage = 0f;

    /// <summary>
    /// Entity prototype of the summoned spirit minion (EN116).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId MinionPrototype = "MobFlyingFlower";

    /// <summary>
    /// Entity prototype of the spectral wind projectile (PR109).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId ProjectilePrototype = "SpiritWindProjectile";

    [DataField]
    public SoundSpecifier? SoundAnticipation = new SoundCollectionSpecifier("NigWebSpiritSpawnerAnticipation");

    [DataField]
    public SoundSpecifier? SoundCast = new SoundCollectionSpecifier("NigWebSpiritSpawnerCast");

    [DataField]
    public SoundSpecifier? SoundAttack = new SoundCollectionSpecifier("NigWebSpiritSpawnerAttack");

    [DataField]
    public SoundSpecifier? SoundRecover = new SoundCollectionSpecifier("NigWebSpiritSpawnerRecover");

    [DataField]
    public SoundSpecifier? SoundStun = new SoundCollectionSpecifier("NigWebSpiritSpawnerStun");

    [DataField]
    public SoundSpecifier? SoundDeath = new SoundCollectionSpecifier("NigWebSpiritSpawnerDeath");

    [DataField]
    public SoundSpecifier? SoundTurn = new SoundCollectionSpecifier("NigWebSpiritSpawnerTurn");
}
