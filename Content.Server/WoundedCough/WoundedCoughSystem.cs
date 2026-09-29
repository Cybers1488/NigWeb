using Content.Shared.WoundedCough;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Humanoid;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.WoundedCough;

public sealed class WoundedCoughSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<WoundedCoughComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, WoundedCoughComponent component, ComponentStartup args)
    {
        var offset = TimeSpan.FromSeconds(_random.NextFloat(0, (float)component.CoughInterval.TotalSeconds));
        component.NextCough = _timing.CurTime + component.CoughInterval + offset;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WoundedCoughComponent, DamageableComponent, MobStateComponent>();
        var curTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var cough, out var damageable, out var mobState))
        {
            if (curTime < cough.NextCough)
                continue;

            var randomJitter = TimeSpan.FromSeconds(_random.NextFloat(-2f, 2f));
            cough.NextCough = curTime + cough.CoughInterval + randomJitter;

            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (damageable.TotalDamage < cough.DamageThreshold)
                continue;

            if (!_random.Prob(cough.CoughChance))
                continue;

            var name = Identity.Name(uid, EntityManager);
            _popup.PopupEntity($"Вы тяжко кашляете", uid, uid, PopupType.MediumCaution);
            _popup.PopupEntity($"{name} тяжко кашляет", uid, Filter.PvsExcept(uid), true, PopupType.MediumCaution);

            var sound = cough.CoughSound;
            if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid) && humanoid.Sex == Sex.Female)
            {
                sound = cough.FemaleCoughSound;
            }

            _audio.PlayPvs(sound, uid);
        }
    }
}
