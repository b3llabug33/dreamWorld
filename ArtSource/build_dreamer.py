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


def gray_fabric(name, source, mean=0.82, contrast=1.3):
    """Load a CC0 fabric texture, drop its color and re-center its brightness so a Unity
    tint shows the chosen color with the fabric's weave/pattern on top."""
    src = bpy.data.images.load(os.path.join(THIRD_PARTY, "SBS_Textures", source))
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


def leather_texture():
    w = h = 32
    lum = 0.78 + np.random.default_rng(3).normal(0, 0.035, (h, w)).astype(np.float32)
    lum[0:2, :] = 0.55   # darker seam at the sole line
    return save_png("Leather", np.dstack([lum, lum, lum, np.ones_like(lum)]))


TEXTURES = {
    "Cotton": gray_fabric("Cotton", "Mat_Cloth_Canvas_04-128x128.png", contrast=1.6),
    "Knit": gray_fabric("Knit", "Synth-Fabric_15-128x128.png", contrast=1.2),
    "Plaid": gray_fabric("Plaid", "Mat_Cloth_Plaid_04-128x128.png", mean=0.75, contrast=1.1),
    "Tartan": gray_fabric("Tartan", "Mat_Cloth_Plaid_03-128x128.png", mean=0.75, contrast=1.1),
    "Denim": gray_fabric("Denim", "Synth-Fabric_06-128x128.png", contrast=1.4),
    "Tweed": gray_fabric("Tweed", "Synth-Fabric_02-128x128.png", contrast=1.0),
    "Mesh": gray_fabric("Mesh", "Synth-Fabric_14-128x128.png", contrast=1.2),
    "Hair": hair_texture(),
    "Leather": leather_texture(),
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


def loft(bm, sections, n=8, axis="z", cap_start=True, cap_end=True, ripple=None):
    """Connect a list of (center, rx, ry) cross-sections into a tube.
    ripple(i, ring_index) can return a radius multiplier per vertex (used for pleats)."""
    rings = []
    for k, (c, rx, ry) in enumerate(sections):
        pts = ring_points(c, rx, ry, n, axis)
        if ripple:
            cv = Vector(c)
            pts = [cv + (p - cv) * ripple(i, k) for i, p in enumerate(pts)]
        rings.append([bm.verts.new(p) for p in pts])
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
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
    """Chunky hair lock: 4-sided spike from a wide root to a point (or a blunt edge)."""
    r, t = Vector(root), Vector(tip)
    side, front = Vector((width, 0, 0)), Vector((0, -depth, 0))
    a, b = bm.verts.new(r - side), bm.verts.new(r + side)
    c, d = bm.verts.new(r + front), bm.verts.new(r - front)
    if flat_tip > 0:
        tip_side = Vector((width * flat_tip, 0, 0))
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
HEAD_R = Vector((0.105, 0.115, 0.135))


def head_taper(z):
    """Anime jaw: the head narrows toward a small pointed chin below its center."""
    t = max(0.0, (HEAD_C.z - z) / HEAD_R.z)
    return 1 - 0.42 * t * t, 1 - 0.18 * t


def build_head(bm, scale=1.0, cut=None, seg=12, rings=8):
    """Low-poly ellipsoid head with the anime jaw taper applied."""
    verts = []
    top = bm.verts.new(HEAD_C + Vector((0, 0, HEAD_R.z * scale)))
    bottom = bm.verts.new(HEAD_C - Vector((0, 0, HEAD_R.z * scale)))
    for r in range(1, rings):
        phi = math.pi * r / rings
        row = []
        for s in range(seg):
            th = 2 * math.pi * s / seg + math.pi / seg
            x = math.sin(phi) * math.cos(th) * HEAD_R.x * scale
            y = math.sin(phi) * math.sin(th) * HEAD_R.y * scale
            z = HEAD_C.z + math.cos(phi) * HEAD_R.z * scale
            tx, ty = head_taper(z)
            row.append(bm.verts.new((x * tx, y * ty, z)))
        verts.append(row)
    for s in range(seg):
        j = (s + 1) % seg
        bm.faces.new((top, verts[0][s], verts[0][j]))
        bm.faces.new((bottom, verts[-1][j], verts[-1][s]))
        for r in range(rings - 2):
            bm.faces.new((verts[r][s], verts[r + 1][s], verts[r + 1][j], verts[r][j]))
    if cut:
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if cut(v.co)], context="VERTS")


