// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.Client._NigWeb.Explosions;

/// <summary>
/// High-performance GPU overlay rendering realistic physical pixel-art tile fragments and scorched blast craters.
/// Tears fragments from the underlying floor/dirt by high explosives and scatters them across the blast area.
/// Zero entities created; pure GPU quads and jagged starburst polygons drawn into WorldSpaceBelowEntities.
/// </summary>
public sealed class TileDebrisPixelOverlay : Overlay
{
    private readonly IRobustRandom _random;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    private const float PixelSize1x1 = 1.0f / 32.0f; // 0.03125m
    private const float PixelSize2x2 = 2.0f / 32.0f; // 0.0625m
    private const float PixelSize3x2 = 3.0f / 32.0f; // 0.09375m
    private const float PixelSize3x3 = 3.0f / 32.0f; // 0.09375m
    private const int MaxActivePixels = 4096;
    private const float Gravity = 22.0f;

    public struct TileDebrisPixel
    {
        public MapId MapId;
        public Vector2 Position;       // 2D ground coordinates (meters)
        public Vector2 Velocity;       // Ground velocity (m/s)
        public float Height;           // Elevation above ground (meters)
        public float VelocityZ;        // Vertical velocity (m/s)
        public float FloorFriction;    // Kinetic deceleration rate on floor
        public Vector2 Size;           // Speck size (width, height)
        public Color ColorFloor;       // Settled tile fragment color
        public Color ColorAir;         // Searing blast tint in flight
        public float Age;              // Elapsed time in seconds
        public float Lifetime;         // Total persistence duration (12-18s)
        public float FadeDuration;     // Fade duration at end of life (3.5s smooth dissolution)
        public bool IsGrounded;        // True once fragment hits the floor
        public bool IsStationary;      // True once fragment stops sliding
    }

    public struct ScorchSpeck
    {
        public Vector2 Offset;
        public Vector2 Size;
        public Color Color;
    }

    public struct ScorchMark
    {
        public MapId MapId;
        public Vector2 Position;
        public float Radius;
        public float Age;
        public float Lifetime;
        public float FadeDuration;
        public Color CoreColor;
        public Color BodyColor;
        public Color OuterColor;
        public Vector2[] OuterFan;
        public Vector2[] BodyFan;
        public Vector2[] CoreFan;
        public ScorchSpeck[] RimSpecks;
    }

    private readonly List<TileDebrisPixel> _pixels = new(2048);
    private readonly List<ScorchMark> _scorchMarks = new(32);

    public TileDebrisPixelOverlay(IRobustRandom random)
    {
        _random = random;
    }

    /// <summary>
    /// Generates an irregular, jagged polygon fan around an epicenter for realistic non-circular craters.
    /// </summary>
    private Vector2[] GenerateJaggedFan(Vector2 center, float baseRadius, float minJitter, float maxJitter, int rayCount)
    {
        var vertices = new Vector2[rayCount + 2];
        vertices[0] = center;

        var step = (MathF.PI * 2.0f) / rayCount;
        for (int i = 0; i <= rayCount; i++)
        {
            var idx = i % rayCount;
            var angle = idx * step;
            var dist = baseRadius * _random.NextFloat(minJitter, maxJitter);
            var pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
            vertices[i + 1] = pos;
        }

        return vertices;
    }

