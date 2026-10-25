using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.CombatMode;
using Content.Goobstation.Common.Medical;
using Content.Goobstation.Common.Religion;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Log;
using Content.Shared.Body.Part;
using Content.Shared._NigWeb.Religion;
using Content.Shared._NigWeb.Altar;
using Content.Shared.Mobs.Systems;

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
    [Dependency] private readonly MobStateSystem _mobState = default!;

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
        Log.Debug($"[CombatAmputationLaughSystem] Processing BeforeAmputation on entity {uid}");

        if (TryComp<LastAttackerComponent>(uid, out var last))
        {
            if (last.Attacker != uid && last.Attacker.IsValid())
            {
                if ((_timing.CurTime - last.LastDamageTime).TotalSeconds < 1.0)
                {
                    if (TryComp<CombatModeComponent>(last.Attacker, out var combat))
                    {
                        if (combat.IsInCombatMode && _mobState.IsAlive(last.Attacker) && !_mobState.IsIncapacitated(last.Attacker))
                        {
                            if (IsLadyFaithful(last.Attacker))
                            {
                                Log.Info($"[CombatAmputationLaughSystem] Attacker {last.Attacker} belongs to the Ordo Carnis Truncatae - praying to the Lady instead of laughing.");
                                _chat.TryEmoteWithChat(last.Attacker, "PrayLady");
                            }
                            else
                            {
                                Log.Info($"[CombatAmputationLaughSystem] Attacker {last.Attacker} amputated limb - laughing in combat mode.");
                                _chat.TryEmoteWithChat(last.Attacker, "Laugh");
                            }
                            last.Attacker = EntityUid.Invalid;
                        }
                    }
                }
            }
        }
    }

    private bool IsLadyFaithful(EntityUid entity)
    {
        if (!entity.IsValid())
            return false;

        if (HasComp<LadyFaithfulComponent>(entity))
            return true;

        if (HasComp<BibleUserComponent>(entity))
            return true;

        if (HasComp<LadyScriptureComponent>(entity))
            return true;

        return false;
    }
}