def head_front_y(x, z, offset):
    """Y of the head's front surface at (x, z), pushed out by `offset` (for face decals)."""
    tx, ty = head_taper(z)
    u = x / (HEAD_R.x * tx)
    w = (z - HEAD_C.z) / HEAD_R.z
    inside = max(0.0, 1 - u * u - w * w)
    return -(HEAD_R.y * ty * math.sqrt(inside)) - offset

# ---------------------------------------------------------------- eyes (face decals)

FACE_W, FACE_H = 0.16, 0.13          # decal size in meters
FACE_Z0 = 1.395                      # decal bottom edge
FACE_PX = 64
EYE_Y = 37                           # eye center row in the face texture
EYE_STYLES = ("Sparkle", "Soft", "Sharp", "Sleepy")


def draw_eyes(style):
    """Three 64x64 layers side by side (192x64): base (whites, lashes, brows, blush, mouth),
    iris (grayscale, tinted by the eye color in Unity), top (pupil + highlights)."""
    base = np.zeros((FACE_PX, FACE_PX, 4), np.float32)
    iris = np.zeros_like(base)
    top = np.zeros_like(base)

    def put(layer, x, y, c):
        if 0 <= x < FACE_PX and 0 <= y < FACE_PX:
            layer[y, x] = (*c, 1) if len(c) == 3 else c

    lash = (0.10, 0.07, 0.14)
    white = (1, 1, 1)
    pupil = (0.08, 0.06, 0.18)
    blush = (0.98, 0.62, 0.70)
    mouth = (0.55, 0.25, 0.30)

    # per style: eye half-width, half-height, iris half-width, lid drop (rows hidden by the lid),
    # outer-corner flick direction (+1 up = cat eye, -1 down = droopy)
    ew, eh, iw, lid, flick = {
        "Sparkle": (8, 10, 6, 0, 0),
        "Soft": (8, 9, 6, 0, -1),
        "Sharp": (8, 7, 5, 0, 1),
        "Sleepy": (8, 9, 6, 6, -1),
    }[style]

    def eye(cx, outer):
        cy = EYE_Y
        lid_top = cy + eh - lid      # rows above this are covered by the eyelid
        for y in range(-eh, eh + 1):
            for x in range(-ew, ew + 1):
                # Sharp eyes tilt up toward the outer corner
                yy = y - (flick * x * outer * 0.25 if style == "Sharp" else 0)
                if (x / (ew + 0.5)) ** 2 + (yy / (eh + 0.5)) ** 2 <= 1 and cy + y < lid_top:
                    put(base, cx + x, cy + y, white)
        for y in range(-eh + 1, eh - 1):
            for x in range(-iw, iw + 1):
                if (x / (iw + 0.5)) ** 2 + (y / (eh - 0.5)) ** 2 <= 1 and cy + y < lid_top:
                    g = 0.45 if y > 3 else 0.72 if y > -4 else 1.0   # dark top -> light bottom
                    put(iris, cx + x, cy + y, (g, g, g))
        for y in range(-3, 4):
            for x in range(-2, 3):
                if cy + y < lid_top:
                    put(top, cx + x, cy + y, pupil)
        if lid < 4:
            for hx, hy in ((-3, 4), (-4, 4), (-3, 3), (-4, 3), (-3, 5)):
                put(top, cx + hx * outer, cy + hy - lid, white)
        put(top, cx + 3 * outer, cy - 4, white)
        # thick upper lash (sits on the lid line), with a flick at the outer corner
        for x in range(-ew - 1, ew + 2):
            arch = 1 if abs(x) < ew - 2 and lid == 0 else 0
            tilt = round(flick * x * outer * 0.25) if style == "Sharp" else 0
            y0 = (lid_top if lid else cy + eh) + arch + tilt
            put(base, cx + x, y0, lash)
            put(base, cx + x, y0 - 1, lash)
        fy = (lid_top if lid else cy + eh) - 1
        put(base, cx + (ew + 2) * outer, fy + flick, lash)
        put(base, cx + (ew + 2) * outer, fy - 1 + flick, lash)
        put(base, cx + (ew + 3) * outer, fy - 2 + 2 * flick, lash)
        for x in range(-2, 4):  # lower lash hint
            put(base, cx + x * outer + 2 * outer, cy - eh, lash)
        for x in range(-5, 5):  # thin brow (often under the bangs)
            put(base, cx + x * outer, cy + eh + 6 + (1 if abs(x) < 3 else 0) - (1 if style == "Soft" else 0), lash)

    eye(17, -1)   # image left = character's right eye
    eye(47, +1)
    for cx in (12, 52):
        for x in range(-3, 4):
            if x % 2 == 0:
                put(base, cx + x, 22, blush)
                put(base, cx + x + 1, 21, blush)
    put(base, 32, 19, (0.85, 0.62, 0.58))  # nose hint
    for x in range(30, 34):
        put(base, x, 13, mouth)
    put(base, 29, 14, mouth)
    put(base, 34, 14, mouth)

    TEXTURES[f"Eyes{style}"] = save_png(f"Eyes{style}", np.concatenate([base, iris, top], axis=1))