    /// <summary>
    /// Emits a violent omnidirectional burst of physical ground tile debris pixels,
    /// and lays down an authentic ragged, charred blast crater on the floor.
    /// </summary>
    public void EmitTileDebrisBurst(MapId mapId, Vector2 origin, Color[] palette, int count, float radius)
    {
        if (mapId == MapId.Nullspace || palette.Length == 0)
            return;

        // 1. Generate realistic jagged blast crater (irregular starburst, never geometric circles)
        var craterRadius = Math.Clamp(radius * 0.45f, 2.5f, 4.2f);
        const int rayCount = 20;

        var outerFan = GenerateJaggedFan(origin, craterRadius * 1.35f, 0.65f, 1.40f, rayCount);
        var bodyFan = GenerateJaggedFan(origin, craterRadius * 0.88f, 0.70f, 1.30f, rayCount);
        var coreFan = GenerateJaggedFan(origin, craterRadius * 0.50f, 0.75f, 1.25f, rayCount);

        var speckCount = _random.Next(110, 160);
        var specks = new ScorchSpeck[speckCount];

        Color[] rimColors =
        {
            Color.FromHex("#0b0908"),
            Color.FromHex("#120f0d"),
            Color.FromHex("#181412"),
            Color.FromHex("#211a16"),
        };

        for (int s = 0; s < speckCount; s++)
        {
            float spAngle = _random.NextFloat(-MathF.PI, MathF.PI);
            float dist = _random.NextFloat(craterRadius * 0.55f, craterRadius * 1.45f);
            var offset = new Vector2(MathF.Cos(spAngle), MathF.Sin(spAngle)) * dist;

            Vector2 spSize = _random.Prob(0.55f)
                ? new Vector2(PixelSize1x1, PixelSize1x1)
                : (_random.Prob(0.6f) ? new Vector2(PixelSize2x2, PixelSize2x2) : new Vector2(PixelSize3x2, PixelSize2x2));

            specks[s] = new ScorchSpeck
            {
                Offset = offset,
                Size = spSize,
                Color = _random.Pick(rimColors),
            };
        }

        _scorchMarks.Add(new ScorchMark
        {
            MapId = mapId,
            Position = origin,
            Radius = craterRadius,
            Age = 0f,
            Lifetime = 35.0f,
            FadeDuration = 8.0f,
            CoreColor = Color.FromHex("#070504"),
            BodyColor = Color.FromHex("#110e0c"),
            OuterColor = Color.FromHex("#1a1512"),
            OuterFan = outerFan,
            BodyFan = bodyFan,
            CoreFan = coreFan,
            RimSpecks = specks,
        });

        // 2. Spawn incandescent yellow sparks and physical flying tile fragments
        var sparkCount = _random.Next(95, 140);
        Color[] sparkTints =
        {
            Color.FromHex("#ffffff"),
            Color.FromHex("#fff59d"),
            Color.FromHex("#ffeb3b"),
            Color.FromHex("#ffd54f"),
            Color.FromHex("#ffb300"),
            Color.FromHex("#ff8f00"),
        };

        for (int s = 0; s < sparkCount; s++)
        {
            if (_pixels.Count >= MaxActivePixels)
                _pixels.RemoveAt(0);

            var angle = _random.NextFloat(-MathF.PI, MathF.PI);
            var radialSpeed = _random.NextFloat(7.0f, 25.0f);
            var vX = MathF.Cos(angle) * radialSpeed;
            var vY = MathF.Sin(angle) * radialSpeed;

            var elevationSpeed = _random.NextFloat(6.0f, 19.0f);
            var initialElevation = _random.NextFloat(0.10f, 0.45f);

            var sparkColor = _random.Pick(sparkTints);
            var coolColor = Color.FromHex("#26170d");

            Vector2 sparkSize = _random.Prob(0.60f)
                ? new Vector2(PixelSize1x1, PixelSize1x1)
                : new Vector2(PixelSize2x2, PixelSize1x1);

            var lifetime = _random.NextFloat(1.8f, 3.8f);
            var friction = _random.NextFloat(8.0f, 16.0f);

            _pixels.Add(new TileDebrisPixel
            {
                MapId = mapId,
                Position = origin,
                Velocity = new Vector2(vX, vY),
                Height = initialElevation,
                VelocityZ = elevationSpeed,
                FloorFriction = friction,
                Size = sparkSize,
                ColorFloor = coolColor,
                ColorAir = sparkColor,
                Age = 0f,
                Lifetime = lifetime,
                FadeDuration = 1.2f,
                IsGrounded = false,
                IsStationary = false,
            });
        }

        // 3. Spawn physical flying terrain fragments
        var spawned = Math.Min(count, 140);

        for (int i = 0; i < spawned; i++)
        {
            if (_pixels.Count >= MaxActivePixels)
                _pixels.RemoveAt(0);

            var angle = _random.NextFloat(-MathF.PI, MathF.PI);
            var radialSpeed = _random.NextFloat(3.0f, 16.0f);
            var vX = MathF.Cos(angle) * radialSpeed;
            var vY = MathF.Sin(angle) * radialSpeed;

            var elevationSpeed = _random.NextFloat(5.0f, 15.0f);
            var initialElevation = _random.NextFloat(0.05f, 0.35f);

            Vector2 speckSize;
            var sizeRoll = _random.NextFloat();
            if (sizeRoll < 0.50f)
                speckSize = new Vector2(PixelSize1x1, PixelSize1x1);
            else if (sizeRoll < 0.85f)
                speckSize = new Vector2(PixelSize2x2, PixelSize2x2);
            else
                speckSize = new Vector2(PixelSize3x2, PixelSize2x2);

            var floorColor = _random.Pick(palette);
            // Searing dust tint catching the brilliant golden flash in flight
            var airColor = Color.InterpolateBetween(floorColor, Color.FromHex("#ffe57f"), _random.NextFloat(0.40f, 0.70f));

            var lifetime = _random.NextFloat(12.0f, 18.0f);
            var friction = _random.NextFloat(5.5f, 12.0f);

            _pixels.Add(new TileDebrisPixel
            {
                MapId = mapId,
                Position = origin,
                Velocity = new Vector2(vX, vY),
                Height = initialElevation,
                VelocityZ = elevationSpeed,
                FloorFriction = friction,
                Size = speckSize,
                ColorFloor = floorColor,
                ColorAir = airColor,
                Age = 0f,
                Lifetime = lifetime,
                FadeDuration = 3.5f,
                IsGrounded = false,
                IsStationary = false,
            });
        }
    }

