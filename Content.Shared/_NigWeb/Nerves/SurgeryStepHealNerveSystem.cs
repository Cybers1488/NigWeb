using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body.Part;

namespace Content.Shared._NigWeb.Nerves;

public sealed class SurgeryStepHealNerveSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryStepHealNerveComponent, SurgeryStepEvent>(OnSurgeryStep);
    }

    private void OnSurgeryStep(Entity<SurgeryStepHealNerveComponent> ent, ref SurgeryStepEvent args)
    {
        if (!args.Complete || !HasComp<SeveredNerveComponent>(args.Part))
            return;

        RemComp<SeveredNerveComponent>(args.Part);
    }
}
