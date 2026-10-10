"""
dreamWorld prop library, built from code (low-poly, flat shaded, vertex colors).
  blender.exe -b --factory-startup --python ArtSource/build_props.py -- [category ...]
Writes ArtSource/Props.blend, a render per prop in Design/Props/<category>/, and a contact sheet per
category (Design/Props/props_<category>.png). Sizes are in meters (the player is ~1.6 m tall).
Add a prop: write a function that builds a Model and decorate it with @prop("category").
"""
import math
import os
import random
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from lowpoly import (M_GLASS, M_GLOW, M_MATTE, M_SHINY, Model, heart_outline, hsv, mix, render_object,  # noqa: E402
                     rgb, setup_render, shade, star_outline)

ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Design", "Props")
PROPS = []


def prop(category):
    def wrap(fn):
        PROPS.append((category, fn))
        return fn
    return wrap


# palette from her boards: moss green, plum, olive, warm lamplight, twilight
MOSS, MOSS_DK, PLUM, PLUM_DK, OLIVE = "#6f8f3a", "#465c26", "#7a4f8f", "#4a2f5c", "#8a8a4a"
WOOD, WOOD_DK, WOOD_LT = "#8a5a3a", "#5a3a26", "#b07a50"
CREAM, PINK, LILAC, TEAL, GOLD = "#f4ead8", "#f29cc4", "#b9a4ea", "#4fb3b0", "#e8c25a"
LAMP = "#ffd27a"


# ================================================================ furniture

@prop("furniture")
def mushroom_stool():
    m = Model("Prop_MushroomStool")
    m.cyl((0, 0, 0), 0.12, 0.35, shade(CREAM), sides=8)
    m.blob((0, 0, 0.38), (0.26, 0.26, 0.1), shade("#d6455a"), seg=12, rings=6)
    for a in range(5):
        t = a * 1.3
        m.blob((math.cos(t) * 0.15, math.sin(t) * 0.15, 0.44), (0.04, 0.04, 0.015), CREAM)
    return m


@prop("furniture")
def plaid_armchair():
    m = Model("Prop_PlaidArmchair")

    def plaid(p, n):
        stripe = (int((p.x + 1) * 12) % 3 == 0) or (int((p.y + 1) * 12) % 3 == 0) or (int((p.z + 1) * 12) % 3 == 0)
        base = rgb(PLUM) if not stripe else rgb(OLIVE)
        return tuple(c * (0.65 + 0.35 * (0.5 + 0.5 * n.z)) for c in base)
    m.box((0, 0, 0.25), (0.8, 0.75, 0.3), plaid, bevel=0.03)
    m.box((0, 0.3, 0.65), (0.8, 0.18, 0.6), plaid, bevel=0.03)
    for sx in (1, -1):
        m.box((sx * 0.36, 0, 0.5), (0.14, 0.75, 0.25), plaid, bevel=0.03)
        for sy in (1, -1):
            m.cyl((sx * 0.32, sy * 0.3, 0), 0.04, 0.1, WOOD_DK, sides=6)
    m.box((0, -0.05, 0.43), (0.55, 0.55, 0.08), shade(LILAC), bevel=0.02)   # cushion
    return m


@prop("furniture")
def cozy_bed():
    m = Model("Prop_CozyBed")
    m.box((0, 0, 0.2), (1.1, 2.0, 0.2), shade(WOOD), bevel=0.02)
    m.box((0, 0, 0.36), (1.0, 1.9, 0.14), shade(CREAM), bevel=0.03)
    m.box((0, -0.2, 0.45), (1.04, 1.4, 0.06), lambda p, n: hsv(0.8 + 0.08 * math.sin(p.y * 9), 0.45, 0.75), bevel=0.02)
    m.blob((0, 0.75, 0.48), (0.38, 0.17, 0.08), CREAM)
    m.box((0, 0.98, 0.55), (1.1, 0.08, 0.7), shade(WOOD_DK), bevel=0.02)   # headboard
    m.plate(heart_outline(0.12), 0, 0.03, PINK, M_SHINY)
    return m


@prop("furniture")
def round_rug():
    m = Model("Prop_RoundRug")
    for k, col in enumerate((PLUM, MOSS, CREAM, PLUM, GOLD)):
        m.cyl((0, 0, 0.002 * k), 1.0 - k * 0.18, 0.01, col, sides=20)
    return m


@prop("furniture")
def bookshelf():
    m = Model("Prop_Bookshelf")
    m.box((0, 0, 0.9), (1.0, 0.32, 1.8), shade(WOOD_DK), bevel=0.01)
    rnd = random.Random(3)
    for shelf in range(4):
        z = 0.15 + shelf * 0.42
        m.box((0, -0.02, z - 0.02), (0.92, 0.3, 0.03), shade(WOOD))
        x = -0.42
        while x < 0.4:
            w = rnd.uniform(0.04, 0.08)
            h = rnd.uniform(0.22, 0.34)
            col = hsv(rnd.choice([0.08, 0.3, 0.55, 0.78, 0.92]), 0.5, 0.7)
            lean = rnd.choice([0, 0, 0, 8])
            m.box((x + w / 2, -0.06, z + h / 2), (w, 0.22, h), col, rot=(0, lean, 0))
            x += w + 0.008
    return m


@prop("furniture")
def crt_desk():
    m = Model("Prop_CRTDesk")
    m.box((0, 0, 0.72), (1.3, 0.65, 0.05), shade(WOOD))
    for sx in (1, -1):
        m.box((sx * 0.6, 0, 0.36), (0.05, 0.6, 0.72), shade(WOOD_DK))
    m.box((0, 0.05, 0.98), (0.48, 0.42, 0.42), shade("#d8d2c0"), bevel=0.03)
    m.box((0, -0.17, 1.0), (0.36, 0.02, 0.28), lambda p, n: hsv(0.35, 0.6, 0.55 + 0.3 * ((p.z * 40) % 2 > 1)), M_GLOW)
    m.box((0, -0.18, 0.76), (0.42, 0.15, 0.03), shade("#3a3a44"))   # keyboard
    m.blob((0.3, -0.15, 0.76), (0.04, 0.06, 0.02), shade("#3a3a44"))
    return m


@prop("furniture")
def beetle_lamp_table():
    m = Model("Prop_BeetleSideTable")
    m.cyl((0, 0, 0), 0.04, 0.55, shade(WOOD_DK), sides=6)
    m.cyl((0, 0, 0.55), 0.3, 0.04, shade(WOOD), sides=12)
    m.cyl((0, 0, 0), 0.2, 0.03, shade(WOOD_DK), sides=10)
    # a tiny beetle figurine on top
    m.blob((0, 0, 0.64), (0.07, 0.1, 0.05), lambda p, n: hsv(0.36 - 3 * (p.y), 0.85, 0.5 + 0.4 * n.z), M_SHINY)
    m.blob((0, 0.1, 0.63), (0.04, 0.03, 0.025), hsv(0.08, 0.7, 0.4), M_SHINY)
    return m


@prop("furniture")
def wardrobe():
    m = Model("Prop_Wardrobe")
    m.box((0, 0, 1.0), (1.1, 0.55, 2.0), shade(PLUM_DK), bevel=0.02)
    m.box((0, 0, 2.04), (1.2, 0.62, 0.08), shade(WOOD_DK))
    for sx in (1, -1):
        m.box((sx * 0.27, -0.28, 1.05), (0.5, 0.02, 1.7), shade(PLUM))
        m.blob((sx * 0.06, -0.31, 1.05), (0.025, 0.025, 0.025), GOLD, M_SHINY)
    m.box((0, -0.29, 1.5), (0.25, 0.01, 0.25), shade(CREAM))       # little mirror plaque
    return m


# ================================================================ decor

@prop("decor")
def potted_monstera():
    m = Model("Prop_PottedMonstera")
    m.cyl((0, 0, 0), 0.18, 0.32, shade("#c46a4a"), sides=10, top=0.22)
    m.cyl((0, 0, 0.3), 0.2, 0.03, shade("#4a3324"), sides=10)
    rnd = random.Random(1)
    for k in range(7):
        a = k * 2 * math.pi / 7 + rnd.uniform(-0.2, 0.2)
        stem_end = Vector((math.cos(a) * 0.3, math.sin(a) * 0.3, 0.55 + rnd.uniform(0, 0.35)))
        m.tube([(0, 0, 0.3), stem_end], 0.012, MOSS_DK)
        leaf = [(0, 0), (0.12, 0.08), (0.18, 0.2), (0.1, 0.32), (0, 0.36), (-0.1, 0.32), (-0.18, 0.2), (-0.12, 0.08)]
        f = m.plate(leaf, 0, 0.01, shade(MOSS))
        m.rotate_last(f, (0, 0, 0), (rnd.uniform(-40, -10), 0, math.degrees(a) - 90))
        verts = list({v for face in f for v in face.verts})
        for v in verts:
            v.co += stem_end
    return m


@prop("decor")
def fairy_light_jar():
    m = Model("Prop_FairyLightJar")
    m.cyl((0, 0, 0), 0.12, 0.3, (0.75, 0.9, 0.85), M_GLASS, sides=10)
    m.cyl((0, 0, 0.3), 0.08, 0.06, shade(WOOD_DK), sides=10)
    rnd = random.Random(5)
    for k in range(14):
        m.blob((rnd.uniform(-0.07, 0.07), rnd.uniform(-0.07, 0.07), rnd.uniform(0.04, 0.27)), (0.015, 0.015, 0.015),
               hsv(rnd.choice([0.12, 0.85, 0.5]), 0.5, 1.0), M_GLOW)
    return m


@prop("decor")
def crt_tv_stack():
    m = Model("Prop_CRTStack")
    rnd = random.Random(8)
    z = 0
    for k in range(3):
        w = rnd.uniform(0.45, 0.6)
        h = w * 0.8
        x = rnd.uniform(-0.05, 0.05)
        m.box((x, 0, z + h / 2), (w, w * 0.9, h), shade(rnd.choice(["#d8d2c0", "#3a3640", "#8a7a6a"])), bevel=0.02)
        hue = rnd.choice([0.35, 0.55, 0.8, 0.9])
        m.box((x, -w * 0.45, z + h / 2), (w * 0.72, 0.02, h * 0.62), lambda p, n, hue=hue: hsv(hue, 0.5, 0.5 + 0.4 * ((p.z * 30) % 2 > 1)), M_GLOW)
        z += h
    # moss creeping over the top
    m.blob((0, 0, z), (0.3, 0.27, 0.06), shade(MOSS))
    m.tube([(0.25, -0.2, z), (0.28, -0.26, z - 0.3), (0.24, -0.27, z - 0.6)], 0.02, MOSS)
    return m


@prop("decor")
def bug_specimen_frame():
    m = Model("Prop_BugSpecimenFrame")
    m.box((0, 0, 0.3), (0.7, 0.05, 0.55), shade(WOOD_DK), bevel=0.01)
    m.box((0, -0.02, 0.3), (0.6, 0.02, 0.45), CREAM)
    bugs = [(-0.18, 0.43, 0.36), (0.0, 0.43, 0.85), (0.18, 0.43, 0.08), (-0.18, 0.2, 0.6), (0.0, 0.2, 0.15), (0.18, 0.2, 0.5)]
    for x, z, hue in bugs:
        m.blob((x, -0.04, z), (0.035, 0.012, 0.05), hsv(hue, 0.75, 0.6), M_SHINY)
        for s in (1, -1):
            m.tube([(x + s * 0.02, -0.04, z + 0.02), (x + s * 0.05, -0.04, z + 0.05)], 0.004, "#222")
            m.tube([(x + s * 0.02, -0.04, z - 0.02), (x + s * 0.05, -0.04, z - 0.05)], 0.004, "#222")
    return m


@prop("decor")
def moss_terrarium():
    m = Model("Prop_MossTerrarium")
    m.cyl((0, 0, 0), 0.25, 0.04, shade(WOOD_DK), sides=12)
    m.blob((0, 0, 0.05), (0.22, 0.22, 0.06), shade(MOSS))
    m.blob((0.06, 0.03, 0.1), (0.06, 0.06, 0.05), shade("#8a8580"))   # rock
    m.cyl((-0.08, -0.04, 0.05), 0.012, 0.12, CREAM, sides=6)
    m.blob((-0.08, -0.04, 0.17), (0.05, 0.05, 0.025), hsv(0.85, 0.6, 0.8))   # tiny mushroom
    m.blob((0, 0, 0.18), (0.25, 0.25, 0.22), (0.8, 0.95, 0.9), M_GLASS, seg=14, rings=8)
    return m


@prop("decor")
def lily_pond_tile():
    m = Model("Prop_LilyPond")
    m.cyl((0, 0, 0), 1.2, 0.12, shade("#7a7468"), sides=14)
    m.cyl((0, 0, 0.1), 1.05, 0.02, (0.16, 0.24, 0.26), M_SHINY, sides=14)
    rnd = random.Random(11)
    for k in range(8):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0.2, 0.8)
        pad = [(math.cos(t) * 0.16, math.sin(t) * 0.16) for t in [i * 2 * math.pi / 10 for i in range(9)]] + [(0, 0)]
        f = m.plate(pad, 0.125, 0.01, hsv(rnd.choice([0.25, 0.3, 0.82]), 0.45, 0.45))
        m.move(f, (math.cos(a) * r, math.sin(a) * r, 0))
        if k % 3 == 0:
            m.blob((math.cos(a) * r, math.sin(a) * r, 0.16), (0.05, 0.05, 0.035), PINK)
    return m


