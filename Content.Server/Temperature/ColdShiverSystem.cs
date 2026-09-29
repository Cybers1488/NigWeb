using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Shared.Popups;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Humanoid;
using Content.Shared.Bed.Sleep;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio;

namespace Content.Server.Temperature;

public sealed class ColdShiverSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly SoundCollectionSpecifier _maleExhaust = new("ExhaustMale");
    private readonly SoundCollectionSpecifier _femaleExhaust = new("ExhaustFemale");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColdShiverComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, ColdShiverComponent component, MapInitEvent args)
    {
        component.NextCheck = _timing.CurTime + component.CheckInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ColdShiverComponent, TemperatureComponent, TemperatureDamageComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out var shiver, out var temp, out var tempDamage, out var mobState))
        {
            if (curTime < shiver.NextCheck)
                continue;

            // Reset timer with jitter
            shiver.NextCheck = curTime + shiver.CheckInterval + TimeSpan.FromSeconds(_random.NextFloat(-2f, 2f));

            // Must be alive and awake
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (HasComp<SleepingComponent>(uid))
                continue;

            // Must be very cold (below the cold damage threshold)
            if (temp.CurrentTemperature > tempDamage.ColdDamageThreshold)
                continue;

            // 30% chance to shiver and play exhaustion sound
            if (!_random.Prob(shiver.ShiverChance))
                continue;

            SoundSpecifier soundToPlay = _maleExhaust;
            if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid) && humanoid.Sex == Sex.Female)
            {
                soundToPlay = _femaleExhaust;
            }

            _popup.PopupEntity($"дрожит от сильного холода!", uid, uid, PopupType.MediumCaution);
            
            var name = Identity.Name(uid, EntityManager);
            _popup.PopupEntity($"{name} дрожит от сильного холода!", uid, Filter.PvsExcept(uid), true, PopupType.MediumCaution);

            _audio.PlayPvs(soundToPlay, uid);
        }
    }
}
