"""
Third-party nature packs in Blender (for previews and layouts). The same models are placed in Unity from the
layout files, so names here match the prefab names Unity makes (Assets/Prefabs/Nature/<name>.prefab).
  - Aberrant Curse "PSX Forest Pack"   Assets/ThirdParty/AberrantCurse_PSXForest   (credit required)
  - ElegantCrow "Retro PSX Nature Pack" Assets/ThirdParty/ElegantCrow_RetroNature
Aberrant models come in lying on their side (exported Z-up, tagged Y-up): clearing the import rotation stands them up.
"""
import os

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AB = os.path.join(ROOT, "Assets", "ThirdParty", "AberrantCurse_PSXForest")
RN = os.path.join(ROOT, "Assets", "ThirdParty", "ElegantCrow_RetroNature")
AB_TEX = {"BirchTree": "BirchTree", "Bush": "Bush", "LogCabin": "LogCabbin", "LogCabinProps": "CabinProps",
          "Cave Entrance": "CaveEntrance", "RockFace": "RockFace", "Misc": "Misc", "MossyRock": "MossyRock",
          "Tree": "Tree", "Material": "Dirt"}

# name -> (pack, file, default height in meters)
MODELS = {
    "AC_Birch1": ("ab", "BirchTree1", 11.0), "AC_Birch2": ("ab", "BirchTree2", 10.0),
    "AC_Bush1": ("ab", "Bush1", 1.3), "AC_Bush2": ("ab", "Bush2", 1.1), "AC_Bush3": ("ab", "Bush3", 2.2),
    "AC_Cabin": ("ab", "Cabin", 4.6), "AC_CaveMouth": ("ab", "CaveMouth", 9.0),
    "AC_Crystal1": ("ab", "Crystal1", 1.3), "AC_Crystal2": ("ab", "Crystal2", 1.2),
    "AC_Frog": ("ab", "Frog", 0.14), "AC_Owl": ("ab", "Owl", 0.45), "AC_Lantern": ("ab", "Lantern", 1.0),
    "AC_Grass1": ("ab", "Grass1", 0.55), "AC_Grass2": ("ab", "Grass2", 0.5),
    "AC_Mushroom1": ("ab", "Mushroom1", 0.35), "AC_Mushroom2": ("ab", "Mushroom2", 0.5), "AC_Mushroom3": ("ab", "Mushroom3", 0.4),
    "AC_Rock1": ("ab", "Rock1", 1.2), "AC_Rock2": ("ab", "Rock2", 1.8),
    "AC_DeadTree1": ("ab", "Tree1", 8.0), "AC_DeadTree2": ("ab", "Tree2", 7.0), "AC_DeadTree3": ("ab", "Tree3", 7.0),
    "AC_DeadTree4": ("ab", "Tree4", 6.0), "AC_DeadTree5": ("ab", "Tree5", 11.0),
}
for i in range(1, 9):
    MODELS[f"RN_Tree{i}"] = ("rn", f"tree0{i}", 7.5 if i < 7 else 9.0)
    MODELS[f"RN_Bush{i}"] = ("rn", f"bush0{i}", 1.2)
for i in range(1, 10):
    MODELS[f"RN_Grass{i}"] = ("rn", f"grass0{i}", 0.5)
MODELS["RN_GrassBush"] = ("rn", "grass_bush", 0.55)
MODELS["RN_GrassPatch"] = ("rn", "grass_patch", 0.5)

_cache = {}


def _texture(pack, mat_name, season):
    base = mat_name.split(".")[0]
    if pack == "ab":
        return os.path.join(AB, "Textures", AB_TEX.get(base, "Misc") + ".png")
    if base.startswith("tree"):
        return os.path.join(RN, "Textures", f"{base}_{season}.png")
    if base.startswith("bush"):
        p = os.path.join(RN, "Textures", f"{base}_{season}.png")
        return p if os.path.exists(p) else os.path.join(RN, "Textures", f"{base}_{season}1.png")
    if base == "grass_bush":
        return os.path.join(RN, "Textures", f"grass_bush_{season}.png")
    return os.path.join(RN, "Textures", f"grass_{season}.png")


def _material(pack, mat_name, season, tint):
    key = (pack, mat_name.split(".")[0], season, tint)
    name = "PK_" + "_".join(str(k) for k in key)
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.interpolation = "Closest"
    p = _texture(pack, mat_name, season)
    if os.path.exists(p):
        tex.image = bpy.data.images.load(p, check_existing=True)
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    mul.inputs[7].default_value = (*tint, 1.0)
    nt.links.new(tex.outputs["Color"], mul.inputs[6])
    nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])
    nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 1.0
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    m.surface_render_method = "DITHERED"
    return m


def template(name, season="summer", tint=(1.0, 1.0, 1.0)):
    """One hidden mesh for the model: upright, base at z=0, centered, 1 unit = its default height."""
    key = (name, season, tint)
    if key in _cache:
        return _cache[key]
    pack, file, _ = MODELS[name]
    path = os.path.join(AB, "Meshes", file + ".fbx") if pack == "ab" else os.path.join(RN, "Models", file + ".fbx")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.parent is None and pack == "ab":
            o.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    meshes = [o for o in new if o.type == "MESH"]
    # bake every part into one mesh in world space
    import bmesh
    bm = bmesh.new()
    mats = []
    for o in meshes:
        tmp = o.data.copy()
        tmp.transform(o.matrix_world)
        offset = len(mats)
        for s in o.material_slots:
            mats.append(_material(pack, s.material.name if s.material else "Misc", season, tint))
        for p in tmp.polygons:
            p.material_index += offset
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    lo = Vector([min(v.co[i] for v in bm.verts) for i in range(3)])
    hi = Vector([max(v.co[i] for v in bm.verts) for i in range(3)])
    h = max(hi.z - lo.z, 1e-4)
    bmesh.ops.transform(bm, verts=bm.verts, matrix=Matrix.Scale(1.0 / h, 4) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    me = bpy.data.meshes.new("PK_" + name + "_" + season)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    _cache[key] = me
    return me


def place(name, pos, yaw_deg=0.0, height=None, season="summer", tint=(1.0, 1.0, 1.0), collection=None):
    """Put one instance in the scene. Returns a layout record for the game."""
    import math
    me = template(name, season, tint)
    h = height if height is not None else MODELS[name][2]
    o = bpy.data.objects.new(name, me)
    o.location = pos
    o.rotation_euler = (0, 0, math.radians(yaw_deg))
    o.scale = (h, h, h)
    (collection or bpy.context.scene.collection).objects.link(o)
    return {"model": name, "pos": [round(pos[0], 2), round(pos[1], 2), round(pos[2], 2)], "yaw": round(yaw_deg, 1),
            "height": round(h, 2), "season": season, "tint": [round(c, 3) for c in tint]}
