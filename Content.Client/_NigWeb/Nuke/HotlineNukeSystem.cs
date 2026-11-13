// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._NigWeb.Nuke;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._NigWeb.Nuke;

public sealed class HotlineNukeSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private HotlineNukeOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new HotlineNukeOverlay();
        _overlayManager.AddOverlay(_overlay);

        SubscribeNetworkEvent<NukeDetonationCinematicEvent>(OnNukeDetonation);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnNukeDetonation(NukeDetonationCinematicEvent ev)
    {
        TriggerCinematic(GetCoordinates(ev.Coordinates));
    }

    public void TriggerCinematic(EntityCoordinates? coordinates = null)
    {
        _overlay.StartTime = _timing.RealTime.TotalSeconds;
        _overlay.Epicenter = coordinates;
        _overlay.IsActive = true;

        _audio.PlayGlobal(
            "/Audio/_NigWeb/Nuke/nuclear_blast.ogg",
            Filter.Local(),
            false,
            AudioParams.Default.WithVolume(4.0f));
    }
}
