import zlib
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

meta = parse_dmi(r'Z:\fork\OpenSourceWeb\icons\mob\screen1.dmi')
img = Image.open(r'Z:\fork\OpenSourceWeb\icons\mob\screen1.dmi')
cols = img.width // 32

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

def extract(name, out, dir=0):
    if name in states:
        i = states[name]['index'] + dir
        img.crop(((i % cols) * 32, (i // cols) * 32, (i % cols) * 32 + 32, (i // cols) * 32 + 32)).save(out)
        print("Saved", out)

extract('zone_sel', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\target_doll.png')