def build_eye_layers(bm, style):
    """Three stacked curved grids on the face; each uses one third of the eye atlas and its
    own material slot (0 base, 1 iris, 2 top), so Unity can tint just the iris."""
    uv_layer = bm.loops.layers.uv.new("UVMap")
    nx, nz = 8, 6
    for layer, offset in enumerate((0.004, 0.0048, 0.0056)):
        grid = []
        for j in range(nz + 1):
            row = []
            for i in range(nx + 1):
                u, v = i / nx, j / nz
                x = (u - 0.5) * FACE_W
                z = FACE_Z0 + v * FACE_H
                row.append((bm.verts.new((x, head_front_y(x, z, offset), z)), ((u + layer) / 3, v)))
            grid.append(row)
        for j in range(nz):
            for i in range(nx):
                quad = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
                f = bm.faces.new([q[0] for q in quad])
                f.material_index = layer
                for loop, q in zip(f.loops, quad):
                    loop[uv_layer].uv = q[1]


def eyes(style):
    draw_eyes(style)
    tex = f"Eyes{style}"
    obj = part(f"eyes{style}", "Head", lambda bm: build_eye_layers(bm, style), material(tex, tint=False))
    # same atlas in all three slots; only the iris slot is tinted
    obj.data.materials.clear()
    for tint in (False, True, False):
        m = bpy.data.materials.new(f"{tex}_{'iris' if tint else 'fixed'}")
        m.use_nodes = True
        node = m.node_tree.nodes.new("ShaderNodeTexImage")
        node.image, node.interpolation = TEXTURES[tex], "Closest"
        bsdf = m.node_tree.nodes["Principled BSDF"]
        m.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        m.node_tree.links.new(node.outputs["Alpha"], bsdf.inputs["Alpha"])
        m["texture"], m["tint"] = tex, tint
        obj.data.materials.append(m)
    # Blender clamped the layer indices while the mesh had one slot; recover them from the
    # UVs (each layer uses its own third of the atlas).
    uv = obj.data.uv_layers[0].data
    for poly in obj.data.polygons:
        u = sum(uv[li].uv[0] for li in poly.loop_indices) / len(poly.loop_indices)
        poly.material_index = min(2, int(u * 3))
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
        part("hips", "Hips", lambda bm: loft(bm, torso_sections(0.76, 0.99), 10), skin),
        part("chest", "Chest", lambda bm: loft(bm, torso_sections(0.99, 1.32), 10), skin),
        part("neck", "Neck", lambda bm: loft(bm, [((0, 0.005, 1.30), 0.029, 0.029), ((0, 0.01, 1.42), 0.026, 0.026)], 6), skin),
        part("head", "Head", lambda bm: build_head(bm), skin),
    ]
    for side, sx in sides():
        parts += [
            part(f"upperarm{side}", f"UpperArm.{side}", lambda bm, sx=sx: loft(
                bm, [((0.11 * sx, 0, 1.28), 0.037, 0.037), ((0.39 * sx, 0, 1.28), 0.029, 0.029)], 6, "x"), skin),
            part(f"lowerarm{side}", f"LowerArm.{side}", lambda bm, sx=sx: loft(
                bm, [((0.39 * sx, 0, 1.28), 0.029, 0.029), ((0.61 * sx, 0, 1.28), 0.022, 0.022)], 6, "x"), skin),
            part(f"hand{side}", f"Hand.{side}", lambda bm, sx=sx: hand(bm, sx), skin),
            part(f"upperleg{side}", f"UpperLeg.{side}", lambda bm, sx=sx: loft(
                bm, [((0.064 * sx, 0, 0.80), 0.054, 0.054), ((0.066 * sx, 0, 0.62), 0.048, 0.048),
                     ((0.065 * sx, 0, 0.45), 0.037, 0.037)], 6), skin),
            part(f"lowerleg{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
                bm, [((0.07 * sx, 0, 0.45), 0.040, 0.040), ((0.07 * sx, 0.008, 0.31), 0.044, 0.044),
                     ((0.07 * sx, 0, 0.08), 0.025, 0.025)], 6), skin),
            part(f"foot{side}", f"Foot.{side}", lambda bm, sx=sx: box(
                bm, (0.07 * sx - 0.03, -0.12, 0.0), (0.07 * sx + 0.03, 0.025, 0.07), taper_front=0.3), skin),
        ]
    return join("Skin_Body", parts)

