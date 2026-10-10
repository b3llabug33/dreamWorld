"""
Mistpine Hollow: the foggy Pacific-Northwest-style forest where the Mullens live (Twilight parody area).
  blender.exe -b --factory-startup --python ArtSource/build_mistpine.py
Builds the whole area as a few merged meshes plus a layout file of named spots for the game:
  - Mist_Ground: rolling mossy ground with a dirt path, the river bed and the quarry pit
  - Mist_Forest_<n>: tall firs and cedars with moss and hanging lichen (chunked by area)
  - Mist_Undergrowth: ferns, mossy logs, wet rocks, mushrooms, stumps
  - Mist_River: the water; Mist_Meadow: the flower meadow; Mist_Baseball: the storm clearing
  - Mist_Quarry: cliffs and boulders where the Boulder Brute sleeps; Mist_Wolfcamp: Wolfpup campfire
  - MullenHouse objects appended from MullenHouse.blend, placed in the clearing
Layout (spots for NPCs, spawns, bushes, boss, zone boxes): Assets/Models/Mistpine/mistpine_layout.json
Area is about 160 x 160 m. The entrance from town is at the east edge (+X), y = 0.
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import packs  # noqa: E402
from lowpoly import M_GLASS, M_GLOW, M_MATTE, M_SHINY, Model, hsv, rgb, setup_render, shade  # noqa: E402

ROOT = os.path.dirname(HERE)
LAYOUT = os.path.join(ROOT, "Assets", "Models", "Mistpine", "mistpine_layout.json")
OUT = os.path.join(ROOT, "Design", "World", "mistpine")
rnd = random.Random(1234)

HALF = 80.0
HOUSE = Vector((-30.0, 10.0, 0.0))      # the Mullen house clearing
MEADOW = Vector((-45.0, -45.0, 0.0))     # Edwerd's sparkly meadow
BASEBALL = Vector((10.0, 45.0, 0.0))     # thunderstorm baseball clearing
RIVER_X = 45.0                           # river runs north-south at about this x
WOLFCAMP = Vector((58.0, -28.0, 0.0))    # Wolfpups' campfire on the far bank
QUARRY = Vector((30.0, -60.0, 0.0))      # the Boulder Brute's quarry
ENTRANCE = Vector((HALF, 0.0, 0.0))
BRIDGE = []                              # filled in when the bridge is built

# the main dirt path: entrance -> past the baseball turnoff -> house; branches to meadow, river, quarry
PATH = [ENTRANCE, Vector((60, 2, 0)), Vector((35, 8, 0)), Vector((10, 12, 0)), Vector((-12, 10, 0)), HOUSE + Vector((12, 0, 0))]
BRANCHES = [
    [Vector((10, 12, 0)), Vector((10, 30, 0)), BASEBALL + Vector((0, -10, 0))],
    [HOUSE + Vector((0, -8, 0)), Vector((-36, -25, 0)), MEADOW + Vector((5, 8, 0))],
    [Vector((35, 8, 0)), Vector((40, -10, 0)), Vector((RIVER_X - 4, -24, 0))],
    [Vector((40, -10, 0)), Vector((34, -35, 0)), QUARRY + Vector((0, 12, 0))],
]
CLEARINGS = [(HOUSE, 22.0), (MEADOW, 16.0), (BASEBALL, 18.0), (WOLFCAMP, 10.0), (QUARRY, 18.0), (ENTRANCE, 8.0)]


def dist_to_polyline(p, pts):
    best = 1e9
    for a, b in zip(pts, pts[1:]):
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-6)))
        best = min(best, (a + ab * t - p).length)
    return best


def path_distance(p):
    p = Vector((p.x, p.y, 0))
    d = dist_to_polyline(p, PATH)
    for br in BRANCHES:
        d = min(d, dist_to_polyline(p, br))
    return d


def river_center_x(y):
    return RIVER_X + 6.0 * math.sin(y * 0.045) + 2.0 * math.sin(y * 0.11)


def height(x, y):
    """Gentle rolling hills, flat clearings, a river channel, the quarry pit, a ridge to the north-west."""
    h = 1.6 * noise.noise(Vector((x * 0.025, y * 0.025, 0.3))) + 0.6 * noise.noise(Vector((x * 0.07, y * 0.07, 1.7)))
    h += max(0.0, (-x - y - 60) * 0.12)                          # ridge rising toward the north-west corner
    for c, r in CLEARINGS:                                       # flatten clearings
        d = (Vector((x, y, 0)) - c).length
        if d < r:
            h *= (d / r) ** 2
    pd = path_distance(Vector((x, y, 0)))
    if pd < 3.0:
        h *= 0.5 + 0.5 * pd / 3.0
    rd = abs(x - river_center_x(y))
    if rd < 7.0:
        h = min(h, -1.2 * (1 - (rd / 7.0) ** 2))                 # river channel
    qd = (Vector((x, y, 0)) - QUARRY).length
    if qd < 16:
        h -= 3.0 * (1 - (qd / 16) ** 2)                          # quarry pit
    return h


def ground_color(p, n):
    x, y = p.x, p.y
    pd = path_distance(p)
    if pd < 1.8:
        return hsv(0.08, 0.35, 0.32 + 0.04 * noise.noise(Vector((x * 0.5, y * 0.5, 0))))   # wet dirt path
    if abs(x - river_center_x(y)) < 7.5 and p.z < -0.2:
        return hsv(0.1, 0.12, 0.3)                                                      # river stones / mud
    if (Vector((x, y, 0)) - QUARRY).length < 16 and p.z < -0.6:
        return hsv(0.08, 0.06, 0.42)                                                    # quarry gravel
    m = 0.5 + 0.5 * noise.noise(Vector((x * 0.12, y * 0.12, 4)))
    if m < 0.38:
        return hsv(0.07, 0.4, 0.2)                                                      # rusty needle litter
    return hsv(0.25 + 0.04 * m, 0.45 + 0.15 * m, 0.14 + 0.1 * m * max(0.3, n.z))        # dark wet moss


def build_ground():
    g = Model("Mist_Ground")
    step = 2.0
    n = int(2 * HALF / step)
    verts = {}
    for i in range(n + 1):
        for j in range(n + 1):
            x, y = -HALF + i * step, -HALF + j * step
            verts[i, j] = g.bm.verts.new((x, y, height(x, y)))
    faces = []
    for i in range(n):
        for j in range(n):
            faces.append(g.bm.faces.new((verts[i, j], verts[i + 1, j], verts[i + 1, j + 1], verts[i, j + 1])))
    g.paint(faces, ground_color, M_MATTE)
    return g


# ---------------------------------------------------------------- trees

def fir(m, x, y, h, rnd):
    """Tall Douglas fir: bare lower trunk with moss, layered drooping tiers, dark blue-green."""
    z0 = height(x, y) - 0.2
    trunk = lambda p, n: hsv(0.07, 0.35, 0.18 + 0.06 * n.z) if (p.z - z0) > h * 0.25 or n.x > 0.2 else hsv(0.25, 0.5, 0.25)
    m.cyl((x, y, z0), 0.45 * h / 18, h * 0.55, trunk, sides=7, top=0.3 * h / 18)
    tiers = 7
    for k in range(tiers):
        t = k / tiers
        r = (3.2 - 2.4 * t) * h / 18
        zc = z0 + h * (0.3 + 0.62 * t)
        col = shade(hsv(0.4 + rnd.uniform(-0.02, 0.02), 0.4, 0.2 + 0.05 * t + rnd.uniform(-0.02, 0.02)), 0.55)
        m.cyl((x, y, zc - h * 0.04), r, h * 0.16, col, sides=7, top=r * 0.25)
    # old man's beard lichen hanging from the lower branches
    for k in range(rnd.randint(2, 5)):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(1.0, 2.2) * h / 18
        zt = z0 + h * rnd.uniform(0.32, 0.5)
        m.tube([(x + math.cos(a) * r, y + math.sin(a) * r, zt), (x + math.cos(a) * r, y + math.sin(a) * r, zt - rnd.uniform(0.8, 1.8))],
               [0.12, 0.02], hsv(0.22, 0.25, 0.55), sides=4)


def cedar(m, x, y, h, rnd):
    """Western red cedar: reddish fluted trunk, shaggy drooping tiers of yellow-green sprays, mossy root flare."""
    z0 = height(x, y) - 0.2
    m.cyl((x, y, z0), 0.9 * h / 18, 1.2, shade(hsv(0.25, 0.5, 0.2)), sides=8, top=0.6 * h / 18)   # mossy root flare
    m.cyl((x, y, z0), 0.6 * h / 18, h * 0.8, lambda p, n: hsv(0.04, 0.45, 0.24 + 0.05 * math.sin(math.atan2(p.y - y, p.x - x) * 6)), sides=8, top=0.2 * h / 18)
    for k in range(8):
        t = k / 8
        r = (2.8 - 2.2 * t) * h / 18
        ox, oy = rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25)
        zc = z0 + h * (0.28 + 0.66 * t)
        col = shade(hsv(0.3 + rnd.uniform(-0.02, 0.02), 0.45, 0.19 + 0.04 * t), 0.55)
        # wide, flat-bottomed and droopy: a short cone whose rim hangs down
        m.cyl((x + ox, y + oy, zc - h * 0.06), r, h * 0.11, col, sides=9, top=r * 0.35, rot=(rnd.uniform(-6, 6), rnd.uniform(-6, 6), rnd.uniform(0, 40)))
    m.cyl((x, y, z0 + h * 0.9), 0.5 * h / 18, h * 0.14, shade(hsv(0.3, 0.45, 0.22), 0.55), sides=7, top=0.05)


def mossy_maple(m, x, y, h, rnd):
    """Bigleaf maple from the Hoh rainforest: crooked trunk and limbs wrapped in bright moss, moss curtains hanging down."""
    z0 = height(x, y) - 0.2
    moss = lambda p, n: hsv(0.24, 0.6, 0.3 + 0.08 * max(0, n.z))
    lean = (rnd.uniform(-1, 1), rnd.uniform(-1, 1))
    top = (x + lean[0], y + lean[1], z0 + h * 0.45)
    m.tube([(x, y, z0), (x + lean[0] * 0.3, y + lean[1] * 0.3, z0 + h * 0.2), top], [0.55, 0.45, 0.35], moss, sides=7)
    for k in range(5):
        a = k * 2 * math.pi / 5 + rnd.uniform(-0.4, 0.4)
        L = h * rnd.uniform(0.25, 0.4)
        mid = (top[0] + math.cos(a) * L * 0.5, top[1] + math.sin(a) * L * 0.5, top[2] + L * 0.45)
        tip = (top[0] + math.cos(a) * L, top[1] + math.sin(a) * L, top[2] + L * 0.55)
        m.tube([top, mid, tip], [0.3, 0.2, 0.1], moss, sides=6)
        m.blob(tip, (L * 0.45, L * 0.45, L * 0.3), shade(hsv(0.22, 0.5, 0.3)), seg=8, rings=5)   # leafy crown clump
        for j in range(3):   # moss curtains draped off the limb
            f = rnd.uniform(0.3, 0.9)
            px, py, pz = (top[0] + (tip[0] - top[0]) * f, top[1] + (tip[1] - top[1]) * f, top[2] + (tip[2] - top[2]) * f - 0.15)
            m.tube([(px, py, pz), (px, py, pz - rnd.uniform(1.2, 2.6))], [0.18, 0.03], hsv(0.2, 0.55, 0.42), sides=4)


def spruce_small(m, x, y, h, rnd):
    z0 = height(x, y) - 0.1
    m.cyl((x, y, z0), 0.15, h * 0.3, shade("#3e2c22"), sides=6)
    m.cyl((x, y, z0 + h * 0.15), 1.1 * h / 5, h * 0.9, shade(hsv(0.38, 0.4, 0.24), 0.55), sides=7, top=0.05)


def free(p, margin=0.0):
    """Is this spot open forest (not a path, clearing, river or the house)?"""
    if path_distance(p) < 4.0 + margin:
        return False
    for c, r in CLEARINGS:
        if (Vector((p.x, p.y, 0)) - c).length < r + 2 + margin:
            return False
    if abs(p.x - river_center_x(p.y)) < 9 + margin:
        return False
    return True


def build_forest():
    chunks = {}
    placed = 0
    for k in range(6000):
        p = Vector((rnd.uniform(-HALF + 2, HALF - 2), rnd.uniform(-HALF + 2, HALF - 2), 0))
        if not free(p):
            continue
        key = (int((p.x + HALF) // 40), int((p.y + HALF) // 40))
        m = chunks.get(key) or chunks.setdefault(key, Model(f"Mist_Forest_{key[0]}_{key[1]}"))
        roll = rnd.random()
        h = rnd.uniform(16, 26)
        if roll < 0.5:
            fir(m, p.x, p.y, h, rnd)
        elif roll < 0.62:
            mossy_maple(m, p.x, p.y, rnd.uniform(9, 13), rnd)
        elif roll < 0.82:
            cedar(m, p.x, p.y, h * 0.8, rnd)
        else:
            spruce_small(m, p.x, p.y, rnd.uniform(3, 7), rnd)
        placed += 1
        if placed >= 900:
            break
    print("trees", placed)
    return list(chunks.values())


# ---------------------------------------------------------------- undergrowth

def fern(m, x, y, s):
    z = height(x, y)
    for k in range(7):
        a = k * 2 * math.pi / 7 + rnd.uniform(-0.2, 0.2)
        L = s * rnd.uniform(0.7, 1.0)
        m.tube([(x, y, z), (x + math.cos(a) * L * 0.4, y + math.sin(a) * L * 0.4, z + L * 0.55),
                (x + math.cos(a) * L, y + math.sin(a) * L, z + L * 0.3)],
               [0.06 * s, 0.05 * s, 0.01], shade(hsv(0.27, 0.55, 0.34)), sides=3)


def slab(m, a, b, side, width, thick, color):
    """A flat plank from a to b (top surface exactly through both points), `width` across `side`."""
    a, b = Vector(a), Vector(b)
    w = side.normalized() * width / 2
    top = [a - w, a + w, b + w, b - w]
    vt = [m.bm.verts.new(p) for p in top]
    vb = [m.bm.verts.new(p - Vector((0, 0, thick))) for p in top]
    f = [m.bm.faces.new(vt), m.bm.faces.new(vb[::-1])]
    for i in range(4):
        j = (i + 1) % 4
        f.append(m.bm.faces.new((vb[i], vb[j], vt[j], vt[i])))
    m.paint(f, color, M_MATTE)


def ground_log(m, a, b, r, color, sink=0.35):
    """A log lying on the ground from a to b: each end sits on the terrain there (sunk a little so it never floats)."""
    pa = Vector((a[0], a[1], height(a[0], a[1]) + r * (1 - sink)))
    pb = Vector((b[0], b[1], height(b[0], b[1]) + r * (1 - sink)))
    mid = (pa + pb) / 2
    mid.z = min(mid.z, height(mid.x, mid.y) + r * (1 - sink))   # sag onto bumps instead of bridging over dips
    m.tube([pa, mid, pb], [r, r * 1.02, r * 0.95], color, sides=8)
    return pa, mid, pb


def mossy_log(m, x, y, L, yaw):
    d = Vector((math.cos(math.radians(yaw)), math.sin(math.radians(yaw)), 0)) * L / 2
    pa, mid, pb = ground_log(m, (x - d.x, y - d.y), (x + d.x, y + d.y), 0.45,
                             lambda p, n: hsv(0.25, 0.5, 0.3) if n.z > 0.3 else hsv(0.06, 0.35, 0.25))
    for t in (0.25, 0.5, 0.75):   # shelf mushrooms sitting on top of the log
        q = (pa.lerp(mid, t * 2) if t <= 0.5 else mid.lerp(pb, t * 2 - 1))
        m.blob((q.x, q.y, q.z + 0.4), (0.15, 0.15, 0.06), hsv(0.08, 0.55, 0.7))


def wet_rock(m, x, y, s):
    z = height(x, y)
    m.blob((x, y, z + s * 0.3), (s, s * 0.85, s * 0.6), lambda p, n: hsv(0.25, 0.45, 0.3) if n.z > 0.5 else hsv(0.58, 0.08, 0.28 + 0.1 * n.z),
           M_SHINY, seg=8, rings=5, rot=(0, 0, rnd.uniform(0, 360)))


def build_undergrowth():
    m = Model("Mist_Undergrowth")
    for k in range(900):
        p = Vector((rnd.uniform(-HALF, HALF), rnd.uniform(-HALF, HALF), 0))
        if path_distance(p) < 2.2 or abs(p.x - river_center_x(p.y)) < 6:
            continue
        if (Vector((p.x, p.y, 0)) - HOUSE).length < 12:
            continue
        r = rnd.random()
        if r < 0.6:
            fern(m, p.x, p.y, rnd.uniform(0.6, 1.3))
        elif r < 0.72 and free(p, -2):
            mossy_log(m, p.x, p.y, rnd.uniform(3, 7), rnd.uniform(0, 180))
        elif r < 0.88:
            wet_rock(m, p.x, p.y, rnd.uniform(0.3, 1.2))
        else:
            z = height(p.x, p.y)
            for j in range(rnd.randint(2, 5)):
                ox, oy = rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4)
                m.cyl((p.x + ox, p.y + oy, z), 0.03, 0.15, "#e8dcc8", sides=5)
                m.blob((p.x + ox, p.y + oy, z + 0.16), (0.08, 0.08, 0.04), hsv(rnd.choice([0.08, 0.07, 0.1]), 0.6, 0.6))
    return m


# ---------------------------------------------------------------- landmarks

def build_river():
    m = Model("Mist_River")
    pts = []
    for k in range(41):
        y = -HALF + k * (2 * HALF / 40)
        pts.append(Vector((river_center_x(y), y, -0.55)))
    verts = []
    for p in pts:
        verts.append((m.bm.verts.new((p.x - 5.2, p.y, p.z)), m.bm.verts.new((p.x + 5.2, p.y, p.z))))
    faces = []
    for (a0, a1), (b0, b1) in zip(verts, verts[1:]):
        faces.append(m.bm.faces.new((a0, a1, b1, b0)))
    m.paint(faces, lambda p, n: hsv(0.52, 0.35, 0.32 + 0.05 * math.sin(p.y * 0.8)), M_SHINY)
    for k in range(40):   # stepping stones and river rocks
        y = rnd.uniform(-HALF, HALF)
        x = river_center_x(y) + rnd.uniform(-5, 5)
        m.blob((x, y, -0.5), (rnd.uniform(0.3, 0.9),) * 2 + (0.3,), hsv(0.58, 0.06, 0.35), M_SHINY, seg=7, rings=4)
    return m


def build_meadow():
    """Edwerd's meadow: soft grass, purple lupines and little white flowers, a ring of light."""
    m = Model("Mist_Meadow")
    for k in range(380):
        a = rnd.uniform(0, 2 * math.pi)
        r = 14 * math.sqrt(rnd.random())
        x, y = MEADOW.x + math.cos(a) * r, MEADOW.y + math.sin(a) * r
        z = height(x, y)
        if rnd.random() < 0.45:
            h = rnd.uniform(0.4, 0.8)
            m.tube([(x, y, z), (x, y, z + h)], 0.015, shade(hsv(0.3, 0.5, 0.35)), sides=3)
            m.cyl((x, y, z + h * 0.55), 0.06, h * 0.45, hsv(rnd.choice([0.75, 0.72, 0.8]), 0.5, 0.75), sides=5, top=0.01)   # lupine spike
        else:
            m.blob((x, y, z + 0.12), (0.06, 0.06, 0.03), hsv(rnd.choice([0.15, 0.0, 0.9]), 0.15, 0.95))
    m.cyl((MEADOW.x, MEADOW.y, height(MEADOW.x, MEADOW.y) - 0.05), 12, 0.04, hsv(0.22, 0.45, 0.42), sides=20)   # sunlit grass patch
    return m


