import zlib
import os
from PIL import Image

def parse_dmi(filename):
    with open(filename, 'rb') as f:
        data = f.read()
        start = data.find(b'zTXtDescription\x00\x00')
        if start != -1:
            try:
                return zlib.decompress(data[start + 17:]).decode('utf-8')
            except Exception:
                pass
    return ''

meta = parse_dmi(r'Z:\fork\test_pngs\screen1_White.png')
img = Image.open(r'Z:\fork\test_pngs\screen1_White.png')
cols = img.width // 32

out_dir = r'Z:\fork\test_pngs\screen1_white_pieces'
if not os.path.exists(out_dir):
    os.makedirs(out_dir)

states = {}
idx = 0
current_state = None
for line in meta.split('\n'):
    line = line.strip()
    if line.startswith('state = '):
        current_state = line.split('\"')[1]
        states[current_state] = {'index': idx, 'dirs': 1, 'frames': 1}
    elif line.startswith('dirs = '):
        states[current_state]['dirs'] = int(line.split('=')[1])
    elif line.startswith('frames = '):
        frames = int(line.split('=')[1])
        states[current_state]['frames'] = frames
        idx += states[current_state]['dirs'] * frames
    elif line == 'state = ""':
        current_state = 'default'
        states[current_state] = {'index': idx, 'dirs': 1, 'frames': 1}

for state_name, data in states.items():
    safe_name = state_name.replace('/', '_').replace('+', 'plus').replace('!', 'not_').replace('<', '_').replace('>', '_').replace('*', '_')
    if not safe_name: safe_name = 'default'
    for f in range(data['frames']):
        for d in range(data['dirs']):
            i = data['index'] + f * data['dirs'] + d
            x = (i % cols) * 32
            y = (i // cols) * 32
            cropped = img.crop((x, y, x + 32, y + 32))
            
            suffix = ''
            if data['frames'] > 1 or data['dirs'] > 1:
                suffix = f'_{d}_{f}'
            
            out = os.path.join(out_dir, f'{safe_name}{suffix}.png')
            cropped.save(out)
