# Copies the Quaternius (CC0) pieces dreamWorld uses out of the downloaded zips' extracted folders,
# shrinking textures to 256px for the retro look.
#   python ArtSource/import_quaternius.py <extracted root with village/ props/ animals/>
import os
import shutil
import sys

from PIL import Image

SRC = sys.argv[1]
DST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "ThirdParty", "Quaternius")
VILLAGE = os.path.join(SRC, "village", "Medieval Village MegaKit[Standard]")
PROPS = os.path.join(SRC, "props")
ANIMALS = os.path.join(SRC, "animals")

VILLAGE_PIECES = """Wall_Plaster_Straight Wall_Plaster_Door_Round Wall_Plaster_Door_Flat Wall_Plaster_Window_Wide_Round
Wall_Plaster_Window_Thin_Round Wall_Plaster_WoodGrid Wall_UnevenBrick_Straight Wall_UnevenBrick_Door_Round
Wall_UnevenBrick_Window_Wide_Round Wall_UnevenBrick_Window_Thin_Round Floor_WoodDark Floor_WoodLight Floor_Brick
Floor_RedBrick Floor_UnevenBrick Corner_Exterior_Wood Corner_Exterior_Brick Roof_RoundTiles_4x4 Roof_RoundTiles_6x6
Roof_RoundTiles_6x8 Roof_RoundTiles_8x8 Roof_Tower_RoundTiles Roof_Front_Brick4 Roof_Front_Brick6 Roof_Front_Brick8
Prop_Chimney Prop_Vine1 Prop_Vine2 Prop_Vine4 Prop_Wagon Prop_Crate Prop_WoodenFence_Single
Prop_WoodenFence_Extension1 Prop_MetalFence_Ornament Window_Wide_Round1 Window_Thin_Round1
WindowShutters_Wide_Round_Open WindowShutters_Thin_Round_Open Balcony_Simple_Straight Stairs_Exterior_Straight""".split()
PROP_PIECES = """Barrel Barrel_Apples Table_Large Chair_1 Stool Bench Shelf_Simple Shelf_Small_Bottles Bookcase_2 Cauldron
Chandelier Lantern_Wall Stall_Empty Stall_Cart_Empty Crate_Wooden Bed_Twin1 Potion_1 Potion_2 Book_Stack_1
Vase_2 CandleStick_Triple Banner_1 Dummy Workbench Anvil FarmCrate_Apple Mug Coin_Pile Cage_Small Pot_1""".split()
ANIMAL_PIECES = "Fox Deer ShibaInu Alpaca Husky Donkey Horse_White".split()


def copy_models(src_dir, names, dst_dir):
    os.makedirs(dst_dir, exist_ok=True)
    for n in names:
        shutil.copy2(os.path.join(src_dir, n + ".fbx"), os.path.join(dst_dir, n + ".fbx"))


def shrink_textures(src_dir, dst_dir, size=256):
    os.makedirs(dst_dir, exist_ok=True)
    for f in os.listdir(src_dir):
        if not f.endswith(".png") or "Normal" in f or "Roughness" in f or "ORM" in f:
            continue
        im = Image.open(os.path.join(src_dir, f))
        im = im.convert("RGBA" if im.mode in ("RGBA", "LA", "P") else "RGB")
        im.resize((size, size), Image.LANCZOS).save(os.path.join(dst_dir, f))


v, p, a = (os.path.join(DST, d) for d in ("MedievalVillage", "FantasyProps", "Animals"))
copy_models(os.path.join(VILLAGE, "FBX"), VILLAGE_PIECES, v)
shrink_textures(os.path.join(VILLAGE, "Textures"), os.path.join(v, "Textures"))
copy_models(os.path.join(PROPS, "Exports", "FBX"), PROP_PIECES, p)
shrink_textures(os.path.join(PROPS, "Textures"), os.path.join(p, "Textures"))
copy_models(os.path.join(ANIMALS, "FBX"), ANIMAL_PIECES, a)
for folder, lic in ((v, VILLAGE), (p, PROPS), (a, ANIMALS)):
    for f in os.listdir(lic):
        if f.lower().startswith("license"):
            shutil.copy2(os.path.join(lic, f), os.path.join(folder, "License.txt"))
print("done")
