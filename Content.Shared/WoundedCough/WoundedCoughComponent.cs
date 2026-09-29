using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.WoundedCough;

[RegisterComponent, NetworkedComponent]
public sealed partial class WoundedCoughComponent : Component
{
    [DataField]
    public FixedPoint2 DamageThreshold = 50;

    [DataField]
    public float CoughChance = 0.5f;

    [DataField]
    public TimeSpan CoughInterval = TimeSpan.FromSeconds(20f);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextCough;
    
    [DataField]
    public SoundSpecifier CoughSound = new SoundCollectionSpecifier("WoundedCough");
    
    [DataField]
    public SoundSpecifier FemaleCoughSound = new SoundCollectionSpecifier("WoundedCoughFemale");
}
