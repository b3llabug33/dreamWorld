"""
Builds the dreamWorld environment kit (PS1-style low-poly props) from code.

Run headless (preview dir optional):
  "C:/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --python ArtSource/build_world.py -- <preview dir>

Every prop is its own object with its pivot at the base center, so Unity can place them.
Textures are small (128px) CC0 photos from ambientCG plus a few generated ones (glowing
mushroom caps, lit windows, lantern glass, leaves). WorldKit.manifest.json lists each prop's
submesh textures, which ones glow (unlit), and what collider it should get.

Blender axes: Z up; doors and fronts face -Y (Unity +Z after export).
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Models", "World")
TEX_DIR = os.path.join(OUT_DIR, "Textures")
SRC_DIR = os.path.join(ROOT, "ArtSource", "ThirdParty", "CC0_World")
PREVIEW_DIR = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else None
os.makedirs(TEX_DIR, exist_ok=True)
rnd = random.Random(3)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- textures

def save_png(name, rgba):
    h, w, _ = rgba.shape
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels = np.clip(rgba, 0, 1).astype(np.float32).ravel()
    img.filepath_raw = os.path.join(TEX_DIR, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def photo(name):
    """A CC0 photo texture, copied as-is (these keep their own colors)."""
    src = bpy.data.images.load(os.path.join(SRC_DIR, name + ".png"))
    w, h = src.size
    px = np.array(src.pixels[:], dtype=np.float32).reshape(h, w, 4)
    return save_png(name, px)


def noise_tex(name, base, spread, size=64, seed=1, spots=None):
    """Flat color with per-texel noise; optional round spots (mushroom caps)."""
    r = np.random.default_rng(seed)
    px = np.ones((size, size, 4), np.float32)
    px[..., :3] = np.array(base) * (1 + r.normal(0, spread, (size, size, 1)))
    if spots:
        color, count, radius = spots
        for _ in range(count):
            cx, cy = r.integers(0, size, 2)
            for y in range(size):
                for x in range(size):
                    dx, dy = min(abs(x - cx), size - abs(x - cx)), min(abs(y - cy), size - abs(y - cy))
                    if dx * dx + dy * dy <= radius * radius:
                        px[y, x, :3] = color
    return save_png(name, px)


def window_tex():
    """Warm lit window: glowing panes with a dark cross frame."""
    n = 32
    px = np.ones((n, n, 4), np.float32)
    px[..., :3] = (1.0, 0.80, 0.45)
    for y in range(n):
        for x in range(n):
            if x < 2 or y < 2 or x >= n - 2 or y >= n - 2 or abs(x - n / 2) < 1.5 or abs(y - n / 2) < 1.5:
                px[y, x, :3] = (0.22, 0.14, 0.10)
            elif y > n * 0.7:
                px[y, x, :3] = (1.0, 0.90, 0.62)   # brighter top: warm lamp inside
    return save_png("WindowGlow", px)


TEX = {name: photo(name) for name in ("Grass", "Paving", "Bark", "Siding", "Planks", "Thatch", "Rock", "Dirt")}
TEX.update({
    "Leaves": noise_tex("Leaves", (0.20, 0.36, 0.30), 0.18, seed=2),
    "LeavesDusk": noise_tex("LeavesDusk", (0.24, 0.24, 0.40), 0.18, seed=4),
    "Stem": noise_tex("Stem", (0.92, 0.86, 0.78), 0.05, seed=5),
    "CapPurple": noise_tex("CapPurple", (0.66, 0.38, 0.95), 0.06, seed=6, spots=((0.95, 0.85, 1.0), 9, 4)),
    "CapBlue": noise_tex("CapBlue", (0.32, 0.62, 1.0), 0.06, seed=7, spots=((0.85, 0.95, 1.0), 9, 4)),
    "CapPink": noise_tex("CapPink", (1.0, 0.50, 0.75), 0.06, seed=8, spots=((1.0, 0.92, 0.95), 9, 4)),
    "LanternGlow": noise_tex("LanternGlow", (1.0, 0.78, 0.42), 0.04, size=16, seed=9),
    "PetalGlow": noise_tex("PetalGlow", (0.95, 0.70, 1.0), 0.05, size=16, seed=10),
    "IronDark": noise_tex("IronDark", (0.16, 0.14, 0.18), 0.08, size=16, seed=11),
    "WindowGlow": window_tex(),
})
GLOW = {"CapPurple", "CapBlue", "CapPink", "LanternGlow", "PetalGlow", "WindowGlow"}

_mats = {}


def mat(name):
    if name not in _mats:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        node = m.node_tree.nodes.new("ShaderNodeTexImage")
        node.image, node.interpolation = TEX[name], "Closest"
        bsdf = m.node_tree.nodes["Principled BSDF"]
        m.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        if name in GLOW:
            m.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = 1.0
        bsdf.inputs["Roughness"].default_value = 1.0
        _mats[name] = m
    return _mats[name]

# ---------------------------------------------------------------- geometry helpers

def loft(bm, rings, n=8, cap_bottom=True, cap_top=True, wobble=0.0):
    """rings: list of (z, rx, ry[, cx, cy]); returns nothing. wobble jitters radii (hand-made look)."""
    vs = []
    for ring in rings:
        z, rx, ry = ring[:3]
        cx, cy = (ring[3], ring[4]) if len(ring) > 3 else (0, 0)
        row = []
        for i in range(n):
            a = 2 * math.pi * i / n + math.pi / n
            w = 1 + rnd.uniform(-wobble, wobble)
            row.append(bm.verts.new((cx + math.cos(a) * rx * w, cy + math.sin(a) * ry * w, z)))
        vs.append(row)
    for a, b in zip(vs, vs[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if cap_bottom:
        bm.faces.new(vs[0][::-1])
    if cap_top:
        bm.faces.new(vs[-1])


def box(bm, lo, hi):
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    v = [bm.verts.new(p) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
                                   (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([v[i] for i in f])


def blob(bm, center, radius, scale=(1, 1, 1), jitter=0.12, subdiv=1):
    """Lumpy low-poly sphere (canopies, bushes, rocks)."""
    before = set(bm.verts)
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius,
                               matrix=Matrix.Translation(center) @ Matrix.Diagonal((*scale, 1)))
    for v in set(bm.verts) - before:
        d = v.co - Vector(center)
        v.co = Vector(center) + d * (1 + rnd.uniform(-jitter, jitter))


def prism(bm, pts2d, y0, y1):
    """Extrude a 2D outline (x, z) between y0 and y1 (gable ends)."""
    front = [bm.verts.new((x, y0, z)) for x, z in pts2d]
    back = [bm.verts.new((x, y1, z)) for x, z in pts2d]
    bm.faces.new(front)
    bm.faces.new(back[::-1])
    n = len(pts2d)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], back[i], back[j], front[j]))


MANIFEST = []


def prop(name, pieces, collider="none", tile=0.6):
    """pieces: list of (material name, build(bm)). Joins them into one object, box-maps UVs."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    slots = []
    for mname, build in pieces:
        if mname not in slots:
            slots.append(mname)
        before = set(bm.faces)
        build(bm)
        for f in set(bm.faces) - before:
            f.material_index = slots.index(mname)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        coords = [((l.vert.co.y, l.vert.co.z), (l.vert.co.x, l.vert.co.z), (l.vert.co.x, l.vert.co.y))[ax] for l in f.loops]
        if slots[f.material_index] == "WindowGlow":
            # each window face shows the whole window texture once
            us, vs = [c[0] for c in coords], [c[1] for c in coords]
            u0, u1, v0, v1 = min(us), max(us), min(vs), max(vs)
            for loop, (u, v) in zip(f.loops, coords):
                loop[uv].uv = ((u - u0) / max(u1 - u0, 1e-4), (v - v0) / max(v1 - v0, 1e-4))
        else:
            for loop, (u, v) in zip(f.loops, coords):
                loop[uv].uv = (u / tile, v / tile)
        f.smooth = False
    bm.to_mesh(me)
    bm.free()
    for s in slots:
        me.materials.append(mat(s))
    obj = bpy.data.objects.new(name, me)
    scene.collection.objects.link(obj)
    MANIFEST.append({"name": name, "collider": collider,
                     "submeshes": [{"texture": s, "unlit": s in GLOW} for s in slots]})
    return obj

