using Robust.Shared.Serialization;
using System;

namespace Content.Shared._NigWeb.Lifeweb
{
    [Serializable, NetSerializable]
    public enum LifewebControlUiKey : byte
    {
        Key
    }

    [Serializable, NetSerializable]
    public sealed class LifewebControlBuiState : BoundUserInterfaceState
    {
        public byte Mode; // 0=Idle, 1=Libido, 2=Mortido
        public float PowerOutput;
        public float BatteryCharge;
        public float BatteryMaxCharge;

        public LifewebControlBuiState(byte mode, float powerOutput, float batteryCharge, float batteryMaxCharge)
        {
            Mode = mode;
            PowerOutput = powerOutput;
            BatteryCharge = batteryCharge;
            BatteryMaxCharge = batteryMaxCharge;
        }
    }
}
