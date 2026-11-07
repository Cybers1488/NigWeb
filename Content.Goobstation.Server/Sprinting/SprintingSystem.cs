// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Goobstation.Shared.Sandevistan;
using Content.Goobstation.Shared.Sprinting;
using Content.Server.Stunnable;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Jittering;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Sprinting;

public sealed class SprintingSystem : SharedSprintingSystem
{
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly SharedJitteringSystem _jittering = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SprinterComponent, StartCollideEvent>(OnCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ConcussedComponent>();
        while (query.MoveNext(out var uid, out var concussed))
        {
            if (curTime < concussed.EndTime)
                continue;

            RemCompDeferred<ConcussedComponent>(uid);
            RemCompDeferred<KnockedDownComponent>(uid);
            _standing.Stand(uid, force: true);
        }
    }

    private void OnCollide(EntityUid uid, SprinterComponent sprinter, ref StartCollideEvent args)
    {
        if (!sprinter.IsSprinting)
            return;

        if (!args.OurFixture.Hard || !args.OtherFixture.Hard)
            return;

        var otherUid = args.OtherEntity;
        if (otherUid == uid)
            return;

        // Sprinter vs Sprinter collision
        if (TryComp(otherUid, out SprinterComponent? otherSprinter) &&
            (otherSprinter.IsSprinting || HasComp<ActiveSandevistanUserComponent>(otherUid)))
        {
            if (uid.Id < otherUid.Id)
                return;

            _stunSystem.TryKnockdown(uid, sprinter.KnockdownDurationOnInterrupt, false, true);
            _stunSystem.TryKnockdown(otherUid, otherSprinter.KnockdownDurationOnInterrupt, false, true);
            return;
        }

        // Avoid re-triggering if already knocked down or concussed
        if (HasComp<KnockedDownComponent>(uid) || HasComp<ConcussedComponent>(uid))
            return;

        // Wall or static impassable obstacle
        var isStatic = args.OtherBody.BodyType == BodyType.Static;
        var isImpassable = (args.OtherFixture.CollisionLayer & (int)(CollisionGroup.Impassable |
                                                                     CollisionGroup.HighImpassable |
                                                                     CollisionGroup.MidImpassable |
                                                                     CollisionGroup.WallLayer)) != 0;

        if (!isStatic && !isImpassable)
            return;

        // Ensure sprinter had substantial movement velocity
        var velocity = args.OurBody.LinearVelocity;
        if (velocity.Length() < 2.0f)
            return;

        // Immediately stop sprinting
        ToggleSprint(uid, sprinter, false, gracefulStop: false);

        var duration = TimeSpan.FromSeconds(5.0);

        // Concussion state (drives double/triple vision, blur and smooth recovery)
        var concussed = EnsureComp<ConcussedComponent>(uid);
        concussed.StartTime = _timing.CurTime;
        concussed.EndTime = _timing.CurTime + duration;
        Dirty(uid, concussed);

        // Ear ringing directly to the affected player
        _audioSystem.PlayEntity(new SoundPathSpecifier("/Audio/Effects/short_ear_ring.ogg"), Filter.Entities(uid), uid, false, AudioParams.Default.WithVolume(4.5f));

        // Impact audio and body fall to PVS
        _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/_NigWeb/Weapons/Ascenta/wallhit1.ogg"), uid, AudioParams.Default.WithVolume(3.5f));
        _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Effects/bodyfall1.ogg"), uid, AudioParams.Default.WithVolume(2f));

        // Camera recoil away from the collision normal
        if (velocity != Vector2.Zero)
            _recoil.KickCamera(uid, -Vector2.Normalize(velocity) * 8f);

        // Knockdown and stun
        _stunSystem.TryKnockdown(uid, duration, true, true);
        _stunSystem.TryAddStunDuration(uid, duration);

        // Jittering tremor
        _jittering.DoJitter(uid, duration, true, 12f, 12f);

        // Head blunt damage
        if (_proto.TryIndex<DamageTypePrototype>("Blunt", out var bluntProto))
        {
            var damage = new DamageSpecifier(bluntProto, 12);
            _damageableSystem.TryChangeDamage(uid, damage, origin: otherUid);
        }

        // Popups
        _popup.PopupEntity("Вы со всего разгона врезаетесь в препятствие и теряете ориентацию!", uid, uid, PopupType.LargeCaution);
        var mobName = MetaData(uid).EntityName;
        _popup.PopupEntity($"{mobName} со всего разгона врезается в препятствие!", uid, Filter.PvsExcept(uid), true, PopupType.LargeCaution);
    }
}
