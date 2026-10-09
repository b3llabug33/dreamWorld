"""
Builds "Dreamer", the dreamWorld PS1-style anime base character, from code.

Run headless (preview dir is optional):
  "C:/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --python ArtSource/build_dreamer.py -- <preview dir>

Like real PS1 characters, the body is made of separate low-poly segments, each rigidly
attached to one bone (no weight painting needed). Every customizable piece is its own mesh
on the same skeleton, named Slot_Style (Hair_Twintails, Top_Sailor, Glasses_Round, ...),
so Unity's character creator can switch them on and off.

Textures are grayscale so Unity can tint them per slot. Pieces can have extra material
slots that keep their own color (e.g. a sailor top's navy collar); Dreamer.manifest.json
tells Unity which texture goes on each submesh and whether it gets tinted.

Blender axes: Z is up, the character faces -Y, and the character's left (.L) is +X.
Outputs: Assets/Models/Dreamer/ (Dreamer.fbx, Textures/*.png, Dreamer.manifest.json),
ArtSource/Dreamer.blend, and preview PNGs.
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
MODEL_DIR = os.path.join(ROOT, "Assets", "Models", "Dreamer")
TEX_DIR = os.path.join(MODEL_DIR, "Textures")
THIRD_PARTY = os.path.join(ROOT, "ArtSource", "ThirdParty")
PREVIEW_DIR = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(ROOT, "ArtSource")
os.makedirs(TEX_DIR, exist_ok=True)
random.seed(7)  # textures with noise come out the same every build

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- skeleton

# name: (head, tail, parent). Names follow Unity Humanoid auto-mapping.
BONES = {
    "Hips":       ((0, 0, 0.80), (0, 0, 0.98), None),
    "Spine":      ((0, 0, 0.98), (0, 0, 1.15), "Hips"),
    "Chest":      ((0, 0, 1.15), (0, 0, 1.33), "Spine"),
    "Neck":       ((0, 0, 1.33), (0, 0, 1.40), "Chest"),
    "Head":       ((0, 0, 1.40), (0, 0, 1.62), "Neck"),
}
for side, sx in (("L", 1), ("R", -1)):
    BONES.update({
        f"UpperArm.{side}": ((0.12 * sx, 0, 1.28), (0.39 * sx, 0, 1.28), "Chest"),
        f"LowerArm.{side}": ((0.39 * sx, 0, 1.28), (0.61 * sx, 0, 1.28), f"UpperArm.{side}"),
        f"Hand.{side}":     ((0.61 * sx, 0, 1.28), (0.71 * sx, 0, 1.28), f"LowerArm.{side}"),
        f"UpperLeg.{side}": ((0.072 * sx, 0, 0.80), (0.065 * sx, 0, 0.45), "Hips"),
        f"LowerLeg.{side}": ((0.07 * sx, 0, 0.45), (0.07 * sx, 0, 0.08), f"UpperLeg.{side}"),
        f"Foot.{side}":     ((0.07 * sx, 0, 0.08), (0.07 * sx, -0.11, 0.02), f"LowerLeg.{side}"),
    })

arm_data = bpy.data.armatures.new("DreamerRig")
rig = bpy.data.objects.new("Dreamer", arm_data)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
for name, (head, tail, parent) in BONES.items():
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    if parent:
        b.parent = arm_data.edit_bones[parent]
        b.use_connect = (Vector(head) - arm_data.edit_bones[parent].tail).length < 1e-4
bpy.ops.object.mode_set(mode="OBJECT")


def sides():
    return (("L", 1), ("R", -1))

# ---------------------------------------------------------------- textures

def save_png(name, rgba):
    """rgba: float array (h, w, 4), row 0 = bottom. Saves to Textures/ and returns the image."""
    h, w, _ = rgba.shape
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels = np.clip(rgba, 0, 1).astype(np.float32).ravel()
    img.filepath_raw = os.path.join(TEX_DIR, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def gray_fabric(name, source, mean=0.82, contrast=1.3, folder="SBS_Textures"):
    """Load a CC0 fabric texture, drop its color and re-center its brightness so a Unity
    tint shows the chosen color with the fabric's weave/pattern on top."""
    src = bpy.data.images.load(os.path.join(THIRD_PARTY, folder, source))
    w, h = src.size
    px = np.array(src.pixels[:], dtype=np.float32).reshape(h, w, 4)
    lum = px[..., 0] * 0.299 + px[..., 1] * 0.587 + px[..., 2] * 0.114
    lum = (lum - lum.mean()) * contrast + mean
    out = np.dstack([lum, lum, lum, np.ones_like(lum)])
    return save_png(name, out)


def solid(name, color, size=4):
    return save_png(name, np.tile(np.array([*color, 1.0], dtype=np.float32), (size, size, 1)))


def hair_texture():
    """64x64 grayscale strands with a bright anime 'angel ring' band."""
    w = h = 64
    lum = np.full((h, w), 0.80, dtype=np.float32)
    for x in range(w):  # strand lines
        if random.random() < 0.35:
            lum[:, x] -= random.uniform(0.08, 0.18)
    for y in range(h):  # darker toward the roots (top)
        lum[y, :] -= max(0.0, (y - 44) / 20) * 0.15
    for x in range(w):  # jagged highlight band around v = 0.6
        top = 40 + random.randint(-1, 2)
        bottom = 36 + random.randint(-2, 1)
        lum[bottom:top, x] = 1.0
    out = np.dstack([lum, lum, lum, np.ones_like(lum)])
    return save_png("Hair", out)


def fishnet_texture():
    """32x32 diamond net with transparent holes (Unity renders it with alpha cutout)."""
    n = 32
    out = np.zeros((n, n, 4), np.float32)
    for y in range(n):
        for x in range(n):
            if (x + y) % 8 == 0 or (x - y) % 8 == 0:
                out[y, x] = (0.85, 0.85, 0.85, 1)
    return save_png("Fishnet", out)


def print_texture():
    """16x16 pixel skull print for the oversized band tee (fixed colors, cream on black)."""
    art = [
        "................",
        "................",
        ".....######.....",
        "....########....",
        "...##########...",
        "...##########...",
        "...#..####..#...",
        "...#..####..#...",
        "...####..####...",
        "....###..###....",
        ".....######.....",
        ".....#.##.#.....",
        "......####......",
        "................",
        "................",
        "................",
    ]
    out = np.zeros((16, 16, 4), np.float32)
    for row, line in enumerate(art):
        for x, ch in enumerate(line):
            out[15 - row, x] = (0.95, 0.92, 0.85, 1) if ch == "#" else (0.09, 0.08, 0.11, 1)
    return save_png("PrintSkull", out)


def leather_texture():
    w = h = 32
    lum = 0.78 + np.random.default_rng(3).normal(0, 0.035, (h, w)).astype(np.float32)
    lum[0:2, :] = 0.55   # darker seam at the sole line
    return save_png("Leather", np.dstack([lum, lum, lum, np.ones_like(lum)]))


def pattern_texture(name, fn, size=64):
    """Grayscale repeating pattern (one 0.2 m tile) with a little fabric grain; Unity tints it."""
    grain = np.random.default_rng(len(name)).normal(0, 0.025, (size, size))
    lum = np.zeros((size, size), np.float32)
    for y in range(size):
        for x in range(size):
            lum[y, x] = fn(x, y) + grain[y, x]
    return save_png(name, np.dstack([lum, lum, lum, np.ones_like(lum)]))


def _heart(x, y, cx, cy, r):
    X, Y = (x - cx) / r, (y - cy) / r - 0.2
    return (X * X + Y * Y - 1) ** 3 - X * X * Y ** 3 <= 0


def _star(x, y, cx, cy, r):
    a = math.atan2(y - cy, x - cx)
    return math.hypot(x - cx, y - cy) <= r * (0.55 + 0.45 * abs(math.cos(2.5 * a + math.pi / 2)))


SPOTS = ((10, 12), (40, 8), (26, 34), (56, 40), (8, 52), (38, 58), (20, 4), (52, 22))


def _leopard(x, y):
    """Rosettes: a dark broken ring around a slightly darker middle (wraps across the 64px tile)."""
    for cx, cy in SPOTS:
        d = math.hypot(((x - cx + 32) % 64) - 32, ((y - cy + 32) % 64) - 32)
        if 3.5 <= d <= 5.5 and (x + y) % 5:
            return DARK
        if d < 3.5:
            return 0.80
    return LIGHT