def build_baseball():
    m = Model("Mist_Baseball")
    c = BASEBALL
    z = height(c.x, c.y)
    m.cyl((c.x, c.y, z - 0.03), 16, 0.05, hsv(0.25, 0.35, 0.3), sides=20)
    for k in range(4):   # diamond of bases
        a = k * math.pi / 2 + math.pi / 4
        bx, by = c.x + math.cos(a) * 9, c.y + math.sin(a) * 9
        m.box((bx, by, z + 0.04), (0.5, 0.5, 0.08), "#e8e4dc")
    m.cyl((c.x, c.y, z), 1.8, 0.25, hsv(0.08, 0.3, 0.35), sides=12)                 # pitcher's mound
    for k in range(3):   # a few bats leaning on a log
        m.cyl((c.x - 11 + k * 0.3, c.y - 10, z), 0.05, 1.0, shade("#c8a070"), sides=6, top=0.07, rot=(15, 0, 0))
    ground_log(m, (c.x - 13, c.y - 10.8), (c.x - 10, c.y - 10.3), 0.3, shade("#5a4030"), sink=0.2)
    return m


def build_quarry():
    m = Model("Mist_Quarry")
    for k in range(26):
        a = k * 2 * math.pi / 26 + rnd.uniform(-0.1, 0.1)
        r = rnd.uniform(14, 18)
        x, y = QUARRY.x + math.cos(a) * r, QUARRY.y + math.sin(a) * r
        h = rnd.uniform(3, 7)
        z = height(x, y)
        m.box((x, y, z + h / 2 - 1), (rnd.uniform(3, 5), rnd.uniform(2, 4), h),
              lambda p, n: hsv(0.25, 0.4, 0.3) if n.z > 0.6 else hsv(0.6, 0.06, 0.3 + 0.12 * n.z),
              rot=(rnd.uniform(-8, 8), rnd.uniform(-8, 8), math.degrees(a)))
    for k in range(10):   # boulders to throw (and to fight around)
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(4, 11)
        x, y = QUARRY.x + math.cos(a) * r, QUARRY.y + math.sin(a) * r
        wet_rock(m, x, y, rnd.uniform(0.8, 1.6))
    return m


