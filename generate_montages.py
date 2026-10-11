import os, math
from PIL import Image, ImageDraw, ImageFont

artifact_dir = r"C:\Users\-\.gemini\antigravity\brain\72058302-d8df-4084-aa6b-e380f2e92223"
target_root = r"Z:\fork\NigWeb\Test_Ravenheart_Sprites"

categories = [
    ("1_Cages_And_Bars", "preview_cages_and_bars.png", "Cages, Prison Bars & Portcullis Gates"),
    ("2_Windows_And_Glass", "preview_windows_and_glass.png", "Windows, Stained Glass & Windoors"),
    ("3_Doors_And_Gates", "preview_doors_and_gates.png", "Fortress Doors & Castle Gates"),
    ("4_Furniture_And_Torture_Decor", "preview_furniture_decor.png", "Raven Thrones, Altars & Decor")
]

for cat_dir_name, out_img_name, title in categories:
    cat_path = os.path.join(target_root, cat_dir_name)
    if not os.path.exists(cat_path):
        continue
    
    files = sorted([f for f in os.listdir(cat_path) if f.endswith('.png')])
    # Exclude _dir variants to keep it readable, show base icons first
    main_files = [f for f in files if "_dir" not in f]
    if len(main_files) < 10:
        main_files = files[:40]
    else:
        main_files = main_files[:48] # Up to 48 icons per sheet
        
    num_items = len(main_files)
    if num_items == 0:
        continue
        
    cols = 8
    rows = math.ceil(num_items / cols)
    
    cell_w, cell_h = 70, 75
    header_h = 40
    
    sheet_w = cols * cell_w + 20
    sheet_h = rows * cell_h + header_h + 20
    
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (25, 25, 30, 255))
    draw = ImageDraw.Draw(sheet)
    
    # Title
    draw.text((15, 12), f"{title} ({len(files)} total files)", fill=(240, 220, 180, 255))
    
    for idx, fname in enumerate(main_files):
        c = idx % cols
        r = idx // cols
        
        x = 10 + c * cell_w
        y = header_h + 10 + r * cell_h
        
        # Border
        draw.rectangle([x, y, x + cell_w - 4, y + cell_h - 4], fill=(35, 35, 42, 255), outline=(60, 60, 70, 255))
        
        icon_path = os.path.join(cat_path, fname)
        try:
            icon = Image.open(icon_path).convert("RGBA")
            # If large, resize proportionally to fit 40x40
            icon.thumbnail((40, 40), Image.Resampling.NEAREST)
            ix = x + (cell_w - icon.width) // 2
            iy = y + 4 + (40 - icon.height) // 2
            sheet.paste(icon, (ix, iy), icon)
        except Exception as e:
            pass
            
        # Label
        short_name = fname.replace(".png", "")
        if len(short_name) > 10:
            short_name = short_name[:9] + ".."
        draw.text((x + 4, y + 50), short_name, fill=(190, 190, 190, 255))
        
    out_dest = os.path.join(artifact_dir, out_img_name)
    sheet.save(out_dest)
    print(f"Saved montage: {out_dest}")

print("Montages generated successfully!")
