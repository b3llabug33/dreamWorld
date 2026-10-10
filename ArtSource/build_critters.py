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
        two = callable(color) and color.__code__.co_argcount >= 2
        if two:
            self.bm.normal_update()
        for f in faces:
            f.material_index = mat
            for loop in f.loops:
                if not callable(color):
                    c = color
                elif two:
                    c = color(loop.vert.co, loop.vert.normal)
                else:
                    c = color(loop.vert.co)
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

    def spike(self, base, tip, radius, color, mat=M_MATTE):
        """A little cone: leg spines, mandible teeth, claws."""
        return self.tube([base, tip], [radius, radius * 0.08], color, mat, sides=4)

    def jointed_leg(self, points, radii, color, mat=M_MATTE, foot=(0, 0.05, -0.01), foot_segments=4,
                    spines=(), claw=0.022):
        """A real insect leg: coxa-femur-tibia as one bent tube, then a chain of little foot segments
        (tarsi) ending in two claws. spines: (segment index, count, length, side vector)."""
        pts = [Vector(q) for q in points]
        self.tube(pts, radii, color, mat, sides=6)
        for seg_i, count, length, side in spines:
            a, b = pts[seg_i], pts[seg_i + 1]
            for k in range(count):
                t = (k + 1) / (count + 1)
                base = a.lerp(b, t)
                r = radii[seg_i] + (radii[seg_i + 1] - radii[seg_i]) * t
                out = Vector(side).normalized()
                self.spike(base + out * r * 0.8, base + out * (r + length) + (b - a).normalized() * length * 0.5,
                           r * 0.35, color, mat)
        step = Vector(foot) / foot_segments
        q = pts[-1]
        r0 = radii[-1]
        for k in range(foot_segments):
            centre = q + step * (k + 0.5)
            size = r0 * (1.1 - 0.12 * k)
            self.blob(centre, (size, step.length * 0.6, size * 0.8), color, mat, seg=6, rings=4,
                      rot=(0, 0, math.degrees(math.atan2(-step.x, step.y))))
        end = q + step * foot_segments
        side = Vector((step.y, -step.x, 0)).normalized() if step.length > 0 else Vector((1, 0, 0))
        for sgn in (1, -1):
            self.spike(end, end + step.normalized() * claw + side * sgn * claw * 0.6 + Vector((0, 0, -claw * 0.7)),
                       r0 * 0.4, color, mat)

    def segmented(self, start, end, radii_w, radii_h, segments, color, mat=M_MATTE, sides=10, droop=0.0):
        """A body made of overlapping rings (insect abdomen): each segment bulges and pinches."""
        a, b = Vector(start), Vector(end)
        for k in range(segments):
            t0, t1 = k / segments, (k + 1) / segments
            c = a.lerp(b, (t0 + t1) / 2) + Vector((0, 0, -droop * ((t0 + t1) / 2) ** 2))
            w = radii_w[0] + (radii_w[1] - radii_w[0]) * (t0 + t1) / 2
            h = radii_h[0] + (radii_h[1] - radii_h[0]) * (t0 + t1) / 2
            length = (b - a).length / segments * 0.62
            d = (b - a).normalized()
            pitch = math.degrees(math.atan2(d.z, math.hypot(d.x, d.y)))
            self.blob(c, (w, length * 1.05, h), color, mat, rot=(pitch, 0, 0), seg=sides, rings=6)

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
    """Phalacrognathus muelleri, female, matched to photos of Orion: short black mandibles and black legs,
    a bronze-copper pronotum with green edges, and wing covers that are metallic green at the rim with
    magenta-pink and teal-blue streaks running lengthwise down the middle, shifting with the light."""
    c = Critter("Critter_RainbowStagBeetle")

    def hsv(h, s_, v):
        return colorsys.hsv_to_rgb(h % 1.0, max(0.0, min(1.0, s_)), max(0.0, min(1.0, v)))

    def elytra(p, n):
        side = abs(p.x) / 0.245                       # 0 at the seam, 1 at the rim
        light = 0.28 + 0.62 * max(0.0, n.z) ** 1.5    # deep metallic: dark sides, bright top
        if side > 0.82 or p.y < -0.42:                # green rim all the way round
            return hsv(0.34 + 0.02 * n.x, 0.95, light)
        # one strong magenta streak with a teal-blue edge on a green shell, running lengthwise
        band = side + 0.05 * math.sin(p.y * 7)
        stops = [(0.0, 0.32, 0.9), (0.2, 0.36, 0.95), (0.34, 0.52, 0.9), (0.46, 0.88, 0.85),
                 (0.62, 0.92, 0.8), (0.74, 0.4, 0.95), (1.0, 0.34, 0.95)]
        for (a, ha, sa), (b, hb, sb) in zip(stops, stops[1:]):
            if a <= band <= b:
                t = (band - a) / (b - a)
                dh = ((hb - ha + 0.5) % 1.0) - 0.5
                return hsv(ha + dh * t, sa + (sb - sa) * t, light)
        return hsv(0.33, 0.85, light)

    def pronotum(p, n):
        light = 0.4 + 0.55 * max(0.0, n.z)
        edge = abs(p.x) > 0.17
        return hsv(0.3 if edge else 0.08 + 0.03 * n.x, 0.9 if edge else 0.7, light * 0.8)   # dark bronze, green edges

    def head(p, n):
        return hsv(0.22 + 0.05 * n.x, 0.85, 0.25 + 0.5 * max(0.0, n.z))                # green-gold

    black = (0.05, 0.045, 0.05)
    sheen_black = lambda p, n: tuple(0.04 + 0.12 * max(0.0, n.z) for _ in range(3))

    # wing covers: one domed oval with a raised seam, slightly squarer shoulders than the back
    c.blob((0, -0.12, 0.16), (0.245, 0.38, 0.13), elytra, M_SHINY, seg=20, rings=12)
    # the seam follows the top of the shell exactly: points sampled along the ellipse, pushed out along the
    # surface normal a hair more than the facets sag (so it never sinks in between polygons)
    seam = []
    cy, cz, ry, rz = -0.12, 0.16, 0.38, 0.13
    for k in range(25):
        t = math.radians(80 - k * (160 / 24))            # from the front shoulder over the top to the back
        y, z = cy + ry * math.sin(t), cz + rz * math.cos(t)
        normal = Vector((0, math.sin(t) / ry, math.cos(t) / rz)).normalized()
        seam.append(Vector((0, y, z)) + normal * 0.009)
    c.tube(seam, 0.006, lambda p, n: hsv(0.33, 0.9, 0.42), M_SHINY, sides=5)
    c.blob((0, -0.12, 0.08), (0.23, 0.36, 0.07), black, seg=12, rings=6)                # dark belly
    c.blob((0, 0.17, 0.235), (0.035, 0.03, 0.01), pronotum, M_SHINY, seg=6, rings=4)     # scutellum
    # pronotum: wide, rounded, a touch narrower than the shell
    c.blob((0, 0.28, 0.18), (0.215, 0.12, 0.09), pronotum, M_SHINY, seg=16, rings=8)
    # head: small and flat (females have a small head), eyes at the sides
    c.blob((0, 0.42, 0.15), (0.13, 0.08, 0.055), head, M_SHINY, seg=12, rings=6)
    for sx in (1, -1):
        c.blob((sx * 0.125, 0.43, 0.15), (0.03, 0.032, 0.03), black, M_SHINY)          # eyes
        # short, stout black mandibles curving inward, with a tooth
        c.tube([(sx * 0.06, 0.48, 0.14), (sx * 0.1, 0.55, 0.15), (sx * 0.07, 0.61, 0.16), (sx * 0.02, 0.63, 0.16)],
               [0.03, 0.026, 0.018, 0.008], sheen_black, M_SHINY, sides=6)
        c.spike((sx * 0.09, 0.56, 0.15), (sx * 0.05, 0.58, 0.17), 0.012, sheen_black, M_SHINY)
        # black elbowed antennae with a small club
        base = Vector((sx * 0.1, 0.47, 0.17))
        elbow = base + Vector((sx * 0.07, 0.07, 0.04))
        c.tube([base, elbow], [0.009, 0.008], sheen_black)
        tip = elbow
        for k in range(4):
            nxt = tip + Vector((sx * 0.02, 0.02, 0.0))
            c.blob(tip.lerp(nxt, 0.5), (0.008, 0.011, 0.008), sheen_black, seg=6, rings=4)
            tip = nxt
        for k in range(3):
            c.blob(tip + Vector((sx * (0.01 + k * 0.013), 0.01, 0)), (0.007, 0.02, 0.015), sheen_black, seg=6, rings=4,
                   rot=(0, 0, sx * 25))
    # black legs: front tibiae toothed for digging, spines on the others
    for sx in (1, -1):
        c.jointed_leg([(sx * 0.12, 0.26, 0.11), (sx * 0.23, 0.33, 0.12), (sx * 0.31, 0.42, 0.05), (sx * 0.33, 0.52, 0.0)],
                      [0.028, 0.03, 0.022, 0.014], sheen_black, M_SHINY, foot=(sx * 0.02, 0.09, -0.005),
                      spines=[(2, 5, 0.03, (sx * 1, 0.3, 0))])
        c.jointed_leg([(sx * 0.15, 0.02, 0.1), (sx * 0.28, 0.06, 0.13), (sx * 0.39, 0.0, 0.05), (sx * 0.44, -0.05, 0.0)],
                      [0.028, 0.028, 0.02, 0.013], sheen_black, M_SHINY, foot=(sx * 0.06, -0.08, -0.005),
                      spines=[(2, 3, 0.02, (sx * 1, -0.2, 0))])
        c.jointed_leg([(sx * 0.15, -0.16, 0.1), (sx * 0.28, -0.22, 0.13), (sx * 0.37, -0.34, 0.05), (sx * 0.41, -0.43, 0.0)],
                      [0.028, 0.028, 0.02, 0.013], sheen_black, M_SHINY, foot=(sx * 0.04, -0.1, -0.005),
                      spines=[(2, 3, 0.02, (sx * 1, -0.2, 0))])
    return c.finish()


