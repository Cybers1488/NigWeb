// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._NigWeb.Atmosphere;

/// <summary>
/// Grimdark post-processing overlay that transforms the visual aesthetic of the world:
/// filmic tone mapping, cold shadow depth, selective blood & holy relic luminance,
/// dynamic light-reactive vignette, and micro film grain.
/// </summary>
public sealed partial class GrimAtmosphereOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "GrimAtmosphere";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public GrimAtmosphereOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        ZIndex = 50; // Draw over world tiles, decals, and sprites, but before HUD and UI
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var intensity = _configManager.GetCVar(CCVars.GrimAtmosphereIntensity);
        if (intensity <= 0.001f)
            return;

        var zoom = args.Viewport.Eye?.Zoom.X ?? 1.0f;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("intensity", intensity);
        _shader.SetParameter("Zoom", zoom);

        if (args.Viewport.LightRenderTarget?.Texture is { } lightTex)
        {
            _shader.SetParameter("LIGHT_TEXTURE", lightTex);
            _shader.SetParameter("hasLightTexture", true);
        }
        else
        {
            _shader.SetParameter("hasLightTexture", false);
        }

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
