// SPDX-License-Identifier: MIT

using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Popups;
using Content.Shared._NigWeb.Altar;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._NigWeb.Altar;

/// <summary>
/// Handles blood and body part offerings into the Holy Chalices of Our Lady.
/// Accepts hands, feet, legs, arms, heads, organs, and liquid blood (torso is rejected).
/// Feeds the Lady Altar with blood to drive her skin flaying.
/// </summary>
public sealed class ChaliceSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly LadyAltarSystem _ladyAltar = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[ChaliceSystem] Initializing ChaliceSystem...");

        SubscribeLocalEvent<ChaliceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChaliceComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ChaliceComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnMapInit(EntityUid uid, ChaliceComponent component, MapInitEvent args)
    {
        Log.Info($"[ChaliceSystem] MapInit for Chalice {ToPrettyString(uid)}");
        UpdateAppearance(uid, component);
    }

    private void OnExamined(EntityUid uid, ChaliceComponent component, ExaminedEvent args)
    {
        Log.Debug($"[ChaliceSystem] Examining Chalice {ToPrettyString(uid)} by {ToPrettyString(args.Examiner)}");

        args.PushMarkup("[bold]Calix Carnificinae (Чаша Мясницкой Казни)[/bold]");
        args.PushMarkup("Тяжелая мраморная чаша для сбора мученической сукровицы и растерзанной плоти. Принимает отсеченные длани, стопы, головы, конечности и кровь грешников (туловище слишком велико и не поместится).");

        string stateDesc = component.FillState switch
        {
            ChaliceFillState.Empty => "Чаша пуста и суха.",
            ChaliceFillState.Quarter => "На дне чаши плещутся багровые сгустки мученической сукровицы.",
            ChaliceFillState.Half => "Чаша наполовину заполнена густой запекшейся кровью и кусками плоти.",
            ChaliceFillState.Full => "Calix Carnificinae переполнен кровавой сукровицей и плотью до самых краев!",
            ChaliceFillState.Broken => "Чаша расколота.",
            _ => "Чаша неподвижна.",
        };

        args.PushMarkup($"[color=crimson]{stateDesc}[/color]");

        var percent = ((component.CurrentProgress / component.MaxProgress).Float() * 100f);
        args.PushMarkup($"Наполнение: [bold]{component.CurrentProgress} / {component.MaxProgress}[/bold] ({percent:F0}%)");
    }

    private void OnInteractUsing(EntityUid uid, ChaliceComponent component, InteractUsingEvent args)
    {
        Log.Debug($"[ChaliceSystem] InteractUsing on {ToPrettyString(uid)} using {ToPrettyString(args.Used)} by {ToPrettyString(args.User)}");

        if (args.Handled)
            return;

        // 1. Check if offering a body part
        if (TryComp<BodyPartComponent>(args.Used, out var bodyPart))
        {
            // Reject torso / chest / groin as requested by user
            if (bodyPart.PartType == BodyPartType.Chest || bodyPart.PartType == BodyPartType.Groin)
            {
                _popup.PopupEntity("Туловище слишком велико и не помещается в Calix Carnificinae!", uid, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }

            FixedPoint2 points = component.LimbBonus;
            string msg = "Вы опустили отсеченную плоть в чашу...";

            if (bodyPart.PartType == BodyPartType.Hand)
            {
                points = component.HandBonus;
                msg = "Вы опустили отсеченную кисть в чашу. Священный багрянец жадно поглощает пальцы...";
            }
            else if (bodyPart.PartType == BodyPartType.Head)
            {
                points = component.HeadBonus;
                msg = "Вы погрузили отсеченную голову в Calix Carnificinae. Густая сукровица закипает во славу Матери Бойни...";
            }
            else if (bodyPart.PartType == BodyPartType.Foot || bodyPart.PartType == BodyPartType.Leg)
            {
                points = component.LimbBonus;
                msg = "Вы погрузили отсеченную ногу в чашу. Плоть растворяется в багровой пучине...";
            }
            else if (bodyPart.PartType == BodyPartType.Arm)
            {
                points = component.LimbBonus;
                msg = "Вы погрузили отсеченную руку в чашу...";
            }

            _popup.PopupEntity(msg, uid, args.User, PopupType.MediumCaution);
            PlayChaliceEffects(uid, component);
            QueueDel(args.Used);
            AddChaliceProgress(uid, component, points, args.User);
            args.Handled = true;
            return;
        }

        // 2. Check if offering an organ
        if (TryComp<OrganComponent>(args.Used, out var organ))
        {
            _popup.PopupEntity("Вы опустили орган в чашу. Он с шипением погружается на дно...", uid, args.User, PopupType.MediumCaution);
            PlayChaliceEffects(uid, component);
            QueueDel(args.Used);
            AddChaliceProgress(uid, component, component.OrganBonus, args.User);
            args.Handled = true;
            return;
        }

        // 3. Check if pouring liquid blood from container
        if (_solutionContainer.TryGetDrainableSolution(args.Used, out var drainSoln, out var drainSolution))
        {
            var bloodQuantity = drainSolution.GetTotalPrototypeQuantity("Blood");
            if (bloodQuantity > FixedPoint2.Zero)
            {
                var removed = _solutionContainer.RemoveReagent(drainSoln.Value, "Blood", bloodQuantity);
                var points = removed * component.BloodPerUnit;

                _popup.PopupEntity($"Вы вылили {removed} ед. крови в Calix Carnificinae...", uid, args.User, PopupType.MediumCaution);
                PlayChaliceEffects(uid, component);
                AddChaliceProgress(uid, component, points, args.User);
                args.Handled = true;
                return;
            }
            else
            {
                _popup.PopupEntity("В сосуде нет крови для наполнения Calix Carnificinae.", uid, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }
        }
    }

    private void PlayChaliceEffects(EntityUid uid, ChaliceComponent comp)
    {
        _audio.PlayPvs(comp.FillSound, uid);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Blood/squelch1.ogg"), uid);
        if (comp.ChantSound != null)
        {
            _audio.PlayPvs(comp.ChantSound, uid);
        }
    }

    private void AddChaliceProgress(EntityUid uid, ChaliceComponent comp, FixedPoint2 points, EntityUid user)
    {
        Log.Info($"[ChaliceSystem] Adding {points} progress to Chalice {ToPrettyString(uid)} (current: {comp.CurrentProgress}/{comp.MaxProgress})");

        comp.CurrentProgress = FixedPoint2.Clamp(comp.CurrentProgress + points, FixedPoint2.Zero, comp.MaxProgress);

        var ratio = (comp.CurrentProgress / comp.MaxProgress).Float();
        ChaliceFillState newState = ChaliceFillState.Empty;

        if (ratio >= 0.85f)
            newState = ChaliceFillState.Full;
        else if (ratio >= 0.50f)
            newState = ChaliceFillState.Half;
        else if (ratio >= 0.25f)
            newState = ChaliceFillState.Quarter;
        else
            newState = ChaliceFillState.Empty;

        bool justBecameFull = (newState == ChaliceFillState.Full && comp.FillState != ChaliceFillState.Full);
        comp.FillState = newState;
        Dirty(uid, comp);
        UpdateAppearance(uid, comp);

        if (justBecameFull)
        {
            _audio.PlayPvs(comp.FullSound, uid, AudioParams.Default.WithVolume(2f));
            _popup.PopupEntity("Calix Carnificinae до краев наполнился мученической сукровицей и плотью!", uid, PopupType.LargeCaution);
        }

        // Feed nearby or map Lady Altars to advance the flaying ritual
        var query = EntityQueryEnumerator<LadyAltarComponent>();
        while (query.MoveNext(out var ladyUid, out var ladyComp))
        {
            _ladyAltar.AddProgress(ladyUid, ladyComp, points, user);
        }
    }

    private void UpdateAppearance(EntityUid uid, ChaliceComponent comp)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.SetData(uid, ChaliceVisuals.FillState, comp.FillState, appearance);
        }
    }
}
