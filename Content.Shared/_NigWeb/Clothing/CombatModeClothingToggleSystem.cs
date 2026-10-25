// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.DoAfter;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Inventory;
using Content.Shared.Toggleable;
using Robust.Shared.Log;

namespace Content.Shared._NigWeb.Clothing;

public sealed class CombatModeClothingToggleSystem : EntitySystem
{
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[CombatModeClothingToggleSystem] Initiating...");

        SubscribeLocalEvent<CombatModeToggledEvent>(OnCombatModeToggled);
        SubscribeLocalEvent<CombatModeClothingToggleComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<CombatModeClothingToggleComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnCombatModeToggled(ref CombatModeToggledEvent ev)
    {
        Log.Debug($"[CombatModeClothingToggleSystem] Processing CombatModeToggled for User: {ev.User}, Activated: {ev.Activated}");

        if (!_inventorySystem.TryGetContainerSlotEnumerator(ev.User, out var enumerator))
            return;

        while (enumerator.NextItem(out var item))
        {
            if (!TryComp<CombatModeClothingToggleComponent>(item, out var toggleComp))
                continue;

            var targetPrefix = ev.Activated ? toggleComp.CombatPrefix : toggleComp.NormalPrefix;
            Log.Debug($"[CombatModeClothingToggleSystem] Swapping EquippedPrefix for {item} to '{targetPrefix}' (Active: {ev.Activated})");
            _clothing.SetEquippedPrefix(item, targetPrefix);
            _appearance.SetData(item, ToggleableVisuals.Enabled, ev.Activated);
        }
    }

    private void OnGotEquipped(Entity<CombatModeClothingToggleComponent> ent, ref ClothingGotEquippedEvent args)
    {
        Log.Debug($"[CombatModeClothingToggleSystem] Item {ent.Owner} equipped by {args.Wearer}");

        var isInCombat = false;
        if (TryComp<CombatModeComponent>(args.Wearer, out var combat))
        {
            isInCombat = combat.IsInCombatMode;
        }

        var targetPrefix = isInCombat ? ent.Comp.CombatPrefix : ent.Comp.NormalPrefix;
        Log.Debug($"[CombatModeClothingToggleSystem] Initial EquippedPrefix for {ent.Owner} set to '{targetPrefix}' (InCombat: {isInCombat})");
        _clothing.SetEquippedPrefix(ent.Owner, targetPrefix);
        _appearance.SetData(ent.Owner, ToggleableVisuals.Enabled, isInCombat);
    }

    private void OnGotUnequipped(Entity<CombatModeClothingToggleComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        Log.Debug($"[CombatModeClothingToggleSystem] Item {ent.Owner} unequipped by {args.Wearer}");
        _clothing.SetEquippedPrefix(ent.Owner, ent.Comp.NormalPrefix);
        _appearance.SetData(ent.Owner, ToggleableVisuals.Enabled, false);
    }
}

