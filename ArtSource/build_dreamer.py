"""
Builds "Dreamer", the dreamWorld PS1-style anime base character, from code.

Run headless:
  "C:/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --python ArtSource/build_dreamer.py

Like real PS1 characters, the body is made of separate low-poly segments, each rigidly
attached to one bone (no weight painting needed). Hair and outfit pieces are separate
meshes on the same skeleton, named Slot_Style (Hair_Bob, Top_Tee, ...), so Unity's
character creator can toggle them on and off.

Blender axes: Z is up, the character faces -Y, and the character's left (.L) is +X.
Outputs: Assets/Models/Dreamer/Dreamer.fbx + Face.png, ArtSource/Dreamer.blend, preview PNGs.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODEL_DIR = os.path.join(ROOT, "Assets", "Models", "Dreamer")
PREVIEW_DIR = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(ROOT, "ArtSource")
os.makedirs(MODEL_DIR, exist_ok=True)

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

# ---------------------------------------------------------------- materials

def make_material(name, color, image=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = 1.0
    if image is not None:
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.interpolation = "Closest"
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        mat.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    return mat

# Pieces are mostly white so Unity can tint them per slot; preview colors make the
# Blender renders readable and are only used for the preview.
PREVIEW = {
    "Skin": (1.0, 0.85, 0.75), "Hair": (0.20, 0.17, 0.30), "Top": (0.80, 0.82, 0.88),
    "Bottom": (0.25, 0.27, 0.45), "Shoes": (0.45, 0.30, 0.35),
}

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


def loft(bm, sections, n=8, axis="z", cap_start=True, cap_end=True):
    """Connect a list of (center, rx, ry) cross-sections into a tube."""
    rings = [[bm.verts.new(p) for p in ring_points(c, rx, ry, n, axis)] for c, rx, ry in sections]
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if cap_start:
        bm.faces.new(list(reversed(rings[0])))
    if cap_end:
        bm.faces.new(rings[-1])
    return rings


def box(bm, lo, hi):
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    v = [bm.verts.new(p) for p in (
        (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
        (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([v[i] for i in f])


def part(name, bone, build, material):
    """Make one rigid mesh segment bound 100% to `bone`."""
    bm = bmesh.new()
    build(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    group = obj.vertex_groups.new(name=bone)
    group.add(range(len(me.vertices)), 1.0, "REPLACE")
    return obj


def join(name, parts):
    """Merge segments into one mesh (vertex groups survive) and skin it to the rig."""
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
    return obj


def sides():
    return (("L", 1), ("R", -1))

# ---------------------------------------------------------------- head shape

HEAD_C = Vector((0, 0, 1.485))
HEAD_R = Vector((0.105, 0.115, 0.135))


def head_taper(z):
    """Anime jaw: the head narrows toward a small pointed chin below its center."""
    t = max(0.0, (HEAD_C.z - z) / HEAD_R.z)
    return 1 - 0.42 * t * t, 1 - 0.18 * t


def build_head(bm, scale=1.0, cut=None):
    """Low-poly ellipsoid head (10 x 7) with the anime jaw taper applied."""
    seg, rings = 10, 7
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
    """Y of the head's front surface at (x, z), pushed out by `offset` (for the face decal)."""
    tx, ty = head_taper(z)
    u = x / (HEAD_R.x * tx)
    w = (z - HEAD_C.z) / HEAD_R.z
    inside = max(0.0, 1 - u * u - w * w)
    return -(HEAD_R.y * ty * math.sqrt(inside)) - offset

# ---------------------------------------------------------------- face texture

FACE_W, FACE_H = 0.16, 0.13          # decal size in meters
FACE_Z0 = 1.395                      # decal bottom edge
FACE_PX = 64
EYE_Y = 37                           # eye center row in the face texture


