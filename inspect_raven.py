import collections, re, os

with open(r'C:\Users\-\Downloads\OpenSourceWeb-master\OpenSourceWeb-master\maps\new\ravenheartnew.dmm', 'r', encoding='latin1') as f:
    lines = f.readlines()

defs = {}
for line in lines:
    if line.startswith('('): break
    if line.startswith('"'):
        parts = line.split(' = (')
        if len(parts) == 2:
            key = parts[0].strip('"')
            vals = [v.strip().strip('"') for v in parts[1].rstrip(')\r\n').split(',')]
            defs[key] = vals

all_objs = collections.Counter()
for k, vals in defs.items():
    for v in vals:
        base = v.split('{')[0]
        all_objs[base] += 1

print("=== TOP STRUCTURES & DOORS & WINDOWS IN RAVENHEART ===")
for item, count in all_objs.most_common():
    if any(x in item.lower() for x in ['structure', 'door', 'window', 'gate', 'grille', 'fence', 'cage', 'bars']):
        print(f"{count:4d}: {item}")
