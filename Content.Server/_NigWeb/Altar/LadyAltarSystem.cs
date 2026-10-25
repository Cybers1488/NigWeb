// SPDX-License-Identifier: MIT

using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Popups;
using Content.Server._NigWeb.BloodSplatter;
using Content.Shared._NigWeb.Altar;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._NigWeb.Altar;

/// <summary>
/// Handles interactions and blood sacrifices at Our Lady of the Chalices altar.
/// </summary>
public sealed class LadyAltarSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly BloodSplatterSystem _bloodSplatter = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[LadyAltarSystem] Initializing LadyAltarSystem...");

        SubscribeLocalEvent<LadyAltarComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LadyAltarComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<LadyAltarComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<LadyAltarComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<LadyAltarComponent, DragDropTargetEvent>(OnDragDropTarget);
    }

    private void OnMapInit(EntityUid uid, LadyAltarComponent component, MapInitEvent args)
    {
        Log.Info($"[LadyAltarSystem] MapInit for Lady Altar {ToPrettyString(uid)}");
        UpdateStageAppearance(uid, component);
    }

    private void OnExamined(EntityUid uid, LadyAltarComponent component, ExaminedEvent args)
    {
        Log.Debug($"[LadyAltarSystem] Examining Lady Altar {ToPrettyString(uid)} by {ToPrettyString(args.Examiner)}");

        args.PushMarkup("[bold]Mater Carnificinae (Матерь Кровавой Бойни)[/bold]");
        args.PushMarkup("Колоссальное изваяние из священного фарфора. Она внемлет мольбам и жаждет евхаристии мученической сукровицы, отсеченных дланей и искромсанной плоти.");

        string stageDesc = component.CurrentStage switch
        {
            1 => "Лик и стан чисты, словно нетронутый белый фарфор.",
            2 => "Фарфоровая кожа покрыта багровыми кровоточащими трещинами.",
            3 => "Кожа с треском отслаивается с лица и шеи, открывая трепещущую плоть.",
            4 => "Кожа содрана пластами! Наружу проступают пульсирующие алые мышцы.",
            5 => "Кожа полностью содрана! Ее чело венчает сияющий золотой венец мученицы.",
            _ => "Изваяние неподвижно.",
        };

        args.PushMarkup($"[color=crimson]{stageDesc}[/color]");

        var percent = ((component.CurrentProgress / component.MaxProgress).Float() * 100f);
        args.PushMarkup($"Стадия жертвы: [bold]{component.CurrentStage}/5[/bold] (Кровь: {component.CurrentProgress} / {component.MaxProgress} — {percent:F0}%)");
    }

    private void OnInteractUsing(EntityUid uid, LadyAltarComponent component, InteractUsingEvent args)
    {
        Log.Debug($"[LadyAltarSystem] InteractUsing on {ToPrettyString(uid)} using {ToPrettyString(args.Used)} by {ToPrettyString(args.User)}");

        if (args.Handled)
            return;

        // 1. Check if holding a severed body part or organ
        if (TrySacrificeItem(uid, component, args.Used, args.User))
        {
            args.Handled = true;
            return;
        }

        // 2. Check if holding a container with blood
        if (_solutionContainer.TryGetDrainableSolution(args.Used, out var drainSoln, out var drainSolution))
        {
            var bloodQuantity = drainSolution.GetTotalPrototypeQuantity("Blood");
            if (bloodQuantity > FixedPoint2.Zero)
            {
                var removed = _solutionContainer.RemoveReagent(drainSoln.Value, "Blood", bloodQuantity);
                var points = removed * component.BloodPerUnit;

                _popup.PopupEntity($"Вы вылили {removed} ед. крови в Calix Carnificinae...", uid, args.User, PopupType.MediumCaution);
                PlaySacrificeEffects(uid, component);
                AddProgress(uid, component, points, args.User);
                args.Handled = true;
                return;
            }
            else
            {
                _popup.PopupEntity("В сосуде нет крови для приношения Матери Бойни.", uid, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }
        }
    }

    private void OnCanDropTarget(EntityUid uid, LadyAltarComponent component, ref CanDropTargetEvent args)
    {
        if (HasComp<BodyPartComponent>(args.Dragged) ||
            HasComp<OrganComponent>(args.Dragged) ||
            HasComp<BloodstreamComponent>(args.Dragged) ||
            HasComp<MobStateComponent>(args.Dragged))
        {
            args.CanDrop = true;
            args.Handled = true;
        }
    }

    private void OnDragDropTarget(EntityUid uid, LadyAltarComponent component, ref DragDropTargetEvent args)
    {
        Log.Debug($"[LadyAltarSystem] DragDropTarget on {ToPrettyString(uid)} dragged {ToPrettyString(args.Dragged)} by {ToPrettyString(args.User)}");

        if (args.Handled)
            return;

        // Try item sacrifice (body part or organ)
        if (TrySacrificeItem(uid, component, args.Dragged, args.User))
        {
            args.Handled = true;
            return;
        }

        // Try corpse sacrifice
        if (TrySacrificeCorpse(uid, component, args.Dragged, args.User))
        {
            args.Handled = true;
            return;
        }
    }

    private bool TrySacrificeItem(EntityUid altarUid, LadyAltarComponent comp, EntityUid itemUid, EntityUid user)
    {
        if (TryComp<BodyPartComponent>(itemUid, out var bodyPart))
        {
            FixedPoint2 points = (bodyPart.PartType == BodyPartType.Hand) ? comp.HandBonus : comp.LimbBonus;
            string msg = (bodyPart.PartType == BodyPartType.Hand)
                ? "Вы возложили отсеченную длань на алтарь. Пальцы рассыпаются в священный багровый пепел..."
                : "Вы бросили отсеченную плоть в Calix Carnificinae. Плоть с шипением растворяется...";

            _popup.PopupEntity(msg, altarUid, user, PopupType.MediumCaution);
            PlaySacrificeEffects(altarUid, comp);
            QueueDel(itemUid);
            AddProgress(altarUid, comp, points, user);
            return true;
        }

        if (TryComp<OrganComponent>(itemUid, out var organ))
        {
            _popup.PopupEntity("Вы принесли в жертву трепещущий орган. Сукровица вскипает в Calix Carnificinae...", altarUid, user, PopupType.MediumCaution);
            PlaySacrificeEffects(altarUid, comp);
            QueueDel(itemUid);
            AddProgress(altarUid, comp, comp.OrganBonus, user);
            return true;
        }

        return false;
    }

    private bool TrySacrificeCorpse(EntityUid altarUid, LadyAltarComponent comp, EntityUid corpseUid, EntityUid user)
    {
        if (TryComp<MobStateComponent>(corpseUid, out var mobState))
        {
            if (mobState.CurrentState != MobState.Dead)
            {
                _popup.PopupEntity("Жертва еще дышит! Умертвите её перед приношением или отсеките плоть!", altarUid, user, PopupType.MediumCaution);
                return true;
            }

            FixedPoint2 points = FixedPoint2.New(100);
            if (TryComp<BloodstreamComponent>(corpseUid, out var bloodstream))
            {
                if (bloodstream.BloodSolution != null)
                {
                    var bloodVol = bloodstream.BloodSolution.Value.Comp.Solution.Volume;
                    points += bloodVol * comp.BloodPerUnit;
                }

                _bloodSplatter.SplatterBlood(altarUid, bloodstream, 4, 1.5f, false);
            }

            _popup.PopupEntity("Вы принесли безжизненное тело в жертву Матери Бойни! Кровь хлынула в чаши казни...", altarUid, user, PopupType.LargeCaution);
            PlaySacrificeEffects(altarUid, comp);
            QueueDel(corpseUid);
            AddProgress(altarUid, comp, points, user);
            return true;
        }

        return false;
    }

    private void PlaySacrificeEffects(EntityUid uid, LadyAltarComponent comp)
    {
        _audio.PlayPvs(comp.OfferingSound, uid);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Blood/squelch1.ogg"), uid);
    }

    public void AddProgress(EntityUid uid, LadyAltarComponent comp, FixedPoint2 points, EntityUid? user)
    {
        Log.Info($"[LadyAltarSystem] Adding {points} progress to Lady Altar {ToPrettyString(uid)} (current: {comp.CurrentProgress}/{comp.MaxProgress})");

        comp.CurrentProgress = FixedPoint2.Clamp(comp.CurrentProgress + points, FixedPoint2.Zero, comp.MaxProgress);

        var ratio = (comp.CurrentProgress / comp.MaxProgress).Float();
        int newStage = 1;
        if (ratio >= 1.0f)
            newStage = 5;
        else if (ratio >= 0.75f)
            newStage = 4;
        else if (ratio >= 0.50f)
            newStage = 3;
        else if (ratio >= 0.25f)
            newStage = 2;
        else
            newStage = 1;

        Dirty(uid, comp);

        if (newStage > comp.CurrentStage)
        {
            comp.CurrentStage = newStage;
            Dirty(uid, comp);
            UpdateStageAppearance(uid, comp);
            OnStageAdvanced(uid, comp, newStage);
        }
    }

    private void UpdateStageAppearance(EntityUid uid, LadyAltarComponent comp)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.SetData(uid, LadyAltarVisuals.Stage, comp.CurrentStage, appearance);
        }
    }

    private void OnStageAdvanced(EntityUid uid, LadyAltarComponent comp, int stage)
    {
        Log.Info($"[LadyAltarSystem] Stage advanced to {stage} on {ToPrettyString(uid)}!");

        if (stage == 5)
        {
            _audio.PlayPvs(comp.AscensionSound, uid, AudioParams.Default.WithVolume(4f));
            _popup.PopupEntity("МАТЕРЬ КРОВАВОЙ БОЙНИ ПОЛНОСТЬЮ СОДРАЛА СВОЙ САВАН! ЗОЛОТОЙ ВЕНЕЦ МУЧЕНИЦЫ ВОССИЯЛ НАД ПУСТОШЬЮ!", uid, PopupType.LargeCaution);
        }
        else
        {
            _audio.PlayPvs(comp.StageAdvanceSound, uid, AudioParams.Default.WithVolume(2f));
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Blood/squelch1.ogg"), uid);

            string stageMsg = stage switch
            {
                2 => "Фарфоровый саван Матери Бойни покрывается глубокими багровыми трещинами...",
                3 => "Саван Матери Бойни с треском отслаивается с лица и шеи, обнажая трепещущую плоть!",
                4 => "Покров срывается пластами! Наружу проступают пульсирующие алые мышцы!",
                _ => "Матерь Бойни содрогается от благодати принятых жертв...",
            };
            _popup.PopupEntity(stageMsg, uid, PopupType.LargeCaution);
        }
    }
}
