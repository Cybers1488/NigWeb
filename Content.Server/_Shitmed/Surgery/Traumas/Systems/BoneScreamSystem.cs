using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server._Shitmed.Medical.Surgery.Traumas.Systems;

public sealed class BoneScreamSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BoneSeverityChangedEvent>(OnBoneSeverityChanged);
    }

    private void OnBoneSeverityChanged(ref BoneSeverityChangedEvent args)
    {
        var component = args.Bone.Comp;
        var uid = args.Bone.Owner;

        if (args.NewSeverity != BoneSeverity.Broken || args.OldSeverity == BoneSeverity.Broken)
            return;

        if (component.BoneWoundable == null)
            return;
            
        if (!TryComp<BodyPartComponent>(component.BoneWoundable.Value, out var bodyPart))
            return;
            
        if (bodyPart.Body == null)
            return;

        var screams = bodyPart.PartType switch
        {
            BodyPartType.Groin => new[] { "О БОЖЕ, ГОСПОДИ!! МОЯ ПАХОВАЯ ОБЛАСТЬ!", "АААА!! МОИ ЯЙЦА!!!", "СУКА, МОЙ ПАХ!!!", "БОЖЕ, МОЁ ДОСТОИНСТВО!!!" },
            BodyPartType.Chest => new[] { "МОИ РЕБРА!!!", "ААА, ГРУДИНА!!!", "БЛЯДЬ, В ГРУДИ ХРУСТНУЛО!" },
            BodyPartType.Head => new[] { "МОЙ ЧЕРЕП!!!", "АААА, ГОЛОВА!!!", "О БОЖЕ, ЧЕРЕП ТРЕСНУЛ!" },
            BodyPartType.Arm => new[] { "МОЯ РУКА!!!", "АААА, РУКУ СЛОМАЛО!!!", "О ГОСПОДИ, РУКА ХРУСТНУЛА!" },
            BodyPartType.Hand => new[] { "МОИ ПАЛЬЦЫ!!!", "БЛЯДЬ, МОЯ КИСТЬ!!!", "ААА! КИСТЬ ВДРЕБЕЗГИ!" },
            BodyPartType.Leg => new[] { "МОЯ НОГА!!!", "ААА, НОГА, НОГА!!!", "КОСТЬ, БЛЯДЬ, КОСТЬ СЛОМАЛАСЬ!" },
            BodyPartType.Foot => new[] { "МОЯ СТОПА!!!", "ААА, ПАЛЬЦЫ НА НОГЕ!!!", "УБЛЮДОК, МОЯ НОГА!!!" },
            BodyPartType.Tail => new[] { "МОЙ ХВОСТ!!!", "БЛЯДЬ, ХВОСТ!!!" },
            _ => new[] { "МОИ КОСТИ!!!", "ААААА, БОЛЬ!!!", "БЛЯДЬ, ХРУСТНУЛО!!!" }
        };

        var scream = _random.Pick(screams);

        // Показываем сообщение только самому игроку красным шрифтом (LargeCaution)
        _popup.PopupEntity(scream, bodyPart.Body.Value, bodyPart.Body.Value, PopupType.LargeCaution);
    }
}
