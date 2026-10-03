import json
import os
from PIL import Image, ImageDraw

rsi_path = 'Z:/fork/NigWeb/Resources/Textures/_NigWeb/Teeth/teeth.rsi'

# Create wooden implant
img_wood = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
draw = ImageDraw.Draw(img_wood)
# U shape in the center (from x=10 to 22, y=12 to 20)
pixels_wood = [
    (10,12), (11,12), (20,12), (21,12),
    (10,13), (11,13), (20,13), (21,13),
    (10,14), (11,14), (20,14), (21,14),
    (10,15), (11,15), (20,15), (21,15),
    (11,16), (12,16), (19,16), (20,16),
    (12,17), (13,17), (18,17), (19,17),
    (13,18), (14,18), (17,18), (18,18),
    (14,19), (15,19), (16,19), (17,19)
]
for x, y in pixels_wood:
    draw.point((x, y), fill=(139, 69, 19, 255)) # SaddleBrown
for x, y in [(11,12), (20,12), (11,13), (20,13), (12,16), (19,16), (13,17), (18,17), (14,18), (17,18), (15,19), (16,19)]:
    draw.point((x, y), fill=(184, 134, 11, 255)) # DarkGoldenRod for highlights

img_wood.save(os.path.join(rsi_path, 'wooden_implant.png'))

# Create steel implant
img_steel = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
draw = ImageDraw.Draw(img_steel)
for x, y in pixels_wood:
    draw.point((x, y), fill=(105, 105, 105, 255)) # DimGray
for x, y in [(11,12), (20,12), (11,13), (20,13), (12,16), (19,16), (13,17), (18,17), (14,18), (17,18), (15,19), (16,19)]:
    draw.point((x, y), fill=(211, 211, 211, 255)) # LightGray for highlights
for x, y in [(10,12), (21,12)]:
    draw.point((x, y), fill=(255, 255, 255, 255)) # White for shiny edge

img_steel.save(os.path.join(rsi_path, 'steel_implant.png'))

# Update meta.json
meta_path = os.path.join(rsi_path, 'meta.json')
with open(meta_path, 'r', encoding='utf-8') as f:
    meta = json.load(f)

states = [s['name'] for s in meta['states']]
if 'wooden_implant' not in states:
    meta['states'].append({'name': 'wooden_implant', 'directions': 1})
if 'steel_implant' not in states:
    meta['states'].append({'name': 'steel_implant', 'directions': 1})

with open(meta_path, 'w', encoding='utf-8') as f:
    json.dump(meta, f, indent=4)

print('Sprites created and meta.json updated!')
