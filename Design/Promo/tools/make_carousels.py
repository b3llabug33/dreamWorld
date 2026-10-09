"""
Builds ready-to-post TikTok photo-carousel slides (1080x1920) from screenshots in Design/.

  python -I Design/Promo/tools/make_carousels.py

For each carousel in CAROUSELS it:
  - copies the chosen source images, in order, into Design/Promo/carousel_N/source/
  - draws a vertical slide for each into Design/Promo/carousel_N/post/ (twilight #1a1030
    background, image centered, short VT323 caption on top, small footer tag)
Upscaling is nearest-neighbor so the PS1 pixels stay crisp; images bigger than the frame are
shrunk with a box filter (nearest would drop pixel rows unevenly).

Layout keeps text inside TikTok's safe zone: the top ~170px (tabs) and the bottom ~420px
(caption, username, buttons) plus the right ~140px (like/comment column) get covered by the app.
"""
import os
import shutil
import site
import sys

# -I hides the user site-packages, which is where Pillow lives on this machine.
sys.path.append(site.getusersitepackages())
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
PROMO = os.path.dirname(HERE)
DESIGN = os.path.dirname(PROMO)
ROOT = os.path.dirname(DESIGN)
FONT_TEXT = os.path.join(ROOT, "Assets", "Fonts", "VT323-Regular.ttf")
FONT_TITLE = os.path.join(ROOT, "Assets", "Fonts", "PixelifySans.ttf")

W, H = 1080, 1920
BG = (0x1A, 0x10, 0x30)
CREAM = (0xFF, 0xF5, 0xEB)
PINK = (0xFA, 0xA8, 0xC7)
LAVENDER = (0xC7, 0xB8, 0xFA)

# image area: centered horizontally, between the caption and the footer
IMG_MAX_W, IMG_TOP, IMG_BOTTOM = 980, 420, 1440
CAPTION_Y = 200
FOOTER_Y = 1462

# Each slide: (source path relative to Design/, caption lines, options)
#   crop=(l, t, r, b) in source pixels, rows=N splits a wide strip into N stacked rows,
#   first caption line is pink (the "> prompt"), the rest cream.
CAROUSELS = {
    "carousel_1_how_it_started": [
        ("Faces/old_anime_eyes.png",
         ["> dreamWorld faces, v1", "anime eyes. felt uncanny"], {"crop": (0, 0, 840, 360)}),
        ("Faces/old_small_cartoon_eyes.png",
         ["> v2", "tiny cartoon eyes. kinda scary"], {"rows": 2}),
        ("Faces/face_drafts_round1.png",
         ["> v3", "drafted 6 bigger eye styles"], {}),
        ("Screens/creator_extras_tab.png",
         ["> now", "12 eye styles + sliders + extras"], {"crop": (600, 190, 1320, 830)}),
        ("World/backdrop_tech_first_try.png",
         ["> backdrop, first try", "pc parts floating in the dark"], {"crop": (240, 0, 1040, 720)}),
        ("Screens/backdrop_overgrown_login.png",
         ["> backdrop, now", "mushrooms ate the computer"], {"crop": (420, 0, 1500, 1080)}),
    ],
    "carousel_2_make_you": [
        ("Screens/backdrop_overgrown_login.png",
         ["> dreamWorld.exe", "a cozy PS1-style game"], {"crop": (420, 0, 1500, 1080)}),
        ("Screens/your_dreamers.png",
         ["> your players", "up to 4 saved characters"], {"crop": (40, 130, 1240, 1000)}),
        ("Outfits/tank_stripes_shortshorts.png",
         ["> make you", "every piece modeled in code"], {"crop": (440, 160, 1400, 1080)}),
        ("Screens/creator_extras_tab.png",
         ["> extras", "freckles, piercings, 3 at once"], {"crop": (600, 190, 1320, 830)}),
        ("Hair/hairstyles_in_game.png",
         ["> hair", "87 hair variations so far"], {"rows": 2}),
        ("World/mushroom_hollow_player_view.png",
         ["> then walk into town", "mushroom hollow"], {"crop": (350, 0, 1250, 900)}),
    ],
    "carousel_3_mushroom_hollow": [
        ("Screens/backdrop_overgrown_select.png",
         ["> where am i", "a forest growing over an old pc"], {"crop": (620, 0, 1920, 1080)}),
        ("World/mushroom_hollow_player_view.png",
         ["> mushroom hollow", "the first area"], {"crop": (350, 0, 1250, 900)}),
        ("World/mushroom_hollow_plaza.png",
         ["> the plaza", "well, benches, lamps"], {"crop": (200, 0, 1400, 900)}),
        ("World/mushroom_hollow_overview.png",
         ["> from above", "small for now. it will grow"], {"crop": (250, 100, 1350, 900)}),
        ("World/world_kit_props.png",
         ["> every prop", "built by a python script"], {"crop": (140, 10, 1000, 250)}),
    ],
}

FOOTER = "dreamWorld // devlog // in development"


def prepare(img, opts):
    img = img.convert("RGB")
    if "crop" in opts:
        img = img.crop(opts["crop"])
    rows = opts.get("rows", 1)
    if rows > 1:
        w = img.width // rows
        stacked = Image.new("RGB", (w, img.height * rows), BG)
        for i in range(rows):
            stacked.paste(img.crop((i * w, 0, (i + 1) * w, img.height)), (0, i * img.height))
        img = stacked
    return img


def fit(img):
    max_h = IMG_BOTTOM - IMG_TOP
    scale = min(IMG_MAX_W / img.width, max_h / img.height)
    if scale >= 1:
        scale = max(1, int(scale)) if scale >= 2 else scale   # whole-number steps when possible
        method = Image.NEAREST
    else:
        method = Image.BOX
    size = (max(1, round(img.width * scale)), max(1, round(img.height * scale)))
    return img.resize(size, method)


def centered(draw, y, text, font, fill):
    w = draw.textlength(text, font=font)
    draw.text(((W - w) / 2, y), text, font=font, fill=fill)


def slide(src, lines, opts):
    canvas = Image.new("RGB", (W, H), BG)
    draw = ImageDraw.Draw(canvas)
    big = ImageFont.truetype(FONT_TEXT, 76)
    small = ImageFont.truetype(FONT_TEXT, 40)

    y = CAPTION_Y
    for i, line in enumerate(lines):
        centered(draw, y, line, big, PINK if i == 0 else CREAM)
        y += 84

    img = fit(prepare(Image.open(src), opts))
    x = (W - img.width) // 2
    top = IMG_TOP + (IMG_BOTTOM - IMG_TOP - img.height) // 2
    # thin lavender frame like the in-game panels
    draw.rectangle((x - 6, top - 6, x + img.width + 5, top + img.height + 5), outline=LAVENDER, width=3)
    canvas.paste(img, (x, top))

    centered(draw, max(FOOTER_Y, top + img.height + 24), FOOTER, small, LAVENDER)
    return canvas


def main():
    for name, slides in CAROUSELS.items():
        out = os.path.join(PROMO, name)
        src_dir, post_dir = os.path.join(out, "source"), os.path.join(out, "post")
        for d in (src_dir, post_dir):
            os.makedirs(d, exist_ok=True)
        for i, (rel, lines, opts) in enumerate(slides, 1):
            src = os.path.join(DESIGN, rel)
            shutil.copy2(src, os.path.join(src_dir, f"{i:02d}_{os.path.basename(rel)}"))
            slide(src, lines, opts).save(os.path.join(post_dir, f"{i:02d}.png"))
        print(f"{name}: {len(slides)} slides")


if __name__ == "__main__":
    main()
