"""
Turnaround renders of the head with each hair style, for checking clipping/bald spots.
Skin is rendered bright red so any exposed scalp jumps out.
  blender.exe -b ArtSource/Dreamer.blend --python ArtSource/preview_heads.py -- <out dir>
Writes head_<Style>_<view>.png for 6 views (front, 3/4, side, back, high front, top).
"""
import math
import os
import sys

import bpy

out_dir = sys.argv[sys.argv.index("--") + 1]
scene = bpy.context.scene
cam_data = bpy.data.cameras.new("HeadCam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 0.5
cam = bpy.data.objects.new("HeadCam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "OBJECT"
scene.render.resolution_x, scene.render.resolution_y = 260, 260

meshes = [o for o in scene.objects if o.type == "MESH"]
for o in meshes:
    o.color = (1, 0, 0, 1) if o.name == "Skin_Body" else (0.25, 0.22, 0.35, 1)
hairs = [o.name for o in meshes if o.name.startswith("Hair_")]

# (label, yaw degrees, pitch degrees down)
VIEWS = (("front", 0, 0), ("q", 45, 0), ("side", 90, 0), ("back", 180, 0), ("high", 20, 35), ("top", 0, 89))
C = (0, 0, 1.50)
for hair in hairs:
    for o in meshes:
        o.hide_render = o.name not in ("Skin_Body", hair)
    for label, yaw, pitch in VIEWS:
        y, p = math.radians(yaw), math.radians(pitch)
        d = 3
        cam.location = (C[0] - math.sin(y) * math.cos(p) * d, C[1] - math.cos(y) * math.cos(p) * d, C[2] + math.sin(p) * d)
        cam.rotation_euler = (math.radians(90) - p, 0, -y)
        scene.render.filepath = os.path.join(out_dir, f"head_{hair}_{label}.png")
        bpy.ops.render.render(write_still=True)
print("DONE", hairs)
