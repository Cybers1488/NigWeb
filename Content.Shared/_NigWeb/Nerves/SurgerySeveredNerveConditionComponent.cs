using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._NigWeb.Nerves;
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Nerves;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySeveredNerveConditionComponent : Component
{
}

public sealed class SurgerySeveredNerveConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgerySeveredNerveConditionComponent, SurgeryValidEvent>(OnConditionValid);
    }

    private void OnConditionValid(Entity<SurgerySeveredNerveConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!HasComp<SeveredNerveComponent>(args.Part))
        {
            args.Cancelled = true;
            return;
        }

        // Must also have an open incision! (User requested this surgery to only appear after opening the incision)
        if (!HasComp<IncisionOpenComponent>(args.Part) || !HasComp<SkinRetractedComponent>(args.Part))
        {
            args.Cancelled = true;
        }
    }
}
