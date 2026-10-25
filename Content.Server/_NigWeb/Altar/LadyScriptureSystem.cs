// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Goobstation.Common.Religion;
using Content.Server.Bible;
using Content.Server.Popups;
using Content.Shared._NigWeb.Altar;
using Content.Shared.Bible.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._NigWeb.Altar;

/// <summary>
/// Handles liturgical reading and miraculous healing from the Sacred Scripture of Our Lady.
/// </summary>
public sealed class LadyScriptureSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UseDelaySystem _delay = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    private readonly string[] _latinVerses = new[]
    {
        "«Per vulnera Eius sanati sumus!»",
        "«Caro dividitur, anima servatur!»",
        "«Cruor pro Matre Carnificinae effunditur, vita redit!»",
        "«Dolor est veritas, sanguis est salus!»"
    };

    private readonly string[] _chantFiles = new[]
    {
        "/Audio/_NigWeb/Voice/Thanati/ordo_chant_1.ogg",
        "/Audio/_NigWeb/Voice/Thanati/ordo_chant_2.ogg",
        "/Audio/_NigWeb/Voice/Thanati/ordo_chant_3.ogg",
        "/Audio/_NigWeb/Voice/Thanati/ordo_chant_4.ogg",
        "/Audio/_NigWeb/Voice/Thanati/ordo_chant_5.ogg"
    };

    private readonly List<int> _chantBag = new();
    private int _lastChantIndex = -1;

    public SoundPathSpecifier GetNextChantSound()
    {
        if (_chantBag.Count == 0)
        {
            var indices = Enumerable.Range(0, _chantFiles.Length).ToList();
            _random.Shuffle(indices);

            // Guarantee that the first sound of the new cycle is not identical to the last sound of previous cycle
            if (_chantFiles.Length > 1 && indices[0] == _lastChantIndex)
            {
                var swapIdx = _random.Next(1, indices.Count);
                (indices[0], indices[swapIdx]) = (indices[swapIdx], indices[0]);
            }

            _chantBag.AddRange(indices);
        }

        var picked = _chantBag[0];
        _chantBag.RemoveAt(0);
        _lastChantIndex = picked;

        Log.Debug($"[LadyScriptureSystem] Picked non-repeating chant sound: {_chantFiles[picked]} (index {picked})");
        return new SoundPathSpecifier(_chantFiles[picked]);
    }

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[LadyScriptureSystem] Initializing LadyScriptureSystem...");
        SubscribeLocalEvent<LadyScriptureComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MobStateComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(EntityUid uid, MobStateComponent component, ref EmoteEvent args)
    {
        if (args.Handled || args.Emote.ID != "PrayLady")
            return;

        Log.Debug($"[LadyScriptureSystem] Entity {ToPrettyString(uid)} invoked PrayLady emote!");
        var sound = GetNextChantSound();
        _audio.PlayPvs(sound, uid);
        args.Handled = true;
    }

    private void OnAfterInteract(EntityUid uid, LadyScriptureComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null)
            return;

        var target = args.Target.Value;
        var user = args.User;

        Log.Debug($"[LadyScriptureSystem] User {ToPrettyString(user)} interacting with target {ToPrettyString(target)} using scripture {ToPrettyString(uid)}");

        if (TryComp<UseDelayComponent>(uid, out var delay) && _delay.IsDelayed((uid, delay)))
        {
            _popup.PopupEntity("Священное Писание еще хранит жар недавней молитвы...", uid, user, PopupType.SmallCaution);
            return;
        }

        // Only works on living mob
        if (!TryComp<MobStateComponent>(target, out var mobState) || !_mobState.IsAlive(target, mobState))
        {
            _popup.PopupEntity("Молитвы за усопших возносятся только перед Алтарем Матери Бойни.", uid, user, PopupType.SmallCaution);
            return;
        }

        // Check if user is an ordained servant (has BibleUserComponent)
        if (!HasComp<BibleUserComponent>(user))
        {
            Log.Warning($"[LadyScriptureSystem] Unordained user {ToPrettyString(user)} attempted to read scripture {ToPrettyString(uid)}");
            _popup.PopupEntity("«Глаза твои недостойны Истины!» Страницы обжигают твои руки священным огнем!", user, user, PopupType.LargeCaution);
            _audio.PlayPvs(component.BackfireSound, user);
            _damageable.TryChangeDamage(user, component.BackfireDamage, true, origin: uid);
            if (delay != null)
                _delay.TryResetDelay((uid, delay));
            return;
        }

        // Target has damageable - heal wounds!
        if (TryComp<DamageableComponent>(target, out var targetDamage))
        {
            var verse = _random.Pick(_latinVerses);
            _popup.PopupEntity(verse, user, PopupType.Medium);
            _popup.PopupEntity($"Вы прикладываете Писание к ранам {ToPrettyString(target)}: плоть сшивается, кровь запекается!", target, user, PopupType.Medium);

            var chantSound = GetNextChantSound();
            _audio.PlayPvs(chantSound, user);
            _damageable.TryChangeDamage(target, component.HealDamage, true, origin: uid);

            // Reduce bleeding
            if (TryComp<BloodstreamComponent>(target, out var bloodstream))
            {
                _bloodstream.TryModifyBleedAmount((target, bloodstream), -5f);
            }

            if (delay != null)
                _delay.TryResetDelay((uid, delay));

            args.Handled = true;
            Log.Info($"[LadyScriptureSystem] User {ToPrettyString(user)} healed {ToPrettyString(target)} with scripture. Verse: {verse}");
        }
    }
}
