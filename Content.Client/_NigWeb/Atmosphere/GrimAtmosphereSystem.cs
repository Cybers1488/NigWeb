// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._NigWeb.Atmosphere;

/// <summary>
/// System responsible for managing the lifecycle and configuration of the GrimAtmosphereOverlay.
/// </summary>
public sealed partial class GrimAtmosphereSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private GrimAtmosphereOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[GrimAtmosphereSystem] Initiating grimdark atmospheric lighting and visual overhaul...");
        _overlay = new GrimAtmosphereOverlay();

        _cfg.OnValueChanged(CCVars.GrimAtmosphere, OnAtmosphereCVarChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        Log.Info("[GrimAtmosphereSystem] Shutting down grimdark atmosphere overlay...");
        _cfg.UnsubValueChanged(CCVars.GrimAtmosphere, OnAtmosphereCVarChanged);

        if (_overlayMan.HasOverlay<GrimAtmosphereOverlay>())
        {
            _overlayMan.RemoveOverlay(_overlay);
        }
    }

    private void OnAtmosphereCVarChanged(bool enabled)
    {
        Log.Info($"[GrimAtmosphereSystem] GrimAtmosphere status updated: {enabled}");

        if (enabled)
        {
            if (!_overlayMan.HasOverlay<GrimAtmosphereOverlay>())
            {
                _overlayMan.AddOverlay(_overlay);
            }
        }
        else
        {
            if (_overlayMan.HasOverlay<GrimAtmosphereOverlay>())
            {
                _overlayMan.RemoveOverlay(_overlay);
            }
        }
    }
}
