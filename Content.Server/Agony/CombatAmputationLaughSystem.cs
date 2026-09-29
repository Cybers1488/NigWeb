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
            EntityUid targetToTag = uid;
            
            // If the entity hit is a body part, tag the root body instead!
            if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
            {
                targetToTag = part.Body.Value;
            }

            var last = EnsureComp<LastAttackerComponent>(targetToTag);
            last.Attacker = args.Origin.Value;
            last.LastDamageTime = _timing.CurTime;
            Log.Debug($"[CombatAmputationLaughSystem] Logged LastAttacker for Entity {targetToTag} (hit {uid}): {args.Origin.Value} at {last.LastDamageTime}");
        }
    }

    private void OnBeforeAmputation(EntityUid uid, Content.Shared.Body.Components.BodyComponent component, ref BeforeAmputationDamageEvent args)
    {
        Log.Debug($"[CombatAmputationLaughSystem] OnBeforeAmputation triggered on Entity {uid}");
        
        if (TryComp<LastAttackerComponent>(uid, out var last))
        {
            Log.Debug($"[CombatAmputationLaughSystem] Found LastAttackerComponent for {uid}. Attacker is {last.Attacker}, time difference: {(_timing.CurTime - last.LastDamageTime).TotalSeconds}s");
            
            if (last.Attacker != uid)
            {
                if ((_timing.CurTime - last.LastDamageTime).TotalSeconds < 1.0)
                {
                    if (TryComp<CombatModeComponent>(last.Attacker, out var combat))
                    {
                        Log.Debug($"[CombatAmputationLaughSystem] Attacker {last.Attacker} combat mode: {combat.IsInCombatMode}");
                        if (combat.IsInCombatMode)
                        {
                            Log.Info($"[CombatAmputationLaughSystem] Attacker {last.Attacker} chopped a limb of {uid} and will now laugh!");
                            _chat.TryEmoteWithChat(last.Attacker, "Laugh", ignoreActionBlocker: true);
                            last.Attacker = EntityUid.Invalid;
                        }
                    }
                    else
                    {
                        Log.Error($"[CombatAmputationLaughSystem] Failed to find CombatModeComponent for attacker {last.Attacker}");
                    }
                }
                else
                {
                    Log.Debug($"[CombatAmputationLaughSystem] Damage too old. Difference is {(_timing.CurTime - last.LastDamageTime).TotalSeconds}s");
                }
            }
        }
        else
        {
            Log.Error($"[CombatAmputationLaughSystem] Failed to find LastAttackerComponent for {uid} during amputation");
        }
    }
}


