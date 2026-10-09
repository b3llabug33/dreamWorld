# Draws the 16x16 pixel item icons (saved 4x as 64x64 so they stay crisp) for the inventory.
#   python ArtSource/make_icons.py   ->  Assets/Resources/Icons/<item id>.png
import os

from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Icons")
_ = None

PALETTE = {
    "k": (40, 24, 56), "w": (255, 250, 245), "p": (246, 150, 200), "P": (214, 92, 158), "l": (196, 170, 250),
    "L": (150, 112, 230), "v": (104, 70, 180), "c": (120, 230, 240), "C": (60, 170, 205), "s": (250, 236, 210),
    "S": (220, 196, 160), "y": (255, 226, 120), "g": (150, 230, 170),
}

ICONS = {
    # wrapped candy snack
    "snack": [
        "................",
        "................",
        "................",
        "..k.........k...",
        ".kpk.kkkkk.kpk..",
        ".kPpkpppppkpPk..",
        "..kPpwpppppPk...",
        "..kpPwppPpppk...",
        "..kPpppPpppPk...",
        "..kpPppppPpPk...",
        ".kPpkpppppkpPk..",
        ".kpk.kkkkk.kpk..",
        "..k.........k...",
        "................",
        "................",
        "................",
    ],
    # glowing mushroom cap a shroomling drops
    "spore_cap": [
        "................",
        "................",
        ".....kkkkkk.....",
        "...kkLlllllkk...",
        "..kLlwlllLlllk..",
        ".kLlwwlllllwllk.",
        ".kLllllLlllwwlk.",
        ".kLlllllllllllk.",
        "..kkkkkkkkkkkk..",
        ".....ksssk......",
        ".....kssSk......",
        ".....kssSk......",
        ".....kSSSk......",
        "......kkk.......",
        "................",
        "................",
    ],
    # wobbly healing jelly
    "glow_jelly": [
        "................",
        "................",
        "......kkkk......",
        ".....kwcwck.....",
        "....kcwccCck....",
        "...kcccccccCk...",
        "...kcwccccccCk..",
        "..kccccccccCCk..",
        "..kcccccccccCk..",
        "..kCccccccCCCk..",
        "..kCCcccccCCCk..",
        "...kCCCCCCCCk...",
        "....kkkkkkkk....",
        "................",
        "................",
        "................",
    ],
    # rare little crystal
    "dream_shard": [
        "................",
        ".......k........",
        "......kwk.......",
        "......kwlk......",
        ".....kwllk......",
        ".....kwlLLk.....",
        "....kwllLLk.....",
        "....kwlLLLvk....",
        "...kwllLLLvk....",
        "...kwlLLLvvk....",
        "....kllLLvk.....",
        "....kwlLvvk.....",
        ".....klLvk......",
        "......kkk.......",
        "................",
        "................",
    ],
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, rows in ICONS.items():
        img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in PALETTE:
                    img.putpixel((x, y), (*PALETTE[ch], 255))
        img.resize((64, 64), Image.NEAREST).save(os.path.join(OUT, name + ".png"))
        print(name)


if __name__ == "__main__":
    main()