# ---------------------------------------------------------------- props

def tree_round():
    return prop("Tree_Round", [
        ("Bark", lambda bm: loft(bm, [(0, 0.2, 0.2), (0.4, 0.16, 0.16), (2.2, 0.11, 0.11)], 7, wobble=0.1)),
        ("Leaves", lambda bm: (blob(bm, (0, 0, 2.9), 1.25), blob(bm, (0.7, 0.2, 2.45), 0.9), blob(bm, (-0.6, -0.35, 2.5), 0.85))),
    ], collider="trunk", tile=0.8)


def tree_pine():
    return prop("Tree_Pine", [
        ("Bark", lambda bm: loft(bm, [(0, 0.17, 0.17), (1.1, 0.12, 0.12)], 6, wobble=0.1)),
        ("LeavesDusk", lambda bm: [loft(bm, [(z0, r, r), (z1, 0.03, 0.03)], 8, wobble=0.12)
                                   for z0, z1, r in ((0.9, 2.6, 1.35), (2.0, 3.6, 1.05), (3.0, 4.5, 0.75))]),
    ], collider="trunk", tile=0.8)


def bush():
    return prop("Bush", [("Leaves", lambda bm: (blob(bm, (0, 0, 0.45), 0.55), blob(bm, (0.45, 0.1, 0.35), 0.4),
                                                blob(bm, (-0.4, -0.1, 0.35), 0.42)))], tile=0.8)


