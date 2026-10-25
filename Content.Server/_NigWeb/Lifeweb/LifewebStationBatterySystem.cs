using System.Collections.Generic;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._NigWeb.Lifeweb;
using Content.Shared.Examine;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Log;

namespace Content.Server._NigWeb.Lifeweb
{
    public sealed class LifewebStationBatterySystem : EntitySystem
    {
        [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
        [Dependency] private readonly SharedBatterySystem _batterySystem = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly ILogManager _logManager = default!;

        private ISawmill _sawmill = default!;
        private float _receiverScanTimer = 0f;
        private const float ReceiverScanInterval = 1.0f;

        public override void Initialize()
        {
            base.Initialize();
            _sawmill = _logManager.GetSawmill("lifeweb.battery");
            _sawmill.Info("[LifewebStationBatterySystem] Initializing LifewebStationBatterySystem...");

            SubscribeLocalEvent<LifewebStationBatteryComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<LifewebStationBatteryComponent, MapInitEvent>(OnBatteryMapInit);
            SubscribeLocalEvent<LifewebStationBatteryComponent, AnchorStateChangedEvent>(OnBatteryAnchorChanged);
            SubscribeLocalEvent<LifewebStationBatteryComponent, ReAnchorEvent>(OnBatteryReAnchor);
            SubscribeLocalEvent<LifewebStationBatteryComponent, ComponentShutdown>(OnBatteryShutdown);
        }

        private void OnExamined(EntityUid uid, LifewebStationBatteryComponent component, ExaminedEvent args)
        {
            if (!TryComp<BatteryComponent>(uid, out var battery))
            {
                _sawmill.Warning($"[LifewebStationBatterySystem] Battery {uid} missing BatteryComponent on examined.");
                return;
            }

            float charge = _batterySystem.GetCharge(uid);
            int percentage = (int)((charge / battery.MaxCharge) * 100);

            args.PushMarkup($"\nЗаряд батареи: [color=cyan]{percentage}%[/color] ({charge}/{battery.MaxCharge} Вт)");
            args.PushMarkup($"Радиус снабжения (ЛКП): [color=yellow]{component.SupplyRange}[/color] тайлов");
        }

        private void OnBatteryMapInit(EntityUid uid, LifewebStationBatteryComponent component, MapInitEvent args)
        {
            _sawmill.Info($"[LifewebStationBatterySystem] Battery {uid} MapInit, initializing receivers within {component.SupplyRange} tiles.");
            UpdateReceivers(uid, component);
        }

        private void OnBatteryAnchorChanged(EntityUid uid, LifewebStationBatteryComponent component, ref AnchorStateChangedEvent args)
        {
            _sawmill.Info($"[LifewebStationBatterySystem] Battery {uid} AnchorStateChanged to {args.Anchored}.");
            if (args.Anchored)
            {
                UpdateReceivers(uid, component);
            }
            else if (TryComp<ApcPowerProviderComponent>(uid, out var provider))
            {
                DisconnectAllReceivers(uid, provider);
            }
        }

        private void OnBatteryReAnchor(EntityUid uid, LifewebStationBatteryComponent component, ref ReAnchorEvent args)
        {
            _sawmill.Info($"[LifewebStationBatterySystem] Battery {uid} ReAnchored, re-evaluating receivers.");
            UpdateReceivers(uid, component);
        }

        private void OnBatteryShutdown(EntityUid uid, LifewebStationBatteryComponent component, ComponentShutdown args)
        {
            _sawmill.Info($"[LifewebStationBatterySystem] Battery {uid} shutting down, disconnecting all receivers.");
            if (TryComp<ApcPowerProviderComponent>(uid, out var provider))
            {
                DisconnectAllReceivers(uid, provider);
            }
        }

        private void UpdateReceivers(EntityUid uid, LifewebStationBatteryComponent comp, TransformComponent? xform = null, ApcPowerProviderComponent? provider = null)
        {
            if (!Resolve(uid, ref xform, ref provider, false))
                return;

            if (!xform.Anchored)
            {
                DisconnectAllReceivers(uid, provider);
                return;
            }

            var batteryMapPos = _transform.GetMapCoordinates(uid, xform);

            // 1. Verify existing linked receivers
            var currentReceivers = provider.LinkedReceivers.ToArray();
            foreach (var recComp in currentReceivers)
            {
                var recUid = recComp.Owner;
                if (Deleted(recUid) || Terminating(recUid) || !recComp.Running)
                {
                    provider.LinkedReceivers.Remove(recComp);
                    continue;
                }

                if (!TryComp<TransformComponent>(recUid, out var recXform) || !recXform.Anchored)
                {
                    DisconnectReceiver(uid, provider, recUid, recComp);
                    continue;
                }

                var recMapPos = _transform.GetMapCoordinates(recUid, recXform);
                if (recMapPos.MapId != batteryMapPos.MapId ||
                    (recMapPos.Position - batteryMapPos.Position).Length() > comp.SupplyRange)
                {
                    DisconnectReceiver(uid, provider, recUid, recComp);
                    continue;
                }
            }

            // 2. Discover new receivers within range
            var nearbyReceivers = _lookup.GetEntitiesInRange<ApcPowerReceiverComponent>(xform.Coordinates, comp.SupplyRange);
            foreach (var ent in nearbyReceivers)
            {
                var recUid = ent.Owner;
                var recComp = ent.Comp;

                if (recUid == uid || Deleted(recUid) || Terminating(recUid) || !recComp.Running)
                    continue;

                if (!TryComp<TransformComponent>(recUid, out var recXform) || !recXform.Anchored)
                    continue;

                // Already connected to us
                if (recComp.Provider == provider)
                    continue;

                // If already connected to another valid provider, don't hijack it
                if (recComp.Provider != null && Exists(recComp.Provider.Owner) && !Deleted(recComp.Provider.Owner) && !Terminating(recComp.Provider.Owner))
                    continue;

                var recMapPos = _transform.GetMapCoordinates(recUid, recXform);
                if (recMapPos.MapId != batteryMapPos.MapId ||
                    (recMapPos.Position - batteryMapPos.Position).Length() > comp.SupplyRange)
                    continue;

                ConnectReceiver(uid, provider, recUid, recComp);
            }
        }

        private void ConnectReceiver(EntityUid batteryUid, ApcPowerProviderComponent provider, EntityUid recUid, ApcPowerReceiverComponent recComp)
        {
            _sawmill.Debug($"[LifewebStationBatterySystem] Connecting receiver {recUid} to battery {batteryUid}.");
            recComp.Provider = provider;
            provider.AddReceiver(recComp);
        }

        private void DisconnectReceiver(EntityUid batteryUid, ApcPowerProviderComponent provider, EntityUid recUid, ApcPowerReceiverComponent recComp)
        {
            _sawmill.Debug($"[LifewebStationBatterySystem] Disconnecting receiver {recUid} from battery {batteryUid}.");
            if (recComp.Provider == provider)
            {
                provider.RemoveReceiver(recComp);
                recComp.Provider = null;
            }
        }

        private void DisconnectAllReceivers(EntityUid batteryUid, ApcPowerProviderComponent provider)
        {
            _sawmill.Info($"[LifewebStationBatterySystem] Disconnecting all receivers from battery {batteryUid}.");
            var receivers = provider.LinkedReceivers.ToArray();
            foreach (var recComp in receivers)
            {
                DisconnectReceiver(batteryUid, provider, recComp.Owner, recComp);
            }

            provider.LinkedReceivers.Clear();
            provider.Net?.QueueNetworkReconnect();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            _receiverScanTimer += frameTime;
            if (_receiverScanTimer >= ReceiverScanInterval)
            {
                _receiverScanTimer = 0f;
                var batteryScanQuery = EntityQueryEnumerator<LifewebStationBatteryComponent, TransformComponent, ApcPowerProviderComponent>();
                while (batteryScanQuery.MoveNext(out var uid, out var stBatt, out var xform, out var provider))
                {
                    if (xform.Anchored)
                    {
                        UpdateReceivers(uid, stBatt, xform, provider);
                    }
                }
            }

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
