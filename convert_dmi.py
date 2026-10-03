# -*- coding: utf-8 -*-
import json
import zlib
import os
from PIL import Image

dmi_path = 'Z:/fork/OpenSourceWeb/icons/obj/surgery.dmi'
rsi_path = 'Z:/fork/NigWeb/Resources/Textures/_NigWeb/Teeth/teeth.rsi'

os.makedirs(rsi_path, exist_ok=True)

with open(dmi_path, 'rb') as f:
    data = f.read()

idx = data.find(b'zTXtDescription\x00')
if idx == -1:
    print("No zTXt")
    exit(1)

chunk_data = data[idx+16:]
compressed = chunk_data[1:chunk_data.find(b'IEND')-8]
desc = zlib.decompress(compressed).decode('utf-8')

states = []
current_state = None
for line in desc.split('\n'):
    line = line.strip()
    if line.startswith('state ='):
        name = line.split('"')[1]
        current_state = {'name': name, 'dirs': 1, 'frames': 1}
        states.append(current_state)
    elif line.startswith('dirs =') and current_state:
        current_state['dirs'] = int(line.split('=')[1].strip())
    elif line.startswith('frames =') and current_state:
        current_state['frames'] = int(line.split('=')[1].strip())

img = Image.open(dmi_path)
width, height = img.size
cell_w, cell_h = 32, 32
cols = width // cell_w

# Save as single spritesheet for RSI, but let's just make it simple: 
# Each state gets its own PNG if we want, OR we just save the whole thing as a single sprite.png and define meta.json!
# Actually, the simplest RSI is ONE `sprite.png` with all frames in a row/grid, but `meta.json` specifies the delays.
# But for SS14, DMI is just a grid.
# DMI maps frames sequentially: index = 0, 1, 2...
# We can just write out each state into its own PNG, or a single sprite sheet.
# SS14 `meta.json` structure for a single file:

meta_states = []
frame_idx = 0

for s in states:
    frames = s['frames']
    dirs = s['dirs']
    total_frames = frames * dirs
    
    # Let's extract the frames for this state into a new image
    # Actually, let's just write them as separate PNGs for simplicity, or 1 row per state!
    # A lot of states are 1 frame.
    state_img = Image.new("RGBA", (cell_w * dirs, cell_h * frames))
    
    for f in range(frames):
        for d in range(dirs):
            idx = frame_idx + f * dirs + d
            row = idx // cols
            col = idx % cols
            crop = img.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h))
            state_img.paste(crop, (d * cell_w, f * cell_h))
            
    # Save the state PNG
    state_file = f"{s['name']}.png"
    # invalid chars? 
    safe_name = s['name'].replace('.', '_').replace(' ', '_')
    state_img.save(os.path.join(rsi_path, f"{safe_name}.png"))
    
    meta_state = {
        "name": s['name'],
        "directions": dirs,
        "delays": [ [0.1] * frames ] * dirs if frames > 1 else None
    }
    # remove None
    if not meta_state["delays"]:
        del meta_state["delays"]
    meta_states.append(meta_state)
    
    frame_idx += total_frames

meta = {
    "version": 1,
    "size": {"x": 32, "y": 32},
    "states": meta_states
}

with open(os.path.join(rsi_path, 'meta.json'), 'w') as f:
    json.dump(meta, f, indent=4)

print("Converted DMI to RSI!")
