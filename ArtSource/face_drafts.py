"""
Face drafts: renders several bigger-eyed cartoon face designs on Dreamer's head so one can
be picked. Each draft is drawn into the same 5-layer eye atlas format build_dreamer.py uses.

  blender.exe -b ArtSource/Dreamer.blend --python ArtSource/face_drafts.py -- <out dir>
Writes face_drafts.png (all drafts, front + 3/4) into <out dir>.
"""
import math
import os
import sys

import bpy
import numpy as np

out_dir = sys.argv[sys.argv.index("--") + 1]
PX, EYE_Y = 128, 66
scene = bpy.context.scene

LINE = (0.16, 0.11, 0.18)
WHITE = (0.99, 0.98, 1.0)
PUPIL = (0.10, 0.07, 0.14)
BLUSH = (0.97, 0.62, 0.68)
MOUTH = (0.55, 0.28, 0.32)

# name: eye spacing, half-width, half-height, iris size (0..1 of eye), outline px, upper lid px,
#       lashes, highlight radius, mouth, brow
DRAFTS = {
    "A  big round":     dict(sp=34, rx=19, ry=20, iris=0.72, outline=2.2, lid=3, lashes=0, shine=4.2, mouth="smile", brow="arc"),
    "B  glossy dot":    dict(sp=33, rx=12, ry=16, iris=0.0, outline=0, lid=0, lashes=0, shine=4.6, mouth="cat", brow="dash"),
    "C  doe":           dict(sp=34, rx=18, ry=21, iris=0.84, outline=2.0, lid=4, lashes=3, shine=4.2, mouth="smile", brow="arc"),
    "D  sparkly":       dict(sp=34, rx=18, ry=19, iris=0.88, outline=1.8, lid=3, lashes=0, shine=4.6, mouth="open", brow="arc", star=True),
    "E  soft almond":   dict(sp=34, rx=20, ry=15, iris=0.80, outline=2.0, lid=4, lashes=2, shine=3.6, mouth="smile", brow="soft"),
    "F  wide toon":     dict(sp=33, rx=17, ry=22, iris=0.55, outline=2.4, lid=3, lashes=0, shine=3.4, mouth="smirk", brow="arc"),
}