LIGHT, DARK = 0.97, 0.52
PATTERNS = {   # menu order; "Plain" (the piece's own fabric) comes first in the creator
    "Stripes": lambda x, y: LIGHT if (y // 8) % 2 else DARK,
    "Pinstripe": lambda x, y: DARK if x % 8 == 0 else LIGHT,
    "Dots": lambda x, y: DARK if ((x % 16 - 8) ** 2 + (y % 16 - 8) ** 2 <= 9 or
                                  ((x + 8) % 16 - 8) ** 2 + ((y + 8) % 16 - 8) ** 2 <= 9) else LIGHT,
    "Checker": lambda x, y: LIGHT if ((x // 8) + (y // 8)) % 2 else DARK,
    "Hearts": lambda x, y: DARK if (_heart(x % 32, y % 32, 16, 16, 7) or _heart((x + 16) % 32, (y + 16) % 32, 16, 16, 7)) else LIGHT,
    "Stars": lambda x, y: DARK if (_star(x % 32, y % 32, 16, 16, 7) or _star((x + 16) % 32, (y + 16) % 32, 16, 16, 5)) else LIGHT,
    "Leopard": lambda x, y: _leopard(x, y),
}

TEXTURES = {
    "Cotton": gray_fabric("Cotton", "Mat_Cloth_Canvas_04-128x128.png", contrast=1.6),
    "Knit": gray_fabric("Knit", "Synth-Fabric_15-128x128.png", contrast=1.2),
    "Plaid": gray_fabric("Plaid", "Mat_Cloth_Plaid_04-128x128.png", mean=0.75, contrast=1.1),
    "Tartan": gray_fabric("Tartan", "Mat_Cloth_Plaid_03-128x128.png", mean=0.75, contrast=1.1),
    "Tweed": gray_fabric("Tweed", "Synth-Fabric_02-128x128.png", contrast=1.0),
    "Mesh": gray_fabric("Mesh", "Synth-Fabric_14-128x128.png", contrast=1.2),
    "Jeans": gray_fabric("Jeans", "Jeans.png", contrast=1.5, folder="CC0_Fabrics"),
    "JeansDark": gray_fabric("JeansDark", "JeansDark.png", contrast=1.4, folder="CC0_Fabrics"),
    "Flannel": gray_fabric("Flannel", "Flannel.png", mean=0.72, contrast=1.0, folder="CC0_Fabrics"),
    "Satin": gray_fabric("Satin", "Satin.png", contrast=3.0, folder="CC0_Fabrics"),
    "Gingham": gray_fabric("Gingham", "Gingham.png", mean=0.78, contrast=0.9, folder="CC0_Fabrics"),
    "Floral": gray_fabric("Floral", "Floral.png", contrast=1.4, folder="CC0_Fabrics"),
    "LeatherHide": gray_fabric("LeatherHide", "LeatherHide.png", contrast=1.6, folder="CC0_Fabrics"),
    "Fleece": gray_fabric("Fleece", "Fleece.png", contrast=1.6, folder="CC0_Fabrics"),
    "Jersey": gray_fabric("Jersey", "Jersey.png", contrast=1.1, folder="CC0_Fabrics"),
    "Velvet": gray_fabric("Velvet", "Velvet.png", contrast=2.0, folder="CC0_Fabrics"),
    "Corduroy": gray_fabric("Corduroy", "Corduroy.png", contrast=1.6, folder="CC0_Fabrics"),
    "Houndstooth": gray_fabric("Houndstooth", "Houndstooth.png", mean=0.7, contrast=1.0, folder="CC0_Fabrics"),
    "Fishnet": fishnet_texture(),
    "PrintSkull": print_texture(),
    "Hair": hair_texture(),
    "Leather": leather_texture(),
    **{f"Pattern{k}": pattern_texture(f"Pattern{k}", fn) for k, fn in PATTERNS.items()},
    "Skin": solid("Skin", (1, 1, 1)),
    "White": solid("White", (1, 1, 1)),
    "SoleWhite": solid("SoleWhite", (0.95, 0.93, 0.90)),
    "Navy": solid("Navy", (0.12, 0.14, 0.32)),
    "Red": solid("Red", (0.80, 0.16, 0.22)),
    "Pink": solid("Pink", (0.98, 0.70, 0.76)),
    "Black": solid("Black", (0.08, 0.07, 0.10)),
    "Gold": solid("Gold", (1.0, 0.82, 0.35)),
}

# ---------------------------------------------------------------- materials + manifest

MANIFEST = {}          # mesh name -> [{"texture": str, "tint": bool}, ...] per submesh
_materials = {}


def material(texture, tint=True):
    """One Blender material per (texture, tint) pair. The preview shows tintable pieces in
    a neutral lilac so untinted trims are easy to tell apart in renders."""
    key = (texture, tint)
    if key in _materials:
        return _materials[key]
    mat = bpy.data.materials.new(f"{texture}{'' if tint else '_Fixed'}")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 1.0
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = TEXTURES[texture]
    tex.interpolation = "Closest"
    if tint:
        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs["B"].default_value = (0.82, 0.78, 0.95, 1)
        mat.node_tree.links.new(tex.outputs["Color"], mix.inputs["A"])
        mat.node_tree.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    else:
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mat.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    mat["texture"], mat["tint"] = texture, tint
    _materials[key] = mat
    return mat

# ---------------------------------------------------------------- mesh helpers

def ring_points(center, rx, ry, n, axis):
    """n points on an ellipse around `center`, in the plane perpendicular to `axis`."""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n + math.pi / n
        c, s = math.cos(a) * rx, math.sin(a) * ry
        if axis == "z":
            pts.append(Vector((center[0] + c, center[1] + s, center[2])))
        else:  # along x (arms): rx is vertical, ry is depth
            pts.append(Vector((center[0], center[1] + s, center[2] + c)))
    return pts


def loft(bm, sections, n=8, axis="z", cap_start=True, cap_end=True, ripple=None, skip=()):
    """Connect a list of (center, rx, ry) cross-sections into a tube.
    ripple(i, ring_index) can return a radius multiplier per vertex (used for pleats);
    skip lists face columns to leave open (column i joins vertex i and i+1)."""
    rings = []
    for k, (c, rx, ry) in enumerate(sections):
        pts = ring_points(c, rx, ry, n, axis)
        if ripple:
            cv = Vector(c)
            pts = [cv + (p - cv) * ripple(i, k) for i, p in enumerate(pts)]
        rings.append([bm.verts.new(p) for p in pts])
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            if i in skip:
                continue
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if cap_start:
        bm.faces.new(list(reversed(rings[0])))
    if cap_end:
        bm.faces.new(rings[-1])
    return rings


def box(bm, lo, hi, taper_front=0.0):
    """Axis-aligned box; taper_front pinches the -Y face (toes) inward on x and z."""
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    def front(x, z):
        return (cx + (x - cx) * (1 - taper_front), y0, z0 + (z - z0) * (1 - taper_front))
    v = [bm.verts.new(p) for p in (
        front(x0, z0), front(x1, z0), (x1, y1, z0), (x0, y1, z0),
        front(x0, z1), front(x1, z1), (x1, y1, z1), (x0, y1, z1))]
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([v[i] for i in f])


def torus(bm, center, major, minor, axis="z", seg=12, tube=4, arc=2 * math.pi, start=0.0):
    """Low-poly ring. axis = the ring's normal ('z' flat halo, 'y' hoop facing forward)."""
    c = Vector(center)
    full = abs(arc - 2 * math.pi) < 1e-6
    count = seg if full else seg + 1
    rings = []
    for i in range(count):
        a = start + arc * i / seg
        if axis == "z":
            radial, up = Vector((math.cos(a), math.sin(a), 0)), Vector((0, 0, 1))
        else:  # hoop in the x-z plane
            radial, up = Vector((math.cos(a), 0, math.sin(a))), Vector((0, 1, 0))
        ring = []
        for j in range(tube):
            b = 2 * math.pi * j / tube
            ring.append(bm.verts.new(c + radial * (major + minor * math.cos(b)) + up * minor * math.sin(b)))
        rings.append(ring)
    pairs = list(zip(rings, rings[1:] + ([rings[0]] if full else [])))
    for r0, r1 in pairs:
        for j in range(tube):
            k = (j + 1) % tube
            bm.faces.new((r0[j], r0[k], r1[k], r1[j]))


def strand(bm, root, tip, width, depth=0.025, flat_tip=0.0):
    """Chunky front-facing hair lock: 4-sided spike from a wide root to a point (or blunt)."""
    strand_v(bm, root, tip, Vector((width, 0, 0)), Vector((0, -depth, 0)), flat_tip)


def strand_v(bm, root, tip, side, front, flat_tip=0.0):
    """Lock with arbitrary orientation: `side` spans its width, `front` its thickness."""
    r, t = Vector(root), Vector(tip)
    side, front = Vector(side), Vector(front)
    width = side.length
    a, b = bm.verts.new(r - side), bm.verts.new(r + side)
    c, d = bm.verts.new(r + front), bm.verts.new(r - front)
    if flat_tip > 0:
        tip_side = side * flat_tip
        p, q = bm.verts.new(t - tip_side), bm.verts.new(t + tip_side)
        for f in ((a, c, p), (c, b, q, p), (b, d, q), (d, a, p, q), (a, d, b, c)):
            bm.faces.new(f)
    else:
        p = bm.verts.new(t)
        for f in ((a, c, p), (c, b, p), (b, d, p), (d, a, p), (a, d, b, c)):
            bm.faces.new(f)


def box_uv(obj, tile, origin=(0, 0, 0)):
    """Project UVs per face from its dominant axis (PS1-style box mapping)."""
    me = obj.data
    uv = me.uv_layers.new(name="UVMap") if not me.uv_layers else me.uv_layers[0]
    o = Vector(origin)
    for poly in me.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co - o
            if ax == 0:
                u, v = co.y, co.z
            elif ax == 1:
                u, v = co.x, co.z
            else:
                u, v = co.x, co.y
            uv.data[li].uv = (u / tile, v / tile)


def part(name, bone, build, mat):
    """Make one rigid mesh segment bound 100% to `bone`."""
    bm = bmesh.new()
    build(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    group = obj.vertex_groups.new(name=bone)
    group.add(range(len(me.vertices)), 1.0, "REPLACE")
    return obj


def join(name, parts, tile=0.2, uv_origin=(0, 0, 0), keep_uv=False):
    """Merge segments into one skinned mesh, give it box UVs and record its submeshes."""
    if not keep_uv:
        for p in parts:
            if not p.data.uv_layers:
                box_uv(p, tile, uv_origin)
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = obj.data.name = name
    obj.parent = rig
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    for poly in obj.data.polygons:
        poly.use_smooth = False  # flat shading reads as PS1
    MANIFEST[name] = [{"texture": s.material["texture"], "tint": bool(s.material["tint"])}
                      for s in obj.material_slots]
    return obj

# ---------------------------------------------------------------- head shape

HEAD_C = Vector((0, 0, 1.485))
HEAD_R = Vector((0.106, 0.114, 0.135))   # widest half-sizes (used by hats)

# Sculpted head, as horizontal rings from the crown down to the chin:
# (height above HEAD_C, half-width, front depth, back depth). A natural, slightly
# stylized human head: full cheeks, a soft rounded jaw and chin.
HEAD_PROFILE = [
    (0.125, 0.050, 0.055, 0.060),
    (0.100, 0.083, 0.088, 0.098),
    (0.065, 0.100, 0.105, 0.112),
    (0.025, 0.105, 0.110, 0.115),
    (-0.015, 0.104, 0.110, 0.112),
    (-0.045, 0.101, 0.107, 0.104),
    (-0.070, 0.094, 0.102, 0.090),   # full cheeks
    (-0.090, 0.082, 0.096, 0.072),   # soft, rounded jaw
    (-0.106, 0.064, 0.090, 0.050),
    (-0.118, 0.042, 0.083, 0.030),
]
HEAD_TOP = 0.135
CHIN = Vector((0, -0.066, -0.127))      # rounded chin, relative to HEAD_C


def head_ring(dz):
    """Interpolated (half-width, front depth, back depth) of the head at height dz."""
    p = HEAD_PROFILE
    if dz >= p[0][0]:
        t = min(1.0, (dz - p[0][0]) / (HEAD_TOP - p[0][0]))
        return tuple(v * (1 - t) for v in p[0][1:])
    for (z0, *a), (z1, *b) in zip(p, p[1:]):
        if z1 <= dz <= z0:
            t = (z0 - dz) / (z0 - z1)
            return tuple(x + (y - x) * t for x, y in zip(a, b))
    return p[-1][1:]


def head_point(theta, dz, scale=1.0):
    """Surface point at angle theta (-90deg = front, +90deg = back) and height dz,
    for a head scaled `scale` around its center."""
    rx, yf, yb = head_ring(dz / scale)
    depth = yf if math.sin(theta) < 0 else yb
    return HEAD_C + Vector((math.cos(theta) * rx * scale, math.sin(theta) * depth * scale, dz))


def build_head(bm, scale=1.0, cut=None, seg=16, rings=None):
    top = bm.verts.new(HEAD_C + Vector((0, 0, HEAD_TOP * scale)))
    chin = bm.verts.new(HEAD_C + CHIN * scale)
    verts = []
    for dz, *_ in HEAD_PROFILE:
        verts.append([bm.verts.new(head_point(2 * math.pi * s / seg + math.pi / seg, dz * scale, scale))
                      for s in range(seg)])
    for s in range(seg):
        j = (s + 1) % seg
        bm.faces.new((top, verts[0][s], verts[0][j]))
        bm.faces.new((chin, verts[-1][j], verts[-1][s]))
        for r in range(len(verts) - 1):
            bm.faces.new((verts[r][s], verts[r + 1][s], verts[r + 1][j], verts[r][j]))
    if cut:
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if cut(v.co)], context="VERTS")


def head_front_y(x, z, offset):
    """Y of the head's front surface at (x, z), pushed out by `offset` (for face decals)."""
    rx, yf, _ = head_ring(z - HEAD_C.z)
    if rx <= 1e-4:
        return -offset
    u = min(1.0, abs(x) / rx)
    return -(yf * math.sqrt(1 - u * u)) - offset

# ---------------------------------------------------------------- eyes (face decals)

FACE_W, FACE_H = 0.16, 0.13          # decal size in meters
FACE_Z0 = 1.395                      # decal bottom edge
FACE_PX = 128                        # texels per layer (1.25 mm each)
EYE_Y = 66                           # eye center row (z = 1.462)
EYE_X = 30                           # eye center offset from the middle column
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eye_designs  # noqa: E402  (the face designs live in their own module, shared with face_drafts.py)

EYE_STYLES = tuple(eye_designs.FACES)   # first one is the default face
EYE_LAYERS = 5                          # face, eye base, iris (tinted), pupil+shine, closed (blink)


def draw_eyes(style):
    # nose, mouth, brows and blush are separate pieces now (movable, mix and match)
    design = dict(eye_designs.FACES[style], nose="none", mouth="none", brow="none", blush="none")
    TEXTURES[f"Eyes{style}"] = save_png(f"Eyes{style}", eye_designs.draw(design))


def face_decal(name, tex, mat, offset):
    """A curved grid over the face area (same size as the eye decal) using one 128px layer."""
    def build(bm):
        uv_layer = bm.loops.layers.uv.new("UVMap")
        nx, nz = 12, 10
        grid = []
        for j in range(nz + 1):
            row = []
            for i in range(nx + 1):
                u, v = i / nx, j / nz
                x = (u - 0.5) * FACE_W
                z = FACE_Z0 + v * FACE_H
                row.append((bm.verts.new((x, head_front_y(x, z, offset), z)), (u, v)))
            grid.append(row)
        for j in range(nz):
            for i in range(nx):
                quad = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
                f = bm.faces.new([q[0] for q in quad])
                for loop, q in zip(f.loops, quad):
                    loop[uv_layer].uv = q[1]
    return join(name, [part(name.lower(), "Head", build, mat)], keep_uv=True)


def face_part(kind, style):
    """A nose or mouth: its own decal on the face (one layer), so any eyes/nose/mouth can mix."""
    tex = f"{kind}{style.capitalize()}"
    TEXTURES[tex] = save_png(tex, eye_designs.draw_part(**{kind.lower(): style}))
    return face_decal(f"{kind}_{style.capitalize()}", tex, material(tex, tint=False), 0.0045)


def brows(style):
    """Eyebrows: tinted (own color), just above the eye layers and under the bangs."""
    tex = f"Brows{style}"
    TEXTURES[tex] = save_png(tex, eye_designs.draw_brows(style))
    return face_decal(f"Brows_{style}", tex, material(tex), 0.0062)


def extras():
    """Blush, freckles, piercings...: three extra slots, each can show any of them (tinted).
    Each slot sits at its own depth so two extras never flicker against each other."""
    out = []
    for style in eye_designs.EXTRAS:
        tex = f"Extra{style}"
        TEXTURES[tex] = save_png(tex, eye_designs.draw_extra(style))
    for n in (1, 2, 3):
        for style in eye_designs.EXTRAS:
            out.append(face_decal(f"Extra{n}_{style}", f"Extra{style}", material(f"Extra{style}"), 0.0047 + 0.0002 * n))
    return out


def build_eye_layers(bm):
    """Stacked curved grids on the face; each uses one fifth of the atlas and its own
    material slot, so Unity can tint just the iris and hide/show layers to blink."""
    uv_layer = bm.loops.layers.uv.new("UVMap")
    nx, nz = 12, 10
    for layer in range(EYE_LAYERS):
        offset = 0.004 + 0.0007 * min(layer, 3)       # closed lids sit on the eye-base level
        grid = []
        for j in range(nz + 1):
            row = []
            for i in range(nx + 1):
                u, v = i / nx, j / nz
                x = (u - 0.5) * FACE_W
                z = FACE_Z0 + v * FACE_H
                row.append((bm.verts.new((x, head_front_y(x, z, offset), z)), ((u + layer) / EYE_LAYERS, v)))
            grid.append(row)
        for j in range(nz):
            for i in range(nx):
                quad = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
                f = bm.faces.new([q[0] for q in quad])
                for loop, q in zip(f.loops, quad):
                    loop[uv_layer].uv = q[1]


def eyes(style):
    draw_eyes(style)
    tex = f"Eyes{style}"
    obj = part(f"eyes{style}", "Head", build_eye_layers, material(tex, tint=False))
    obj.data.materials.clear()
    for layer in range(EYE_LAYERS):
        m = bpy.data.materials.new(f"{tex}_L{layer}")
        m.use_nodes = True
        node = m.node_tree.nodes.new("ShaderNodeTexImage")
        node.image, node.interpolation = TEXTURES[tex], "Closest"
        bsdf = m.node_tree.nodes["Principled BSDF"]
        m.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        m.node_tree.links.new(node.outputs["Alpha"], bsdf.inputs["Alpha"])
        m["texture"], m["tint"] = tex, layer == 2
        obj.data.materials.append(m)
    # each layer uses its own fifth of the atlas: recover the layer from the UVs
    uv = obj.data.uv_layers[0].data
    for poly in obj.data.polygons:
        u = sum(uv[li].uv[0] for li in poly.loop_indices) / len(poly.loop_indices)
        poly.material_index = min(EYE_LAYERS - 1, int(u * EYE_LAYERS))
    return join(f"Eyes_{style}", [obj], keep_uv=True)

# ---------------------------------------------------------------- body

skin = material("Skin")

TORSO = [  # (z, half-width, half-depth): slim hourglass, small waist at 0.99
    (0.76, 0.100, 0.074), (0.84, 0.118, 0.082), (0.92, 0.102, 0.070),
    (0.99, 0.075, 0.055), (1.07, 0.092, 0.066), (1.15, 0.108, 0.082),
    (1.23, 0.108, 0.072), (1.29, 0.104, 0.060), (1.32, 0.068, 0.048),
]


def torso_sections(z0, z1, grow=0.0):
    return [((0, 0, z), w + grow, d + grow) for z, w, d in TORSO if z0 <= z <= z1]


def hand(bm, sx):
    """Mitten hand with a thumb, PS1 style."""
    x0, x1 = sorted((0.60 * sx, 0.69 * sx))
    box(bm, (x0, -0.013, 1.25), (x1, 0.013, 1.30))
    tx0, tx1 = sorted((0.615 * sx, 0.65 * sx))
    box(bm, (tx0, -0.03, 1.255), (tx1, -0.012, 1.275))


def body():
    parts = [
        part("hips", "Hips", lambda bm: loft(bm, torso_sections(0.76, 0.99), 12), skin),
        part("chest", "Chest", lambda bm: loft(bm, torso_sections(0.99, 1.32), 12), skin),
        part("neck", "Neck", lambda bm: loft(bm, [((0, 0.005, 1.30), 0.029, 0.029), ((0, 0.01, 1.42), 0.026, 0.026)], 6), skin),
        part("head", "Head", lambda bm: build_head(bm), skin),
    ]
    for side, sx in sides():
        parts += [
            part(f"upperarm{side}", f"UpperArm.{side}", lambda bm, sx=sx: loft(
                bm, [((0.11 * sx, 0, 1.28), 0.037, 0.037), ((0.39 * sx, 0, 1.28), 0.029, 0.029)], 8, "x"), skin),
            part(f"lowerarm{side}", f"LowerArm.{side}", lambda bm, sx=sx: loft(
                bm, [((0.39 * sx, 0, 1.28), 0.029, 0.029), ((0.61 * sx, 0, 1.28), 0.022, 0.022)], 8, "x"), skin),
            part(f"hand{side}", f"Hand.{side}", lambda bm, sx=sx: hand(bm, sx), skin),
            part(f"upperleg{side}", f"UpperLeg.{side}", lambda bm, sx=sx: loft(
                bm, [((0.064 * sx, 0, 0.80), 0.054, 0.054), ((0.066 * sx, 0, 0.62), 0.048, 0.048),
                     ((0.065 * sx, 0, 0.45), 0.037, 0.037)], 8), skin),
            part(f"lowerleg{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
                bm, [((0.07 * sx, 0, 0.45), 0.040, 0.040), ((0.07 * sx, 0.008, 0.31), 0.044, 0.044),
                     ((0.07 * sx, 0, 0.08), 0.025, 0.025)], 8), skin),
            part(f"foot{side}", f"Foot.{side}", lambda bm, sx=sx: box(
                bm, (0.07 * sx - 0.03, -0.12, 0.0), (0.07 * sx + 0.03, 0.025, 0.07), taper_front=0.3), skin),
        ]
    return join("Skin_Body", parts)

# ---------------------------------------------------------------- hair

hair_mat = material("Hair")
HAIR_UV = dict(tile=0.6, uv_origin=(0, 0, 1.20))  # one angel-ring band around z = 1.56
CAP = 1.10   # hair shell size relative to the head
FRONT = -math.pi / 2


def hair_cap(bm, hairline=0.018, nape=-0.11, side_cut=None, scale=CAP):
    """Hair shell over the skull. Open only at the face: below the hairline at the front,
    and at the cheeks/jaw, so nothing bald shows from the side, back or top.
    hairline can be a function of x (meters across the forehead) for parted bangs."""
    def cut(co):
        r = co - HEAD_C
        front = r.y < -0.02
        hl = hairline(r.x) if callable(hairline) else hairline   # parted bangs: a shaped hairline
        face = front and r.z < hl and abs(r.x) < 0.085
        jaw = r.y < 0.01 and r.z < -0.035
        bottom = r.z < nape
        # cropped sides (pixie): a diagonal line behind the ear, lower toward the back
        sides = side_cut is not None and r.y < 0.05 and r.z < side_cut - 0.25 * (r.y + 0.05)
        return face or jaw or bottom or sides
    build_head(bm, scale=scale, cut=cut)


def surf(theta, z, scale, offset):
    """Point on the (scaled) head surface pushed out by `offset`. Below the cheekbones the
    surface stops shrinking, so hair hangs straight down instead of hugging the jaw."""
    dz = max(z - HEAD_C.z, -0.03 * scale)
    p = head_point(theta, dz, scale)
    radial = Vector((p.x - HEAD_C.x, p.y - HEAD_C.y, 0))
    if radial.length > 1e-6:
        p += radial.normalized() * offset
    return Vector((p.x, p.y, z))


def lock(bm, th0, z0, th1, z1, w0, tip=0.25, thick=0.014, s0=CAP, s1=CAP, off0=0.004, off1=0.004,
         flare=0.0, steps=5, curl=0.0):
    """A flat hair lock laid on the head: starts at (th0, z0) on a shell of scale s0 and
    runs to (th1, z1), narrowing from width w0 to w0*tip. `flare` pushes the end away from
    the head (volume), `curl` tucks the very tip back in (bob ends)."""
    rings = []
    for k in range(steps + 1):
        t = k / steps
        th = th0 + (th1 - th0) * t
        z = z0 + (z1 - z0) * t
        off = off0 + (off1 - off0) * t + flare * t * t - curl * max(0, t - 0.75) * 4
        c = surf(th, z, s0 + (s1 - s0) * t, off)
        side = (surf(th + 0.01, z, s0 + (s1 - s0) * t, off) - c)
        side = side.normalized() if side.length > 1e-6 else Vector((1, 0, 0))
        normal = Vector((c.x - HEAD_C.x, c.y - HEAD_C.y, 0))
        normal = normal.normalized() if normal.length > 1e-6 else Vector((0, -1, 0))
        w = w0 * (1 - (1 - tip) * t ** 1.3) / 2
        rings.append([bm.verts.new(c - side * w), bm.verts.new(c + side * w),
                      bm.verts.new(c + side * w + normal * thick), bm.verts.new(c - side * w + normal * thick)])
    for a, b in zip(rings, rings[1:]):
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])


