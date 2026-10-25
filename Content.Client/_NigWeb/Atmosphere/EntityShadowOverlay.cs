// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Standing;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._NigWeb.Atmosphere;

/// <summary>
/// Dynamic realistic shadow overlay that grounds entities onto world tiles:
/// renders smooth Gaussian contact shadows beneath feet, bodies, and items,
/// and projects directional cast shadows away from nearby point light sources.
/// </summary>
public sealed partial class EntityShadowOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ContactShaderId = "ContactShadow";
    private static readonly ProtoId<ShaderPrototype> DirectionalShaderId = "DirectionalShadow";

    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    private readonly ShaderInstance _contactShader;
    private readonly ShaderInstance _directionalShader;

    private readonly SharedTransformSystem _xformSys;
    private readonly EntityLookupSystem _lookup;
    private readonly LightTreeSystem _lightTree;
    private readonly SharedContainerSystem _container;

    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<MetaDataComponent> _metaQuery;
    private readonly EntityQuery<SpriteComponent> _spriteQuery;
    private readonly EntityQuery<MobStateComponent> _mobStateQuery;
    private readonly EntityQuery<HumanoidAppearanceComponent> _humanoidQuery;
    private readonly EntityQuery<StandingStateComponent> _standingQuery;
    private readonly EntityQuery<ItemComponent> _itemQuery;

    private readonly HashSet<EntityUid> _intersectingEntities = new();
    private readonly List<Entity<PointLightComponent, TransformComponent>> _nearbyLights = new();
    private readonly List<(Box2Rotated Quad, Color Color)> _contactShadows = new();
    private readonly List<(Box2Rotated Quad, Color Color)> _directionalShadows = new();

    public EntityShadowOverlay()
    {
        IoCManager.InjectDependencies(this);

        _contactShader = _protoManager.Index(ContactShaderId).InstanceUnique();
        _directionalShader = _protoManager.Index(DirectionalShaderId).InstanceUnique();

        _xformSys = _entManager.System<SharedTransformSystem>();
        _lookup = _entManager.System<EntityLookupSystem>();
        _lightTree = _entManager.System<LightTreeSystem>();
        _container = _entManager.System<SharedContainerSystem>();

        _xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        _metaQuery = _entManager.GetEntityQuery<MetaDataComponent>();
        _spriteQuery = _entManager.GetEntityQuery<SpriteComponent>();
        _mobStateQuery = _entManager.GetEntityQuery<MobStateComponent>();
        _humanoidQuery = _entManager.GetEntityQuery<HumanoidAppearanceComponent>();
        _standingQuery = _entManager.GetEntityQuery<StandingStateComponent>();
        _itemQuery = _entManager.GetEntityQuery<ItemComponent>();

        ZIndex = 0; // WorldSpaceBelowEntities: right above floor tiles, beneath mob sprites
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null || args.MapId == MapId.Nullspace)
            return;

        if (!_config.GetCVar(CCVars.EntityShadows))
            return;

        var opacity = _config.GetCVar(CCVars.EntityShadowsOpacity);
        if (opacity <= 0.001f)
            return;

        _intersectingEntities.Clear();
        _nearbyLights.Clear();
        _contactShadows.Clear();
        _directionalShadows.Clear();

        // Query entities in the visible viewport
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldAABB, _intersectingEntities);

        // Query nearby active point lights in expanded bounds for cast shadows
        _lightTree.QueryAabb(_nearbyLights, args.MapId, args.WorldAABB.Enlarged(8f));

        foreach (var ent in _intersectingEntities)
        {
            if (_entManager.Deleted(ent))
                continue;

            if (!_xformQuery.TryGetComponent(ent, out var xform) || xform.MapUid == null)
                continue;

            if (!_metaQuery.TryGetComponent(ent, out var meta))
                continue;

            // Skip entities currently inside containers (backpacks, lockers, crates, etc.)
            if (_container.IsEntityOrParentInContainer(ent, meta, xform))
                continue;

            if (_spriteQuery.TryGetComponent(ent, out var sprite) && !sprite.Visible)
                continue;

            var isHumanoid = _humanoidQuery.HasComponent(ent);
            var isMob = isHumanoid || _mobStateQuery.HasComponent(ent);

            if (isMob)
            {
                var isDown = false;
                if (_standingQuery.TryGetComponent(ent, out var standing) && !standing.Standing)
                    isDown = true;
                else if (_mobStateQuery.TryGetComponent(ent, out var mobState) && mobState.CurrentState != MobState.Alive)
                    isDown = true;

                var (worldPos, worldRot) = _xformSys.GetWorldPositionRotation(xform);

                Vector2 center;
                Vector2 size;
                Angle angle;

                if (isDown)
                {
                    center = worldPos;
                    size = new Vector2(1.15f, 0.48f);
                    angle = (sprite != null && !sprite.Rotation.Equals(Angle.Zero)) ? sprite.Rotation : worldRot;
                }
                else
                {
                    // Grounding contact shadow beneath humanoid / mob feet
                    center = worldPos + new Vector2(0f, -0.32f);
                    size = isHumanoid ? new Vector2(0.68f, 0.28f) : new Vector2(0.58f, 0.28f);
                    angle = Angle.Zero;
                }

                var contactBox = Box2.CenteredAround(center, size);
                var contactQuad = new Box2Rotated(contactBox, angle, center);
                var contactAlpha = Math.Clamp(0.62f * opacity, 0.0f, 1.0f);
                _contactShadows.Add((contactQuad, Color.Black.WithAlpha(contactAlpha)));

                // Directional cast shadow projected away from dominant point light
                Entity<PointLightComponent, TransformComponent>? dominantLight = null;
                var maxLightPower = 0f;
                var dominantLightPos = Vector2.Zero;

                foreach (var light in _nearbyLights)
                {
                    var lightComp = light.Comp1;
                    if (!lightComp.Enabled || lightComp.Radius <= 0.2f || lightComp.Energy <= 0.05f)
                        continue;

                    var lightPos = _xformSys.GetWorldPosition(light.Comp2);
                    var toMob = center - lightPos;
                    var dist = toMob.Length();

                    if (dist >= lightComp.Radius || dist < 0.08f)
                        continue;

                    var attenuation = 1.0f - (dist / lightComp.Radius);
                    var power = lightComp.Energy * attenuation;

                    if (power > maxLightPower)
                    {
                        maxLightPower = power;
                        dominantLight = light;
                        dominantLightPos = lightPos;
                    }
                }

                if (dominantLight != null && maxLightPower > 0.06f)
                {
                    var delta = center - dominantLightPos;
                    var dist = delta.Length();
                    if (dist > 0.02f)
                    {
                        var dir = delta / dist;
                        var radius = dominantLight.Value.Comp1.Radius;
                        var distFraction = Math.Clamp(dist / radius, 0.0f, 1.0f);
                        var shadowLength = Math.Clamp(1.5f * (1.0f - distFraction * 0.7f), 0.4f, 1.8f);
                        var shadowWidth = isDown ? 0.65f : 0.48f;

                        var castCenter = center + dir * (shadowLength * 0.5f);
                        var castAngle = new Angle(MathF.Atan2(dir.Y, dir.X) - MathF.PI / 2f);

                        var castBox = Box2.CenteredAround(castCenter, new Vector2(shadowWidth, shadowLength));
                        var castQuad = new Box2Rotated(castBox, castAngle, castCenter);

                        var castAlpha = Math.Clamp(maxLightPower * 0.55f * opacity, 0.12f, 0.68f);
                        _directionalShadows.Add((castQuad, Color.Black.WithAlpha(castAlpha)));
                    }
                }
            }
            else if (_itemQuery.HasComponent(ent))
            {
                var worldPos = _xformSys.GetWorldPosition(xform);
                var itemCenter = worldPos + new Vector2(0f, -0.05f);
                var itemSize = new Vector2(0.35f, 0.18f);
                var itemBox = Box2.CenteredAround(itemCenter, itemSize);
                var itemQuad = new Box2Rotated(itemBox, Angle.Zero, itemCenter);
                var itemAlpha = Math.Clamp(0.32f * opacity, 0.0f, 1.0f);
                _contactShadows.Add((itemQuad, Color.Black.WithAlpha(itemAlpha)));
            }
        }

        var handle = args.WorldHandle;

        if (_contactShadows.Count > 0)
        {
            handle.UseShader(_contactShader);
            foreach (var (quad, color) in _contactShadows)
            {
                handle.DrawRect(quad, color);
            }
        }

        if (_directionalShadows.Count > 0)
        {
            handle.UseShader(_directionalShader);
            foreach (var (quad, color) in _directionalShadows)
            {
                handle.DrawRect(quad, color);
            }
        }

        handle.UseShader(null);
    }
}
