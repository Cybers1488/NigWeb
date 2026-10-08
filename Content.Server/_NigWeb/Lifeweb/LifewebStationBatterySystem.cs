using Content.Server.Power.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Examine;
using Content.Shared._NigWeb.Lifeweb;
using Robust.Server.GameObjects;
using System.Collections.Generic;

namespace Content.Server._NigWeb.Lifeweb
{
    public sealed class LifewebStationBatterySystem : EntitySystem
    {
        [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
        [Dependency] private readonly SharedBatterySystem _batterySystem = default!;
        
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<LifewebStationBatteryComponent, ExaminedEvent>(OnExamined);
        }

        private void OnExamined(EntityUid uid, LifewebStationBatteryComponent component, ExaminedEvent args)
        {
            if (!TryComp<BatteryComponent>(uid, out var battery))
                return;

            float charge = _batterySystem.GetCharge(uid);
            int percentage = (int)((charge / battery.MaxCharge) * 100);
            
            args.PushMarkup($"\nЗаряд батареи: [color=cyan]{percentage}%[/color] ({charge}/{battery.MaxCharge} Вт)");
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            
            float totalMainCharge = 0f;
            var mainBatteries = new List<Entity<BatteryComponent>>();
            var mainQuery = EntityQueryEnumerator<LifewebBatteryComponent, BatteryComponent>();
            while (mainQuery.MoveNext(out var uid, out var lwBatt, out var battery))
            {
                float charge = _batterySystem.GetCharge(uid);
                totalMainCharge += charge;
                mainBatteries.Add((uid, battery));
            }

            var stationBatteries = new List<Entity<BatteryComponent>>();
            var stationQuery = EntityQueryEnumerator<LifewebStationBatteryComponent, BatteryComponent, AppearanceComponent>();
            while (stationQuery.MoveNext(out var uid, out var stBatt, out var battery, out var appearance))
            {
                float charge = _batterySystem.GetCharge(uid);
                float fraction = charge / battery.MaxCharge;
                string state;
                
                if (fraction >= 0.875f) state = "ob100_1";
                else if (fraction >= 0.625f) state = "ob75_1";
                else if (fraction >= 0.375f) state = "ob50_1";
                else if (fraction > 0f) state = "ob25_1";
                else state = "battery_station_overlay";

                _appearanceSystem.SetData(uid, LifewebStationBatteryVisuals.State, state, appearance);

                if (charge < battery.MaxCharge)
                {
                    stationBatteries.Add((uid, battery));
                }
            }

            if (mainBatteries.Count > 0 && stationBatteries.Count > 0)
            {
                float transferRatePerSecond = 10000f; 
                float maxTotalNeeded = 0f;
                
                foreach (var stBat in stationBatteries)
                {
                    float charge = _batterySystem.GetCharge(stBat.Owner);
                    maxTotalNeeded += System.Math.Min(transferRatePerSecond * frameTime, stBat.Comp.MaxCharge - charge);
                }

                if (maxTotalNeeded > 0 && totalMainCharge > 0)
                {
                    float actualTotalTransferred = System.Math.Min(maxTotalNeeded, totalMainCharge);
                    float ratio = actualTotalTransferred / maxTotalNeeded;

                    float drainPerMain = actualTotalTransferred / mainBatteries.Count;
                    foreach (var mainBat in mainBatteries)
                    {
                        float charge = _batterySystem.GetCharge(mainBat.Owner);
                        _batterySystem.SetCharge(mainBat.Owner, System.Math.Max(0, charge - drainPerMain));
                    }

                    foreach (var stBat in stationBatteries)
                    {
                        float charge = _batterySystem.GetCharge(stBat.Owner);
                        float requested = System.Math.Min(transferRatePerSecond * frameTime, stBat.Comp.MaxCharge - charge);
                        float received = requested * ratio;
                        _batterySystem.SetCharge(stBat.Owner, charge + received);
                    }
                }
            }
        }
    }
}
