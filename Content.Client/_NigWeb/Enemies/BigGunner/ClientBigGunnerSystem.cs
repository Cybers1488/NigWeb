// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Shared._NigWeb.Enemies.BigGunner;
using Robust.Client.GameObjects;

namespace Content.Client._NigWeb.Enemies.BigGunner;

/// <summary>
/// Client visualizer for EN114 Big Gunner from Blasphemous 2.
/// Directly maps BigGunnerState and Facing direction to authentic Blasphemous 2 sprite animations.
/// </summary>
public sealed class ClientBigGunnerSystem : VisualizerSystem<BigGunnerComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[ClientBigGunnerSystem] Initiating Client Big Gunner System...");
    }

    protected override void OnAppearanceChange(EntityUid uid, BigGunnerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Base RSI texture faces Right by default.
        // FacingLeft == true -> scale.X = -1f (faces Left towards target)
        // FacingLeft == false -> scale.X = 1f (faces Right towards target)
        var facingLeft = AppearanceSystem.TryGetData<bool>(uid, BigGunnerVisuals.FacingLeft, out var f, args.Component) ? f : component.FacingLeft;
        _sprite.SetScale((uid, args.Sprite), new Vector2(facingLeft ? -1f : 1f, 1f));
        Log.Debug($"[ClientBigGunnerSystem] Processing Entity {uid}: FacingLeft={facingLeft}, ScaleX={(facingLeft ? -1f : 1f)}");

        if (AppearanceSystem.TryGetData<BigGunnerState>(uid, BigGunnerVisuals.State, out var state, args.Component))
        {
            Log.Debug($"[ClientBigGunnerSystem] Processing Entity {uid} with State={state}");

            string targetState = state switch
            {
                BigGunnerState.Idle => "idle",
                BigGunnerState.RangedAnticipating => "ranged_anticipation",
                BigGunnerState.RangedAttacking => "ranged_attack",
                BigGunnerState.MeleeAnticipating => "melee_anticipation",
                BigGunnerState.MeleeAttacking => "melee_attack",
                BigGunnerState.Stunned => "stun",
                BigGunnerState.Dying => "death",
                _ => "idle"
            };

            if (args.Sprite.AllLayers.Any() && args.Sprite.LayerGetState(0) != targetState)
            {
                args.Sprite.LayerSetState(0, targetState);
            }
        }
    }
}
