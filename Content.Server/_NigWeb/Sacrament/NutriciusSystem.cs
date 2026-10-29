// SPDX-License-Identifier: MIT
using System;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Popups;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared._NigWeb.Sacrament;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._NigWeb.Sacrament;

/// <summary>
/// Controls the Sorrow Patriarch (Вскормитель Скорби).
/// Thaws upon receiving the warmth of sacrificial blood from an incised palm,
/// allowing containers (chalices, bottles, beakers) to harvest healing Seraphic Wax reagent.
/// Wax pool holds up to 100u, regenerating over 10 minutes, with a per-extraction cooldown.
/// When harvested, plays deep sorrowful voice lines from the Patriarch.
/// </summary>
public sealed class NutriciusSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[NutriciusSystem] Initializing NutriciusSystem...");

        SubscribeLocalEvent<NutriciusComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<NutriciusComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<NutriciusComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NutriciusComponent>();
        while (query.MoveNext(out var uid, out var nutricius))
        {
            // Passive wax pool regeneration (100u over 10 minutes / 600s)
            if (nutricius.CurrentWaxPool < nutricius.MaxWaxPool)
            {
                var regenAmount = (nutricius.MaxWaxPool / nutricius.WaxRegenTime) * frameTime;
                nutricius.CurrentWaxPool = MathF.Min(nutricius.MaxWaxPool, nutricius.CurrentWaxPool + regenAmount);
            }

            if (!nutricius.IsWarm)
                continue;

            nutricius.WarmthTimeLeft -= frameTime;
            if (nutricius.WarmthTimeLeft <= 0f)
            {
                Log.Info($"[NutriciusSystem] Sorrow Patriarch {uid} cooled back down to stone.");
                nutricius.IsWarm = false;
                nutricius.WarmthTimeLeft = 0f;
                Dirty(uid, nutricius);

                _popup.PopupEntity(Loc.GetString("sacrament-nutricius-cooled"), uid, PopupType.MediumCaution);
            }
        }
    }

    private void OnInteractHand(Entity<NutriciusComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var uid = ent.Owner;
        var comp = ent.Comp;
        var user = args.User;

        Log.Debug($"[NutriciusSystem] Hand interaction on {uid} by {user}");

        if (TryComp<IncisedPalmComponent>(user, out _))
        {
            Log.Info($"[NutriciusSystem] User {user} warming {uid} with incised palm!");

            // Consume sacrificial cut
            RemCompDeferred<IncisedPalmComponent>(user);

            // Warm up
            comp.IsWarm = true;
            comp.WarmthTimeLeft = comp.WarmthDuration;
            Dirty(uid, comp);

            // Sounds
            _audio.PlayPvs(comp.SizzleSound, uid);
            _audio.PlayPvs(comp.WarmthSound, uid);

            _popup.PopupEntity(Loc.GetString("sacrament-nutricius-warmed-user"), uid, user, PopupType.LargeCaution);
            args.Handled = true;
            return;
        }

        // Without incised palm, just touching the frozen flesh
        if (comp.IsWarm)
        {
            _popup.PopupEntity(Loc.GetString("sacrament-nutricius-warm-touch"), uid, user, PopupType.Small);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("sacrament-nutricius-cold-touch"), uid, user, PopupType.MediumCaution);
        }

        args.Handled = true;
    }

    private void OnInteractUsing(Entity<NutriciusComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var uid = ent.Owner;
        var comp = ent.Comp;
        var used = args.Used;
        var user = args.User;

        if (!_solutionContainer.TryGetRefillableSolution(used, out var solEnt, out var refillSol))
            return;

        Log.Debug($"[NutriciusSystem] Container {used} used on {uid} by {user}");

        if (!comp.IsWarm)
        {
            _popup.PopupEntity(Loc.GetString("sacrament-nutricius-is-cold"), uid, user, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var curTime = _timing.CurTime;
        if (curTime < comp.NextGatherTime)
        {
            Log.Debug($"[NutriciusSystem] Extraction cooldown active on {uid} for {user}");
            _popup.PopupEntity(Loc.GetString("sacrament-nutricius-gather-cooldown"), uid, user, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (comp.CurrentWaxPool < 5f)
        {
            _popup.PopupEntity(Loc.GetString("sacrament-nutricius-depleted"), uid, user, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var containerName = Identity.Name(used, EntityManager);
        if (refillSol.AvailableVolume <= 0)
        {
            _popup.PopupEntity(Loc.GetString("sacrament-nutricius-container-full", ("container", containerName)), uid, user, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var maxTake = FixedPoint2.Min(FixedPoint2.New(20), FixedPoint2.New((int)comp.CurrentWaxPool));
        var amount = FixedPoint2.Min(refillSol.AvailableVolume, maxTake);
        if (amount <= 0)
        {
            args.Handled = true;
            return;
        }

        _solutionContainer.TryAddReagent(solEnt.Value, "SeraphicWax", amount, out _);

        comp.CurrentWaxPool -= (float)amount;
        comp.NextGatherTime = curTime + TimeSpan.FromSeconds(comp.GatherCooldown);
        Dirty(uid, comp);

        // Play deep patriarch voice line and drip sound
        _audio.PlayPvs(comp.VoiceSound, uid);
        _audio.PlayPvs(comp.WaxDripSound, uid);

        _popup.PopupEntity(Loc.GetString("sacrament-nutricius-gather-wax",
            ("container", containerName),
            ("amount", amount)), uid, user, PopupType.Medium);

        Log.Info($"[NutriciusSystem] Gathered {amount}u SeraphicWax into {used} from {uid}. Remaining pool: {comp.CurrentWaxPool:F1}/{comp.MaxWaxPool}");
        args.Handled = true;
    }

    private void OnExamined(Entity<NutriciusComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        if (comp.IsWarm)
        {
            args.PushMarkup(Loc.GetString("sacrament-nutricius-examined-warm",
                ("pool", (int)comp.CurrentWaxPool),
                ("max", (int)comp.MaxWaxPool),
                ("seconds", (int)comp.WarmthTimeLeft)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("sacrament-nutricius-examined-cold",
                ("pool", (int)comp.CurrentWaxPool),
                ("max", (int)comp.MaxWaxPool)));
        }
    }
}
