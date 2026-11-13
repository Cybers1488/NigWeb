// SPDX-License-Identifier: MIT
using System;
using System.Numerics;
using Content.Server.Popups;
using Content.Shared._NigWeb.Cherub;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;

namespace Content.Server._NigWeb.Cherub;

public sealed class CherubFamiliarSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CherubFamiliarOwnerComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<CherubFamiliarOwnerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CherubFamiliarOwnerComponent, ComponentShutdown>(OnOwnerShutdown);
    }

    private void OnBeforeDamage(EntityUid uid, CherubFamiliarOwnerComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (comp.Familiar == null || !Exists(comp.Familiar))
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        if (!_mobThreshold.TryGetIncapThreshold(uid, out var incapThreshold))
            return;

        var incoming = args.Damage.GetTotal();
        if (damageable.TotalDamage + incoming >= incapThreshold)
        {
            TriggerDeathWard(uid, comp);
            args.Cancelled = true;
            args.Damage.ClampMax(0);
        }
    }

    private void OnMobStateChanged(EntityUid uid, CherubFamiliarOwnerComponent comp, ref MobStateChangedEvent args)
    {
        if (comp.Familiar == null || !Exists(comp.Familiar))
            return;

        if (args.NewMobState == MobState.Critical || args.NewMobState == MobState.Dead)
        {
            TriggerDeathWard(uid, comp);

            _mobState.ChangeMobState(uid, MobState.Alive);

            if (TryComp<DamageableComponent>(uid, out var dam) &&
                _mobThreshold.TryGetIncapThreshold(uid, out var threshold))
            {
                var targetDamage = threshold - 30;
                if (dam.TotalDamage > targetDamage)
                {
                    var excess = dam.TotalDamage - targetDamage;
                    var ratio = excess / dam.TotalDamage;
                    _damageable.TryChangeDamage(uid, -dam.Damage * (float) ratio, true);
                }
            }
        }
    }

    public void TriggerDeathWard(EntityUid owner, CherubFamiliarOwnerComponent comp)
    {
        if (comp.Familiar is { } familiar && Exists(familiar))
        {
            var fCoords = Transform(familiar).Coordinates;
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Cherub/cherub_break.ogg"), fCoords, AudioParams.Default.WithVolume(4f));
            Spawn("NigWebCherubCageShatterEffect", fCoords);
            QueueDel(familiar);
            comp.Familiar = null;
        }

        _popup.PopupEntity("Херувим принял смертельный удар на себя и рассыпался!", owner, owner, PopupType.LargeCaution);
        RemComp<CherubFamiliarOwnerComponent>(owner);
    }

    private void OnOwnerShutdown(EntityUid uid, CherubFamiliarOwnerComponent comp, ComponentShutdown args)
    {
        if (comp.Familiar is { } familiar && Exists(familiar))
        {
            QueueDel(familiar);
            comp.Familiar = null;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CherubFamiliarComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var familiar, out var xform))
        {
            if (familiar.Owner == null || !Exists(familiar.Owner.Value))
            {
                QueueDel(uid);
                continue;
            }

            var ownerUid = familiar.Owner.Value;
            if (!TryComp<TransformComponent>(ownerUid, out var ownerXform))
            {
                QueueDel(uid);
                continue;
            }

            if (ownerXform.MapID != xform.MapID)
            {
                _transform.SetCoordinates(uid, ownerXform.Coordinates);
                continue;
            }

            familiar.OrbitPhase += frameTime * familiar.OrbitSpeed;

            var offsetX = MathF.Cos(familiar.OrbitPhase) * familiar.OrbitRadiusX;
            var offsetY = familiar.OrbitBaseHeight + MathF.Sin(familiar.OrbitPhase * 1.6f) * familiar.OrbitRadiusY;

            var ownerWorldPos = _transform.GetWorldPosition(ownerXform);
            var targetWorldPos = ownerWorldPos + new Vector2(offsetX, offsetY);
            var currentPos = _transform.GetWorldPosition(xform);

            var dist = Vector2.Distance(currentPos, targetWorldPos);
            if (dist > 12f)
            {
                _transform.SetWorldPosition(uid, targetWorldPos);
            }
            else
            {
                var factor = 1.0f - MathF.Exp(-7.5f * frameTime);
                var newPos = Vector2.Lerp(currentPos, targetWorldPos, factor);
                _transform.SetWorldPosition(uid, newPos);
            }
        }
    }
}
