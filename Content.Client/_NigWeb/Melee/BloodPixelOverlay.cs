// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.Client._NigWeb.Melee;

/// <summary>
/// High-performance GPU overlay rendering realistic visceral pixel-art blood gushing, pooling, and floor stains.
/// Features dark, heavy, non-cartoonish coagulated blood dynamics: heavy downward pouring, arterial jets,
/// kinetic floor skidding, and persistent clotted stains beneath entities.
/// Zero entities created; pure GPU quads drawn into WorldSpaceBelowEntities.
/// </summary>
public sealed class BloodPixelOverlay : Overlay
{
    private readonly ISawmill _sawmill = Logger.GetSawmill("blood_pixels");
    private readonly IRobustRandom _random;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    // Single world pixel in meters (32 pixels per meter standard: 1px = 0.03125m, 2px = 0.0625m, 3px = 0.09375m)
    private const float PixelSize1x1 = 1.0f / 32.0f; // 0.03125m
    private const float PixelSize2x2 = 2.0f / 32.0f; // 0.0625m
    private const float PixelSize3x2 = 3.0f / 32.0f; // 0.09375m
    private const float PixelSize3x3 = 3.0f / 32.0f; // 0.09375m
    private const int MaxActivePixels = 18000; // Large buffer to support hundreds of consecutive melee strikes without truncating floor blood
    private const float Gravity = 17.5f; // Heavy, viscous downward gravity

    // Authentic rich visceral arterial palette for airborne droplets (vivid blood red, not neon cherry, not black)
    private static readonly Color[] ArterialPalette =
    {
        Color.FromHex("#940808"), // High-pressure fresh arterial red
        Color.FromHex("#880000"), // Pure visceral crimson
        Color.FromHex("#9e0404"), // Bright wound spurt red
        Color.FromHex("#7c0000"), // Deep arterial red
        Color.FromHex("#8f0303"), // Liquid blood highlight
    };

    // Deep clotted crimson floor stain palette (visibly dark coagulated red blood, NOT pitch black tar)
    private static readonly Color[] FloorPalette =
    {
        Color.FromHex("#6b0000"), // Dark coagulated blood clot
        Color.FromHex("#5e0000"), // Thick dried crimson stain
        Color.FromHex("#650303"), // Deep oxidized blood puddle
        Color.FromHex("#540000"), // Dark floor gore
        Color.FromHex("#720404"), // Wet blood pool center
    };

    public struct BloodPixel
    {
        public MapId MapId;
        public Vector2 Position;       // 2D ground coordinates (meters)
        public Vector2 Velocity;       // Ground velocity (m/s)
        public float Height;           // Elevation above ground (meters)
        public float VelocityZ;        // Vertical velocity (m/s)
        public float FloorFriction;    // Kinetic deceleration rate on floor
        public Vector2 Size;           // Speck size (width, height)
        public Color ColorFresh;       // Deep crimson in flight
        public Color ColorFloor;       // Pitch clotted dark gore on floor
        public float Age;              // Elapsed time in seconds
        public float Lifetime;         // Total persistence duration (exactly 18 seconds)
        public float FadeDuration;     // Fade duration at end of life (3.0s smooth dissolution)
        public bool IsGrounded;        // True once droplet hits the floor
        public bool IsStationary;      // True once droplet stops sliding
    }

    private readonly List<BloodPixel> _pixels = new(4096);

    public BloodPixelOverlay(IRobustRandom random)
    {
        _random = random;
        _sawmill.Info("[BloodPixelOverlay] Initiated realistic dark visceral GPU blood gush overlay with 18s floor persistence.");
    }

