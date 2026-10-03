import codecs
content = '''using Content.Shared.Movement.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Components;

namespace Content.Shared._NigWeb.Nerves;

public sealed class NerveMovementSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnRefreshMovespeed(Entity<BodyComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var hasLegNerveDamage = false;
        var hasTorsoNerveDamage = false;

        var query = EntityQueryEnumerator<SeveredNerveComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var nerve, out var part))
        {
            if (part.Body != ent.Owner)
                continue;

            if (part.PartType == BodyPartType.Leg)
                hasLegNerveDamage = true;
                
            if (part.PartType == BodyPartType.Chest)
                hasTorsoNerveDamage = true;
        }

        if (hasTorsoNerveDamage)
        {
            // Paralyzed lower body
            args.ModifySpeed(0.2f, 0.2f);
        }
        else if (hasLegNerveDamage)
        {
            // Limping
            args.ModifySpeed(0.6f, 0.6f);
        }
    }
}
'''
with codecs.open('Content.Shared/_NigWeb/Nerves/NerveMovementSystem.cs', 'w', 'utf-8') as f:
    f.write(content)
