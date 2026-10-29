// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Shared._NigWeb.Enemies.BuriedMonk;
using Robust.Client.GameObjects;

namespace Content.Client._NigWeb.Enemies.BuriedMonk;

/// <summary>
/// Client visualizer for EN18 Buried Monk from Blasphemous 2.
/// Directly maps BuriedMonkState and Facing direction to authentic Blasphemous 2 sprite animations.
/// </summary>
public sealed class ClientBuriedMonkSystem : VisualizerSystem<BuriedMonkComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[ClientBuriedMonkSystem] Initiating Client Buried Monk System with pure walking and instant turning...");
    }

    protected override void OnAppearanceChange(EntityUid uid, BuriedMonkComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Apply instant horizontal sprite flipping for Left/Right facing
        // Base RSI texture faces Right by default.
        // FacingLeft == true -> scale.X = -1f (faces Left towards target)
        // FacingLeft == false -> scale.X = 1f (faces Right towards target)
        var facingLeft = AppearanceSystem.TryGetData<bool>(uid, BuriedMonkVisuals.FacingLeft, out var f, args.Component) ? f : component.FacingLeft;
        _sprite.SetScale((uid, args.Sprite), new Vector2(facingLeft ? -1f : 1f, 1f));
        Log.Debug($"[ClientBuriedMonkSystem] Processing Entity {uid}: FacingLeft={facingLeft}, ScaleX={(facingLeft ? -1f : 1f)}");

        // Apply state animation
        if (AppearanceSystem.TryGetData<BuriedMonkState>(uid, BuriedMonkVisuals.State, out var state, args.Component))
        {
            Log.Debug($"[ClientBuriedMonkSystem] Processing Entity {uid} with State={state}");

            string targetState = state switch
            {
                BuriedMonkState.Idle => "idle",
                BuriedMonkState.Walking => "walk",
                BuriedMonkState.Anticipating => "attack",
                BuriedMonkState.Screaming => "scream",
                BuriedMonkState.Recovery => "recovery",
                BuriedMonkState.Dying => "death",
                _ => "idle"
            };

            if (args.Sprite.AllLayers.Any() && args.Sprite.LayerGetState(0) != targetState)
            {
                args.Sprite.LayerSetState(0, targetState);
            }
        }
    }
}
