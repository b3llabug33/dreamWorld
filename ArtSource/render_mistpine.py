"""
Preview renders of Mistpine Hollow (open the built Mistpine.blend, overcast light + mist-pass fog).
  blender.exe -b --factory-startup --python ArtSource/render_mistpine.py
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "Mistpine.blend"))
sys.path.insert(0, HERE)
from lowpoly import setup_render  # noqa: E402

OUT = os.path.join(os.path.dirname(HERE), "Design", "World", "mistpine")
FOG = (0.5, 0.56, 0.6)
scene = bpy.context.scene
cam = setup_render(900, bg=FOG)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
cam.data.type = "PERSP"
cam.data.lens = 22
cam.data.clip_end = 1000
for o in scene.objects:
    if o.type == "LIGHT":
        o.data.energy = 1.3
        o.data.color = (0.78, 0.86, 1.0)
        o.rotation_euler = (math.radians(35), 0, math.radians(-40))

# fog: mix the image toward the fog color by the mist pass (distance based)
scene.view_layers[0].use_pass_mist = True
scene.world.mist_settings.start = 6
scene.world.mist_settings.depth = 70
scene.world.mist_settings.falloff = "QUADRATIC"
scene.use_nodes = True
nt = scene.node_tree
nt.nodes.clear()
rl = nt.nodes.new("CompositorNodeRLayers")
mix = nt.nodes.new("CompositorNodeMixRGB")
mix.inputs[2].default_value = (*FOG, 1)
comp = nt.nodes.new("CompositorNodeComposite")
pw = nt.nodes.new("CompositorNodeMath")
pw.operation = "MULTIPLY"
pw.inputs[1].default_value = 0.85
nt.links.new(rl.outputs["Mist"], pw.inputs[0])
nt.links.new(pw.outputs[0], mix.inputs[0])
nt.links.new(rl.outputs["Image"], mix.inputs[1])
nt.links.new(mix.outputs[0], comp.inputs[0])

H = Vector((-30, 10, 0))
M = Vector((-45, -45, 0))
shots = {
    "overview": ((70, -75, 55), (-5, 0, 0), 0.25),
    "path_in": ((72, 3, 2.2), (30, 9, 4), 1),
    "house": ((-10, 12, 2.5), (H.x, H.y, 4), 1),
    "meadow": ((M.x + 13, M.y + 9, 2.5), (M.x, M.y, 1), 1),
    "wolfcamp": ((38, -22, 2.5), (58, -28, 1), 1),
    "quarry": ((40, -40, 9), (30, -60, -1), 1),
    "baseball": ((10, 28, 3), (10, 45, 1), 1),
}
for name, (pos, look, fogk) in shots.items():
    pw.inputs[1].default_value = 0.85 * fogk
    cam.location = Vector(pos)
    cam.rotation_euler = (Vector(look) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(OUT, f"mistpine_{name}.png")
    bpy.ops.render.render(write_still=True)
print("RENDER OK")
