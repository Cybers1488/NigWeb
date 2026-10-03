using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.CombatMode;
using Content.Goobstation.Common.Medical;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Log;
using Content.Shared.Body.Part;

namespace Content.Server.Agony;

[RegisterComponent]
public sealed partial class LastAttackerComponent : Component
{
    [DataField]
    public EntityUid Attacker;
    
    [DataField]
    public TimeSpan LastDamageTime;
}

public sealed class CombatAmputationLaughSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[CombatAmputationLaughSystem] Initiating...");
        
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged, before: new[] { typeof(Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems.WoundSystem) });
        SubscribeLocalEvent<Content.Shared.Body.Components.BodyComponent, BeforeAmputationDamageEvent>(OnBeforeAmputation);
    }

    private void OnDamageChanged(EntityUid uid, DamageableComponent component, ref DamageChangedEvent args)
    {
        if (args.Origin != null && args.Origin != uid)
        {
            // Only update if it actually dealt damage
            if (args.DamageDelta == null || args.DamageDelta.GetTotal() <= 0)
                return;

            EntityUid targetToTag = uid;
            
            // If the entity hit is a body part, tag the root body instead!
            if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
            {
                targetToTag = part.Body.Value;
            }

            var last = EnsureComp<LastAttackerComponent>(targetToTag);
            last.Attacker = args.Origin.Value;
            last.LastDamageTime = _timing.CurTime;
        }
    }

    private void OnBeforeAmputation(EntityUid uid, Content.Shared.Body.Components.BodyComponent component, ref BeforeAmputationDamageEvent args)
    {
        if (TryComp<LastAttackerComponent>(uid, out var last))
        {
            if (last.Attacker != uid)
            {
                if ((_timing.CurTime - last.LastDamageTime).TotalSeconds < 1.0)
                {
                    if (TryComp<CombatModeComponent>(last.Attacker, out var combat))
                    {
                        if (combat.IsInCombatMode)
                        {
                            _chat.TryEmoteWithChat(last.Attacker, "Laugh", ignoreActionBlocker: true, forceEmote: true);
                            last.Attacker = EntityUid.Invalid;
                        }
                    }
                }
            }
        }
    }
}
