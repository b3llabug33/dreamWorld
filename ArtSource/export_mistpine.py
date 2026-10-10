"""
Exports Mistpine Hollow (ArtSource/Mistpine.blend, made by build_mistpine.py) to FBX for Unity.
  blender.exe -b --factory-startup --python ArtSource/export_mistpine.py
One FBX per piece in Assets/Models/Mistpine/, all in the area's own coordinates (Blender origin = area center),
so Unity places them all at the same spot. Same settings as export_unity.py: vertex colors (sRGB), materials
named Matte/Shiny/Glow/Glass (Unity swaps them for the shared PSX vertex-color materials). The third-party
dressing is not exported; Unity places those prefabs from mistpine_layout.json.
"""
import os

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Assets", "Models", "Mistpine")

bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "Mistpine.blend"))

groups = {}
for o in bpy.data.objects:
    if o.type != "MESH" or o.users_collection and o.users_collection[0].name == "Mist_PackDressing":
        continue
    if o.name.startswith("Mist_Forest_"):
        groups.setdefault(o.name, [o])              # forest chunks stay separate (culling)
    elif o.name.startswith("Mist_"):
        groups.setdefault(o.name, [o])
    elif o.name.startswith("Bld_MullenHouse"):
        groups.setdefault("Mist_MullenHouse", []).append(o)

os.makedirs(OUT, exist_ok=True)
for name, objs in sorted(groups.items()):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", colors_type="SRGB", bake_anim=False, path_mode="STRIP")
    print("EXPORTED", name, len(objs), sum(len(o.data.polygons) for o in objs), "faces")
print("EXPORT OK")
