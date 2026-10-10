"""
Original building drafts for dreamWorld, made from code in the same low-poly retro style as the critters
(build_critters.py): boxes, prisms, stretched spheres and tubes, colored per vertex, flat shaded.
Inspired by her Pinterest boards: nature reclaiming old tech, greenhouses full of CRTs, stilt houses,
mossy stone bridges, cozy cluttered shops, bugs everywhere. Moss green + plum + olive, warm lamplight.

Every building has a real doorway on its front (+Y) and a hollow inside with a floor, so it can be
walked into later. Each one is two objects: <Name> (walls, floor, inside) and <Name>_Roof, so the
cutaway render can lift the roof off.
  blender.exe -b --factory-startup --python ArtSource/build_buildings.py -- <preview dir>
Writes ArtSource/Buildings.blend and 3/4, front and cutaway renders of each building to <preview dir>.
"""
import colorsys
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(ROOT, "Design", "Buildings")
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- materials (all read the vertex colors)


def vertex_material(name, metallic=0.0, roughness=0.85, glow=0.0, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    attr = nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if glow:
        links.new(attr.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = glow
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        try:
            mat.surface_render_method = "BLENDED"
        except AttributeError:
            mat.blend_method = "BLEND"
    return mat


MATS = [vertex_material("Matte"), vertex_material("Shiny", metallic=0.55, roughness=0.3),
        vertex_material("Glow", glow=1.3), vertex_material("Glass", roughness=0.1, alpha=0.22)]
M_MATTE, M_SHINY, M_GLOW, M_GLASS = 0, 1, 2, 3


def rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def jitter(col, amount=0.06, seed=0):
    """A color that varies a little per vertex, so big flat walls don't look like plastic."""
    def color(p):
        r = random.Random(int(p.x * 731 + p.y * 977 + p.z * 1301) + seed)
        k = 1.0 + (r.random() - 0.5) * 2 * amount
        return tuple(min(1.0, ch * k) for ch in col)
    return color


# palette pulled from her boards
MOSS, MOSS_DK, MOSS_LT = rgb("#5f7a33"), rgb("#3e5224"), rgb("#8fae4c")
PLUM, PLUM_DK, LILAC = rgb("#6b3d6e"), rgb("#43264a"), rgb("#a98bc9")
OLIVE, WOOD, WOOD_DK = rgb("#7d7a3c"), rgb("#7a5233"), rgb("#4a3222")
STONE, STONE_DK, CREAM = rgb("#8d8a80"), rgb("#5f5c56"), rgb("#e9dcc0")
BEIGE_PC, CRT_GREEN, LAMP = rgb("#cfc6aa"), rgb("#7dff9a"), rgb("#ffcf7a")
TEAL, TEAL_DK, WATER = rgb("#3f9a95"), rgb("#24605e"), rgb("#4fb6d6")
BLACK, WHITE, RED = rgb("#1b1820"), rgb("#f4efe6"), rgb("#c2453c")

# ---------------------------------------------------------------- builder


class Builder:
    """Collects parts into one bmesh, then makes a flat-shaded object."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.float_color.new("Col")

    def paint(self, faces, color, mat):
        for f in faces:
            f.material_index = mat
            for loop in f.loops:
                c = color(loop.vert.co) if callable(color) else color
                loop[self.col] = (*c, 1.0)
        return faces

    def _place(self, verts, center, scale, rot):
        m = Matrix.Translation(Vector(center)) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() \
            @ Matrix.Diagonal((*scale, 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        return list({f for v in verts for f in v.link_faces})

    def box(self, center, size, color, mat=M_MATTE, rot=(0, 0, 0)):
        verts = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        return self.paint(self._place(verts, center, size, rot), color, mat)

    def prism(self, center, radius, height, color, mat=M_MATTE, sides=8, top_radius=None, rot=(0, 0, 0)):
        """A cylinder (or a cone/frustum with top_radius), centered on `center`."""
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=sides, radius1=radius,
                                    radius2=radius if top_radius is None else top_radius, depth=height)
        return self.paint(self._place(res["verts"], center, (1, 1, 1), rot), color, mat)

    def blob(self, center, radii, color, mat=M_MATTE, rot=(0, 0, 0), seg=10, rings=7):
        verts = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=1.0)["verts"]
        return self.paint(self._place(verts, center, radii, rot), color, mat)

    def tube(self, points, radii, color, mat=M_MATTE, sides=5):
        pts = [Vector(p) for p in points]
        if isinstance(radii, (int, float)):
            radii = [radii] * len(pts)
        rings, prev = [], None
        for i, p in enumerate(pts):
            d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            side = d.cross(Vector((0, 0, 1)))
            if side.length < 0.1:
                side = d.cross(Vector((1, 0, 0)))
            side.normalize()
            if prev is not None and side.dot(prev) < 0:
                side = -side
            prev = side
            up = side.cross(d).normalized()
            rings.append([self.bm.verts.new(p + (side * math.cos(a) + up * math.sin(a)) * radii[i])
                          for a in (2 * math.pi * k / sides for k in range(sides))])
        faces = []
        for r0, r1 in zip(rings, rings[1:]):
            for k in range(sides):
                faces.append(self.bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k])))
        faces.append(self.bm.faces.new(rings[-1]))
        faces.append(self.bm.faces.new(list(reversed(rings[0]))))
        return self.paint(faces, color, mat)

    # ---- architecture

    def room(self, center, w, d, h, wall, floor, door_w=1.3, door_h=2.2, t=0.2, door_x=0.0, open_back=False):
        """Floor + four walls with a doorway in the front (+Y) wall. Returns the inside floor height."""
        cx, cy, cz = center
        self.box((cx, cy, cz + 0.05), (w, d, 0.1), floor)
        z = cz + 0.1 + h / 2
        if not open_back:
            self.box((cx, cy - d / 2 + t / 2, z), (w, t, h), wall)
        self.box((cx - w / 2 + t / 2, cy, z), (t, d, h), wall)
        self.box((cx + w / 2 - t / 2, cy, z), (t, d, h), wall)
        fy = cy + d / 2 - t / 2
        left_w = (w / 2 + door_x - door_w / 2)
        right_w = (w / 2 - door_x - door_w / 2)
        self.box((cx - w / 2 + left_w / 2, fy, z), (left_w, t, h), wall)
        self.box((cx + w / 2 - right_w / 2, fy, z), (right_w, t, h), wall)
        self.box((cx + door_x, fy, cz + 0.1 + door_h + (h - door_h) / 2), (door_w, t, h - door_h), wall)
        return cz + 0.1

    def ring_wall(self, center, radius, h, color, door_w=1.3, door_h=2.2, t=0.22, segments=16):
        """A round wall made of segments; the ones facing +Y leave a doorway (only a lintel above)."""
        cx, cy, cz = center
        seg_w = 2 * radius * math.sin(math.pi / segments) * 1.08
        for k in range(segments):
            a = 2 * math.pi * k / segments
            x, y = cx + math.cos(a) * radius, cy + math.sin(a) * radius
            rot = (0, 0, math.degrees(a) + 90)
            if abs(x - cx) < door_w / 2 + 0.05 and y > cy:
                self.box((x, y, cz + door_h + (h - door_h) / 2), (seg_w, t, h - door_h), color, rot=rot)
            else:
                self.box((x, y, cz + h / 2), (seg_w, t, h), color, rot=rot)

    def door(self, center, w=1.1, h=2.1, color=WOOD_DK, open_deg=70):
        """An open door leaf hinged on the left of the doorway, swung inward."""
        cx, cy, cz = center
        a = math.radians(open_deg)
        hx = cx - w / 2
        self.box((hx + math.cos(a) * w / 2, cy - math.sin(a) * w / 2 - 0.05, cz + h / 2), (w, 0.07, h), color,
                 rot=(0, 0, -open_deg))

    def lamp(self, center, color=LAMP, size=0.18):
        self.blob(center, (size, size, size * 1.2), color, M_GLOW, seg=8, rings=6)

    def rug(self, center, w, d, a, b):
        def stripes(p):
            return a if int((p.x + 50) / 0.25) % 2 == 0 else b
        self.box((center[0], center[1], center[2] + 0.015), (w, d, 0.03), stripes)

    def plant(self, center, size=1.0, leaf=MOSS, pot=RED):
        cx, cy, cz = center
        self.prism((cx, cy, cz + 0.18 * size), 0.18 * size, 0.36 * size, pot, sides=6, top_radius=0.22 * size)
        for k in range(5):
            a = k * 1.26
            self.blob((cx + math.cos(a) * 0.15 * size, cy + math.sin(a) * 0.15 * size, cz + (0.5 + 0.12 * (k % 2)) * size),
                      (0.16 * size, 0.16 * size, 0.24 * size), jitter(leaf, 0.12, k), rot=(20, 0, math.degrees(a)))

    def crt(self, center, size=0.6, case=BEIGE_PC, screen=CRT_GREEN, rot=0.0):
        """A chunky old CRT monitor with a glowing screen facing its local +Y."""
        cx, cy, cz = center
        self.box(center, (size, size * 0.9, size * 0.85), case, rot=(0, 0, rot))
        a = math.radians(rot)
        fwd = Vector((-math.sin(a), math.cos(a), 0))
        self.box(Vector(center) + fwd * size * 0.45, (size * 0.78, 0.03, size * 0.62), screen, M_GLOW, rot=(0, 0, rot))
        self.box(Vector(center) - fwd * size * 0.5 + Vector((0, 0, -size * 0.05)), (size * 0.6, size * 0.4, size * 0.6),
                 case, rot=(0, 0, rot))

    def vines(self, top, length, color=MOSS, n=3, seed=0, spread=0.6):
        r = random.Random(seed)
        for k in range(n):
            x = top[0] + (r.random() - 0.5) * spread
            pts = [Vector((x, top[1], top[2]))]
            for j in range(4):
                pts.append(pts[-1] + Vector(((r.random() - 0.5) * 0.25, 0.02, -length / 4)))
            self.tube(pts, [0.05, 0.045, 0.04, 0.035, 0.02], color, sides=4)
            for p in pts[1:]:
                self.blob(p + Vector((0.06, 0.03, 0)), (0.09, 0.03, 0.07), jitter(MOSS_LT, 0.15, k), seg=6, rings=4)

    def moss_cap(self, center, w, d, color=MOSS, seed=0, lumps=14, height=0.3):
        """Lumpy moss blanket over a flat top."""
        r = random.Random(seed)
        for k in range(lumps):
            x = center[0] + (r.random() - 0.5) * w
            y = center[1] + (r.random() - 0.5) * d
            s = 0.4 + r.random() * 0.5
            self.blob((x, y, center[2]), (s, s, height * (0.6 + r.random())), jitter(color, 0.12, k), seg=7, rings=5)

    def finish(self, location=(0, 0, 0)):
        me = bpy.data.meshes.new(self.name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in MATS:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.new(self.name, me)
        obj.location = location
        scene.collection.objects.link(obj)
        return obj


def ground(b, radius, color=MOSS_DK, seed=0):
    """A grassy disc for the building to sit on (part of the drafts, not the game)."""
    b.prism((0, 0, -0.06), radius, 0.12, jitter(color, 0.1, seed), sides=12)


# ---------------------------------------------------------------- 1. moss server cafe


def moss_server_cafe():
    b, roof = Builder("Bld_MossServerCafe"), Builder("Bld_MossServerCafe_Roof")
    ground(b, 6.2, seed=1)
    # a giant beige computer tower, sunk into a mossy mound and tipped back a little
    w, d, h = 4.0, 6.0, 5.6
    case = jitter(rgb("#d8cba4"), 0.05, 2)
    fz = b.room((0, 0, 0), w, d, h, case, jitter(rgb("#8c7a5f"), 0.08, 3), door_w=1.4, door_h=2.3, t=0.25)
    # front face: drive bays glowing like windows, a giant power button, vent slots
    # 5.25 inch drive bays stacked up the front (they glow like windows), a floppy slot, a giant power button
    for k, z in enumerate((4.9, 4.35, 3.8)):
        b.box((0, d / 2 + 0.02, z), (3.2, 0.06, 0.42), rgb("#bfb291"))
        b.box((0, d / 2 + 0.05, z), (2.9, 0.04, 0.16), [CRT_GREEN, LAMP, rgb("#9ad7ff")][k], M_GLOW)
    b.box((-0.7, d / 2 + 0.04, 3.2), (1.4, 0.05, 0.14), rgb("#2a2a2a"))
    b.box((0.9, d / 2 + 0.05, 3.2), (0.3, 0.05, 0.12), rgb("#ff5a4a"), M_GLOW)          # hard drive light
    b.prism((1.3, d / 2 + 0.06, 2.6), 0.32, 0.12, rgb("#5a5a5a"), sides=10, rot=(90, 0, 0))
    b.prism((1.3, d / 2 + 0.13, 2.6), 0.16, 0.05, CRT_GREEN, M_GLOW, sides=10, rot=(90, 0, 0))
    for k in range(4):
        b.box((-1.5 + k * 0.18, d / 2 + 0.03, 1.3), (0.08, 0.05, 1.3), rgb("#a99d7c"))
    b.box((0, d / 2 + 0.05, 0.35), (w, 0.1, 0.4), rgb("#a99d7c"))                       # plinth stripe
    b.door((0, d / 2 - 0.15, fz))
    # side windows are CRT screens set into the case
    for sx in (1, -1):
        for y in (-1.4, 1.2):
            b.box((sx * (w / 2 + 0.03), y, 2.6), (0.06, 1.2, 0.9), rgb("#3a3a3a"))
            b.box((sx * (w / 2 + 0.07), y, 2.6), (0.04, 1.0, 0.72), CRT_GREEN, M_GLOW)
            b.box((sx * (w / 2 + 0.03), y, 4.3), (0.05, 1.2, 0.08), rgb("#a99d7c"))       # vent lines
            b.box((sx * (w / 2 + 0.03), y, 4.5), (0.05, 1.2, 0.08), rgb("#a99d7c"))
    # the mound swallowing the back and sides
    for k, (x, y, s) in enumerate(((-2.3, -2.4, 1.8), (2.3, -2.6, 1.9), (0, -3.5, 2.2), (-2.4, 0.5, 1.3),
                                   (2.4, 1.2, 1.2), (-2.0, 2.6, 0.8))):
        b.blob((x, y, 0.3), (s, s, 1.1 * s), jitter(MOSS_DK, 0.1, k), seg=8, rings=6)
        b.blob((x * 0.9, y * 0.95, 0.3 + 0.8 * s), (s * 0.5, s * 0.5, 0.35 * s), jitter(MOSS_LT, 0.12, k), seg=7, rings=5)
    b.vines((-w / 2 - 0.05, 1.0, 4.8), 2.5, seed=21, n=2, spread=0.1)
    # inside: cafe counter made of old keyboards, stools, a rug, mugs, a hanging lamp, a fern
    b.box((0, -1.9, fz + 0.5), (2.6, 0.7, 1.0), jitter(WOOD, 0.06, 5))
    for k in range(5):
        b.box((-1.0 + k * 0.5, -1.65, fz + 1.03), (0.42, 0.18, 0.06), rgb("#d8d2be"))
    for x in (-1.0, 0.0, 1.0):
        b.prism((x, -0.9, fz + 0.35), 0.22, 0.08, PLUM, sides=8)
        b.prism((x, -0.9, fz + 0.15), 0.05, 0.4, WOOD_DK, sides=5)
    b.rug((0, 0.6, fz), 2.4, 1.6, PLUM, OLIVE)
    b.crt((-1.3, -2.3, fz + 1.4), 0.55)
    b.tube([(0, 0, fz + h), (0, 0, fz + h - 0.8)], 0.02, BLACK)
    b.lamp((0, 0, fz + h - 0.95))
    b.plant((1.4, 1.8, fz), 1.2)
    # roof: the top of the case, blanketed in moss, with vines spilling down the front
    roof.box((0, 0, fz + h + 0.1), (w + 0.1, d + 0.1, 0.2), case)
    roof.moss_cap((0, 0, fz + h + 0.25), w, d, seed=7, lumps=22)
    roof.blob((1.6, -1.2, fz + h + 0.7), (0.2, 0.2, 0.2), rgb("#c75b8a"), seg=6, rings=4)     # a flower
    roof.vines((-1.6, d / 2 + 0.08, fz + h + 0.2), 2.0, seed=3, n=4, spread=1.6)
    roof.vines((1.4, d / 2 + 0.08, fz + h + 0.2), 1.3, seed=4, n=2, spread=0.8)
    return b.finish(), roof.finish()


# ---------------------------------------------------------------- 2. beetle shell house


def beetle_shell_house():
    b, roof = Builder("Bld_BeetleShellHouse"), Builder("Bld_BeetleShellHouse_Roof")
    ground(b, 6.0, seed=2)

    def shell(p):
        # Orion: metallic green with a lengthwise magenta and teal streak, darker low down
        side = abs(p.x) / 3.6
        light = 0.25 + 0.55 * max(0.0, min(1.0, (p.z - 1.0) / 3.0))
        band = side + 0.05 * math.sin(p.y * 2.2)
        if band < 0.25 or band > 0.8:
            hue, sat = 0.34, 0.9
        elif band < 0.45:
            hue, sat = 0.88, 0.8
        else:
            hue, sat = 0.52, 0.85
        return colorsys.hsv_to_rgb(hue, sat, light)

    wall = jitter(rgb("#3b2c2a"), 0.06, 1)
    b.ring_wall((0, -0.3, 0), 3.0, 2.2, wall, door_w=1.3, door_h=2.1, segments=18)
    b.prism((0, -0.3, 0.05), 3.0, 0.1, jitter(rgb("#9c7b52"), 0.06, 2), sides=18)
    # the shell is the roof: a big domed elytra with a seam, overhanging the wall
    roof.blob((0, -0.5, 2.0), (3.6, 4.2, 2.4), shell, M_SHINY, seg=20, rings=12)
    roof.tube([(0, 3.4, 2.6), (0, 0, 4.4), (0, -4.0, 2.8)], 0.06, rgb("#1f6b34"), M_SHINY)
    # pronotum porch: a bronze shield over the doorway, with the mandible archway in front
    bronze = lambda p: colorsys.hsv_to_rgb(0.08, 0.65, 0.45 + 0.08 * p.z)
    b.blob((0, 2.9, 2.5), (1.9, 0.9, 0.55), bronze, M_SHINY, seg=14, rings=8)
    for sx in (1, -1):
        b.tube([(sx * 0.95, 3.2, 0.0), (sx * 1.2, 3.6, 1.2), (sx * 1.05, 4.0, 2.2), (sx * 0.55, 4.25, 2.9),
                (sx * 0.12, 4.3, 3.05)], [0.22, 0.2, 0.16, 0.12, 0.06], BLACK, M_SHINY, sides=7)
        b.blob((sx * 1.15, 3.85, 1.6), (0.12, 0.12, 0.12), BLACK, M_SHINY)                    # mandible tooth
        b.blob((sx * 1.55, 3.0, 2.3), (0.28, 0.28, 0.28), BLACK, M_SHINY)                     # eyes as round windows
        b.blob((sx * 1.62, 3.12, 2.33), (0.12, 0.08, 0.12), LAMP, M_GLOW)
        # antennae with clubbed tips holding lanterns
        b.tube([(sx * 1.0, 3.3, 2.8), (sx * 1.6, 4.1, 3.7), (sx * 2.1, 4.5, 3.9)], 0.06, BLACK)
        b.lamp((sx * 2.15, 4.55, 3.75), size=0.2)
        # stubby legs propping the shell up
        for y in (-2.6, -0.6, 1.4):
            b.tube([(sx * 3.1, y, 1.6), (sx * 3.9, y + 0.3, 1.2), (sx * 4.1, y + 0.4, 0.0)], [0.14, 0.12, 0.08], BLACK, M_SHINY)
    b.door((0, 2.62, 0.1), color=rgb("#5c3a22"))
    # inside: bed of moss, a bug-themed rug, a bookshelf, a jar lamp
    b.rug((0, 0.0, 0.1), 2.2, 1.6, PLUM, LILAC)
    b.box((-1.7, -1.6, 0.4), (1.1, 1.8, 0.6), jitter(MOSS, 0.1, 9))
    b.box((1.6, -1.8, 1.0), (0.35, 1.4, 2.0), WOOD)
    for k in range(4):
        b.box((1.5, -1.8, 0.4 + k * 0.45), (0.25, 1.2, 0.3), [RED, TEAL, OLIVE, LILAC][k])
    b.prism((0.0, -2.3, 0.7), 0.25, 0.5, rgb("#bfe6ef"), M_GLASS, sides=8)
    b.lamp((0.0, -2.3, 0.75), color=rgb("#c8ff7a"), size=0.12)
    return b.finish(), roof.finish()


# ---------------------------------------------------------------- 3. stilt watchtower


def stilt_watchtower():
    b, roof = Builder("Bld_StiltWatchtower"), Builder("Bld_StiltWatchtower_Roof")
    ground(b, 5.5, seed=3)
    post = jitter(WOOD_DK, 0.08, 1)
    plank = lambda p: tuple(ch * (0.85 + 0.15 * (int((p.x + 20) * 3) % 2)) for ch in WOOD)
    H = 4.2
    for sx in (1, -1):
        for sy in (1, -1):
            b.tube([(sx * 1.7, sy * 1.7, 0), (sx * 1.6, sy * 1.6, H)], 0.16, post, sides=6)
        b.tube([(sx * 1.65, -1.65, 1.4), (sx * 1.65, 1.65, 3.0)], 0.07, post)          # cross bracing
        b.tube([(sx * 1.65, 1.65, 1.4), (sx * 1.65, -1.65, 3.0)], 0.07, post)
    # platform with a balcony railing all round
    b.box((0, 0, H + 0.1), (4.4, 4.4, 0.2), plank)
    for k in range(12):
        t = -2.1 + k * 0.38
        for (x, y) in ((t, 2.1), (t, -2.1), (2.1, t), (-2.1, t)):
            if not (abs(x) < 0.5 and y > 2.0):
                b.tube([(x, y, H + 0.2), (x, y, H + 1.1)], 0.04, post, sides=4)
    for (a, c) in (((-2.1, 2.1), (-0.5, 2.1)), ((0.5, 2.1), (2.1, 2.1)), ((-2.1, -2.1), (2.1, -2.1)),
                   ((2.1, -2.1), (2.1, 2.1)), ((-2.1, -2.1), (-2.1, 2.1))):
        b.tube([(a[0], a[1], H + 1.1), (c[0], c[1], H + 1.1)], 0.06, post, sides=4)
    # the hut on top
    fz = b.room((0, -0.3, H + 0.2), 3.0, 2.6, 2.3, jitter(rgb("#6c5a7a"), 0.06, 4), plank, door_w=1.0, door_h=1.9)
    b.door((0, 0.85, fz), w=0.9, h=1.8, color=WOOD)
    for sx in (1, -1):
        b.box((sx * 1.52, -0.3, fz + 1.4), (0.06, 0.8, 0.6), LAMP, M_GLOW)          # warm windows
    # ladder up the front
    for sx in (1, -1):
        b.tube([(sx * 0.35, 3.3, 0), (sx * 0.35, 2.25, H + 0.3)], 0.06, post, sides=4)
    for k in range(10):
        t = (k + 0.5) / 10
        y, z = 3.3 + (2.25 - 3.3) * t, (H + 0.3) * t
        b.tube([(-0.35, y, z), (0.35, y, z)], 0.04, WOOD, sides=4)
    # inside: a tiny desk with a telescope, a stool, a rug
    b.box((0.8, -1.1, fz + 0.4), (1.0, 0.6, 0.8), WOOD)
    b.tube([(0.7, -1.0, fz + 0.9), (0.4, -1.3, fz + 1.2)], 0.08, rgb("#b08a3c"), M_SHINY)
    b.rug((-0.4, -0.3, fz), 1.4, 1.2, OLIVE, PLUM)
    # hanging lantern at the balcony corner and a pennant
    b.tube([(1.9, 1.9, H + 1.1), (1.9, 1.9, H + 0.75)], 0.015, BLACK)
    b.lamp((1.9, 1.9, H + 0.62), size=0.16)
    # roof: a steep shingled pyramid with a weathervane beetle
    roof.prism((0, -0.3, H + 2.6 + 0.9), 2.5, 1.8, jitter(PLUM_DK, 0.08, 5), sides=4, top_radius=0.0, rot=(0, 0, 45))
    roof.tube([(0, -0.3, H + 4.2), (0, -0.3, H + 4.8)], 0.04, BLACK)
    roof.blob((0, -0.3, H + 4.85), (0.18, 0.28, 0.1), rgb("#1f6b34"), M_SHINY)
    return b.finish(), roof.finish()


# ---------------------------------------------------------------- 4. greenhouse arcade


def greenhouse_arcade():
    b, roof = Builder("Bld_GreenhouseArcade"), Builder("Bld_GreenhouseArcade_Roof")
    ground(b, 6.4, seed=4)
    w, d, h = 6.0, 7.0, 2.8
    checker = lambda p: CREAM if (int((p.x + 50) / 0.6) + int((p.y + 50) / 0.6)) % 2 == 0 else rgb("#6a8f5c")
    b.box((0, 0, 0.05), (w, d, 0.1), checker)
    frame = rgb("#e7e3d8")
    glass = rgb("#8fd8de")
    # low brick knee wall, then glass walls on a white frame
    b.box((0, -d / 2 + 0.1, 0.4), (w, 0.2, 0.6), jitter(RED, 0.08, 1))
    for sx in (1, -1):
        b.box((sx * (w / 2 - 0.1), 0, 0.4), (0.2, d, 0.6), jitter(RED, 0.08, 2))
    for sx in (1, -1):
        b.box((sx * (w / 2 - 1.7), d / 2 - 0.1, 0.4), (2.6, 0.2, 0.6), jitter(RED, 0.08, 3))
    for x in range(-3, 4):
        b.box((x * 0.98, -d / 2 + 0.1, 0.7 + h / 2), (0.08, 0.1, h), frame)
    for k in range(8):
        y = -d / 2 + k * d / 7
        for sx in (1, -1):
            b.box((sx * (w / 2 - 0.1), y, 0.7 + h / 2), (0.1, 0.08, h), frame)
    for sx in (1, -1):
        b.box((sx * (w / 2 - 0.1), 0, 0.7 + h / 2), (0.04, d, h), glass, M_GLASS)
        b.box((sx * (w / 2 - 1.7), d / 2 - 0.1, 0.7 + h / 2), (2.6, 0.04, h), glass, M_GLASS)
    b.box((0, -d / 2 + 0.1, 0.7 + h / 2), (w, 0.04, h), glass, M_GLASS)
    b.box((0, d / 2 - 0.1, 0.1 + h + 0.3), (0.9, 0.12, 0.1), frame)                    # door header
    for sx in (1, -1):
        b.box((sx * 0.45, d / 2 - 0.1, 0.1 + (h + 0.6) / 2), (0.1, 0.12, h + 0.6), frame)
    # the back wall: a stack of CRT TVs all showing the same glowing sky
    for row in range(3):
        for col in range(6):
            x = -2.25 + col * 0.9
            screen = [CRT_GREEN, rgb("#9ad7ff"), rgb("#ff9ad0")][(row + col) % 3]
            b.crt((x + (row % 2) * 0.12, -d / 2 + 0.75, 0.55 + row * 0.75), 0.7, case=rgb("#6c6c70"), screen=screen)
    # plants everywhere: big potted palms, hanging ferns, a planter bed down the middle
    for (x, y, s) in ((-2.3, 2.4, 1.6), (2.3, 2.4, 1.4), (-2.4, -0.4, 1.8), (2.4, 0.3, 1.5), (0.0, 1.4, 1.1)):
        b.plant((x, y, 0.1), s, leaf=MOSS if x < 0 else MOSS_LT)
    for (x, y) in ((-1.2, -0.6), (1.2, 0.6)):
        b.tube([(x, y, 0.1 + h + 0.7), (x, y, 0.1 + h - 0.2)], 0.015, BLACK)
        b.blob((x, y, 0.1 + h - 0.45), (0.4, 0.4, 0.3), jitter(MOSS_LT, 0.15, 6), seg=7, rings=5)
    # an arcade desk with a beige computer and a green chair
    b.box((0.6, -1.4, 0.5), (1.6, 0.7, 0.8), WOOD_DK)
    b.crt((0.6, -1.5, 1.2), 0.55)
    b.box((0.6, -0.7, 0.4), (0.6, 0.6, 0.6), jitter(rgb("#5f8f4a"), 0.08, 8))
    # roof: glass gable with ribs and a wood-paneled ceiling strip down the ridge
    top = 0.7 + h
    slope = math.tan(math.radians(24))
    for sx in (1, -1):
        zc = top + (w / 4) * slope
        roof.box((sx * w / 4, 0, zc), (w / 2 / math.cos(math.radians(24)) + 0.2, d + 0.1, 0.04), glass, M_GLASS,
                 rot=(0, sx * 24, 0))
        for k in range(8):
            y = -d / 2 + k * d / 7
            roof.box((sx * w / 4, y, zc + 0.05), (w / 2 / math.cos(math.radians(24)) + 0.2, 0.08, 0.1), frame,
                     rot=(0, sx * 24, 0))
    roof.box((0, 0, top + (w / 2) * slope + 0.05), (0.4, d + 0.2, 0.16), WOOD)
    for y in (-d / 2 + 0.1, d / 2 - 0.1):   # gable ends (glass triangles approximated by framed panes)
        for k in range(5):
            x = -w / 2 + 0.6 + k * (w - 1.2) / 4
            hz = (w / 2 - abs(x)) * slope
            roof.box((x, y, top + hz / 2), (0.06, 0.08, hz), frame)
    return b.finish(), roof.finish()


# ---------------------------------------------------------------- 5. tide pool aquarium shop


def aquarium_shop():
    b, roof = Builder("Bld_AquariumShop"), Builder("Bld_AquariumShop_Roof")
    ground(b, 6.0, seed=5)
    w, d, h = 6.0, 5.0, 3.0
    tile = lambda p: TEAL if (int((p.x + 50) / 0.4) + int((p.z + 50) / 0.4)) % 2 == 0 else TEAL_DK
    fz = b.room((0, 0, 0), w, d, h, tile, jitter(rgb("#c7b893"), 0.05, 1), door_w=1.3, door_h=2.3)
    b.door((0, d / 2 - 0.12, fz), color=rgb("#2e5c78"))

    def tank(center, size):
        cx, cy, cz = center
        b.box(center, (size[0] + 0.12, size[1] + 0.12, size[2] + 0.12), rgb("#202a33"))
        b.box((cx, cy + size[1] / 2 + 0.05, cz), (size[0], 0.04, size[2]), WATER, M_GLOW)
        r = random.Random(int(cx * 10 + cz * 7))
        for k in range(4):   # fish swimming in front of the glow
            fx, fz_ = cx + (r.random() - 0.5) * size[0] * 0.7, cz + (r.random() - 0.5) * size[2] * 0.6
            col = [rgb("#ff8a3d"), rgb("#ffd34d"), rgb("#ff6fb0"), WHITE][k]
            b.blob((fx, cy + size[1] / 2 + 0.1, fz_), (0.16, 0.05, 0.1), col, seg=6, rings=4)
            b.blob((fx - 0.16, cy + size[1] / 2 + 0.1, fz_), (0.06, 0.03, 0.08), col, seg=4, rings=3)
        for k in range(3):   # seaweed
            x = cx - size[0] * 0.35 + k * size[0] * 0.35
            b.tube([(x, cy + size[1] / 2 + 0.08, cz - size[2] / 2), (x + 0.05, cy + size[1] / 2 + 0.08, cz)], 0.04, MOSS_LT, sides=4)

    # the shop windows ARE fish tanks
    for sx in (1, -1):
        tank((sx * 1.9, d / 2 + 0.02, 1.4), (1.5, 0.3, 1.2))
    # inside: tanks along the side walls, a counter shaped like a clam, a shell rug
    for y in (-1.3, 0.3):
        for sx in (1, -1):
            b.box((sx * (w / 2 - 0.45), y, 0.5), (0.5, 1.3, 0.8), WOOD_DK)
            b.box((sx * (w / 2 - 0.45), y, 1.35), (0.5, 1.3, 0.9), WATER, M_GLOW)
    b.blob((0, -1.5, 0.6), (1.2, 0.5, 0.45), rgb("#f2c6d6"), seg=12, rings=6)
    b.blob((0, -1.7, 1.15), (1.1, 0.25, 0.5), rgb("#e8a9c4"), seg=12, rings=6, rot=(-35, 0, 0))
    b.rug((0, 0.6, fz), 2.0, 1.3, rgb("#f0b45a"), CREAM)
    b.lamp((0, 0, fz + h - 0.4), color=rgb("#9ae6ff"))
    # roof: flat with a striped awning over the door and a giant fish sign riding on top
    roof.box((0, 0, fz + h + 0.1), (w + 0.3, d + 0.3, 0.2), TEAL_DK)
    stripe = lambda p: WHITE if int((p.x + 50) / 0.45) % 2 == 0 else rgb("#e0646b")
    roof.box((0, d / 2 + 0.55, fz + 2.55), (3.2, 1.1, 0.08), stripe, rot=(-18, 0, 0))
    fish = rgb("#ff8a3d")
    roof.blob((0, 0, fz + h + 1.3), (1.6, 0.35, 0.8), fish, seg=12, rings=8)
    roof.blob((-1.9, 0, fz + h + 1.3), (0.5, 0.2, 0.7), fish, rot=(0, 0, 0), seg=6, rings=5)
    roof.blob((1.05, 0.33, fz + h + 1.45), (0.18, 0.06, 0.18), WHITE)
    roof.blob((1.1, 0.38, fz + h + 1.45), (0.08, 0.04, 0.08), BLACK)
    roof.tube([(0, 0, fz + h + 0.2), (0, 0, fz + h + 0.6)], 0.08, BLACK)
    return b.finish(), roof.finish()


# ---------------------------------------------------------------- 6. mushroom bakery


def mushroom_bakery():
    b, roof = Builder("Bld_MushroomBakery"), Builder("Bld_MushroomBakery_Roof")
    ground(b, 5.8, seed=6)
    stem = jitter(CREAM, 0.05, 1)
    b.ring_wall((0, 0, 0), 2.6, 2.6, stem, door_w=1.2, door_h=2.1, segments=18)
    b.prism((0, 0, 0.05), 2.6, 0.1, lambda p: rgb("#7a4b38") if (int((p.x + 50) / 0.5) + int((p.y + 50) / 0.5)) % 2 else rgb("#9b6b4c"),
            sides=18)
    b.door((0, 2.45, 0.1), color=rgb("#8a4f2a"))
    # round windows glowing warm
    for a in (30, 150, 250, 290):
        x, y = math.cos(math.radians(a)) * 2.66, math.sin(math.radians(a)) * 2.66
        b.prism((x, y, 1.6), 0.38, 0.12, LAMP, M_GLOW, sides=8, rot=(90, 0, a + 90))
    # sign: a loaf of bread held by a tiny skull chef
    b.tube([(1.3, 2.6, 2.4), (1.9, 3.1, 2.4)], 0.05, BLACK)
    b.box((1.9, 3.15, 2.0), (0.9, 0.08, 0.6), WOOD)
    b.blob((1.75, 3.22, 2.0), (0.32, 0.12, 0.18), rgb("#d99a4c"))
    b.blob((2.15, 3.22, 2.08), (0.13, 0.1, 0.14), WHITE)                     # skull chef
    b.blob((2.15, 3.22, 2.25), (0.14, 0.1, 0.08), WHITE)                     # chef hat
    for sx in (-0.045, 0.045):
        b.blob((2.15 + sx, 3.31, 2.09), (0.03, 0.02, 0.035), BLACK)
    # inside: a stone oven glowing, a counter of bread loaves, shelves, a rug
    b.box((0, -1.8, 0.9), (1.6, 0.9, 1.6), jitter(STONE, 0.1, 4))
    b.box((0, -1.32, 0.7), (0.8, 0.05, 0.5), rgb("#ff7a2e"), M_GLOW)
    b.box((1.0, 0.0, 0.5), (0.7, 1.8, 0.8), WOOD)
    for k in range(5):
        b.blob((1.0, -0.7 + k * 0.35, 0.98), (0.14, 0.2, 0.1), rgb("#d99a4c"), seg=6, rings=4)
    b.rug((-0.6, 0.6, 0.1), 1.6, 1.4, PLUM, CREAM)
    b.lamp((0, 0, 2.3), size=0.16)
    # roof: a giant plum mushroom cap with cream spots, a gill skirt, and a chimney puffing smoke
    roof.blob((0, 0, 2.9), (3.8, 3.8, 1.9), jitter(rgb("#8a3d6b"), 0.06, 7), seg=18, rings=10)
    roof.prism((0, 0, 2.75), 3.6, 0.15, jitter(rgb("#e6cba6"), 0.05, 8), sides=18)
    r = random.Random(3)
    for k in range(11):
        a = r.random() * 2 * math.pi
        rad = 0.5 + r.random() * 2.6
        z = 2.9 + 1.9 * math.sqrt(max(0.0, 1 - (rad / 3.8) ** 2))
        roof.blob((math.cos(a) * rad, math.sin(a) * rad, z), (0.4, 0.4, 0.12), CREAM, seg=8, rings=4)
    roof.prism((1.6, -0.8, 4.6), 0.3, 1.4, jitter(STONE_DK, 0.1, 9), sides=6)
    for k in range(3):
        roof.blob((1.6 + k * 0.2, -0.8, 5.5 + k * 0.45), (0.3 + k * 0.08,) * 3, rgb("#d9d3dd"), seg=7, rings=5)
    return b.finish(), roof.finish()


# ---------------------------------------------------------------- 7. circuit grove shrine


def circuit_grove_shrine():
    b, roof = Builder("Bld_CircuitGroveShrine"), Builder("Bld_CircuitGroveShrine_Roof")
    ground(b, 5.5, seed=7)

    def pcb(p):
        trace = (abs((p.x * 2.5) % 1.0 - 0.5) < 0.06) or (abs((p.y * 2.5) % 1.0 - 0.5) < 0.06)
        return rgb("#d6b04a") if trace and p.z > 0.55 else jitter(rgb("#2f6b3a"), 0.06, 1)(p)

    b.box((0, 0, 0.3), (8.0, 8.0, 0.6), pcb)                                        # the board
    b.box((0, 0, 0.62), (4.0, 4.0, 0.06), jitter(rgb("#2a2a2e"), 0.05, 2))           # shrine floor: a big chip
    for k in range(10):
        t = -1.8 + k * 0.4
        for (x, y) in ((t, 2.15), (t, -2.15), (2.15, t), (-2.15, t)):
            b.box((x, y, 0.62), (0.12, 0.12, 0.06), rgb("#c9c9cf"), M_SHINY)          # chip legs
    # capacitors and resistors scattered on the board, a few little trees growing between them
    r = random.Random(5)
    for k in range(10):
        a, rad = r.random() * 2 * math.pi, 2.6 + r.random() * 1.0
        x, y = math.cos(a) * rad, math.sin(a) * rad
        if k % 2:
            b.prism((x, y, 0.95), 0.22, 0.7, rgb("#3b5fae"), sides=8)
            b.prism((x, y, 1.31), 0.22, 0.02, rgb("#c9c9cf"), M_SHINY, sides=8)
        else:
            b.prism((x, y, 0.75), 0.1, 0.5, rgb("#d9c08a"), sides=6, rot=(90, 0, math.degrees(a)))
    for k in range(7):
        a, rad = r.random() * 2 * math.pi, 2.7 + r.random() * 1.1
        x, y = math.cos(a) * rad, math.sin(a) * rad
        s = 0.7 + r.random() * 0.6
        b.tube([(x, y, 0.6), (x, y, 0.6 + 0.6 * s)], 0.07 * s, WOOD_DK)
        b.prism((x, y, 0.6 + 1.1 * s), 0.45 * s, 1.1 * s, jitter(MOSS, 0.12, k), sides=6, top_radius=0.0)
        b.prism((x, y, 0.6 + 1.6 * s), 0.32 * s, 0.8 * s, jitter(MOSS_LT, 0.12, k), sides=6, top_radius=0.0)
    # the shrine: four ram-stick pillars and a torii-style double beam; inside, a glowing core on a pedestal
    for sx in (1, -1):
        for sy in (1, -1):
            b.box((sx * 1.6, sy * 1.6, 2.0), (0.5, 0.14, 2.8), rgb("#2b4f2e"))
            b.box((sx * 1.6, sy * 1.6 + 0.08, 2.0), (0.42, 0.02, 2.4), rgb("#d6b04a"), M_SHINY)
        b.box((sx * 1.6, 0, 0.95), (0.12, 3.2, 0.6), jitter(rgb("#2b4f2e"), 0.05, 3))     # low side rails
    b.box((0, 0, 1.0), (0.7, 0.7, 0.7), rgb("#2a2a2e"))
    b.blob((0, 0, 1.75), (0.38, 0.38, 0.38), rgb("#a6ff9e"), M_GLOW, seg=10, rings=7)
    b.rug((0, 1.0, 0.65), 1.2, 1.2, PLUM, OLIVE)
    roof.box((0, 0, 3.55), (4.6, 4.0, 0.25), rgb("#7a2f3f"))
    roof.box((0, 0, 3.85), (5.4, 4.6, 0.22), rgb("#7a2f3f"), rot=(0, 0, 0))
    roof.box((0, 2.0, 3.2), (3.6, 0.2, 0.25), rgb("#2a2a2e"))
    roof.moss_cap((0, 0, 4.0), 4.6, 4.0, seed=11, lumps=9, height=0.2)
    return b.finish(), roof.finish()


# ---------------------------------------------------------------- 8. mossy arch bridge (bonus)


def mossy_arch_bridge():
    b, roof = Builder("Bld_MossyArchBridge"), Builder("Bld_MossyArchBridge_Roof")
    # a pond under it, with lily pads, and autumn flowers on the banks
    b.box((0, 0, -0.1), (5.0, 12.0, 0.1), jitter(rgb("#2f6f86"), 0.08, 1), M_SHINY)
    for sx in (1, -1):
        b.box((sx * 4.0, 0, 0.0), (3.2, 12.0, 0.4), jitter(MOSS_DK, 0.1, 2))
    r = random.Random(8)
    for k in range(9):
        x, y = (r.random() - 0.5) * 4.0, (r.random() - 0.5) * 10.0
        b.prism((x, y, -0.03), 0.3 + r.random() * 0.25, 0.04, jitter(MOSS, 0.15, k), sides=7)
    for k in range(40):
        sx = 1 if k % 2 else -1
        x, y = sx * (2.7 + r.random() * 2.4), (r.random() - 0.5) * 11
        col = [rgb("#e0653a"), rgb("#f0b236"), rgb("#c74a6a"), rgb("#e88a3a")][k % 4]
        b.tube([(x, y, 0.2), (x, y, 0.5 + r.random() * 0.4)], 0.02, MOSS_DK, sides=3)
        b.blob((x, y, 0.55 + r.random() * 0.4), (0.12, 0.12, 0.09), col, seg=6, rings=4)
    # the arch: stone blocks along a curve, two spans side by side like the pin, with moss on top
    stone = jitter(STONE, 0.12, 4)
    span, rise = 5.6, 2.2
    for k in range(15):
        t = k / 14
        x = -span / 2 + span * t
        z = rise * math.sin(math.pi * t)
        ang = math.degrees(math.atan2(rise * math.pi * math.cos(math.pi * t), span))
        b.box((x, 0, z + 0.3), (0.5, 3.0, 0.6), stone, rot=(0, -ang, 0))
        b.box((x, 0, z + 0.7), (0.52, 3.0, 0.2), jitter(STONE_DK, 0.1, 6), rot=(0, -ang, 0))
    for sx in (1, -1):
        b.box((sx * 3.3, 0, 0.6), (1.4, 3.2, 1.4), stone)
        for k in range(9):          # low parapet walls along both sides
            t = k / 8
            x = -span / 2 + span * t
            z = rise * math.sin(math.pi * t) + 1.0
            b.box((x, sx * 1.4, z), (0.62, 0.25, 0.45), stone)
    b.moss_cap((0, 0, rise + 0.95), 4.0, 2.6, seed=12, lumps=12, height=0.15)
    b.vines((-0.6, 1.55, rise + 0.7), 1.8, seed=13, n=3, spread=1.5)
    # the "interior" here is the walkway itself; a lantern post at each end
    for sx in (1, -1):
        b.tube([(sx * 3.3, 1.2, 1.3), (sx * 3.3, 1.2, 2.8)], 0.06, BLACK, sides=5)
        b.lamp((sx * 3.3, 1.2, 2.95), size=0.2)
    roof.blob((0, -3.0, 0.1), (0.25, 0.4, 0.1), rgb("#1f6b34"), M_SHINY)   # a beetle on the bank (tiny roof object)
    return b.finish(), roof.finish()


BUILDINGS = [
    ("moss server cafe", moss_server_cafe),
    ("beetle shell house", beetle_shell_house),
    ("stilt watchtower", stilt_watchtower),
    ("greenhouse arcade", greenhouse_arcade),
    ("tide pool aquarium shop", aquarium_shop),
    ("mushroom bakery", mushroom_bakery),
    ("circuit grove shrine", circuit_grove_shrine),
    ("mossy arch bridge", mossy_arch_bridge),
]

made = []
for i, (label, make) in enumerate(BUILDINGS):
    body, roof = make()
    made.append((label, body, roof))
for i, (_, body, roof) in enumerate(made):      # spread them out in the .blend
    body.location.x = roof.location.x = (i % 4) * 16.0
    body.location.y = roof.location.y = -(i // 4) * 16.0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "Buildings.blend"))

# ---------------------------------------------------------------- preview renders

for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        pass
scene.render.resolution_x, scene.render.resolution_y = 600, 600
scene.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("W")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.2, 0.15, 0.3, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.1
scene.world = world
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.2
sun.rotation_euler = (math.radians(50), 0, math.radians(35))
scene.collection.objects.link(sun)
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.type = "ORTHO"
scene.collection.objects.link(cam)
scene.camera = cam

for label, body, roof in made:
    for _, ob, rf in made:
        ob.hide_render = rf.hide_render = ob is not body
    center = body.location + Vector((0, 0, 2.2))
    for view, yaw, pitch, scale, show_roof in (("q", 35, 22, 15, True), ("front", 0, 6, 14, True),
                                               ("cutaway", 30, 58, 15, False)):
        roof.hide_render = not show_roof
        a, p = math.radians(yaw), math.radians(pitch)
        cam.location = center + Vector((math.sin(a) * math.cos(p), math.cos(a) * math.cos(p), math.sin(p))) * 40
        cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = scale
        cam.data.clip_end = 200
        scene.render.filepath = os.path.join(OUT, f"{body.name}_{view}.png")
        bpy.ops.render.render(write_still=True)
print("buildings:", ", ".join(b.name for _, b, _ in made))
