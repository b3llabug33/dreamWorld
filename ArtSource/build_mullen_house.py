"""
The Mullen house (dreamWorld's parody of the Twilight vampire family's glass house in the woods).
Modern three-story stack of offset boxes: concrete base, cedar-clad upper floors that cantilever out,
floor-to-ceiling glass walls, glass balcony rails, a big front deck, flat roofs, tall pines around it.
Inside: open-plan floors you can walk through, a floating staircase, white sofas and a black grand piano.
  blender.exe -b --factory-startup --python ArtSource/build_mullen_house.py
Writes ArtSource/MullenHouse.blend (Bld_MullenHouse + Bld_MullenHouse_Roof) and renders to Design/Buildings/.
Front (entrance) faces +Y. Ground floor at z=0. Footprint about 18 x 12 m.
"""
import math
import os
import random
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from lowpoly import M_GLASS, M_GLOW, M_MATTE, M_SHINY, Model, hsv, render_object, rgb, setup_render, shade  # noqa: E402

ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Design", "Buildings")

CONCRETE, CONCRETE_DK = "#8e8a84", "#66625c"
CEDAR, CEDAR_DK, CEDAR_LT = "#7a4a2a", "#56331c", "#946038"
STEEL = "#2c2a30"
GLASS = (0.62, 0.74, 0.78)
WHITE = "#f0ece4"
WARM = "#ffd69a"
T = 0.2          # wall thickness
H = 3.4          # floor height


def cedar(p, n):
    """Horizontal cedar boards with alternating tones."""
    board = int((p.z + 10) * 7) % 3
    col = [rgb(CEDAR), rgb(CEDAR_DK), rgb(CEDAR_LT)][board]
    k = 0.6 + 0.4 * (0.5 + 0.5 * n.z) + 0.1 * abs(n.x)
    return tuple(min(1.0, c * k) for c in col)


def glass_wall(m, x0, x1, y, z0, z1, mullions=6):
    """A floor-to-ceiling glass wall along X at depth y, with thin steel mullions."""
    m.box(((x0 + x1) / 2, y, (z0 + z1) / 2), (x1 - x0, 0.06, z1 - z0), GLASS, M_GLASS)
    for k in range(mullions + 1):
        x = x0 + (x1 - x0) * k / mullions
        m.box((x, y, (z0 + z1) / 2), (0.06, 0.1, z1 - z0), shade(STEEL), M_SHINY)
    for z in (z0, z1):
        m.box(((x0 + x1) / 2, y, z), (x1 - x0, 0.12, 0.08), shade(STEEL), M_SHINY)


def glass_wall_x(m, x, y0, y1, z0, z1, mullions=4):
    """Same, running along Y at x."""
    m.box((x, (y0 + y1) / 2, (z0 + z1) / 2), (0.06, y1 - y0, z1 - z0), GLASS, M_GLASS)
    for k in range(mullions + 1):
        y = y0 + (y1 - y0) * k / mullions
        m.box((x, y, (z0 + z1) / 2), (0.1, 0.06, z1 - z0), shade(STEEL), M_SHINY)


def railing(m, x0, x1, y, z):
    m.box(((x0 + x1) / 2, y, z + 0.5), (x1 - x0, 0.04, 1.0), GLASS, M_GLASS)
    m.box(((x0 + x1) / 2, y, z + 1.02), (x1 - x0, 0.08, 0.05), shade(STEEL), M_SHINY)


def floor_slab(m, cx, cy, w, d, z, col=CONCRETE_DK):
    m.box((cx, cy, z - 0.15), (w, d, 0.3), shade(col))
    # warm wood floorboards on top (inside)
    m.box((cx, cy, z + 0.005), (w - 0.4, d - 0.4, 0.01),
          lambda p, n: hsv(0.07, 0.45, 0.55 + 0.08 * (int(p.x * 3) % 2)))


