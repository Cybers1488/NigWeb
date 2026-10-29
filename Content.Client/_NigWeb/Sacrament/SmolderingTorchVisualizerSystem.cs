// SPDX-License-Identifier: MIT
using System;
using Content.Client.Light.Components;
using Content.Client.Light.EntitySystems;
using Content.Shared._NigWeb.Sacrament;
using Robust.Client.GameObjects;

namespace Content.Client._NigWeb.Sacrament;

/// <summary>
/// Client visualizer for the Smoldering Brand (Смоляной Светоч).
/// Triggers smooth flickering fire ignition behaviour on PointLight when lit,
/// and smooth dimming fade_out behaviour when extinguished.
/// </summary>
public sealed class SmolderingTorchVisualizerSystem : VisualizerSystem<SmolderingTorchComponent>
{
    [Dependency] private readonly LightBehaviorSystem _lightBehavior = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[SmolderingTorchVisualizerSystem] Initializing client torch visualizer system...");
    }

    protected override void OnAppearanceChange(EntityUid uid, SmolderingTorchComponent comp, ref AppearanceChangeEvent args)
    {
        if (!AppearanceSystem.TryGetData<bool>(uid, SmolderingTorchVisuals.Lit, out var lit, args.Component))
            return;

        if (!TryComp<LightBehaviourComponent>(uid, out var lightBehaviour))
            return;

        try
        {
            if (lit)
            {
                Log.Debug($"[SmolderingTorchVisualizerSystem] Starting smooth turn_on light behaviour for {uid}");
                if (TryComp<PointLightComponent>(uid, out var pointLight))
                {
                    _pointLight.SetEnabled(uid, true, pointLight);
                }
                _lightBehavior.StopLightBehaviour((uid, lightBehaviour));
                _lightBehavior.StartLightBehaviour((uid, lightBehaviour), "turn_on");
            }
            else
            {
                Log.Debug($"[SmolderingTorchVisualizerSystem] Starting smooth fade_out light behaviour for {uid}");
                _lightBehavior.StopLightBehaviour((uid, lightBehaviour));
                _lightBehavior.StartLightBehaviour((uid, lightBehaviour), "fade_out");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[SmolderingTorchVisualizerSystem] Failed to toggle light behaviour on {uid}: {ex}");
        }
    }
}
