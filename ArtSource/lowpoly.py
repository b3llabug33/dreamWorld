"""
Shared low-poly modeling kit for dreamWorld's code-built assets (props, decor, weapons...).
Everything is flat shaded with per-vertex colors, matching build_critters.py.
Import from a script run inside Blender:  sys.path.insert(0, ArtSource); from lowpoly import *
"""
import colorsys
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

M_MATTE, M_SHINY, M_GLOW, M_GLASS = 0, 1, 2, 3
_MATS = []


def rgb(h):
    h = h.lstrip("#")
    if len(h) == 3:
        h = "".join(c * 2 for c in h)
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def hsv(h, s, v):
    return colorsys.hsv_to_rgb(h % 1.0, max(0.0, min(1.0, s)), max(0.0, min(1.0, v)))


def shade(col, dark=0.6):
    """A color function that darkens faces pointing away from the sky (cheap baked lighting)."""
    col = rgb(col) if isinstance(col, str) else col

    def f(p, n):
        k = dark + (1.0 - dark) * (0.5 + 0.5 * n.z)
        return tuple(min(1.0, c * k) for c in col)
    return f


def mix(a, b, t):
    a = rgb(a) if isinstance(a, str) else a
    b = rgb(b) if isinstance(b, str) else b
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def _vertex_material(name, metallic=0.0, roughness=0.8, glow=0.0, alpha=1.0):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
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
        mat.surface_render_method = "BLENDED"
    mat["dw_kind"] = name
    return mat


def materials():
    global _MATS
    if not _MATS or any(m.name not in bpy.data.materials for m in _MATS):
        _MATS = [_vertex_material("Matte"), _vertex_material("Shiny", 0.55, 0.3),
                 _vertex_material("Glow", glow=1.2), _vertex_material("Glass", 0.1, 0.05, alpha=0.35)]
    return _MATS


