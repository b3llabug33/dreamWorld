"""
Gatherable resource nodes for dreamWorld's survival layer (wood, rock, ore, thread, plus food).
Each node has a full version and a "used up" version (stump, rubble, empty vein, bare bush) so the
game can swap them when gathered and swap back when they regrow.
  blender.exe -b --factory-startup --python ArtSource/build_resources.py
Writes ArtSource/Resources.blend, renders to Design/Resources/<name>.png and a sheet resources_sheet.png.
Naming: Res_<Kind>_<Variant> and Res_<Kind>_<Variant>_Used.
"""
import math
import os
import random
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from lowpoly import M_GLOW, M_MATTE, M_SHINY, Model, hsv, render_object, rgb, setup_render, shade  # noqa: E402

ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Design", "Resources")
NODES = []


def node(material):
    def wrap(fn):
        NODES.append((material, fn))
        return fn
    return wrap


BARK, BARK_DK, WOOD_IN = "#5a4030", "#3e2c22", "#d8b088"
MOSS, MOSS_DK = "#6f8f3a", "#465c26"


def bark(col=BARK):
    def f(p, n):
        stripe = 0.85 + 0.15 * ((int(p.z * 14 + math.atan2(p.y, p.x) * 2)) % 2)
        k = (0.55 + 0.45 * (0.5 + 0.5 * n.z)) * stripe
        return tuple(min(1.0, c * k) for c in rgb(col))
    return f


def stump(m, r, h, col=BARK):
    m.cyl((0, 0, 0), r * 1.15, 0.12, bark(col), sides=8, top=r)          # flared roots
    m.cyl((0, 0, 0.1), r, h, bark(col), sides=8, top=r * 0.95)
    m.cyl((0, 0, 0.1 + h), r * 0.92, 0.02, WOOD_IN, sides=8)             # cut face with rings
    m.cyl((0, 0, 0.11 + h), r * 0.55, 0.02, shade("#c49870"), sides=8)
    m.cyl((0, 0, 0.12 + h), r * 0.2, 0.02, shade("#a87850"), sides=8)
    for k in range(3):
        a = k * 2.1
        m.tube([(math.cos(a) * r, math.sin(a) * r, 0.08), (math.cos(a) * r * 1.6, math.sin(a) * r * 1.6, 0.0)],
               [r * 0.25, r * 0.08], bark(col))


# ================================================================ wood

def pine_tree(m, h, rnd, hue=0.36):
    m.cyl((0, 0, 0), 0.28, h * 0.3, bark(), sides=7, top=0.22)
    for k in range(5):
        r = 1.5 * (1 - k / 6)
        m.cyl((0, 0, h * 0.18 + k * h * 0.15), r, h * 0.26, shade(hsv(hue + rnd.uniform(-0.02, 0.02), 0.45, 0.32)), sides=7, top=0.12)


@node("wood")
def pine():
    m = Model("Res_Tree_Pine")
    pine_tree(m, 7.0, random.Random(1))
    return m


@node("wood")
def pine_used():
    m = Model("Res_Tree_Pine_Used")
    stump(m, 0.3, 0.35)
    return m


@node("wood")
def round_oak():
    m = Model("Res_Tree_Oak")
    m.tube([(0, 0, 0), (0.1, 0, 1.6), (0, 0, 2.6)], [0.4, 0.3, 0.25], bark(), sides=8)
    for a, z in ((0.5, 2.0), (2.6, 2.3), (4.4, 1.9)):
        m.tube([(0, 0, z), (math.cos(a) * 1.1, math.sin(a) * 1.1, z + 0.9)], [0.12, 0.06], bark())
    rnd = random.Random(2)
    for k in range(9):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0, 1.3)
        m.blob((math.cos(a) * r, math.sin(a) * r, 3.2 + rnd.uniform(-0.4, 0.6)), (1.1, 1.1, 0.9),
               shade(hsv(0.27 + rnd.uniform(-0.03, 0.03), 0.5, 0.42)), seg=8, rings=5)
    for k in range(5):   # apples to gather too
        a = rnd.uniform(0, 2 * math.pi)
        m.blob((math.cos(a) * 1.4, math.sin(a) * 1.4, 2.8 + rnd.uniform(0, 0.8)), (0.1, 0.1, 0.1), "#d8343f", M_SHINY)
    return m


