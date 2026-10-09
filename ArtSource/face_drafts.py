"""
Face drafts: renders several bigger-eyed cartoon face designs on Dreamer's head so one can
be picked. Each draft is drawn into the same 5-layer eye atlas format build_dreamer.py uses.

  blender.exe -b ArtSource/Dreamer.blend --python ArtSource/face_drafts.py -- <out dir>
Writes draft PNGs into <out dir>; finished sheets go in Design/Faces/.
"""
import math
import os
import sys

import bpy
import numpy as np

out_dir = sys.argv[sys.argv.index("--") + 1]
PX, EYE_Y = 128, 66
scene = bpy.context.scene

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eye_designs  # noqa: E402

# the current pick first for reference, then the new round of drafts
DRAFTS = {"E  soft almond (current)": eye_designs.FACES["Almond"]}
DRAFTS.update(eye_designs.DRAFTS_2)


def draw(d):
    return eye_designs.draw(d)


# ----- scene setup: show body, the Round eyes mesh (we swap its texture), a hairstyle
eyes = bpy.data.objects["Eyes_Almond"]
for o in scene.objects:
    if o.type == "MESH":
        o.hide_render = o.name not in ("Skin_Body", "Eyes_Almond", "Hair_Long")
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
        path = os.path.join(out_dir, f"draft2_{name[0]}_{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        shots.append(path)
print("DONE", len(shots))
