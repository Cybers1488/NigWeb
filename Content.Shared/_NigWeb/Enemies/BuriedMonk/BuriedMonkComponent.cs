// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Enemies.BuriedMonk;

/// <summary>
/// Component for EN18 Buried Monk from Blasphemous 2.
/// Data ONLY per RobustToolbox ECS rules.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BuriedMonkComponent : Component
{
    [DataField, AutoNetworkedField]
    public BuriedMonkState State = BuriedMonkState.Idle;

    [DataField, AutoNetworkedField]
    public bool FacingLeft = false;

    /// <summary>
    /// Aura light color emitted during anticipation and scream attacks.
    /// Deep mystical cyan/blue aura.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color AttackLightColor = Color.FromHex("#4fc3f7");

    /// <summary>
    /// Light radius for the aura burst.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackLightRadius = 5.0f;

    /// <summary>
    /// Maximum energy/intensity of the aura light during the scream.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackLightEnergy = 3.2f;

    /// <summary>
    /// Walking speed when stalking the player (slow, deliberate pacing).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WalkSpeed = 1.3f;

    /// <summary>
    /// Sprint speed when stalking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SprintSpeed = 1.3f;

    /// <summary>
    /// Time in seconds for the preparation/anticipation crouching loop before screaming.
    /// 10 frames * 0.08s = 0.80s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AnticipationDuration = 0.80f;

    /// <summary>
    /// Distance in tiles at which monk halts and begins his anticipation attack.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackRange = 2.6f;

    /// <summary>
    /// Cooldown between scream attacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackCooldown = 3.5f;

    [DataField, AutoNetworkedField]
    public float AttackCooldownTimer = 0.5f;

    /// <summary>
    /// Duration of the scream animation.
    /// 11 frames * 0.09s = 0.99s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ScreamDuration = 0.99f;

    /// <summary>
    /// Duration of the recovery animation returning to idle/walk.
    /// 8 frames * 0.09s = 0.72s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RecoveryDuration = 0.72f;

    /// <summary>
    /// Radius of the scream shockwave area.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ScreamRadius = 3.5f;

    /// <summary>
    /// Force of the linear impulse pushing targets away on scream.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ScreamKnockback = 7.0f;

    /// <summary>
    /// Time in seconds for the death dissolution animation before deletion.
    /// 20 frames * 0.09s = 1.80s.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeathDuration = 1.80f;

    [DataField, AutoNetworkedField]
    public float StateTimer = 0f;

    /// <summary>
    /// Target entity monk is currently tracking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentTarget;

    [DataField]
    public SoundSpecifier? SoundAnticipation = new SoundCollectionSpecifier("NigWebBuriedMonkAnticipation");

    [DataField]
    public SoundSpecifier? SoundAttack = new SoundCollectionSpecifier("NigWebBuriedMonkAttack");

    [DataField]
    public SoundSpecifier? SoundDeath = new SoundCollectionSpecifier("NigWebBuriedMonkDeath");

    [DataField]
    public SoundSpecifier? SoundDeathVoice = new SoundCollectionSpecifier("NigWebBuriedMonkDeathVoice");

    [DataField]
    public SoundSpecifier? SoundFootstep = new SoundCollectionSpecifier("NigWebBuriedMonkFootstep");
}

[Serializable, NetSerializable]
public enum BuriedMonkState : byte
{
    Idle,
    Walking,
    Anticipating,
    Screaming,
    Recovery,
    Dying
}
