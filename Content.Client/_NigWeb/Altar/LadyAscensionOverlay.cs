// SPDX-License-Identifier: MIT
using System;
using Content.Shared._NigWeb.Altar;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._NigWeb.Altar;

/// <summary>
/// World overlay that renders the Flayed Lady Ascension shader.
/// Transforms the game world into strictly White, Black, and Crimson Red once the Lady of the Chalices ascends.
/// </summary>
public sealed partial class LadyAscensionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "FlayedLadyAscension";
    private static readonly TimeSpan FadeInDuration = TimeSpan.FromSeconds(2.5);

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;
    private readonly SharedMapSystem _mapSystem;

    public LadyAscensionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _mapSystem = _entityManager.System<SharedMapSystem>();
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        ZIndex = 55;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.MapId == MapId.Nullspace)
            return;

        var mapUid = _mapSystem.GetMapOrInvalid(args.MapId);
        if (!mapUid.IsValid() || !_entityManager.TryGetComponent<LadyAscensionMapComponent>(mapUid, out var comp) || !comp.Active)
            return;

        var curTime = _timing.CurTime;
        var elapsed = (curTime - comp.StartTime).TotalSeconds;
        var intensity = Math.Clamp((float)(elapsed / FadeInDuration.TotalSeconds), 0.0f, 1.0f);

        if (intensity <= 0.001f)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("intensity", intensity);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
