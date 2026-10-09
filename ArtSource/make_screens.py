# Draws the little terminal screens shown on the giant CRT monitors in the creator backdrop.
#   python ArtSource/make_screens.py   ->  Assets/Models/Tech/Textures/Screen_*.png
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(__file__)
OUT = os.path.join(HERE, "..", "Assets", "Models", "Tech", "Textures")
FONT = os.path.join(HERE, "..", "Assets", "Fonts", "VT323-Regular.ttf")
SIZE = 256
BG = (20, 12, 42)
TEXT = (201, 168, 255)
PINK = (255, 143, 216)
DIM = (110, 86, 170)

SCREENS = {
    "Screen_Boot": [
        (PINK, "dreamWorld OS v0.3"),
        (DIM, "------------------"),
        (TEXT, "> mounting dreams..."),
        (TEXT, "> loading wardrobe"),
        (TEXT, "  [#########---] 72%"),
        (TEXT, "> twilight.cfg  OK"),
        (TEXT, "> mushrooms.dat OK"),
        (DIM, ""),
        (PINK, "> make_you.exe _"),
    ],
    "Screen_Stats": [
        (PINK, "SYS MONITOR"),
        (DIM, "-----------"),
        (TEXT, "CPU  ||||||||   48%"),
        (TEXT, "GPU  |||||||||| 91%"),
        (TEXT, "RAM  |||||      33%"),
        (TEXT, "TEMP 41C   FANS ON"),
        (DIM, ""),
        (TEXT, "dreamers online: 1"),
        (PINK, "status: dreaming <3"),
    ],
}


def draw(lines):
    im = Image.new("RGB", (SIZE, SIZE), BG)
    d = ImageDraw.Draw(im)
    font = ImageFont.truetype(FONT, 22)
    y = 18
    for color, text in lines:
        d.text((16, y), text, font=font, fill=color)
        y += 24
    for row in range(0, SIZE, 3):   # baked-in CRT scanlines
        d.line([(0, row), (SIZE, row)], fill=tuple(int(c * 0.55) for c in BG))
    return im


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, lines in SCREENS.items():
        draw(lines).save(os.path.join(OUT, name + ".png"))
        print(name)
