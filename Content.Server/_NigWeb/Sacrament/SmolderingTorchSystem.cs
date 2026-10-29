// SPDX-License-Identifier: MIT
using Content.Server.Popups;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._NigWeb.Sacrament;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;

namespace Content.Server._NigWeb.Sacrament;

/// <summary>
/// Controls the Smoldering Brand (Смоляной Светоч).
/// Can be lit or extinguished. Emits warm light and deals bonus heat damage when striking enemies.
/// Cumulative 10-minute burn lifetime; when exhausted, becomes spent.
/// </summary>
public sealed class SmolderingTorchSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[SmolderingTorchSystem] Initializing SmolderingTorchSystem...");

        SubscribeLocalEvent<SmolderingTorchComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SmolderingTorchComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<SmolderingTorchComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
        SubscribeLocalEvent<SmolderingTorchComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<SmolderingTorchComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SmolderingTorchComponent>();
        while (query.MoveNext(out var uid, out var torch))
        {
            if (!torch.IsLit)
                continue;

            torch.RemainingBurnTime -= frameTime;
            if (torch.RemainingBurnTime <= 0f)
            {
                Log.Info($"[SmolderingTorchSystem] Torch {uid} burned out completely!");
                torch.RemainingBurnTime = 0f;
                torch.IsSpent = true;
                SetLit(uid, torch, false, playSound: true);
                _popup.PopupEntity(Loc.GetString("sacrament-torch-burned-out"), uid, PopupType.MediumCaution);
            }
        }
    }

    private void OnUseInHand(Entity<SmolderingTorchComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        Log.Debug($"[SmolderingTorchSystem] Brand {ent.Owner} used in hand by {args.User}");
        SetLit(ent.Owner, ent.Comp, !ent.Comp.IsLit, args.User);
        args.Handled = true;
    }

    private void OnGetVerbs(Entity<SmolderingTorchComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var uid = ent.Owner;
        var comp = ent.Comp;
        var user = args.User;

        ActivationVerb verb = new()
        {
            Text = comp.IsLit
                ? Loc.GetString("sacrament-torch-snuff-verb")
                : Loc.GetString("sacrament-torch-light-verb"),
            Priority = 1,
            Act = () =>
            {
                Log.Debug($"[SmolderingTorchSystem] Brand verb toggled on {uid} by {user}");
                SetLit(uid, comp, !comp.IsLit, user);
            }
        };

        args.Verbs.Add(verb);
    }

    private void OnMapInit(Entity<SmolderingTorchComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.IsLit)
        {
            SetLit(ent.Owner, ent.Comp, true, playSound: false);
        }
    }

    public void SetLit(EntityUid uid, SmolderingTorchComponent comp, bool lit, EntityUid? user = null, bool playSound = true)
    {
        Log.Info($"[SmolderingTorchSystem] Setting lit state of {uid} to {lit} (by {user})...");

        if (lit && (comp.IsSpent || comp.RemainingBurnTime <= 0f))
        {
            Log.Debug($"[SmolderingTorchSystem] Torch {uid} is spent; cannot be lit.");
            if (user != null)
                _popup.PopupEntity(Loc.GetString("sacrament-torch-is-spent"), uid, user.Value, PopupType.SmallCaution);
            return;
        }

        comp.IsLit = lit;
        Dirty(uid, comp);

        _appearance.SetData(uid, SmolderingTorchVisuals.Lit, lit);
        _item.SetHeldPrefix(uid, lit ? "lit" : "unlit");

        if (TryComp<PointLightComponent>(uid, out var light))
        {
            _pointLight.SetEnabled(uid, lit, light);
        }

        if (lit)
        {
            if (playSound)
                _audio.PlayPvs(comp.LightSound, uid);
            if (user != null)
                _popup.PopupEntity(Loc.GetString("sacrament-torch-lit-popup"), uid, user.Value, PopupType.Small);
        }
        else
        {
            if (playSound)
                _audio.PlayPvs(comp.SnuffSound, uid);
            if (user != null)
                _popup.PopupEntity(Loc.GetString("sacrament-torch-snuffed-popup"), uid, user.Value, PopupType.Small);
        }
    }

    private void OnGetMeleeDamage(Entity<SmolderingTorchComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (!ent.Comp.IsLit)
            return;

        Log.Debug($"[SmolderingTorchSystem] Applying bonus heat damage from brand {ent.Owner}");
        var currentHeat = args.Damage.DamageDict.GetValueOrDefault("Heat");
        args.Damage.DamageDict["Heat"] = currentHeat + ent.Comp.BonusHeatDamage;
    }

    private void OnExamined(Entity<SmolderingTorchComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        var minutes = (int)(comp.RemainingBurnTime / 60);
        var seconds = (int)(comp.RemainingBurnTime % 60);
        var timeStr = $"{minutes:D2}:{seconds:D2}";

        if (comp.IsSpent)
        {
            args.PushMarkup(Loc.GetString("sacrament-torch-examined-spent"));
        }
        else if (comp.IsLit)
        {
            args.PushMarkup(Loc.GetString("sacrament-torch-examined-lit-time", ("time", timeStr)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("sacrament-torch-examined-unlit-time", ("time", timeStr)));
        }
    }
}
