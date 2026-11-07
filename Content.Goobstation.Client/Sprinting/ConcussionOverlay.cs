// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Sprinting;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Sprinting;

public sealed class ConcussionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ConcussionShader = "Concussion";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _shader;

    public float Intensity = 0.0f;

    public ConcussionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(ConcussionShader).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;
        if (playerEntity == null)
        {
            Intensity = 0f;
            return;
        }

        if (!_entityManager.TryGetComponent<ConcussedComponent>(playerEntity, out var concussed))
        {
            Intensity = 0f;
            return;
        }

        var curTime = _timing.CurTime;
        var total = (float)(concussed.EndTime - concussed.StartTime).TotalSeconds;
        if (total <= 0f)
            total = 1f;

        var remaining = (float)(concussed.EndTime - curTime).TotalSeconds;
        Intensity = Math.Clamp(remaining / total, 0f, 1f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return Intensity > 0.001f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var worldHandle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("intensity", Intensity);
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}
