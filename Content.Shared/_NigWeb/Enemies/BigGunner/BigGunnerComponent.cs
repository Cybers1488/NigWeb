// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._NigWeb.Enemies.BigGunner;

/// <summary>
/// Component for EN114 Big Gunner from Blasphemous 2.
/// Data ONLY per RobustToolbox ECS rules.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BigGunnerComponent : Component
{
    [DataField, AutoNetworkedField]
    public BigGunnerState State = BigGunnerState.Idle;

    [DataField, AutoNetworkedField]
    public bool FacingLeft = false;

    /// <summary>
    /// Movement speed for Big Gunner (0f - stationary siege artillery).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WalkSpeed = 0f;

    [DataField, AutoNetworkedField]
    public float SprintSpeed = 0f;

    /// <summary>
    /// Distance threshold for switching from cannon fire to close-quarters cannon slam.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeleeRange = 2.5f;

    /// <summary>
    /// Maximum distance to engage targets with cannon shots.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RangedRange = 12.0f;

    /// <summary>
    /// Minimum distance for ranged attacks (closer than this triggers melee instead).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinRangedRange = 2.2f;

    /// <summary>
    /// Cooldown between cannon shots.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RangedCooldown = 4.0f;

    [DataField, AutoNetworkedField]
    public float RangedCooldownTimer = 1.0f;

    /// <summary>
    /// Cooldown between melee cannon slams.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeleeCooldown = 3.0f;

    [DataField, AutoNetworkedField]
    public float MeleeCooldownTimer = 0.5f;

    /// <summary>
    /// Duration of aiming stance before firing cannon.
    /// 6 frames * 0.10s = 0.60s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RangedAnticipationDuration = 0.60f;

    /// <summary>
    /// Duration of cannon blast and recoil.
    /// 8 frames * 0.09s = 0.72s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RangedAttackDuration = 0.72f;

    /// <summary>
    /// Duration of raising the massive cannon overhead.
    /// 7 frames * 0.09s = 0.63s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeleeAnticipationDuration = 0.63f;

    /// <summary>
    /// Duration of slamming cannon into the ground.
    /// 9 frames * 0.08s = 0.72s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeleeAttackDuration = 0.72f;

    /// <summary>
    /// Radius of the melee slam shock area in front of gunner.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeleeRadius = 2.6f;

    /// <summary>
    /// Impulse force pushing targets away on melee slam.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeleeKnockback = 9.0f;

    /// <summary>
    /// Blunt damage dealt by the melee slam.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeleeDamage = 30.0f;

    /// <summary>
    /// Velocity of the cannonball projectile in tiles/sec.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CannonballSpeed = 18.0f;

    /// <summary>
    /// Prototype ID for the fired cannonball projectile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId CannonballPrototype = "CannonballBigGunner";

    /// <summary>
    /// Duration of the death collapse animation.
    /// 25 frames * 0.08s = 2.0s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeathDuration = 2.0f;

    [DataField, AutoNetworkedField]
    public float StateTimer = 0f;

    [DataField, AutoNetworkedField]
    public EntityUid? CurrentTarget;

    [DataField]
    public SoundSpecifier? SoundGunAttack = new SoundCollectionSpecifier("NigWebBigGunnerGunAttack");

    [DataField]
    public SoundSpecifier? SoundGunBlast = new SoundCollectionSpecifier("NigWebBigGunnerGunBlast");

    [DataField]
    public SoundSpecifier? SoundMeleeAttack = new SoundCollectionSpecifier("NigWebBigGunnerMeleeAttack");

    [DataField]
    public SoundSpecifier? SoundMeleeSlam = new SoundCollectionSpecifier("NigWebBigGunnerMeleeSlam");

    [DataField]
    public SoundSpecifier? SoundMeleeHit = new SoundCollectionSpecifier("MetalThud");

    [DataField]
    public SoundSpecifier? SoundIdle = new SoundCollectionSpecifier("NigWebBigGunnerIdle");

    [DataField]
    public SoundSpecifier? SoundTurn = new SoundCollectionSpecifier("NigWebBigGunnerTurn");

    [DataField]
    public SoundSpecifier? SoundHurt = new SoundCollectionSpecifier("NigWebBigGunnerHurt");

    [DataField]
    public SoundSpecifier? SoundDeath = new SoundCollectionSpecifier("NigWebBigGunnerDeath");
}
