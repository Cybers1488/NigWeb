// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared._NigWeb.Enemies.Shockwave;
using Robust.Client.GameObjects;

namespace Content.Client._NigWeb.Enemies.Shockwave;

/// <summary>
/// Client visualizer for EN115 Shockwave Generator from Blasphemous 2.
/// Directly maps ShockwaveState to authentic Blasphemous 2 sprite animations.
/// </summary>
public sealed class ClientShockwaveSystem : VisualizerSystem<ShockwaveComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[ClientShockwaveSystem] Initiating Client Shockwave System...");
    }

    protected override void OnAppearanceChange(EntityUid uid, ShockwaveComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<ShockwaveState>(uid, ShockwaveVisuals.State, out var state, args.Component))
        {
            Log.Debug($"[ClientShockwaveSystem] Processing Entity {uid} with State={state}");

            string targetState = state switch
            {
                ShockwaveState.Spawn => "spawn",
                ShockwaveState.Idle => "idle",
                ShockwaveState.Anticipating => "anticipation",
                ShockwaveState.Attacking => "attack",
                ShockwaveState.Recovery => "recovery",
                ShockwaveState.Dying => "death",
                _ => "idle"
            };

            if (args.Sprite.AllLayers.Any() && args.Sprite.LayerGetState(0) != targetState)
            {
                args.Sprite.LayerSetState(0, targetState);
            }
        }
    }
}
