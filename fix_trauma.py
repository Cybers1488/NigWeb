# -*- coding: utf-8 -*-
import re

with open('Content.Shared/_Shitmed/Surgery/Traumas/Systems/TraumaSystem.Bones.cs', 'r', encoding='utf-8') as f:
    content = f.read()

orig = '''    private void OnBoneSeverityChanged(Entity<BoneComponent> bone, ref BoneSeverityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable == null
            || args.NewSeverity < args.OldSeverity)
            return;'''

new_val = '''    private void OnBoneSeverityChanged(Entity<BoneComponent> bone, ref BoneSeverityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable == null)
            return;
            
        var bodyComp = Comp<BodyPartComponent>(bone.Comp.BoneWoundable.Value);

        if (!bodyComp.Body.HasValue)
            return;
            
        // NigWeb: Heal jaw if skull is healed
        if (args.NewSeverity < args.OldSeverity)
        {
            if (args.NewSeverity < BoneSeverity.Broken && bodyComp.PartType == BodyPartType.Head)
            {
                if (HasComp<Content.Shared._NigWeb.Teeth.BrokenJawComponent>(bodyComp.Body.Value))
                {
                    RemComp<Content.Shared._NigWeb.Teeth.BrokenJawComponent>(bodyComp.Body.Value);
                    _popup.PopupEntity("Ваша челюсть восстановлена.", bodyComp.Body.Value, bodyComp.Body.Value, PopupType.Small);
                }
            }
            return; // Original return for healing
        }'''

content = content.replace(orig, new_val)

# Fix the duplicate var bodyComp = Comp<BodyPartComponent>(bone.Comp.BoneWoundable.Value);
# and if (!bodyComp.Body.HasValue) return; right after since we just added it before.
duplicate = '''        var bodyComp = Comp<BodyPartComponent>(bone.Comp.BoneWoundable.Value);

        if (!bodyComp.Body.HasValue)
            return;

        var part = bodyComp.ParentSlot is null'''

content = content.replace(duplicate, '''        var part = bodyComp.ParentSlot is null''')


with open('Content.Shared/_Shitmed/Surgery/Traumas/Systems/TraumaSystem.Bones.cs', 'w', encoding='utf-8') as f:
    f.write(content)
