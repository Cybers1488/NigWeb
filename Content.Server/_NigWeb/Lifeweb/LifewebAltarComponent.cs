using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._NigWeb.Lifeweb
{
    [RegisterComponent]
    public sealed partial class LifewebAltarComponent : Component
    {
        [DataField("powerPerSecond")]
        public float PowerPerSecond = 5000f;

        [DataField("isActive")]
        public bool IsActive = false;
        
        [DataField("drainDamage")]
        public float DrainDamage = 5f;

        [DataField("stuckOverlay")]
        public EntityUid? StuckOverlay;

        [ViewVariables]
        public byte CurrentMode = 0; // 0=Idle, 1=Libido, 2=Mortido

        public EntityUid? ExtractionSoundStream;
    }
}