def box_room(m, cx, cy, w, d, z, front_glass=True, back_glass=False, side_glass=False, clad=cedar, door_gap=None):
    """One floor volume: floor slab, solid (clad) walls except where glass is asked for."""
    x0, x1, y0, y1 = cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2
    floor_slab(m, cx, cy, w, d, z)
    zt = z + H
    # front (+Y) wall
    if front_glass:
        if door_gap:
            gx0, gx1 = door_gap
            glass_wall(m, x0, gx0, y1, z, zt, 3)
            glass_wall(m, gx1, x1, y1, z, zt, 3)
            m.box(((gx0 + gx1) / 2, y1, zt - 0.4), (gx1 - gx0, 0.12, 0.8), shade(STEEL), M_SHINY)   # header over the door
        else:
            glass_wall(m, x0, x1, y1, z, zt, max(3, int(w / 1.6)))
    else:
        m.box((cx, y1, z + H / 2), (w, T, H), clad)
    # back (-Y) wall
    if back_glass:
        glass_wall(m, x0, x1, y0, z, zt, max(3, int(w / 1.6)))
    else:
        m.box((cx, y0, z + H / 2), (w, T, H), clad)
    # sides
    for x in (x0, x1):
        if side_glass:
            glass_wall_x(m, x, y0, y1, z, zt)
        else:
            m.box((x, cy, z + H / 2), (T, d, H), clad)


def white_sofa(m, x, y, z, yaw=0):
    f = []
    f += m.box((x, y, z + 0.22), (2.0, 0.85, 0.44), shade(WHITE), bevel=0.03)
    f += m.box((x, y - 0.38, z + 0.6), (2.0, 0.18, 0.5), shade(WHITE), bevel=0.03)
    for s in (1, -1):
        f += m.box((x + s * 0.95, y, z + 0.5), (0.18, 0.85, 0.35), shade(WHITE), bevel=0.03)
    f += m.box((x - 0.5, y + 0.05, z + 0.5), (0.4, 0.15, 0.35), shade("#b8443c"), bevel=0.02)   # a red pillow
    m.rotate_last(f, (x, y, z), (0, 0, yaw))


def grand_piano(m, x, y, z):
    black = shade("#141218")
    outline = [(0, 0), (1.5, 0), (1.5, 0.9), (1.2, 1.6), (0.6, 2.0), (0.1, 1.9), (0, 1.2)]
    m.plate([(x - 0.75 + px, y - 1.0 + py) for px, py in outline], z + 0.75, 0.3, black, M_SHINY)
    lid = m.plate([(x - 0.75 + px, y - 1.0 + py) for px, py in outline], z + 1.05, 0.03, black, M_SHINY)
    m.rotate_last(lid, (x + 0.75, y - 1.0, z + 1.05), (0, -35, 0))
    for lx, ly in ((0.2, 0.15), (1.3, 0.15), (0.5, 1.6)):
        m.box((x - 0.75 + lx, y - 1.0 + ly, z + 0.38), (0.1, 0.1, 0.75), black)
    m.box((x, y - 1.05, z + 0.82), (1.4, 0.18, 0.04), shade(WHITE))                 # keys
    for k in range(10):
        m.box((x - 0.6 + k * 0.14, y - 1.04, z + 0.85), (0.04, 0.1, 0.02), black)
    m.box((x, y - 1.45, z + 0.24), (0.9, 0.35, 0.48), black, M_SHINY)                # bench


def floating_stairs(m, x, y0, z0, z1, steps=16, width=1.2):
    for k in range(steps):
        t = k / steps
        m.box((x, y0 + t * 4.0, z0 + (z1 - z0) * (k + 1) / steps - 0.05), (width, 0.32, 0.1), shade(CEDAR_LT))
    m.box((x - width / 2 - 0.02, y0 + 2.0, (z0 + z1) / 2 + 0.5), (0.04, 4.2, (z1 - z0) + 1.0), GLASS, M_GLASS)


def pine(m, x, y, h, rnd):
    m.cyl((x, y, 0), 0.25, h * 0.35, shade("#4a3426"), sides=6)
    for k in range(5):
        r = 1.6 * (1 - k / 6)
        m.cyl((x, y, h * 0.25 + k * h * 0.14), r, h * 0.28, shade(hsv(0.36 + rnd.uniform(-0.02, 0.02), 0.45, 0.28)), sides=7, top=0.15)


