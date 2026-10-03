# -*- coding: utf-8 -*-
import re

with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Add usings
new_content = content.replace('using Content.Shared.Damage;', 'using Content.Shared.Damage;\nusing Content.Shared._Shitmed.Medical.Surgery.Traumas;\nusing Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;\nusing Content.Shared.Body.Part;')

# Add SubscribeLocalEvent
new_content = new_content.replace('SubscribeLocalEvent<TeethComponent, DamageExamineEvent>(OnDamageExamine);', 'SubscribeLocalEvent<TeethComponent, DamageExamineEvent>(OnDamageExamine);\n        SubscribeLocalEvent<BoneComponent, BoneSeverityChangedEvent>(OnBoneSeverityChanged);')

# Add OnBoneSeverityChanged
bone_method = '''
    private void OnBoneSeverityChanged(Entity<BoneComponent> bone, ref BoneSeverityChangedEvent args)
    {
        if (args.NewSeverity != BoneSeverity.Broken || args.OldSeverity == BoneSeverity.Broken)
            return;

        if (bone.Comp.BoneWoundable == null)
            return;

        if (!TryComp<BodyPartComponent>(bone.Comp.BoneWoundable.Value, out var bodyPart) || bodyPart.PartType != BodyPartType.Head)
            return;

        if (!bodyPart.Body.HasValue)
            return;

        var person = bodyPart.Body.Value;
        
        if (HasComp<BrokenJawComponent>(person))
            return;

        if (_net.IsServer)
        {
            AddComp<BrokenJawComponent>(person);
            _popup.PopupEntity("Вам ломает челюсть!", person, person, PopupType.LargeCaution);
        }
    }
'''
new_content = new_content.replace('private void OnDamageExamine', bone_method.strip() + '\n\n    private void OnDamageExamine')

# Replace OnDamageChanged
old_ondamagechanged = re.search(r'private void OnDamageChanged.*?\n        }\n    }', new_content, flags=re.DOTALL).group(0)

new_ondamagechanged = '''private void OnDamageChanged(EntityUid uid, TeethComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;
            
        // Look for blunt damage specifically
        if (!args.DamageDelta.DamageDict.TryGetValue("Blunt", out var bluntDamage) || bluntDamage <= 0)
            return;

        // Ensure we only knock out teeth/break jaw if the attacker targeted the head
        if (args.Origin != null && TryComp<Content.Shared._Shitmed.Targeting.TargetingComponent>(args.Origin, out var targeting))
        {
            if (targeting.Target != Content.Shared._Shitmed.Targeting.TargetBodyPart.Head)
                return;
        }

        if (_net.IsServer)
        {
            float damage = bluntDamage.Float();
            int maxRolls = Math.Max(1, (int)(damage / 10f));
            float chance = Math.Min(damage * component.DropChancePerBlunt, 0.9f);
            
            for (int i = 0; i < maxRolls; i++)
            {
                if (component.CurrentTeeth > 0 && _random.Prob(chance))
                {
                    component.CurrentTeeth--;
                    Dirty(uid, component);

                    // Spawn tooth item
                    var tooth = Spawn(component.ToothPrototype, Transform(uid).Coordinates);
                    
                    // Throw it out randomly
                    var throwDir = _random.NextAngle().ToVec();
                    _throwing.TryThrow(tooth, throwDir, _random.NextFloat(2f, 4f), uid, 0f);
                }
                chance /= 2f; // Decrease chance for consecutive drops
            }
        }
    }'''

new_content = new_content.replace(old_ondamagechanged, new_ondamagechanged)

with open('Content.Shared/_NigWeb/Teeth/TeethSystem.cs', 'w', encoding='utf-8') as f:
    f.write(new_content)