# ---------------------------------------------------------------- hair

hair_mat = material("Hair")
HAIR_UV = dict(tile=0.3, uv_origin=(0, 0, 1.38))  # puts the angel-ring band around z = 1.56


def hair_cap(bm):
    # Slightly bigger head shell. Only open it where the face is, so the sides and
    # back of the head stay covered from every angle (ny/nz: -1..1 across the shell).
    def cut(co):
        r = co - HEAD_C
        ny = r.y / (HEAD_R.y * 1.12)
        nz = r.z / (HEAD_R.z * 1.12)
        face = ny < -0.5 and nz < 0.4        # the face opening, up to the brow
        cheeks = ny < 0 and nz < -0.4        # keep the jaw line clear
        bottom = nz < -0.8                   # no cap under the chin/neck
        return face or cheeks or bottom
    build_head(bm, scale=1.12, cut=cut)


def spiky_bangs(bm):
    y = head_front_y(0, 1.56, 0.012)
    for x, tip_z, w in ((-0.075, 1.47, 0.03), (-0.035, 1.50, 0.028), (0.0, 1.49, 0.028),
                        (0.035, 1.505, 0.028), (0.075, 1.47, 0.03)):
        strand(bm, (x, y + abs(x) * 0.5, 1.60), (x * 1.1, y - 0.005 + abs(x) * 0.6, tip_z), w)


def blunt_bangs(bm):
    """Straight-cut 'hime' fringe: wide flat panels that end in a level line."""
    y = head_front_y(0, 1.56, 0.012)
    for x in (-0.06, -0.02, 0.02, 0.06):
        strand(bm, (x, y + abs(x) * 0.45, 1.60), (x * 1.05, y - 0.004 + abs(x) * 0.5, 1.515), 0.024, flat_tip=0.9)


def side_locks(bm, tip_z, blunt=False):
    for sx in (1, -1):
        strand(bm, (0.105 * sx, -0.03, 1.55), (0.10 * sx, -0.05, tip_z), 0.028, 0.03, flat_tip=0.9 if blunt else 0)


def tail(bm, root, tip_drop, out, thickness=0.05, back=0.0, n=6):
    """A hanging tail (twintail/ponytail): bulges below the tie, tapers to a point."""
    x0, y0, z0 = root
    sections = []
    for k, t in enumerate((0.0, 0.08, 0.3, 0.6, 0.85, 1.0)):
        r = thickness * (0.55, 1.0, 1.05, 0.85, 0.5, 0.12)[k]
        sections.append(((x0 + out * math.sin(t * math.pi / 2), y0 + back * t, z0 - tip_drop * t), r, r * 0.8))
    loft(bm, sections, n)
    torus(bm, (x0, y0, z0 - 0.005), thickness * 0.55, 0.008, seg=8, tube=3)  # hair tie