def rock(name, scale):
    return prop(name, [("Rock", lambda bm: blob(bm, (0, 0, scale[2] * 0.35), 0.5, scale, jitter=0.2))],
                collider="box", tile=0.7)


def glow_mushroom(name, cap):
    return prop(name, [
        ("Stem", lambda bm: loft(bm, [(0, 0.14, 0.14), (0.7, 0.10, 0.10), (1.4, 0.09, 0.09)], 8, wobble=0.06)),
        (cap, lambda bm: loft(bm, [(1.32, 0.10, 0.10), (1.36, 0.55, 0.55), (1.46, 0.62, 0.62), (1.60, 0.48, 0.48),
                                   (1.72, 0.24, 0.24), (1.76, 0.02, 0.02)], 10, wobble=0.05)),
    ], collider="trunk", tile=0.5)


def mushroom_cluster():
    pieces = []
    for (x, y, s, cap) in ((0, 0, 0.35, "CapBlue"), (0.28, 0.12, 0.25, "CapPink"), (-0.22, 0.18, 0.2, "CapBlue")):
        pieces.append(("Stem", lambda bm, x=x, y=y, s=s: loft(bm, [(0, 0.12 * s, 0.12 * s, x, y), (1.3 * s, 0.09 * s, 0.09 * s, x, y)], 6)))
        pieces.append((cap, lambda bm, x=x, y=y, s=s: loft(bm, [(1.25 * s, 0.1 * s, 0.1 * s, x, y), (1.3 * s, 0.5 * s, 0.5 * s, x, y),
                                                                  (1.5 * s, 0.4 * s, 0.4 * s, x, y), (1.7 * s, 0.02, 0.02, x, y)], 8)))
    return prop("Mushroom_Cluster", pieces, tile=0.3)


def mushroom_house():
    def windows(bm):
        for x, z in ((-0.55, 1.35), (0.55, 1.35)):
            box(bm, (x - 0.22, -1.08, z - 0.22), (x + 0.22, -0.98, z + 0.22))
    return prop("Mushroom_House", [
        ("Stem", lambda bm: loft(bm, [(0, 1.15, 1.15), (1.2, 1.08, 1.08), (2.1, 1.0, 1.0)], 12, wobble=0.03)),
        ("Planks", lambda bm: box(bm, (-0.35, -1.18, 0), (0.35, -1.0, 1.25))),
        ("WindowGlow", windows),
        ("CapPurple", lambda bm: loft(bm, [(2.0, 0.9, 0.9), (2.1, 2.1, 2.1), (2.4, 2.25, 2.25), (3.0, 1.7, 1.7),
                                           (3.5, 0.75, 0.75), (3.65, 0.05, 0.05)], 14, wobble=0.04)),
        ("Rock", lambda bm: box(bm, (0.6, 0.2, 3.0), (0.85, 0.45, 3.7))),
    ], collider="box", tile=0.6)


