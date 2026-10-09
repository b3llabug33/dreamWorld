"""
Clothing audit: renders every piece of one category on the body from the front, side and back,
with the skin in bright red so any skin poking through (clipping) is easy to spot.
  blender.exe -b ArtSource/Dreamer.blend --python ArtSource/preview_pieces.py -- <out dir> <Category>
Writes <Category>_<piece>_<view>.png files.
"""
import math
import os
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
out_dir, category = args[0], args[1]
scene = bpy.context.scene
cam = bpy.data.objects.new("AuditCam", bpy.data.cameras.new("AuditCam"))
cam.data.type = "ORTHO"
scene.collection.objects.link(cam)
scene.camera = cam
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "OBJECT"
scene.display.shading.show_object_outline = True
scene.render.resolution_x, scene.render.resolution_y = 220, 380

meshes = [o for o in scene.objects if o.type == "MESH"]
pieces = sorted(o.name for o in meshes if o.name.startswith(category + "_"))
for o in meshes:
    o.color = (1, 0, 0, 1) if o.name == "Skin_Body" else (0.62, 0.60, 0.78, 1)

# frame the part of the body the category covers
frames = {"Top": (1.12, 0.75), "Bottom": (0.62, 1.15), "Legwear": (0.45, 1.0), "Shoes": (0.2, 0.55),
          "Neck": (1.35, 0.35), "Headwear": (1.55, 0.5), "Glasses": (1.47, 0.35), "Hair": (1.4, 0.75)}
z, size = frames.get(category, (0.85, 1.9))
cam.data.ortho_scale = size
for name in pieces:
    for o in meshes:
        o.hide_render = o.name not in ("Skin_Body", name)
    for label, yaw in (("front", 0), ("side", 90), ("back", 180)):
        a = math.radians(yaw)
        cam.location = (-math.sin(a) * 4, -math.cos(a) * 4, z)
        cam.rotation_euler = (math.radians(90), 0, -a)
        scene.render.filepath = os.path.join(out_dir, f"{category}_{name}_{label}.png")
        bpy.ops.render.render(write_still=True)
print("DONE", category, len(pieces))