def hair_style(name, extra, bangs=spiky_bangs):
    def build(bm):
        hair_cap(bm)
        bangs(bm)
        extra(bm)
    return join(name, [part(name.lower(), "Head", build, hair_mat)], **HAIR_UV)


def back_panel(bm, bottom_z, width=0.13, depth=0.05):
    loft(bm, [((0, 0.04, 1.50), 0.12, 0.095), ((0, 0.075, (1.50 + bottom_z) / 2 + 0.05), width, depth + 0.01),
              ((0, 0.09, bottom_z), width * 0.8, depth * 0.6)], 8, cap_start=False)


def build_hair():
    return [
        hair_style("Hair_Bob", lambda bm: (side_locks(bm, 1.40), loft(
            bm, [((0, 0.03, 1.50), 0.125, 0.10), ((0, 0.045, 1.38), 0.13, 0.085)], 8, cap_start=False))),
        hair_style("Hair_Long", lambda bm: (side_locks(bm, 1.22), back_panel(bm, 1.05))),
        hair_style("Hair_Hime", lambda bm: (side_locks(bm, 1.36, blunt=True), back_panel(bm, 0.98, 0.135)), blunt_bangs),
        hair_style("Hair_Twintails", lambda bm: (side_locks(bm, 1.42), [
            tail(bm, (0.115 * sx, 0.03, 1.60), 0.62, 0.07 * sx, 0.055, back=0.03) for sx in (1, -1)])),
        hair_style("Hair_Pigtails", lambda bm: (side_locks(bm, 1.42), [
            tail(bm, (0.11 * sx, 0.06, 1.41), 0.30, 0.03 * sx, 0.04) for sx in (1, -1)])),
        hair_style("Hair_Ponytail", lambda bm: (side_locks(bm, 1.44), tail(bm, (0, 0.13, 1.57), 0.45, 0.0, 0.055, back=0.04))),
        hair_style("Hair_Buns", lambda bm: (side_locks(bm, 1.40), [bmesh.ops.create_icosphere(
            bm, subdivisions=1, radius=0.055, matrix=Matrix.Translation((0.09 * sx, 0.02, 1.63))) for sx in (1, -1)])),
        hair_style("Hair_Pixie", lambda bm: side_locks(bm, 1.48)),
    ]

# ---------------------------------------------------------------- clothes

G = 0.012  # how far clothes sit outside the skin


def sleeve(sx, x1, mat, puff=0.0, grow=G):
    """Arm tube from the shoulder out to x1 (meters along the arm), split across the elbow."""
    side = "L" if sx > 0 else "R"
    pieces = [part(f"sleeveU{side}", f"UpperArm.{side}", lambda bm: loft(
        bm, [((0.10 * sx, 0, 1.28), 0.037 + grow + puff, 0.037 + grow + puff),
             ((min(x1, 0.39) * sx, 0, 1.28), 0.030 + grow + puff * 0.5, 0.030 + grow + puff * 0.5)], 6, "x"), mat)]
    if x1 > 0.39:
        pieces.append(part(f"sleeveL{side}", f"LowerArm.{side}", lambda bm: loft(
            bm, [((0.39 * sx, 0, 1.28), 0.030 + grow, 0.030 + grow), ((x1 * sx, 0, 1.28), 0.026 + grow, 0.026 + grow)],
            6, "x"), mat))
    return pieces


def torso_shell(prefix, mat, z0=0.99, grow=G, hem=None):
    """Top body: Spine piece (waist to bust) + Chest piece (bust to collar)."""
    low = torso_sections(z0, 1.15, grow) if hem is None else [hem] + torso_sections(0.99, 1.15, grow + 0.008)
    return [part(f"{prefix}Low", "Spine", lambda bm: loft(bm, low, 10), mat),
            part(f"{prefix}Up", "Chest", lambda bm: loft(bm, torso_sections(1.15, 1.32, grow), 10), mat)]


def top_tee():
    m = material("Cotton")
    return join("Top_Tee", torso_shell("tee", m) + sleeve(1, 0.24, m) + sleeve(-1, 0.24, m))


