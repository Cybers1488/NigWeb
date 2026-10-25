// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Decals;
using Content.Server.Forensics;
using Content.Server.Popups;
using Content.Shared._NigWeb.BloodSplatter;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Decals;
using Content.Shared.Fluids;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Standing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._NigWeb.BloodSplatter;

public sealed class BloodyItemSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;



    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[BloodyItemSystem] Initiating BloodyItemSystem...");

        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<PullableComponent, MoveEvent>(OnPullableMove);
        SubscribeLocalEvent<StandingStateComponent, MoveEvent>(OnCrawlingMove);
        SubscribeLocalEvent<BloodyItemComponent, InteractUsingEvent>(OnCleanBloodyItem);
    }

    /// <summary>
    /// Gets the authentic blood color of a mob, respecting alien/synth reagents or defaulting to standard deep red #780000.
    /// </summary>
    public Color GetBloodColor(EntityUid uid)
    {
        if (_solutionContainer.TryGetSolution(uid, BloodstreamComponent.DefaultBloodSolutionName, out var soln, out var bloodSolution))
        {
            var solution = new Solution();
            foreach (var reagent in bloodSolution.Contents)
            {
                solution.AddReagent(reagent.Reagent, FixedPoint2.New(1));
                break;
            }
            if (solution.Volume > 0)
            {
                var reagentColor = solution.GetColor(_prototype);
                if (reagentColor.R > reagentColor.G + 0.1f && reagentColor.R > reagentColor.B + 0.1f)
                    return Color.FromHex("#780000");

                return reagentColor;
            }
        }

        return Color.FromHex("#780000");
    }

    /// <summary>
    /// Stains a single item or weapon with blood, adjusting its saturation level, color, and notifying clients.
    /// </summary>
    public void BloodyEntity(EntityUid item, float amount = 0.15f, Color? bloodColor = null)
    {
        if (!Exists(item) || Deleted(item) || Terminating(item))
            return;

        // Never mobs, only items, weapons, or clothing can be bloodied.
        if (HasComp<MobStateComponent>(item))
            return;

        if (!HasComp<ItemComponent>(item) && !HasComp<ClothingComponent>(item) && !HasComp<MeleeWeaponComponent>(item))
            return;

        var comp = EnsureComp<BloodyItemComponent>(item);
        comp.BloodLevel = Math.Clamp(comp.BloodLevel + amount, 0.05f, 1.0f);
        if (comp.BloodSeed == 0.0f)
            comp.BloodSeed = (float)(item.Id % 1000);

        if (bloodColor != null)
            comp.BloodColor = bloodColor.Value;

        Dirty(item, comp);
        _item.VisualsChanged(item);

        Log.Debug($"[BloodyItemSystem] Stained item {item} with blood level {comp.BloodLevel:F2} color {comp.BloodColor.ToHex()}");
    }

    /// <summary>
    /// Stains items equipped on a humanoid in specified inventory slots.
    /// Uses subtle chances and amounts so clothing does not become excessively drenched.
    /// </summary>
    public void BloodyEquippedItems(EntityUid mob, float amount, SlotFlags flags, float chancePerSlot = 0.40f, Color? bloodColor = null)
    {
        if (!Exists(mob) || Deleted(mob) || Terminating(mob))
            return;

        var items = _inventory.GetHandOrInventoryEntities(mob, flags);
        foreach (var item in items)
        {
            if (_random.Prob(chancePerSlot))
            {
                BloodyEntity(item, amount, bloodColor);
            }
        }
    }

    /// <summary>
    /// Removes blood stains from an item and updates its visuals.
    /// </summary>
    public void CleanEntity(EntityUid item)
    {
        if (!Exists(item) || Deleted(item) || Terminating(item))
            return;

        RemCompDeferred<BloodyItemComponent>(item);
        _item.VisualsChanged(item);

        Log.Debug($"[BloodyItemSystem] Cleaned blood from item {item}");
    }

    /// <summary>
    /// Stains weapons and worn clothes during melee combat with restrained, realistic splatter amounts.
    /// </summary>
    private void OnMeleeHit(MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        bool hitBloodyTarget = false;
        Color bloodColor = Color.FromHex("#780000");

        foreach (var hit in args.HitEntities)
        {
            if (HasComp<BloodstreamComponent>(hit))
            {
                hitBloodyTarget = true;
                bloodColor = GetBloodColor(hit);

                // Moderately stain the victim's clothes
                BloodyEquippedItems(hit, 0.15f, SlotFlags.WITHOUT_POCKET, 0.40f, bloodColor);
            }
        }

        if (!hitBloodyTarget)
            return;

        // Stain the weapon used to strike the bloody target (or hands/gloves if unarmed)
        if (args.Weapon == args.User)
        {
            BloodyEquippedItems(args.User, 0.20f, SlotFlags.GLOVES, 1.0f, bloodColor);
        }
        else
        {
            BloodyEntity(args.Weapon, 0.20f, bloodColor);
        }

        // Also stain clothing of attacker (gloves/outerwear)
        BloodyEquippedItems(args.User, 0.10f, SlotFlags.GLOVES, 0.50f, bloodColor);
        BloodyEquippedItems(args.User, 0.08f, SlotFlags.OUTERCLOTHING, 0.20f, bloodColor);

        Log.Debug($"[BloodyItemSystem] Melee combat bloodying applied from attacker {args.User} with weapon {args.Weapon}");
    }

    /// <summary>
    /// Gradually stains clothes of dragged bloody bodies.
    /// Does not spawn extra decals because body smears are handled by the engine's footprint/smear system.
    /// </summary>
    private void OnPullableMove(Entity<PullableComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.BeingPulled)
            return;

        if (!HasComp<BloodstreamComponent>(ent.Owner))
            return;

        if (!_random.Prob(0.30f))
            return;

        var bloodColor = GetBloodColor(ent.Owner);
        // Gradually stain back and outerwear while dragged
        BloodyEquippedItems(ent.Owner, 0.08f, SlotFlags.OUTERCLOTHING | SlotFlags.BACK | SlotFlags.FEET, 0.35f, bloodColor);

        Log.Debug($"[BloodyItemSystem] Drag clothing stain updated for {ent.Owner}");
    }

    /// <summary>
    /// Stains clothes of crawling injured mobs.
    /// </summary>
    private void OnCrawlingMove(Entity<StandingStateComponent> ent, ref MoveEvent args)
    {
        // Only trigger when down / lying on the floor
        if (ent.Comp.Standing)
            return;

        if (!TryComp<BloodstreamComponent>(ent.Owner, out var blood) || blood.BleedAmount <= 0.5f)
            return;

        if (!_random.Prob(0.25f))
            return;

        var bloodColor = GetBloodColor(ent.Owner);
        BloodyEquippedItems(ent.Owner, 0.06f, SlotFlags.OUTERCLOTHING | SlotFlags.FEET | SlotFlags.LEGS, 0.30f, bloodColor);

        Log.Debug($"[BloodyItemSystem] Crawling clothing stain updated for {ent.Owner}");
    }

    /// <summary>
    /// Allows cleaning items with sponges, wet rags, soap, or cleaning tools.
    /// </summary>
    private void OnCleanBloodyItem(Entity<BloodyItemComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        bool isCleaner = HasComp<AbsorbentComponent>(args.Used) || HasComp<CleansForensicsComponent>(args.Used);

        if (!isCleaner)
            return;

        CleanEntity(ent.Owner);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Fluids/slosh.ogg"), ent.Owner, AudioParams.Default.WithVolume(-4f));
        _popup.PopupEntity(Loc.GetString("Кровь смыта с предмета."), ent.Owner, args.User);

        args.Handled = true;
        Log.Debug($"[BloodyItemSystem] Item {ent.Owner} cleaned by {args.User} using {args.Used}");
    }
}