def bella_truck(m, x, y, yaw=0):
    """Stella's old faded-red pickup truck (rounded 1950s cab, chrome bumpers, big wheel arches)."""
    red = lambda p, n: tuple(c * (0.55 + 0.45 * (0.5 + 0.5 * n.z)) for c in (0.62, 0.16, 0.14))
    rust = shade("#7a3a20")
    chrome = shade("#d8d8de")
    f = []
    f += m.box((x, y, 0.75), (2.0, 5.0, 0.5), red, bevel=0.06)                       # chassis / body
    f += m.box((x, y + 1.6, 1.15), (1.9, 1.6, 0.5), red, bevel=0.15)                 # long rounded hood
    f += m.blob((x, y + 0.4, 1.5), (0.95, 0.8, 0.65), red, seg=10, rings=6)          # rounded cab
    f += m.box((x, y + 0.75, 1.65), (1.5, 0.05, 0.5), (0.55, 0.65, 0.72), M_GLASS)   # windshield
    for sx in (1, -1):
        f += m.box((x + sx * 0.93, y + 0.35, 1.6), (0.05, 0.9, 0.45), (0.55, 0.65, 0.72), M_GLASS)
    f += m.box((x, y - 1.5, 1.1), (1.9, 2.0, 0.08), rust)                            # truck bed floor
    for sx in (1, -1):
        f += m.box((x + sx * 0.95, y - 1.5, 1.3), (0.08, 2.0, 0.45), red)               # bed walls
    f += m.box((x, y - 2.5, 1.3), (1.9, 0.08, 0.45), red)                             # tailgate
    f += m.box((x, y + 2.45, 0.7), (2.1, 0.15, 0.22), chrome, M_SHINY)                # front bumper
    f += m.box((x, y - 2.55, 0.65), (2.1, 0.12, 0.2), chrome, M_SHINY)               # rear bumper
    f += m.box((x, y + 2.42, 1.0), (1.3, 0.05, 0.4), chrome, M_SHINY)                # grille
    for sx in (1, -1):
        f += m.blob((x + sx * 0.75, y + 2.4, 1.15), (0.14, 0.05, 0.14), "#fff6c8", M_GLOW)   # headlights
        for wy in (1.6, -1.6):
            f += m.cyl((x + sx * 1.02, y + wy, 0.48), 0.48, 0.3, shade("#1a1a1e"), rot=(0, 90, 0), sides=12)   # tires
            f += m.cyl((x + sx * 1.18, y + wy, 0.48), 0.2, 0.04, chrome, M_SHINY, rot=(0, 90, 0), sides=10)     # hubcaps
            f += m.blob((x + sx * 0.98, y + wy, 0.85), (0.15, 0.6, 0.35), red)                                 # fender arches
    f += m.blob((x - 0.4, y + 2.46, 0.95), (0.12, 0.03, 0.1), rust)                  # a little rust patch
    m.rotate_last(f, (x, y, 0), (0, 0, yaw))


def stacked_books(m, x, y, z, n, rnd):
    for k in range(n):
        m.box((x + rnd.uniform(-0.03, 0.03), y, z + 0.035 + k * 0.07), (0.32, 0.24, 0.065),
              hsv(rnd.choice([0.0, 0.02, 0.6, 0.75, 0.1]), 0.5, 0.45), rot=(0, 0, rnd.uniform(-12, 12)))


def apple_book(m, x, y, z):
    """The famous cover: a black book with hands holding a red apple."""
    m.box((x, y, z + 0.02), (0.36, 0.5, 0.04), shade("#121014"))
    m.blob((x, y + 0.02, z + 0.05), (0.07, 0.07, 0.03), "#c8202c", M_SHINY)
    m.box((x, y - 0.07, z + 0.045), (0.16, 0.06, 0.01), shade("#e8dcd0"))         # the cupped hands


