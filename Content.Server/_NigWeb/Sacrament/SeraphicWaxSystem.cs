// SPDX-License-Identifier: MIT
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Server._NigWeb.BloodSplatter;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared._NigWeb.Sacrament;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;

namespace Content.Server._NigWeb.Sacrament;

/// <summary>
/// System handling Seraphic Wax (Священный Воск).
/// Blocks ingesting/drinking the wax from any container.
/// Allows applying wax directly from any container (chalice, beaker, bottle, etc.) to wounds:
/// - Consumes wax scaling dynamically with bleeding severity (2.5u wax per 1 level of bleeding).
/// - If the target is NOT bleeding, consumes 0 wax and notifies the player.
/// - Cauterizes bleeding wounds, knits flesh, and sizzles with holy heat.
/// </summary>
public sealed class SeraphicWaxSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public static readonly string WaxReagentId = "SeraphicWax";
    public static readonly FixedPoint2 MinWaxToTreat = FixedPoint2.New(1);

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[SeraphicWaxSystem] Initializing SeraphicWaxSystem...");

        SubscribeLocalEvent<SolutionContainerManagerComponent, EdibleEvent>(OnEdible);
        SubscribeLocalEvent<SolutionContainerManagerComponent, BeforeIngestedEvent>(OnBeforeIngested);

        SubscribeLocalEvent<SolutionContainerManagerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SolutionContainerManagerComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnEdible(Entity<SolutionContainerManagerComponent> ent, ref EdibleEvent args)
    {
        if (args.Cancelled)
            return;

        foreach (var (_, solution) in _solutionContainer.EnumerateSolutions((ent.Owner, ent.Comp)))
        {
            if (solution.Comp.Solution.GetTotalPrototypeQuantity(WaxReagentId) > 0)
            {
                Log.Debug($"[SeraphicWaxSystem] Denied drinking {ent.Owner} containing {WaxReagentId} by {args.User}");
                args.Cancelled = true;
                _popup.PopupEntity(Loc.GetString("sacrament-wax-cannot-drink"), ent.Owner, args.User, PopupType.SmallCaution);
                return;
            }
        }
    }

    private void OnBeforeIngested(Entity<SolutionContainerManagerComponent> ent, ref BeforeIngestedEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Solution != null && args.Solution.GetTotalPrototypeQuantity(WaxReagentId) > 0)
        {
            Log.Debug($"[SeraphicWaxSystem] Cancelled BeforeIngestedEvent on {ent.Owner} due to {WaxReagentId}");
            args.Cancelled = true;
        }
    }

    private void OnUseInHand(Entity<SolutionContainerManagerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!HasSeraphicWax(ent.Owner, out var solEnt, out var waxAmount))
            return;

        Log.Debug($"[SeraphicWaxSystem] Container {ent.Owner} used in hand by {args.User} with {waxAmount}u wax");
        if (TryApplyWaxToTarget(ent.Owner, solEnt, waxAmount, args.User, args.User))
        {
            args.Handled = true;
        }
    }

    private void OnAfterInteract(Entity<SolutionContainerManagerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!HasSeraphicWax(ent.Owner, out var solEnt, out var waxAmount))
            return;

        var target = args.Target ?? args.User;

        // Do not intercept interaction with Patriarch itself (used for gathering wax)
        if (HasComp<NutriciusComponent>(target))
            return;

        // Only damageable entities can be treated
        if (!HasComp<DamageableComponent>(target))
            return;

        Log.Debug($"[SeraphicWaxSystem] Container {ent.Owner} after-interact on target {target} by {args.User}");
        if (TryApplyWaxToTarget(ent.Owner, solEnt, waxAmount, args.User, target))
        {
            args.Handled = true;
        }
    }

    public bool TryApplyWaxToTarget(
        EntityUid containerUid,
        Entity<SolutionComponent> solEnt,
        FixedPoint2 availableWax,
        EntityUid user,
        EntityUid target)
    {
        Log.Info($"[SeraphicWaxSystem] Attempting to treat {target} with wax from {containerUid} by {user}...");

        if (!target.IsValid() || EntityManager.IsQueuedForDeletion(target))
        {
            Log.Error($"[SeraphicWaxSystem] Target {target} is invalid or queued for deletion.");
            return false;
        }

        // 1. Check total bleed severity
        var totalBleed = GetTotalBleedSeverity(target);
        if (totalBleed <= 0.05f)
        {
            Log.Debug($"[SeraphicWaxSystem] Target {target} has no active bleeding wounds (severity {totalBleed}). Consuming no wax.");
            _popup.PopupEntity(Loc.GetString("sacrament-wax-no-bleeding"), target, user, PopupType.SmallCaution);
            return true; // Handled so container doesn't do normal interact/splash
        }

        // 2. Check if container has at least minimum required wax
        if (availableWax < MinWaxToTreat)
        {
            Log.Debug($"[SeraphicWaxSystem] Container {containerUid} has only {availableWax}u wax; required minimum is {MinWaxToTreat}u.");
            _popup.PopupEntity(Loc.GetString("sacrament-wax-not-enough"), containerUid, user, PopupType.SmallCaution);
            return true;
        }

        // 3. Dynamic wax calculation:
        // 2.5 units of wax per 1 level of bleeding (5u = 2 levels).
        // The more severe the bleeding, the more wax is consumed to seal it.
        var desiredWaxFloat = MathF.Max(1.0f, totalBleed * 2.5f);
        var desiredWax = FixedPoint2.New(MathF.Ceiling(desiredWaxFloat));

        var waxToConsume = FixedPoint2.Min(availableWax, desiredWax);
        if (waxToConsume < MinWaxToTreat)
            waxToConsume = MinWaxToTreat;
        if (waxToConsume > availableWax)
            waxToConsume = availableWax;

        Log.Info($"[SeraphicWaxSystem] Target {target} has bleed severity {totalBleed:F2}. Consuming {waxToConsume}u wax (desired {desiredWax}u, available {availableWax}u).");
        _solutionContainer.RemoveReagent(solEnt, WaxReagentId, waxToConsume);

        // 4. Calculate bleed reduction from consumed wax:
        // 2.5u wax = 1.0f bleed reduction
        var bleedReduction = waxToConsume.Float() / 2.5f;

        // Reduce bloodstream bleed
        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            Log.Debug($"[SeraphicWaxSystem] Reducing bloodstream bleed ({bloodstream.BleedAmount}) by {bleedReduction} on {target}");
            _bloodstream.TryModifyBleedAmount((target, bloodstream), -bleedReduction);
        }

        // Heal most severe bleeding wounds
        if (TryComp<BodyComponent>(target, out var bodyComp))
        {
            _wounds.TryHealMostSevereBleedingWoundables(target, bleedReduction, out _, bodyComp);
        }
        else if (TryComp<WoundableComponent>(target, out var rootWoundable))
        {
            _wounds.TryHealBleedingWounds(target, -bleedReduction, out _, rootWoundable);
        }

        // Check if all bleeding is now fully arrested
        var stillBleeding = IsBleeding(target);
        if (!stillBleeding && HasComp<ArterialBleedSpurtComponent>(target))
        {
            Log.Debug($"[SeraphicWaxSystem] Bleeding fully stopped on {target}, removing ArterialBleedSpurtComponent");
            RemCompDeferred<ArterialBleedSpurtComponent>(target);
        }

        // 5. Scaled flesh healing
        if (TryComp<DamageableComponent>(target, out _))
        {
            var healScale = waxToConsume.Float() / 5.0f;
            var healDamage = new DamageSpecifier();
            healDamage.DamageDict["Slash"] = -Math.Max(1, (int)(12 * healScale));
            healDamage.DamageDict["Piercing"] = -Math.Max(1, (int)(10 * healScale));
            healDamage.DamageDict["Heat"] = Math.Max(1, (int)(2 * healScale));
            _damageable.TryChangeDamage(target, healDamage, true, origin: user);
        }

        // 6. Sound & Visual Feedback
        _audio.PlayPvs(new SoundCollectionSpecifier("NigWebWaxSizzle"), target);

        if (!stillBleeding)
        {
            if (user == target)
            {
                _popup.PopupEntity(Loc.GetString("sacrament-wax-apply-self-stopped"), target, user, PopupType.Medium);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("sacrament-wax-apply-target-stopped", ("target", target)), target, user, PopupType.Medium);
                _popup.PopupEntity(Loc.GetString("sacrament-wax-applied-by-other-stopped", ("user", user)), target, target, PopupType.Medium);
            }
        }
        else
        {
            if (user == target)
            {
                _popup.PopupEntity(Loc.GetString("sacrament-wax-apply-self-reduced"), target, user, PopupType.Medium);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("sacrament-wax-apply-target-reduced", ("target", target)), target, user, PopupType.Medium);
                _popup.PopupEntity(Loc.GetString("sacrament-wax-applied-by-other-reduced", ("user", user)), target, target, PopupType.Medium);
            }
        }

        Log.Info($"[SeraphicWaxSystem] Successfully treated wounds on {target} using {waxToConsume}u SeraphicWax. Bleed reduced by {bleedReduction:F2} levels. Still bleeding: {stillBleeding}");
        return true;
    }

    public float GetTotalBleedSeverity(EntityUid target)
    {
        float severity = 0f;

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            severity = MathF.Max(severity, bloodstream.BleedAmount);
        }

        if (TryComp<BodyComponent>(target, out var bodyComp))
        {
            float woundsBleed = 0f;
            foreach (var (partId, _) in _body.GetBodyChildren(target, bodyComp))
            {
                foreach (var wound in _wounds.GetWoundableWounds(partId))
                {
                    if (TryComp<BleedInflicterComponent>(wound, out var bleeds) && bleeds.IsBleeding)
                        woundsBleed += bleeds.BleedingAmount.Float();
                }
            }
            severity = MathF.Max(severity, woundsBleed);
        }
        else if (TryComp<WoundableComponent>(target, out _))
        {
            float woundsBleed = 0f;
            foreach (var wound in _wounds.GetWoundableWounds(target))
            {
                if (TryComp<BleedInflicterComponent>(wound, out var bleeds) && bleeds.IsBleeding)
                    woundsBleed += bleeds.BleedingAmount.Float();
            }
            severity = MathF.Max(severity, woundsBleed);
        }

        if (HasComp<ArterialBleedSpurtComponent>(target))
        {
            severity = MathF.Max(severity, 3.0f);
        }

        return severity;
    }

    public bool IsBleeding(EntityUid target)
    {
        return GetTotalBleedSeverity(target) > 0.05f;
    }

    public bool HasSeraphicWax(EntityUid uid, out Entity<SolutionComponent> solEnt, out FixedPoint2 waxAmount)
    {
        solEnt = default;
        waxAmount = FixedPoint2.Zero;

        if (_solutionContainer.TryGetRefillableSolution(uid, out var refillEnt, out var refillSol))
        {
            var amount = refillSol.GetTotalPrototypeQuantity(WaxReagentId);
            if (amount > 0)
            {
                solEnt = refillEnt.Value;
                waxAmount = amount;
                return true;
            }
        }

        if (TryComp<SolutionContainerManagerComponent>(uid, out var manager))
        {
            foreach (var (_, solution) in _solutionContainer.EnumerateSolutions((uid, manager)))
            {
                var amount = solution.Comp.Solution.GetTotalPrototypeQuantity(WaxReagentId);
                if (amount > 0)
                {
                    solEnt = solution;
                    waxAmount = amount;
                    return true;
                }
            }
        }

        return false;
    }
}
