using Content.Shared.Damage;
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

    private void OnAccentGet(EntityUid uid, BrokenJawComponent component, ref AccentGetEvent args)
    {
        var msg = args.Message;
        if (string.IsNullOrEmpty(msg))
            return;

        // Replace all letters with mumbles
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < msg.Length; i++)
        {
            char ch = msg[i];
            if (char.IsLetter(ch))
            {
                if (char.IsUpper(ch))
                    sb.Append('М');
                else
                    sb.Append('м');
            }
            else
            {
                sb.Append(ch);
            }
        }
        
        var newMsg = sb.ToString();
        
        // Just make it a generic mumble if it's too repetitive
        if (newMsg.Length > 0 && !newMsg.Contains("-"))
        {
             var finalSb = new System.Text.StringBuilder();
             for (int i = 0; i < newMsg.Length; i++)
             {
                 // Replace every ~3rd 'м' with a dash for effect
                 if (i >= 2 && i % 3 == 2 && newMsg[i] == 'м')
                 {
                     finalSb.Append('-');
                 }
                 else
                 {
                     finalSb.Append(newMsg[i]);
                 }
             }
             newMsg = finalSb.ToString();
        }
        
        args.Message = newMsg;
    }
}
