// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared._NigWeb.BloodSplatter;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._NigWeb.BloodSplatter;

public sealed class ClientBloodyItemSystem : SharedBloodyItemSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private const string ShaderId = "BloodyItem";

    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[ClientBloodyItemSystem] Initiating bloody item shader visualizer...");

        SubscribeLocalEvent<BloodyItemComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BloodyItemComponent, AfterAutoHandleStateEvent>(OnColorStateHandled);
        SubscribeLocalEvent<BloodyItemComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BloodyItemComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
        SubscribeLocalEvent<BloodyItemComponent, HeldVisualsUpdatedEvent>(OnHeldVisualsUpdated);
    }

    private void OnStartup(Entity<BloodyItemComponent> ent, ref ComponentStartup args)
    {
        Log.Debug($"[ClientBloodyItemSystem] Startup for bloody item {ent.Owner}");
        ApplyItemShader(ent.Owner, ent.Comp);
        _item.VisualsChanged(ent.Owner);
    }

    private void OnColorStateHandled(Entity<BloodyItemComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Log.Debug($"[ClientBloodyItemSystem] State handled for bloody item {ent.Owner}");
        ApplyItemShader(ent.Owner, ent.Comp);
        _item.VisualsChanged(ent.Owner);
    }

    private void OnShutdown(Entity<BloodyItemComponent> ent, ref ComponentShutdown args)
    {
        Log.Debug($"[ClientBloodyItemSystem] Shutdown for bloody item {ent.Owner}");
        RemoveItemShader(ent.Owner);
        _item.VisualsChanged(ent.Owner);
    }

    /// <summary>
    /// When worn as clothing on a mob, applies the blood shader directly onto the worn clothing layers.
    /// Because the shader runs on the clothing texture, blood strictly adheres to the clothing surface.
    /// </summary>
    private void OnEquipmentVisualsUpdated(Entity<BloodyItemComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        if (!TryComp<SpriteComponent>(args.Equipee, out var sprite))
            return;

        if (!_prototype.TryIndex<ShaderPrototype>(ShaderId, out var proto))
            return;

        var shader = proto.InstanceUnique();
        shader.SetParameter("blood_level", ent.Comp.BloodLevel);
        shader.SetParameter("blood_seed", ent.Comp.BloodSeed != 0f ? ent.Comp.BloodSeed : (float)(ent.Owner.Id % 1000));
        shader.SetParameter("blood_color", new Vector3(ent.Comp.BloodColor.R, ent.Comp.BloodColor.G, ent.Comp.BloodColor.B));

        foreach (var key in args.RevealedLayers)
        {
            if (_sprite.LayerMapTryGet((args.Equipee, sprite), key, out var layer, false))
            {
                sprite.LayerSetShader(layer, shader);
            }
        }

        Log.Debug($"[ClientBloodyItemSystem] Applied clothing blood shader to {args.Equipee} for slot {args.Slot}");
    }

    /// <summary>
    /// When held in hand, applies the blood shader directly onto the revealed in-hand layers.
    /// </summary>
    private void OnHeldVisualsUpdated(Entity<BloodyItemComponent> ent, ref HeldVisualsUpdatedEvent args)
    {
        if (!TryComp<SpriteComponent>(args.User, out var sprite))
            return;

        if (!_prototype.TryIndex<ShaderPrototype>(ShaderId, out var proto))
            return;

        var shader = proto.InstanceUnique();
        shader.SetParameter("blood_level", ent.Comp.BloodLevel);
        shader.SetParameter("blood_seed", ent.Comp.BloodSeed != 0f ? ent.Comp.BloodSeed : (float)(ent.Owner.Id % 1000));
        shader.SetParameter("blood_color", new Vector3(ent.Comp.BloodColor.R, ent.Comp.BloodColor.G, ent.Comp.BloodColor.B));

        foreach (var key in args.RevealedLayers)
        {
            if (_sprite.LayerMapTryGet((args.User, sprite), key, out var layer, false))
            {
                sprite.LayerSetShader(layer, shader);
            }
        }

        Log.Debug($"[ClientBloodyItemSystem] Applied in-hand blood shader to {args.User} for held item {ent.Owner}");
    }

    /// <summary>
    /// Applies the blood shader directly to the item's sprite layers when dropped or inspected.
    /// Strictly guards to never operate on mob entities.
    /// </summary>
    private void ApplyItemShader(EntityUid item, BloodyItemComponent comp)
    {
        if (HasComp<MobStateComponent>(item))
            return;

        if (!TryComp<SpriteComponent>(item, out var sprite))
            return;

        if (!_prototype.TryIndex<ShaderPrototype>(ShaderId, out var proto))
            return;

        var shader = proto.InstanceUnique();
        shader.SetParameter("blood_level", comp.BloodLevel);
        shader.SetParameter("blood_seed", comp.BloodSeed != 0f ? comp.BloodSeed : (float)(item.Id % 1000));
        shader.SetParameter("blood_color", new Vector3(comp.BloodColor.R, comp.BloodColor.G, comp.BloodColor.B));

        // Ensure PostShader is cleared so it does not composite twice
        sprite.PostShader = null;

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            sprite.LayerSetShader(i, shader);
        }

        Log.Debug($"[ClientBloodyItemSystem] Applied blood shader to item {item} with level {comp.BloodLevel:F2}");
    }

    /// <summary>
    /// Clears the blood shader when the clothing item is washed.
    /// </summary>
    private void RemoveItemShader(EntityUid item)
    {
        if (!TryComp<SpriteComponent>(item, out var sprite))
            return;

        sprite.PostShader = null;

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            sprite.LayerSetShader(i, null, null);
        }

        Log.Debug($"[ClientBloodyItemSystem] Removed blood shader from item {item}");
    }
}