@node("wood")
def round_oak_used():
    m = Model("Res_Tree_Oak_Used")
    stump(m, 0.42, 0.45)
    return m


@node("wood")
def birch():
    m = Model("Res_Tree_Birch")

    def birch_bark(p, n):
        dark = (int(p.z * 9) % 4 == 0) and (math.sin(p.x * 40 + p.y * 30) > 0.2)
        return (0.18, 0.16, 0.16) if dark else (0.9 * (0.75 + 0.25 * n.z), 0.88 * (0.75 + 0.25 * n.z), 0.84 * (0.75 + 0.25 * n.z))
    m.tube([(0, 0, 0), (0.08, 0, 2.5), (-0.05, 0, 5.0)], [0.2, 0.16, 0.1], birch_bark, sides=7)
    rnd = random.Random(3)
    for k in range(10):
        a = rnd.uniform(0, 2 * math.pi)
        m.blob((math.cos(a) * rnd.uniform(0.2, 0.9), math.sin(a) * rnd.uniform(0.2, 0.9), rnd.uniform(3.2, 5.4)), (0.6, 0.6, 0.5),
               shade(hsv(0.14 + rnd.uniform(-0.04, 0.03), 0.65, 0.75)), seg=7, rings=4)   # golden autumn leaves
    return m


@node("wood")
def birch_used():
    m = Model("Res_Tree_Birch_Used")
    stump(m, 0.2, 0.3, "#d8d4cc")
    return m


@node("wood")
def glow_tree():
    """A rarer magic tree (night wood): purple bark, glowing leaves. Gives 'glow wood'."""
    m = Model("Res_Tree_Glow")
    m.tube([(0, 0, 0), (-0.2, 0, 1.5), (0.15, 0, 3.0), (0, 0, 3.8)], [0.35, 0.28, 0.2, 0.12], bark("#4a3060"), sides=8)
    rnd = random.Random(4)
    for k in range(7):
        a = rnd.uniform(0, 2 * math.pi)
        m.blob((math.cos(a) * rnd.uniform(0.3, 1.2), math.sin(a) * rnd.uniform(0.3, 1.2), 3.6 + rnd.uniform(-0.3, 0.7)),
               (0.8, 0.8, 0.55), hsv(rnd.choice([0.78, 0.55, 0.85]), 0.55, 0.85), M_GLOW, seg=8, rings=5)
    for k in range(8):
        a = rnd.uniform(0, 2 * math.pi)
        m.tube([(math.cos(a) * 1.0, math.sin(a) * 1.0, 3.4), (math.cos(a) * 1.05, math.sin(a) * 1.05, 2.6 - rnd.uniform(0, 0.8))],
               0.015, hsv(0.78, 0.4, 0.95), M_GLOW)
    return m


@node("wood")
def glow_tree_used():
    m = Model("Res_Tree_Glow_Used")
    stump(m, 0.36, 0.4, "#4a3060")
    m.blob((0.2, 0.1, 0.55), (0.1, 0.1, 0.06), hsv(0.78, 0.5, 0.9), M_GLOW)   # a little regrowing sprout
    return m


@node("wood")
def fallen_log():
    m = Model("Res_Log_Fallen")
    m.cyl((-1.6, 0, 0.38), 0.38, 3.2, bark(), sides=9, rot=(0, 90, 0))   # lying along x, centered
    m.blob((0.4, 0, 0.72), (0.5, 0.3, 0.08), shade(MOSS))
    m.blob((-0.7, 0.2, 0.68), (0.3, 0.2, 0.06), shade(MOSS))
    for k in range(4):
        m.cyl((-0.9 + k * 0.5, -0.3, 0.3), 0.025, 0.12, "#f4ead8", sides=6, rot=(60, 0, 0))
        m.blob((-0.9 + k * 0.5, -0.38, 0.36), (0.06, 0.06, 0.03), hsv(0.08, 0.6, 0.85), rot=(60, 0, 0))   # shelf fungus
    return m