def baseball_bat_rack(m, x, y):
    m.box((x, y, 1.0), (0.9, 0.12, 0.06), shade(CEDAR_DK))
    for k in range(3):
        f = m.cyl((x - 0.3 + k * 0.3, y + 0.05, 0.1), 0.035, 1.0, shade("#c8a070"), sides=6, top=0.05)
    m.blob((x + 0.55, y + 0.1, 0.12), (0.06, 0.06, 0.06), shade("#f4f0e8"))         # a baseball
    m.box((x + 0.55, y + 0.1, 0.12), (0.01, 0.01, 0.12), "#c8202c")                 # stitching


def graduation_cap_wall(m, x, y, z, rnd):
    """The wall of graduation caps (they've graduated a LOT of times)."""
    for i in range(6):
        for j in range(4):
            cx, cz = x - 1.25 + i * 0.5, z + 0.5 + j * 0.45
            m.box((cx, y, cz), (0.32, 0.04, 0.32), shade("#141218"), rot=(0, 45, 0))
            m.tube([(cx, y - 0.03, cz), (cx + 0.08, y - 0.04, cz - 0.15)], 0.008, hsv(rnd.choice([0.12, 0.0, 0.6]), 0.7, 0.8))


def vase_flowers(m, x, y, z):
    m.cyl((x, y, z), 0.09, 0.32, (0.7, 0.85, 0.9), M_GLASS, sides=8, top=0.06)
    for k in range(5):
        a = k * 1.25
        m.tube([(x, y, z + 0.3), (x + math.cos(a) * 0.12, y + math.sin(a) * 0.12, z + 0.6)], 0.006, shade("#4a6a2a"))
        m.blob((x + math.cos(a) * 0.12, y + math.sin(a) * 0.12, z + 0.62), (0.05, 0.05, 0.04), hsv(0.92, 0.35, 0.95))


def record_shelf(m, x, y, rnd):
    m.box((x, y, 0.6), (1.8, 0.35, 1.2), shade(CEDAR_DK))
    for k in range(22):
        m.box((x - 0.82 + k * 0.078, y - 0.05, 0.62), (0.03, 0.3, 0.3), hsv(rnd.random(), 0.35, 0.5))
    m.cyl((x - 0.5, y, 1.2), 0.2, 0.06, shade("#2a2a30"), sides=10)                   # record player
    m.cyl((x - 0.5, y, 1.26), 0.15, 0.01, shade("#101010"), M_SHINY, sides=12)


def cozy_rug(m, x, y, z, w, d):
    m.box((x, y, z + 0.01), (w, d, 0.02), lambda p, n: (0.82, 0.78, 0.72) if (int(p.x * 4) + int(p.y * 4)) % 2 else (0.72, 0.68, 0.62))