@prop("decor")
def wind_chimes():
    m = Model("Prop_WindChimes")
    m.torus((0, 0, 1.0), 0.15, 0.01, shade(WOOD), seg=10)
    m.tube([(0, 0, 1.0), (0, 0, 1.25)], 0.006, "#cfc6b0")
    for k in range(6):
        a = k * math.pi / 3
        x, y = math.cos(a) * 0.15, math.sin(a) * 0.15
        L = 0.3 + 0.08 * (k % 3)
        m.tube([(x, y, 1.0), (x, y, 1.0 - 0.1)], 0.003, "#cfc6b0")
        m.cyl((x, y, 1.0 - 0.1 - L), 0.018, L, hsv(0.75 + 0.03 * k, 0.3, 0.85), M_SHINY, sides=6)
    m.plate(star_outline(0.06, 0.025), 0.45, 0.01, GOLD, M_SHINY)
    return m


@prop("decor")
def plaid_banner():
    m = Model("Prop_PlaidBanner")
    m.cyl((0, 0, 0), 0.03, 2.2, shade(WOOD_DK), sides=6)
    m.tube([(-0.02, 0, 2.1), (0.65, 0, 2.1)], 0.015, shade(WOOD_DK))

    def plaid(p, n):
        s = (int(p.x * 18) % 3 == 0) or (int(p.z * 18) % 3 == 0)
        return rgb(MOSS) if s else rgb(PLUM)
    flag = [(0.05, 2.08), (0.62, 2.08), (0.62, 1.2), (0.335, 1.38), (0.05, 1.2)]
    f = m.plate([(x, z) for x, z in flag], 0, 0.01, plaid)
    m.rotate_last(f, (0, 0, 0), (90, 0, 0))
    return m


# ================================================================ food

@prop("food")
def mushroom_bread():
    m = Model("Prop_MushroomBread")
    m.blob((0, 0, 0.06), (0.16, 0.16, 0.09), shade("#c98a4a"), seg=12, rings=6)
    m.cyl((0, 0, -0.04), 0.07, 0.07, shade("#e8d0a8"), sides=8)
    for a in range(5):
        t = a * 1.25
        m.blob((math.cos(t) * 0.09, math.sin(t) * 0.09, 0.13), (0.02, 0.02, 0.01), CREAM)
    return m


@prop("food")
def strawberry_cake():
    m = Model("Prop_StrawberryCake")
    m.cyl((0, 0, 0), 0.2, 0.06, shade("#e8d0b0"), sides=14)
    m.cyl((0, 0, 0.06), 0.2, 0.03, shade("#f6c6d6"), sides=14)
    m.cyl((0, 0, 0.09), 0.2, 0.06, shade("#e8d0b0"), sides=14)
    m.cyl((0, 0, 0.15), 0.21, 0.03, shade(CREAM), sides=14)
    for k in range(7):
        a = k * 2 * math.pi / 7
        m.blob((math.cos(a) * 0.14, math.sin(a) * 0.14, 0.2), (0.03, 0.03, 0.04), "#e0354a")
    m.cyl((0, 0, 0), 0.25, 0.01, shade("#d0d8e8"), sides=14)   # plate
    return m


@prop("food")
def boba_tea():
    m = Model("Prop_BobaTea")
    m.cyl((0, 0, 0), 0.06, 0.2, (0.9, 0.82, 0.7), M_GLASS, sides=10, top=0.07)
    m.cyl((0, 0, 0.005), 0.058, 0.15, shade("#d8b088"), sides=10, top=0.064)
    for k in range(9):
        a = k * 2.4
        m.blob((math.cos(a) * 0.035, math.sin(a) * 0.035, 0.02 + (k % 3) * 0.015), (0.012, 0.012, 0.012), "#2a1a14", M_SHINY)
    m.cyl((0, 0, 0.2), 0.072, 0.015, (0.9, 0.9, 0.95), M_SHINY, sides=10)
    m.tube([(0.02, 0, 0.05), (0.03, 0.01, 0.3)], 0.008, PINK)
    return m


@prop("food")
def onigiri():
    m = Model("Prop_Onigiri")
    tri = [(0, 0.12), (-0.11, -0.07), (0.11, -0.07)]
    f = m.plate(tri, -0.04, 0.08, CREAM)
    m.rotate_last(f, (0, 0, 0), (90, 0, 0))
    m.box((0, -0.045, -0.04), (0.1, 0.012, 0.07), "#1a2a1e")   # nori
    m.blob((-0.03, -0.045, 0.02), (0.008, 0.004, 0.008), "#2a1a24")
    m.blob((0.03, -0.045, 0.02), (0.008, 0.004, 0.008), "#2a1a24")
    m.blob((-0.05, -0.045, 0.0), (0.012, 0.004, 0.006), PINK)
    m.blob((0.05, -0.045, 0.0), (0.012, 0.004, 0.006), PINK)
    return m


@prop("food")
def potion_bottles():
    m = Model("Prop_PotionBottles")
    for k, (hue, x) in enumerate(((0.95, -0.12), (0.5, 0.0), (0.8, 0.13))):
        h = 0.18 + 0.05 * k
        m.blob((x, 0, 0.08), (0.06, 0.06, 0.08), hsv(hue, 0.7, 0.8), M_GLOW)
        m.cyl((x, 0, 0.14), 0.02, 0.06, (0.85, 0.9, 0.95), M_GLASS, sides=8)
        m.cyl((x, 0, 0.2), 0.024, 0.03, shade(WOOD_DK), sides=8)
    return m


@prop("food")
def fruit_crate():
    m = Model("Prop_FruitCrate")
    m.box((0, 0, 0.12), (0.5, 0.36, 0.24), shade(WOOD_LT), bevel=0.01)
    rnd = random.Random(2)
    for k in range(10):
        m.blob((rnd.uniform(-0.18, 0.18), rnd.uniform(-0.12, 0.12), 0.26 + rnd.uniform(0, 0.04)),
               (0.06, 0.06, 0.055), rnd.choice(["#e0354a", "#f0a030", "#9ac040", "#c0304a"]))
    return m


# ================================================================ nature

@prop("nature")
def glowcap_cluster():
    m = Model("Prop_GlowcapCluster")
    rnd = random.Random(4)
    for k in range(6):
        x, y = rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3)
        h = rnd.uniform(0.15, 0.55)
        m.cyl((x, y, 0), 0.025 + h * 0.04, h, CREAM, sides=6)
        m.blob((x, y, h), (0.08 + h * 0.15, 0.08 + h * 0.15, 0.04 + h * 0.06), hsv(rnd.choice([0.55, 0.78, 0.9]), 0.6, 0.95), M_GLOW, seg=10, rings=5)
    return m


@prop("nature")
def mossy_boulder():
    m = Model("Prop_MossyBoulder")

    def rock(p, n):
        if n.z > 0.45:
            return hsv(0.24, 0.55, 0.35 + 0.25 * n.z)
        return hsv(0.08, 0.08, 0.4 + 0.15 * n.z)
    m.blob((0, 0, 0.35), (0.65, 0.55, 0.45), rock, seg=9, rings=6, rot=(8, 0, 20))
    m.blob((0.45, -0.2, 0.2), (0.3, 0.28, 0.25), rock, seg=8, rings=5)
    m.blob((-0.3, 0.3, 0.6), (0.06, 0.06, 0.03), PINK)
    return m


@prop("nature")
def crystal_cluster():
    m = Model("Prop_CrystalCluster")
    rnd = random.Random(9)
    for k in range(7):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0, 0.15)
        h = rnd.uniform(0.3, 0.8)
        f = m.cyl((math.cos(a) * r, math.sin(a) * r, 0), 0.06, h, hsv(0.78 + rnd.uniform(-0.05, 0.08), 0.55, 0.9), M_GLOW,
                  sides=6, top=0.0)
        m.rotate_last(f, (math.cos(a) * r, math.sin(a) * r, 0), (rnd.uniform(-25, 25), rnd.uniform(-25, 25), 0))
    return m


@prop("nature")
def fern_clump():
    m = Model("Prop_FernClump")
    rnd = random.Random(6)
    for k in range(9):
        a = k * 2 * math.pi / 9 + rnd.uniform(-0.2, 0.2)
        L = rnd.uniform(0.45, 0.7)
        pts = [(0, 0, 0.02), (math.cos(a) * L * 0.4, math.sin(a) * L * 0.4, L * 0.55), (math.cos(a) * L, math.sin(a) * L, L * 0.5)]
        m.tube(pts, [0.01, 0.008, 0.002], MOSS_DK, sides=4)
        for j in range(6):
            t = 0.25 + j * 0.12
            c = Vector(pts[0]).lerp(Vector(pts[1]), min(1, t * 2)) if t < 0.5 else Vector(pts[1]).lerp(Vector(pts[2]), (t - 0.5) * 2)
            side = Vector((-math.sin(a), math.cos(a), 0))
            for s in (1, -1):
                m.tube([c, c + side * s * 0.09 * (1.2 - t)], [0.012, 0.003], shade(MOSS), sides=3)
    return m


@prop("nature")
def autumn_bush():
    m = Model("Prop_AutumnBush")
    rnd = random.Random(12)
    for k in range(7):
        m.blob((rnd.uniform(-0.35, 0.35), rnd.uniform(-0.3, 0.3), rnd.uniform(0.25, 0.55)), (0.32, 0.3, 0.28),
               hsv(rnd.choice([0.03, 0.07, 0.11, 0.9]), 0.7, 0.6), seg=8, rings=5)
    return m


@prop("nature")
def tiny_circuit_tree():
    m = Model("Prop_CircuitTree")
    m.box((0, 0, 0.03), (0.6, 0.6, 0.06), lambda p, n: hsv(0.33, 0.7, 0.35 + 0.15 * ((int(p.x * 30) + int(p.y * 30)) % 2)))
    for k in range(6):
        m.box((-0.25 + k * 0.1, 0.2, 0.065), (0.02, 0.15, 0.01), GOLD, M_SHINY)
    m.box((0.15, -0.15, 0.09), (0.12, 0.12, 0.04), "#1a1a22")   # chip
    m.tube([(0, 0, 0.06), (0.02, 0.01, 0.3), (0, 0, 0.5)], [0.04, 0.03, 0.02], shade(WOOD_DK))
    for k in range(5):
        a = k * 1.3
        m.blob((math.cos(a) * 0.12, math.sin(a) * 0.12, 0.52 + (k % 2) * 0.06), (0.15, 0.15, 0.12), shade(MOSS), seg=8, rings=5)
    return m


# ================================================================ lights

@prop("lights")
def paper_lantern_string():
    m = Model("Prop_LanternString")
    for sx in (-1, 1):
        m.cyl((sx * 1.2, 0, 0), 0.04, 2.2, shade(WOOD_DK), sides=6)
    pts = [(x, 0, 2.1 - 0.35 * (1 - (x / 1.2) ** 2)) for x in [i * 0.2 - 1.2 for i in range(13)]]
    m.tube(pts, 0.008, "#2a2420", sides=4)
    for k, x in enumerate([-0.8, -0.4, 0.0, 0.4, 0.8]):
        z = 2.1 - 0.35 * (1 - (x / 1.2) ** 2) - 0.14
        m.blob((x, 0, z), (0.09, 0.09, 0.12), hsv([0.95, 0.12, 0.8, 0.5, 0.12][k], 0.55, 1.0), M_GLOW, seg=8, rings=6)
    return m


@prop("lights")
def firefly_lantern():
    m = Model("Prop_FireflyLantern")
    m.cyl((0, 0, 0), 0.12, 0.03, shade("#3a3440"), sides=6)
    m.cyl((0, 0, 0.03), 0.1, 0.25, (0.95, 0.95, 0.8), M_GLASS, sides=6)
    for k in range(6):
        a = k * math.pi / 3
        m.box((math.cos(a) * 0.1, math.sin(a) * 0.1, 0.155), (0.015, 0.015, 0.25), shade("#3a3440"))
    m.cyl((0, 0, 0.28), 0.13, 0.08, shade("#3a3440"), sides=6, top=0.03)
    m.torus((0, 0, 0.4), 0.04, 0.008, shade("#3a3440"), rot=(90, 0, 0), seg=8)
    rnd = random.Random(7)
    for k in range(7):
        m.blob((rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06), rnd.uniform(0.07, 0.24)), (0.012, 0.012, 0.012), "#f6ff7a", M_GLOW)
    return m


