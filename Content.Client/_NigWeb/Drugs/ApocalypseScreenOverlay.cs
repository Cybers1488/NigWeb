// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._NigWeb.Drugs;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._NigWeb.Drugs;

/// <summary>
/// Full-screen psychedelic hallucinations shader matching Hotline Miami 2 Apocalypse.
/// </summary>
public sealed class ApocalypseScreenOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "ApocalypseTrip";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shaderInstance;
    public float Intensity;

    public ApocalypseScreenOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shaderInstance = _prototypeManager.Index(ShaderProto).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var player = _playerManager.LocalEntity;
        var hasTrip = player != null && _entityManager.HasComponent<ApocalypseTripComponent>(player.Value);
        var target = hasTrip ? 1.0f : 0.0f;

        Intensity = MathHelper.Lerp(Intensity, target, args.DeltaSeconds * 2.8f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return Intensity > 0.005f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _shaderInstance.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shaderInstance.SetParameter("intensity", Intensity);
        handle.UseShader(_shaderInstance);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
