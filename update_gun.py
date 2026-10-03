import os

files = [
    'Z:/fork/NigWeb/Content.Client/Weapons/Ranged/Systems/GunSystem.Ballistic.cs',
    'Z:/fork/NigWeb/Content.Server/Weapons/Ranged/Systems/GunSystem.Ballistic.cs'
]

for file_path in files:
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    replacement = '''            var existing = ent.Comp.Entities[^1];
            ent.Comp.Entities.RemoveAt(ent.Comp.Entities.Count - 1);
            
            if (Exists(existing) && !Deleted(existing))
            {
                Containers.Remove(existing, ent.Comp.Container);
                EnsureShootable(existing);
            }
'''

    content = content.replace('''            var existing = ent.Comp.Entities[^1];
            ent.Comp.Entities.RemoveAt(ent.Comp.Entities.Count - 1);

            Containers.Remove(existing, ent.Comp.Container);
            EnsureShootable(existing);''', replacement)
            
    content = content.replace('''            var existing = ent.Comp.Entities[^1];
            ent.Comp.Entities.RemoveAt(ent.Comp.Entities.Count - 1);
            DirtyField(ent.AsNullable(), nameof(BallisticAmmoProviderComponent.Entities));

            Containers.Remove(existing, ent.Comp.Container);
            EnsureShootable(existing);''', '''            var existing = ent.Comp.Entities[^1];
            ent.Comp.Entities.RemoveAt(ent.Comp.Entities.Count - 1);
            DirtyField(ent.AsNullable(), nameof(BallisticAmmoProviderComponent.Entities));

            if (Exists(existing) && !Deleted(existing))
            {
                Containers.Remove(existing, ent.Comp.Container);
                EnsureShootable(existing);
            }''')

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)

print('Updated both GunSystem.Ballistic.cs files!')