def theta_at(x):
    """Head angle for a point `x` meters across the forehead."""
    return FRONT + math.asin(max(-0.95, min(0.95, x / 0.104)))


def fringe_lock(bm, x_root, x_tip, tip_z, width, tip=0.6):
    """One bang lock from the crown down onto the forehead."""
    lock(bm, theta_at(x_root), 1.615, theta_at(x_tip), tip_z, width, tip=tip, s0=CAP, s1=1.0,
         off0=-0.008, off1=0.0075, steps=6)


def fringe_base(bm, edge, z1=1.568):
    """Hair-colored layer hugging the forehead under the bangs, up past the hair cap's face
    opening, so no skin shows between swept or parted locks. Its bottom edge follows edge(x)
    (just above the bang tips) so it never peeks out below the locks as a flat band."""
    nx, nz = 12, 4
    xs = np.linspace(-0.092, 0.092, nx + 1)
    rows = []
    for j in range(nz + 1):
        rows.append([bm.verts.new((x, head_front_y(x, z, 0.0068), z))
                     for x in xs for z in [min(edge(x), z1 - 0.004) + (z1 - min(edge(x), z1 - 0.004)) * j / nz]])
    for a, b in zip(rows, rows[1:]):
        for i in range(nx):
            bm.faces.new((a[i], a[i + 1], b[i + 1], b[i]))


