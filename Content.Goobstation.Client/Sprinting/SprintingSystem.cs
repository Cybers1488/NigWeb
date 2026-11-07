// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Sprinting;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Sprinting;

public sealed class SprintingSystem : SharedSprintingSystem
{
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private ConcussionOverlay _concussionOverlay = default!;

    private static readonly Animation InitialAnimation = new()
    {
        Length = TimeSpan.FromSeconds(0.48),
        AnimationTracks =
        {
            new AnimationTrackSpriteFlick
            {
                LayerKey = SprintVisualLayers.Base,
                KeyFrames =
                {
                    new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId("sprint_cloud"), 0f),
                }
            }
        }
    };

    private static readonly Animation SmallAnimation = new()
    {
        Length = TimeSpan.FromSeconds(0.34),
        AnimationTracks =
        {
            new AnimationTrackSpriteFlick
            {
                LayerKey = SprintVisualLayers.Base,
                KeyFrames =
                {
                    new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId("sprint_cloud_small"), 0f),
                },
            },
        },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SprinterComponent, SprintStartEvent>(OnSprintStart);

        _concussionOverlay = new();
        SubscribeLocalEvent<ConcussedComponent, ComponentInit>(OnConcussedInit);
        SubscribeLocalEvent<ConcussedComponent, ComponentShutdown>(OnConcussedShutdown);
        SubscribeLocalEvent<ConcussedComponent, LocalPlayerAttachedEvent>(OnConcussedPlayerAttached);
        SubscribeLocalEvent<ConcussedComponent, LocalPlayerDetachedEvent>(OnConcussedPlayerDetached);
    }

    private void OnConcussedInit(EntityUid uid, ConcussedComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlayMan.AddOverlay(_concussionOverlay);
        }
    }

    private void OnConcussedShutdown(EntityUid uid, ConcussedComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlayMan.RemoveOverlay(_concussionOverlay);
        }
    }

    private void OnConcussedPlayerAttached(EntityUid uid, ConcussedComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_concussionOverlay);
    }

    private void OnConcussedPlayerDetached(EntityUid uid, ConcussedComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_concussionOverlay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SprinterComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.IsSprinting
                || !_timing.IsFirstTimePredicted
                || _timing.CurTime - component.LastStep < component.TimeBetweenSteps)
                continue;

            var ent = Spawn(component.StepAnimation, Transform(uid).Coordinates);
            _animationPlayer.Play(ent, SmallAnimation, "sprint_cloud_small");
            component.LastStep = _timing.CurTime;
        }
    }

    private void OnSprintStart(EntityUid uid, SprinterComponent component, ref SprintStartEvent args)
    {
        if (TerminatingOrDeleted(uid)
            || !_timing.IsFirstTimePredicted)
            return;

        var ent = Spawn(component.SprintAnimation, Transform(uid).Coordinates);
        _animationPlayer.Play(ent, InitialAnimation, "sprint_cloud");
    }

    public enum SprintVisualLayers : byte
    {
        Base,
    }
}
