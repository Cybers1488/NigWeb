// SPDX-License-Identifier: MIT
using System;
using Content.Server.Popups;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared._NigWeb.BloodTears;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._NigWeb.BloodTears;

/// <summary>
/// Handles the Blood Tears Wipe emote.
/// When interacting with oneself via context menu, triggers a gothic Blasphemous animation
/// where blood tears well up in the eyes and stream down the cheeks, then the performer wipes them with their hand.
/// </summary>
public sealed class BloodTearsSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string EffectEntityProto = "EffectBloodTearsWipe";
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(3.5);

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[BloodTearsSystem] Initializing BloodTearsSystem...");

        SubscribeLocalEvent<HumanoidAppearanceComponent, GetVerbsEvent<ActivationVerb>>(OnGetHumanoidVerbs);
    }

    private void OnGetHumanoidVerbs(Entity<HumanoidAppearanceComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Only allow wiping one's own blood tears
        if (args.Target != args.User)
            return;

        // Check if alive
        if (TryComp<MobStateComponent>(args.User, out var mobState) && mobState.CurrentState != MobState.Alive)
            return;

        var user = args.User;
        ActivationVerb verb = new()
        {
            Text = Loc.GetString("blood-tears-verb-wipe"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("_NigWeb/Effects/blood_tears.rsi"), "wipe"),
            Priority = -1,
            Act = () => TryPerformBloodTearsWipe(user),
        };

        args.Verbs.Add(verb);
    }

    public bool TryPerformBloodTearsWipe(EntityUid user)
    {
        Log.Info($"[BloodTearsSystem] User {user} attempting to perform blood tears wipe emote...");

        if (!user.IsValid() || EntityManager.IsQueuedForDeletion(user))
        {
            Log.Error($"[BloodTearsSystem] User {user} is invalid or queued for deletion.");
            return false;
        }

        if (TryComp<MobStateComponent>(user, out var mobState) && mobState.CurrentState != MobState.Alive)
        {
            Log.Debug($"[BloodTearsSystem] User {user} cannot emote while dead or in crit.");
            return false;
        }

        var curTime = _timing.CurTime;
        var cd = EnsureComp<BloodTearsCooldownComponent>(user);
        if (curTime < cd.NextEmoteTime)
        {
            Log.Debug($"[BloodTearsSystem] Emote cooldown active for user {user}.");
            _popup.PopupEntity(Loc.GetString("blood-tears-cooldown"), user, user, PopupType.SmallCaution);
            return false;
        }

        cd.NextEmoteTime = curTime + CooldownDuration;
        Dirty(user, cd);

        // 1. Spawn visual effect attached to user
        var coords = Transform(user).Coordinates;
        var effect = Spawn(EffectEntityProto, coords);
        _transform.SetParent(effect, user);

        // 2. Play sound
        _audio.PlayPvs(new SoundCollectionSpecifier("NigWebBloodTearsWipe"), user);

        // 3. Popup
        _popup.PopupEntity(Loc.GetString("blood-tears-wipe-popup-self"), user, user, PopupType.Medium);

        Log.Info($"[BloodTearsSystem] Successfully played blood tears emote on {user}.");
        return true;
    }
}
