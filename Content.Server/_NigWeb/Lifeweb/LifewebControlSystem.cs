using Content.Shared._NigWeb.Lifeweb;
using Content.Server.Power.Components;
using Robust.Server.GameObjects;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._NigWeb.Lifeweb
{
    public sealed class LifewebControlSystem : EntitySystem
    {
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly SharedBatterySystem _batterySystem = default!;
        
        private float _accumulator = 0f;

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            _accumulator += frameTime;

            if (_accumulator < 1f)
                return;

            _accumulator -= 1f;

            byte altarMode = 0;
            var totalPower = 0f;
            var charge = 0f;
            var maxCharge = 0f;

            // Find an altar
            var queryAltars = EntityQueryEnumerator<LifewebAltarComponent>();
            while (queryAltars.MoveNext(out var uid, out var altar))
            {
                if (altar.CurrentMode > 0)
                {
                    altarMode = altar.CurrentMode;
                    totalPower = altarMode == 1 ? 5000f : 2500f; // UI mockup logic
                }
            }

            // Find battery
            var queryBatteries = EntityQueryEnumerator<LifewebBatteryComponent, BatteryComponent>();
            while (queryBatteries.MoveNext(out var uid, out var lwBatt, out var battery))
            {
                charge += _batterySystem.GetCharge(uid);
                maxCharge += battery.MaxCharge;
            }

            var state = new LifewebControlBuiState(altarMode, totalPower, charge, maxCharge);

            var queryControls = EntityQueryEnumerator<LifewebControlComponent>();
            while (queryControls.MoveNext(out var uid, out var control))
            {
                _uiSystem.SetUiState(uid, LifewebControlUiKey.Key, state);
            }
        }
    }
}