def draw_face():
    """Pixel-art anime face on a transparent 64x64 image: big eyes, blush, tiny mouth."""
    img = bpy.data.images.new("Face", FACE_PX, FACE_PX, alpha=True)
    px = [0.0] * (FACE_PX * FACE_PX * 4)

    def put(x, y, c):
        if 0 <= x < FACE_PX and 0 <= y < FACE_PX:
            i = (y * FACE_PX + x) * 4
            px[i:i + 4] = c

    lash = (0.10, 0.07, 0.14, 1)
    white = (1, 1, 1, 1)
    iris_dark = (0.22, 0.20, 0.55, 1)
    iris = (0.40, 0.42, 0.85, 1)
    iris_light = (0.62, 0.75, 1.0, 1)
    pupil = (0.08, 0.06, 0.18, 1)
    blush = (0.98, 0.62, 0.70, 1)
    mouth = (0.55, 0.25, 0.30, 1)

    def eye(cx, outer):
        # outer = +1 if the eye's outer corner points to +x
        ew, eh = 8, 10  # half-sizes: big tall anime eye
        cy = EYE_Y
        for y in range(-eh, eh + 1):
            for x in range(-ew, ew + 1):
                if (x / (ew + 0.5)) ** 2 + (y / (eh + 0.5)) ** 2 <= 1:
                    put(cx + x, cy + y, white)
        for y in range(-eh + 1, eh - 1):
            for x in range(-6, 7):
                if (x / 6.5) ** 2 + (y / (eh - 0.5)) ** 2 <= 1:
                    c = iris_dark if y > 3 else iris if y > -4 else iris_light
                    put(cx + x, cy + y, c)
        for y in range(-3, 4):
            for x in range(-2, 3):
                put(cx + x, cy + y, pupil)
        # highlights
        for hx, hy in ((-3, 4), (-4, 4), (-3, 3), (-4, 3), (-3, 5)):
            put(cx + hx * outer, cy + hy, white)
        put(cx + 3 * outer, cy - 4, white)
        # thick upper lash with a flick at the outer corner
        for x in range(-ew - 1, ew + 2):
            arch = 1 if abs(x) < ew - 2 else 0  # lash curves up in the middle
            put(cx + x, cy + eh + arch, lash)
            put(cx + x, cy + eh - 1 + arch, lash)
        put(cx + (ew + 2) * outer, cy + eh - 1, lash)
        put(cx + (ew + 2) * outer, cy + eh - 2, lash)
        put(cx + (ew + 3) * outer, cy + eh - 3, lash)
        # lower lash hint
        for x in range(-2, 4):
            put(cx + x * outer + 2 * outer, cy - eh, lash)

    eye(17, -1)   # image left = character's right eye
    eye(47, +1)
    for cx in (12, 52):
        for x in range(-3, 4):
            if x % 2 == 0:
                put(cx + x, 22, blush)
                put(cx + x + 1, 21, blush)
    for x in range(30, 34):
        put(x, 13, mouth)
    put(29, 14, mouth)
    put(34, 14, mouth)

    img.pixels = px
    path = os.path.join(MODEL_DIR, "Face.png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def build_face_decal(bm):
    """Curved grid hugging the front of the head; UVs map the face texture onto it."""
    nx, nz = 8, 6
    grid = []
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for j in range(nz + 1):
        row = []
        for i in range(nx + 1):
            u, v = i / nx, j / nz
            x = (u - 0.5) * FACE_W
            z = FACE_Z0 + v * FACE_H
            row.append((bm.verts.new((x, head_front_y(x, z, 0.004), z)), (u, v)))
        grid.append(row)
    for j in range(nz):
        for i in range(nx):
            quad = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
            f = bm.faces.new([q[0] for q in quad])
            for loop, q in zip(f.loops, quad):
                loop[uv_layer].uv = q[1]

# ---------------------------------------------------------------- body

skin = make_material("Skin", PREVIEW["Skin"])
face_img = draw_face()
face_mat = make_material("Face", (1, 1, 1), face_img)

body_parts = []
TORSO = [  # (z, half-width, half-depth): slim hourglass, small waist at 0.99
    (0.76, 0.100, 0.074), (0.84, 0.118, 0.082), (0.92, 0.102, 0.070),
    (0.99, 0.075, 0.055), (1.07, 0.092, 0.066), (1.15, 0.108, 0.082),
    (1.23, 0.108, 0.072), (1.29, 0.104, 0.060), (1.32, 0.068, 0.048),
]


def torso_sections(z0, z1, grow=0.0):
    return [((0, 0, z), w + grow, d + grow) for z, w, d in TORSO if z0 <= z <= z1]


body_parts.append(part("hips", "Hips", lambda bm: loft(bm, torso_sections(0.76, 0.99), 8), skin))
body_parts.append(part("chest", "Chest", lambda bm: loft(bm, torso_sections(0.99, 1.32), 8), skin))
body_parts.append(part("neck", "Neck", lambda bm: loft(bm, [((0, 0.005, 1.30), 0.029, 0.029), ((0, 0.01, 1.42), 0.026, 0.026)], 6), skin))
body_parts.append(part("head", "Head", lambda bm: build_head(bm), skin))

for side, sx in sides():
    body_parts.append(part(f"upperarm{side}", f"UpperArm.{side}", lambda bm, sx=sx: loft(
        bm, [((0.11 * sx, 0, 1.28), 0.037, 0.037), ((0.39 * sx, 0, 1.28), 0.029, 0.029)], 6, "x"), skin))
    body_parts.append(part(f"lowerarm{side}", f"LowerArm.{side}", lambda bm, sx=sx: loft(
        bm, [((0.39 * sx, 0, 1.28), 0.029, 0.029), ((0.61 * sx, 0, 1.28), 0.022, 0.022)], 6, "x"), skin))
    body_parts.append(part(f"hand{side}", f"Hand.{side}", lambda bm, sx=sx: box(
        bm, (min(0.60 * sx, 0.69 * sx), -0.013, 1.25), (max(0.60 * sx, 0.69 * sx), 0.013, 1.30)), skin))
    body_parts.append(part(f"upperleg{side}", f"UpperLeg.{side}", lambda bm, sx=sx: loft(
        bm, [((0.064 * sx, 0, 0.80), 0.054, 0.054), ((0.066 * sx, 0, 0.62), 0.048, 0.048),
             ((0.065 * sx, 0, 0.45), 0.037, 0.037)], 6), skin))
    body_parts.append(part(f"lowerleg{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
        bm, [((0.07 * sx, 0, 0.45), 0.040, 0.040), ((0.07 * sx, 0.008, 0.31), 0.044, 0.044),
             ((0.07 * sx, 0, 0.08), 0.025, 0.025)], 6), skin))
    body_parts.append(part(f"foot{side}", f"Foot.{side}", lambda bm, sx=sx: box(
        bm, (0.07 * sx - 0.03, -0.12, 0.0), (0.07 * sx + 0.03, 0.025, 0.07)), skin))