# bang styles (the "bangs" option in the creator), used as mesh name suffixes
BANGS = ("Straight", "Choppy", "Middle", "Left", "Right", "Micro")


# Choppy: alternating longer/shorter points, visibly textured but still an even overall line
CHOPPY = ((-0.082, 1.489), (-0.059, 1.503), (-0.036, 1.486), (-0.012, 1.499),
          (0.012, 1.484), (0.036, 1.501), (0.059, 1.487), (0.082, 1.498))
SIDE_TIP = (1.535, 1.465)    # side part: lock length at the part side -> far side


def bangs(bm, kind):
    if kind == "Straight":    # full blunt fringe, one level line just above the brows
        for x in np.linspace(-0.075, 0.075, 6):
            fringe_lock(bm, x * 0.6, x * 1.04, 1.506, 0.036, tip=1.0)
    elif kind == "Micro":     # very short blunt fringe high on the forehead
        for x in np.linspace(-0.075, 0.075, 6):
            fringe_lock(bm, x * 0.6, x * 1.04, 1.535, 0.036, tip=1.0)
    elif kind == "Choppy":
        for x, z in CHOPPY:
            fringe_lock(bm, x * 0.6, x * 1.06, z, 0.029, tip=0.3)
    elif kind == "Middle":    # middle part: curtains sweeping out to the sides, longer at the edges
        for x, z in ((-0.085, 1.44), (-0.06, 1.47), (-0.035, 1.50), (0.035, 1.50), (0.06, 1.47), (0.085, 1.44)):
            fringe_lock(bm, x * 0.25, x, z, 0.036, tip=0.45)
    else:                     # side part: sweeps from a part on one side across the forehead
        s = 1 if kind == "Left" else -1      # character's left is +X
        for k, x in enumerate(np.linspace(0.075, -0.085, 7)):
            t = k / 6
            fringe_lock(bm, s * 0.045, s * x, SIDE_TIP[0] + (SIDE_TIP[1] - SIDE_TIP[0]) * t, 0.034 + 0.006 * t, tip=0.5)


def bang_edge(kind):
    """Bottom edge of the forehead layer for each bang style: tucked just above the lock tips."""
    if kind == "Micro":
        return lambda x: 1.545
    if kind == "Straight":
        return lambda x: 1.516
    if kind == "Choppy":
        return lambda x: max(z for _, z in CHOPPY) + 0.010
    if kind == "Middle":      # a soft inverted V: the part shows a little forehead, like a real part
        return lambda x: 1.532 - 0.45 * abs(x)
    if kind in ("Left", "Right"):
        s = 1 if kind == "Left" else -1
        def side(x):
            t = min(1, max(0, (0.075 - s * x) / 0.16))   # 0 at the part side, 1 across the forehead
            return SIDE_TIP[0] + (SIDE_TIP[1] - SIDE_TIP[0]) * t + 0.016
        return side
    return lambda x: 1.497    # styles that draw their own fringe


def side_locks(bm, tip_z, width=0.05, tip=0.7, angle=58):
    """Face-framing pieces in front of the ears."""
    for sx in (1, -1):
        th = FRONT + sx * math.radians(angle)
        lock(bm, th, 1.58, th + sx * math.radians(4), tip_z, width, tip=tip, off0=-0.006, off1=0.012)


def hang(bm, bottom_z, count=11, width=0.075, tip=0.3, start=-35, end=215, root_z=1.615, rnd=None,
         jitter=0.0, flare=0.015, curl=0.0, thick=0.018, scale=CAP):
    """A curtain of locks around the sides and back, hanging to bottom_z."""
    for k in range(count):
        th = math.radians(start + (end - start) * k / (count - 1))
        z1 = bottom_z + (rnd.uniform(-jitter, jitter) if rnd else 0)
        lock(bm, th, root_z, th, z1, width, tip=tip, flare=flare, curl=curl, steps=6, off0=-0.008, thick=thick,
             s0=scale, s1=scale)


def tail(bm, root, tip_drop, out, thickness=0.05, back=0.0, n=8):
    """A hanging tail (twintail/ponytail): bulges below the tie, tapers to a point."""
    x0, y0, z0 = root
    sections = []
    for k, t in enumerate((0.0, 0.08, 0.25, 0.5, 0.75, 0.92, 1.0)):
        r = thickness * (0.55, 1.0, 1.08, 0.95, 0.7, 0.35, 0.08)[k]
        sections.append(((x0 + out * math.sin(t * math.pi / 2), y0 + back * t, z0 - tip_drop * t), r, r * 0.8))
    loft(bm, sections, n)
    torus(bm, (x0, y0, z0 - 0.005), thickness * 0.55, 0.008, seg=8, tube=3)  # hair tie


def under_layer(bm, bottom_z):
    """Hair-colored inner curtain behind the locks (back half only), so gaps never show the neck."""
    loft(bm, [((0, 0.045, 1.52), 0.105, 0.09), ((0, 0.06, 1.40), 0.11, 0.07),
              ((0, 0.07, bottom_z), 0.10, 0.05)], 12, cap_start=False, cap_end=False, skip={5, 6, 7, 8, 9, 10, 11})


LENGTHS = ("Short", "Medium", "Long")


def hair_set(base, extra, bang_kinds=BANGS, hairline=0.018, nape=-0.11, side_cut=None, cap_scale=CAP, suffix=""):
    """Every bangs x length combination of one hairstyle, named Hair_<Base>[_<Bangs>]_<Length>.
    extra(bm, length_index) builds the rest of the hair; bang_kinds=None means no bangs option
    (the style draws its own fringe in `extra`)."""
    out = []
    for li, length in enumerate(LENGTHS):
        for kind in (bang_kinds or (None,)):
            name = (f"Hair_{base}_{kind}_{length}" if kind else f"Hair_{base}_{length}") + (f"_{suffix}" if suffix else "")

            def build(bm, kind=kind, li=li):
                if kind == "Micro":
                    line = 0.035
                elif kind in ("Middle", "Left", "Right"):   # open the cap up along the part; the layer below fills its steps
                    line = lambda x, edge=bang_edge(kind): min(edge(x) + 0.016, 1.556) - HEAD_C.z
                else:
                    line = hairline
                hair_cap(bm, hairline=line, nape=nape, side_cut=side_cut, scale=cap_scale)
                # forehead under the bangs stays hair-colored, shaped to each bang style
                fringe_base(bm, bang_edge(kind))
                if kind:
                    bangs(bm, kind)
                extra(bm, li)
            out.append(join(name, [part(name.lower(), "Head", build, hair_mat)], **HAIR_UV))
    return out


def long_extra(bm, li):
    """Long straight hair: even, full ends (no stringy taper), face-framing layers."""
    bottom = (1.16, 1.02, 0.86)[li]
    under_layer(bm, bottom + 0.02)
    side_locks(bm, 1.33, width=0.05, tip=0.75)
    side_locks(bm, max(bottom + 0.1, 1.2), width=0.06, tip=0.85, angle=74)
    hang(bm, bottom, count=16, width=0.085, tip=0.85, start=-12, end=192, flare=0.012)


def bob_extra(bm, li):
    bottom = (1.40, 1.34, 1.27)[li]
    under_layer(bm, bottom + 0.02)
    side_locks(bm, bottom + 0.02, width=0.05, tip=0.9)
    hang(bm, bottom, count=14, width=0.08, tip=0.95, start=-20, end=200, curl=0.012)


def wolf_extra(bm, li):
    """Shaggy wolf cut: choppy layers flicking out at the crown, a fluffy mid layer, long ends."""
    rnd = random.Random(11)
    top, mid, end = ((1.44, 1.34, 1.22), (1.40, 1.26, 1.10), (1.36, 1.18, 0.96))[li]
    under_layer(bm, end + 0.06)
    side_locks(bm, mid + 0.08, width=0.05, tip=0.45)
    hang(bm, top, count=13, width=0.08, tip=0.5, start=-40, end=220, root_z=1.62, rnd=rnd, jitter=0.03, flare=0.035)
    hang(bm, mid, count=10, width=0.085, tip=0.45, start=-12, end=192, root_z=1.56, rnd=rnd, jitter=0.05, flare=0.045)
    hang(bm, end, count=9, width=0.08, tip=0.4, start=5, end=175, root_z=1.50, rnd=rnd, jitter=0.06, flare=0.035)


def twintails_extra(bm, li):
    side_locks(bm, 1.42, width=0.045, tip=0.6)
    for sx in (1, -1):
        tail(bm, (0.115 * sx, 0.03, 1.60), (0.42, 0.62, 0.85)[li], 0.07 * sx, 0.055, back=0.03)


def pigtails_extra(bm, li):
    side_locks(bm, 1.42, width=0.045, tip=0.6)
    for sx in (1, -1):
        tail(bm, (0.10 * sx, 0.075, 1.425), (0.25, 0.40, 0.58)[li], 0.035 * sx, 0.048, back=0.015)


