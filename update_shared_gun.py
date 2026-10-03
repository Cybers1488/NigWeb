file_path = 'Z:/fork/NigWeb/Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.Ballistic.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

replacement1 = '''                if (Exists(existingEnt) && !Deleted(existingEnt))
                {
                    Containers.Remove(existingEnt, ent.Comp.Container);
                    ammoEntity = existingEnt;
                }'''

content = content.replace('''                Containers.Remove(existingEnt, ent.Comp.Container);
                ammoEntity = existingEnt;''', replacement1)

replacement2 = '''        foreach (var ent in entity.Comp.Entities)
        {
            if (Exists(ent) && !Deleted(ent))
            {
                Containers.Remove(ent, entity.Comp.Container);
                QueueDel(ent);
            }
        }'''

content = content.replace('''        foreach (var ent in entity.Comp.Entities)
        {
            Containers.Remove(ent, entity.Comp.Container);
            QueueDel(ent);
        }''', replacement2)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print('Updated SharedGunSystem.Ballistic.cs!')