class Model:
    """Collects shapes into one mesh, then makes a flat-shaded object. Colors are an RGB tuple, a hex
    string, or a function f(position) / f(position, normal)."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.float_color.new("Col")

    # ---------- painting ----------
    def paint(self, faces, color, mat):
        if isinstance(color, str):
            color = rgb(color)
        two = callable(color) and color.__code__.co_argcount >= 2
        if two:
            self.bm.normal_update()
        for f in faces:
            f.material_index = mat
            for loop in f.loops:
                if not callable(color):
                    c = color
                elif two:
                    c = color(loop.vert.co, f.normal)
                else:
                    c = color(loop.vert.co)
                loop[self.col] = (*c, 1.0)
        return faces

    def _place(self, verts, center, scale, rot):
        m = Matrix.Translation(Vector(center)) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() \
            @ Matrix.Diagonal((*scale, 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        return list({f for v in verts for f in v.link_faces})

    # ---------- primitives ----------
    def box(self, center, size, color, mat=M_MATTE, rot=(0, 0, 0), bevel=0.0):
        before = set(self.bm.faces)
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        faces = self._place(res["verts"], center, size, rot)
        if bevel:
            edges = list({e for f in faces for e in f.edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES")
            faces = [f for f in self.bm.faces if f not in before]   # bevel replaces faces: take everything new
        return self.paint(faces, color, mat)

    def blob(self, center, radii, color, mat=M_MATTE, rot=(0, 0, 0), seg=10, rings=7):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=1.0)
        return self.paint(self._place(res["verts"], center, radii, rot), color, mat)

    def cyl(self, center, radius, height, color, mat=M_MATTE, rot=(0, 0, 0), sides=10, top=None, cap=True):
        """Cylinder (or cone/frustum with `top` radius) standing on `center` (center is the bottom middle)."""
        top = radius if top is None else top
        res = bmesh.ops.create_cone(self.bm, cap_ends=cap, cap_tris=False, segments=sides,
                                    radius1=radius, radius2=max(top, 0.0001), depth=height)
        bmesh.ops.translate(self.bm, vec=Vector((0, 0, height / 2)), verts=res["verts"])
        return self.paint(self._place(res["verts"], center, (1, 1, 1), rot), color, mat)

    def tube(self, points, radii, color, mat=M_MATTE, sides=6):
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

    def torus(self, center, major, minor, color, mat=M_MATTE, rot=(0, 0, 0), seg=14, tube_seg=6):
        pts = [(major * math.cos(2 * math.pi * k / seg), major * math.sin(2 * math.pi * k / seg), 0) for k in range(seg + 1)]
        faces = self.tube(pts, minor, color, mat, sides=tube_seg)
        verts = list({v for f in faces for v in f.verts})
        self._place(verts, center, (1, 1, 1), rot)
        return faces

    def plate(self, outline, z, thick, color, mat=M_MATTE):
        """An extruded flat shape from a 2D outline [(x, y), ...] (signs, leaves, blades, stars)."""
        bottom = [self.bm.verts.new((x, y, z)) for x, y in outline]
        top = [self.bm.verts.new((x, y, z + thick)) for x, y in outline]
        faces = [self.bm.faces.new(list(reversed(bottom))), self.bm.faces.new(top)]
        n = len(outline)
        for k in range(n):
            faces.append(self.bm.faces.new((bottom[k], bottom[(k + 1) % n], top[(k + 1) % n], top[k])))
        return self.paint(faces, color, mat)

    def move(self, faces, offset):
        """Shift a group of faces (each shared corner only once)."""
        for v in {v for f in faces for v in f.verts}:
            v.co += Vector(offset)

    def rotate_last(self, faces, center, rot):
        verts = list({v for f in faces for v in f.verts})
        m = Matrix.Translation(Vector(center)) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() \
            @ Matrix.Translation(-Vector(center))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)

    def finish(self, collection=None):
        me = bpy.data.meshes.new(self.name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in materials():
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.new(self.name, me)
        (collection or bpy.context.scene.collection).objects.link(obj)
        return obj


def star_outline(r_out, r_in, points=5, rot=90):
    out = []
    for k in range(points * 2):
        a = math.radians(rot) + math.pi * k / points
        r = r_out if k % 2 == 0 else r_in
        out.append((r * math.cos(a), r * math.sin(a)))
    return out


def heart_outline(size, n=24):
    out = []
    for k in range(n):
        t = 2 * math.pi * k / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        out.append((x * size / 17, y * size / 17))
    return out


# ---------------------------------------------------------------- rendering previews

def setup_render(res=420, bg=(0.16, 0.12, 0.26)):
    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            pass
    scene.render.resolution_x = scene.render.resolution_y = res
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (*bg, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.2
    scene.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.2
    sun.rotation_euler = (math.radians(45), 0, math.radians(30))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def render_object(obj, all_objs, cam, path, yaw=35, pitch=22, pad=1.35):
    scene = bpy.context.scene
    for o in all_objs:
        o.hide_render = o is not obj
    bpy.context.view_layer.update()
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    size = max(hi - lo)
    a, p = math.radians(yaw), math.radians(pitch)
    cam.location = center + Vector((math.sin(a) * math.cos(p), -math.cos(a) * math.cos(p), math.sin(p))) * size * 4
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = size * pad
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def contact_sheet(paths_labels, out_path, cols=6, cell=300, font=None, title=None):
    """Called from plain Python (Pillow), not Blender."""
    from PIL import Image, ImageDraw, ImageFont
    f = ImageFont.truetype(font, 26) if font else None
    ft = ImageFont.truetype(font, 40) if font else None
    rows = (len(paths_labels) + cols - 1) // cols
    top = 60 if title else 0
    sheet = Image.new("RGB", (cols * cell, rows * (cell + 34) + top), (40, 30, 62))
    d = ImageDraw.Draw(sheet)
    if title:
        d.text((cols * cell // 2, 30), title, font=ft, fill=(250, 230, 245), anchor="mm")
    for i, (p, label) in enumerate(paths_labels):
        x, y = (i % cols) * cell, (i // cols) * (cell + 34) + top
        if os.path.exists(p):
            sheet.paste(Image.open(p).convert("RGB").resize((cell, cell)), (x, y + 34))
        d.text((x + cell // 2, y + 17), label, font=f, fill=(250, 230, 245), anchor="mm")
    sheet.save(out_path)
