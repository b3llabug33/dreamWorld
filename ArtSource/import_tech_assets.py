# Copies the few computer parts the creator backdrop uses out of Asset Store packages,
# without importing the whole 1.5 GB pack into the project.
#   - Ultimate Pc Computer Pack (Cole): models + albedo/emission textures, shrunk to 512px
#   - Stylized Textures Vol 248 Circuit (Yoge): one circuit-board texture, recolored twilight purple
# Output goes to Assets/ThirdParty/AssetStore/ (gitignored: Asset Store files can't be redistributed).
#
#   python ArtSource/import_tech_assets.py
import io
import os
import tarfile

from PIL import Image

STORE = os.path.expandvars(r"%APPDATA%\Unity\Asset Store-5.x")
PC_PACK = os.path.join(STORE, "Cole", "3D ModelsProps", "Ultimate Pc Computer Pack.unitypackage")
CIRCUIT_PACK = os.path.join(STORE, "Yoge", "Textures Materials", "Stylized Textures - Vol 248 - Circuit.unitypackage")
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "ThirdParty", "AssetStore")

MODELS = ["Monitor_a", "pc_case_b_cover", "cpu_1", "cpu_cooler_e", "headphone_b", "keyboard_d",
          "motherboard_pcie_a", "ram_e_ddr4", "vga_pcie_b"]
# texture name prefixes (the backdrop picks which one each material uses)
TEXTURES = ["Monitor_a", "pc_case_b_body", "pc_case_b_cover", "pc_case_b_wire", "cpu_a__i",
            "cpu_cooler_e_2", "headphone_b_1", "Keyboard_d_2", "motherboard_pcie_a", "ram_a__f", "vga_pcie_b"]
TEX_SIZE = 512
GRAY = ["vga_pcie_b_Albedo"]   # albedos whose colors fight the twilight palette
CIRCUIT = "T_Circuit3"


def read_package(path, wanted):
    """Returns {asset path: bytes} for package entries whose path passes wanted(path)."""
    names = {}
    with tarfile.open(path, "r:gz") as tar:          # pass 1: guid -> asset path
        for m in tar:
            if m.name.endswith("/pathname"):
                p = tar.extractfile(m).read().decode("utf-8").splitlines()[0]
                if wanted(p):
                    names[m.name.split("/")[0]] = p
    found = {}
    with tarfile.open(path, "r:gz") as tar:          # pass 2: grab those assets
        for m in tar:
            guid, _, leaf = m.name.partition("/")
            if leaf == "asset" and guid in names:
                found[names[guid]] = tar.extractfile(m).read()
    return found


def save_texture(data, dest, size=TEX_SIZE, recolor=None):
    im = Image.open(io.BytesIO(data)).convert("RGBA")
    im = im.resize((size, size), Image.LANCZOS)
    if recolor:
        im = recolor(im)
    elif any(os.path.basename(dest).startswith(g) for g in GRAY):
        im = im.convert("L").convert("RGBA")   # loud colors -> gray, tinted twilight in the game
    elif dest.endswith("_Emission.png"):
        # glow maps become white-on-black so the game can tint them twilight colors
        r, g, b, _ = im.split()
        im = Image.merge("RGB", (Image.eval(Image.merge("RGB", (r, g, b)).convert("HSV").split()[2], lambda v: v),) * 3)
    im.save(dest)
    print("  ", os.path.relpath(dest, OUT))


def twilight(im, glow=False):
    """Green PCB -> deep indigo board with lavender/pink traces (glow=True: just the traces, on black)."""
    r, g, b, a = im.split()
    px = []
    for R, G, B in zip(r.get_flattened_data(), g.get_flattened_data(), b.get_flattened_data()):
        lum = (R + G + B) / 3
        warm = max(0, R - B) / 255          # gold traces/pads are the warm (red > blue) pixels
        base = (lum * 0.55 + 18, lum * 0.35 + 10, lum * 0.95 + 40)
        trace = (255, 150, 230)
        t = min(1, warm * 2.2)
        if glow:
            px.append(tuple(int(trace[i] * t * t) for i in range(3)))
        else:
            px.append(tuple(int(min(255, base[i] * (1 - t) + trace[i] * t * (0.5 + lum / 510))) for i in range(3)))
    out = Image.new("RGB", im.size)
    out.putdata(px)
    return out


def main():
    os.makedirs(os.path.join(OUT, "PCParts", "Textures"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "Circuit"), exist_ok=True)

    print("PC parts:")
    def pc_wanted(p):
        base = os.path.splitext(os.path.basename(p))[0]
        if p.endswith(".fbx"):
            return base in MODELS
        return p.endswith(".png") and any(base in (t + "_AlbedoTransparency", t + "_Emission") for t in TEXTURES)
    for path, data in read_package(PC_PACK, pc_wanted).items():
        name = os.path.basename(path)
        if name.endswith(".fbx"):
            with open(os.path.join(OUT, "PCParts", name), "wb") as f:
                f.write(data)
            print("  ", "PCParts/" + name)
        else:
            save_texture(data, os.path.join(OUT, "PCParts", "Textures", name))

    print("Circuit:")
    found = read_package(CIRCUIT_PACK, lambda p: os.path.basename(p).startswith(CIRCUIT + "_Color"))
    for path, data in found.items():
        save_texture(data, os.path.join(OUT, "Circuit", "Circuit_Twilight.png"), 512, twilight)
        save_texture(data, os.path.join(OUT, "Circuit", "Circuit_Glow.png"), 512, lambda im: twilight(im, glow=True))


if __name__ == "__main__":
    main()
