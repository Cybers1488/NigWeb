import zlib
from PIL import Image

def parse_dmi(filename):
    with open(filename, 'rb') as f:
        data = f.read()
        start = data.find(b'zTXtDescription\x00\x00')
        if start != -1:
            compressed = data[start + 17:]
            try:
                meta = zlib.decompress(compressed).decode('utf-8')
                return meta
            except Exception as e:
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
        # Default state
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
    else:
        print('State not found:', state_name)

extract_state('back', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\back.png')
extract_state('id', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\id.png')
extract_state('belt', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\belt.png')
extract_state('gloves', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\gloves.png')
extract_state('shoes', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\shoes.png')
extract_state('glasses', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\eyes.png')
extract_state('ears', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\ears.png')
extract_state('pocket', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\pocket.png')
extract_state('mask', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\mask.png') # Maybe not in screen1?
extract_state('head', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\head.png') # Not in screen1?
extract_state('innerclothing', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\innerclothing.png')
extract_state('outerclothing', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\outerclothing.png')
extract_state('neck', r'Z:\fork\NigWeb\Resources\Textures\Interface\NigWeb\Slots\neck.png')

# Also hands! hand_inactive and hand_active
# Wait, hand_active has 4 dirs! Left, Right? 
# In BYOND, dirs are usually SOUTH, NORTH, EAST, WEST (or SOUTH, EAST, NORTH, WEST).
# SS14 has item_status_left and item_status_right.
