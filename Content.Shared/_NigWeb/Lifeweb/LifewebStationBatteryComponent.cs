using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Lifeweb
{
    [RegisterComponent]
    public sealed partial class LifewebStationBatteryComponent : Component
    {
        /// <summary>
        /// Радиус снабжения электричеством в тайлах (как ЛКП).
        /// </summary>
        [DataField("supplyRange")]
        public float SupplyRange = 20f;
    }

    [Serializable, NetSerializable]
    public enum LifewebStationBatteryVisuals : byte
    {
        State
    }
}
