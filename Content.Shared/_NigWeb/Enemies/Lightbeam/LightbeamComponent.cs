// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._NigWeb.Enemies.Lightbeam;

/// <summary>
/// Component for EN112 Lightbeam (Divine Light Emitter) from Blasphemous 2.
/// Data ONLY per RobustToolbox ECS rules.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LightbeamComponent : Component
{
    [DataField, AutoNetworkedField]
    public LightbeamState State = LightbeamState.Idle;

    [DataField, AutoNetworkedField]
    public bool FacingLeft = false;

    [DataField, AutoNetworkedField]
    public Angle AimAngle = Angle.Zero;

    /// <summary>
    /// Detection and maximum engagement range.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 12.0f;

    /// <summary>
    /// Telegraph charge duration before unleashing the holy beam (attack anticipation).
    /// 14 frames * 0.06s = 0.84s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ChargeDuration = 0.84f;

    /// <summary>
    /// Duration of the active holy beam firing phase (attack loop).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FiringDuration = 1.60f;

    /// <summary>
    /// Settling and cooldown duration after beam ceases (attack recovery).
    /// 12 frames * 0.05s = 0.60s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RecoveryDuration = 0.60f;

    /// <summary>
    /// Cooldown between beam attack cycles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackCooldown = 3.5f;

    [DataField, AutoNetworkedField]
    public float CooldownTimer = 1.0f;

    [DataField, AutoNetworkedField]
    public float StateTimer = 0f;

    /// <summary>
    /// Angular tracking speed in radians per second towards the moving target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TrackingSpeed = 2.8f;

    /// <summary>
    /// Heat / burn damage applied per beam tick.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageHeat = 6.0f;

    /// <summary>
    /// Shock / holy electric damage applied per beam tick.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageShock = 4.0f;

    /// <summary>
    /// Knockback impulse pushing entities caught in the line of the holy beam.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Knockback = 6.0f;

    /// <summary>
    /// Interval between raycast damage and beam updates during firing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TickInterval = 0.15f;

    [DataField, AutoNetworkedField]
    public float TickTimer = 0f;

    /// <summary>
    /// Duration of shattering death animation.
    /// 10 frames * 0.08s = 0.80s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeathDuration = 0.80f;

    /// <summary>
    /// Beam entity segment prototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId BeamPrototype = "HolyBeamSegment";

    /// <summary>
    /// Telegraph targeting beam prototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId TelegraphPrototype = "HolyBeamTelegraph";

    /// <summary>
    /// Divine holy light illumination color.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color AttackLightColor = Color.FromHex("#FFF59D");

    [DataField, AutoNetworkedField]
    public float AttackLightRadius = 6.0f;

    [DataField, AutoNetworkedField]
    public float AttackLightEnergy = 3.5f;

    [DataField, AutoNetworkedField]
    public EntityUid? CurrentTarget;

    [DataField]
    public SoundSpecifier? SoundActivate = new SoundCollectionSpecifier("NigWebLightbeamAnticipation");

    [DataField]
    public SoundSpecifier? SoundFiring = new SoundCollectionSpecifier("NigWebLightbeamBeam");

    [DataField]
    public SoundSpecifier? SoundRecover = new SoundCollectionSpecifier("NigWebLightbeamRecover");

    [DataField]
    public SoundSpecifier? SoundDeath = new SoundCollectionSpecifier("NigWebLightbeamDeath");

    [DataField]
    public SoundSpecifier? SoundTurn = new SoundCollectionSpecifier("NigWebLightbeamTurn");

    [DataField]
    public SoundSpecifier? SoundWakeup = new SoundCollectionSpecifier("NigWebLightbeamWakeup");

    [DataField]
    public SoundSpecifier? SoundHit = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningshock.ogg");
}