def praying_mantis(name="Critter_Mantis"):
    """Chinese mantis in a threat pose: reared up on its walking legs, raptorial forelegs raised and
    spread with dark spines showing, wings half-lifted. Muted olive-green and brown, angular head,
    small hard eyes, veined wing covers. Meant to look a little scary, not cute."""
    c = Critter(name)
    olive, olive_dk, tan, brown, spine = rgb("#5a7230"), rgb("#3b4a22"), rgb("#7d8a4a"), rgb("#5e4529"), rgb("#2a2416")

    def shaded(col, dark=0.5):
        def color(p, n):
            k = dark + (1.05 - dark) * max(0.0, n.z) + 0.1 * abs(n.x)
            return tuple(min(1.0, ch * k) for ch in col)
        return color

    b = shaded(olive)
    bd = shaded(olive_dk, 0.45)
    # abdomen: one smooth tapering body, with thin dark rings marking the 7 segments
    c.tube([(0, -0.06, 0.3), (0, -0.25, 0.28), (0, -0.45, 0.25), (0, -0.6, 0.21), (0, -0.67, 0.19)],
           [0.06, 0.07, 0.062, 0.045, 0.025], b, sides=10)
    for k in range(1, 7):
        t = k / 7
        y = -0.06 - 0.61 * t
        z = 0.3 - 0.11 * t
        r = 0.06 + 0.01 * math.sin(t * math.pi) - 0.035 * t * t
        c.tube([(0, y + 0.006, z), (0, y - 0.006, z)], r + 0.004, bd, sides=10)
    for sx in (1, -1):
        c.tube([(sx * 0.02, -0.68, 0.19), (sx * 0.05, -0.77, 0.21), (sx * 0.06, -0.8, 0.22)], [0.008, 0.005, 0.002], bd)
    # wing covers half-lifted and spread (threat display), with veins and a brown leading edge
    for sx in (1, -1):
        def wing(p, n, sx=sx):
            outer = abs(p.x) > 0.1
            vein = (abs(p.x) * 40) % 1.0 < 0.12 or (abs(p.y) * 25) % 1.0 < 0.08
            base = brown if outer else tan
            k = 0.5 + 0.4 * max(0.0, n.z)
            col = tuple(ch * (0.7 if vein else 1.0) * k for ch in base)
            return col
        c.blob((sx * 0.1, -0.28, 0.37), (0.09, 0.3, 0.012), wing, rot=(-10, sx * 18, sx * 14), seg=12, rings=8)
    # thorax and the long neck rising steeply (reared up)
    c.blob((0, -0.02, 0.3), (0.06, 0.1, 0.055), b)
    neck = [(0, 0.04, 0.33), (0, 0.1, 0.45), (0, 0.15, 0.58), (0, 0.18, 0.66)]
    c.tube(neck, [0.04, 0.032, 0.03, 0.036], b, sides=7)
    c.tube([(-0.035, 0.1, 0.45), (0.035, 0.1, 0.45)], 0.008, bd)          # ridge at the leg joint
    # head: sharp triangle, eyes on the top corners, small and hard, mandibles at the point
    hc = Vector((0, 0.22, 0.75))
    c.blob(hc, (0.095, 0.04, 0.05), b, rot=(10, 0, 0), seg=8, rings=5)
    c.tube([hc + Vector((0, 0.02, -0.03)), hc + Vector((0, 0.035, -0.09))], [0.03, 0.012], bd, sides=5)   # face to jaw
    for sx in (1, -1):
        eye_c = hc + Vector((sx * 0.09, 0.0, 0.03))
        c.blob(eye_c, (0.03, 0.03, 0.04), shaded(rgb("#8a9a54"), 0.6), seg=8, rings=6)
        c.blob(eye_c + Vector((sx * 0.012, 0.024, 0.0)), (0.006, 0.004, 0.012), spine)          # narrow pseudopupil
        c.tube([hc + Vector((sx * 0.03, 0.03, 0.035)), hc + Vector((sx * 0.07, 0.025, 0.06))], 0.006, bd)   # brow ridge
        c.spike(hc + Vector((sx * 0.015, 0.035, -0.09)), hc + Vector((sx * 0.0, 0.05, -0.12)), 0.008, spine)  # jaws
        c.tube([hc + Vector((sx * 0.02, 0.03, 0.05)), hc + Vector((sx * 0.1, 0.18, 0.2)),
                hc + Vector((sx * 0.22, 0.3, 0.22)), hc + Vector((sx * 0.32, 0.34, 0.18))],
               [0.005, 0.004, 0.003, 0.0015], bd)                                            # long antennae
        # raised raptorial forelegs, spread wide, jaws of the claws open
        cox0 = Vector((sx * 0.035, 0.1, 0.46))
        cox1 = Vector((sx * 0.12, 0.2, 0.52))
        fem1 = Vector((sx * 0.2, 0.3, 0.74))
        tib1 = Vector((sx * 0.17, 0.4, 0.66))
        claw = tib1 + Vector((sx * -0.02, 0.03, -0.05))
        c.tube([cox0, cox1], [0.026, 0.024], b, sides=6)
        c.tube([cox1, cox1.lerp(fem1, 0.45), fem1], [0.024, 0.034, 0.02], b, sides=7)
        c.tube([fem1, tib1, claw], [0.018, 0.014, 0.005], b, sides=6)
        inner = Vector((-sx * 0.6, 0.6, -0.3)).normalized()
        for k in range(7):   # long dark spines down the femur
            base = cox1.lerp(fem1, 0.2 + k * 0.11)
            ln = 0.035 if k % 2 == 0 else 0.022
            c.spike(base + inner * 0.015, base + inner * (0.015 + ln) + Vector((0, 0.01, 0.005)), 0.006, spine)
        for k in range(4):   # and the tibia
            base = fem1.lerp(tib1, 0.25 + k * 0.2)
            c.spike(base, base + Vector((-sx * 0.012, -0.02, -0.025)), 0.005, spine)
        c.blob(cox0.lerp(cox1, 0.6) + Vector((-sx * 0.018, 0, -0.006)), (0.01, 0.014, 0.012), spine)   # coxa eyespot
        # walking legs: long, thin, knees high, planted wide
        c.jointed_leg([(sx * 0.05, 0.02, 0.28), (sx * 0.26, 0.12, 0.42), (sx * 0.4, 0.2, 0.02)],
                      [0.015, 0.012, 0.008], bd, foot=(sx * 0.03, 0.06, -0.01), foot_segments=4, claw=0.012,
                      spines=[(1, 3, 0.012, (sx * 0.2, -1, 0))])
        c.jointed_leg([(sx * 0.05, -0.06, 0.28), (sx * 0.3, -0.18, 0.42), (sx * 0.46, -0.4, 0.02)],
                      [0.015, 0.012, 0.008], bd, foot=(sx * 0.03, -0.07, -0.01), foot_segments=4, claw=0.012,
                      spines=[(1, 3, 0.012, (sx * 0.2, 1, 0))])
    return c.finish()


