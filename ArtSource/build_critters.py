"""
Low-poly retro critters for dreamWorld, built from code like the character (build_dreamer.py).
Every part is a stretched sphere or a tapered tube, colored per vertex, flat shaded.
  blender.exe -b --factory-startup --python ArtSource/build_critters.py -- <preview dir>
Writes ArtSource/Critters.blend and a 3/4 + side render of each critter to <preview dir>.
"""
import colorsys
import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(ROOT, "Design", "Critters")
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- materials (all read the vertex colors)


def vertex_material(name, metallic=0.0, roughness=0.8, glow=0.0):
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
    return mat


MATTE = vertex_material("Matte")
SHINY = vertex_material("Shiny", metallic=0.55, roughness=0.3)   # beetle shells
GLOW = vertex_material("Glow", glow=4.0)                         # firefly lantern
MATS = [MATTE, SHINY, GLOW]
M_MATTE, M_SHINY, M_GLOW = 0, 1, 2


def rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


# ---------------------------------------------------------------- shape helpers


class Critter:
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

    def blob(self, center, radii, color, mat=M_MATTE, rot=(0, 0, 0), seg=10, rings=7):
        """A stretched sphere: bodies, heads, eyes, spots."""
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=1.0)
        verts = res["verts"]
        m = Matrix.Translation(Vector(center)) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() \
            @ Matrix.Diagonal((*radii, 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self.paint(faces, color, mat)
        return faces

    def tube(self, points, radii, color, mat=M_MATTE, sides=5):
        """A tapered tube through points: legs, antennae, mandibles, tails."""
        pts = [Vector(p) for p in points]
        if isinstance(radii, (int, float)):
            radii = [radii] * len(pts)
        rings = []
        prev_side = None
        for i, p in enumerate(pts):
            d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            side = d.cross(Vector((0, 0, 1)))
            if side.length < 0.1:
                side = d.cross(Vector((1, 0, 0)))
            side.normalize()
            if prev_side is not None and side.dot(prev_side) < 0:
                side = -side    # don't let the tube twist inside out
            prev_side = side
            up = side.cross(d).normalized()
            rings.append([self.bm.verts.new(p + (side * math.cos(a) + up * math.sin(a)) * radii[i])
                          for a in (2 * math.pi * k / sides for k in range(sides))])
        faces = []
        for r0, r1 in zip(rings, rings[1:]):
            for k in range(sides):
                faces.append(self.bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k])))
        faces.append(self.bm.faces.new(rings[-1]))
        faces.append(self.bm.faces.new(list(reversed(rings[0]))))
        self.paint(faces, color, mat)
        return faces

    def legs(self, mounts, color, mat=M_MATTE, spread=1.0, lift=0.0, thick=0.022):
        """Mirrored jointed legs. Each mount: (y, x0, z0, knee(dx, dy, dz), foot(dx, dy, dz))."""
        for y, x0, z0, knee, foot in mounts:
            for sx in (1, -1):
                hip = Vector((sx * x0, y, z0))
                k = hip + Vector((sx * knee[0] * spread, knee[1], knee[2] + lift))
                f = k + Vector((sx * foot[0] * spread, foot[1], foot[2]))
                self.tube([hip, k, f], [thick, thick * 0.8, thick * 0.45], color, mat)

    def finish(self, location=(0, 0, 0)):
        me = bpy.data.meshes.new(self.name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in MATS:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = False      # flat shading reads as PS1
        obj = bpy.data.objects.new(self.name, me)
        obj.location = location
        scene.collection.objects.link(obj)
        return obj


# ---------------------------------------------------------------- the critters (length ~1, head toward +Y)


def rainbow_stag_beetle():
    """Like Orion: a metallic shell that shifts green -> gold -> red -> purple toward the head."""
    c = Critter("Critter_RainbowStagBeetle")

    def shell(p):
        t = min(1.0, max(0.0, (p.y + 0.5) / 1.1))
        hue = (0.36 - 0.52 * t) % 1.0
        seam = abs(p.x) < 0.018 and p.y < 0.12
        r, g, b = colorsys.hsv_to_rgb(hue, 0.9, 0.55 if seam else 0.95)
        return (r, g, b)

    def leg_color(p):
        r, g, b = colorsys.hsv_to_rgb((0.36 - 0.52 * min(1.0, max(0.0, (p.y + 0.5) / 1.1))) % 1.0, 0.7, 0.45)
        return (r, g, b)

    c.blob((0, -0.12, 0.17), (0.25, 0.36, 0.14), shell, M_SHINY, seg=14, rings=8)      # wing covers
    c.blob((0, 0.25, 0.18), (0.24, 0.13, 0.1), shell, M_SHINY, seg=12)                  # wide pronotum, snug to the shell
    c.blob((0, 0.39, 0.16), (0.19, 0.09, 0.075), shell, M_SHINY)                        # broad flat head
    for sx in (1, -1):
        # big pincer mandibles: out, forward, curling up and in until the tips nearly touch
        c.tube([(sx * 0.1, 0.44, 0.16), (sx * 0.2, 0.55, 0.2), (sx * 0.19, 0.67, 0.27), (sx * 0.1, 0.75, 0.33),
                (sx * 0.025, 0.76, 0.35)], [0.045, 0.04, 0.032, 0.022, 0.01], shell, M_SHINY, sides=6)
        c.tube([(sx * 0.19, 0.6, 0.23), (sx * 0.12, 0.62, 0.26)], [0.018, 0.006], shell, M_SHINY)   # inner tooth
        c.blob((sx * 0.17, 0.42, 0.18), (0.035, 0.035, 0.035), (0.04, 0.02, 0.05))                # eyes
        c.tube([(sx * 0.15, 0.45, 0.19), (sx * 0.25, 0.52, 0.24), (sx * 0.29, 0.56, 0.25)], 0.012, leg_color)
        c.blob((sx * 0.3, 0.57, 0.25), (0.025, 0.035, 0.02), leg_color)                            # antenna club
    c.legs([(0.22, 0.15, 0.1, (0.17, 0.08, 0.03), (0.1, 0.1, -0.13)),
            (0.0, 0.18, 0.1, (0.19, 0.0, 0.04), (0.11, -0.02, -0.14)),
            (-0.2, 0.18, 0.1, (0.17, -0.08, 0.04), (0.1, -0.13, -0.14))], leg_color, M_SHINY, thick=0.034)
    return c.finish()


def praying_mantis(name="Critter_Mantis", body="#7bc96f", light="#b6e6a0", eye="#d9f2a8", orchid=False):
    c = Critter(name)
    body, light, eye = rgb(body), rgb(light), rgb(eye)
    c.blob((0, -0.36, 0.24), (0.1, 0.32, 0.09), body, rot=(-6, 0, 0))                   # abdomen
    c.blob((0, -0.3, 0.32), (0.12, 0.34, 0.025), light, rot=(-6, 0, 0))                 # folded wings
    c.tube([(0, -0.04, 0.24), (0, 0.18, 0.38), (0, 0.34, 0.54)], [0.06, 0.045, 0.04], body)   # long neck-thorax
    c.blob((0, 0.38, 0.6), (0.1, 0.055, 0.075), body, rot=(20, 0, 0))                   # triangle-ish head
    for sx in (1, -1):
        c.blob((sx * 0.1, 0.39, 0.64), (0.045, 0.045, 0.05), eye)                       # big eyes
        c.blob((sx * 0.115, 0.42, 0.65), (0.015, 0.015, 0.018), (0.1, 0.1, 0.12))       # pupils
        c.tube([(sx * 0.03, 0.42, 0.67), (sx * 0.12, 0.6, 0.82), (sx * 0.22, 0.66, 0.86)], 0.006, body)
        # folded grabbing forelegs
        fore = [(sx * 0.05, 0.3, 0.5), (sx * 0.1, 0.42, 0.38), (sx * 0.08, 0.52, 0.52), (sx * 0.06, 0.46, 0.4)]
        c.tube(fore, [0.03, 0.028, 0.02, 0.012], body)
        if orchid:   # orchid mantis: petal-shaped flaps on the legs
            c.blob((sx * 0.2, -0.05, 0.3), (0.12, 0.07, 0.015), light, rot=(0, sx * 20, sx * 30))
            c.blob((sx * 0.2, -0.3, 0.3), (0.13, 0.08, 0.015), light, rot=(0, sx * 20, -sx * 30))
            c.blob((sx * 0.13, 0.45, 0.45), (0.05, 0.08, 0.012), light, rot=(30, 0, 0))
    c.legs([(0.02, 0.05, 0.22, (0.25, 0.1, 0.12), (0.14, 0.08, -0.34)),
            (-0.12, 0.06, 0.22, (0.26, -0.15, 0.1), (0.16, -0.22, -0.32))], body, thick=0.02)
    return c.finish()


def ladybug():
    c = Critter("Critter_Ladybug")
    red, black = rgb("#e23a3f"), rgb("#1b1420")
    c.blob((0, -0.05, 0.14), (0.32, 0.38, 0.22), red, M_SHINY, seg=14, rings=9)
    c.tube([(0, 0.33, 0.21), (0, -0.42, 0.18)], 0.008, black)                          # wing seam
    for x, y in ((0.14, 0.12), (-0.14, 0.12), (0.2, -0.12), (-0.2, -0.12), (0.1, -0.3), (-0.1, -0.3), (0, 0.24)):
        z = 0.14 + 0.22 * math.sqrt(max(0.0, 1 - (x / 0.32) ** 2 - ((y + 0.05) / 0.38) ** 2))
        c.blob((x, y, z), (0.06, 0.06, 0.02), black)                                   # spots
    c.blob((0, 0.36, 0.1), (0.15, 0.09, 0.08), black)                                  # head
    for sx in (1, -1):
        c.blob((sx * 0.08, 0.41, 0.13), (0.035, 0.03, 0.025), (0.97, 0.96, 0.92))      # white cheeks
        c.tube([(sx * 0.05, 0.43, 0.14), (sx * 0.1, 0.52, 0.2)], 0.007, black)
    c.legs([(0.2, 0.14, 0.05, (0.14, 0.06, 0.0), (0.06, 0.06, -0.06)),
            (0.0, 0.16, 0.05, (0.15, 0.0, 0.0), (0.06, 0.0, -0.06)),
            (-0.2, 0.14, 0.05, (0.14, -0.06, 0.0), (0.06, -0.06, -0.06))], black, thick=0.016)
    return c.finish()


def bumblebee():
    c = Critter("Critter_Bumblebee")
    yellow, black, wing = rgb("#ffd34d"), rgb("#231a26"), rgb("#e8f4ff")

    def stripes(p):
        return black if int((p.y + 0.6) / 0.12) % 2 == 0 else yellow

    c.blob((0, -0.12, 0.26), (0.24, 0.3, 0.22), stripes, seg=12, rings=8)              # fuzzy body
    c.blob((0, 0.2, 0.3), (0.17, 0.14, 0.16), yellow)                                  # thorax
    c.blob((0, 0.38, 0.28), (0.13, 0.09, 0.11), black)                                 # head
    c.tube([(0, -0.4, 0.24), (0, -0.48, 0.22)], [0.03, 0.004], black)                  # stinger
    for sx in (1, -1):
        c.blob((sx * 0.08, 0.44, 0.32), (0.045, 0.03, 0.05), (0.05, 0.04, 0.08))       # big round eyes
        c.blob((sx * 0.09, 0.465, 0.34), (0.012, 0.01, 0.012), (1, 1, 1))              # eye shine
        c.tube([(sx * 0.04, 0.44, 0.38), (sx * 0.1, 0.52, 0.5), (sx * 0.16, 0.58, 0.52)], 0.008, black)
        c.blob((sx * 0.2, 0.08, 0.5), (0.18, 0.1, 0.012), wing, rot=(0, sx * 25, sx * 35))   # wings
    c.legs([(0.18, 0.12, 0.18, (0.12, 0.06, -0.06), (0.04, 0.06, -0.1)),
            (0.08, 0.14, 0.18, (0.13, 0.0, -0.06), (0.04, 0.0, -0.1)),
            (-0.04, 0.14, 0.18, (0.13, -0.06, -0.06), (0.04, -0.06, -0.1))], black, thick=0.016)
    return c.finish()


def luna_moth():
    c = Critter("Critter_LunaMoth")
    green, pale, white, rim = rgb("#9fe8b0"), rgb("#d2f8da"), rgb("#fbfaf4"), rgb("#b07ad0")

    def wing_color(p):
        return rim if abs(p.x) > 0.52 else (pale if abs(p.x) < 0.16 else green)

    c.tube([(0, 0.3, 0.2), (0, 0.02, 0.21), (0, -0.28, 0.18)], [0.07, 0.085, 0.04], white, sides=6)   # fuzzy body
    c.blob((0, 0.34, 0.21), (0.07, 0.06, 0.06), white)
    for sx in (1, -1):
        # forewings sweep forward, hindwings back, the long tails grow out of the hindwings
        c.blob((sx * 0.3, 0.14, 0.24), (0.3, 0.2, 0.014), wing_color, rot=(0, sx * 8, sx * 28), seg=12)
        c.blob((sx * 0.22, -0.16, 0.22), (0.22, 0.17, 0.014), wing_color, rot=(0, sx * 6, -sx * 22), seg=12)
        c.tube([(sx * 0.24, -0.26, 0.22), (sx * 0.3, -0.48, 0.21), (sx * 0.26, -0.72, 0.2)], [0.06, 0.035, 0.012], green)
        c.blob((sx * 0.32, 0.14, 0.258), (0.05, 0.05, 0.006), (0.98, 0.84, 0.42), rot=(0, sx * 8, sx * 28))  # eyespots
        c.blob((sx * 0.32, 0.14, 0.266), (0.022, 0.022, 0.004), (0.35, 0.2, 0.42), rot=(0, sx * 8, sx * 28))
        c.blob((sx * 0.1, 0.46, 0.3), (0.05, 0.11, 0.01), (0.96, 0.85, 0.5), rot=(-30, 0, sx * 25))      # feathery antennae
    c.legs([(0.15, 0.05, 0.14, (0.08, 0.06, -0.06), (0.04, 0.04, -0.08))], white, thick=0.014)
    return c.finish()


def firefly():
    c = Critter("Critter_Firefly")
    body, orange, glow = rgb("#2e2a3a"), rgb("#ff8a3d"), rgb("#f6ff7a")
    c.blob((0, -0.05, 0.16), (0.14, 0.34, 0.09), body, M_SHINY)                         # wing covers
    c.blob((0, -0.36, 0.14), (0.11, 0.13, 0.08), glow, M_GLOW)                          # the lantern
    c.blob((0, 0.3, 0.17), (0.13, 0.1, 0.06), orange)                                  # orange shield
    c.blob((0, 0.42, 0.14), (0.07, 0.06, 0.05), body)
    for sx in (1, -1):
        c.blob((sx * 0.05, 0.46, 0.16), (0.022, 0.02, 0.022), (0.05, 0.05, 0.05))
        c.tube([(sx * 0.03, 0.47, 0.17), (sx * 0.1, 0.62, 0.26), (sx * 0.14, 0.7, 0.27)], 0.007, body)
    c.legs([(0.22, 0.08, 0.1, (0.1, 0.05, 0.0), (0.05, 0.05, -0.1)),
            (0.05, 0.1, 0.1, (0.11, 0.0, 0.0), (0.05, 0.0, -0.1)),
            (-0.1, 0.1, 0.1, (0.1, -0.05, 0.0), (0.05, -0.05, -0.1))], body, thick=0.012)
    return c.finish()


def mushroom_snail():
    """The random one: a snail that wears a glowing mushroom as its shell."""
    c = Critter("Critter_MushroomSnail")
    slug, cap, dots, stem = rgb("#e9c4a6"), rgb("#9b6cf0"), rgb("#fff4fb"), rgb("#f5ead8")
    c.tube([(0, -0.42, 0.04), (0, -0.1, 0.07), (0, 0.22, 0.09), (0, 0.38, 0.16), (0, 0.44, 0.24)],
           [0.06, 0.11, 0.1, 0.08, 0.06], slug, sides=8)                                 # squishy foot
    for sx in (1, -1):
        c.tube([(sx * 0.03, 0.44, 0.27), (sx * 0.07, 0.48, 0.42)], [0.018, 0.012], slug)   # eye stalks
        c.blob((sx * 0.075, 0.485, 0.44), (0.032, 0.032, 0.032), (1, 1, 1))
        c.blob((sx * 0.08, 0.51, 0.445), (0.016, 0.012, 0.016), (0.1, 0.06, 0.12))
    c.tube([(0, -0.05, 0.12), (0, -0.05, 0.3)], [0.08, 0.07], stem, sides=8)           # stem shell
    c.blob((0, -0.05, 0.34), (0.3, 0.3, 0.16), cap, seg=14, rings=8)                  # mushroom cap
    for x, y in ((0.12, 0.05), (-0.14, -0.02), (0.02, -0.2), (0.15, -0.18), (-0.06, 0.14), (-0.18, -0.2)):
        z = 0.34 + 0.16 * math.sqrt(max(0.0, 1 - (x / 0.3) ** 2 - ((y + 0.05) / 0.3) ** 2))
        c.blob((x, y, z), (0.045, 0.045, 0.012), dots)
    return c.finish()


CRITTERS = [rainbow_stag_beetle, lambda: praying_mantis(),
            lambda: praying_mantis("Critter_OrchidMantis", "#f7c6dc", "#fff0f6", "#ffd9ec", orchid=True),
            ladybug, bumblebee, luna_moth, firefly, mushroom_snail]

objs = [make() for make in CRITTERS]
for i, obj in enumerate(objs):      # line them up for the .blend
    obj.location.x = (i - len(objs) / 2) * 1.6
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "Critters.blend"))

# ---------------------------------------------------------------- preview renders

for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        pass
scene.render.resolution_x, scene.render.resolution_y = 480, 480
scene.render.film_transparent = False
scene.view_settings.view_transform = "Standard"   # true colors (the default filmic look washes them out)
world = bpy.data.worlds.new("W")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.16, 0.12, 0.26, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.2
scene.world = world
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.5
sun.rotation_euler = (math.radians(45), 0, math.radians(30))
scene.collection.objects.link(sun)
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.type = "ORTHO"
scene.collection.objects.link(cam)
scene.camera = cam

for obj in objs:
    for o in objs:
        o.hide_render = o is not obj
    center = obj.location + Vector((0, 0, 0.25))
    for label, yaw, pitch, scale in (("q", 35, 25, 1.9), ("side", 90, 8, 1.9)):
        a, p = math.radians(yaw), math.radians(pitch)
        cam.location = center + Vector((math.sin(a) * math.cos(p), math.cos(a) * math.cos(p), math.sin(p))) * 5
        cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = scale
        scene.render.filepath = os.path.join(OUT, f"{obj.name}_{label}.png")
        bpy.ops.render.render(write_still=True)
print("critters:", ", ".join(o.name for o in objs))
