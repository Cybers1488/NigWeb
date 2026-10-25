// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._NigWeb.Atmosphere;

/// <summary>
/// System managing the lifecycle and registration of the EntityShadowOverlay.
/// </summary>
public sealed partial class EntityShadowSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private EntityShadowOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[EntityShadowSystem] Initiating dynamic realistic entity shadow system...");
        _overlay = new EntityShadowOverlay();

        _cfg.OnValueChanged(CCVars.EntityShadows, OnShadowsCVarChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        Log.Info("[EntityShadowSystem] Shutting down dynamic realistic entity shadow system...");
        _cfg.UnsubValueChanged(CCVars.EntityShadows, OnShadowsCVarChanged);

        if (_overlayMan.HasOverlay<EntityShadowOverlay>())
        {
            _overlayMan.RemoveOverlay(_overlay);
        }
    }

    private void OnShadowsCVarChanged(bool enabled)
    {
        Log.Info($"[EntityShadowSystem] EntityShadows status updated: {enabled}");

        if (enabled)
        {
            if (!_overlayMan.HasOverlay<EntityShadowOverlay>())
            {
                _overlayMan.AddOverlay(_overlay);
            }
        }
        else
        {
            if (_overlayMan.HasOverlay<EntityShadowOverlay>())
            {
                _overlayMan.RemoveOverlay(_overlay);
            }
        }
    }
}