@node("wood")
def fallen_log_used():
    m = Model("Res_Log_Fallen_Used")
    for k in range(6):
        m.box((-1.0 + k * 0.4, (k % 2) * 0.2 - 0.1, 0.03), (0.3, 0.12, 0.06), shade(WOOD_IN), rot=(0, 0, k * 25))   # wood chips
    return m


# ================================================================ rock

def rock_color(p, n):
    if n.z > 0.55:
        return hsv(0.24, 0.45, 0.3 + 0.25 * n.z)        # moss on the tops
    return hsv(0.62, 0.06, 0.38 + 0.2 * n.z + 0.05 * math.sin(p.x * 12))


@node("rock")
def boulder():
    m = Model("Res_Rock_Boulder")
    m.blob((0, 0, 0.55), (0.95, 0.8, 0.65), rock_color, seg=9, rings=6, rot=(6, 4, 30))
    m.blob((0.7, -0.4, 0.3), (0.45, 0.4, 0.35), rock_color, seg=8, rings=5)
    m.blob((-0.6, 0.45, 0.25), (0.35, 0.3, 0.28), rock_color, seg=7, rings=5)
    return m


@node("rock")
def boulder_used():
    m = Model("Res_Rock_Boulder_Used")
    rnd = random.Random(11)
    for k in range(7):
        m.blob((rnd.uniform(-0.7, 0.7), rnd.uniform(-0.6, 0.6), 0.1), (rnd.uniform(0.12, 0.25),) * 2 + (0.12,), rock_color, seg=6, rings=4)
    return m


@node("rock")
def rock_pile():
    m = Model("Res_Rock_Pile")
    rnd = random.Random(12)
    for k in range(9):
        r = rnd.uniform(0.18, 0.4)
        m.blob((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.5, 0.5), r * 0.7 + (0.25 if k > 6 else 0)), (r, r * 0.9, r * 0.75), rock_color, seg=7, rings=5)
    return m


@node("rock")
def rock_pile_used():
    m = Model("Res_Rock_Pile_Used")
    rnd = random.Random(13)
    for k in range(5):
        m.blob((rnd.uniform(-0.5, 0.5), rnd.uniform(-0.4, 0.4), 0.06), (0.1, 0.09, 0.06), rock_color, seg=5, rings=3)
    return m


@node("rock")
def standing_stone():
    """Tall rune stone (a little bit magic): gives rock + a chance of dream shards."""
    m = Model("Res_Rock_RuneStone")
    m.cyl((0, 0, 0), 0.5, 2.6, rock_color, sides=6, top=0.3)
    for k, z in enumerate((0.8, 1.3, 1.8)):
        m.box((0, -0.42 + 0.04 * k, z), (0.25, 0.02, 0.12), hsv(0.55, 0.6, 0.9), M_GLOW)
    m.blob((0, 0, 2.6), (0.32, 0.32, 0.12), shade(MOSS))
    return m


@node("rock")
def standing_stone_used():
    m = Model("Res_Rock_RuneStone_Used")
    m.cyl((0, 0, 0), 0.5, 0.6, rock_color, sides=6, top=0.42)
    m.box((0.6, 0.2, 0.15), (0.6, 0.35, 0.3), rock_color, rot=(0, 0, 30))
    return m


# ================================================================ ore

def craggy(m, faces, rnd, amount):
    """Jitter a blob's vertices so it reads as a chipped rock, not a smooth ball."""
    from mathutils import Vector
    for v in {v for f in faces for v in f.verts}:
        v.co += Vector((rnd.uniform(-amount, amount), rnd.uniform(-amount, amount), rnd.uniform(-amount, amount) * 0.6))