def orchid_mantis():
    """Hymenopus coronatus: pink-white, with petal-shaped lobes on the walking legs."""
    c = Critter("Critter_OrchidMantis")
    body, petal, eye = rgb("#f7cde0"), rgb("#fff1f7"), rgb("#ffd3ea")
    c.blob((0, -0.32, 0.24), (0.12, 0.26, 0.09), body, rot=(-6, 0, 0))
    c.blob((0, -0.28, 0.31), (0.13, 0.26, 0.025), petal, rot=(-6, 0, 0))
    c.tube([(0, -0.04, 0.24), (0, 0.18, 0.38), (0, 0.34, 0.54)], [0.05, 0.04, 0.035], body)
    c.blob((0, 0.38, 0.6), (0.09, 0.05, 0.065), body, rot=(20, 0, 0))
    for sx in (1, -1):
        c.blob((sx * 0.085, 0.39, 0.64), (0.04, 0.04, 0.045), eye)
        c.tube([(sx * 0.05, 0.3, 0.5), (sx * 0.1, 0.42, 0.38), (sx * 0.08, 0.52, 0.52), (sx * 0.06, 0.46, 0.4)],
               [0.026, 0.024, 0.018, 0.011], body)
        c.blob((sx * 0.2, -0.05, 0.3), (0.13, 0.08, 0.016), petal, rot=(0, sx * 20, sx * 30))
        c.blob((sx * 0.21, -0.3, 0.3), (0.14, 0.09, 0.016), petal, rot=(0, sx * 20, -sx * 30))
        c.jointed_leg([(sx * 0.05, 0.02, 0.22), (sx * 0.24, 0.1, 0.33), (sx * 0.36, 0.16, 0.0)],
                      [0.016, 0.013, 0.009], body, foot=(sx * 0.03, 0.05, -0.005), claw=0.012)
        c.jointed_leg([(sx * 0.05, -0.1, 0.22), (sx * 0.26, -0.2, 0.33), (sx * 0.4, -0.4, 0.0)],
                      [0.016, 0.013, 0.009], body, foot=(sx * 0.03, -0.06, -0.005), claw=0.012)
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


CRITTERS = [rainbow_stag_beetle, praying_mantis, orchid_mantis,
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
    views = [("q", 35, 25, 1.9), ("side", 90, 8, 1.9)]
    if obj.name in ("Critter_RainbowStagBeetle", "Critter_Mantis"):   # the detailed ones get a closer look
        views += [("top", 0, 89, 1.7), ("front", 0, 12, 1.4), ("close", 140, 22, 1.1)]
    for label, yaw, pitch, scale in views:
        a, p = math.radians(yaw), math.radians(pitch)
        cam.location = center + Vector((math.sin(a) * math.cos(p), math.cos(a) * math.cos(p), math.sin(p))) * 5
        cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = scale
        scene.render.filepath = os.path.join(OUT, f"{obj.name}_{label}.png")
        bpy.ops.render.render(write_still=True)
print("critters:", ", ".join(o.name for o in objs))
