// SPDX-License-Identifier: MIT
using Content.Shared._NigWeb.Altar;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Client._NigWeb.Altar;

/// <summary>
/// Manages the LadyAscensionOverlay and listens for Lady ascension world events.
/// Switches lighting off during ascension exactly like the admin togglelight mode.
/// </summary>
public sealed class LadyAscensionOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ILightManager _lightManager = default!;

    private LadyAscensionOverlay _overlay = default!;
    private bool _ascensionLightDisabled;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[LadyAscensionOverlaySystem] Initializing LadyAscensionOverlaySystem...");

        _overlay = new LadyAscensionOverlay();
        if (!_overlayMan.HasOverlay<LadyAscensionOverlay>())
        {
            _overlayMan.AddOverlay(_overlay);
        }

        SubscribeNetworkEvent<LadyAscendedEvent>(OnLadyAscended);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Log.Info("[LadyAscensionOverlaySystem] Shutting down LadyAscensionOverlaySystem...");

        if (_ascensionLightDisabled)
        {
            Log.Info("[LadyAscensionOverlaySystem] Restoring light rendering on system shutdown.");
            _lightManager.Enabled = true;
            _ascensionLightDisabled = false;
        }

        if (_overlayMan.HasOverlay<LadyAscensionOverlay>())
        {
            _overlayMan.RemoveOverlay(_overlay);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var isAscensionActive = false;
        var query = EntityQueryEnumerator<LadyAscensionMapComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.Active)
            {
                isAscensionActive = true;
                break;
            }
        }

        if (isAscensionActive)
        {
            if (_lightManager.Enabled)
            {
                Log.Info("[LadyAscensionOverlaySystem] Ascension active worldwide: switching off lighting like admin togglelight.");
                _lightManager.Enabled = false;
            }
            _ascensionLightDisabled = true;
        }
        else if (_ascensionLightDisabled)
        {
            Log.Info("[LadyAscensionOverlaySystem] Ascension inactive: restoring lighting.");
            _lightManager.Enabled = true;
            _ascensionLightDisabled = false;
        }
    }

    private void OnLadyAscended(LadyAscendedEvent ev)
    {
        Log.Info($"[LadyAscensionOverlaySystem] Received LadyAscendedEvent for altar {ev.Altar}. Reality is collapsing into crimson and monochrome!");

        // Play solemn ascension chime globally to mark the transformation
        _audio.PlayGlobal("/Audio/_NigWeb/Lady/lady_bell_low.ogg", Filter.Local(), false, AudioParams.Default.WithVolume(3.5f));
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        if (_ascensionLightDisabled)
        {
            Log.Info("[LadyAscensionOverlaySystem] Restoring light rendering on round restart.");
            _lightManager.Enabled = true;
            _ascensionLightDisabled = false;
        }
    }
}