def cottage():
    w, d, h = 2.2, 1.7, 2.3      # half-width, half-depth, wall height
    roof_h = 1.6

    def walls(bm):
        box(bm, (-w, -d, 0), (w, d, h))
        for y0, y1 in ((-d, -d + 0.01), (d - 0.01, d)):   # gable triangles
            prism(bm, [(-w, h), (w, h), (0, h + roof_h)], y0 - 0.001 if y0 < 0 else y0, y1 + 0.001 if y1 > 0 else y1)

    def roof(bm):
        for sx in (-1, 1):
            # one slanted slab per side, overhanging the walls
            p0 = Vector((sx * (w + 0.35), 0, h - 0.25))
            p1 = Vector((0, 0, h + roof_h + 0.1))
            for k, (y0, y1) in enumerate(((-d - 0.35, d + 0.35),)):
                t = Vector((0, 0, 0.18))
                vs = [bm.verts.new(p) for p in (p0 + Vector((0, y0, 0)), p1 + Vector((0, y0, 0)), p1 + Vector((0, y1, 0)),
                                                 p0 + Vector((0, y1, 0)))]
                vt = [bm.verts.new(v.co + t) for v in vs]
                bm.faces.new(vs[::-1] if sx > 0 else vs)
                bm.faces.new(vt if sx > 0 else vt[::-1])
                for i in range(4):
                    j = (i + 1) % 4
                    bm.faces.new((vs[i], vs[j], vt[j], vt[i]))

    def windows(bm):
        for x in (-1.3, 1.3):
            box(bm, (x - 0.35, -d - 0.06, 1.0), (x + 0.35, -d + 0.02, 1.7))
        for y in (-0.6, 0.6):
            box(bm, (w - 0.02, y - 0.3, 1.0), (w + 0.06, y + 0.3, 1.6))
            box(bm, (-w - 0.06, y - 0.3, 1.0), (-w + 0.02, y + 0.3, 1.6))

    return prop("Cottage", [
        ("Siding", walls),
        ("Planks", lambda bm: (box(bm, (-0.45, -d - 0.08, 0), (0.45, -d + 0.02, 1.75)),
                               box(bm, (-w - 0.05, -d - 0.05, 0), (w + 0.05, d + 0.05, 0.25)))),
        ("WindowGlow", windows),
        ("Thatch", roof),
        ("Rock", lambda bm: box(bm, (1.0, 0.3, h + 0.6), (1.45, 0.75, h + roof_h + 0.6))),
    ], collider="box", tile=0.7)


def lamp_post():
    return prop("Lamp_Post", [
        ("IronDark", lambda bm: (box(bm, (-0.06, -0.06, 0), (0.06, 0.06, 2.3)), box(bm, (-0.16, -0.16, 0), (0.16, 0.16, 0.18)),
                                 loft(bm, [(2.48, 0.2, 0.2), (2.62, 0.04, 0.04)], 4, cap_bottom=True))),
        ("LanternGlow", lambda bm: loft(bm, [(2.18, 0.13, 0.13), (2.48, 0.16, 0.16)], 6)),
    ], collider="trunk", tile=0.3)


def fence():
    return prop("Fence", [("Planks", lambda bm: (
        box(bm, (-1.05, -0.06, 0), (-0.93, 0.06, 0.95)), box(bm, (0.93, -0.06, 0), (1.05, 0.06, 0.95)),
        box(bm, (-1.05, -0.03, 0.35), (1.05, 0.03, 0.47)), box(bm, (-1.05, -0.03, 0.68), (1.05, 0.03, 0.8))))],
        collider="box", tile=0.5)


def bench():
    return prop("Bench", [("Planks", lambda bm: (
        box(bm, (-0.8, -0.25, 0.42), (0.8, 0.22, 0.5)), box(bm, (-0.8, 0.18, 0.5), (0.8, 0.26, 0.95)),
        [box(bm, (x - 0.05, -0.2, 0), (x + 0.05, 0.2, 0.42)) for x in (-0.65, 0.65)]))],
        collider="box", tile=0.4)