def top_crop():
    m = material("Cotton")
    parts = [part("cropUp", "Chest", lambda bm: loft(bm, [((0, 0, 1.10), 0.100 + G, 0.075 + G)] +
                                                     torso_sections(1.15, 1.32, G), 10), m)]
    return join("Top_Crop", parts + sleeve(1, 0.20, m, puff=0.012) + sleeve(-1, 0.20, m, puff=0.012))


def top_sweater():
    m = material("Knit")
    parts = torso_shell("sw", m, grow=G + 0.004, hem=((0, 0, 0.88), 0.165, 0.12))
    parts.append(part("swNeck", "Neck", lambda bm: loft(bm, [((0, 0.005, 1.30), 0.055, 0.05),
                                                            ((0, 0.01, 1.40), 0.045, 0.045)], 6), m))
    return join("Top_Sweater", parts + sleeve(1, 0.60, m, grow=G + 0.004) + sleeve(-1, 0.60, m, grow=G + 0.004))


def top_hoodie():
    m = material("Knit")
    parts = torso_shell("hd", m, grow=G + 0.008, hem=((0, 0, 0.86), 0.15, 0.11))
    # hood lying on the upper back, behind the neck
    parts.append(part("hood", "Chest", lambda bm: loft(bm, [((0, 0.05, 1.29), 0.09, 0.06), ((0, 0.075, 1.36), 0.10, 0.06),
                                                           ((0, 0.085, 1.40), 0.07, 0.04)], 8), m))
    parts.append(part("pocket", "Spine", lambda bm: box(bm, (-0.07, -0.085, 0.92), (0.07, -0.07, 1.00)), m))
    parts.append(part("string", "Chest", lambda bm: [box(bm, (x - 0.004, -0.09, 1.18), (x + 0.004, -0.08, 1.29))
                                                    for x in (-0.03, 0.03)], material("White", False)))
    return join("Top_Hoodie", parts + sleeve(1, 0.62, m, grow=G + 0.008) + sleeve(-1, 0.62, m, grow=G + 0.008))


def top_sailor():
    m = material("Cotton")
    navy = material("Navy", False)
    parts = torso_shell("sl", m, hem=((0, 0, 0.93), 0.13, 0.095))
    # square sailor collar: flat plate on the back + lapels down the front
    parts.append(part("collarBack", "Chest", lambda bm: box(bm, (-0.10, 0.06, 1.17), (0.10, 0.085, 1.33)), navy))
    for sx in (1, -1):
        parts.append(part(f"lapel{sx}", "Chest", lambda bm, sx=sx: strand(
            bm, (0.075 * sx, -0.06, 1.33), (0.01 * sx, -0.10, 1.18), 0.03, 0.01, flat_tip=0.2), navy))
    parts.append(part("ribbon", "Chest", lambda bm: [strand(bm, (0, -0.10, 1.20), (0.05 * sx, -0.11, 1.15), 0.02, 0.008)
                                                    for sx in (1, -1)], material("Red", False)))
    return join("Top_Sailor", parts + sleeve(1, 0.58, m) + sleeve(-1, 0.58, m))


def skirt_rings(length_z, flare, pleats=False):
    sections = [((0, 0, 0.99), 0.075 + G + 0.004, 0.055 + G + 0.004), ((0, 0, 0.90), 0.138, 0.10),
                ((0, 0.01, length_z), 0.138 + flare, 0.10 + flare * 0.8)]
    ripple = (lambda i, k: (1.12 if i % 2 else 0.94) if (pleats and k > 0) else 1.0)
    return sections, ripple


def bottom_skirt(name, texture, length_z=0.64, flare=0.062, pleats=False):
    sections, ripple = skirt_rings(length_z, flare, pleats)
    return join(name, [part(name.lower(), "Hips", lambda bm: loft(
        bm, sections, 16 if pleats else 8, cap_start=False, cap_end=False, ripple=ripple), material(texture))])