def build_wolfcamp():
    m = Model("Mist_Wolfcamp")
    c = WOLFCAMP
    z = height(c.x, c.y)
    for k in range(8):   # fire ring
        a = k * math.pi / 4
        m.blob((c.x + math.cos(a) * 1.0, c.y + math.sin(a) * 1.0, z + 0.1), (0.25, 0.22, 0.18), hsv(0.58, 0.05, 0.35))
    m.cyl((c.x, c.y, z), 0.5, 1.2, "#ff8a3a", M_GLOW, sides=6, top=0.0)
    m.cyl((c.x + 0.1, c.y, z), 0.3, 1.5, "#ffd84a", M_GLOW, sides=6, top=0.0)
    for a in (0.6, 2.5, 4.2):   # logs to sit on
        cx, cy = c.x + math.cos(a) * 3, c.y + math.sin(a) * 3
        tx, ty = -math.sin(a) * 1.3, math.cos(a) * 1.3
        ground_log(m, (cx - tx, cy - ty), (cx + tx, cy + ty), 0.3, shade("#5a4030"), sink=0.2)
    for k in range(6):   # a pile of snacks (they're always eating)
        m.blob((c.x + 2 + rnd.uniform(-0.4, 0.4), c.y - 2 + rnd.uniform(-0.4, 0.4), z + 0.15), (0.12, 0.12, 0.1),
               rnd.choice(["#d8343f", "#f0a030", "#c98a4a"]))
    return m