@prop("lights")
def mushroom_streetlamp():
    m = Model("Prop_MushroomStreetlamp")
    m.cyl((0, 0, 0), 0.06, 2.4, shade(CREAM), sides=8, top=0.045)
    m.tube([(0, 0, 1.6), (0.15, 0, 1.4)], 0.02, shade(MOSS), sides=5)
    m.blob((0.17, 0, 1.38), (0.08, 0.04, 0.02), shade(MOSS))
    m.blob((0, 0, 2.4), (0.45, 0.45, 0.2), shade(PLUM), seg=12, rings=6)
    m.blob((0, 0, 2.28), (0.2, 0.2, 0.1), LAMP, M_GLOW)
    for a in range(6):
        t = a * 1.05
        m.blob((math.cos(t) * 0.28, math.sin(t) * 0.28, 2.52), (0.05, 0.05, 0.02), LILAC, M_GLOW)
    return m


@prop("lights")
def candle_cluster():
    m = Model("Prop_CandleCluster")
    for k, (x, y, h) in enumerate(((0, 0, 0.3), (0.1, 0.05, 0.2), (-0.08, 0.07, 0.15), (0.03, -0.1, 0.24))):
        m.cyl((x, y, 0), 0.04, h, shade(CREAM), sides=8)
        m.blob((x, y, h + 0.03), (0.015, 0.015, 0.035), "#ffb84a", M_GLOW)
        m.blob((x + 0.035, y, h - 0.04), (0.01, 0.01, 0.03), CREAM)   # drip
    return m


# ================================================================ tech (nature reclaiming tech)

@prop("tech")
def mossy_server_rack():
    m = Model("Prop_MossyServerRack")
    m.box((0, 0, 1.0), (0.7, 0.8, 2.0), shade("#2e3036"), bevel=0.02)
    rnd = random.Random(10)
    for k in range(8):
        z = 0.25 + k * 0.22
        m.box((0, -0.41, z), (0.6, 0.02, 0.16), shade("#3e424a"))
        for j in range(5):
            if rnd.random() < 0.6:
                m.blob((-0.22 + j * 0.06, -0.425, z), (0.01, 0.005, 0.01), hsv(rnd.choice([0.33, 0.12, 0.55]), 0.9, 1.0), M_GLOW)
    m.blob((0, 0, 2.02), (0.4, 0.45, 0.1), shade(MOSS))
    for k in range(4):
        x = rnd.uniform(-0.3, 0.3)
        m.tube([(x, -0.4, 2.0), (x + 0.03, -0.43, 1.6), (x - 0.02, -0.42, 1.1 + rnd.uniform(0, 0.4))], [0.03, 0.02, 0.01], shade(MOSS))
    m.blob((0.25, -0.45, 0.6), (0.05, 0.03, 0.04), hsv(0.9, 0.5, 0.9))   # a little flower
    return m


@prop("tech")
def overgrown_terminal():
    m = Model("Prop_OvergrownTerminal")
    m.box((0, 0, 0.45), (1.2, 0.6, 0.9), shade("#c8c2b0"), bevel=0.02)
    m.box((0, -0.1, 0.95), (1.1, 0.5, 0.15), shade("#c8c2b0"), rot=(-20, 0, 0))
    for k, x in enumerate((-0.32, 0.0, 0.32)):
        m.box((x, -0.18, 1.25), (0.3, 0.3, 0.3), shade("#b0aa98"), bevel=0.02)
        m.box((x, -0.34, 1.25), (0.22, 0.02, 0.18), lambda p, n, k=k: hsv([0.5, 0.35, 0.55][k], 0.6, 0.55 + 0.3 * ((p.z * 50) % 2 > 1)), M_GLOW)
    rnd = random.Random(13)
    for k in range(12):
        m.blob((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.3, 0.3), rnd.choice([0.9, 1.4, 0.05])), (rnd.uniform(0.08, 0.18),) * 2 + (0.05,), shade(MOSS))
    m.tube([(-0.6, -0.3, 1.4), (-0.65, -0.35, 0.8), (-0.6, -0.32, 0.1)], 0.025, shade(MOSS_DK))
    return m


@prop("tech")
def floppy_disk_sign():
    m = Model("Prop_FloppySign")
    m.cyl((0, 0, 0), 0.03, 1.4, shade(WOOD_DK), sides=6)
    m.box((0, 0, 1.6), (0.55, 0.05, 0.55), shade(PLUM_DK), bevel=0.01)
    m.box((0, -0.03, 1.73), (0.3, 0.02, 0.18), (0.75, 0.75, 0.8), M_SHINY)
    m.box((0, -0.03, 1.48), (0.4, 0.02, 0.2), CREAM)
    return m


@prop("tech")
def cable_vine_arch():
    m = Model("Prop_CableArch")
    for sx in (1, -1):
        m.box((sx * 1.0, 0, 1.2), (0.25, 0.25, 2.4), shade("#5a5e66"), bevel=0.02)
    pts = [(x, 0, 2.4 + 0.5 * (1 - x * x)) for x in [i * 0.2 - 1.0 for i in range(11)]]
    m.tube(pts, 0.06, "#2a2a30", sides=6)
    m.tube([(x, 0.06, z - 0.05) for x, _, z in pts], 0.03, hsv(0.0, 0.7, 0.5), sides=5)
    m.tube([(x, -0.06, z - 0.03) for x, _, z in pts], 0.03, hsv(0.6, 0.6, 0.5), sides=5)
    rnd = random.Random(14)
    for x, _, z in pts[::2]:
        m.blob((x, 0, z + 0.06), (0.12, 0.1, 0.06), shade(MOSS))
        if rnd.random() < 0.5:
            m.tube([(x, 0, z), (x, -0.05, z - 0.4 - rnd.uniform(0, 0.4))], [0.02, 0.006], shade(MOSS))
    return m


# ================================================================ weapons (silly + cute)

@prop("weapons")
def beetle_horn_sword():
    m = Model("Prop_BeetleHornSword")
    blade = [(0, 0), (0.05, 0.1), (0.045, 0.7), (0.0, 0.85), (-0.03, 0.72), (-0.04, 0.1)]
    m.plate(blade, -0.01, 0.02, lambda p, n: hsv(0.36 - 0.6 * p.y, 0.85, 0.55 + 0.35 * abs(n.z)), M_SHINY)
    m.box((0, -0.02, 0), (0.24, 0.05, 0.04), shade("#2a1b2e"), M_SHINY)
    m.tube([(0, -0.03, 0), (0, -0.2, 0)], 0.022, shade(WOOD_DK))
    m.blob((0, -0.22, 0), (0.035, 0.035, 0.035), hsv(0.85, 0.7, 0.8), M_SHINY)
    return m


@prop("weapons")
def mushroom_mace():
    m = Model("Prop_MushroomMace")
    m.cyl((0, 0, 0), 0.025, 0.7, shade(WOOD), sides=6)
    m.blob((0, 0, 0.78), (0.16, 0.16, 0.12), shade("#d6455a"), seg=10, rings=6)
    for a in range(6):
        t = a * 1.05
        m.blob((math.cos(t) * 0.11, math.sin(t) * 0.11, 0.84), (0.03, 0.03, 0.015), CREAM)
    m.torus((0, 0, 0.08), 0.03, 0.01, GOLD, M_SHINY, seg=8)
    return m


@prop("weapons")
def crystal_staff():
    m = Model("Prop_CrystalStaff")
    m.tube([(0, 0, 0), (0.02, 0, 0.8), (-0.02, 0, 1.4), (0, 0, 1.55)], [0.025, 0.022, 0.02, 0.03], shade(WOOD_DK))
    m.tube([(0, 0, 1.5), (0.1, 0, 1.6), (0.06, 0, 1.75), (-0.04, 0, 1.75), (-0.09, 0, 1.6), (0, 0, 1.5)], 0.012, shade(WOOD_DK))
    f = m.cyl((0, 0, 1.55), 0.05, 0.22, hsv(0.78, 0.5, 0.95), M_GLOW, sides=6, top=0.0)
    m.cyl((0, 0, 1.55), 0.05, 0.08, hsv(0.78, 0.5, 0.95), M_GLOW, sides=6, top=0.0, rot=(180, 0, 0))
    m.tube([(0, 0, 1.0), (0.05, 0.02, 0.9), (0.03, 0.04, 0.75)], 0.008, shade(MOSS))
    return m


@prop("weapons")
def keyboard_hammer():
    m = Model("Prop_KeyboardHammer")
    m.cyl((0, 0, 0), 0.025, 0.75, shade(WOOD), sides=6)
    m.box((0, 0, 0.82), (0.45, 0.16, 0.08), shade("#d8d2c0"), bevel=0.01)
    rnd = random.Random(15)
    for i in range(8):
        for j in range(2):
            m.box((-0.19 + i * 0.054, -0.04 + j * 0.07, 0.87), (0.045, 0.055, 0.02), hsv(rnd.choice([0.0, 0.8, 0.12]), 0.15, 0.85))
    return m


@prop("weapons")
def petal_shield():
    m = Model("Prop_PetalShield")
    for k in range(6):
        a = k * math.pi / 3
        f = m.plate([(0, 0), (0.12, 0.25), (0, 0.38), (-0.12, 0.25)], 0, 0.03, hsv(0.88, 0.45, 0.9))
        m.rotate_last(f, (0, 0, 0), (0, 0, math.degrees(a)))
    m.cyl((0, 0, 0.02), 0.12, 0.04, GOLD, M_SHINY, sides=10)
    f = m.plate(star_outline(0.07, 0.03), 0.06, 0.01, hsv(0.12, 0.6, 1.0), M_GLOW)
    return m


# ================================================================ creatures-adjacent / silly

@prop("silly")
def frog_mailbox():
    m = Model("Prop_FrogMailbox")
    m.cyl((0, 0, 0), 0.05, 0.9, shade(WOOD_DK), sides=6)
    m.blob((0, 0, 1.05), (0.25, 0.2, 0.18), shade("#7ab85a"), seg=10, rings=6)
    for sx in (1, -1):
        m.blob((sx * 0.1, -0.08, 1.2), (0.07, 0.07, 0.07), shade("#7ab85a"))
        m.blob((sx * 0.1, -0.13, 1.21), (0.035, 0.02, 0.035), "#1a1a1a", M_SHINY)
    m.box((0, -0.19, 1.0), (0.22, 0.02, 0.04), "#2a3a20")    # mail slot mouth
    m.blob((0, -0.18, 0.95), (0.06, 0.02, 0.025), PINK)       # tongue
    m.box((0.2, 0, 1.15), (0.02, 0.06, 0.15), "#e0354a")     # little flag
    return m


@prop("silly")
def giant_snail_bench():
    m = Model("Prop_SnailBench")
    m.box((0, 0, 0.4), (1.6, 0.4, 0.06), shade(WOOD))
    for sx in (1, -1):
        m.box((sx * 0.65, 0, 0.2), (0.08, 0.35, 0.4), shade(WOOD_DK))
    m.box((0, 0.18, 0.7), (1.6, 0.05, 0.5), shade(WOOD))
    m.torus((-0.85, 0.05, 0.65), 0.28, 0.1, shade(PLUM), rot=(0, 90, 0), seg=16)
    m.blob((-0.85, 0.05, 0.65), (0.08, 0.14, 0.14), shade(LILAC))
    m.tube([(0.8, 0, 0.42), (0.95, -0.05, 0.6), (1.0, -0.05, 0.8)], [0.07, 0.06, 0.05], shade("#e9c4a6"))
    for s in (1, -1):
        m.tube([(1.0, -0.05 + s * 0.03, 0.82), (1.05, -0.05 + s * 0.06, 0.98)], 0.012, shade("#e9c4a6"))
        m.blob((1.05, -0.05 + s * 0.06, 1.0), (0.025, 0.025, 0.025), "#fff")
    return m


@prop("silly")
def rubber_duck_statue():
    m = Model("Prop_DuckStatue")
    m.cyl((0, 0, 0), 0.5, 0.3, shade("#9a9488"), sides=8)
    m.blob((0, 0, 0.75), (0.45, 0.55, 0.4), shade("#ffd84a"), seg=12, rings=7)
    m.blob((0, -0.35, 1.2), (0.28, 0.28, 0.28), shade("#ffd84a"))
    m.blob((0, -0.62, 1.15), (0.12, 0.08, 0.05), "#ff8a2a")
    for sx in (1, -1):
        m.blob((sx * 0.13, -0.55, 1.3), (0.04, 0.02, 0.05), "#1a1a1a", M_SHINY)
    m.blob((0, -0.35, 1.5), (0.2, 0.2, 0.06), shade("#2a2a30"))         # tiny top hat brim
    m.cyl((0, -0.35, 1.5), 0.12, 0.22, shade("#2a2a30"), sides=10)
    return m


