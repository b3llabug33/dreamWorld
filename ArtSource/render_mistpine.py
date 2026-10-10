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

import json
LAY = json.load(open(os.path.join(os.path.dirname(HERE), "Assets", "Models", "Mistpine", "mistpine_layout.json")))
BR = Vector(LAY["spots"]["bridge"])
H = Vector((-30, 10, 0))
M = Vector((-45, -45, 0))
shots = {
    "overview": ((70, -75, 55), (-5, 0, 0), 0.25),
    "path_in": ((72, 3, 2.2), (30, 9, 4), 1),
    "house": ((-10, 12, 2.5), (H.x, H.y, 4), 1),
    "meadow": ((M.x + 13, M.y + 9, 2.5), (M.x, M.y, 1), 1),
    "wolfcamp": ((38, -22, 2.5), (58, -28, 1), 1),
    "quarry": ((40, -40, 9), (30, -60, -1), 1),
    "bridge": ((BR.x + 4, BR.y - 11, BR.z + 1.5), (BR.x, BR.y, BR.z - 0.3), 0.6),
    "wolfcamp_logs": ((58 - 6, -28 - 6, 2.0), (58, -28, 0.3), 0.6),
    "cabin": ((LAY["spots"]["jakeob_cabin"][0] - 16, LAY["spots"]["jakeob_cabin"][1] - 9, 3.5), tuple(LAY["spots"]["jakeob_cabin"][:2]) + (2,), 0.6),
    "cave": ((30, -44, 5), (30, -72, 1), 0.6),
    "forest_floor": ((-33, -12, 1.7), (-42, -30, 0.6), 1),
    "baseball": ((10, 28, 3), (10, 45, 1), 1),
}
deps = bpy.context.evaluated_depsgraph_get()


def gz(x, y):
    """Ground height under (x, y): ray down onto the terrain mesh."""
    ground = bpy.data.objects["Mist_Ground"]
    inv = ground.matrix_world.inverted()
    hit, loc, n, i = ground.ray_cast(inv @ Vector((x, y, 300)), Vector((0, 0, -1)))
    return (ground.matrix_world @ loc).z if hit else 0.0


shots["cave"] = ((30, -50, 3), (30, -73, 3), 0.6)
for name, (pos, look, fogk) in shots.items():
    if name != "overview":   # shot heights are above the ground there
        pos = (pos[0], pos[1], gz(pos[0], pos[1]) + pos[2])
        look = (look[0], look[1], gz(look[0], look[1]) + look[2])
    pw.inputs[1].default_value = 0.85 * fogk
    cam.location = Vector(pos)
    cam.rotation_euler = (Vector(look) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(OUT, f"mistpine_{name}.png")
    bpy.ops.render.render(write_still=True)
print("RENDER OK")
