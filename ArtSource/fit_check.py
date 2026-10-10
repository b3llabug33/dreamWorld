"""
Renders outfits on every body type straight from Dreamer.blend, to spot clothes that clip through the skin.
  blender -b ArtSource/Dreamer.blend --python ArtSource/fit_check.py -- <out.png> <mesh name> [<mesh name> ...]
Each argument after the png is one outfit (a set of meshes joined with '+', e.g. "Top_Tee+Bottom_SkinnyJeans_High").
The sheet has one row per outfit and a column per body (woman slim/average/curvy/plus, man slim/average/muscle/plus),
front and 3/4 view, using the same blend shape weights as CharacterAppearance.ApplyBody in Unity.
"""
import math
import os
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
OUT, OUTFITS = args[0], [a.split("+") for a in args[1:]]
TMP = os.path.join(os.path.dirname(os.path.abspath(OUT)), "fit_tmp")
os.makedirs(TMP, exist_ok=True)

# woman: Fem 100 + (Slim | - | Curvy | Plus); man: Masc 100 + (Slim | - | Muscle | Plus)
BODIES = [
    {"Fem": 1, "Slim": 1}, {"Fem": 1}, {"Fem": 1, "Curvy": 1}, {"Fem": 1, "Plus": 1},
    {"Masc": 1, "Slim": 1}, {"Masc": 1}, {"Masc": 1, "Muscle": 1}, {"Masc": 1, "Plus": 1},
]

scene = bpy.context.scene
rig = next(o for o in scene.objects if o.type == "ARMATURE")
meshes = [o for o in rig.children if o.type == "MESH"]
scene.render.resolution_x, scene.render.resolution_y = 200, 300
for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        pass
cam_data = bpy.data.cameras.new("FitCam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 1.15
cam = bpy.data.objects.new("FitCam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
sun = bpy.data.objects.new("FitSun", bpy.data.lights.new("FitSun", "SUN"))
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
scene.collection.objects.link(sun)

# flat colors: skin red, clothes blue, so any skin poking through shows up as red
def flat(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nodes = m.node_tree.nodes
    nodes.clear()
    e = nodes.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = (*rgb, 1)
    out = nodes.new("ShaderNodeOutputMaterial")
    m.node_tree.links.new(e.outputs[0], out.inputs[0])
    return m


SKIN, CLOTH = flat("FitSkin", (1.0, 0.05, 0.05)), flat("FitCloth", (0.35, 0.55, 1.0))
LOWER = flat("FitLower", (0.2, 0.85, 0.3))   # bottoms green: pants poking through a top show up too
for obj in meshes:
    mat = SKIN if obj.name == "Skin_Body" else LOWER if obj.name.startswith("Bottom_") else CLOTH
    for i in range(len(obj.material_slots)):
        obj.material_slots[i].link = "OBJECT"
        obj.material_slots[i].material = mat

shots = []
for r, outfit in enumerate(OUTFITS):
    shown = {"Skin_Body", *outfit}
    for obj in meshes:
        obj.hide_render = obj.name not in shown
    for c, body in enumerate(BODIES):
        for obj in meshes:
            keys = obj.data.shape_keys
            if keys is None:
                continue
            for kb in keys.key_blocks[1:]:
                kb.value = body.get(kb.name, 0.0)
        for v, angle in enumerate((0, 35)):
            a = math.radians(angle)
            cam.location = (math.sin(a) * -4, -math.cos(a) * 4, 0.85)
            cam.rotation_euler = (math.radians(90), 0, math.radians(-angle))
            path = os.path.join(TMP, f"r{r}_c{c}_v{v}.png")
            scene.render.filepath = path
            bpy.ops.render.render(write_still=True)
            shots.append((r, c * 2 + v, path))

# contact sheet: rows = outfits, 16 columns
W, H = 200, 300
sheet = bpy.data.images.new("FitSheet", 16 * W, len(OUTFITS) * H)
px = [0.0] * (16 * W * len(OUTFITS) * H * 4)
for r, col, path in shots:
    img = bpy.data.images.load(path)
    src = list(img.pixels)
    row = len(OUTFITS) - 1 - r                     # Blender images start at the bottom
    for y in range(H):
        s0 = y * W * 4
        d0 = ((row * H + y) * 16 * W + col * W) * 4
        px[d0:d0 + W * 4] = src[s0:s0 + W * 4]
    bpy.data.images.remove(img)
sheet.pixels = px
sheet.filepath_raw = OUT
sheet.file_format = "PNG"
sheet.save()
print("FIT SHEET", OUT)