@prop("silly")
def lost_sock_pile():
    m = Model("Prop_LostSockPile")
    rnd = random.Random(16)
    for k in range(9):
        x, y = rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3)
        a = rnd.uniform(0, 2 * math.pi)
        col = lambda p, n, h=rnd.random(): hsv(h, 0.45, 0.85 if (int(p.z * 40) % 2) else 0.65)
        m.tube([(x, y, 0.04 + k * 0.03), (x + math.cos(a) * 0.15, y + math.sin(a) * 0.15, 0.05 + k * 0.03),
                (x + math.cos(a) * 0.2, y + math.sin(a) * 0.2 - 0.06, 0.06 + k * 0.03)], 0.04, col, sides=6)
    m.plate([(-0.12, 0.0), (0.12, 0.0), (0.12, 0.08), (-0.12, 0.08)], 0.35, 0.01, CREAM)   # "LOST SOCKS" sign card
    return m


# ================================================================ town (outdoor pieces)

@prop("town")
def crooked_signpost():
    m = Model("Prop_CrookedSignpost")
    m.tube([(0, 0, 0), (0.03, 0, 1.0), (-0.02, 0, 1.9)], [0.06, 0.05, 0.045], shade(WOOD_DK))
    for z, yaw, col, length in ((1.7, 15, CREAM, 0.7), (1.42, -30, PINK, 0.6), (1.14, 170, LILAC, 0.75)):
        arrow = [(0.0, -0.1), (length, -0.1), (length + 0.14, 0.0), (length, 0.1), (0.0, 0.1)]
        f = m.plate(arrow, -0.02, 0.04, shade(col))
        m.rotate_last(f, (0, 0, 0), (90, 0, 0))          # stand it up in the XZ plane
        m.move(f, (0, 0, z))
        m.rotate_last(f, (0, 0, z), (0, 0, yaw))          # then swing it around the pole
        m.box((0, 0, z), (0.1, 0.08, 0.08), shade(WOOD_DK))
    m.blob((0, 0, 1.95), (0.12, 0.12, 0.05), shade(MOSS))
    return m


@prop("town")
def stone_fountain():
    m = Model("Prop_StoneFountain")
    m.cyl((0, 0, 0), 1.3, 0.45, shade("#9a9488"), sides=12)
    m.cyl((0, 0, 0.4), 1.15, 0.04, (0.35, 0.55, 0.62), M_SHINY, sides=12)
    m.cyl((0, 0, 0.4), 0.18, 0.9, shade("#9a9488"), sides=8)
    m.cyl((0, 0, 1.3), 0.6, 0.15, shade("#9a9488"), sides=12, top=0.4)
    m.cyl((0, 0, 1.4), 0.36, 0.03, (0.35, 0.55, 0.62), M_SHINY, sides=12)
    # a frog on top spitting water
    m.blob((0, 0, 1.6), (0.18, 0.16, 0.14), shade("#7ab85a"))
    for sx in (1, -1):
        m.blob((sx * 0.07, -0.09, 1.72), (0.05, 0.05, 0.05), shade("#7ab85a"))
        m.blob((sx * 0.07, -0.13, 1.73), (0.025, 0.012, 0.025), "#111", M_SHINY)
    m.tube([(0, -0.16, 1.6), (0, -0.4, 1.55), (0, -0.6, 1.2), (0, -0.7, 0.45)], [0.025, 0.03, 0.035, 0.03], (0.6, 0.85, 0.95), M_GLASS)
    for sx in (1, -1):
        m.blob((sx * 0.9, sx * 0.4, 0.45), (0.12, 0.12, 0.02), hsv(0.3, 0.5, 0.45))   # lily pads
    return m


@prop("town")
def market_stall():
    m = Model("Prop_MarketStall")
    m.box((0, 0, 0.45), (2.0, 0.9, 0.9), shade(WOOD), bevel=0.02)
    m.box((0, -0.05, 0.92), (2.1, 1.0, 0.06), shade(WOOD_DK))
    for sx in (1, -1):
        for sy in (1, -1):
            m.cyl((sx * 0.95, sy * 0.42, 0.9), 0.04, 1.3, shade(WOOD_DK), sides=6)

    def stripes(p, n):
        return rgb(PINK) if int((p.x + 2) * 3.3) % 2 else rgb(CREAM)
    m.box((0, -0.25, 2.3), (2.3, 1.5, 0.05), stripes, rot=(-15, 0, 0))
    for k in range(7):
        x = -1.05 + k * 0.35
        m.blob((x, -1.0, 2.1), (0.17, 0.05, 0.12), stripes)   # scalloped edge
    rnd = random.Random(21)
    for k in range(12):
        m.blob((rnd.uniform(-0.85, 0.85), rnd.uniform(-0.3, 0.3), 1.0), (0.08, 0.08, 0.07),
               hsv(rnd.choice([0.0, 0.08, 0.3, 0.85]), 0.6, 0.85))
    return m


@prop("town")
def picket_fence():
    m = Model("Prop_PicketFence")
    for k in range(9):
        x = -1.6 + k * 0.4
        f = m.plate([(-0.07, 0), (0.07, 0), (0.07, 0.85), (0, 0.98), (-0.07, 0.85)], -0.02, 0.04,
                    shade(hsv([0.85, 0.12, 0.55][k % 3], 0.25, 0.92)))
        m.rotate_last(f, (0, 0, 0), (90, 0, 0))
        m.move(f, (x, 0, 0))
    for z in (0.25, 0.65):
        m.box((0, 0.03, z), (3.4, 0.04, 0.08), shade(WOOD))
    m.tube([(-1.6, -0.05, 0.9), (-1.3, -0.08, 0.6), (-0.9, -0.06, 0.8), (-0.5, -0.08, 0.5)], 0.02, shade(MOSS))
    for x in (-1.3, -0.7):
        m.blob((x, -0.1, 0.62), (0.05, 0.05, 0.05), PINK)
    return m


@prop("town")
def well_bucket():
    m = Model("Prop_WellBucket")
    m.cyl((0, 0, 0), 0.7, 0.8, shade("#8a8278"), sides=12)
    m.cyl((0, 0, 0.78), 0.55, 0.03, (0.12, 0.2, 0.25), M_SHINY, sides=12)
    for sx in (1, -1):
        m.box((sx * 0.62, 0, 1.3), (0.1, 0.1, 1.1), shade(WOOD_DK))
    m.tube([(-0.62, 0, 1.7), (0.62, 0, 1.7)], 0.04, shade(WOOD))
    f = m.box((0, 0, 2.05), (1.6, 1.2, 0.06), shade(PLUM), rot=(0, 25, 0))
    m.box((0, 0, 2.05), (1.6, 1.2, 0.06), shade(PLUM), rot=(0, -25, 0))
    m.tube([(0.1, 0, 1.7), (0.1, 0, 1.1)], 0.008, "#d8c8a0")
    m.cyl((0.1, 0, 0.95), 0.12, 0.16, shade(WOOD), sides=8, top=0.14)
    return m


@prop("town")
def notice_board():
    m = Model("Prop_NoticeBoard")
    for sx in (1, -1):
        m.cyl((sx * 0.7, 0, 0), 0.05, 2.0, shade(WOOD_DK), sides=6)
    m.box((0, 0, 1.35), (1.5, 0.08, 1.0), shade(WOOD), bevel=0.02)
    m.box((0, -0.02, 1.95), (1.7, 0.4, 0.06), shade(PLUM_DK), rot=(-15, 0, 0))
    rnd = random.Random(22)
    for k in range(7):
        x, z = rnd.uniform(-0.55, 0.55), rnd.uniform(1.0, 1.7)
        m.box((x, -0.05, z), (rnd.uniform(0.18, 0.3), 0.01, rnd.uniform(0.2, 0.3)),
              rnd.choice([CREAM, "#f8e8a0", "#d8f0d0", "#f8d8e8"]), rot=(0, rnd.uniform(-10, 10), 0))
        m.blob((x, -0.07, z + 0.1), (0.015, 0.01, 0.015), "#e0354a")
    return m


@prop("town")
def bug_hotel():
    m = Model("Prop_BugHotel")
    m.cyl((0, 0, 0), 0.06, 1.0, shade(WOOD_DK), sides=6)
    m.box((0, 0, 1.35), (0.7, 0.3, 0.7), shade(WOOD), bevel=0.01)
    f = m.box((0, 0, 1.78), (0.85, 0.4, 0.05), shade(MOSS_DK), rot=(0, 28, 0))
    m.box((0, 0, 1.78), (0.85, 0.4, 0.05), shade(MOSS_DK), rot=(0, -28, 0))
    rnd = random.Random(23)
    for i in range(5):
        for j in range(5):
            x, z = -0.26 + i * 0.13, 1.09 + j * 0.13
            m.cyl((x, -0.12, z), 0.05, 0.05, shade(rnd.choice(["#c8a070", "#a07850", "#d8c8a0"])), rot=(90, 0, 0), sides=8)
            m.cyl((x, -0.165, z), 0.02, 0.01, "#2a1a14", rot=(90, 0, 0), sides=6)
    m.blob((0.15, -0.18, 1.3), (0.03, 0.04, 0.025), "#e0354a", M_SHINY)   # resident ladybug
    return m


# ================================================================ fashion (the tailor shop)

@prop("fashion")
def dress_mannequin():
    m = Model("Prop_DressMannequin")
    m.cyl((0, 0, 0), 0.22, 0.04, shade(WOOD_DK), sides=8)
    m.cyl((0, 0, 0), 0.02, 0.9, shade(WOOD_DK), sides=6)
    m.blob((0, 0, 1.2), (0.17, 0.12, 0.3), shade(CREAM), seg=10, rings=8)
    m.cyl((0, 0, 1.48), 0.04, 0.08, shade(WOOD_DK), sides=6)

    def plaid(p, n):
        s = (int((p.x + 1) * 25) % 3 == 0) or (int(p.z * 25) % 3 == 0)
        return hsv(0.78, 0.4, 0.55 if s else 0.75)
    m.cyl((0, 0, 0.55), 0.32, 0.5, plaid, sides=12, top=0.16)
    m.box((0, -0.12, 1.15), (0.18, 0.02, 0.25), shade(PINK))
    m.plate(heart_outline(0.05), 0, 0.01, PINK)
    return m


@prop("fashion")
def clothes_rack():
    m = Model("Prop_ClothesRack")
    for sx in (1, -1):
        m.tube([(sx * 0.7, 0, 0), (sx * 0.7, 0, 1.6)], 0.025, "#c8c8d0")
        m.tube([(sx * 0.7, -0.25, 0.02), (sx * 0.7, 0.25, 0.02)], 0.025, "#c8c8d0")
    m.tube([(-0.7, 0, 1.6), (0.7, 0, 1.6)], 0.02, "#c8c8d0")
    rnd = random.Random(31)
    for k in range(9):
        x = -0.55 + k * 0.14
        hue = rnd.choice([0.78, 0.3, 0.92, 0.12, 0.55])
        m.torus((x, 0, 1.58), 0.03, 0.006, "#888", rot=(90, 0, 0), seg=6)
        m.box((x, 0, 1.2), (0.06, 0.38, 0.62), shade(hsv(hue, 0.45, 0.75)))
    return m


@prop("fashion")
def sewing_machine():
    m = Model("Prop_SewingMachine")
    m.box((0, 0, 0.04), (0.5, 0.25, 0.08), shade(WOOD))
    m.box((0.15, 0, 0.22), (0.12, 0.18, 0.3), shade("#2a2a32"), bevel=0.02)
    m.box((-0.03, 0, 0.36), (0.42, 0.16, 0.1), shade("#2a2a32"), bevel=0.02)
    m.box((-0.2, 0, 0.27), (0.05, 0.05, 0.1), shade("#2a2a32"))
    m.cyl((0.22, 0.12, 0.3), 0.07, 0.03, GOLD, M_SHINY, rot=(90, 0, 0), sides=10)
    m.cyl((0.02, 0, 0.42), 0.04, 0.05, hsv(0.9, 0.6, 0.8), sides=8)   # thread spool
    for k in range(4):
        m.tube([(-0.25 + k * 0.03, -0.05, 0.36), (-0.23 + k * 0.03, -0.12, 0.36)], 0.003, GOLD, M_SHINY)
    return m


@prop("fashion")
def fabric_bolts():
    m = Model("Prop_FabricBolts")
    for k, hue in enumerate((0.78, 0.3, 0.92, 0.55, 0.12)):
        m.cyl((-0.4 + k * 0.2, 0, 0), 0.09, 0.9, lambda p, n, hue=hue: hsv(hue, 0.45, 0.6 + 0.3 * ((int(p.z * 20)) % 2)), sides=8)
    return m


@prop("fashion")
def full_length_mirror():
    m = Model("Prop_FullMirror")
    m.blob((0, 0, 0.95), (0.35, 0.04, 0.8), shade(GOLD), M_SHINY, seg=12, rings=10)
    m.blob((0, -0.02, 0.95), (0.3, 0.03, 0.72), (0.75, 0.85, 0.95), M_SHINY, seg=12, rings=10)
    for sx in (1, -1):
        m.tube([(sx * 0.2, 0.1, 0), (sx * 0.15, 0.02, 0.5)], 0.02, shade(WOOD_DK))
    for k in range(5):
        a = k * math.pi / 4
        m.blob((math.cos(a) * 0.36, -0.04, 1.65 + math.sin(a) * 0.1), (0.05, 0.02, 0.05), PINK)
    return m