body = join("Skin_Body", body_parts)
face = join("Face", [part("face", "Head", build_face_decal, face_mat)])

# ---------------------------------------------------------------- hair

hair_mat = make_material("Hair", PREVIEW["Hair"])


def hair_cap(bm):
    # Slightly bigger head shell; cut away the face below the brow and the nape.
    def cut(co):
        front_face = co.y < -0.02 and co.z < HEAD_C.z + 0.035
        below_ears = co.z < HEAD_C.z - 0.06
        return front_face or below_ears
    build_head(bm, scale=1.12, cut=cut)


def strand(bm, root, tip, width, depth=0.025):
    """Flat chunky hair lock: a 4-sided spike from a wide root to a point."""
    r = Vector(root)
    t = Vector(tip)
    side = Vector((width, 0, 0))
    front = Vector((0, -depth, 0))
    a, b = bm.verts.new(r - side), bm.verts.new(r + side)
    c, d = bm.verts.new(r + front), bm.verts.new(r - front)
    p = bm.verts.new(t)
    for f in ((a, c, p), (c, b, p), (b, d, p), (d, a, p), (a, d, b, c)):
        bm.faces.new(f)


def bangs(bm):
    y = head_front_y(0, 1.56, 0.012)
    for x, tip_z, w in ((-0.075, 1.47, 0.03), (-0.035, 1.50, 0.028), (0.0, 1.49, 0.028),
                        (0.035, 1.505, 0.028), (0.075, 1.47, 0.03)):
        strand(bm, (x, y + abs(x) * 0.5, 1.60), (x * 1.1, y - 0.005 + abs(x) * 0.6, tip_z), w)


