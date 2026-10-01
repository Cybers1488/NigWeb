using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Atmos;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Shared.Atmos.Components;
using Content.Shared.Humanoid;
using Content.Shared.Agony;
using Robust.Shared.Audio;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Bed.Sleep;

namespace Content.Server.Agony;

public sealed class AgonyScreamerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly SoundCollectionSpecifier _maleAgony = new("AgonyMale");
    private readonly SoundCollectionSpecifier _femaleAgony = new("AgonyFemale");
    
    private readonly SoundCollectionSpecifier _maleFire = new("FireScreamMale");
    private readonly SoundCollectionSpecifier _femaleFire = new("FireScreamFemale");

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<MobStateComponent, BodyPartRemovedEvent>(OnPartRemoved);
        SubscribeLocalEvent<MobStateComponent, SurgeryStepEvent>(OnSurgeryStep);
        SubscribeLocalEvent<MobStateComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnPartRemoved(EntityUid uid, MobStateComponent component, ref BodyPartRemovedEvent args)
    {
        TryPlayAgonyScream(uid, component, false);
    }

    private void OnSurgeryStep(EntityUid uid, MobStateComponent component, ref SurgeryStepEvent args)
    {
        // Только пациент кричит, а не хирург!
        if (uid != args.Body)
            return;

        TryPlayAgonyScream(uid, component, false);
    }

    private void OnDamageChanged(EntityUid uid, MobStateComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

        bool isFire = false;
        bool isPoison = false;

        if (TryComp<FlammableComponent>(uid, out var flammable) && flammable.OnFire)
        {
            isFire = true;
        }

        if (args.DamageDelta.DamageDict.TryGetValue("Poison", out var poisonDmg) && poisonDmg > 0)
        {
            isPoison = true;
        }

        if (isFire)
        {
            TryPlayAgonyScream(uid, component, true);
        }
        else if (isPoison)
        {
            TryPlayAgonyScream(uid, component, false);
        }
    }

    private void TryPlayAgonyScream(EntityUid uid, MobStateComponent mobState, bool isFire)
    {
        if (TerminatingOrDeleted(uid))
            return;

        // Только люди (гуманоиды) могут кричать в агонии, животные не должны.
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        // Человек без сознания (в крите) или мертвый не кричит
        if (mobState.CurrentState != MobState.Alive && mobState.CurrentState != MobState.Critical)
            return;

        // Если человек спит (например, под N2O наркозом), он не чувствует боли
        if (HasComp<SleepingComponent>(uid))
            return;

        EnsureComp<AgonyScreamerComponent>(uid, out var screamer);

        var curTime = _timing.CurTime;
        if (curTime < screamer.NextScream)
            return;

        screamer.NextScream = curTime + screamer.ScreamCooldown + TimeSpan.FromSeconds(_random.NextFloat(-1f, 1f));

        SoundSpecifier soundToPlay = isFire ? _maleFire : _maleAgony;

        if (humanoid.Sex == Sex.Female)
            soundToPlay = isFire ? _femaleFire : _femaleAgony;

        _popup.PopupEntity($"кричит в агонии!", uid, uid, PopupType.LargeCaution);
        
        var name = Identity.Name(uid, EntityManager);
        _popup.PopupEntity($"{name} кричит в агонии!", uid, Filter.PvsExcept(uid), true, PopupType.LargeCaution);

        _audio.PlayPvs(soundToPlay, uid);
    }
}