def bush_hide(m, x, y):
    z = height(x, y)
    for k in range(5):
        m.blob((x + rnd.uniform(-0.6, 0.6), y + rnd.uniform(-0.6, 0.6), z + rnd.uniform(0.5, 0.9)), (0.8, 0.8, 0.7),
               shade(hsv(0.3, 0.45, 0.28)), seg=8, rings=5)


def build_landmark_misc():
    m = Model("Mist_Details")
    # wolf-spy bushes on the near bank, looking across at the camp
    spots = []
    for k, (dx, dy) in enumerate(((-12, 6), (-10, -4), (-9, -14))):
        x, y = river_center_x(WOLFCAMP.y + dy) - 7.5, WOLFCAMP.y + dy
        bush_hide(m, x, y)
        spots.append((x, y, height(x, y)))
    # a wooden sign at the entrance
    z = height(ENTRANCE.x - 4, 3)
    m.cyl((ENTRANCE.x - 4, 3.5, z), 0.08, 1.8, shade("#5a4030"), sides=6)
    m.box((ENTRANCE.x - 4, 3.5, z + 1.7), (0.1, 1.6, 0.6), shade("#7a5a3a"))
    # lanterns along the last stretch of path to the house (so it's findable in the fog)
    lamps = []
    for t, side in ((0.1, 1), (0.35, -1), (0.6, 1), (0.85, -1)):
        a, b = PATH[-3], PATH[-2]
        p = a.lerp(b, t) + Vector((0, 3.0 * side, 0))
        z = height(p.x, p.y)
        m.cyl((p.x, p.y, z), 0.06, 1.6, shade("#2a2a30"), sides=6)
        m.blob((p.x, p.y, z + 1.7), (0.15, 0.15, 0.2), "#ffd69a", M_GLOW)
        lamps.append((p.x, p.y, z + 1.7))
    bridge = None
    for a, b in zip(PATH, PATH[1:]):
        for k in range(200):
            p = a.lerp(b, k / 200)
            if abs(p.x - river_center_x(p.y)) < 0.15:
                bridge = p
                break
        if bridge:
            break
    if bridge:
        d = (b - a).normalized()
        d.z = 0
        d.normalize()
        yaw = math.degrees(math.atan2(d.y, d.x))
        side = Vector((-d.y, d.x, 0))
        wood = lambda p, n: hsv(0.07, 0.4, 0.3 + 0.05 * math.sin(p.x * 9 + p.y * 9))
        # walk out from the river to where each bank rises, and rest the deck on the banks
        ends = []
        for sgn in (-1, 1):                           # where the river channel ends on each side
            t = 2.0
            while t < 14 and abs((bridge + d * t * sgn).x - river_center_x((bridge + d * t * sgn).y)) < 7.2:
                t += 0.25
            ends.append(t + 0.5)
        zbank = [height(*(bridge + d * ends[i] * (1 if i else -1)).xy) for i in (0, 1)]
        zb = max(zbank) + 0.1                         # deck top just above the higher bank
        p0, p1 = bridge - d * ends[0], bridge + d * ends[1]
        span = (p1 - p0).length
        mid = (p0 + p1) / 2
        m.box((mid.x, mid.y, zb - 0.12), (span, 3.2, 0.25), wood, rot=(0, 0, yaw))
        for sgn, pe, ze in ((-1, p0, zbank[0]), (1, p1, zbank[1])):
            # stone footing under the deck end, then a short ramp down to the ground beyond it
            m.box((pe.x, pe.y, (ze - 0.3 + zb - 0.24) / 2), (1.6, 3.6, zb - 0.24 - ze + 0.3), hsv(0.58, 0.05, 0.33), rot=(0, 0, yaw))
            pg = pe + d * sgn * 3.0
            slab(m, (pe.x, pe.y, zb), (pg.x, pg.y, height(pg.x, pg.y) + 0.02), side, 3.2, 0.25, wood)   # ramp onto the path
        n = int(span // 2.5)
        for k in range(1, n):                         # pilings down into the riverbed
            q = p0 + (p1 - p0) * (k / n)
            zg = height(q.x, q.y)
            if zg < zb - 0.3:
                for s_ in (-1, 1):
                    pq = q + side * 1.4 * s_
                    m.box((pq.x, pq.y, (zg - 0.3 + zb) / 2), (0.3, 0.3, zb - zg + 0.3), shade("#3e2c22"))
        for s_ in (-1, 1):                            # railings sitting on the deck
            off = side * 1.5 * s_
            for k in range(n + 1):
                q = p0 + (p1 - p0) * (k / n) + off
                m.box((q.x, q.y, zb + 0.5), (0.15, 0.15, 1.0), shade("#4a3426"))
            m.box((mid.x + off.x, mid.y + off.y, zb + 1.0), (span, 0.12, 0.12), wood, rot=(0, 0, yaw))
            m.box((mid.x + off.x, mid.y + off.y, zb + 1.07), (span * 0.35, 0.16, 0.05), hsv(0.25, 0.6, 0.35), rot=(0, 0, yaw))
        BRIDGE[:] = [round(mid.x, 2), round(mid.y, 2), round(zb, 2)]
        print("bridge", round(span, 1), "m, deck", round(zb, 2), "banks", [round(z, 2) for z in zbank])
    return m, spots, lamps


def dress():
    """Third-party pack models (see packs.py) for texture and life. Returns layout records for Unity."""
    col = bpy.data.collections.new("Mist_PackDressing")
    bpy.context.scene.collection.children.link(col)
    out = []
    drnd = random.Random(77)

    def put(name, x, y, yaw=None, h=None, season="summer", tint=(1.0, 1.0, 1.0), dz=0.0):
        z = height(x, y) + dz
        out.append(packs.place(name, (x, y, z), drnd.uniform(0, 360) if yaw is None else yaw,
                               h, season, tint, col))
        return z

    def spot(test, tries=200):
        for _ in range(tries):
            p = Vector((drnd.uniform(-HALF + 3, HALF - 3), drnd.uniform(-HALF + 3, HALF - 3), 0))
            if test(p):
                return p
        return None

    def off_path(p, d=2.6):
        return (path_distance(p) > d and abs(p.x - river_center_x(p.y)) > 6.5
                and (Vector((p.x, p.y, 0)) - HOUSE).length > 12 and (Vector((p.x, p.y, 0)) - QUARRY).length > 17)

    moody = (0.72, 0.82, 0.74)                                   # cool, damp tint so the packs sit in the fog palette
    for k in range(70):                                          # mossy rocks
        p = spot(lambda p: off_path(p, 3))
        if p: put(drnd.choice(["AC_Rock1", "AC_Rock2"]), p.x, p.y, h=drnd.uniform(0.7, 2.0), tint=moody, dz=-0.15)
    for k in range(140):                                         # scraggly understory
        p = spot(lambda p: off_path(p))
        if p: put(drnd.choice(["AC_Bush1", "AC_Bush2", "AC_Bush1", "AC_Bush3"]), p.x, p.y, h=drnd.uniform(0.9, 2.2), tint=moody)
    for k in range(260):                                         # grass clumps, thicker near paths and clearings
        p = spot(lambda p: path_distance(p) > 1.9 and abs(p.x - river_center_x(p.y)) > 6)
        if p: put(drnd.choice(["AC_Grass1", "AC_Grass2", "RN_Grass1", "RN_Grass4", "RN_Grass6", "RN_GrassBush"]), p.x, p.y,
                  h=drnd.uniform(0.35, 0.7), season=drnd.choice(["summer", "fall"]), tint=moody)
    for k in range(36):                                          # alders (the birch model) along the riverbanks
        y = drnd.uniform(-HALF + 4, HALF - 4)
        x = river_center_x(y) + drnd.choice((-1, 1)) * drnd.uniform(8.5, 15)
        if path_distance(Vector((x, y, 0))) > 4 and (Vector((x, y, 0)) - QUARRY).length > 22:
            put(drnd.choice(["AC_Birch1", "AC_Birch2"]), x, y, h=drnd.uniform(8, 12), tint=(0.85, 0.9, 0.85))
    for k in range(14):                                          # gnarled dead trees around the quarry + on the ridge
        if k < 9:
            a = drnd.uniform(0, 2 * math.pi)
            x, y = QUARRY.x + math.cos(a) * drnd.uniform(19, 26), QUARRY.y + math.sin(a) * drnd.uniform(19, 26)
        else:
            x, y = drnd.uniform(-75, -50), drnd.uniform(-75, -50)
        if abs(x) < HALF - 2 and abs(y) < HALF - 2 and path_distance(Vector((x, y, 0))) > 4:
            put(f"AC_DeadTree{drnd.randint(1, 5)}", x, y, h=drnd.uniform(6, 11), tint=(0.8, 0.8, 0.85), dz=-0.2)
    for k in range(45):                                          # mushrooms in the damp
        p = spot(lambda p: off_path(p, 2.2))
        if p: put(f"AC_Mushroom{drnd.randint(1, 3)}", p.x, p.y, h=drnd.uniform(0.25, 0.5))
    for k in range(8):                                           # frogs at the water's edge
        y = drnd.uniform(-HALF + 5, HALF - 5)
        x = river_center_x(y) + drnd.choice((-1, 1)) * 6.8
        put("AC_Frog", x, y, h=0.14)
    for (x, y) in ((22, 14.5), (-8, 15), (-44, -32), (5, 33), (60, -14)):   # owls perched on rocks by the paths
        z = put("AC_Rock2", x, y, h=1.4, tint=moody, dz=-0.2)
        out.append(packs.place("AC_Owl", (x, y, z + 1.15), drnd.uniform(0, 360), 0.45, "summer", (1, 1, 1), col))
    # Jakeob's cabin by the Wolfpup campfire, door toward the fire
    cab = WOLFCAMP + Vector((9.0, 5.0, 0))
    put("AC_Cabin", cab.x, cab.y, yaw=math.degrees(math.atan2(WOLFCAMP.y - cab.y, WOLFCAMP.x - cab.x)) - 90, dz=-0.1)
    # the Boulder Brute's cave in the quarry wall, opening toward the pit
    cave = QUARRY + Vector((0, -13.0, 0))
    put("AC_CaveMouth", cave.x, cave.y, yaw=0, h=9.0, tint=(0.85, 0.88, 0.9), dz=-2.0)
    # a tidy garden around the Mullen house: clipped shrubs and two ornamental trees
    for k in range(12):
        a = k * 2 * math.pi / 12 + 0.2
        x, y = HOUSE.x + math.cos(a) * 15, HOUSE.y + math.sin(a) * 15
        if path_distance(Vector((x, y, 0))) > 3:
            put(drnd.choice(["RN_Bush5", "RN_Bush4", "RN_Bush7"]), x, y, h=drnd.uniform(1.0, 1.6), tint=(0.8, 0.9, 0.85))
    for dx, dy in ((-10, 11), (10, -12)):
        put("RN_Tree8", HOUSE.x + dx, HOUSE.y + dy, h=7.5, tint=(0.8, 0.9, 0.85))
    print("dressing", len(out))
    return out, cab, cave


def place_house():
    path = os.path.join(HERE, "MullenHouse.blend")
    names = ["Bld_MullenHouse", "Bld_MullenHouse_Roof"]
    with bpy.data.libraries.load(path) as (src, dst):
        dst.objects = [n for n in src.objects if n in names]
    objs = []
    for o in dst.objects:
        if o is None:
            continue
        bpy.context.scene.collection.objects.link(o)
        o.location = (HOUSE.x, HOUSE.y, height(HOUSE.x, HOUSE.y) + 0.05)
        o.rotation_euler = (0, 0, math.radians(-90))     # entrance (+Y) turned to face the path coming from the east
        objs.append(o)
    return objs


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    built = [build_ground()] + build_forest() + [build_undergrowth(), build_river(), build_meadow(), build_baseball(),
                                                 build_quarry(), build_wolfcamp()]
    misc, bush_spots, lamps = build_landmark_misc()
    built.append(misc)
    objs = [b.finish() for b in built] + place_house()
    dressing, cabin, cave = dress()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Mistpine.blend"))

    # layout for the game (Blender Z-up -> the game converts to Unity Y-up: (x, z, y))
    hz = lambda v: round(height(v.x, v.y), 2)
    house_door = HOUSE + Vector((7.0, 0.0, 0.0))
    spots = {
        "entrance": [ENTRANCE.x - 3, 0.0, hz(ENTRANCE)],
        "house": [HOUSE.x, HOUSE.y, hz(HOUSE)],
        "house_door": [house_door.x, house_door.y, hz(house_door)],
        "meadow": [MEADOW.x, MEADOW.y, hz(MEADOW)],
        "baseball": [BASEBALL.x, BASEBALL.y, hz(BASEBALL)],
        "wolfcamp": [WOLFCAMP.x, WOLFCAMP.y, hz(WOLFCAMP)],
        "quarry": [QUARRY.x, QUARRY.y, hz(QUARRY) - 2.5],
        # where each Mullen hangs out (step 2: find all 8)
        "Carlily Mullen": [HOUSE.x - 3, HOUSE.y - 4, hz(HOUSE)],
        "Esmay Mullen": [HOUSE.x + 1, HOUSE.y + 2, hz(HOUSE)],
        "Edwerd Mullen": [MEADOW.x + 1, MEADOW.y, hz(MEADOW)],
        "Alyss Mullen": [HOUSE.x - 1, HOUSE.y + 3, hz(HOUSE) + 3.4],
        "Jaspurr Mullen": [HOUSE.x + 9, HOUSE.y - 10, hz(HOUSE + Vector((9, -10, 0)))],
        "Rosaleaf Mullen": [HOUSE.x + 14, HOUSE.y + 8, hz(HOUSE + Vector((14, 8, 0)))],
        "Emmitt Mullen": [BASEBALL.x + 2, BASEBALL.y, hz(BASEBALL)],
        "Stella": [house_door.x + 2.5, house_door.y + 1, hz(house_door)],
        # Wolfpups
        "Jakeob": [WOLFCAMP.x - 1.5, WOLFCAMP.y + 2.5, hz(WOLFCAMP)],
        "boulder_brute": [QUARRY.x, QUARRY.y, hz(QUARRY) - 2.5],
    }
    spots["bridge"] = BRIDGE
    spots["jakeob_cabin"] = [cabin.x, cabin.y, hz(cabin)]
    spots["brute_cave"] = [cave.x, cave.y, hz(cave)]
    for i, (x, y, z) in enumerate(bush_spots):
        spots[f"wolf_bush_{i + 1}"] = [round(x, 2), round(y, 2), round(z, 2)]
    zones = {   # boxes: center, size (safe = the path and the house clearing; the rest is the wilds)
        "safe:mistpine path": [[(ENTRANCE.x + HOUSE.x) / 2 + 6, 6, 0], [abs(ENTRANCE.x - HOUSE.x) - 10, 7, 10]],
        "safe:the Mullen house": [[HOUSE.x, HOUSE.y, 0], [40, 40, 14]],
    }
    os.makedirs(os.path.dirname(LAYOUT), exist_ok=True)
    with open(LAYOUT, "w") as f:
        json.dump({"spots": spots, "dressing": dressing, "zones": zones, "lamps": lamps, "size": HALF * 2}, f, indent=1)

    print("MISTPINE OK  (previews: ArtSource/render_mistpine.py)")


main()
