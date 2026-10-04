// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared.Mobs;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.DamageOverlays.Overlays;

public sealed class DamageOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _critShader;
    private readonly ShaderInstance _oxygenShader;
    private readonly ShaderInstance _bruteShader;

    public MobState State = MobState.Alive;

    /// <summary>
    /// Shitmed Change: Handles the red pulsing overlay
    /// </summary>
    public float PainLevel = 0f;

    private float _oldPainLevel = 0f;

    /// <summary>
    /// Handles the darkening overlay.
    /// </summary>
    public float OxygenLevel = 0f;

    private float _oldOxygenLevel = 0f;

    /// <summary>
    /// Handles the white overlay when crit.
    /// </summary>
    public float CritLevel = 0f;

    private float _oldCritLevel = 0f;

    public float DeadLevel = 1f;

    public DamageOverlay()
    {
        // TODO: Replace
        IoCManager.InjectDependencies(this);
        _oxygenShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
        _critShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
        _bruteShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return;

        if (args.Viewport.Eye != eyeComp.Eye)
            return;

        /*
         * Here's the rundown:
         * 1. There's lerping for each level so the transitions are smooth.
         * 2. There's 3 overlays, 1 for brute damage, 1 for oxygen damage (that also doubles as a crit overlay),
         * and a white one during crit that closes in as you progress towards death. When you die it slowly disappears.
         * The crit overlay also occasionally reduces its alpha as a "blink"
         */

        var viewport = args.WorldAABB;
        var handle = args.WorldHandle;
        var distance = args.ViewportBounds.Width;

        var time = (float) _timing.RealTime.TotalSeconds;
        var lastFrameTime = (float) _timing.FrameTime.TotalSeconds;

        // If they just died then lerp out the white overlay.
        if (State != MobState.Dead)
        {
            DeadLevel = 1f;
        }
        else if (!MathHelper.CloseTo(0f, DeadLevel, 0.001f))
        {
            var diff = -DeadLevel;
            DeadLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            DeadLevel = 0f;
        }

        if (!MathHelper.CloseTo(_oldPainLevel, PainLevel, 0.001f))
        {
            var diff = PainLevel - _oldPainLevel;
            _oldPainLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldPainLevel = PainLevel;
        }

        if (!MathHelper.CloseTo(_oldOxygenLevel, OxygenLevel, 0.001f))
        {
            var diff = OxygenLevel - _oldOxygenLevel;
            _oldOxygenLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldOxygenLevel = OxygenLevel;
        }

        if (!MathHelper.CloseTo(_oldCritLevel, CritLevel, 0.001f))
        {
            var diff = CritLevel - _oldCritLevel;
            _oldCritLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldCritLevel = CritLevel;
        }

        /*
         * darknessAlphaOuter is the maximum alpha for anything outside of the larger circle
         * darknessAlphaInner (on the shader) is the alpha for anything inside the smallest circle
         *
         * outerCircleRadius is what we end at for max level for the outer circle
         * outerCircleMaxRadius is what we start at for 0 level for the outer circle
         *
         * innerCircleRadius is what we end at for max level for the inner circle
         * innerCircleMaxRadius is what we start at for 0 level for the inner circle
         */

        // Only show pain overlay if there's actual pain and we're not in critical stat
        // Goobstation start
        float level = 0f;
        level = _oldPainLevel;
        if (_oldPainLevel > 0f && _oldCritLevel <= 0f)
        {
            var pulseRate = 3f;
            var adjustedTime = time * pulseRate;
            var sin = MathF.Max(0f, MathF.Sin(adjustedTime));
            var pulse = sin * sin; // Punchy heartbeat / pain spasm flash

            // Lifeweb / BYOND style from mob_screen1_full2.png:
            // Color transitions from bright red (206, 0, 0) at low damage
            // to dark black-red (35, 0, 0) at high damage (near-crit agony)
            var r = MathHelper.Lerp(206f, 35f, _oldPainLevel) / 255f;

            // Base persistent haze on severe pain (like frame [0,0] A=42 R=32)
            var baseAlpha = 0.15f * (_oldPainLevel * _oldPainLevel);

            // Peak flash alpha scales with pain level (up to 0.75 on near-crit)
            var flashAlpha = MathHelper.Lerp(0.35f, 0.65f, _oldPainLevel) * _oldPainLevel * pulse;

            var currentAlpha = Math.Clamp(baseAlpha + flashAlpha, 0f, 0.80f);

            if (currentAlpha > 0.005f)
            {
                handle.UseShader(null);
                handle.DrawRect(viewport, new Color(r, 0f, 0f, currentAlpha));
            }
        }
        else
        {
            _oldPainLevel = PainLevel;
        }

        level = State != MobState.Critical ? _oldOxygenLevel : 1f;

        if (level > 0f)
        {
            float outerMaxLevel = 0.6f * distance;
            float outerMinLevel = 0.06f * distance;
            float innerMaxLevel = 0.02f * distance;
            float innerMinLevel = 0.02f * distance;

            var outerRadius = outerMaxLevel - level * (outerMaxLevel - outerMinLevel);
            var innerRadius = innerMaxLevel - level * (innerMaxLevel - innerMinLevel);

            float outerDarkness;
            float critTime;

            // If in crit then just fix it; also pulse it very occasionally so they can see more.
            if (_oldCritLevel > 0f)
            {
                var adjustedTime = time * 2f;
                critTime = MathF.Max(0, MathF.Sin(adjustedTime) + 2 * MathF.Sin(2 * adjustedTime / 4f) + MathF.Sin(adjustedTime / 4f) - 3f);

                if (critTime > 0f)
                {
                    outerDarkness = 1f - critTime / 1.5f;
                }
                else
                {
                    outerDarkness = 1f;
                }
            }
            else
            {
                outerDarkness = MathF.Min(0.98f, 0.3f * MathF.Log(level) + 1f);
            }

            _oxygenShader.SetParameter("time", 0.0f);
            _oxygenShader.SetParameter("color", new Vector3(0f, 0f, 0f));
            _oxygenShader.SetParameter("darknessAlphaOuter", outerDarkness);
            _oxygenShader.SetParameter("innerCircleRadius", innerRadius);
            _oxygenShader.SetParameter("innerCircleMaxRadius", innerRadius);
            _oxygenShader.SetParameter("outerCircleRadius", outerRadius);
            _oxygenShader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
            handle.UseShader(_oxygenShader);
            handle.DrawRect(viewport, Color.White);
        }

        // NigWeb: simple black blinking square for crit
        level = State != MobState.Dead ? _oldCritLevel : DeadLevel;

        if (level > 0f)
        {
            // Calculate a pulse for the black square
            var pulse = MathF.Max(0f, MathF.Sin(time * 3f)); // Blinks faster

            // In dead state, just stay solid black
            if (State == MobState.Dead)
            {
                pulse = 1f;
            }

            // Draw a black rectangle over the entire screen with the pulsing alpha
            handle.UseShader(null);
            handle.DrawRect(viewport, new Color(0f, 0f, 0f, pulse * level));
        }

        handle.UseShader(null);
    }

    private float GetDiff(float value, float lastFrameTime)
    {
        var adjustment = value * 5f * lastFrameTime;

        if (value < 0f)
            adjustment = Math.Clamp(adjustment, value, -value);
        else
            adjustment = Math.Clamp(adjustment, -value, value);

        return adjustment;
    }
}
