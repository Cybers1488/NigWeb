// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Shared._NigWeb.Enemies.SpiritSpawner;
using Robust.Client.GameObjects;

namespace Content.Client._NigWeb.Enemies.SpiritSpawner;

/// <summary>
/// Client visualizer for EN109 Spirit Spawner from Blasphemous 2.
/// </summary>
public sealed class ClientSpiritSpawnerSystem : VisualizerSystem<SpiritSpawnerComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[ClientSpiritSpawnerSystem] Initiating Client Spirit Spawner System...");
    }

    protected override void OnAppearanceChange(EntityUid uid, SpiritSpawnerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var facingLeft = AppearanceSystem.TryGetData<bool>(uid, SpiritSpawnerVisuals.FacingLeft, out var f, args.Component) ? f : component.FacingLeft;
        _sprite.SetScale((uid, args.Sprite), new Vector2(facingLeft ? -1f : 1f, 1f));
        Log.Debug($"[ClientSpiritSpawnerSystem] Processing Entity {uid}: FacingLeft={facingLeft}");

        if (AppearanceSystem.TryGetData<SpiritSpawnerState>(uid, SpiritSpawnerVisuals.State, out var state, args.Component))
        {
            Log.Debug($"[ClientSpiritSpawnerSystem] Processing Entity {uid} with State={state}");

            string targetState = state switch
            {
                SpiritSpawnerState.Idle => "idle",
                SpiritSpawnerState.SpawnAnticipation => "spawn_anticipation",
                SpiritSpawnerState.Spawning => "spawn",
                SpiritSpawnerState.SpawnRecovery => "spawn_recovery",
                SpiritSpawnerState.AttackAnticipation => "attack_anticipation",
                SpiritSpawnerState.Attacking => "attack",
                SpiritSpawnerState.AttackRecovery => "attack_recovery",
                SpiritSpawnerState.Stunned => "stun_loop",
                SpiritSpawnerState.Dying => "death",
                _ => "idle"
            };

            if (args.Sprite.AllLayers.Any() && args.Sprite.LayerGetState(0) != targetState)
            {
                args.Sprite.LayerSetState(0, targetState);
            }
        }
    }
}
