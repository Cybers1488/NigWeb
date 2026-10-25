// SPDX-License-Identifier: MIT

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Altar;

/// <summary>
/// Component for Our Lady of the Chalices sacrificial altar.
/// Holds data about current blood offering progress and stage (1 to 5).
/// Data only!
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LadyAltarComponent : Component
{
    /// <summary>
    /// Current blood/offering progress points.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 CurrentProgress = FixedPoint2.Zero;

    /// <summary>
    /// Total points needed to reach Stage 5 (final ascension).
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxProgress = FixedPoint2.New(1000);

    /// <summary>
    /// Current visual and ritual stage from 1 to 5.
    /// Stage 1: Porcelain (0..199)
    /// Stage 2: Cracking skin (200..399)
    /// Stage 3: Flaying face (400..599)
    /// Stage 4: Bloody muscles (600..799)
    /// Stage 5: Flayed gold crown (800..1000)
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurrentStage = 1;

    /// <summary>
    /// Points awarded for offering a severed hand.
    /// Severed hands are especially prized in canonical Blasphemous lore.
    /// </summary>
    [DataField]
    public FixedPoint2 HandBonus = FixedPoint2.New(50);

    /// <summary>
    /// Points awarded for offering an organ (heart, brain, etc.).
    /// </summary>
    [DataField]
    public FixedPoint2 OrganBonus = FixedPoint2.New(35);

    /// <summary>
    /// Points awarded for offering other limbs (arms, legs, feet).
    /// </summary>
    [DataField]
    public FixedPoint2 LimbBonus = FixedPoint2.New(25);

    /// <summary>
    /// Points awarded per unit of liquid blood poured into the altar.
    /// </summary>
    [DataField]
    public FixedPoint2 BloodPerUnit = FixedPoint2.New(1);

    /// <summary>
    /// Sound played when an offering is absorbed into the chalice.
    /// </summary>
    [DataField]
    public SoundSpecifier OfferingSound = new SoundPathSpecifier("/Audio/_NigWeb/Lady/lady_fill_chalice.ogg");

    /// <summary>
    /// Sound played when a stage advance occurs and skin is flayed.
    /// </summary>
    [DataField]
    public SoundSpecifier StageAdvanceSound = new SoundPathSpecifier("/Audio/_NigWeb/Lady/lady_stage_bell.ogg");

    /// <summary>
    /// Sound played when Stage 5 (Victory / Final Ascension) is reached.
    /// </summary>
    [DataField]
    public SoundSpecifier AscensionSound = new SoundPathSpecifier("/Audio/_NigWeb/Lady/lady_bell_low.ogg");
}