def ore_vein(name, ore_col, glow=False, shine=True):
    """A knee-high cluster of chipped rocks with chunks of ore (or crystal spikes) sticking out on every side."""
    m = Model(name)
    rnd = random.Random(len(name) * 7)
    rocks = [((0, 0, 0.36), (0.55, 0.48, 0.42), 20), ((0.5, 0.25, 0.2), (0.3, 0.28, 0.24), 70), ((-0.42, 0.3, 0.17), (0.26, 0.24, 0.2), 140)]
    for c, r, yaw in rocks:
        craggy(m, m.blob(c, r, rock_color, seg=7, rings=5, rot=(rnd.uniform(-8, 8), rnd.uniform(-8, 8), yaw)), rnd, 0.045)
    # ore on the big rock's surface, all the way round, poking well out
    n = 7 if glow else 12
    for k in range(n):
        az = 2 * math.pi * (k + rnd.uniform(-0.3, 0.3)) / n
        el = rnd.uniform(0.15, 1.1) if glow else rnd.uniform(-0.3, 0.9)
        nx, ny, nz = math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)
        cx, cy, cz = 0.5 * nx, 0.44 * ny, 0.36 + 0.38 * nz
        if glow:
            tilt = math.degrees(math.acos(max(-1, min(1, nz))))
            for j in range(rnd.choice((2, 3))):
                h = rnd.uniform(0.28, 0.5)
                m.cyl((cx, cy, cz), rnd.uniform(0.07, 0.1), h, ore_col, M_GLOW, sides=5, top=0.0,
                      rot=(0, tilt + rnd.uniform(-18, 18), math.degrees(az) + rnd.uniform(-25, 25)))
        else:
            s = rnd.uniform(0.09, 0.14)
            m.blob((cx + nx * 0.03, cy + ny * 0.03, cz + nz * 0.03), (s, s * 0.85, s * 0.8), ore_col,
                   M_SHINY if shine else M_MATTE, seg=5, rings=3, rot=(rnd.uniform(0, 60), rnd.uniform(0, 60), rnd.uniform(0, 360)))
    if glow:   # one big crystal on top
        m.cyl((0.05, 0, 0.7), 0.13, 0.7, ore_col, M_GLOW, sides=6, top=0.0, rot=(8, 6, 0))
    return m


@node("ore")
def copper_vein():
    return ore_vein("Res_Ore_Copper", hsv(0.065, 0.8, 0.95))


@node("ore")
def silver_vein():
    return ore_vein("Res_Ore_Silver", (0.92, 0.94, 1.0))


@node("ore")
def dream_crystal_vein():
    return ore_vein("Res_Ore_DreamCrystal", hsv(0.79, 0.62, 1.0), glow=True)


@node("ore")
def ore_used():
    m = Model("Res_Ore_Used")
    rnd = random.Random(77)
    for c, r, yaw in [((0, 0, 0.3), (0.52, 0.46, 0.34), 20), ((0.5, 0.25, 0.17), (0.28, 0.26, 0.2), 70), ((-0.42, 0.3, 0.15), (0.24, 0.22, 0.17), 140)]:
        craggy(m, m.blob(c, r, rock_color, seg=7, rings=5, rot=(0, 0, yaw)), rnd, 0.045)
    for k in range(6):   # dug-out holes where the ore was
        az = 2 * math.pi * k / 6
        m.blob((0.5 * math.cos(az), 0.44 * math.sin(az), 0.32), (0.08, 0.08, 0.06), hsv(0.62, 0.05, 0.2), seg=5, rings=3)
    for k in range(5):   # chips on the ground
        m.blob((rnd.uniform(-0.8, 0.8), rnd.uniform(-0.7, 0.7), 0.03), (0.07, 0.06, 0.04), rock_color, seg=5, rings=3)
    return m


# ================================================================ thread

