import zlib
from PIL import Image, ImageDraw

# Create transparent SlotBackground
bg = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
bg.save(r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\SlotBackground.png')

# Create green highlight
hl = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
draw = ImageDraw.Draw(hl)
draw.rectangle([0, 0, 31, 31], outline=(0, 255, 0, 128), width=2)
hl.save(r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\slot_highlight.png')
hl.save(r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\item_status_left_highlight.png')
hl.save(r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\item_status_right_highlight.png')

def parse_dmi(filename):
    with open(filename, 'rb') as f:
        data = f.read()
        start = data.find(b'zTXtDescription\x00\x00')
        if start != -1:
            compressed = data[start + 17:]
            try:
                return zlib.decompress(compressed).decode('utf-8')
            except Exception:
                pass
    return ''

meta = parse_dmi(r'Z:\fork\OpenSourceWeb\icons\mob\screen1.dmi')
img = Image.open(r'Z:\fork\OpenSourceWeb\icons\mob\screen1.dmi')
width, height = img.size
cols = width // 32

states = {}
current_state = None
current_index = 0

for line in meta.split('\n'):
    line = line.strip()
    if line.startswith('state = '):
        current_state = line.split('\"')[1]
        states[current_state] = {'index': current_index, 'dirs': 1, 'frames': 1}
    elif line.startswith('dirs = '):
        states[current_state]['dirs'] = int(line.split('=')[1].strip())
    elif line.startswith('frames = '):
        states[current_state]['frames'] = int(line.split('=')[1].strip())
        current_index += states[current_state]['dirs'] * states[current_state]['frames']
    elif line == 'state = ""':
        current_state = 'default'
        states[current_state] = {'index': current_index, 'dirs': 1, 'frames': 1}

def extract_state(state_name, out_path, dir=0, frame=0):
    if state_name in states:
        idx = states[state_name]['index'] + frame * states[state_name]['dirs'] + dir
        x = (idx % cols) * 32
        y = (idx // cols) * 32
        cropped = img.crop((x, y, x + 32, y + 32))
        cropped.save(out_path)
        print('Saved', out_path)

# Extract active hand (usually frame 0 is south, which we can use for right or left?)
extract_state('hand_active', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\item_status_left.png', dir=0)
extract_state('hand_active', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\item_status_right.png', dir=1)