def bottom_legs(name, texture, knee_z=None):
    """Pants (knee_z None) or shorts (legs end at knee_z)."""
    m = material(texture)
    parts = [part("hip", "Hips", lambda bm: loft(bm, torso_sections(0.76, 0.99, G), 10), m)]
    for side, sx in sides():
        if knee_z is None:
            parts += [part(f"u{side}", f"UpperLeg.{side}", lambda bm, sx=sx: loft(
                          bm, [((0.064 * sx, 0, 0.80), 0.054 + G, 0.054 + G), ((0.065 * sx, 0, 0.45), 0.040 + G, 0.040 + G)], 6), m),
                      part(f"l{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
                          bm, [((0.07 * sx, 0, 0.45), 0.043 + G, 0.043 + G), ((0.07 * sx, 0, 0.11), 0.042 + G, 0.042 + G)], 6), m)]
        else:
            parts.append(part(f"u{side}", f"UpperLeg.{side}", lambda bm, sx=sx: loft(
                bm, [((0.064 * sx, 0, 0.80), 0.054 + G + 0.004, 0.054 + G + 0.004),
                     ((0.065 * sx, 0, knee_z), 0.052 + G, 0.052 + G)], 6), m))
    return join(name, parts)


def legwear(name, texture, z_top, grow=0.004, z_bottom=0.0, chunky=False):
    """Socks/tights hugging the leg from z_bottom to z_top (feet included when z_bottom is 0)."""
    m = material(texture)
    parts = []

    def leg_radius(z):  # matches the body's leg profile
        pts = [(0.08, 0.025), (0.31, 0.044), (0.45, 0.040), (0.62, 0.048), (0.80, 0.054)]
        for (z0, r0), (z1, r1) in zip(pts, pts[1:]):
            if z <= z1:
                return r0 + (r1 - r0) * (max(z, z0) - z0) / (z1 - z0)
        return pts[-1][1]

    for side, sx in sides():
        lower = [z for z in (max(0.08, z_bottom), 0.31, 0.45) if z_bottom <= z <= min(z_top, 0.45)]
        if z_top < 0.45:
            lower.append(z_top)
        upper = [z for z in (0.45, 0.62, 0.80) if z <= z_top]
        if z_top > 0.45 and z_top not in upper:
            upper.append(z_top)
        extra = 0.018 if chunky else 0.0
        if len(lower) >= 2:
            parts.append(part(f"lw{side}", f"LowerLeg.{side}", lambda bm, sx=sx, lower=lower: loft(
                bm, [((0.07 * sx, 0.008 if 0.2 < z < 0.4 else 0, z), leg_radius(z) + grow + extra,
                      leg_radius(z) + grow + extra) for z in lower], 6), m))
        if len(upper) >= 2:
            parts.append(part(f"uw{side}", f"UpperLeg.{side}", lambda bm, sx=sx, upper=upper: loft(
                bm, [((0.065 * sx, 0, z), leg_radius(z) + grow, leg_radius(z) + grow) for z in upper], 6), m))
        if z_bottom == 0.0:
            parts.append(part(f"ft{side}", f"Foot.{side}", lambda bm, sx=sx: box(
                bm, (0.07 * sx - 0.033, -0.123, -0.002), (0.07 * sx + 0.033, 0.028, 0.073), taper_front=0.3), m))
    if z_top >= 0.76:  # tights: cover the hips too
        parts.append(part("lwHip", "Hips", lambda bm: loft(bm, torso_sections(0.76, 0.92, grow), 10), m))
    return join(name, parts)


def shoes(name, top_z, sole=None, upper="Leather", platform=0.0, low=False, strap=False):
    m = material(upper)
    parts = []
    for side, sx in sides():
        h = 0.06 if low else 0.085
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
            parts.append(part(f"{name}L{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
                bm, [((0.07 * sx, 0, 0.08), 0.058, 0.058), ((0.07 * sx, 0.008, top_z), 0.052 + G + 0.006, 0.052 + G + 0.006)],
                6), m))
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


