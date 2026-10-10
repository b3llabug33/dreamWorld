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
        for face in f:
            for v in face.verts:
                v.co.x += math.cos(a) * r
                v.co.y += math.sin(a) * r
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
    for face in m.bm.faces:
        pass
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