# ================================================================ gothic (vampires live here too)

@prop("gothic")
def velvet_coffin_bed():
    m = Model("Prop_CoffinBed")
    hexo = [(-0.35, -1.0), (0.35, -1.0), (0.45, 0.5), (0.3, 1.0), (-0.3, 1.0), (-0.45, 0.5)]
    m.plate(hexo, 0, 0.45, shade("#2a1a24"))
    inner = [(x * 0.82, y * 0.92) for x, y in hexo]
    m.plate(inner, 0.3, 0.16, shade("#8a1a3a"))
    m.blob((0, 0.7, 0.5), (0.2, 0.12, 0.06), shade("#c03050"))
    m.plate(heart_outline(0.1), 0.46, 0.01, "#c03050")
    return m


@prop("gothic")
def bat_candelabra():
    m = Model("Prop_BatCandelabra")
    m.cyl((0, 0, 0), 0.15, 0.04, shade("#2a2430"), sides=8)
    m.tube([(0, 0, 0), (0, 0, 0.6)], 0.025, shade("#2a2430"))
    for sx in (-1, 0, 1):
        x = sx * 0.2
        m.tube([(0, 0, 0.45), (x, 0, 0.55), (x, 0, 0.62)], 0.015, shade("#2a2430"))
        m.cyl((x, 0, 0.62), 0.03, 0.18, CREAM, sides=8)
        m.blob((x, 0, 0.83), (0.012, 0.012, 0.03), "#ffb84a", M_GLOW)
    wing = [(0, 0), (0.15, 0.06), (0.22, 0.0), (0.18, -0.04), (0.12, -0.02), (0.06, -0.05)]
    for sx in (1, -1):
        f = m.plate([(sx * x, y) for x, y in wing], 0, 0.01, "#1a1420")
        m.rotate_last(f, (0, 0, 0), (90, 0, 0))
        m.move(f, (0, 0, 0.4))
    m.blob((0, -0.01, 0.4), (0.04, 0.03, 0.04), "#1a1420")
    for sx in (1, -1):
        m.blob((sx * 0.012, -0.04, 0.41), (0.006, 0.004, 0.006), "#e0304a", M_GLOW)
    return m


@prop("gothic")
def witch_cauldron():
    m = Model("Prop_WitchCauldron")
    m.blob((0, 0, 0.4), (0.5, 0.5, 0.4), shade("#2a2a30"), M_SHINY, seg=14, rings=8)
    m.torus((0, 0, 0.72), 0.42, 0.04, shade("#3a3a42"), seg=16)
    m.cyl((0, 0, 0.7), 0.4, 0.03, hsv(0.38, 0.7, 0.8), M_GLOW, sides=14)
    rnd = random.Random(41)
    for k in range(6):
        m.blob((rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25), 0.78 + rnd.uniform(0, 0.3)), (0.05,) * 3, hsv(0.38, 0.5, 0.9), M_GLOW)
    for k in range(3):
        a = k * 2 * math.pi / 3
        m.cyl((math.cos(a) * 0.35, math.sin(a) * 0.35, 0), 0.05, 0.12, shade("#2a2a30"), sides=6)
    for k in range(5):
        a = k * 1.2
        m.blob((math.cos(a) * 0.55, math.sin(a) * 0.55, 0.05), (0.12, 0.05, 0.05), shade(WOOD_DK))
        m.blob((math.cos(a) * 0.45, math.sin(a) * 0.45, 0.1), (0.05, 0.05, 0.08), "#ff7a2a", M_GLOW)
    return m


@prop("gothic")
def spooky_tree_lamp():
    m = Model("Prop_SpookyTreeLamp")
    m.tube([(0, 0, 0), (0.05, 0, 1.0), (-0.05, 0, 1.8), (0.0, 0, 2.3)], [0.12, 0.09, 0.07, 0.04], shade("#3a2e2a"))
    for k, (a, h) in enumerate(((0.5, 1.4), (2.8, 1.7), (4.5, 2.0))):
        tip = (math.cos(a) * 0.6, math.sin(a) * 0.6, h + 0.3)
        m.tube([(0, 0, h), (tip[0] * 0.6, tip[1] * 0.6, h + 0.25), tip], [0.04, 0.025, 0.01], shade("#3a2e2a"))
        m.tube([tip, (tip[0], tip[1], tip[2] - 0.2)], 0.004, "#222")
        m.blob((tip[0], tip[1], tip[2] - 0.3), (0.07, 0.07, 0.1), hsv([0.78, 0.5, 0.12][k], 0.5, 1.0), M_GLOW)
    return m


@prop("gothic")
def grave_with_flowers():
    m = Model("Prop_GraveFlowers")
    stone = [(-0.3, 0), (0.3, 0), (0.3, 0.6), (0.2, 0.75), (0, 0.8), (-0.2, 0.75), (-0.3, 0.6)]
    f = m.plate(stone, -0.08, 0.16, shade("#8a8680"))
    m.rotate_last(f, (0, 0, 0), (90, 0, 0))
    m.box((0, 0.3, 0.02), (0.6, 0.8, 0.04), shade(MOSS_DK))
    m.box((0, -0.09, 0.5), (0.3, 0.01, 0.04), "#5a5650")
    m.box((0, -0.09, 0.42), (0.2, 0.01, 0.04), "#5a5650")
    rnd = random.Random(42)
    for k in range(8):
        x, y = rnd.uniform(-0.25, 0.25), rnd.uniform(0.0, 0.6)
        m.tube([(x, y, 0.04), (x, y, 0.2)], 0.006, MOSS)
        m.blob((x, y, 0.22), (0.035, 0.035, 0.025), hsv(rnd.choice([0.85, 0.78, 0.12]), 0.5, 0.95))
    m.blob((-0.22, -0.05, 0.75), (0.1, 0.06, 0.04), shade(MOSS))
    return m


# ================================================================ tools + items

@prop("items")
def bug_net():
    m = Model("Prop_BugNet")
    m.tube([(0, 0, 0), (0, 0, 1.2)], 0.018, shade(WOOD))
    m.torus((0, 0, 1.4), 0.2, 0.012, "#c8c8d0", rot=(90, 0, 0), seg=14)
    m.blob((0, 0.08, 1.4), (0.19, 0.25, 0.19), (0.95, 0.95, 0.9), M_GLASS, seg=10, rings=6)
    return m


@prop("items")
def watering_can():
    m = Model("Prop_WateringCan")
    m.cyl((0, 0, 0), 0.15, 0.28, shade(TEAL), M_SHINY, sides=12)
    m.tube([(0.12, 0, 0.08), (0.3, 0, 0.3), (0.38, 0, 0.36)], [0.025, 0.02, 0.018], shade(TEAL), M_SHINY)
    m.cyl((0.38, 0, 0.34), 0.04, 0.04, shade(TEAL), M_SHINY, sides=8, rot=(0, 50, 0))
    m.tube([(-0.12, 0, 0.26), (-0.05, 0, 0.42), (0.08, 0, 0.42), (0.12, 0, 0.27)], 0.015, shade(TEAL), M_SHINY)
    m.plate(heart_outline(0.05), 0, 0.005, PINK)
    return m


@prop("items")
def coin_and_gems():
    m = Model("Prop_CoinsGems")
    rnd = random.Random(51)
    for k in range(10):
        x, y = rnd.uniform(-0.2, 0.2), rnd.uniform(-0.2, 0.2)
        m.cyl((x, y, k * 0.012), 0.06, 0.015, shade(GOLD), M_SHINY, sides=10)
    for k, hue in enumerate((0.8, 0.5, 0.95)):
        a = k * 2.1
        f = m.cyl((math.cos(a) * 0.15, math.sin(a) * 0.15, 0.03), 0.05, 0.07, hsv(hue, 0.6, 0.9), M_GLOW, sides=6, top=0.0)
        m.cyl((math.cos(a) * 0.15, math.sin(a) * 0.15, 0.03), 0.05, 0.03, hsv(hue, 0.6, 0.9), M_GLOW, sides=6, top=0.0, rot=(180, 0, 0))
    return m


@prop("items")
def old_key():
    m = Model("Prop_OldKey")
    m.torus((0, 0, 0), 0.06, 0.015, shade(GOLD), M_SHINY, rot=(90, 0, 0), seg=12)
    m.tube([(0.06, 0, 0), (0.3, 0, 0)], 0.014, shade(GOLD), M_SHINY)
    m.box((0.26, 0, -0.03), (0.02, 0.02, 0.05), shade(GOLD), M_SHINY)
    m.box((0.22, 0, -0.025), (0.02, 0.02, 0.04), shade(GOLD), M_SHINY)
    m.plate(heart_outline(0.03), -0.005, 0.01, PINK)
    return m


@prop("items")
def scroll_and_quill():
    m = Model("Prop_ScrollQuill")
    m.cyl((-0.2, 0, 0.03), 0.03, 0.4, shade(CREAM), rot=(0, 90, 0), sides=10)
    m.box((0.02, 0, 0.005), (0.4, 0.3, 0.01), shade("#f0e4c8"))
    m.cyl((0.22, 0, 0.03), 0.03, 0.4, shade(CREAM), rot=(0, 90, 0), sides=10)
    for k in range(4):
        m.box((0.0, -0.08 + k * 0.05, 0.012), (0.28, 0.008, 0.002), "#5a4a6a")
    f = m.plate([(0, 0), (0.04, 0.12), (0.02, 0.3), (-0.01, 0.12)], 0, 0.004, shade(LILAC))
    m.rotate_last(f, (0, 0, 0), (60, 0, -30))
    m.move(f, Vector((0.15, 0.1, 0.02)))
    return m


@prop("items")
def honey_jar():
    m = Model("Prop_HoneyJar")
    m.cyl((0, 0, 0), 0.1, 0.18, hsv(0.1, 0.8, 0.85), M_GLASS, sides=10)
    m.cyl((0, 0, 0.01), 0.09, 0.14, hsv(0.1, 0.85, 0.9), M_GLOW, sides=10)
    m.cyl((0, 0, 0.18), 0.11, 0.04, shade("#c8a8e0"), sides=10)
    m.blob((0, 0, 0.23), (0.11, 0.11, 0.02), shade("#c8a8e0"))
    m.box((0, -0.1, 0.09), (0.1, 0.01, 0.06), CREAM)
    m.blob((0.0, -0.11, 0.09), (0.025, 0.008, 0.02), hsv(0.12, 0.8, 0.9))   # little bee on the label
    return m


# ================================================================ more weapons

@prop("weapons")
def mantis_scythe():
    m = Model("Prop_MantisScythe")
    m.tube([(0, 0, 0), (0.02, 0, 0.8), (0, 0, 1.45)], [0.026, 0.022, 0.026], shade("#4a5a2a"))
    blade = [(0, 0, 1.42), (-0.15, 0, 1.5), (-0.35, 0, 1.5), (-0.52, 0, 1.4), (-0.62, 0, 1.22)]
    m.tube(blade, [0.05, 0.055, 0.045, 0.03, 0.006], lambda p, n: hsv(0.24, 0.45, 0.45 + 0.4 * max(0.0, n.z)), M_SHINY, sides=4)
    for k in range(6):   # spines along the inner edge, like a mantis foreleg
        x = -0.1 - k * 0.08
        z = 1.47 - 0.02 * k * k * 0.3
        m.cyl((x, 0, z - 0.03), 0.012, 0.06, "#2a2416", sides=4, top=0.0, rot=(180, 0, 0))
    m.blob((0, 0, 1.42), (0.05, 0.05, 0.05), "#2a2416")
    return m


@prop("weapons")
def umbrella_sword():
    m = Model("Prop_UmbrellaSword")
    m.tube([(0, 0, 0), (0, 0, 1.1)], 0.015, "#c8c8d0")
    m.cyl((0, 0, 0.25), 0.12, 0.85, lambda p, n: hsv(0.8 if int((math.atan2(p.y, p.x) + 4) * 2) % 2 else 0.9, 0.45, 0.8), sides=8, top=0.02)
    m.tube([(0, 0, 0.25), (0, 0, 0.0), (0.05, 0, -0.08), (0.1, 0, -0.03)], 0.025, shade(WOOD))
    return m


@prop("weapons")
def frying_pan():
    m = Model("Prop_FryingPan")
    m.cyl((0, 0, 0), 0.22, 0.05, shade("#2a2a30"), M_SHINY, sides=14)
    m.cyl((0, 0, 0.01), 0.19, 0.045, shade("#3a3a44"), M_SHINY, sides=14)
    m.tube([(0.22, 0, 0.03), (0.6, 0, 0.06)], 0.025, shade(WOOD_DK))
    m.blob((0.03, 0.02, 0.06), (0.06, 0.06, 0.012), CREAM)      # egg white
    m.blob((0.03, 0.02, 0.07), (0.025, 0.025, 0.015), "#ffb820", M_SHINY)
    return m


