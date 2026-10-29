// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Shared._NigWeb.Enemies.SpiritSpawner;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.Client._NigWeb.Enemies.SpiritSpawner;

/// <summary>
/// Client visualizer for EN116 Flying Flower (Spirit) from Blasphemous 2.
/// </summary>
public sealed class ClientFlyingFlowerSystem : VisualizerSystem<FlyingFlowerComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[ClientFlyingFlowerSystem] Initiating Client Flying Flower System...");
    }

    protected override void OnAppearanceChange(EntityUid uid, FlyingFlowerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var facingLeft = AppearanceSystem.TryGetData<bool>(uid, FlyingFlowerVisuals.FacingLeft, out var f, args.Component) ? f : component.FacingLeft;
        _sprite.SetScale((uid, args.Sprite), new Vector2(facingLeft ? -1f : 1f, 1f));
        Log.Debug($"[ClientFlyingFlowerSystem] Processing Entity {uid}: FacingLeft={facingLeft}");

        if (AppearanceSystem.TryGetData<FlyingFlowerState>(uid, FlyingFlowerVisuals.State, out var state, args.Component))
        {
            Log.Debug($"[ClientFlyingFlowerSystem] Processing Entity {uid} with State={state}");

            string targetState = state switch
            {
                FlyingFlowerState.Idle => "idle",
                FlyingFlowerState.Anticipating => "attack_anticipation",
                FlyingFlowerState.Exploding => "attack",
                FlyingFlowerState.Dying => "death",
                _ => "idle"
            };

            if (args.Sprite.AllLayers.Any() && args.Sprite.LayerGetState(0) != targetState)
            {
                args.Sprite.LayerSetState(0, targetState);
            }
        }
    }
}
