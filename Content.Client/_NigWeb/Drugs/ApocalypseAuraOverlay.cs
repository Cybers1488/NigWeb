// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._NigWeb.Drugs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Components;

namespace Content.Client._NigWeb.Drugs;

/// <summary>
/// Continuous chromatic directional motion smear matching Hotline Miami 2 Apocalypse.
/// Only visible to players under the influence of Ephedrine (subjective hallucination).
/// </summary>
public sealed class ApocalypseAuraOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    private readonly IEntityManager _entityManager;
    private readonly IPlayerManager _playerManager;
    private readonly TransformSystem _transform;

    // Authentic Hotline Miami 2 palette sampled from Scene 24/25 Apocalypse
    private static readonly Color LightPink = Color.FromHex("#ffc2d4");
    private static readonly Color NeonPink = Color.FromHex("#ff2a75");
    private static readonly Color MagentaRed = Color.FromHex("#c4103c");
    private static readonly Color Crimson = Color.FromHex("#800517");

    private const int SmearSteps = 24;

    public ApocalypseAuraOverlay(IEntityManager entityManager, IPlayerManager playerManager)
    {
        ZIndex = (int) DrawDepth.BelowMobs;
        _entityManager = entityManager;
        _playerManager = playerManager;
        _transform = _entityManager.System<TransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (eye == null)
            return;

        // Visual hallucination: ONLY rendered for a player with Ephedrine in their blood
        var localPlayer = _playerManager.LocalEntity;
        if (localPlayer == null || !_entityManager.HasComponent<ApocalypseTripComponent>(localPlayer.Value))
            return;

        var eyeRot = eye.Rotation;
        var handle = args.WorldHandle;
        var bounds = args.WorldAABB;
        handle.SetTransform(Matrix3x2.Identity);

        // The tripping player sees this continuous motion wake behind EVERY moving mob (themselves and others)
        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, TransformComponent, SpriteComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var xform, out var sprite, out var physics))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (mobState.CurrentState == MobState.Dead)
                continue;

            var velocity = physics.LinearVelocity;
            var speedSq = velocity.LengthSquared();
            if (speedSq < 0.04f)
                continue; // Stationary -> no smear

            var speed = MathF.Sqrt(speedSq);
            var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);

            // Bounding check
            if (!bounds.Enlarged(2.0f).Contains(worldPos))
                continue;

            // Direction opposite of velocity
            var dragDir = -Vector2.Normalize(velocity);
            // Smear length scales dynamically with movement speed (0.35m up to 1.15m)
            var smearLen = Math.Clamp(speed * 0.20f, 0.35f, 1.15f);

            var originalColor = sprite.Color;
            var originalScale = sprite.Scale;
            var dir = (worldRot + eyeRot).GetCardinalDir();

            // Render dense overlapping micro-steps from furthest to closest to form a continuous liquid smear
            for (var i = SmearSteps; i >= 1; i--)
            {
                var fraction = (float) i / SmearSteps; // 1.0 down to ~0.04
                var offset = dragDir * (smearLen * fraction);
                var renderPos = worldPos + offset;

                // Smooth non-linear alpha decay: highest near body, bleeding away at tail tip
                var alpha = MathF.Pow(1.0f - fraction, 1.2f) * 0.40f;

                Color tint;
                if (fraction < 0.30f)
                {
                    var t = fraction / 0.30f;
                    tint = Color.InterpolateBetween(LightPink, NeonPink, t).WithAlpha(alpha);
                }
                else if (fraction < 0.70f)
                {
                    var t = (fraction - 0.30f) / 0.40f;
                    tint = Color.InterpolateBetween(NeonPink, MagentaRed, t).WithAlpha(alpha);
                }
                else
                {
                    var t = (fraction - 0.70f) / 0.30f;
                    tint = Color.InterpolateBetween(MagentaRed, Crimson, t).WithAlpha(alpha);
                }

                var scaleMult = MathHelper.Lerp(1.0f, 0.88f, fraction);
                sprite.Color = tint;
                sprite.Scale = originalScale * scaleMult;
                sprite.Render(handle, eyeRot, worldRot, dir, renderPos);
            }

            sprite.Color = originalColor;
            sprite.Scale = originalScale;
        }
    }
}