@prop("weapons")
def slingshot():
    m = Model("Prop_Slingshot")
    m.tube([(0, 0, 0), (0, 0, 0.18), (-0.08, 0, 0.32)], 0.025, shade(WOOD))
    m.tube([(0, 0, 0.18), (0.08, 0, 0.32)], 0.025, shade(WOOD))
    m.tube([(-0.08, 0, 0.32), (0, -0.06, 0.26), (0.08, 0, 0.32)], 0.006, PINK)
    m.box((0, -0.06, 0.26), (0.05, 0.01, 0.03), shade(WOOD_DK))
    return m


# crafted wooden weapons: the wood parts are pale and neutral so the game can tint them with the wood
# they were made from (oak, birch, Mistpine fir...); wraps and crystals keep their own colors.
WOOD_NEUTRAL = "#eadcc8"


@prop("weapons")
def wood_club():
    m = Model("Prop_WoodClub")
    m.tube([(0, 0, 0), (0, 0, 0.3), (0.01, 0, 0.62), (0, 0, 0.8)], [0.026, 0.03, 0.06, 0.07], WOOD_NEUTRAL, sides=8)
    m.blob((0, 0, 0.8), (0.07, 0.07, 0.05), WOOD_NEUTRAL, seg=8, rings=5)
    for z, a in ((0.5, 0.4), (0.66, 2.6), (0.72, 4.4)):   # knots
        m.blob((math.cos(a) * 0.055, math.sin(a) * 0.055, z), (0.018, 0.018, 0.022), shade("#8a6a4a"))
    for k in range(4):   # thread grip wrap
        m.torus((0, 0, 0.05 + k * 0.045), 0.028, 0.008, CREAM, seg=8)
    return m


@prop("weapons")
def wood_staff():
    m = Model("Prop_WoodStaff")
    m.tube([(0, 0, 0), (0.015, 0, 0.7), (-0.015, 0, 1.3), (0, 0, 1.5)], [0.022, 0.022, 0.02, 0.024], WOOD_NEUTRAL, sides=7)
    # a curled crook holding a small dream crystal
    m.tube([(0, 0, 1.5), (0.08, 0, 1.6), (0.05, 0, 1.72), (-0.04, 0, 1.7), (-0.06, 0, 1.6)], 0.016, WOOD_NEUTRAL, sides=6)
    m.cyl((0, 0, 1.56), 0.04, 0.12, hsv(0.78, 0.5, 0.95), M_GLOW, sides=6, top=0.0)
    m.cyl((0, 0, 1.56), 0.04, 0.05, hsv(0.78, 0.5, 0.95), M_GLOW, sides=6, top=0.0, rot=(180, 0, 0))
    for k in range(3):
        m.torus((0, 0, 0.75 + k * 0.04), 0.026, 0.007, CREAM, seg=8)
    return m


# ================================================================ home + garden (bases)

@prop("home")
def welcome_doormat():
    m = Model("Prop_WelcomeMat")
    m.box((0, 0, 0.01), (0.9, 0.55, 0.02), shade("#b08a5a"), bevel=0.005)
    for k in range(5):
        m.box((-0.3 + k * 0.15, 0, 0.022), (0.06, 0.3, 0.005), hsv(0.85, 0.4, 0.7))
    m.plate(heart_outline(0.06), 0.025, 0.004, PINK)
    return m


@prop("home")
def flower_window_box():
    m = Model("Prop_FlowerWindowBox")
    m.box((0, 0, 0.12), (1.0, 0.25, 0.24), shade(PLUM), bevel=0.01)
    m.box((0, 0, 0.23), (0.94, 0.2, 0.03), shade("#4a3324"))
    rnd = random.Random(61)
    for k in range(12):
        x = -0.42 + k * 0.077
        h = rnd.uniform(0.15, 0.32)
        m.tube([(x, 0, 0.24), (x + rnd.uniform(-0.03, 0.03), 0, 0.24 + h)], 0.008, MOSS)
        m.blob((x, 0, 0.24 + h), (0.04, 0.04, 0.03), hsv(rnd.choice([0.85, 0.12, 0.6, 0.78]), 0.55, 0.95))
    return m


@prop("home")
def garden_gnome_vampire():
    m = Model("Prop_VampireGnome")
    m.blob((0, 0, 0.18), (0.13, 0.11, 0.18), shade("#2a1a2e"))
    m.blob((0, 0, 0.38), (0.09, 0.09, 0.09), shade("#e8e0ea"))
    m.cyl((0, 0, 0.43), 0.1, 0.25, shade("#8a1a3a"), sides=8, top=0.0)
    m.blob((0, -0.05, 0.35), (0.08, 0.05, 0.06), shade("#f4f0f6"))   # beard
    for sx in (1, -1):
        m.blob((sx * 0.035, -0.08, 0.4), (0.012, 0.008, 0.012), "#e0304a", M_GLOW)
    m.cyl((0.015, -0.085, 0.33), 0.006, 0.03, "#fff", sides=4, top=0.0, rot=(180, 0, 0))
    wing = [(0, 0), (0.18, 0.08), (0.26, 0.0), (0.2, -0.05), (0.12, -0.02)]
    for sx in (1, -1):
        f = m.plate([(sx * x, y) for x, y in wing], 0, 0.01, "#2a1a2e")
        m.rotate_last(f, (0, 0, 0), (90, 0, 0))
        m.move(f, Vector((0, 0.06, 0.25)))
    return m


@prop("home")
def vegetable_patch():
    m = Model("Prop_VegetablePatch")
    m.box((0, 0, 0.1), (1.6, 1.0, 0.2), shade(WOOD_DK))
    m.box((0, 0, 0.19), (1.5, 0.9, 0.04), shade("#4a3324"))
    for i in range(4):
        for j in range(2):
            x, y = -0.55 + i * 0.37, -0.22 + j * 0.44
            if (i + j) % 2:
                m.blob((x, y, 0.24), (0.09, 0.09, 0.07), "#f07a2a")       # pumpkin
                m.tube([(x, y, 0.3), (x, y, 0.34)], 0.015, MOSS_DK)
            else:
                m.cyl((x, y, 0.15), 0.035, 0.12, "#f07a2a", sides=6)       # carrot
                for k in range(3):
                    m.tube([(x, y, 0.27), (x + (k - 1) * 0.04, y, 0.42)], 0.01, MOSS)
    return m


@prop("home")
def hammock():
    m = Model("Prop_Hammock")
    for sx in (1, -1):
        m.tube([(sx * 1.4, 0, 0), (sx * 1.4, 0, 1.6)], 0.07, shade(WOOD_DK))
    pts = [(x, 0, 1.2 - 0.5 * (1 - (x / 1.1) ** 2)) for x in [i * 0.22 - 1.1 for i in range(11)]]
    for dy in (-0.25, 0.0, 0.25):
        m.tube([(px, dy, pz) for px, _, pz in pts], 0.04, lambda p, n: hsv(0.8 if int((p.x + 2) * 5) % 2 else 0.3, 0.35, 0.8), sides=5)
    for sx in (1, -1):
        m.tube([(sx * 1.1, 0, 1.2), (sx * 1.4, 0, 1.4)], 0.01, "#d8c8a0")
    return m


@prop("home")
def campfire():
    m = Model("Prop_Campfire")
    for k in range(8):
        a = k * math.pi / 4
        m.blob((math.cos(a) * 0.4, math.sin(a) * 0.4, 0.06), (0.1, 0.09, 0.08), shade("#7a746a"))
    for k in range(4):
        a = k * math.pi / 4
        m.tube([(math.cos(a) * 0.3, math.sin(a) * 0.3, 0.05), (-math.cos(a) * 0.3, -math.sin(a) * 0.3, 0.12)], 0.04, shade(WOOD_DK))
    m.cyl((0, 0, 0.08), 0.18, 0.4, "#ff7a2a", M_GLOW, sides=6, top=0.0)
    m.cyl((0.03, 0.02, 0.1), 0.1, 0.5, "#ffd84a", M_GLOW, sides=6, top=0.0)
    m.tube([(0.35, -0.3, 0.3), (0.0, 0.0, 0.35)], 0.008, shade(WOOD))
    m.blob((0.05, -0.04, 0.36), (0.04, 0.04, 0.04), CREAM)   # toasting marshmallow
    return m


# ================================================================ elf village

@prop("elf")
def leaf_archway():
    m = Model("Prop_LeafArchway")
    for sx in (1, -1):
        m.tube([(sx * 1.0, 0, 0), (sx * 1.05, 0, 1.2), (sx * 0.85, 0, 2.2)], [0.12, 0.1, 0.07], shade(WOOD))
    m.tube([(-0.85, 0, 2.2), (-0.4, 0, 2.6), (0.4, 0, 2.6), (0.85, 0, 2.2)], 0.07, shade(WOOD))
    rnd = random.Random(71)
    leaf = [(0, 0), (0.08, 0.1), (0, 0.25), (-0.08, 0.1)]
    for k in range(26):
        t = k / 25
        a = math.pi * t
        x, z = -math.cos(a) * 0.95, 1.6 + math.sin(a) * 1.0
        f = m.plate(leaf, 0, 0.01, hsv(rnd.choice([0.25, 0.3, 0.12, 0.08]), 0.55, 0.6))
        m.rotate_last(f, (0, 0, 0), (rnd.uniform(-60, 60), rnd.uniform(-60, 60), rnd.uniform(0, 360)))
        m.move(f, Vector((x, rnd.uniform(-0.1, 0.1), z)))
    for x in (-0.5, 0.5):
        m.tube([(x, 0, 2.55), (x, 0, 2.25)], 0.004, "#888")
        m.blob((x, 0, 2.18), (0.06, 0.06, 0.08), hsv(0.3, 0.4, 1.0), M_GLOW)
    return m


@prop("elf")
def acorn_house_door():
    m = Model("Prop_AcornDoor")
    door = [(-0.45, 0), (0.45, 0), (0.45, 1.3), (0.3, 1.7), (0, 1.85), (-0.3, 1.7), (-0.45, 1.3)]
    f = m.plate(door, -0.05, 0.1, lambda p, n: hsv(0.07, 0.5, 0.45 + 0.1 * (int((p.x + 1) * 10) % 2)))
    m.rotate_last(f, (0, 0, 0), (90, 0, 0))
    m.cyl((0, -0.06, 1.1), 0.12, 0.05, (0.8, 0.9, 0.95), M_GLASS, rot=(90, 0, 0), sides=10)
    m.blob((0.3, -0.08, 0.85), (0.04, 0.04, 0.04), GOLD, M_SHINY)
    m.blob((0, 0.05, 2.0), (0.6, 0.35, 0.3), shade("#6a4a2a"), seg=12, rings=6)   # acorn cap
    m.tube([(0, 0.05, 2.25), (0.05, 0.05, 2.45)], 0.04, shade("#4a3324"))
    return m


@prop("elf")
def mushroom_ladder_tree():
    m = Model("Prop_ShelfFungusTree")
    m.tube([(0, 0, 0), (0.05, 0, 1.5), (-0.03, 0, 3.0), (0, 0, 3.8)], [0.35, 0.3, 0.25, 0.15], shade("#6a5040"), sides=8)
    for k in range(6):
        a = k * 1.2
        z = 0.5 + k * 0.5
        r = 0.3 - k * 0.02
        m.blob((math.cos(a) * r, math.sin(a) * r, z), (0.25, 0.25, 0.04), shade(hsv(0.08 + 0.02 * k, 0.5, 0.75)))
    for k in range(4):
        a = k * 1.6
        m.blob((math.cos(a) * 0.6, math.sin(a) * 0.6, 3.9), (0.6, 0.6, 0.4), shade(MOSS), seg=8, rings=5)
    return m


@prop("elf")
def moonflower_lamp():
    m = Model("Prop_MoonflowerLamp")
    m.tube([(0, 0, 0), (0.05, 0, 0.8), (0.2, 0, 1.4), (0.35, 0, 1.55)], [0.03, 0.025, 0.02, 0.015], shade(MOSS_DK))
    for k in range(5):
        a = k * 2 * math.pi / 5
        f = m.plate([(0, 0), (0.06, 0.1), (0, 0.22), (-0.06, 0.1)], 0, 0.005, hsv(0.7, 0.2, 1.0), M_GLOW)
        m.rotate_last(f, (0, 0, 0), (-40, 0, math.degrees(a)))
        m.move(f, Vector((0.35, 0, 1.5)))
    m.blob((0.35, 0, 1.52), (0.03, 0.03, 0.03), GOLD, M_GLOW)
    leaf = [(0, 0), (0.1, 0.08), (0.2, 0), (0.1, -0.06)]
    m.plate(leaf, 0.5, 0.01, shade(MOSS))
    return m


# ================================================================ music + games

@prop("fun")
def jukebox():
    m = Model("Prop_Jukebox")
    m.box((0, 0, 0.5), (0.7, 0.45, 1.0), shade(PLUM), bevel=0.03)
    m.blob((0, 0, 1.0), (0.35, 0.225, 0.3), shade(PLUM), seg=12, rings=6)
    m.blob((0, -0.18, 1.05), (0.26, 0.06, 0.2), lambda p, n: hsv(0.1 + p.x, 0.6, 1.0), M_GLOW)
    for k in range(6):
        m.box((-0.2 + k * 0.08, -0.23, 0.55), (0.03, 0.01, 0.3), hsv(0.08 * k, 0.6, 1.0), M_GLOW)
    m.box((0, -0.23, 0.2), (0.5, 0.01, 0.12), shade(GOLD), M_SHINY)
    return m


