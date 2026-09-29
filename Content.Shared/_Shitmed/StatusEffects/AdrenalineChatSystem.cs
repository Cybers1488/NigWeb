using Content.Shared.StatusEffect;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Shared._Shitmed.StatusEffects;

public sealed class AdrenalineChatSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StatusEffectsComponent, StatusEffectAddedEvent>(OnStatusEffectAdded);
    }

    private void OnStatusEffectAdded(Entity<StatusEffectsComponent> ent, ref StatusEffectAddedEvent args)
    {
        if (args.Key == "Adrenaline")
        {
            _popup.PopupEntity("Вы чувствуете прилив адреналина!", ent, ent);
        }
    }
}
