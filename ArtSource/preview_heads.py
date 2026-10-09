"""
Turnaround renders of the head with each hair style, for checking clipping.
  blender.exe -b ArtSource/Dreamer.blend --python ArtSource/preview_heads.py -- <out dir>
Writes one 4-angle strip per hair style: heads_<Style>.png
"""
import math
import os
import sys

import bpy

out_dir = sys.argv[sys.argv.index("--") + 1]
scene = bpy.context.scene
cam_data = bpy.data.cameras.new("HeadCam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 0.45
cam = bpy.data.objects.new("HeadCam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        pass
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
scene.collection.objects.link(sun)
world = bpy.data.worlds.new("W")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.32, 0.45, 1)
scene.world = world
scene.render.resolution_x, scene.render.resolution_y = 360, 400

meshes = [o for o in scene.objects if o.type == "MESH"]
hairs = [o.name for o in meshes if o.name.startswith("Hair_")]

for hair in hairs:
    shown = {"Skin_Body", "Face", hair}
    for o in meshes:
        o.hide_render = o.name not in shown
    for angle in (0, 60, 120, 180):
        a = math.radians(angle)
        cam.location = (math.sin(a) * -4, -math.cos(a) * 4, 1.47)
        cam.rotation_euler = (math.radians(90), 0, math.radians(-angle))
        scene.render.filepath = os.path.join(out_dir, f"head_{hair}_{angle}.png")
        bpy.ops.render.render(write_still=True)
print("DONE", hairs)