@prop("fun")
def bug_arcade_cabinet():
    m = Model("Prop_BugArcade")
    m.box((0, 0, 0.8), (0.65, 0.6, 1.6), shade("#2a2440"), bevel=0.02)
    m.box((0, -0.25, 1.15), (0.5, 0.15, 0.4), lambda p, n: hsv(0.33, 0.6, 0.35 + 0.35 * (int(p.x * 40 + p.z * 40) % 2)), M_GLOW, rot=(-15, 0, 0))
    m.box((0, -0.32, 0.85), (0.6, 0.25, 0.06), shade("#3a3460"))
    m.cyl((-0.12, -0.38, 0.88), 0.012, 0.08, "#ccc", sides=6)
    m.blob((-0.12, -0.38, 0.97), (0.03, 0.03, 0.03), "#e0354a", M_SHINY)
    for k in range(3):
        m.cyl((0.05 + k * 0.08, -0.36, 0.88), 0.025, 0.02, hsv(0.15 + 0.3 * k, 0.7, 0.9), M_SHINY, sides=8)
    m.box((0, -0.29, 1.5), (0.6, 0.04, 0.18), lambda p, n: hsv(0.85, 0.6, 1.0), M_GLOW)   # marquee
    m.blob((0, -0.32, 1.5), (0.06, 0.02, 0.04), hsv(0.36, 0.8, 0.5), M_SHINY)          # beetle logo
    return m


@prop("fun")
def toadstool_drum_set():
    m = Model("Prop_ToadstoolDrums")
    for k, (x, y, r, h) in enumerate(((0, 0, 0.3, 0.5), (0.45, 0.2, 0.2, 0.6), (-0.45, 0.2, 0.2, 0.6), (0.3, -0.35, 0.15, 0.4))):
        m.cyl((x, y, 0), r * 0.5, h, shade(CREAM), sides=8)
        m.cyl((x, y, h), r, 0.15, shade(["#d6455a", "#7a4f8f", "#4fb3b0", "#e8c25a"][k]), sides=12)
        m.cyl((x, y, h + 0.15), r * 0.95, 0.01, (0.95, 0.92, 0.85), sides=12)
    m.tube([(-0.6, -0.2, 0.0), (-0.6, -0.2, 1.0)], 0.015, "#ccc")
    m.cyl((-0.6, -0.2, 1.0), 0.22, 0.01, GOLD, M_SHINY, sides=12)
    return m


@prop("fun")
def claw_machine():
    m = Model("Prop_ClawMachine")
    m.box((0, 0, 0.4), (0.8, 0.8, 0.8), shade(PINK), bevel=0.02)
    m.box((0, 0, 1.2), (0.8, 0.8, 0.8), (0.85, 0.92, 0.98), M_GLASS)
    m.box((0, 0, 1.7), (0.86, 0.86, 0.2), shade(PINK), bevel=0.02)
    rnd = random.Random(81)
    for k in range(9):
        m.blob((rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), 0.85 + rnd.uniform(0, 0.08)), (0.08, 0.08, 0.07),
               hsv(rnd.choice([0.36, 0.0, 0.8, 0.12]), 0.6, 0.75), M_SHINY)   # plush beetles
    m.tube([(0, 0, 1.6), (0, 0, 1.35)], 0.008, "#ccc")
    for k in range(3):
        a = k * 2 * math.pi / 3
        m.tube([(0, 0, 1.35), (math.cos(a) * 0.08, math.sin(a) * 0.08, 1.25)], 0.01, "#ccc")
    return m


# ================================================================ vehicles / getting around

@prop("travel")
def beetle_cart():
    m = Model("Prop_BeetleCart")
    m.box((0, -0.3, 0.55), (1.0, 1.2, 0.3), shade(WOOD), bevel=0.02)
    for sx in (1, -1):
        m.cyl((sx * 0.55, -0.3, 0.35), 0.35, 0.08, shade(WOOD_DK), rot=(0, 90, 0), sides=10)
        m.tube([(sx * 0.4, 0.3, 0.55), (sx * 0.3, 1.2, 0.5)], 0.03, shade(WOOD_DK))
    # the beetle pulling it
    m.blob((0, 1.5, 0.45), (0.45, 0.6, 0.35), lambda p, n: hsv(0.36 - 0.3 * max(0, p.y - 1.3), 0.85, 0.35 + 0.5 * max(0, n.z)), M_SHINY, seg=14, rings=8)
    m.blob((0, 2.05, 0.4), (0.25, 0.18, 0.15), shade("#3a2a1a"), M_SHINY)
    for sx in (1, -1):
        m.tube([(sx * 0.1, 2.2, 0.38), (sx * 0.18, 2.4, 0.42), (sx * 0.08, 2.55, 0.5)], [0.04, 0.03, 0.01], "#1a1414", M_SHINY)
        for y in (1.2, 1.5, 1.8):
            m.tube([(sx * 0.4, y, 0.3), (sx * 0.6, y + 0.05, 0.15), (sx * 0.65, y + 0.05, 0.0)], 0.03, "#1a1414")
    m.box((0, -0.3, 0.85), (0.9, 1.1, 0.04), shade(PLUM))
    return m


@prop("travel")
def leaf_boat():
    m = Model("Prop_LeafBoat")
    hull = [(0, -1.2), (0.45, -0.6), (0.5, 0.2), (0.3, 0.9), (0, 1.3), (-0.3, 0.9), (-0.5, 0.2), (-0.45, -0.6)]
    m.plate(hull, 0, 0.06, lambda p, n: hsv(0.28, 0.6, 0.45 + 0.15 * (abs(p.x) < 0.03)))
    for sx in (1, -1):
        m.tube([(sx * 0.42, -0.6, 0.06), (sx * 0.5, 0.2, 0.2), (sx * 0.3, 0.9, 0.18)], 0.04, shade(MOSS_DK))
    m.tube([(0, 0, 0.06), (0, 0, 1.4)], 0.025, shade(WOOD_DK))
    f = m.plate([(0, 0), (0.6, 0.2), (0, 1.0)], 0, 0.01, shade(CREAM))
    m.rotate_last(f, (0, 0, 0), (90, 0, 0))
    m.move(f, (0, 0, 0.35))
    return m


@prop("travel")
def dragonfly_signpost_lamp():
    m = Model("Prop_DragonflyBikeRack")
    for k in range(4):
        x = -0.6 + k * 0.4
        m.tube([(x, 0, 0), (x, 0, 0.5), (x + 0.2, 0, 0.6), (x + 0.4, 0, 0.5), (x + 0.4, 0, 0)], 0.025, "#8a8aa0")
    m.blob((0, 0, 1.1), (0.04, 0.3, 0.04), hsv(0.5, 0.6, 0.6), M_SHINY)
    for sx in (1, -1):
        for y in (0.08, -0.05):
            m.blob((sx * 0.22, y, 1.12), (0.2, 0.05, 0.01), (0.8, 0.95, 0.95), M_GLASS)
    m.tube([(0, 0, 0.6), (0, 0, 1.08)], 0.015, "#8a8aa0")
    return m


# ================================================================ bug life (outdoor critter decor)

@prop("bugs")
def ant_hill():
    m = Model("Prop_AntHill")
    m.cyl((0, 0, 0), 0.6, 0.35, shade("#a07850"), sides=12, top=0.12)
    m.cyl((0, 0, 0.33), 0.08, 0.03, "#2a1a14", sides=8)
    rnd = random.Random(91)
    for k in range(10):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0.2, 0.9)
        x, y = math.cos(a) * r, math.sin(a) * r
        z = max(0.0, 0.35 * (1 - r / 0.6)) + 0.02
        for j in range(3):
            m.blob((x + math.cos(a) * j * 0.025, y + math.sin(a) * j * 0.025, z), (0.015, 0.015, 0.012), "#1a1010", M_SHINY)
    return m


@prop("bugs")
def spiderweb_corner():
    m = Model("Prop_Spiderweb")
    for k in range(7):
        a = k * math.pi / 12
        m.tube([(0, 0, 0), (math.cos(a) * 1.0, 0, math.sin(a) * 1.0)], 0.004, (0.92, 0.92, 0.95))
    for r in (0.25, 0.45, 0.65, 0.85):
        pts = [(math.cos(k * math.pi / 12) * r, 0, math.sin(k * math.pi / 12) * r) for k in range(7)]
        m.tube(pts, 0.003, (0.92, 0.92, 0.95))
    m.blob((0.4, -0.01, 0.35), (0.04, 0.03, 0.05), "#2a2430")
    m.blob((0.4, -0.01, 0.41), (0.025, 0.02, 0.025), "#2a2430")
    for s in (1, -1):
        for k in range(4):
            m.tube([(0.4, -0.01, 0.37), (0.4 + s * 0.07, -0.02, 0.4 - k * 0.03), (0.4 + s * 0.1, -0.02, 0.33 - k * 0.03)], 0.004, "#2a2430")
    m.plate(heart_outline(0.02), 0.44, 0.003, PINK)   # a cute little bow on the spider
    return m


@prop("bugs")
def caterpillar_bench_buddy():
    m = Model("Prop_Caterpillar")
    for k in range(7):
        z = 0.1 + (0.12 if k == 6 else 0.02 * math.sin(k))
        m.blob((-0.36 + k * 0.12, 0, z), (0.08, 0.08, 0.08), hsv(0.3 if k % 2 else 0.25, 0.55, 0.7))
    m.blob((0.4, 0, 0.27), (0.1, 0.1, 0.1), hsv(0.3, 0.45, 0.8))
    for s in (1, -1):
        m.blob((0.47, s * 0.04, 0.3), (0.02, 0.02, 0.025), "#111", M_SHINY)
        m.tube([(0.4, s * 0.03, 0.35), (0.42, s * 0.06, 0.45)], 0.006, "#222")
    m.blob((0.48, 0, 0.24), (0.02, 0.02, 0.01), PINK)
    for k in range(6):
        for s in (1, -1):
            m.blob((-0.36 + k * 0.12, s * 0.07, 0.03), (0.015, 0.015, 0.02), hsv(0.08, 0.6, 0.6))
    return m


@prop("bugs")
def snail_shell_planter():
    m = Model("Prop_SnailShellPlanter")
    pts = []
    for k in range(40):
        t = k / 39 * 3.2 * math.pi
        r = 0.05 + 0.06 * t
        pts.append((math.cos(t) * r * 0.6, 0, 0.35 + math.sin(t) * r * 0.6))
    m.tube(list(reversed(pts)), [0.04 + 0.12 * (1 - k / 39) for k in range(40)][::-1],
           lambda p, n: hsv(0.06 + 0.05 * math.sin(p.x * 30), 0.55, 0.75), sides=8)
    m.blob((0, 0, 0.42), (0.18, 0.18, 0.12), shade("#4a3324"))
    rnd = random.Random(92)
    for k in range(6):
        x, y = rnd.uniform(-0.1, 0.1), rnd.uniform(-0.1, 0.1)
        m.tube([(x, y, 0.5), (x, y, 0.75)], 0.008, MOSS)
        m.blob((x, y, 0.76), (0.035, 0.035, 0.02), hsv(rnd.choice([0.85, 0.12]), 0.5, 0.95))
    return m


# ================================================================ enemies (static drafts; rigged later)

def _eyes(m, center, spread, size, color="#e0304a"):
    for s in (1, -1):
        c = Vector(center) + Vector((s * spread, 0, 0))
        m.blob(c, (size, size * 0.6, size), color, M_GLOW)


@prop("enemies")
def moss_golem():
    """Slow tank of the woods: a walking boulder overgrown with moss and mushrooms."""
    m = Model("Enemy_MossGolem")
    rock = lambda p, n: hsv(0.24, 0.5, 0.3 + 0.25 * n.z) if n.z > 0.35 else hsv(0.08, 0.1, 0.35 + 0.15 * n.z)
    m.blob((0, 0, 1.2), (0.8, 0.6, 0.7), rock, seg=9, rings=6)                       # body
    m.blob((0, -0.35, 1.85), (0.38, 0.32, 0.3), rock, seg=8, rings=5)               # head
    _eyes(m, (0, -0.64, 1.88), 0.13, 0.06, "#c8ff7a")
    for s in (1, -1):
        m.blob((s * 0.95, -0.1, 1.2), (0.3, 0.28, 0.35), rock, seg=8, rings=5)      # shoulders
        m.blob((s * 1.1, -0.2, 0.6), (0.28, 0.26, 0.45), rock, seg=8, rings=5)      # big fists
        m.blob((s * 0.4, 0, 0.35), (0.28, 0.28, 0.35), rock, seg=8, rings=5)        # legs
    rnd = random.Random(101)
    for k in range(5):
        x, y = rnd.uniform(-0.5, 0.5), rnd.uniform(-0.3, 0.3)
        h = rnd.uniform(0.15, 0.35)
        m.cyl((x, y, 1.75), 0.03, h, CREAM, sides=6)
        m.blob((x, y, 1.75 + h), (0.1, 0.1, 0.05), hsv(rnd.choice([0.78, 0.55]), 0.6, 0.9), M_GLOW)
    return m


