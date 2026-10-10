"""Makes one labeled contact sheet per prop category from Design/Props/manifest.txt.  python -I ArtSource/props_sheets.py"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
OUT = os.path.join(ROOT, "Design", "Props")
FONT = os.path.join(ROOT, "Assets", "Fonts", "VT323-Regular.ttf")


def label(name):
    return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", name.replace("Prop_", "").replace("Enemy_", "")).lower()


def main():
    import importlib.util
    spec = importlib.util.spec_from_file_location("sheet", os.path.join(HERE, "lowpoly_sheet.py"))
    cats = {}
    for line in open(os.path.join(OUT, "manifest.txt")):
        cat, name = line.strip().split("\t")
        cats.setdefault(cat, []).append(name)
    from PIL import Image, ImageDraw, ImageFont
    f = ImageFont.truetype(FONT, 26)
    ft = ImageFont.truetype(FONT, 40)
    for cat, names in cats.items():
        cols, cell = 5, 300
        rows = (len(names) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * cell, rows * (cell + 34) + 60), (40, 30, 62))
        d = ImageDraw.Draw(sheet)
        d.text((cols * cell // 2, 30), f"dreamWorld props: {cat}", font=ft, fill=(250, 230, 245), anchor="mm")
        for i, n in enumerate(names):
            x, y = (i % cols) * cell, (i // cols) * (cell + 34) + 60
            p = os.path.join(OUT, cat, n + ".png")
            if os.path.exists(p):
                sheet.paste(Image.open(p).convert("RGB").resize((cell, cell)), (x, y + 34))
            d.text((x + cell // 2, y + 17), label(n), font=f, fill=(250, 230, 245), anchor="mm")
        sheet.save(os.path.join(OUT, f"props_{cat}.png"))
        print(cat, len(names))


main()