def ponytail_extra(bm, li):
    tail(bm, (0, 0.13, 1.57), (0.30, 0.45, 0.65)[li], 0.0, 0.055, back=0.04)


def buns_extra(bm, li):
    r = (0.045, 0.055, 0.068)[li]      # "length" is bun size for buns
    for sx in (1, -1):
        bmesh.ops.create_icosphere(bm, subdivisions=2, radius=r, matrix=Matrix.Translation((0.09 * sx, 0.02, 1.62 + r * 0.2)))


def pixie_extra(bm, li):
    """Pixie cut: close-cropped back and sides, a soft side-swept fringe, textured crown.
    Longer lengths get a longer fringe and a little more at the nape."""
    fringe = (1.53, 1.51, 1.49)[li]
    nape = (1.45, 1.42, 1.38)[li]
    for k, x in enumerate(np.linspace(0.07, -0.085, 6)):     # short side-swept fringe
        t = k / 5
        fringe_lock(bm, 0.04, x, fringe + 0.03 - 0.035 * t, 0.036, tip=0.5)
    for k in range(12):                                     # textured crown pieces lying flat
        th = math.radians(-30 + k * 240 / 11)
        lock(bm, th, 1.63, th + 0.08, 1.575, 0.06, tip=0.5, steps=4, off0=-0.008, off1=-0.002, s0=1.05, s1=1.05)
    # tapered nape: short pieces lying close at the back only (sides stay cropped)
    hang(bm, nape, count=9, width=0.075, tip=0.25, start=25, end=155, root_z=1.57, flare=-0.014, thick=0.012, scale=1.05)
    for sx in (1, -1):                                      # little sideburn wisps in front of the ears
        th = FRONT + sx * math.radians(66)
        lock(bm, th, 1.54, th, 1.47 - 0.01 * li, 0.028, tip=0.3, off0=-0.006, off1=0.004)


def build_hair():
    return (hair_set("Long", long_extra) +
            hair_set("Bob", bob_extra, BANGS[:5]) +
            hair_set("WolfCut", wolf_extra, BANGS[:5]) +
            hair_set("Twintails", twintails_extra, BANGS[:5], suffix="High") +     # tie: high = twintails
            hair_set("Twintails", pigtails_extra, BANGS[:5], suffix="Low") +       # tie: low = pigtails
            hair_set("Ponytail", ponytail_extra, ("Choppy",)) +
            hair_set("Buns", buns_extra, ("Choppy",)) +
            hair_set("Pixie", pixie_extra, None, hairline=0.03, nape=-0.08, side_cut=-0.005, cap_scale=1.05))


# ---------------------------------------------------------------- clothes

G = 0.012  # how far clothes sit outside the skin


def sleeve(sx, x1, mat, puff=0.0, grow=G):
    """Arm tube from the shoulder out to x1 (meters along the arm), split across the elbow."""
    side = "L" if sx > 0 else "R"
    pieces = [part(f"sleeveU{side}", f"UpperArm.{side}", lambda bm: loft(
        bm, [((0.10 * sx, 0, 1.28), 0.037 + grow + puff, 0.037 + grow + puff),
             ((min(x1, 0.39) * sx, 0, 1.28), 0.030 + grow + puff * 0.5, 0.030 + grow + puff * 0.5)], 8, "x"), mat)]
    if x1 > 0.39:
        pieces.append(part(f"sleeveL{side}", f"LowerArm.{side}", lambda bm: loft(
            bm, [((0.39 * sx, 0, 1.28), 0.030 + grow, 0.030 + grow), ((x1 * sx, 0, 1.28), 0.026 + grow, 0.026 + grow)],
            8, "x"), mat))
    return pieces


def torso_shell(prefix, mat, z0=0.99, grow=G, hem=None, skip=(), n=12):
    """Top body: Spine piece (waist to bust) + Chest piece (bust to collar)."""
    if hem is None:
        low = torso_sections(z0, 1.15, grow)
    else:   # loose top: straight from the bust down to the hem, no cinched waist
        (_, _, hz), hw_, hd_ = hem
        bw, bd = torso_at(1.15)
        bust = ((0, 0, 1.15), bw + grow + 0.008, bd + grow + 0.008)
        mid_z = (hz + 1.15) / 2
        mw, md = torso_at(mid_z)
        mid = ((0, 0, mid_z), max(mw + grow + 0.01, (hw_ + bust[1]) / 2), max(md + grow + 0.01, (hd_ + bust[2]) / 2))
        low = [hem, mid, bust]
    open_front = bool(skip)
    return [part(f"{prefix}Low", "Spine", lambda bm: loft(bm, low, n, skip=skip, cap_start=not open_front), mat),
            part(f"{prefix}Up", "Chest", lambda bm: loft(bm, torso_sections(1.15, 1.32, grow), n, skip=skip,
                                                         cap_end=not open_front), mat)]


def buttons(z_values, y, bone="Spine", mat=None):
    mat = mat or material("White", False)
    return [part(f"btn{z:.2f}", bone if z < 1.15 else "Chest", lambda bm, z=z: box(
        bm, (-0.007, y - 0.006, z - 0.007), (0.007, y + 0.002, z + 0.007)), mat) for z in z_values]


def print_plate(x0, x1, z0, z1, y, texture):
    """Flat decal on the chest (band-tee print) with its own 0..1 UVs."""
    def build(bm):
        uv = bm.loops.layers.uv.new("UVMap")
        v = [bm.verts.new(p) for p in ((x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1))]
        f = bm.faces.new(v)
        for loop, c in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
            loop[uv].uv = c
    return part("print", "Chest", build, material(texture, False))


def top_tee():
    m = material("Cotton")
    return join("Top_Tee", torso_shell("tee", m) + sleeve(1, 0.24, m) + sleeve(-1, 0.24, m))


def top_long_sleeve():
    """Plain fitted long-sleeve tee with a crew neck."""
    m = material("Jersey")
    return join("Top_LongSleeve", torso_shell("ls", m) + sleeve(1, 0.60, m) + sleeve(-1, 0.60, m))


def top_tank():
    """Tank top: scoop neck and wide straps over the shoulders."""
    m = material("Jersey")
    parts = [part("tkLow", "Spine", lambda bm: loft(bm, torso_sections(0.97, 1.15, G), 12, cap_start=False), m),
             part("tkUp", "Chest", lambda bm: loft(bm, torso_sections(1.15, 1.23, G) + [((0, 0, 1.25), 0.104, 0.078)], 12), m)]
    for sx in (1, -1):
        parts.append(part(f"strap{sx}", "Chest", lambda bm, sx=sx: (
            box(bm, (0.058 * sx - 0.011, -0.084, 1.24), (0.058 * sx + 0.011, -0.072, 1.33)),
            box(bm, (0.058 * sx - 0.011, -0.078, 1.32), (0.058 * sx + 0.011, 0.070, 1.338)),
            box(bm, (0.058 * sx - 0.011, 0.062, 1.24), (0.058 * sx + 0.011, 0.074, 1.33))), m))
    return join("Top_Tank", parts)


def top_crop():
    m = material("Cotton")
    parts = [part("cropUp", "Chest", lambda bm: loft(bm, [((0, 0, 1.10), 0.100 + G, 0.075 + G)] +
                                                     torso_sections(1.15, 1.32, G), 12), m)]
    return join("Top_Crop", parts + sleeve(1, 0.20, m, puff=0.012) + sleeve(-1, 0.20, m, puff=0.012))


def top_sweater():
    m = material("Knit")
    parts = torso_shell("sw", m, grow=G + 0.004, hem=((0, 0, 0.88), 0.128 + G, 0.090 + G))
    parts.append(part("swNeck", "Neck", lambda bm: loft(bm, [((0, 0.005, 1.30), 0.055, 0.05),
                                                            ((0, 0.01, 1.40), 0.045, 0.045)], 8), m))
    return join("Top_Sweater", parts + sleeve(1, 0.60, m, grow=G + 0.004) + sleeve(-1, 0.60, m, grow=G + 0.004))


def top_hoodie():
    m = material("Fleece")
    parts = torso_shell("hd", m, grow=G + 0.008, hem=((0, 0, 0.85), 0.132 + G, 0.094 + G))
    # hood lying on the upper back, behind the neck
    parts.append(part("hood", "Chest", lambda bm: loft(bm, [((0, 0.072, 1.345), 0.075, 0.022), ((0, 0.098, 1.29), 0.098, 0.028),
                                                           ((0, 0.108, 1.21), 0.085, 0.024), ((0, 0.104, 1.15), 0.05, 0.015)], 8), m))
    parts.append(part("pocket", "Spine", lambda bm: box(bm, (-0.07, -0.085, 0.92), (0.07, -0.07, 1.00)), m))
    parts.append(part("string", "Chest", lambda bm: [box(bm, (x - 0.004, -0.09, 1.18), (x + 0.004, -0.08, 1.29))
                                                    for x in (-0.03, 0.03)], material("White", False)))
    return join("Top_Hoodie", parts + sleeve(1, 0.62, m, grow=G + 0.008) + sleeve(-1, 0.62, m, grow=G + 0.008))


def top_sailor():
    m = material("Cotton")
    navy = material("Navy", False)
    parts = torso_shell("sl", m, hem=((0, 0, 0.93), 0.110 + G, 0.078 + G))
    # square sailor collar: flat plate on the back + lapels down the front
    parts.append(part("collarBack", "Chest", lambda bm: box(bm, (-0.10, 0.06, 1.17), (0.10, 0.085, 1.33)), navy))
    for sx in (1, -1):
        parts.append(part(f"lapel{sx}", "Chest", lambda bm, sx=sx: strand(
            bm, (0.075 * sx, -0.06, 1.33), (0.01 * sx, -0.10, 1.18), 0.03, 0.01, flat_tip=0.2), navy))
    parts.append(part("ribbon", "Chest", lambda bm: [strand(bm, (0, -0.10, 1.20), (0.05 * sx, -0.11, 1.15), 0.02, 0.008)
                                                    for sx in (1, -1)], material("Red", False)))
    return join("Top_Sailor", parts + sleeve(1, 0.58, m) + sleeve(-1, 0.58, m))


def top_flannel():
    """Grunge: open plaid flannel (long, loose) over a black tee. Wide enough at the hips to hang
    over skirts and pants instead of disappearing into them."""
    m = material("Flannel")
    parts = torso_shell("tee", material("Black", False), z0=0.88)
    low = [((0, 0.005, 0.74), 0.178, 0.132), ((0, 0, 0.90), 0.166, 0.122), ((0, 0, 1.03), 0.136, 0.102),
           ((0, 0, 1.15), 0.108 + G + 0.014, 0.082 + G + 0.014)]
    parts.append(part("flLow", "Spine", lambda bm: loft(bm, low, 12, cap_start=False, cap_end=False, skip={8}), m))
    parts.append(part("flUp", "Chest", lambda bm: loft(bm, torso_sections(1.15, 1.32, G + 0.014), 12,
                                                        cap_end=False, skip={8}), m))
    for sx in (1, -1):  # collar points
        parts.append(part(f"collar{sx}", "Chest", lambda bm, sx=sx: strand(
            bm, (0.06 * sx, -0.045, 1.33), (0.035 * sx, -0.085, 1.25), 0.025, 0.008, flat_tip=0.3), m))
    return join("Top_Flannel", parts + sleeve(1, 0.56, m, grow=G + 0.014) + sleeve(-1, 0.56, m, grow=G + 0.014))


