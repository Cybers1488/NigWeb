// SPDX-License-Identifier: MIT

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Altar;

/// <summary>
/// Component for Our Lady of the Chalices sacrificial chalices.
/// Accepts liquid blood, severed hands, feet, legs, arms, heads, and organs (everything except torso).
/// Data only!
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChaliceComponent : Component
{
    /// <summary>
    /// Current blood and flesh progress points in this chalice.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 CurrentProgress = FixedPoint2.Zero;

    /// <summary>
    /// Points required to fill this chalice to the brim (100%).
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxProgress = FixedPoint2.New(200);

    /// <summary>
    /// Current visual fill state (Empty, Quarter, Half, Full, Broken).
    /// </summary>
    [DataField, AutoNetworkedField]
    public ChaliceFillState FillState = ChaliceFillState.Empty;

    /// <summary>
    /// Points awarded for offering a severed hand.
    /// </summary>
    [DataField]
    public FixedPoint2 HandBonus = FixedPoint2.New(50);

    /// <summary>
    /// Points awarded for offering a severed head.
    /// </summary>
    [DataField]
    public FixedPoint2 HeadBonus = FixedPoint2.New(50);

    /// <summary>
    /// Points awarded for offering other limbs (arms, legs, feet).
    /// </summary>
    [DataField]
    public FixedPoint2 LimbBonus = FixedPoint2.New(25);

    /// <summary>
    /// Points awarded for offering an organ.
    /// </summary>
    [DataField]
    public FixedPoint2 OrganBonus = FixedPoint2.New(35);

    /// <summary>
    /// Points awarded per unit of blood reagent poured into the chalice.
    /// </summary>
    [DataField]
    public FixedPoint2 BloodPerUnit = FixedPoint2.New(1);

    /// <summary>
    /// Sound played when an offering is absorbed into the chalice.
    /// </summary>
    [DataField]
    public SoundSpecifier FillSound = new SoundPathSpecifier("/Audio/_NigWeb/Lady/lady_fill_chalice.ogg");

    /// <summary>
    /// Cultist chant played when an offering is made to the chalice.
    /// </summary>
    [DataField]
    public SoundSpecifier? ChantSound = new SoundCollectionSpecifier("CultistChantSounds");

    /// <summary>
    /// Sound played when the chalice reaches maximum capacity.
    /// </summary>
    [DataField]
    public SoundSpecifier FullSound = new SoundPathSpecifier("/Audio/_NigWeb/Lady/lady_stage_bell.ogg");
}