def build():
    house = Model("Bld_MullenHouse")
    roof = Model("Bld_MullenHouse_Roof")
    rnd = random.Random(7)

    # ---- ground floor: concrete base, glass front with the entrance, garage block on the left
    box_room(house, 0, 0, 14, 9, 0, front_glass=True, back_glass=True, clad=shade(CONCRETE), door_gap=(-1.0, 1.0))
    house.box((-9.0, 0.5, 1.6), (4.0, 8.0, 3.2), shade(CONCRETE))            # garage
    house.box((-9.0, 4.55, 1.3), (3.2, 0.08, 2.6), shade(CEDAR_DK))           # garage door
    # ---- second floor: cedar box, cantilevered 2.5 m out over the deck, glass on front and right side
    box_room(house, 1.5, 1.2, 15, 8.5, H, front_glass=True, back_glass=False, side_glass=True)
    railing(house, -6.0, 9.0, 5.5, H)                                          # balcony in front of the glass
    floor_slab(house, 1.5, 5.0, 15, 1.2, H)                                    # balcony floor
    # ---- third floor: smaller box shifted back and left (the stacked, staggered look)
    box_room(house, -1.5, -1.0, 10, 7, 2 * H, front_glass=True, back_glass=True)
    railing(house, -6.5, 3.5, 2.6, 2 * H)
    floor_slab(house, -1.5, 2.0, 10, 1.2, 2 * H)

    # ---- flat roofs with a thin dark edge (separate object so it can be hidden for interiors)
    for cx, cy, w, d, z in ((1.5, 1.2, 15.4, 8.9, 2 * H), (-1.5, -1.0, 10.4, 7.4, 3 * H), (-9.0, 0.5, 4.2, 8.2, 3.2)):
        roof.box((cx, cy, z + 0.12), (w, d, 0.25), shade(CONCRETE_DK))
        roof.box((cx, cy, z + 0.27), (w + 0.1, d + 0.1, 0.06), shade(STEEL), M_SHINY)
    roof.blob((-3.0, -2.0, 3 * H + 0.35), (1.6, 1.2, 0.15), shade("#5a7a3a"))  # a little moss on the roof

    # ---- front deck + steps up to the door
    house.box((0, 7.0, 0.15), (12, 4.0, 0.3), cedar)
    for k in range(3):
        house.box((0, 9.2 + k * 0.35, 0.1 - k * 0.04), (3.0, 0.35, 0.2), shade(CONCRETE))
    railing(house, -6.0, -2.0, 9.0, 0.3)
    railing(house, 2.0, 6.0, 9.0, 0.3)

    # ---- warm light inside (glow strips under each ceiling) + furniture
    for z in (H - 0.1, 2 * H - 0.1, 3 * H - 0.1):
        house.box((0, 0, z), (8, 0.15, 0.06), WARM, M_GLOW)
        for x in (-4.0, 0.0, 4.0):   # round pendant lights
            house.tube([(x, 1.5, z), (x, 1.5, z - 0.8)], 0.01, shade(STEEL))
            house.blob((x, 1.5, z - 0.9), (0.18, 0.18, 0.12), WARM, M_GLOW)
    white_sofa(house, -2.5, -1.5, 0.0)
    white_sofa(house, -2.5, 1.5, 0.0, yaw=180)
    house.box((-2.5, 0.0, 0.2), (1.4, 0.8, 0.4), shade("#3a3434"), M_SHINY)   # coffee table
    house.box((-2.5, 0.0, 0.42), (0.3, 0.3, 0.05), "#e03040")                 # an apple. of course.
    grand_piano(house, 3.5, 1.0, 0.0)
    floating_stairs(house, 5.8, -3.8, 0.0, H)
    floating_stairs(house, -4.8, -3.6, H, 2 * H)
    # second floor: a long bookshelf wall and a bed (that nobody sleeps in)
    for k in range(8):
        house.box((-5.5 + k * 1.3, -2.8, H + 1.2), (1.2, 0.4, 2.4), shade(CEDAR_DK))
        for j in range(3):
            house.box((-5.5 + k * 1.3, -2.65, H + 0.5 + j * 0.75), (1.0, 0.1, 0.5),
                      hsv(rnd.choice([0.0, 0.08, 0.6, 0.75]), 0.4, 0.45))
    house.box((5.0, -1.0, H + 0.3), (2.2, 2.6, 0.5), shade(WHITE), bevel=0.04)
    house.box((5.0, -2.3, H + 0.75), (2.2, 0.15, 0.9), shade("#141218"))
    # third floor: Alyss's closet of a room. clothes racks everywhere
    for k in range(4):
        x = -4.5 + k * 2.0
        house.tube([(x - 0.6, -3.0, 2 * H + 1.6), (x + 0.6, -3.0, 2 * H + 1.6)], 0.03, shade(STEEL))
        for j in range(6):
            house.box((x - 0.5 + j * 0.2, -3.0, 2 * H + 1.1), (0.08, 0.45, 0.85), hsv(rnd.random(), 0.45, 0.7))


    # ---- movie details (cute edition)
    cozy_rug(house, -2.5, 0.0, 0.0, 4.2, 3.6)
    apple_book(house, -2.7, 0.1, 0.42)
    stacked_books(house, -2.2, -0.15, 0.4, 3, rnd)
    vase_flowers(house, -1.9, 0.25, 0.4)
    house.box((3.5, 1.4, 1.1), (0.4, 0.02, 0.28), shade("#f4ead8"))                  # sheet music on the piano
    vase_flowers(house, 4.6, 0.7, 1.05)
    record_shelf(house, 5.5, 3.6, rnd)                                                # Edwerd's music wall
    baseball_bat_rack(house, -6.3, 3.5)                                               # thunderstorm baseball
    graduation_cap_wall(house, 1.0, -3.95, H, rnd)                                    # second floor hallway
    for k in range(6):                                                                # stacks of books everywhere
        stacked_books(house, -5.2 + k * 1.8, -1.8, H, rnd.randint(2, 5), rnd)
    stacked_books(house, 4.2, -0.3, H + 0.55, 4, rnd)                                 # on the bed
    house.blob((5.0, 0.2, H + 0.6), (0.35, 0.25, 0.1), shade("#c8202c"))            # a red pillow on the bed
    for k in range(3):                                                                # candles by the windows
        house.cyl((-6.0 + k * 0.25, 3.8, 0.0), 0.05, 0.25 + 0.1 * k, shade("#f4ead8"), sides=6)
        house.blob((-6.0 + k * 0.25, 3.8, 0.3 + 0.1 * k), (0.02, 0.02, 0.04), "#ffb84a", M_GLOW)
    # Stella's red truck parked out front, a little crooked
    bella_truck(house, 7.5, 12.5, yaw=-12)

    # ---- the forest around it: tall pines (separate object so the game can use its own trees instead)
    trees = Model("Bld_MullenHouse_Pines")
    for k in range(14):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(11, 17)
        if math.sin(a) > 0.55:      # leave the front lawn/driveway open
            continue
        pine(trees, math.cos(a) * r, math.sin(a) * r - 1.0, rnd.uniform(11, 16), rnd)
    return house.finish(), roof.finish(), trees.finish()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    house, roof, trees = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "MullenHouse.blend"))
    os.makedirs(OUT, exist_ok=True)
    cam = setup_render(640, bg=(0.42, 0.47, 0.52))     # an overcast Forks-y sky
    scene = bpy.context.scene
    everything = [house, roof, trees]
    # the entrance faces +Y, so the camera sits on the +Y side (yaw 180 in render_object's convention)
    views = [("q", 205, 16, [house, roof, trees], 1.25), ("front", 180, 6, [house, roof], 1.2),
             ("side", 250, 10, [house, roof], 1.2), ("cutaway", 210, 42, [house], 1.2)]
    for label, yaw, pitch, show, pad in views:
        for o in everything:
            o.hide_render = o not in show
        # frame the house (not the trees) so it stays big in the shot
        render_object(house, [], cam, os.path.join(OUT, f"Bld_MullenHouse_{label}.png"), yaw=yaw, pitch=pitch, pad=pad * (1.6 if label == "q" else 1.0))
        for o in everything:
            o.hide_render = o not in show
    # interior close-ups: no roof, no upper floors in the way (hide everything above the ground floor
    # by rendering a camera low and inside the glass, looking at the living room)
    from mathutils import Vector
    for o in everything:
        o.hide_render = o is not house
    cam.data.type = "PERSP"
    cam.data.lens = 20
    for light in [o for o in scene.objects if o.type == "LIGHT"]:
        light.data.energy = 4.5
    pt = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", "POINT"))
    pt.data.energy = 1800
    pt.data.color = (1.0, 0.85, 0.7)
    pt.location = (0, 0, H - 0.6)
    scene.collection.objects.link(pt)
    shots = [("living", (-6.2, 3.6, 1.6), (0.5, -0.5, 0.7)), ("piano", (0.5, 3.8, 1.7), (3.8, 0.4, 0.8)),
             ("upstairs", (-5.0, 3.6, H + 1.7), (1.0, -3.6, H + 1.4))]
    for label, pos, look in shots:
        cam.location = Vector(pos)
        cam.rotation_euler = (Vector(look) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, f"Bld_MullenHouse_in_{label}.png")
        bpy.ops.render.render(write_still=True)
    print("MULLEN HOUSE OK")


main()
