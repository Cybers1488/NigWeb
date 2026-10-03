# -*- coding: utf-8 -*-
import re

with open('Content.Shared/_NigWeb/Teeth/BrokenJawSystem.cs', 'r', encoding='utf-8') as f:
    old_content = f.read()

top = '''using Content.Shared.Damage;
using Content.Shared.Speech;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition;
using Robust.Shared.Network;
using Content.Shared.Chemistry.Components;
using Content.Shared.Popups;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction;

namespace Content.Shared._NigWeb.Teeth;

public sealed class BrokenJawSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<BrokenJawComponent, AttemptIngestEvent>(OnAttemptIngest, before: new[] { typeof(IngestionSystem) });
        SubscribeLocalEvent<BrokenJawComponent, AccentGetEvent>(OnAccentGet, before: new[] { typeof(TeethSystem) });
    }

    private void OnAttemptIngest(EntityUid uid, BrokenJawComponent component, ref AttemptIngestEvent args)
    {
        if (args.Handled)
            return;

        var food = args.Ingested;
        
        if (HasComp<DrainableSolutionComponent>(food))
            return;
            
        if (args.Ingest)
        {
            if (args.User == uid)
                _popup.PopupClient("Вам слишком больно жевать со сломанной челюстью!", uid, uid, PopupType.LargeCaution);
            else
                _popup.PopupClient("У него сломана челюсть, он не может жевать!", uid, args.User, PopupType.LargeCaution);
        }

        args.Handled = true;
    }

    private void OnAccentGet'''

bottom = old_content.split('private void OnAccentGet')[1]

with open('Content.Shared/_NigWeb/Teeth/BrokenJawSystem.cs', 'w', encoding='utf-8') as f:
    f.write(top + bottom)