def top_oversized():
    """Grunge: boxy oversized band tee with a pixel skull print and loose short sleeves."""
    m = material("Jersey")
    parts = [part("osLow", "Spine", lambda bm: loft(bm, [((0, 0.005, 0.80), 0.145, 0.105), ((0, 0, 0.99), 0.138, 0.10),
                                                         ((0, 0, 1.15), 0.135, 0.10)], 12, cap_start=False), m),
             part("osUp", "Chest", lambda bm: loft(bm, [((0, 0, 1.15), 0.135, 0.10), ((0, 0, 1.27), 0.13, 0.088),
                                                        ((0, 0, 1.32), 0.075, 0.055)], 12), m),
             print_plate(-0.06, 0.06, 1.08, 1.20, -0.102, "PrintSkull")]
    return join("Top_Oversized", parts + sleeve(1, 0.29, m, puff=0.006, grow=G + 0.01) + sleeve(-1, 0.29, m, puff=0.006, grow=G + 0.01))


def top_cardigan():
    """Girly: cropped buttoned cardigan with long cozy sleeves."""
    m = material("Knit")
    parts = torso_shell("cd", m, grow=G + 0.006, hem=((0, 0, 1.02), 0.098 + G, 0.074 + G))
    parts += buttons((1.05, 1.11, 1.17, 1.23), -0.094)
    return join("Top_Cardigan", parts + sleeve(1, 0.60, m, grow=G + 0.008) + sleeve(-1, 0.60, m, grow=G + 0.008))


def top_puff_blouse():
    """Girly: floral blouse with big puff sleeves and a frilly hem."""
    m = material("Floral")
    parts = torso_shell("pb", m, hem=((0, 0, 0.94), 0.125, 0.092))
    parts.append(part("frill", "Spine", lambda bm: loft(
        bm, [((0, 0, 0.95), 0.12, 0.088), ((0, 0, 0.90), 0.15, 0.115)], 16, cap_start=False, cap_end=False,
        ripple=lambda i, k: (1.12 if i % 2 else 0.94) if k == 1 else 1.0), m))
    for sx in (1, -1):
        side = "L" if sx > 0 else "R"
        parts.append(part(f"puff{side}", f"UpperArm.{side}", lambda bm, sx=sx: loft(
            bm, [((0.09 * sx, 0, 1.28), 0.045, 0.045), ((0.15 * sx, 0, 1.28), 0.075, 0.075),
                 ((0.24 * sx, 0, 1.28), 0.07, 0.07), ((0.29 * sx, 0, 1.28), 0.042, 0.042)], 10, "x"), m))
    return join("Top_PuffBlouse", parts)


def top_slip_dress():
    """Girly/twilight: satin slip dress with thin straps (pair it with no bottom)."""
    m = material("Satin")
    parts = [part("sdLow", "Spine", lambda bm: loft(bm, torso_sections(0.99, 1.15, G), 12, cap_start=False), m),
             part("sdUp", "Chest", lambda bm: loft(bm, torso_sections(1.15, 1.23, G) + [((0, 0, 1.25), 0.10, 0.076)], 12), m),
             part("sdSkirt", "Hips", lambda bm: loft(bm, [((0, 0, 0.99), 0.075 + G + 0.002, 0.055 + G + 0.002),
                                                          ((0, 0, 0.86), 0.13, 0.095), ((0, 0.01, 0.52), 0.17, 0.14)],
                                                     12, cap_start=False, cap_end=False), m)]
    for sx in (1, -1):  # thin straps over the shoulders
        parts.append(part(f"strap{sx}", "Chest", lambda bm, sx=sx: (
            box(bm, (0.05 * sx - 0.005, -0.082, 1.24), (0.05 * sx + 0.005, -0.072, 1.33)),
            box(bm, (0.05 * sx - 0.005, -0.075, 1.32), (0.05 * sx + 0.005, 0.068, 1.335)),
            box(bm, (0.05 * sx - 0.005, 0.062, 1.24), (0.05 * sx + 0.005, 0.072, 1.33))), m))
    return join("Top_SlipDress", parts)


def skirt_rings(length_z, flare, pleats=False):
    sections = [((0, 0, 0.99), 0.075 + G + 0.004, 0.055 + G + 0.004), ((0, 0, 0.90), 0.138, 0.10),
                ((0, 0.01, length_z), 0.138 + flare, 0.10 + flare * 0.8)]
    ripple = (lambda i, k: (1.12 if i % 2 else 0.94) if (pleats and k > 0) else 1.0)
    return sections, ripple


def bottom_skirt(name, texture, length_z=0.64, flare=0.062, pleats=False):
    sections, ripple = skirt_rings(length_z, flare, pleats)
    return join(name, [part(name.lower(), "Hips", lambda bm: loft(
        bm, sections, 16 if pleats else 8, cap_start=False, cap_end=False, ripple=ripple), material(texture))])


def bottom_ruffle(name, texture):
    """Girly: three-tier ruffled mini skirt."""
    tiers = [(0.99, 0.87, 0.075 + G + 0.004, 0.13), (0.89, 0.76, 0.125, 0.17), (0.78, 0.66, 0.16, 0.21)]

    def build(bm):
        for z0, z1, r0, r1 in tiers:
            loft(bm, [((0, 0, z0), r0, r0 * 0.74), ((0, 0.005, z1), r1, r1 * 0.8)], 16, cap_start=False, cap_end=False,
                 ripple=lambda i, k: (1.10 if i % 2 else 0.95) if k == 1 else 1.0)
    return join(name, [part(name.lower(), "Hips", build, material(texture))])


def torso_at(z):
    """Interpolated (half-width, half-depth) of the body at height z."""
    for (z0, w0, d0), (z1, w1, d1) in zip(TORSO, TORSO[1:]):
        if z0 <= z <= z1:
            t = (z - z0) / (z1 - z0)
            return w0 + (w1 - w0) * t, d0 + (d1 - d0) * t
    return TORSO[-1][1:]


# Waistlines: "high" sits above the natural waist (tops look tucked in), "low" rides on the hips.
RISE = {"high": 1.06, "mid": 0.99, "low": 0.875}


def pants_top(m, rise, grow, leg_r=0.066):
    """Hip piece from the crotch up to the waistline, plus a waistband and a gold button.
    Its bottom spreads to the outer edge of the legs so it flows into them."""
    top = RISE[rise]
    outer = 0.066 + leg_r
    rings = [((0, 0, 0.74), outer, leg_r + 0.004)]
    rings += [((0, 0, z), max(w + grow, outer * (0.95 if z < 0.8 else 0)), d + grow)
              for z, w, d in TORSO if 0.76 <= z < top - 0.01]
    w, d = torso_at(top)
    rings.append(((0, 0, top), w + grow, d + grow))
    band = [((0, 0, top - 0.04), torso_at(top - 0.04)[0] + grow + 0.007, torso_at(top - 0.04)[1] + grow + 0.007),
            ((0, 0, top + 0.003), w + grow + 0.006, d + grow + 0.006)]
    by = -(torso_at(top - 0.02)[1] + grow + 0.007)
    return [part("hip", "Hips", lambda bm: loft(bm, rings, 12), m),
            part("band", "Hips", lambda bm: loft(bm, band, 12), m),
            part("button", "Hips", lambda bm: box(bm, (-0.006, by - 0.005, top - 0.03), (0.006, by + 0.002, top - 0.015)),
                 material("Gold", False))]


def bottom_legs(name, texture, knee_z=None, rise="high"):
    """Fitted pants (knee_z None) or shorts (legs end at knee_z)."""
    m = material(texture)
    parts = pants_top(m, rise, G, leg_r=0.054 + G + (0.004 if knee_z else 0))
    for side, sx in sides():
        if knee_z is None:
            parts += [part(f"u{side}", f"UpperLeg.{side}", lambda bm, sx=sx: loft(
                          bm, [((0.064 * sx, 0, 0.80), 0.054 + G, 0.054 + G), ((0.065 * sx, 0, 0.45), 0.040 + G, 0.040 + G)], 8), m),
                      part(f"l{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
                          bm, [((0.07 * sx, 0, 0.45), 0.043 + G, 0.043 + G), ((0.07 * sx, 0, 0.11), 0.042 + G, 0.042 + G)], 8), m)]
        else:
            parts.append(part(f"u{side}", f"UpperLeg.{side}", lambda bm, sx=sx: loft(
                bm, [((0.064 * sx, 0, 0.80), 0.054 + G + 0.004, 0.054 + G + 0.004),
                     ((0.065 * sx, 0, knee_z), 0.052 + G, 0.052 + G)], 8), m))
    return join(name, parts)


def bottom_wide(name, texture, thigh=0.072, knee=0.07, hem=0.078, hem_z=0.03, pockets=False, fitted_thigh=False, rise="high"):
    """Baggy / cargo / flare pants: wide straight legs that bunch up at the hem over the shoes."""
    m = material(texture)
    parts = pants_top(m, rise, G + 0.008, leg_r=(0.054 + G) if fitted_thigh else thigh)
    bunch = lambda i, k: (1.08 if i % 2 else 0.96) if k >= 2 else 1.0   # stacked fabric at the ankle
    for side, sx in sides():
        top_r = 0.054 + G if fitted_thigh else thigh
        parts += [part(f"u{side}", f"UpperLeg.{side}", lambda bm, sx=sx, top_r=top_r: loft(
                      bm, [((0.068 * sx, 0, 0.82), top_r, top_r), ((0.068 * sx, 0, 0.62), (top_r + knee) / 2, (top_r + knee) / 2),
                           ((0.068 * sx, 0, 0.45), knee, knee)], 10), m),
                  part(f"l{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
                      bm, [((0.07 * sx, 0, 0.45), knee, knee), ((0.07 * sx, 0, 0.22), (knee + hem) / 2, (knee + hem) / 2),
                           ((0.07 * sx, -0.005, 0.09), hem, hem), ((0.07 * sx, -0.01, hem_z), hem * 1.02, hem * 1.02)],
                      10, ripple=bunch), m)]
        if pockets:  # cargo pockets on the outer thighs
            parts.append(part(f"pk{side}", f"UpperLeg.{side}", lambda bm, sx=sx: box(
                bm, (min(sx * (0.068 + thigh - 0.01), sx * (0.068 + thigh + 0.012)), -0.035, 0.52),
                    (max(sx * (0.068 + thigh - 0.01), sx * (0.068 + thigh + 0.012)), 0.035, 0.63)), m))
    return join(name, parts)


def leg_radius(z):
    """The body's leg profile (matches body())."""
    pts = [(0.08, 0.025), (0.31, 0.044), (0.45, 0.040), (0.62, 0.048), (0.80, 0.054)]
    for (z0, r0), (z1, r1) in zip(pts, pts[1:]):
        if z <= z1:
            return r0 + (r1 - r0) * (max(z, z0) - z0) / (z1 - z0)
    return pts[-1][1]