def well():
    return prop("Well", [
        ("Rock", lambda bm: loft(bm, [(0, 0.75, 0.75), (0.8, 0.75, 0.75)], 10, cap_top=False, wobble=0.04)),
        ("Planks", lambda bm: [box(bm, (x - 0.07, -0.07, 0.5), (x + 0.07, 0.07, 2.1)) for x in (-0.7, 0.7)]),
        ("Thatch", lambda bm: prism(bm, [(-1.0, 1.95), (1.0, 1.95), (0, 2.55)], -0.75, 0.75)),
    ], collider="round", tile=0.5)


def flowers():
    pieces = []
    for k in range(6):
        x, y = rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35)
        hgt = rnd.uniform(0.18, 0.32)
        pieces.append(("Leaves", lambda bm, x=x, y=y, hgt=hgt: box(bm, (x - 0.01, y - 0.01, 0), (x + 0.01, y + 0.01, hgt))))
        pieces.append(("PetalGlow", lambda bm, x=x, y=y, hgt=hgt: blob(bm, (x, y, hgt + 0.03), 0.05, (1, 1, 0.6), jitter=0.0, subdiv=1)))
    return prop("Flowers", pieces, tile=0.2)


def signpost():
    return prop("Signpost", [("Planks", lambda bm: (box(bm, (-0.06, -0.06, 0), (0.06, 0.06, 1.6)),
                                                   box(bm, (-0.55, -0.08, 1.1), (0.55, -0.04, 1.5))))],
                collider="trunk", tile=0.4)


def hill():
    def build(bm):
        before = set(bm.verts)
        bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0, matrix=Matrix.Diagonal((14, 10, 5, 1)))
        new = set(bm.verts) - before
        for v in new:
            v.co.z = max(v.co.z, -0.5) * (1 + rnd.uniform(-0.08, 0.08))
            v.co.x *= 1 + rnd.uniform(-0.05, 0.05)
    return prop("Hill", [("Grass", build)], tile=2.5)


def stage():
    return prop("Stage", [("Paving", lambda bm: loft(bm, [(0, 1.7, 1.7), (0.12, 1.7, 1.7), (0.16, 1.6, 1.6)], 16)),
                          ("Leaves", lambda bm: loft(bm, [(0, 1.78, 1.78), (0.05, 1.78, 1.78)], 16, cap_bottom=False))],
                tile=0.6)


for build in (tree_round, tree_pine, bush, lambda: rock("Rock_A", (1.0, 0.8, 0.7)), lambda: rock("Rock_B", (1.8, 1.4, 0.8)),
              lambda: glow_mushroom("Mushroom_Purple", "CapPurple"), lambda: glow_mushroom("Mushroom_Blue", "CapBlue"),
              lambda: glow_mushroom("Mushroom_Pink", "CapPink"), mushroom_cluster, mushroom_house, cottage, lamp_post,
              fence, bench, well, flowers, signpost, hill, stage):
    build()

with open(os.path.join(OUT_DIR, "WorldKit.manifest.json"), "w") as f:
    json.dump({"props": MANIFEST}, f, indent=1)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, "WorldKit.fbx"), use_selection=True, object_types={"MESH"},
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", path_mode="STRIP",
                         bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "WorldKit.blend"))

# ---------------------------------------------------------------- preview: all props in a row

if PREVIEW_DIR:
    x = 0.0
    for o in scene.objects:
        o.location.x = x + o.dimensions.x / 2
        x += o.dimensions.x + 0.8
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            pass
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.rotation_euler = (math.radians(55), 0, math.radians(30))
    scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.2, 0.18, 0.3, 1)
    scene.world = world
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = x * 0.52
    cam.location = (x / 2, -60, 6)
    cam.rotation_euler = (math.radians(84), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = 2400, 500
    scene.render.filepath = os.path.join(PREVIEW_DIR, "worldkit.png")
    bpy.ops.render.render(write_still=True)
print("DONE", len(MANIFEST), "props")
