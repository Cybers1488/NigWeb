// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._NigWeb.Nuke;

public sealed class HotlineNukeOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "HotlineNuke";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;

    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shaderInstance;
    private readonly Texture _smokeTexture;
    private readonly Texture _spreadTexture;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public bool IsActive;
    public double StartTime;
    public EntityCoordinates? Epicenter;

    public HotlineNukeOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 250;
        _transform = _entityManager.System<SharedTransformSystem>();
        _shaderInstance = _prototypeManager.Index(ShaderProto).InstanceUnique();
        _smokeTexture = _resCache.GetResource<TextureResource>("/Textures/_NigWeb/Nuke/nuclear_smoke.png").Texture;
        _spreadTexture = _resCache.GetResource<TextureResource>("/Textures/_NigWeb/Nuke/spread_atlas.png").Texture;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!IsActive)
            return false;

        var elapsed = (float) (_timing.RealTime.TotalSeconds - StartTime);
        if (elapsed < 0.0f || elapsed >= 13.0f)
        {
            IsActive = false;
            return false;
        }

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var elapsed = (float) (_timing.RealTime.TotalSeconds - StartTime);
        var viewport = args.Viewport;
        var vpSize = (Vector2) viewport.Size;
        if (vpSize.X <= 0 || vpSize.Y <= 0)
            return;

        var aspect = vpSize.X / vpSize.Y;
        var epicenterUV = new Vector2(0.5f, 0.5f);

        if (Epicenter != null && Epicenter.Value.IsValid(_entityManager))
        {
            var mapCoords = _transform.ToMapCoordinates(Epicenter.Value);
            if (mapCoords.MapId == args.MapId)
            {
                var screenPos = viewport.WorldToLocal(mapCoords.Position);
                epicenterUV = new Vector2(screenPos.X / vpSize.X, 1.0f - (screenPos.Y / vpSize.Y));
            }
        }

        var handle = args.WorldHandle;
        _shaderInstance.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shaderInstance.SetParameter("nuclear_smoke", _smokeTexture);
        _shaderInstance.SetParameter("spread_texture", _spreadTexture);
        _shaderInstance.SetParameter("timer", elapsed);
        _shaderInstance.SetParameter("epicenter_uv", epicenterUV);
        _shaderInstance.SetParameter("aspect_ratio", aspect);

        handle.UseShader(_shaderInstance);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