def side_locks(bm, tip_z):
    for sx in (1, -1):
        strand(bm, (0.105 * sx, -0.03, 1.55), (0.10 * sx, -0.05, tip_z), 0.028, 0.03)


def hair_style(name, extra):
    def build(bm):
        hair_cap(bm)
        bangs(bm)
        extra(bm)
    return join(name, [part(name.lower(), "Head", build, hair_mat)])


def bob_extra(bm):
    side_locks(bm, 1.40)
    loft(bm, [((0, 0.03, 1.50), 0.125, 0.10), ((0, 0.045, 1.38), 0.13, 0.085)], 8, cap_start=False)


def long_extra(bm):
    side_locks(bm, 1.22)
    loft(bm, [((0, 0.04, 1.50), 0.12, 0.095), ((0, 0.08, 1.30), 0.13, 0.05),
              ((0, 0.10, 1.05), 0.10, 0.03)], 8, cap_start=False)


def buns_extra(bm):
    side_locks(bm, 1.40)
    for sx in (1, -1):
        c = Vector((0.09 * sx, 0.02, 1.63))
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.055,
                                   matrix=Matrix.Translation(c))


hair_styles = [hair_style("Hair_Bob", bob_extra), hair_style("Hair_Long", long_extra),
               hair_style("Hair_Buns", buns_extra)]

# ---------------------------------------------------------------- clothes

top_mat = make_material("Top", PREVIEW["Top"])
bottom_mat = make_material("Bottom", PREVIEW["Bottom"])
shoe_mat = make_material("Shoes", PREVIEW["Shoes"])
G = 0.012  # how far clothes sit outside the skin


def sleeve(sx, x1, bone_split=True):
    """Arm tube from the shoulder out to x1 (meters along the arm), split across the elbow."""
    pieces = [part(f"sleeveU{sx}", f"UpperArm.{'L' if sx > 0 else 'R'}", lambda bm: loft(
        bm, [((0.10 * sx, 0, 1.28), 0.037 + G, 0.037 + G), ((min(x1, 0.39) * sx, 0, 1.28), 0.030 + G, 0.030 + G)],
        6, "x"), top_mat)]
    if x1 > 0.39:
        pieces.append(part(f"sleeveL{sx}", f"LowerArm.{'L' if sx > 0 else 'R'}", lambda bm: loft(
            bm, [((0.39 * sx, 0, 1.28), 0.030 + G, 0.030 + G), ((x1 * sx, 0, 1.28), 0.026 + G, 0.026 + G)],
            6, "x"), top_mat))
    return pieces


def top_tee():
    parts = [part("teeLow", "Spine", lambda bm: loft(bm, torso_sections(0.99, 1.15, G), 8), top_mat),
             part("teeUp", "Chest", lambda bm: loft(bm, torso_sections(1.15, 1.32, G), 8), top_mat)]
    for sx in (1, -1):
        parts += sleeve(sx, 0.24)
    return join("Top_Tee", parts)


def top_sweater():
    parts = [part("swLow", "Spine", lambda bm: loft(bm, [((0, 0, 0.88), 0.165, 0.12),
                                                         ((0, 0, 0.99), 0.075 + G + 0.010, 0.055 + G + 0.010),
                                                         ((0, 0, 1.07), 0.092 + G + 0.006, 0.066 + G + 0.006),
                                                         ((0, 0, 1.15), 0.108 + G + 0.004, 0.082 + G + 0.004)], 8), top_mat),
             part("swUp", "Chest", lambda bm: loft(bm, torso_sections(1.15, 1.32, G + 0.004), 8), top_mat),
             part("swNeck", "Neck", lambda bm: loft(bm, [((0, 0.005, 1.30), 0.055, 0.05),
                                                         ((0, 0.01, 1.40), 0.045, 0.045)], 6), top_mat)]
    for sx in (1, -1):
        parts += sleeve(sx, 0.60)
    return join("Top_Sweater", parts)


