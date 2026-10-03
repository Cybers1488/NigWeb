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
    elif line.startswith('delay =') and current_state:
        delays = [float(x)/10.0 for x in line.split('=')[1].split(',')]
        current_state['delays'] = delays

img = Image.open(dmi_path)
width, height = img.size
cell_w, cell_h = 32, 32
cols = width // cell_w

meta_states = []
frame_idx = 0

for s in states:
    frames = s['frames']
    dirs = s['dirs']
    total_frames = frames * dirs
    
    state_img = Image.new("RGBA", (cell_w * dirs, cell_h * frames))
    
    for f in range(frames):
        for d in range(dirs):
            idx = frame_idx + f * dirs + d
            row = idx // cols
            col = idx % cols
            crop = img.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h))
            state_img.paste(crop, (d * cell_w, f * cell_h))
            
    # Remove colon/slash if any, but keep spaces!
    safe_name = s['name'].replace('/', '_').replace('\\', '_').replace(':', '_')
    state_img.save(os.path.join(rsi_path, f"{safe_name}.png"))
    
    delays_list = s.get('delays', [0.1] * frames)
    
    meta_state = {
        "name": safe_name, # Use the safe name as the state name just to be consistent
        "directions": dirs,
        "delays": [ delays_list ] * dirs if frames > 1 else None
    }
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

print("Regenerated RSI!")
