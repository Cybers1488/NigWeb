// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._NigWeb.Drugs;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client._NigWeb.Drugs;

public sealed class ClientApocalypseTripSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private ApocalypseScreenOverlay _screenOverlay = default!;
    private ApocalypseAuraOverlay _auraOverlay = default!;

    private EntityUid? _heartbeatStream;

    public override void Initialize()
    {
        base.Initialize();

        _screenOverlay = new ApocalypseScreenOverlay();
        _auraOverlay = new ApocalypseAuraOverlay(EntityManager, _playerManager);

        _overlayManager.AddOverlay(_screenOverlay);
        _overlayManager.AddOverlay(_auraOverlay);

        SubscribeLocalEvent<ApocalypseTripComponent, ComponentStartup>(OnStartup);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayManager.RemoveOverlay(_screenOverlay);
        _overlayManager.RemoveOverlay(_auraOverlay);

        if (_heartbeatStream != null)
        {
            _audio.Stop(_heartbeatStream);
            _heartbeatStream = null;
        }
    }

    private void OnStartup(Entity<ApocalypseTripComponent> ent, ref ComponentStartup args)
    {
        if (ent.Owner == _playerManager.LocalEntity)
        {
            _audio.PlayGlobal(
                "/Audio/_NigWeb/Drugs/lsd_freak.ogg",
                Filter.Local(),
                false,
                AudioParams.Default.WithVolume(-1f));
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var local = _playerManager.LocalEntity;
        var hasTrip = local != null && HasComp<ApocalypseTripComponent>(local.Value);

        if (hasTrip && _heartbeatStream == null)
        {
            _heartbeatStream = _audio.PlayGlobal(
                "/Audio/_NigWeb/Drugs/lsd_heartbeat.ogg",
                Filter.Local(),
                false,
                AudioParams.Default.WithVolume(-3f).WithLoop(true))?.Entity;
        }
        else if (!hasTrip && _heartbeatStream != null)
        {
            _audio.Stop(_heartbeatStream);
            _heartbeatStream = null;
        }
    }
}