@node("thread")
def cotton_bush():
    m = Model("Res_Thread_CottonBush")
    rnd = random.Random(21)
    for k in range(8):
        a = k * 2 * math.pi / 8
        top = (math.cos(a) * 0.4, math.sin(a) * 0.4, 0.8 + rnd.uniform(-0.1, 0.2))
        m.tube([(0, 0, 0), (top[0] * 0.5, top[1] * 0.5, 0.5), top], [0.03, 0.02, 0.012], shade("#6a4a30"))
        m.blob(top, (0.12, 0.12, 0.11), shade("#f8f4ee"), seg=7, rings=5)
        for s in (1, -1):
            m.blob((top[0] * 0.8 + s * 0.06, top[1] * 0.8, top[2] - 0.15), (0.06, 0.03, 0.02), shade(MOSS_DK))
    return m


@node("thread")
def cotton_bush_used():
    m = Model("Res_Thread_CottonBush_Used")
    for k in range(8):
        a = k * 2 * math.pi / 8
        top = (math.cos(a) * 0.4, math.sin(a) * 0.4, 0.75)
        m.tube([(0, 0, 0), (top[0] * 0.5, top[1] * 0.5, 0.5), top], [0.03, 0.02, 0.012], shade("#6a4a30"))
        m.blob(top, (0.04, 0.04, 0.03), shade("#8a6a40"))
    return m


@node("thread")
def cocoon_cluster():
    """Silk cocoons hanging from a branch (moth silk = fancy thread)."""
    m = Model("Res_Thread_Cocoons")
    m.tube([(-0.9, 0, 2.2), (0, 0, 2.3), (0.9, 0, 2.15)], 0.07, bark())
    for sx in (-0.9, 0.9):
        m.tube([(sx, 0, 2.2), (sx * 1.05, 0, 0)], 0.1, bark())
    rnd = random.Random(22)
    for k in range(6):
        x = -0.6 + k * 0.24
        L = rnd.uniform(0.2, 0.5)
        m.tube([(x, 0, 2.25), (x, 0, 2.25 - L)], 0.006, (0.95, 0.95, 0.9))
        m.blob((x, 0, 2.25 - L - 0.14), (0.07, 0.07, 0.15), lambda p, n: (0.95, 0.93, 0.85 + 0.1 * math.sin(p.z * 60)), seg=7, rings=6)
    return m


@node("thread")
def cocoon_cluster_used():
    m = Model("Res_Thread_Cocoons_Used")
    m.tube([(-0.9, 0, 2.2), (0, 0, 2.3), (0.9, 0, 2.15)], 0.07, bark())
    for sx in (-0.9, 0.9):
        m.tube([(sx, 0, 2.2), (sx * 1.05, 0, 0)], 0.1, bark())
    for k in range(3):
        m.tube([(-0.4 + k * 0.4, 0, 2.25), (-0.4 + k * 0.4, 0, 2.05)], 0.006, (0.9, 0.9, 0.85))
    return m


@node("thread")
def web_gather_spot():
    m = Model("Res_Thread_Web")
    for sx in (1, -1):
        m.tube([(sx * 0.9, 0, 0), (sx * 0.85, 0, 2.0)], 0.08, bark())
    for k in range(10):
        a = math.pi * k / 9
        m.tube([(0, 0, 1.0), (math.cos(a) * 0.85, 0, 1.0 + math.sin(a) * 0.95)], 0.005, (0.94, 0.94, 0.97))
        m.tube([(0, 0, 1.0), (math.cos(a) * 0.85, 0, 1.0 - math.sin(a) * 0.95)], 0.005, (0.94, 0.94, 0.97))
    for r in (0.25, 0.5, 0.75):
        pts = [(math.cos(2 * math.pi * k / 18) * r, 0, 1.0 + math.sin(2 * math.pi * k / 18) * r) for k in range(19)]
        m.tube(pts, 0.004, (0.94, 0.94, 0.97))
    return m


@node("thread")
def web_gather_spot_used():
    m = Model("Res_Thread_Web_Used")
    for sx in (1, -1):
        m.tube([(sx * 0.9, 0, 0), (sx * 0.85, 0, 2.0)], 0.08, bark())
    m.tube([(-0.85, 0, 1.6), (-0.4, 0, 1.3)], 0.004, (0.9, 0.9, 0.93))
    return m


