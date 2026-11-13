// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Forensics;
using Content.Server.Popups;
using Content.Shared._NigWeb.Weapons;
using Content.Shared.Body.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;

namespace Content.Server._NigWeb.Weapons;

public sealed class BloodyBladeSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodyBladeComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<BloodyBladeComponent, InteractUsingEvent>(OnCleanBlade);
        SubscribeLocalEvent<BloodyBladeComponent, ItemWieldedEvent>(OnItemWielded);
        SubscribeLocalEvent<BloodyBladeComponent, ItemUnwieldedEvent>(OnItemUnwielded);
    }

    private void OnItemWielded(Entity<BloodyBladeComponent> ent, ref ItemWieldedEvent args)
    {
        UpdateVisualPrefixes(ent);
    }

    private void OnItemUnwielded(Entity<BloodyBladeComponent> ent, ref ItemUnwieldedEvent args)
    {
        UpdateVisualPrefixes(ent);
    }

    private void OnMeleeHit(Entity<BloodyBladeComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        bool hitLiving = false;
        foreach (var victim in args.HitEntities)
        {
            if (HasComp<MobStateComponent>(victim) || HasComp<BloodstreamComponent>(victim))
            {
                hitLiving = true;
                break;
            }
        }

        if (!hitLiving)
            return;

        ent.Comp.CurrentHits++;
        if (ent.Comp.CurrentHits >= ent.Comp.HitsToBloody && !ent.Comp.IsBloody)
        {
            SetBloody(ent, true);
        }
        else
        {
            Dirty(ent);
        }
    }

    private void OnCleanBlade(Entity<BloodyBladeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        bool isCleaner = HasComp<AbsorbentComponent>(args.Used) || HasComp<CleansForensicsComponent>(args.Used);
        if (!isCleaner)
            return;

        SetBloody(ent, false);
        ent.Comp.CurrentHits = 0;
        Dirty(ent);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Fluids/slosh.ogg"), ent.Owner, AudioParams.Default.WithVolume(-4f));
        _popup.PopupEntity("Клинок начисто протерт от крови.", ent.Owner, args.User);

        args.Handled = true;
    }

    public void SetBloody(Entity<BloodyBladeComponent> ent, bool bloody)
    {
        ent.Comp.IsBloody = bloody;
        Dirty(ent);

        _appearance.SetData(ent.Owner, BloodyBladeVisuals.Bloody, bloody);
        UpdateVisualPrefixes(ent);
    }

    public void UpdateVisualPrefixes(Entity<BloodyBladeComponent> ent)
    {
        bool isWielded = TryComp<WieldableComponent>(ent.Owner, out var wieldable) && wieldable.Wielded;
        string? prefix;

        if (ent.Comp.IsBloody && isWielded)
        {
            prefix = "wielded-blood";
        }
        else if (isWielded)
        {
            prefix = "wielded";
        }
        else if (ent.Comp.IsBloody)
        {
            prefix = ent.Comp.HeldPrefix ?? "blood";
        }
        else
        {
            prefix = null;
        }

        if (wieldable != null)
        {
            wieldable.OldInhandPrefix = ent.Comp.IsBloody ? (ent.Comp.HeldPrefix ?? "blood") : null;
        }

        _item.SetHeldPrefix(ent.Owner, prefix);

        if (TryComp<ClothingComponent>(ent.Owner, out var clothing))
        {
            _clothing.SetEquippedPrefix(ent.Owner, ent.Comp.IsBloody ? "blood" : null, clothing);
        }
    }
}