    /// <summary>
    /// Updates flight ballistics, floor friction, and lifetime culling.
    /// </summary>
    public void FrameUpdate(float dt)
    {
        // 1. Update scorch marks
        for (int s = _scorchMarks.Count - 1; s >= 0; s--)
        {
            var sm = _scorchMarks[s];
            sm.Age += dt;
            if (sm.Age >= sm.Lifetime)
            {
                _scorchMarks.RemoveAt(s);
                continue;
            }
            _scorchMarks[s] = sm;
        }

        // 2. Update physical tile fragments
        for (int i = _pixels.Count - 1; i >= 0; i--)
        {
            var p = _pixels[i];
            p.Age += dt;
            if (p.Age >= p.Lifetime)
            {
                _pixels.RemoveAt(i);
                continue;
            }

            if (!p.IsGrounded)
            {
                p.Position += p.Velocity * dt;
                p.Height += p.VelocityZ * dt;
                p.VelocityZ -= Gravity * dt;

                p.Velocity *= MathF.Max(0f, 1.0f - (1.1f * dt));

                if (p.Height <= 0f)
                {
                    p.Height = 0f;
                    p.VelocityZ = 0f;
                    p.IsGrounded = true;
                    p.Velocity *= 0.35f;
                }
            }
            else if (!p.IsStationary)
            {
                var speed = p.Velocity.Length();
                if (speed > 0.0001f)
                {
                    p.Position += p.Velocity * dt;
                    var decel = p.FloorFriction * dt;

                    if (speed <= decel || speed < 0.035f)
                    {
                        p.Velocity = Vector2.Zero;
                        p.IsStationary = true;
                    }
                    else
                    {
                        p.Velocity -= (p.Velocity / speed) * decel;
                    }
                }
                else
                {
                    p.IsStationary = true;
                }
            }

            _pixels[i] = p;
        }
    }

    /// <summary>
    /// Renders realistic jagged blast craters and physical tile fragments beneath entities.
    /// </summary>
    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var viewport = args.WorldAABB;
        var mapId = args.MapId;

        // 1. Draw jagged scorched blast craters on the floor (never geometric circles)
        for (int s = 0; s < _scorchMarks.Count; s++)
        {
            var scorch = _scorchMarks[s];
            if (scorch.MapId != mapId)
                continue;

            var scorchBox = Box2.CenteredAround(scorch.Position, new Vector2(scorch.Radius * 3.2f, scorch.Radius * 3.2f));
            if (!viewport.Intersects(in scorchBox))
                continue;

            var alpha = 1.0f;
            var fadeStart = scorch.Lifetime - scorch.FadeDuration;
            if (scorch.Age > fadeStart)
            {
                alpha = Math.Clamp(1.0f - (scorch.Age - fadeStart) / scorch.FadeDuration, 0.0f, 1.0f);
            }

            // Outer heat-scorched earth halo
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, scorch.OuterFan, scorch.OuterColor.WithAlpha(alpha * 0.35f));

            // Mid charred soot body
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, scorch.BodyFan, scorch.BodyColor.WithAlpha(alpha * 0.65f));

            // Deep blackened carbon core
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, scorch.CoreFan, scorch.CoreColor.WithAlpha(alpha * 0.88f));

            // Ragged jagged rim specks
            for (int r = 0; r < scorch.RimSpecks.Length; r++)
            {
                var sp = scorch.RimSpecks[r];
                var spBox = Box2.CenteredAround(scorch.Position + sp.Offset, sp.Size);
                handle.DrawRect(spBox, sp.Color.WithAlpha(alpha * 0.85f), filled: true);
            }
        }

        if (_pixels.Count == 0)
            return;

        // 2. Draw flying and settled tile fragments
        for (int i = 0; i < _pixels.Count; i++)
        {
            var p = _pixels[i];
            if (p.MapId != mapId)
                continue;

            var drawPos = p.Position;

            Vector2 drawSize;
            var velSq = p.Velocity.LengthSquared();
            if (!p.IsGrounded && velSq > 1.2f)
            {
                if (MathF.Abs(p.Velocity.X) >= MathF.Abs(p.Velocity.Y))
                    drawSize = new Vector2(p.Size.X * 1.6f, p.Size.Y);
                else
                    drawSize = new Vector2(p.Size.X, p.Size.Y * 1.6f);
            }
            else
            {
                drawSize = p.Size;
            }

            var box = Box2.CenteredAround(drawPos, drawSize);
            if (!viewport.Intersects(in box))
                continue;

            Color baseColor;
            if (!p.IsGrounded)
            {
                var t = Math.Clamp(p.Height / 0.15f, 0f, 1f);
                baseColor = Color.InterpolateBetween(p.ColorFloor, p.ColorAir, t);
            }
            else
            {
                baseColor = p.ColorFloor;
            }

            var alpha = 1.0f;
            var fadeStart = p.Lifetime - p.FadeDuration;
            if (p.Age > fadeStart)
            {
                alpha = Math.Clamp(1.0f - (p.Age - fadeStart) / p.FadeDuration, 0.0f, 1.0f);
            }

            handle.DrawRect(box, baseColor.WithAlpha(alpha), filled: true);
        }
    }
}