# ================================================================ food + small stuff

@node("food")
def berry_bush():
    m = Model("Res_Food_BerryBush")
    rnd = random.Random(31)
    for k in range(6):
        m.blob((rnd.uniform(-0.45, 0.45), rnd.uniform(-0.4, 0.4), rnd.uniform(0.35, 0.65)), (0.42, 0.4, 0.35), shade(hsv(0.3, 0.5, 0.35)), seg=8, rings=5)
    for k in range(16):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0.5, 0.75)
        m.blob((math.cos(a) * r, math.sin(a) * r, rnd.uniform(0.3, 0.8)), (0.05, 0.05, 0.05), rnd.choice(["#6a2a9a", "#3a4ab0", "#d8343f"]), M_SHINY)
    return m


@node("food")
def berry_bush_used():
    m = Model("Res_Food_BerryBush_Used")
    rnd = random.Random(31)
    for k in range(6):
        m.blob((rnd.uniform(-0.45, 0.45), rnd.uniform(-0.4, 0.4), rnd.uniform(0.35, 0.65)), (0.42, 0.4, 0.35), shade(hsv(0.3, 0.45, 0.3)), seg=8, rings=5)
    return m


@node("food")
def mushroom_patch():
    m = Model("Res_Food_MushroomPatch")
    rnd = random.Random(32)
    for k in range(8):
        x, y = rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4)
        h = rnd.uniform(0.08, 0.22)
        m.cyl((x, y, 0), 0.02, h, "#f4ead8", sides=6)
        m.blob((x, y, h), (0.07, 0.07, 0.035), shade(rnd.choice(["#c98a4a", "#b05a3a", "#e8d0a8"])), seg=7, rings=4)
    m.blob((0, 0, 0.0), (0.5, 0.5, 0.03), shade(MOSS_DK))
    return m


@node("food")
def mushroom_patch_used():
    m = Model("Res_Food_MushroomPatch_Used")
    m.blob((0, 0, 0.0), (0.5, 0.5, 0.03), shade(MOSS_DK))
    return m


@node("decor")
def gather_tools():
    """Axe, pickaxe and sickle: tool items you hold while gathering."""
    m = Model("Res_Tools")
    # axe
    m.tube([(-0.6, 0, 0), (-0.6, 0, 0.8)], 0.025, shade("#8a5a3a"))
    m.plate([(0, 0), (0.18, 0.05), (0.2, 0.2), (0, 0.16)], 0, 0.03, shade("#a8aab4"), M_SHINY)
    # pickaxe
    m.tube([(0, 0, 0), (0, 0, 0.85)], 0.025, shade("#8a5a3a"))
    m.tube([(-0.28, 0, 0.75), (0, 0, 0.85), (0.28, 0, 0.75)], [0.01, 0.04, 0.01], shade("#a8aab4"), M_SHINY)
    # sickle (for thread/cotton)
    m.tube([(0.6, 0, 0), (0.6, 0, 0.45)], 0.025, shade("#8a5a3a"))
    m.tube([(0.6, 0, 0.45), (0.75, 0, 0.6), (0.7, 0, 0.75), (0.55, 0, 0.72)], [0.025, 0.02, 0.012, 0.004], shade("#a8aab4"), M_SHINY)
    return m


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = []
    for i, (mat, fn) in enumerate(NODES):
        obj = fn().finish()
        obj["material"] = mat
        obj.location = ((i % 8) * 5.0, (i // 8) * 5.0, 0)
        objs.append(obj)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Resources.blend"))
    os.makedirs(OUT, exist_ok=True)
    cam = setup_render(360)
    for obj in objs:
        render_object(obj, objs, cam, os.path.join(OUT, obj.name + ".png"))
    with open(os.path.join(OUT, "manifest.txt"), "w") as f:
        for obj in objs:
            f.write(f"{obj['material']}\t{obj.name}\n")
    print("RESOURCES", len(objs))


main()