def headwear():
    gold, white = material("White"), material("White")
    pink = material("Pink", False)
    items = [
        join("Headwear_Halo", [part("halo", "Head", lambda bm: torus(bm, (0, 0.02, 1.72), 0.085, 0.009, seg=14, tube=4), gold)]),
        join("Headwear_Bow", [part("bow", "Head", lambda bm: (
            [strand(bm, (0, 0.13, 1.56), (0.09 * sx, 0.14, 1.60), 0.035, 0.018, flat_tip=0.7) for sx in (1, -1)],
            [strand(bm, (0, 0.13, 1.54), (0.04 * sx, 0.14, 1.45), 0.015, 0.01, flat_tip=1.0) for sx in (1, -1)],
            box(bm, (-0.018, 0.12, 1.535), (0.018, 0.15, 1.575))), white)]),
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
    return items

# ---------------------------------------------------------------- build everything

pieces = [body()]
pieces += [eyes(s) for s in EYE_STYLES]
pieces += build_hair()
pieces += [top_tee(), top_crop(), top_sweater(), top_hoodie(), top_sailor()]
pieces += [bottom_skirt("Bottom_Skirt", "Tweed"),
           bottom_skirt("Bottom_Pleated", "Plaid", pleats=True),
           bottom_skirt("Bottom_LongSkirt", "Tartan", length_z=0.22, flare=0.10),
           bottom_legs("Bottom_Jeans", "Denim"),
           bottom_legs("Bottom_Shorts", "Denim", knee_z=0.66)]
pieces += [legwear("Legwear_KneeSocks", "Knit", 0.42, z_bottom=0.0),
           legwear("Legwear_ThighHighs", "Knit", 0.66),
           legwear("Legwear_Tights", "Mesh", 0.92, grow=0.003),
           legwear("Legwear_LegWarmers", "Knit", 0.36, z_bottom=0.10, chunky=True)]
pieces += [shoes("Shoes_Sneakers", 0.0, sole="SoleWhite", upper="Cotton"),
           shoes("Shoes_Boots", 0.38),
           shoes("Shoes_Platforms", 0.42, platform=0.05),
           shoes("Shoes_Loafers", 0.0, low=True, sole="Black"),
           shoes("Shoes_MaryJanes", 0.0, low=True, strap=True)]
pieces += [glasses("Glasses_Round", "glasses3.001"),
           glasses("Glasses_CatEye", "glasses17.001"),
           glasses("Glasses_Slim", "glasses13.001"),
           glasses("Glasses_Pixel", "glasses16.001", remove_lens=False),
           glasses("Glasses_Shutter", "glasses22.001", remove_lens=False)]
pieces += headwear()

# Shaped for Unity's JsonUtility (no dictionaries): pieces keep build order = menu order.
with open(os.path.join(MODEL_DIR, "Dreamer.manifest.json"), "w") as f:
    json.dump({"pieces": [{"name": k, "submeshes": v} for k, v in MANIFEST.items()]}, f, indent=1)

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
    "look1": {"Skin_Body", "Eyes_Sparkle", "Hair_Twintails", "Top_Sailor", "Bottom_Pleated", "Legwear_ThighHighs", "Shoes_Loafers", "Headwear_Halo"},
    "look2": {"Skin_Body", "Eyes_Sleepy", "Hair_Hime", "Top_Hoodie", "Bottom_Shorts", "Legwear_Tights", "Shoes_Platforms", "Headwear_CatEars"},
    "look3": {"Skin_Body", "Eyes_Sharp", "Hair_Ponytail", "Top_Crop", "Bottom_Jeans", "Shoes_Sneakers", "Glasses_Round", "Headwear_Headphones"},
    "look4": {"Skin_Body", "Eyes_Soft", "Hair_Pigtails", "Top_Sweater", "Bottom_LongSkirt", "Legwear_LegWarmers", "Shoes_MaryJanes", "Glasses_CatEye", "Headwear_Bow"},
}
for look, shown in LOOKS.items():
    preview(f"look_{look}.png", shown)
    preview(f"look_{look}_back.png", shown, angle=160)
for s in EYE_STYLES:
    preview(f"eyes_{s}.png", {"Skin_Body", f"Eyes_{s}", "Hair_Pixie"}, angle=0, z=1.47, ortho=0.32, res=(300, 300))
for g in ("Round", "CatEye", "Slim", "Pixel", "Shutter"):
    preview(f"glasses_{g}.png", {"Skin_Body", "Eyes_Sparkle", "Hair_Pixie", f"Glasses_{g}"}, angle=25, z=1.47, ortho=0.35, res=(300, 300))

print("DONE", len(rig.children), "meshes")