def leg_center_y(z):
    """Matches the body's lower leg: the calf bulge sits 8 mm toward the back."""
    if z <= 0.08 or z >= 0.45:
        return 0.0
    return 0.008 * ((z - 0.08) / 0.23 if z <= 0.31 else (0.45 - z) / 0.14)


def legwear(name, texture, z_top, grow=0.005, z_bottom=0.0, frill=False):
    """Socks/tights hugging the leg from z_bottom to z_top (feet included when z_bottom is 0)."""
    m = material(texture)
    parts = []
    for side, sx in sides():
        lower = [z for z in (max(0.08, z_bottom), 0.2, 0.31, 0.45) if z_bottom <= z <= min(z_top, 0.45)]
        if z_top < 0.45:
            lower.append(z_top)
        upper = [z for z in (0.45, 0.62, 0.80) if z <= z_top]
        if z_top > 0.45 and z_top not in upper:
            upper.append(z_top)
        if len(lower) >= 2:
            parts.append(part(f"lw{side}", f"LowerLeg.{side}", lambda bm, sx=sx, lower=lower: loft(
                bm, [((0.07 * sx, leg_center_y(z), z), leg_radius(z) + grow, leg_radius(z) + grow)
                     for z in lower], 8), m))
        if len(upper) >= 2:
            parts.append(part(f"uw{side}", f"UpperLeg.{side}", lambda bm, sx=sx, upper=upper: loft(
                bm, [((0.065 * sx, 0, z), leg_radius(z) + grow, leg_radius(z) + grow) for z in upper], 8), m))
        if z_bottom == 0.0:
            parts.append(part(f"ft{side}", f"Foot.{side}", lambda bm, sx=sx: box(
                bm, (0.07 * sx - 0.033, -0.123, -0.002), (0.07 * sx + 0.033, 0.028, 0.073), taper_front=0.3), m))
        if frill:  # ruffle cuff at the top of ankle socks
            r = leg_radius(z_top) + grow
            parts.append(part(f"fr{side}", f"LowerLeg.{side}", lambda bm, sx=sx, r=r: loft(
                bm, [((0.07 * sx, 0, z_top - 0.005), r, r), ((0.07 * sx, 0, z_top + 0.03), r + 0.022, r + 0.022)], 16,
                cap_start=False, cap_end=False, ripple=lambda i, k: (1.15 if i % 2 else 0.9) if k == 1 else 1.0), m))
    if z_top >= 0.76:  # tights: cover the hips too
        parts.append(part("lwHip", "Hips", lambda bm: loft(bm, torso_sections(0.76, 0.92, grow), 12), m))
    return join(name, parts)


def leg_warmers(name, texture):
    """Slouchy knit warmers bunched over the top of low shoes (z 0.04 up to the calf)."""
    m = material(texture)
    parts = []
    rings = [(0.04, 0.066), (0.085, 0.064), (0.14, 0.062), (0.20, 0.064), (0.26, 0.062), (0.31, 0.058)]
    for side, sx in sides():
        parts.append(part(f"wm{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
            bm, [((0.07 * sx, 0.004, z), r, r) for z, r in rings], 10, cap_start=False, cap_end=False,
            ripple=lambda i, k: (1.07 if (i + k) % 2 else 0.95)), m))
    return join(name, parts)


def shoes(name, top_z, sole=None, upper="Leather", platform=0.0, low=False, strap=False, laces=False):
    m = material(upper)
    parts = []
    for side, sx in sides():
        h = 0.078 if low else 0.085
        parts.append(part(f"{name}F{side}", f"Foot.{side}", lambda bm, sx=sx: box(
            bm, (0.07 * sx - 0.04, -0.135, platform), (0.07 * sx + 0.04, 0.04, h + platform), taper_front=0.25), m))
        if sole or platform:
            parts.append(part(f"{name}S{side}", f"Foot.{side}", lambda bm, sx=sx: box(
                bm, (0.07 * sx - 0.043, -0.14, -0.006), (0.07 * sx + 0.043, 0.045, max(platform, 0.012))),
                material(sole or upper, sole is None)))
        if strap:
            parts.append(part(f"{name}T{side}", f"Foot.{side}", lambda bm, sx=sx: box(
                bm, (0.07 * sx - 0.041, -0.06, 0.055), (0.07 * sx + 0.041, -0.04, 0.075)), m))
        if top_z > 0.1:
            zs = [0.06] + [z for z in (0.2, 0.31) if z < top_z - 0.03] + [top_z]
            parts.append(part(f"{name}L{side}", f"LowerLeg.{side}", lambda bm, sx=sx, zs=zs: loft(
                bm, [((0.07 * sx, leg_center_y(z), z), max(0.046, leg_radius(z) + 0.014), max(0.046, leg_radius(z) + 0.014))
                     for z in zs], 8), m))
        if laces:  # criss-cross lace bars up the front of the shaft
            def lace_bars(bm, sx=sx):
                for z in np.arange(0.10, top_z - 0.02, 0.035):
                    y = leg_center_y(z) - max(0.046, leg_radius(z) + 0.014)   # front of the shaft
                    box(bm, (0.07 * sx - 0.02, y - 0.006, z), (0.07 * sx + 0.02, y + 0.003, z + 0.008))
            parts.append(part(f"{name}X{side}", f"LowerLeg.{side}", lace_bars, material("SoleWhite", False)))
    return join(name, parts, tile=0.15)

# ---------------------------------------------------------------- accessories

EYE_Z = FACE_Z0 + FACE_H * EYE_Y / FACE_PX     # eye line height


def glasses(name, source_obj, remove_lens=True):
    """Pull one frame from the CC0 LowPoly Glasses Pack, strip its lenses and fit it to the face."""
    path = os.path.join(THIRD_PARTY, "LowPolyGlasses", "LowPoly Glasses Pack.blend")
    with bpy.data.libraries.load(path) as (src, dst):
        dst.objects = [source_obj]
    obj = dst.objects[0]
    scene.collection.objects.link(obj)
    bpy.context.view_layer.update()      # makes matrix_world valid for the appended object
    me = obj.data = obj.data.copy()
    me.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)

    if remove_lens:  # lens material: the one named Glass*, else the slot with the fewest faces
        counts = {}
        for p in me.polygons:
            counts[p.material_index] = counts.get(p.material_index, 0) + 1
        names = [s.material.name if s.material else "" for s in obj.material_slots]
        lens = [i for i, n in enumerate(names) if n.lower().startswith("glass")]
        if not lens:
            lens = [min(counts, key=counts.get)]
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in lens], context="FACES")
        bm.to_mesh(me)
        bm.free()

    co = np.array([v.co[:] for v in me.vertices])
    lo, hi = co.min(0), co.max(0)
    size = hi - lo
    depth_axis = 1 if size[1] >= size[2] else 2
    up_axis = 3 - depth_axis
    # the frame (front) is where vertices bunch up near the middle; the temples are sparse
    mid = co[np.abs(co[:, 0] - (lo[0] + hi[0]) / 2) < size[0] * 0.25]
    front_sign = 1 if mid[:, depth_axis].mean() < (lo[depth_axis] + hi[depth_axis]) / 2 else -1
    # temples hinge near the top of the frame
    temples = co[np.abs(co[:, 0] - (lo[0] + hi[0]) / 2) > size[0] * 0.42]
    up_sign = 1 if temples[:, up_axis].mean() > (lo[up_axis] + hi[up_axis]) / 2 else -1

    # map (x, depth, up) -> (x, -front.., z): front goes to -Y, top goes to +Z
    rows = [[1, 0, 0], [0, 0, 0], [0, 0, 0]]
    rows[1][depth_axis] = front_sign
    rows[2][up_axis] = up_sign
    if Matrix(rows).determinant() < 0:   # never mirror (it would turn the mesh inside out)
        rows[0][0] = -1
    rot = Matrix(rows).to_4x4()
    me.transform(rot)
    co = np.array([v.co[:] for v in me.vertices])
    lo, hi = co.min(0), co.max(0)
    s = 0.20 / (hi[0] - lo[0])
    front_center = Vector(((lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2))
    me.transform(Matrix.Translation((0, head_front_y(0, EYE_Z, 0.012), EYE_Z + 0.004)) @
                 Matrix.Scale(s, 4) @ Matrix.Translation(-front_center))

    obj.data.materials.clear()
    obj.data.materials.append(material("White"))
    for p in me.polygons:
        p.material_index = 0
    obj.name = name.lower()
    group = obj.vertex_groups.new(name="Head")
    group.add(range(len(me.vertices)), 1.0, "REPLACE")
    return join(name, [obj], tile=0.05)


def star(bm, center, r_out, r_in, depth):
    """Five-point star prism facing forward (-Y)."""
    c = Vector(center)
    front, back = [], []
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5
        r = r_out if i % 2 == 0 else r_in
        p = c + Vector((math.cos(a) * r, 0, math.sin(a) * r))
        front.append(bm.verts.new(p - Vector((0, depth / 2, 0))))
        back.append(bm.verts.new(p + Vector((0, depth / 2, 0))))
    bm.faces.new(front[::-1])
    bm.faces.new(back)
    for i in range(10):
        j = (i + 1) % 10
        bm.faces.new((front[i], front[j], back[j], back[i]))


def headwear():
    white = material("White")
    pink = material("Pink", False)
    items = [
        join("Headwear_Halo", [part("halo", "Head", lambda bm: torus(bm, (0, 0.02, 1.72), 0.085, 0.009, seg=14, tube=4), white)]),
        join("Headwear_Bow", [part("bow", "Head", lambda bm: (
            [bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0, matrix=Matrix.Translation((0.05 * sx, 0.128, 1.575))
                                        @ Matrix.Diagonal((0.05, 0.022, 0.034, 1))) for sx in (1, -1)],
            bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0, matrix=Matrix.Translation((0, 0.132, 1.575))
                                       @ Matrix.Diagonal((0.018, 0.02, 0.022, 1))),
            [strand(bm, (0.008 * sx, 0.135, 1.56), (0.035 * sx, 0.14, 1.50), 0.012, 0.008, flat_tip=1.0) for sx in (1, -1)]),
            material("Satin"))]),
    ]
    ears = []
    for sx in (1, -1):
        ears.append(part(f"ear{sx}", "Head", lambda bm, sx=sx: strand(
            bm, (0.07 * sx, 0.0, 1.615), (0.095 * sx, 0.01, 1.71), 0.035, 0.022), white))
        ears.append(part(f"inner{sx}", "Head", lambda bm, sx=sx: strand(
            bm, (0.07 * sx, -0.024, 1.625), (0.092 * sx, -0.012, 1.69), 0.022, 0.003), pink))
    items.append(join("Headwear_CatEars", ears))
    phones = [part("band", "Head", lambda bm: torus(bm, (0, 0.0, 1.49), 0.13, 0.008, axis="y", seg=10, tube=4,
                                                     arc=math.pi, start=0.0), white)]
    for sx in (1, -1):
        phones.append(part(f"cup{sx}", "Head", lambda bm, sx=sx: loft(
            bm, [((0.12 * sx, 0, 1.47), 0.04, 0.04), ((0.145 * sx, 0, 1.47), 0.04, 0.04)], 8, "x"), white))
    items.append(join("Headwear_Headphones", phones))

    # grunge: slouchy knit beanie with a folded cuff, sitting low on the forehead
    def beanie(bm):
        build_head(bm, scale=1.2, cut=lambda co: (co - HEAD_C).z < 0.035, seg=16, rings=10)
        loft(bm, [((0, 0.005, 1.515), HEAD_R.x * 1.22, HEAD_R.y * 1.22), ((0, 0.005, 1.56), HEAD_R.x * 1.2, HEAD_R.y * 1.2)],
             16, cap_start=False, cap_end=False)
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.018, matrix=Matrix.Translation((0, 0.035, 1.66)))
    items.append(join("Headwear_Beanie", [part("beanie", "Head", beanie, material("Knit"))], tile=0.12))

    # girly/twilight: soft velvet beret tilted to one side
    def beret(bm):
        loft(bm, [((0.012, 0.01, 1.565), 0.118, 0.126), ((0.025, 0.01, 1.605), 0.15, 0.15),
                  ((0.025, 0.01, 1.63), 0.135, 0.135), ((0.02, 0.01, 1.645), 0.07, 0.07)], 12, cap_start=False)
        box(bm, (0.015, 0.005, 1.645), (0.025, 0.015, 1.665))
    items.append(join("Headwear_Beret", [part("beret", "Head", beret, material("Velvet"))], tile=0.15))

    # girly: two little star clips in the side bangs
    clips = [part(f"star{i}", "Head", lambda bm, z=z, x=x: star(bm, (x, head_front_y(x, z, 0.02), z), 0.016, 0.007, 0.006), white)
             for i, (x, z) in enumerate(((0.072, 1.535), (0.088, 1.505)))]
    items.append(join("Headwear_StarClips", clips))
    return items


