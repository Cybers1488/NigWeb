// SPDX-License-Identifier: MIT
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server._NigWeb.BloodSplatter;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared._NigWeb.Sacrament;
using Content.Shared._Shitmed.Targeting;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._NigWeb.Sacrament;

/// <summary>
/// Handles the Sacrificial Palm Incision mechanic.
/// Players can make a cut on their palm using sharp blades via the context menu (ПКМ)
/// when targeting an arm/hand in the limb targeting interface.
/// </summary>
public sealed class PalmIncisionSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly BloodSplatterSystem _bloodSplatter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[PalmIncisionSystem] Initializing PalmIncisionSystem...");

        SubscribeLocalEvent<SharpComponent, GetVerbsEvent<ActivationVerb>>(OnGetSharpVerbs);
        SubscribeLocalEvent<MeleeWeaponComponent, GetVerbsEvent<ActivationVerb>>(OnGetMeleeVerbs);
        SubscribeLocalEvent<DamageableComponent, GetVerbsEvent<ActivationVerb>>(OnGetDamageableVerbs);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<IncisedPalmComponent>();
        while (query.MoveNext(out var uid, out var incised))
        {
            if (curTime < incised.ExpiresAt)
                continue;

            Log.Debug($"[PalmIncisionSystem] Incision expired for {uid}");
            RemCompDeferred<IncisedPalmComponent>(uid);
            _popup.PopupEntity(Loc.GetString("sacrament-palm-incised-expired"), uid, uid, PopupType.Small);
        }
    }

    private bool IsTargetingArm(EntityUid user)
    {
        if (TryComp<TargetingComponent>(user, out var targeting) &&
            (targeting.Target & TargetBodyPart.FullArms) != 0)
        {
            return true;
        }

        return false;
    }

    private void AddIncisionVerb(EntityUid user, SortedSet<ActivationVerb> verbs, EntityUid tool)
    {
        ActivationVerb verb = new()
        {
            Text = Loc.GetString("sacrament-verb-incise-palm"),
            Priority = 2,
            Act = () =>
            {
                Log.Debug($"[PalmIncisionSystem] Incision verb executed by {user} using tool {tool}");
                TryIncisePalm(user, tool);
            }
        };

        verbs.Add(verb);
    }

    private void OnGetSharpVerbs(Entity<SharpComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!IsTargetingArm(args.User))
            return;

        AddIncisionVerb(args.User, args.Verbs, ent.Owner);
    }

    private void OnGetMeleeVerbs(Entity<MeleeWeaponComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (HasComp<SharpComponent>(ent.Owner))
            return;

        if (!IsSharp(ent.Owner))
            return;

        if (!IsTargetingArm(args.User))
            return;

        AddIncisionVerb(args.User, args.Verbs, ent.Owner);
    }

    private void OnGetDamageableVerbs(Entity<DamageableComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (args.Target != args.User)
            return;

        if (!IsTargetingArm(args.User))
            return;

        var sharp = FindHeldSharpItem(args.User);
        if (sharp == null)
            return;

        AddIncisionVerb(args.User, args.Verbs, sharp.Value);
    }

    public bool TryIncisePalm(EntityUid user, EntityUid? blade)
    {
        Log.Info($"[PalmIncisionSystem] Attempting palm incision on {user} with tool {blade}...");

        if (!user.IsValid() || EntityManager.IsQueuedForDeletion(user))
        {
            Log.Error($"[PalmIncisionSystem] User {user} is invalid or queued for deletion.");
            return false;
        }

        // 1. Check limb targeting: MUST be LeftArm, RightArm, LeftHand, or RightHand
        if (!TryComp<TargetingComponent>(user, out var targeting) ||
            (targeting.Target & TargetBodyPart.FullArms) == 0)
        {
            Log.Debug($"[PalmIncisionSystem] User {user} attempted incision without targeting an arm/hand (Target: {targeting?.Target})");
            _popup.PopupEntity(Loc.GetString("sacrament-palm-need-arm-targeted"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<IncisedPalmComponent>(user))
        {
            Log.Debug($"[PalmIncisionSystem] User {user} already has IncisedPalmComponent.");
            _popup.PopupEntity(Loc.GetString("sacrament-palm-already-incised"), user, user, PopupType.SmallCaution);
            return false;
        }

        // Find or verify sharp tool
        EntityUid? usedTool = blade;
        if (usedTool == null || !IsSharp(usedTool.Value))
        {
            usedTool = FindHeldSharpItem(user);
        }

        if (usedTool == null)
        {
            Log.Debug($"[PalmIncisionSystem] User {user} does not hold a sharp item.");
            _popup.PopupEntity(Loc.GetString("sacrament-palm-need-sharp-blade"), user, user, PopupType.SmallCaution);
            return false;
        }

        // Deal minor slash damage
        var damage = new DamageSpecifier();
        damage.DamageDict["Slash"] = 4;
        _damageable.TryChangeDamage(user, damage, origin: usedTool.Value);

        // Slightly increase bleeding if bloodstream is present
        if (TryComp<BloodstreamComponent>(user, out var bloodstream))
        {
            _bloodstream.TryModifyBleedAmount((user, bloodstream), 0.5f);
            _bloodSplatter.SplatterBlood(user, bloodstream, 2, 0.5f, false);
        }

        // Attach IncisedPalmComponent
        var incised = EnsureComp<IncisedPalmComponent>(user);
        incised.ExpiresAt = _timing.CurTime + incised.Duration;
        Dirty(user, incised);

        // Sound effect: sharp slice + shivering shudder from pain/cold
        _audio.PlayPvs(incised.SliceSound, user);

        SoundSpecifier shiverSound = new SoundCollectionSpecifier("ExhaustMale");
        if (TryComp<HumanoidAppearanceComponent>(user, out var humanoid) && humanoid.Sex == Sex.Female)
        {
            shiverSound = new SoundCollectionSpecifier("ExhaustFemale");
        }
        _audio.PlayPvs(shiverSound, user);

        // Atmospheric visceral popup
        _popup.PopupEntity(Loc.GetString("sacrament-palm-incised-success"), user, user, PopupType.MediumCaution);

        Log.Info($"[PalmIncisionSystem] Successfully incised palm of {user} using {usedTool.Value}. Target limb was {targeting.Target}.");
        return true;
    }

    public bool IsSharp(EntityUid uid)
    {
        if (HasComp<SharpComponent>(uid))
            return true;

        if (TryComp<MeleeWeaponComponent>(uid, out var melee) &&
            melee.Damage.DamageDict.TryGetValue("Slash", out var slash) &&
            slash > 0)
        {
            return true;
        }

        return false;
    }

    private EntityUid? FindHeldSharpItem(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (IsSharp(held))
                return held;
        }

        return null;
    }
}