def draw(d):
    L = [np.zeros((PX, PX, 4), np.float32) for _ in range(5)]

    def put(layer, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < PX and 0 <= y < PX:
            L[layer][y, x] = (*c, 1)

    def ellipse(layer, cx, cy, rx, ry, c, clip=None):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1 and (clip is None or clip(x, y)):
                    put(layer, x, y, c)

    def ring(layer, cx, cy, rx, ry, w, c):
        for y in range(int(cy - ry) - 2, int(cy + ry) + 3):
            for x in range(int(cx - rx) - 2, int(cx + rx) + 3):
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1 and \
                        ((x - cx) / max(1, rx - w)) ** 2 + ((y - cy) / max(1, ry - w)) ** 2 > 1:
                    put(layer, x, y, c)

    for side in (-1, 1):
        cx, cy = 64 + side * d["sp"], EYE_Y
        rx, ry = d["rx"], d["ry"]
        if d["iris"] == 0:   # glossy dot eyes: the whole eye is the tinted iris
            ellipse(2, cx, cy, rx, ry, (0.30, 0.30, 0.30))
            ellipse(2, cx, cy - ry * 0.35, rx * 0.75, ry * 0.45, (0.55, 0.55, 0.55))
            ellipse(3, cx - 2.5 * side, cy + ry * 0.35, d["shine"], d["shine"] * 1.2, WHITE)
            ellipse(3, cx + 2 * side, cy - ry * 0.45, 1.4, 1.4, WHITE)
        else:
            ellipse(1, cx, cy, rx, ry, WHITE)
            if d["outline"]:
                ring(1, cx, cy, rx, ry, d["outline"], LINE)
            ir = min(rx, ry) * d["iris"]
            iy = cy - ry * 0.08
            ellipse(2, cx, iy, ir, ir * 1.12, (0.70, 0.70, 0.70), clip=lambda x, y: ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 0.92)
            ellipse(2, cx, iy - ir * 0.45, ir * 0.7, ir * 0.45, (0.92, 0.92, 0.92),
                    clip=lambda x, y: ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 0.92)   # lighter lower iris
            ring(2, cx, iy, ir, ir * 1.12, 1.3, (0.42, 0.42, 0.42))
            ellipse(3, cx, iy, ir * 0.42, ir * 0.48, PUPIL)
            ellipse(3, cx - ir * 0.4 * side, iy + ir * 0.42, d["shine"], d["shine"], WHITE)
            ellipse(3, cx + ir * 0.4 * side, iy - ir * 0.5, d["shine"] * 0.45, d["shine"] * 0.45, WHITE)
            if d.get("star"):
                for k in range(-3, 4):
                    put(3, cx + ir * 0.35 * side + k, iy - ir * 0.1, WHITE)
                    put(3, cx + ir * 0.35 * side, iy - ir * 0.1 + k, WHITE)
            # thicker upper lid line along the top of the eye
            for dx in range(-rx, rx + 1):
                y = cy + ry * math.sqrt(max(0, 1 - (dx / rx) ** 2))
                for k in range(d["lid"]):
                    put(1, cx + dx, y - k, LINE)
            for k in range(d["lashes"]):   # tiny outer lashes
                for t in range(4):
                    put(1, cx + side * (rx - 1 + t), cy + ry * 0.5 - k * 4 + t, LINE)
        # closed (blink) layer
        for dx in range(-rx, rx + 1):
            y = cy - 2 + (dx / rx) ** 2 * 4
            put(4, cx + dx, y, LINE); put(4, cx + dx, y + 1, LINE)
        # brows
        top = cy + ry + 5
        for dx in range(-8, 9):
            if d["brow"] == "dash" and abs(dx) > 5:
                continue
            arch = (1 - (dx / 8) ** 2) * (3 if d["brow"] == "arc" else 1.5)
            for k in range(2):
                put(0, cx + dx, top + arch + k, (0.30, 0.22, 0.26))
    for side in (-1, 1):
        ellipse(0, 64 + side * 42, 34, 7, 3.5, BLUSH)
    put(0, 63, 27, (0.85, 0.63, 0.58)); put(0, 64, 26, (0.85, 0.63, 0.58))
    m = d["mouth"]
    if m == "smile":
        for dx in range(-5, 6):
            put(0, 64 + dx, 13 - (1 - (dx / 5) ** 2) * 2, MOUTH)
    elif m == "cat":   # little ":3" / w mouth
        for dx in range(-6, 7):
            put(0, 64 + dx, 13 - abs(math.sin(dx / 6 * math.pi)) * 2, MOUTH)
    elif m == "open":
        ellipse(0, 64, 12, 4, 2.5, MOUTH)
        ellipse(0, 64, 11, 2.5, 1.2, (0.92, 0.55, 0.60))
    elif m == "smirk":
        for dx in range(-5, 6):
            put(0, 64 + dx, 13 - (1 - (dx / 5) ** 2) * 1.5 + dx * 0.15, MOUTH)
    return np.concatenate(L, axis=1)


# ----- scene setup: show body, the Round eyes mesh (we swap its texture), a hairstyle
eyes = bpy.data.objects["Eyes_Round"]
for o in scene.objects:
    if o.type == "MESH":
        o.hide_render = o.name not in ("Skin_Body", "Eyes_Round", "Hair_Long")
img = bpy.data.images.new("Draft", PX * 5, PX, alpha=True)
iris_color = (0.30, 0.55, 0.95, 1)
for i, slot in enumerate(eyes.material_slots):
    m = slot.material
    nodes, links = m.node_tree.nodes, m.node_tree.links
    tex = next(n for n in nodes if n.type == "TEX_IMAGE")
    tex.image = img
    bsdf = nodes["Principled BSDF"]
    if i == 2:   # tint the iris like Unity does
        mix = nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1
        mix.inputs["B"].default_value = iris_color
        links.new(tex.outputs["Color"], mix.inputs["A"]); links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    if i == 4:   # closed lids hidden
        for l in list(bsdf.inputs["Alpha"].links):
            links.remove(l)
        bsdf.inputs["Alpha"].default_value = 0
for o in scene.objects:   # neutral preview colors: skin and brown hair
    if o.type == "MESH" and o.name in ("Skin_Body", "Hair_Long"):
        for slot in o.material_slots:
            nt = slot.material.node_tree
            mix = next((n for n in nt.nodes if n.type == "MIX"), None)
            if mix:
                mix.inputs["B"].default_value = (1.0, 0.84, 0.74, 1) if o.name == "Skin_Body" else (0.35, 0.22, 0.16, 1)

for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        pass
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 2.5
sun.rotation_euler = (math.radians(60), 0, math.radians(-20))
scene.collection.objects.link(sun)
world = bpy.data.worlds.new("W"); world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.50, 0.62, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.2
scene.world = world
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.type = "ORTHO"; cam.data.ortho_scale = 0.34
scene.collection.objects.link(cam); scene.camera = cam
scene.render.resolution_x = scene.render.resolution_y = 360

shots = []
for name, d in DRAFTS.items():
    img.pixels = draw(d).ravel()
    img.update()
    for label, ang in (("front", 0), ("q", 30)):
        a = math.radians(ang)
        cam.location = (math.sin(a) * -3, -math.cos(a) * 3, 1.47)
        cam.rotation_euler = (math.radians(90), 0, math.radians(-ang))
        path = os.path.join(out_dir, f"draft_{name[0]}_{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        shots.append(path)
print("DONE", len(shots))