@prop("enemies")
def glitch_moth():
    """Fast flier near the old tech: a moth whose wings are corrupted pixels."""
    m = Model("Enemy_GlitchMoth")
    m.tube([(0, 0.3, 1.5), (0, 0, 1.5), (0, -0.35, 1.45)], [0.08, 0.1, 0.05], shade("#3a3446"), sides=6)
    m.blob((0, 0.36, 1.52), (0.08, 0.07, 0.07), shade("#3a3446"))
    _eyes(m, (0, 0.42, 1.55), 0.05, 0.03, "#4affd8")
    rnd = random.Random(102)
    for s in (1, -1):
        for i in range(6):
            for j in range(5):
                if rnd.random() < 0.18:
                    continue   # missing pixels
                x = s * (0.1 + i * 0.1 + rnd.uniform(-0.01, 0.03))
                y = 0.25 - j * 0.12
                z = 1.55 + i * 0.04
                hue = rnd.choice([0.5, 0.8, 0.92, 0.33])
                m.box((x, y, z), (0.095, 0.11, 0.01), hsv(hue, 0.6, rnd.uniform(0.5, 1.0)), M_GLOW if rnd.random() < 0.3 else M_MATTE)
        m.tube([(s * 0.03, 0.42, 1.58), (s * 0.15, 0.6, 1.75)], 0.008, "#3a3446")
    return m


@prop("enemies")
def thorn_beetle():
    """Charger: a big beetle with a thorny horn that rams you (roll sideways!)."""
    m = Model("Enemy_ThornBeetle")
    shell = lambda p, n: hsv(0.95 + 0.06 * n.x, 0.65, 0.25 + 0.45 * max(0.0, n.z))
    m.blob((0, -0.1, 0.5), (0.55, 0.75, 0.4), shell, M_SHINY, seg=14, rings=8)
    m.blob((0, 0.6, 0.45), (0.35, 0.25, 0.25), shell, M_SHINY)
    m.tube([(0, 0.75, 0.5), (0, 1.05, 0.75), (0, 1.15, 1.05), (0, 1.05, 1.2)], [0.1, 0.08, 0.05, 0.01], "#2a1418", M_SHINY)
    for k in range(4):
        z = 0.6 + k * 0.15
        m.cyl((0.0, 0.95 + 0.04 * k, z), 0.025, 0.12, "#5a7a2a", sides=4, top=0.0, rot=(-70, 0, 0))
    _eyes(m, (0, 0.82, 0.5), 0.18, 0.05)
    for s in (1, -1):
        for y in (0.4, 0.0, -0.4):
            m.tube([(s * 0.4, y, 0.35), (s * 0.75, y + 0.05, 0.4), (s * 0.85, y + 0.1, 0.0)], [0.06, 0.05, 0.03], "#1a0e10", M_SHINY)
    rnd = random.Random(103)
    for k in range(8):   # thorns growing out of the shell
        x, y = rnd.uniform(-0.4, 0.4), rnd.uniform(-0.6, 0.3)
        m.cyl((x, y, 0.82), 0.03, 0.15, "#5a7a2a", sides=4, top=0.0)
    return m


@prop("enemies")
def bat_vampire_spawn():
    """Nighttime swarm enemy: a fluffy bat with red eyes and tiny fangs."""
    m = Model("Enemy_NightBat")
    m.blob((0, 0, 1.6), (0.18, 0.16, 0.2), shade("#2a2030"))
    m.blob((0, 0.12, 1.72), (0.13, 0.11, 0.12), shade("#2a2030"))
    for s in (1, -1):
        m.cyl((s * 0.07, 0.12, 1.8), 0.04, 0.12, shade("#2a2030"), sides=4, top=0.0)
        m.blob((s * 0.05, 0.22, 1.74), (0.025, 0.012, 0.025), "#ff3050", M_GLOW)
        m.cyl((s * 0.02, 0.23, 1.66), 0.008, 0.04, "#fff", sides=4, top=0.0, rot=(180, 0, 0))
        wing = [(0, 0), (0.3, 0.15), (0.55, 0.05), (0.5, -0.1), (0.38, -0.05), (0.28, -0.15), (0.15, -0.05)]
        f = m.plate([(s * x, y) for x, y in wing], 0, 0.01, "#3a2a40")
        m.rotate_last(f, (0, 0, 0), (90, 0, 0))
        m.move(f, (s * 0.12, 0.0, 1.65))
    m.blob((0, 0.24, 1.69), (0.02, 0.01, 0.015), PINK)   # little nose
    return m


@prop("enemies")
def rot_slug():
    """Slow, leaves a slippery trail; weak but spits goo."""
    m = Model("Enemy_RotSlug")
    m.tube([(0, -0.7, 0.08), (0, -0.3, 0.15), (0, 0.1, 0.2), (0, 0.4, 0.35), (0, 0.55, 0.5)],
           [0.12, 0.22, 0.24, 0.18, 0.13], lambda p, n: hsv(0.2 + 0.05 * math.sin(p.y * 20), 0.55, 0.35 + 0.3 * max(0, n.z)), sides=10)
    for s in (1, -1):
        m.tube([(s * 0.05, 0.55, 0.6), (s * 0.12, 0.65, 0.85)], 0.025, hsv(0.2, 0.5, 0.45))
        m.blob((s * 0.12, 0.66, 0.88), (0.05, 0.05, 0.05), "#e8ff6a", M_GLOW)
    rnd = random.Random(104)
    for k in range(7):
        m.blob((rnd.uniform(-0.15, 0.15), rnd.uniform(-0.5, 0.3), 0.32 + rnd.uniform(0, 0.08)), (0.05, 0.05, 0.03), hsv(0.3, 0.6, 0.6), M_GLOW)
    m.box((0, -1.2, 0.005), (0.3, 1.2, 0.01), (0.6, 0.8, 0.3), M_SHINY)   # slime trail
    return m


@prop("enemies")
def mushroom_knight():
    """Mini-boss of the mushroom woods: armored shroom with a stem sword and cap shield."""
    m = Model("Enemy_MushroomKnight")
    m.cyl((0, 0, 0), 0.3, 1.2, shade(CREAM), sides=10, top=0.25)
    m.blob((0, 0, 1.4), (0.75, 0.75, 0.35), shade("#a02838"), seg=14, rings=7)
    for k in range(7):
        a = k * 0.9
        m.blob((math.cos(a) * 0.5, math.sin(a) * 0.5, 1.55), (0.08, 0.08, 0.04), CREAM)
    m.box((0, -0.26, 0.9), (0.35, 0.06, 0.12), shade("#5a5a66"), M_SHINY)          # visor
    _eyes(m, (0, -0.3, 0.9), 0.08, 0.035, "#ffd84a")
    m.box((0, -0.15, 0.55), (0.62, 0.25, 0.5), shade("#7a7a88"), M_SHINY, bevel=0.03)   # breastplate
    for s in (1, -1):
        m.blob((s * 0.38, 0, 0.75), (0.15, 0.15, 0.12), shade("#7a7a88"), M_SHINY)
        m.cyl((s * 0.15, 0, -0.0), 0.1, 0.25, shade("#5a5a66"), sides=8)
    m.tube([(0.55, -0.2, 0.6), (0.6, -0.4, 1.6)], [0.05, 0.03], shade(CREAM))       # stem sword
    m.box((0.55, -0.22, 0.65), (0.25, 0.06, 0.06), shade(GOLD), M_SHINY)
    m.cyl((-0.65, -0.1, 0.4), 0.38, 0.06, shade("#7a4f8f"), rot=(80, 0, 20), sides=12)   # cap shield
    return m


@prop("enemies")
def static_wisp():
    """A little floating ghost of old TV static that zaps you. Hovers near CRTs."""
    m = Model("Enemy_StaticWisp")
    rnd = random.Random(105)
    for k in range(40):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0, 0.3) * (1.2 - rnd.random() * 0.4)
        z = 1.2 + rnd.uniform(-0.25, 0.35) - r * 0.4
        g = rnd.uniform(0.4, 1.0)
        m.box((math.cos(a) * r, math.sin(a) * r, z), (0.06, 0.06, 0.06), (g, g, g * 1.05), M_GLOW if g > 0.85 else M_MATTE)
    for k in range(6):   # wispy tail
        m.box((rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), 0.9 - k * 0.08), (0.05 - k * 0.006,) * 3, (0.7, 0.7, 0.75))
    _eyes(m, (0, -0.28, 1.35), 0.09, 0.04, "#7affd8")
    m.box((0, -0.3, 1.22), (0.12, 0.02, 0.025), "#1a1a1a")
    return m


@prop("enemies")
def ivy_serpent():
    """Lurks in the trees: a snake made of tangled ivy with a flower mouth."""
    m = Model("Enemy_IvySerpent")
    pts = [(math.sin(t * 2.2) * 0.45, -1.2 + t * 1.6, 0.15 + 0.6 * max(0, t - 0.6) ** 1.5 + 0.05 * math.sin(t * 7)) for t in [i / 16 for i in range(17)]]
    m.tube(pts, [0.06 + 0.1 * math.sin(math.pi * i / 16) for i in range(17)], lambda p, n: hsv(0.3, 0.6, 0.3 + 0.25 * max(0, n.z)), sides=8)
    rnd = random.Random(106)
    leaf = [(0, 0), (0.06, 0.07), (0, 0.16), (-0.06, 0.07)]
    for i in range(1, 16, 1):
        x, y, z = pts[i]
        f = m.plate(leaf, 0, 0.008, hsv(rnd.choice([0.28, 0.32]), 0.6, 0.55))
        m.rotate_last(f, (0, 0, 0), (rnd.uniform(-50, 50), rnd.uniform(-50, 50), rnd.uniform(0, 360)))
        m.move(f, (x, y, z + 0.1))
    hx, hy, hz = pts[-1]
    for k in range(6):
        a = k * math.pi / 3
        f = m.plate([(0, 0), (0.08, 0.1), (0, 0.2), (-0.08, 0.1)], 0, 0.01, hsv(0.9, 0.55, 0.9))
        m.rotate_last(f, (0, 0, 0), (-60, 0, math.degrees(a)))
        m.move(f, (hx, hy + 0.1, hz))
    m.blob((hx, hy + 0.12, hz), (0.05, 0.05, 0.05), "#ffd84a", M_GLOW)
    return m


@prop("enemies")
def cursed_plush():
    """Silly-creepy: an abandoned plush bear that waddles at you with button eyes."""
    m = Model("Enemy_CursedPlush")
    fur = shade("#c8a8b8")
    m.blob((0, 0, 0.45), (0.3, 0.25, 0.35), fur)
    m.blob((0, 0, 0.95), (0.28, 0.25, 0.25), fur)
    for s in (1, -1):
        m.blob((s * 0.2, 0, 1.17), (0.08, 0.05, 0.08), fur)
        m.blob((s * 0.32, -0.05, 0.55), (0.1, 0.1, 0.18), fur, rot=(0, s * 30, 0))
        m.blob((s * 0.15, -0.05, 0.1), (0.1, 0.12, 0.1), fur)
    m.cyl((0.09, -0.24, 1.0), 0.05, 0.02, "#1a1a1a", rot=(90, 0, 0), sides=6)
    m.cyl((-0.09, -0.24, 1.0), 0.04, 0.02, "#e0304a", M_GLOW, rot=(90, 0, 0), sides=6)
    m.tube([(-0.1, -0.24, 0.87), (0, -0.25, 0.84), (0.1, -0.24, 0.87)], 0.008, "#1a1a1a")
    for k in range(4):   # stitches
        m.box((0.0, -0.24, 0.62 - k * 0.07), (0.06, 0.01, 0.008), "#1a1a1a")
    m.tube([(-0.08, 0, 1.2), (-0.12, 0.05, 1.35)], 0.015, "#e8d8a0")   # stuffing poking out
    return m


# ================================================================ build

def main():
    wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    todo = [(c, f) for c, f in PROPS if not wanted or c in wanted]
    objs = []
    for i, (cat, fn) in enumerate(todo):
        model = fn()
        obj = model.finish()
        obj["category"] = cat
        obj.location.x = (i % 10) * 3.0
        obj.location.y = (i // 10) * 3.0
        objs.append((cat, obj))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Props.blend"))

    cam = setup_render(360)
    all_objs = [o for _, o in objs]
    for cat, obj in objs:
        folder = os.path.join(OUT, cat)
        os.makedirs(folder, exist_ok=True)
        render_object(obj, all_objs, cam, os.path.join(folder, obj.name + ".png"))
    with open(os.path.join(OUT, "manifest.txt"), "w") as f:
        for cat, obj in objs:
            f.write(f"{cat}\t{obj.name}\n")
    print("PROPS", len(objs))


main()