def bottom_skirt():
    return join("Bottom_Skirt", [part("skirt", "Hips", lambda bm: loft(
        bm, [((0, 0, 0.99), 0.075 + G + 0.004, 0.055 + G + 0.004), ((0, 0, 0.90), 0.138, 0.10),
             ((0, 0.01, 0.64), 0.20, 0.165)], 8, cap_start=False, cap_end=False), bottom_mat)])


def bottom_pants():
    parts = [part("pantsHip", "Hips", lambda bm: loft(bm, torso_sections(0.76, 0.99, G), 8), bottom_mat)]
    for side, sx in sides():
        parts.append(part(f"pantsU{side}", f"UpperLeg.{side}", lambda bm, sx=sx: loft(
            bm, [((0.064 * sx, 0, 0.80), 0.054 + G, 0.054 + G), ((0.065 * sx, 0, 0.45), 0.040 + G, 0.040 + G)], 6), bottom_mat))
        parts.append(part(f"pantsL{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
            bm, [((0.07 * sx, 0, 0.45), 0.043 + G, 0.043 + G), ((0.07 * sx, 0, 0.10), 0.040 + G, 0.040 + G)], 6), bottom_mat))
    return join("Bottom_Pants", parts)


def shoes(name, top_z):
    parts = []
    for side, sx in sides():
        parts.append(part(f"{name}F{side}", f"Foot.{side}", lambda bm, sx=sx: box(
            bm, (0.07 * sx - 0.04, -0.135, -0.005), (0.07 * sx + 0.04, 0.04, 0.085)), shoe_mat))
        if top_z > 0.1:
            parts.append(part(f"{name}L{side}", f"LowerLeg.{side}", lambda bm, sx=sx: loft(
                bm, [((0.07 * sx, 0, 0.08), 0.040, 0.040), ((0.07 * sx, 0.008, top_z), 0.050 + G, 0.050 + G)],
                6), shoe_mat))
    return join(name, parts)


clothes = [top_tee(), top_sweater(), bottom_skirt(), bottom_pants(),
           shoes("Shoes_Sneakers", 0.0), shoes("Shoes_Boots", 0.38)]

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

def preview(name, shown):
    for obj in rig.children:
        obj.hide_render = obj.name not in shown
    out = os.path.join(PREVIEW_DIR, name)
    scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print("PREVIEW", out)


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
scene.render.resolution_x, scene.render.resolution_y = 600, 900
scene.render.film_transparent = False
world = bpy.data.worlds.new("W")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.32, 0.45, 1)
scene.world = world

cam_data = bpy.data.cameras.new("Cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 1.9
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

outfit = {"Skin_Body", "Face", "Hair_Long", "Top_Sweater", "Bottom_Skirt", "Shoes_Boots"}
fitted = {"Skin_Body", "Face", "Hair_Bob", "Top_Tee", "Bottom_Pants", "Shoes_Sneakers"}
cam.location = (0, -4, 0.85)
cam.rotation_euler = (math.radians(90), 0, 0)
preview("dreamer_fitted.png", fitted)
for label, angle in (("front", 0), ("threequarter", 35), ("side", 90)):
    a = math.radians(angle)
    cam.location = (math.sin(a) * -4, -math.cos(a) * 4, 0.85)
    cam.rotation_euler = (math.radians(90), 0, math.radians(-angle))
    preview(f"dreamer_{label}.png", outfit)

cam.location = (0, -4, 1.48)
cam.rotation_euler = (math.radians(90), 0, 0)
cam_data.ortho_scale = 0.5
preview("dreamer_face.png", {"Skin_Body", "Face", "Hair_Bob"})

print("DONE", len(rig.children), "meshes")
