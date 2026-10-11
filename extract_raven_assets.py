import os, re
from PIL import Image

def parse_and_extract_dmi(dmi_path, output_dir, prefix="", filter_func=None):
    if not os.path.exists(dmi_path):
        return []
    
    img = Image.open(dmi_path)
    desc = img.info.get('Description', '')
    if not desc:
        return []

    # Get tile width and height
    m_w = re.search(r'width = (\d+)', desc)
    m_h = re.search(r'height = (\d+)', desc)
    tile_w = int(m_w.group(1)) if m_w else 32
    tile_h = int(m_h.group(1)) if m_h else 32

    # Parse states
    states = []
    lines = desc.splitlines()
    cur_state = None
    cur_dirs = 1
    cur_frames = 1

    for l in lines:
        l = l.strip()
        if l.startswith('state = "'):
            if cur_state is not None:
                states.append((cur_state, cur_dirs, cur_frames))
            cur_state = l.split('state = "')[1].rstrip('"')
            cur_dirs = 1
            cur_frames = 1
        elif l.startswith('dirs = '):
            cur_dirs = int(l.split('dirs = ')[1])
        elif l.startswith('frames = '):
            cur_frames = int(l.split('frames = ')[1])

    if cur_state is not None:
        states.append((cur_state, cur_dirs, cur_frames))

    cols = img.width // tile_w
    current_tile_idx = 0
    saved_files = []

    os.makedirs(output_dir, exist_ok=True)

    for state_name, dirs, frames in states:
        total_subtiles = dirs * frames
        
        # Check if we want this state
        if filter_func is None or filter_func(state_name):
            safe_name = re.sub(r'[^a-zA-Z0-9_\-]', '_', state_name)
            if not safe_name:
                safe_name = "default"
            
            # Extract first direction, first frame (idle icon)
            tile_idx = current_tile_idx
            tx = (tile_idx % cols) * tile_w
            ty = (tile_idx // cols) * tile_h
            cropped = img.crop((tx, ty, tx + tile_w, ty + tile_h))
            
            out_filename = f"{prefix}{safe_name}.png"
            out_path = os.path.join(output_dir, out_filename)
            cropped.save(out_path)
            saved_files.append((out_filename, out_path, state_name, tile_w, tile_h))

            # If there are open/closed or multiple dirs, maybe also save
            if total_subtiles > 1 and total_subtiles <= 8:
                for sub in range(1, total_subtiles):
                    tile_idx = current_tile_idx + sub
                    tx = (tile_idx % cols) * tile_w
                    ty = (tile_idx // cols) * tile_h
                    sub_cropped = img.crop((tx, ty, tx + tile_w, ty + tile_h))
                    sub_filename = f"{prefix}{safe_name}_dir{sub}.png"
                    sub_path = os.path.join(output_dir, sub_filename)
                    sub_cropped.save(sub_path)
        
        current_tile_idx += total_subtiles

    return saved_files

base_source = r"C:\Users\-\Downloads\OpenSourceWeb-master\OpenSourceWeb-master"
target_root = r"Z:\fork\NigWeb\Test_Ravenheart_Sprites"

categories = {
    "1_Cages_And_Bars": [
        (os.path.join(base_source, r"icons\obj\structures.dmi"), "struct_", lambda s: any(k in s.lower() for k in ['bar', 'grill', 'cemetery', 'gate'])),
        (os.path.join(base_source, r"icons\obj\monkeycage.dmi"), "cage_", None),
        (os.path.join(base_source, r"icons\life\sgate1.dmi"), "sgate1_", None),
        (os.path.join(base_source, r"icons\life\sgate2.dmi"), "sgate2_", None),
        (os.path.join(base_source, r"icons\life\sgate3.dmi"), "sgate3_", None),
        (os.path.join(base_source, r"icons\life\tgate1.dmi"), "tgate1_", None),
        (os.path.join(base_source, r"icons\life\tgate2.dmi"), "tgate2_", None),
        (os.path.join(base_source, r"icons\life\tgate3.dmi"), "tgate3_", None),
    ],
    "2_Windows_And_Glass": [
        (os.path.join(base_source, r"icons\obj\structures.dmi"), "struct_", lambda s: any(k in s.lower() for k in ['window', 'glass'])),
        (os.path.join(base_source, r"icons\obj\podwindows.dmi"), "podwin_", None),
        (os.path.join(base_source, r"icons\obj\graypodwindows.dmi"), "graypod_", None),
        (os.path.join(base_source, r"icons\obj\doors\Door2x1glass.dmi"), "door2x1glass_", None),
        (os.path.join(base_source, r"icons\obj\doors\windoor.dmi"), "windoor_", None),
        (os.path.join(base_source, r"icons\life\doormedglass.dmi"), "medglass1_", None),
        (os.path.join(base_source, r"icons\life\doormedglass2.dmi"), "medglass2_", None),
    ],
    "3_Doors_And_Gates": [
        (os.path.join(base_source, r"icons\obj\doors\Door_wooden.dmi"), "wooddoor_", None),
        (os.path.join(base_source, r"icons\obj\doors\Door2x1church.dmi"), "church2x1_", None),
        (os.path.join(base_source, r"icons\obj\doors\Door2x1keep.dmi"), "keep2x1_", None),
        (os.path.join(base_source, r"icons\obj\doors\Door2x1metal.dmi"), "metal2x1_", None),
        (os.path.join(base_source, r"icons\obj\doors\brothel_door.dmi"), "brothel_", None),
        (os.path.join(base_source, r"icons\obj\doors\mineral_doors.dmi"), "mineral_", None),
        (os.path.join(base_source, r"icons\obj\doors\DoorHazard2x1.dmi"), "hazard2x1_", None),
        (os.path.join(base_source, r"icons\life\gate1.dmi"), "gate1_", None),
        (os.path.join(base_source, r"icons\life\gate2.dmi"), "gate2_", None),
        (os.path.join(base_source, r"icons\life\gate3.dmi"), "gate3_", None),
    ],
    "4_Furniture_And_Torture_Decor": [
        (os.path.join(base_source, r"icons\obj\raventhrone.dmi"), "throne_", None),
        (os.path.join(base_source, r"icons\obj\structures.dmi"), "struct_", lambda s: any(k in s.lower() for k in ['chair', 'bed', 'closet', 'coffin', 'fire', 'statue', 'anvil', 'rack', 'table'])),
        (os.path.join(base_source, r"icons\life\chairs.dmi"), "chair_", None),
        (os.path.join(base_source, r"icons\life\crosses.dmi"), "cross_", None),
        (os.path.join(base_source, r"icons\life\comatic_statue.dmi"), "comatic_", None),
    ]
}

report = []

for cat_name, items in categories.items():
    cat_dir = os.path.join(target_root, cat_name)
    total_in_cat = 0
    print(f"\nProcessing {cat_name}...")
    for dmi_path, prefix, flt in items:
        if os.path.exists(dmi_path):
            extracted = parse_and_extract_dmi(dmi_path, cat_dir, prefix, flt)
            total_in_cat += len(extracted)
            print(f"  From {os.path.basename(dmi_path)}: {len(extracted)} sprites")
        else:
            print(f"  NOT FOUND: {dmi_path}")
    report.append(f"{cat_name}: {total_in_cat} sprites")

print("\n=== SUMMARY ===")
for r in report:
    print(r)
print(f"Target folder: {target_root}")
