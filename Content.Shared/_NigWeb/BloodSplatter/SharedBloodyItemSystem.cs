// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;

namespace Content.Shared._NigWeb.BloodSplatter;

public abstract class SharedBloodyItemSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodyItemComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<BloodyItemComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.BloodLevel >= 0.7f)
        {
            args.PushMarkup(Loc.GetString("[color=#820000]Вещь насквозь пропитана густой запекшейся кровью.[/color]"));
        }
        else if (ent.Comp.BloodLevel >= 0.35f)
        {
            args.PushMarkup(Loc.GetString("[color=#8f1111]Поверхность густо испачкана каплями и брызгами крови.[/color]"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("[color=#9e2222]На поверхности видны засохшие капли крови.[/color]"));
        }
    }
}