def necklace_beads(bm):
    """Small beads in a U from the sides of the neck down onto the chest, resting on the body."""
    for k in range(17):
        t = -1 + 2 * k / 16
        x = 0.056 * t
        z = 1.318 - 0.064 * (1 - t * t)
        w, d = torso_at(z)
        y = -d * math.sqrt(max(0.0, 1 - (x / w) ** 2)) - 0.007
        box(bm, (x - 0.004, y - 0.004, z - 0.004), (x + 0.004, y + 0.004, z + 0.004))


def neckwear():
    """New slot: chokers and necklaces."""
    gold = material("Gold", False)
    items = [
        join("Neck_Choker", [
            part("band", "Neck", lambda bm: loft(bm, [((0, 0.006, 1.345), 0.032, 0.032), ((0, 0.006, 1.37), 0.031, 0.031)],
                                                 10, cap_start=False, cap_end=False), material("LeatherHide")),
            part("ring", "Neck", lambda bm: torus(bm, (0, -0.033, 1.338), 0.009, 0.0025, axis="y", seg=8, tube=3), gold)]),
        join("Neck_Ribbon", [
            part("band", "Neck", lambda bm: loft(bm, [((0, 0.006, 1.35), 0.031, 0.031), ((0, 0.006, 1.365), 0.0305, 0.0305)],
                                                 10, cap_start=False, cap_end=False), material("Satin")),
            part("bow", "Neck", lambda bm: [strand(bm, (0, -0.03, 1.357), (0.028 * sx, -0.034, 1.37), 0.012, 0.006, flat_tip=0.8)
                                            for sx in (1, -1)], material("Satin"))]),
        join("Neck_Chain", [part("chain", "Chest", necklace_beads, material("White")),
                            part("heart", "Chest", lambda bm: star(bm, (0, -torso_at(1.24)[1] - 0.011, 1.24), 0.011, 0.010, 0.005),
                                 material("White"))]),
    ]
    return items

# ---------------------------------------------------------------- build everything

pieces = [body()]
pieces += [eyes(s) for s in EYE_STYLES]
pieces += [face_part("Nose", n) for n in eye_designs.NOSES]
pieces += [face_part("Mouth", m) for m in eye_designs.MOUTHS]
pieces += [brows(b) for b in eye_designs.BROWS]
pieces += extras()
pieces += build_hair()
pieces += [top_tee(), top_long_sleeve(), top_tank(), top_crop(), top_sweater(), top_hoodie(), top_sailor(),
           top_flannel(), top_oversized(), top_cardigan(), top_puff_blouse(), top_slip_dress()]
pieces += [bottom_skirt("Bottom_Skirt", "Tweed"),
           bottom_skirt("Bottom_Pleated", "Plaid", pleats=True),
           bottom_ruffle("Bottom_RuffleSkirt", "Gingham"),
           bottom_skirt("Bottom_LongSkirt", "Tartan", length_z=0.22, flare=0.10),
           ]
for rise in ("High", "Mid", "Low"):   # every pair of pants comes in three rises (picked separately in the creator)
    r = rise.lower()
    pieces += [bottom_legs(f"Bottom_SkinnyJeans_{rise}", "Jeans", rise=r),
               bottom_wide(f"Bottom_BaggyJeans_{rise}", "Jeans", rise=r),
               bottom_wide(f"Bottom_Cargo_{rise}", "Cotton", thigh=0.078, knee=0.074, hem=0.072, pockets=True, rise=r),
               bottom_wide(f"Bottom_FlareJeans_{rise}", "JeansDark", knee=0.046 + G, hem=0.092, hem_z=0.02, fitted_thigh=True, rise=r),
               bottom_legs(f"Bottom_Shorts_{rise}", "Jeans", knee_z=0.66, rise=r),
               bottom_legs(f"Bottom_ShortShorts_{rise}", "Jeans", knee_z=0.715, rise=r)]
pieces += [legwear("Legwear_KneeSocks", "Knit", 0.42, z_bottom=0.0),
           legwear("Legwear_ThighHighs", "Knit", 0.66),
           legwear("Legwear_Tights", "Mesh", 0.92, grow=0.004),
           legwear("Legwear_Fishnets", "Fishnet", 0.92, grow=0.0045),
           legwear("Legwear_FrillySocks", "Cotton", 0.15, z_bottom=0.0, frill=True),
           leg_warmers("Legwear_LegWarmers", "Knit")]
pieces += [shoes("Shoes_Sneakers", 0.0, sole="SoleWhite", upper="Cotton"),
           shoes("Shoes_Boots", 0.38),
           shoes("Shoes_Combat", 0.30, sole="Black", upper="LeatherHide", platform=0.025, laces=True),
           shoes("Shoes_Platforms", 0.42, platform=0.05),
           shoes("Shoes_Loafers", 0.0, low=True, sole="Black"),
           shoes("Shoes_MaryJanes", 0.0, low=True, strap=True)]
pieces += neckwear()
pieces += [glasses("Glasses_Round", "glasses3.001"),
           glasses("Glasses_CatEye", "glasses17.001"),
           glasses("Glasses_Slim", "glasses13.001"),
           glasses("Glasses_Pixel", "glasses16.001", remove_lens=False),
           glasses("Glasses_Shutter", "glasses22.001", remove_lens=False)]
pieces += headwear()

# Shaped for Unity's JsonUtility (no dictionaries): pieces keep build order = menu order.
with open(os.path.join(MODEL_DIR, "Dreamer.manifest.json"), "w") as f:
    json.dump({"pieces": [{"name": k, "submeshes": v} for k, v in MANIFEST.items()],
               "patterns": list(PATTERNS)}, f, indent=1)

# ---------------------------------------------------------------- export

bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
for obj in rig.children:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "Dreamer.blend"))
bpy.ops.export_scene.fbx(
    filepath=os.path.join(MODEL_DIR, "Dreamer.fbx"),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y",
    add_leaf_bones=False,
    use_armature_deform_only=True,
    bake_anim=False,
    path_mode="STRIP",
)

# ---------------------------------------------------------------- previews

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
cam_data = bpy.data.cameras.new("Cam")
cam_data.type = "ORTHO"
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


for m in bpy.data.materials:   # the blink (closed-eye) layer stays hidden in previews
    if m.name.endswith("_L4"):
        bsdf = m.node_tree.nodes["Principled BSDF"]
        for link in list(bsdf.inputs["Alpha"].links):
            m.node_tree.links.remove(link)
        bsdf.inputs["Alpha"].default_value = 0.0


def preview(name, shown, angle=20, z=0.85, ortho=1.9, res=(420, 700)):
    for obj in rig.children:
        obj.hide_render = obj.name not in shown
    a = math.radians(angle)
    cam.location = (math.sin(a) * -4, -math.cos(a) * 4, z)
    cam.rotation_euler = (math.radians(90), 0, math.radians(-angle))
    cam_data.ortho_scale = ortho
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = os.path.join(PREVIEW_DIR, name)
    bpy.ops.render.render(write_still=True)


LOOKS = {
    "grunge": {"Skin_Body", "Eyes_Toon", "Hair_WolfCut_Middle_Medium", "Top_Flannel", "Bottom_BaggyJeans_Low", "Shoes_Combat", "Neck_Choker", "Headwear_Beanie"},
    "girly": {"Skin_Body", "Eyes_Almond", "Hair_Twintails_Choppy_Medium_High", "Top_PuffBlouse", "Bottom_RuffleSkirt", "Legwear_FrillySocks", "Shoes_MaryJanes", "Neck_Ribbon", "Headwear_StarClips"},
    "twilight": {"Skin_Body", "Eyes_Doe", "Hair_Long_Micro_Long", "Top_SlipDress", "Legwear_Fishnets", "Shoes_Platforms", "Neck_Chain", "Headwear_Beret"},
    "baggy": {"Skin_Body", "Eyes_Glossy", "Hair_Twintails_Choppy_Medium_Low", "Top_Oversized", "Bottom_Cargo_Low", "Legwear_LegWarmers", "Shoes_Sneakers", "Glasses_Round"},
    "y2k": {"Skin_Body", "Eyes_Almond", "Hair_Ponytail_Choppy_Medium", "Top_Cardigan", "Bottom_FlareJeans_High", "Shoes_Platforms", "Headwear_Halo"},
}
for look, shown in LOOKS.items():
    preview(f"look_{look}.png", shown)
    preview(f"look_{look}_back.png", shown, angle=160)
for s in EYE_STYLES:
    preview(f"eyes_{s}.png", {"Skin_Body", f"Eyes_{s}", "Hair_Long_Straight_Medium"}, angle=0, z=1.47, ortho=0.36, res=(360, 360))
    preview(f"eyes_{s}_q.png", {"Skin_Body", f"Eyes_{s}", "Hair_Long_Straight_Medium"}, angle=35, z=1.47, ortho=0.36, res=(360, 360))
for g in ("Round", "CatEye", "Slim", "Pixel", "Shutter"):
    preview(f"glasses_{g}.png", {"Skin_Body", "Eyes_Almond", "Hair_Pixie_Medium", f"Glasses_{g}"}, angle=25, z=1.47, ortho=0.35, res=(300, 300))

print("DONE", len(rig.children), "meshes")