    /// <summary>
    /// Emits a brutal, organic burst of dark pixel-art blood.
    /// Features a wide natural forward cone, wide flank spray, chaotic 360-degree rebound splatter,
    /// and viscous pooling beneath the victim.
    /// Persists on the floor for 18 seconds without premature disappearing when striking others.
    /// </summary>
    public void EmitBloodBurst(MapId mapId, Vector2 origin, Vector2 baseDir)
    {
        if (mapId == MapId.Nullspace)
            return;

        var normDir = baseDir.LengthSquared() > 0.001f ? Vector2.Normalize(baseDir) : Vector2.UnitX;

        // 65 to 95 physical blood droplets per heavy strike for rich, varied organic splatter
        int count = _random.Next(65, 95);

        for (int i = 0; i < count; i++)
        {
            if (_pixels.Count >= MaxActivePixels)
            {
                // Discard small batch of oldest pixels only under extreme load
                _pixels.RemoveRange(0, Math.Min(100, _pixels.Count));
            }

            float angleDeg;
            float groundSpeed;
            float initHeight;
            float vz;
            Vector2 baseSize;

            float roll = _random.NextFloat();
            if (roll < 0.45f)
            {
                // 1. Forward Main Arterial Cone (45%): wide, natural forward spray (-35 deg to +35 deg)
                angleDeg = _random.NextFloat(-35f, 35f);
                groundSpeed = _random.NextFloat(2.6f, 5.4f);
                initHeight = _random.NextFloat(0.30f, 0.65f);
                vz = _random.NextFloat(0.8f, 2.4f);

                baseSize = _random.Prob(0.55f)
                    ? new Vector2(PixelSize2x2, PixelSize2x2)
                    : new Vector2(PixelSize1x1, PixelSize1x1);
            }
            else if (roll < 0.70f)
            {
                // 2. Wide Flank Dispersion (25%): breaks cone geometry (+-30 deg to +-75 deg)
                angleDeg = _random.Prob(0.5f) ? _random.NextFloat(30f, 75f) : _random.NextFloat(-75f, -30f);
                groundSpeed = _random.NextFloat(1.4f, 3.6f);
                initHeight = _random.NextFloat(0.20f, 0.50f);
                vz = _random.NextFloat(0.5f, 1.8f);

                baseSize = _random.Prob(0.60f)
                    ? new Vector2(PixelSize2x2, PixelSize2x2)
                    : new Vector2(PixelSize1x1, PixelSize1x1);
            }
            else if (roll < 0.85f)
            {
                // 3. Chaotic Omnidirectional Rebound & Side-Splatter (15%): droplets flying in ALL 360 directions
                angleDeg = _random.NextFloat(-180f, 180f);
                groundSpeed = _random.NextFloat(0.6f, 2.8f);
                initHeight = _random.NextFloat(0.15f, 0.55f);
                vz = _random.NextFloat(0.4f, 2.2f);

                baseSize = _random.Prob(0.40f)
                    ? new Vector2(PixelSize1x1, PixelSize1x1)
                    : (_random.Prob(0.5f) ? new Vector2(PixelSize2x2, PixelSize2x2) : new Vector2(PixelSize3x2, PixelSize2x2));
            }
            else
            {
                // 4. Heavy Pool Clots & Ground Splash (15%): dense gore dropping directly beneath the victim/wound
                angleDeg = _random.NextFloat(-180f, 180f);
                groundSpeed = _random.NextFloat(0.12f, 0.85f);
                initHeight = _random.NextFloat(0.10f, 0.30f);
                vz = _random.NextFloat(-0.2f, 0.6f);

                baseSize = _random.Prob(0.35f)
                    ? new Vector2(PixelSize3x3, PixelSize3x3)
                    : (_random.Prob(0.5f) ? new Vector2(PixelSize3x2, PixelSize2x2) : new Vector2(PixelSize2x2, PixelSize2x2));
            }

            // Direct rotation around normDir with natural angular dispersion
            var sprayVec = Angle.FromDegrees(angleDeg).RotateVec(normDir);
            var initialVel = sprayVec * groundSpeed;

            var friction = _random.NextFloat(12.0f, 18.5f);
            var lifetime = _random.NextFloat(17.8f, 18.2f); // Exactly 18 seconds persistence on floor
            var fadeDuration = 3.0f; // Smooth fade-out during final 3 seconds (from 15s to 18s)

            var freshColor = _random.Pick(ArterialPalette);
            var floorColor = _random.Pick(FloorPalette);

            var jitter = new Vector2(_random.NextFloat(-0.08f, 0.08f), _random.NextFloat(-0.08f, 0.08f));

            _pixels.Add(new BloodPixel
            {
                MapId = mapId,
                Position = origin + jitter,
                Velocity = initialVel,
                Height = initHeight,
                VelocityZ = vz,
                FloorFriction = friction,
                Size = baseSize,
                ColorFresh = freshColor,
                ColorFloor = floorColor,
                Age = 0f,
                Lifetime = lifetime,
                FadeDuration = fadeDuration,
                IsGrounded = false,
                IsStationary = false,
            });
        }

        _sawmill.Debug($"[BloodPixelOverlay] Emitted {count} organic visceral blood pixels at {origin} along {normDir}. Active total: {_pixels.Count}");
    }

    /// <summary>
    /// Simulates 2.5D parabolic projectile arc, heavy gravity, floor impact damping, and kinetic sliding.
    /// </summary>
    public void FrameUpdate(float frameTime)
    {
        if (_pixels.Count == 0)
            return;

        var dt = MathF.Min(frameTime, 0.1f);

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
                // Airborne flight: advance ground position and vertical height
                p.Position += p.Velocity * dt;
                p.Height += p.VelocityZ * dt;
                p.VelocityZ -= Gravity * dt;

                // Air resistance
                p.Velocity *= MathF.Max(0f, 1f - 1.5f * dt);

                // Check impact with the floor
                if (p.Height <= 0f)
                {
                    p.Height = 0f;
                    p.VelocityZ = 0f;
                    p.IsGrounded = true;

                    // Heavy viscous floor impact damping
                    p.Velocity *= 0.38f;
                }
            }
            else if (!p.IsStationary)
            {
                // Oozing and sliding across floor tiles with heavy kinetic friction
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
    /// Renders all physical blood droplets as crisp pixel quads on the floor beneath entities.
    /// </summary>
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_pixels.Count == 0)
            return;

        var handle = args.WorldHandle;
        var viewport = args.WorldAABB;
        var mapId = args.MapId;

        for (int i = 0; i < _pixels.Count; i++)
        {
            var p = _pixels[i];
            if (p.MapId != mapId)
                continue;

            // Draw directly at ground position — avoids artificial downward vertical drop
            var drawPos = p.Position;

            // Dynamic streak elongation while moving fast in the air
            Vector2 drawSize;
            var velSq = p.Velocity.LengthSquared();
            if (!p.IsGrounded && velSq > 1.0f)
            {
                if (MathF.Abs(p.Velocity.X) >= MathF.Abs(p.Velocity.Y))
                    drawSize = new Vector2(p.Size.X * 1.7f, p.Size.Y);
                else
                    drawSize = new Vector2(p.Size.X, p.Size.Y * 1.7f);
            }
            else
            {
                drawSize = p.Size;
            }

            var box = Box2.CenteredAround(drawPos, drawSize);
            if (!viewport.Intersects(in box))
                continue;

            // Color grading: dark visceral crimson in flight, pitch clotted gore on floor
            Color baseColor;
            if (!p.IsGrounded)
            {
                var t = Math.Clamp(p.Height / 0.55f, 0f, 1f);
                baseColor = Color.InterpolateBetween(p.ColorFloor, p.ColorFresh, t);
            }
            else
            {
                baseColor = p.ColorFloor;
            }

            // Alpha fadeout during the final moments of floor stain lifetime
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
