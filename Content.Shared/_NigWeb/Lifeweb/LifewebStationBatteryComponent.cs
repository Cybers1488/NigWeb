using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Lifeweb
{
    [RegisterComponent]
    public sealed partial class LifewebStationBatteryComponent : Component
    {
    }

    [Serializable, NetSerializable]
    public enum LifewebStationBatteryVisuals : byte
    {
        State
    }
}
