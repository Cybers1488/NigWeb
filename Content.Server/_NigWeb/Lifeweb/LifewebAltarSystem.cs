using Content.Server.Popups;
using Content.Shared.Buckle.Components;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Damage;
using Content.Shared._NigWeb.Lifeweb;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Audio;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Server._NigWeb.BloodSplatter;
using Content.Shared.Chemistry.Components;
using Content.Shared.Body.Components;
using Robust.Shared.Random;

namespace Content.Server._NigWeb.Lifeweb
{
    public sealed class LifewebAltarSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedBatterySystem _batterySystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly SharedAmbientSoundSystem _ambientSoundSystem = default!;
        [Dependency] private readonly BloodSplatterSystem _bloodSplatterSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<LifewebAltarComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
            SubscribeLocalEvent<LifewebAltarComponent, StrappedEvent>(OnStrapped);
            SubscribeLocalEvent<LifewebAltarComponent, UnstrappedEvent>(OnUnstrapped);
        }

        private void OnStrapped(EntityUid uid, LifewebAltarComponent component, StrappedEvent args)
        {
            var overlay = Spawn("NigWebLifewebStuckOverlay", Transform(uid).Coordinates);
            component.StuckOverlay = overlay;
        }

        private void OnUnstrapped(EntityUid uid, LifewebAltarComponent component, UnstrappedEvent args)
        {
            if (component.StuckOverlay != null)
            {
                QueueDel(component.StuckOverlay.Value);
                component.StuckOverlay = null;
            }
        }

        private void OnUnstrapAttempt(EntityUid uid, LifewebAltarComponent component, ref UnstrapAttemptEvent args)
        {
            if (args.User.HasValue && args.User.Value == args.Buckle.Owner)
            {
                args.Cancelled = true;
                _popupSystem.PopupEntity("Вы прикованы к алтарю и не можете освободиться!", args.User.Value, args.User.Value, PopupType.LargeCaution);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            
            float totalGeneratedWatts = 0f;
            
            var query = EntityQueryEnumerator<LifewebAltarComponent, StrapComponent>();
            while (query.MoveNext(out var uid, out var altar, out var strap))
            {
                byte mode = 0;
                if (strap.BuckledEntities.Count > 0)
                {
                    foreach (var entity in strap.BuckledEntities)
                    {
                        if (HasComp<DamageableComponent>(entity))
                        {
                            var dmg = new DamageSpecifier();
                            dmg.DamageDict.Add("Bloodloss", altar.DrainDamage * frameTime);
                            _damageableSystem.TryChangeDamage(entity, dmg, true);
                            
                            if (TryComp<BloodstreamComponent>(entity, out var bloodstream) && _random.Prob(0.1f))
                            {
                                _bloodSplatterSystem.SplatterBlood(entity, bloodstream, 1, 1.0f, false);
                            }

                            if (TryComp<MobStateComponent>(entity, out var mobState) && mobState.CurrentState == MobState.Dead)
                            {
                                mode = 2; // Mortido
                                totalGeneratedWatts += (altar.PowerPerSecond / 2f) * frameTime; 
                            }
                            else
                            {
                                mode = 1; // Libido
                                totalGeneratedWatts += altar.PowerPerSecond * frameTime; 
                            }
                            break; 
                        }
                    }
                }
                
                altar.CurrentMode = mode;
                bool active = mode > 0;

                if (altar.IsActive != active)
                {
                    altar.IsActive = active;
                    
                    if (active)
                    {
                        altar.ExtractionSoundStream = _audio.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Lifeweb/altar.ogg"), uid, AudioParams.Default.WithLoop(true))?.Entity;
                    }
                    else if (altar.ExtractionSoundStream != null)
                    {
                        _audio.Stop(altar.ExtractionSoundStream);
                        altar.ExtractionSoundStream = null;
                    }
                }
            }

            if (totalGeneratedWatts > 0)
            {
                var mainBatteries = new System.Collections.Generic.List<Entity<BatteryComponent>>();
                var batQuery = EntityQueryEnumerator<LifewebBatteryComponent, BatteryComponent>();
                while (batQuery.MoveNext(out var uid, out var lwbat, out var battery))
                {
                    mainBatteries.Add((uid, battery));
                }

                if (mainBatteries.Count > 0)
                {
                    float wattsPerBattery = totalGeneratedWatts / mainBatteries.Count;
                    foreach (var mainBat in mainBatteries)
                    {
                        float currentCharge = _batterySystem.GetCharge(mainBat.Owner);
                        _batterySystem.SetCharge(mainBat.Owner, System.Math.Min(currentCharge + wattsPerBattery, mainBat.Comp.MaxCharge));
                    }
                }
            }
        }
    }
}
