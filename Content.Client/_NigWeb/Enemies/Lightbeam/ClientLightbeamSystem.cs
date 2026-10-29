// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Shared._NigWeb.Enemies.Lightbeam;
using Robust.Client.GameObjects;

namespace Content.Client._NigWeb.Enemies.Lightbeam;

/// <summary>
/// Client visualizer for EN112 Lightbeam (Divine Light Emitter) from Blasphemous 2.
/// Maps LightbeamState and facing direction to authentic Blasphemous 2 animations.
/// </summary>
public sealed class ClientLightbeamSystem : VisualizerSystem<LightbeamComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[ClientLightbeamSystem] Initiating Client Lightbeam System...");
    }

    protected override void OnAppearanceChange(EntityUid uid, LightbeamComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Base RSI texture faces Right by default.
        // FacingLeft == true -> scale.X = -1f (faces Left towards target)
        // FacingLeft == false -> scale.X = 1f (faces Right towards target)
        var facingLeft = AppearanceSystem.TryGetData<bool>(uid, LightbeamVisuals.FacingLeft, out var f, args.Component) ? f : component.FacingLeft;
        _sprite.SetScale((uid, args.Sprite), new Vector2(facingLeft ? -1f : 1f, 1f));
        Log.Debug($"[ClientLightbeamSystem] Processing Entity {uid}: FacingLeft={facingLeft}, ScaleX={(facingLeft ? -1f : 1f)}");

        if (AppearanceSystem.TryGetData<LightbeamState>(uid, LightbeamVisuals.State, out var state, args.Component))
        {
            Log.Debug($"[ClientLightbeamSystem] Processing Entity {uid} with State={state}");

            string targetState = state switch
            {
                LightbeamState.Idle => "idle",
                LightbeamState.Activating => "attack_anticipation",
                LightbeamState.Firing => "attack_loop",
                LightbeamState.Recovery => "attack_recovery",
                LightbeamState.Dying => "death",
                _ => "idle"
            };

            if (args.Sprite.AllLayers.Any() && args.Sprite.LayerGetState(0) != targetState)
            {
                args.Sprite.LayerSetState(0, targetState);
            }
        }
    }
}
