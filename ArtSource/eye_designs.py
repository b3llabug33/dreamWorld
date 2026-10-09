"""
Parametric cartoon face designs for Dreamer, drawn as pixel art into the 5-layer face atlas:
  0 face   - brows, nose, mouth, blush (always shown)
  1 base   - eye whites, outline, lid line, lashes, wing
  2 iris   - grayscale; Unity tints it with the eye color
  3 top    - pupil and shine
  4 closed - blink pose (hidden until Unity swaps it in)
Shared by build_dreamer.py (the real faces) and face_drafts.py (preview sheets).

An eye's shape comes from an upper and lower edge curve over t = -1..1 (inner -> outer corner):
  edge(t) = height * (1 - |t|^p)^q    p=2,q=0.5 -> ellipse, p=2,q=1 -> almond, p=1,q=1 -> diamond
plus `tilt` (outer corner lift), `hood` (flattens the top), or shape="hex" for a faceted PS1 eye.
"""
import math

import numpy as np

PX = 128          # texels per layer
EYE_Y = 66        # eye center row

LINE = (0.16, 0.11, 0.18)
SOFT = (0.40, 0.28, 0.36)
WHITE = (0.99, 0.98, 1.0)
PUPIL = (0.10, 0.07, 0.14)
BLUSH = (0.97, 0.62, 0.68)
MOUTH = (0.55, 0.28, 0.32)
LIP = (0.92, 0.62, 0.64)

BASE = dict(sp=34, hw=20, hu=15, hl=15, p=2.0, q=0.5, tilt=0.0, hood=0.0, shape="curve",
            iris=0.8, outline=2, lid=4, lashes=0, wing=0, lower=False, shine=3.6,
            pupil="round", dot=False, mouth="smile", nose="dots", brow="soft", blush="round")

# --- the faces in the game (first one is the default) -----------------------------------
FACES = {
    "Almond":  dict(hw=20, hu=15, hl=15, iris=0.8, lid=4, lashes=2, shine=3.6, mouth="smile", brow="soft"),   # draft E
    "Round":   dict(hw=19, hu=20, hl=20, iris=0.72, outline=2, lid=3, shine=4.2, mouth="smile", brow="arc"),  # draft A
    "Glossy":  dict(hw=12, hu=16, hl=16, dot=True, shine=4.6, mouth="cat", brow="dash"),                      # draft B
    "Doe":     dict(hw=18, hu=21, hl=21, iris=0.84, lid=4, lashes=3, shine=4.2, mouth="smile", brow="arc"),   # draft C
    "Sparkly": dict(hw=18, hu=19, hl=19, iris=0.88, lid=3, shine=4.6, pupil="star", mouth="open", brow="arc"),  # draft D
    "Toon":    dict(sp=33, hw=17, hu=22, hl=22, iris=0.55, outline=2, lid=3, shine=3.4, mouth="smirk", brow="arc"),  # draft F
}

# --- second round of drafts: angular, almond-family, and a few unique ideas ---------------
# (picked ones are added to FACES below)
DRAFTS_2 = {
    "G  cat eye":       dict(hu=14, hl=10, q=1.0, tilt=6, lid=4, wing=8, lower=True, iris=0.9, mouth="smirk", brow="angled"),
    "H  hooded":        dict(hu=15, hl=12, q=0.8, hood=0.42, lid=5, iris=0.95, shine=3.0, mouth="flat", brow="straight"),
    "I  sharp diamond": dict(hw=19, hu=13, hl=11, p=1.0, q=1.0, lid=3, iris=0.85, shine=3.0, mouth="smile", brow="angled"),
    "J  upturned":      dict(hu=13, hl=11, q=0.8, tilt=4, lid=4, lashes=1, iris=0.9, mouth="pout", brow="soft"),
    "K  puppy":         dict(hu=14, hl=12, q=0.7, tilt=-5, lid=4, lower=True, iris=0.9, mouth="smile", brow="sad"),
    "L  pixel hex":     dict(hu=14, hl=12, shape="hex", lid=3, iris=0.85, shine=3.0, mouth="flat", brow="straight"),
    "M  monolid":       dict(hw=21, hu=10, hl=13, q=0.25, lid=4, iris=1.0, shine=3.0, mouth="smile", brow="straight"),
    "N  starry almond": dict(hu=14, hl=13, q=0.7, lid=4, lashes=2, iris=0.9, pupil="star", shine=2.6, mouth="cat", brow="soft", blush="lines"),
    "O  slit cat":      dict(hu=13, hl=11, q=1.0, tilt=4, lid=4, wing=5, iris=0.95, pupil="slit", shine=2.6, mouth="smirk", brow="angled"),
}


def design(params):
    d = dict(BASE)
    d.update(params)
    return d


def draw(params):
    """Returns the 5-layer atlas (PX x 5*PX x 4, float RGBA, row 0 = bottom)."""
    d = design(params)
    L = [np.zeros((PX, PX, 4), np.float32) for _ in range(5)]

    def put(layer, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < PX and 0 <= y < PX:
            L[layer][y, x] = (*c, 1)

    def ellipse(layer, cx, cy, rx, ry, c, clip=None):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1 and (clip is None or clip(x, y)):
                    put(layer, x, y, c)

    hw, hu, hl = d["hw"], d["hu"], d["hl"]

    def edges(t):
        """(upper, lower) edge heights at t in [-1, 1] (t > 0 = outer corner)."""
        a = abs(t)
        if a > 1:
            return None
        if d["shape"] == "hex":           # flat top and bottom with cut corners
            k = min(1.0, (1 - a) / 0.38)
            up, lo = hu * k, -hl * k
        else:
            f = max(0.0, 1 - a ** d["p"]) ** d["q"]
            up, lo = hu * f, -hl * f
        if d["hood"]:
            up = min(up, hu * (1 - d["hood"]))
        lift = d["tilt"] * t
        return up + lift, lo + lift

    def inside(dx, dy):
        e = edges(dx / hw)
        return e is not None and e[1] <= dy <= e[0]

    for side in (-1, 1):
        cx, cy = 64 + side * d["sp"], EYE_Y
        X = lambda dx: cx + dx * side                  # dx measured toward this eye's outer corner
        iris_r = d["iris"] * min(hu, hl) * (1.05 if d["hood"] else 1.0)
        iy = -1.5 + (d["tilt"] * 0.15)

        for dy in range(-hl - 2 - abs(int(d["tilt"])), hu + 3 + abs(int(d["tilt"]))):
            for dx in range(-hw - 1, hw + 2):
                if not inside(dx, dy):
                    continue
                if d["dot"]:   # glossy dot eyes: the whole eye is a dark tinted iris
                    g = 0.55 if dy < -hl * 0.3 else 0.30
                    put(2, X(dx), cy + dy, (g, g, g))
                    continue
                put(1, X(dx), cy + dy, WHITE)
                # iris disc clipped to the eye shape, light at the bottom
                ix, iy2 = dx / iris_r, (dy - iy) / (iris_r * 1.08)
                if ix * ix + iy2 * iy2 <= 1:
                    edge = ix * ix + iy2 * iy2 > 0.80
                    g = 0.42 if edge else (0.95 if iy2 < -0.35 else 0.70)
                    put(2, X(dx), cy + dy, (g, g, g))
                    # pupil
                    pr = iris_r * 0.42
                    px_, py_ = dx / pr, (dy - iy) / (pr * 1.1)
                    hit = False
                    if d["pupil"] == "round":
                        hit = px_ * px_ + py_ * py_ <= 1
                    elif d["pupil"] == "slit":
                        hit = abs(dx) <= 1.2 * (1 - min(1, abs(dy - iy) / (iris_r * 0.95))) + 0.4 and abs(dy - iy) < iris_r * 0.95
                    elif d["pupil"] == "star":
                        ang = math.atan2(dy - iy, dx)
                        r = math.hypot(dx, dy - iy)
                        hit = r <= pr * (0.55 + 0.45 * abs(math.cos(2.5 * ang)))
                    if hit:
                        put(3, X(dx), cy + dy, PUPIL)
        if not d["dot"]:
            # outline + heavier upper lid line, following the shape's edges
            for dx in range(-hw - 1, hw + 2):
                e = edges(dx / hw)
                if e is None:
                    continue
                up, lo = e
                for k in range(d["lid"]):
                    put(1, X(dx), cy + up - k, LINE)
                if d["outline"]:
                    put(1, X(dx), cy + lo, LINE if dx > -hw * 0.6 else SOFT)
                    if d["outline"] > 1:
                        put(1, X(dx), cy + lo + 1, SOFT)
                if d["lower"] and dx > hw * 0.15:
                    put(1, X(dx), cy + lo - 1, LINE)
            # corners
            for t in (-1, 1):
                e = edges(t * 0.98)
                for yy in range(int(e[1]), int(e[0]) + 1):
                    put(1, X(t * hw), cy + yy, LINE)
            # wing liner from the outer corner
            if d["wing"]:
                _, corner = hw, edges(0.98)[0]
                for k in range(d["wing"]):
                    for w in range(max(1, 3 - k // 3)):
                        put(1, X(hw + k), cy + corner - 1 + k * 0.55 - w, LINE)
            # little outer lashes
            for n in range(d["lashes"]):
                base_t = 0.55 + n * 0.22
                e = edges(base_t)
                for k in range(4):
                    put(1, X(base_t * hw + k * 0.6), cy + e[0] + 1 + k, LINE)
        # shine
        r = d["shine"]
        sx, sy = (-hw * 0.25, hu * 0.35) if not d["dot"] else (-hw * 0.25, hu * 0.3)
        ellipse(3, X(sx), cy + sy + iy, r, r * 1.05, WHITE, clip=lambda x, y, cy=cy, X=X, side=side: inside((x - cx) * side, y - cy))
        ellipse(3, X(hw * 0.3), cy - hl * 0.4, r * 0.4, r * 0.4, WHITE, clip=lambda x, y, cy=cy, side=side: inside((x - cx) * side, y - cy))
        # closed lids (blink): a soft line along the lower half of the eye shape
        for dx in range(-hw, hw + 1):
            e = edges(dx / hw)
            y = (e[0] + e[1]) / 2 - 1 - (e[0] - e[1]) * 0.12
            put(4, X(dx), cy + y, LINE)
            put(4, X(dx), cy + y + 1, LINE)
        # brows (the game draws brows as their own piece; drafts still sketch them here)
        top = cy + max(edges(t / 10)[0] for t in range(-10, 11)) + 6
        for dx in range(-9, 10) if d["brow"] != "none" else ():
            t = dx / 9
            y = {"arc": (1 - t * t) * 3, "soft": (1 - t * t) * 1.6, "straight": 0, "dash": 0,
                 "angled": 2.5 - abs(t - 0.3) * 2.5 + 1, "sad": -t * 2.5}[d["brow"]]
            if d["brow"] == "dash" and abs(dx) > 5:
                continue
            for k in range(2):
                put(0, X(dx), top + y + k, (0.30, 0.22, 0.26))

    # blush, nose, mouth
    for side in (-1, 1):
        bx, by = 64 + side * 43, EYE_Y - d["hl"] - 9
        if d["blush"] == "none":
            continue
        if d["blush"] == "round":
            ellipse(0, bx, by, 7, 3.5, BLUSH)
        elif d["blush"] == "lines":
            for k in (-4, 0, 4):
                for j in range(3):
                    put(0, bx + k + j, by + j, BLUSH)
    draw_nose(d["nose"], put, ellipse)
    draw_mouth(d["mouth"], put, ellipse)
    return np.concatenate(L, axis=1)


# Round 2 picks join the eye menu (names as they show in the game)
for _draft, _name in (("J  upturned", "Upturned"), ("N  starry almond", "Starry"), ("G  cat eye", "CatEye"),
                      ("M  monolid", "Monolid"), ("L  pixel hex", "PixelHex"), ("K  puppy", "Puppy")):
    FACES[_name] = DRAFTS_2[_draft]


# ---------------------------------------------------------------- noses and mouths
# Drawn on the face layer around column 64: nose near row 27-34, mouth near row 8-16.

SHADE = (0.85, 0.63, 0.58)
SHADE_DARK = (0.72, 0.48, 0.45)
TEETH = (0.98, 0.96, 0.94)
TONGUE = (0.95, 0.52, 0.58)
FRECKLE = (0.80, 0.55, 0.45)

NOSES = ("dots", "button", "line", "hook", "triangle", "nostrils", "none")   # freckles moved to the extras
MOUTHS = ("smile", "tiny", "flat", "smirk", "smug", "pout", "cat", "open", "o", "grin", "tongue",
          "fang", "lips", "wavy", "kiss")


def draw_nose(style, put, ellipse):
    if style == "dots":            # two shadow pixels (the original)
        put(0, 63, 27, SHADE); put(0, 64, 26, SHADE)
    elif style == "button":        # small round tip with a highlight
        ellipse(0, 64, 27, 2.6, 1.8, SHADE)
        put(0, 63, 28, (0.97, 0.80, 0.74))
    elif style == "line":          # simple vertical line
        for y in range(26, 33):
            put(0, 63, y, SHADE)
    elif style == "hook":          # anime-style "L" shadow on the side
        for y in range(27, 34):
            put(0, 62, y, SHADE)
        for x in range(62, 66):
            put(0, x, 27, SHADE_DARK)
    elif style == "triangle":      # small shaded triangle under the tip
        for row, (x0, x1) in enumerate(((62, 66), (63, 65), (64, 64))):
            for x in range(x0, x1 + 1):
                put(0, x, 26 + row, SHADE)
    elif style == "nostrils":
        put(0, 62, 26, SHADE_DARK); put(0, 66, 26, SHADE_DARK)
    # "none": nothing


def draw_mouth(style, put, ellipse):
    def curve(half, y0, depth, tilt=0.0, color=MOUTH):
        for dx in range(-half, half + 1):
            put(0, 64 + dx, y0 - (1 - (dx / half) ** 2) * depth + dx * tilt, color)

    if style == "smile":
        curve(5, 13, 2)
    elif style == "tiny":
        curve(3, 12, 1)
    elif style == "flat":
        curve(4, 12, 0)
    elif style == "smirk":
        curve(5, 13, 1.5, 0.18)
    elif style == "smug":          # one-sided curl up
        for dx in range(-4, 6):
            put(0, 64 + dx, 12 + max(0, dx - 2) * 0.7, MOUTH)
    elif style == "pout":
        curve(3, 12, 1)
        ellipse(0, 64, 10, 2.5, 1.0, LIP)
    elif style == "cat":           # ":3"
        for dx in range(-6, 7):
            put(0, 64 + dx, 13 - abs(math.sin(dx / 6 * math.pi)) * 2, MOUTH)
    elif style == "open":
        ellipse(0, 64, 12, 4, 2.5, MOUTH)
        ellipse(0, 64, 11, 2.5, 1.2, TONGUE)
    elif style == "o":
        ellipse(0, 64, 12, 2.2, 2.4, MOUTH)
        ellipse(0, 64, 12, 1.0, 1.2, (0.40, 0.18, 0.22))
    elif style == "grin":          # wide smile showing teeth
        for dx in range(-7, 8):
            top = 14 - (1 - (dx / 7) ** 2) * 1.2
            bottom = 14 - (1 - (dx / 7) ** 2) * 4.2
            for y in range(int(bottom), int(top) + 1):
                put(0, 64 + dx, y, TEETH)
            put(0, 64 + dx, top, MOUTH)
            put(0, 64 + dx, bottom, MOUTH)
    elif style == "tongue":        # cheeky smile with the tongue poking out
        curve(5, 13, 2)
        ellipse(0, 65, 9.5, 2, 1.8, TONGUE)
    elif style == "fang":          # small smile with one little fang (grunge/vampire)
        curve(5, 13, 1.5)
        put(0, 66, 11, TEETH); put(0, 66, 10, TEETH); put(0, 67, 11, TEETH)
    elif style == "lips":          # filled lips (lipstick look)
        ellipse(0, 64, 12.5, 5, 1.6, LIP)
        ellipse(0, 64, 10.5, 4, 1.6, (0.86, 0.48, 0.55))
        curve(4, 11.5, 0.6, color=MOUTH)
    elif style == "wavy":          # nervous squiggle
        for dx in range(-5, 6):
            put(0, 64 + dx, 12 + math.sin(dx * 1.3) * 1.0, MOUTH)
    elif style == "kiss":          # little pucker
        ellipse(0, 64, 12, 2, 1.6, LIP)
        put(0, 64, 12, MOUTH)


def draw_part(nose=None, mouth=None):
    """A single 128x128 layer with just a nose or just a mouth (separate creator options)."""
    layer = np.zeros((PX, PX, 4), np.float32)

    def put(_, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < PX and 0 <= y < PX:
            layer[y, x] = (*c, 1)

    def ellipse(_, cx, cy, rx, ry, c, clip=None):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1:
                    put(0, x, y, c)

    if nose:
        draw_nose(nose, put, ellipse)
    if mouth:
        draw_mouth(mouth, put, ellipse)
    return layer


# ---------------------------------------------------------------- brows and extras
# Their own layers on the face, drawn in light grays so Unity can tint them (brow color,
# blush pink, piercing silver...). Symmetric pieces are drawn on both sides; the game moves
# them with "spacing", one-sided ones with "across".

BROW_Y = 97       # brow row at the default eye position (eye top is about row 81)
BROW_X = 34       # brow center offset from the middle column (same as the eyes)
INK = (0.96, 0.96, 0.96)
INK_SOFT = (0.80, 0.80, 0.80)

# style -> (half length, curve(t) in pixels for t in -1..1 inner->outer, thickness inner, thickness outer)
BROWS = {
    "Soft":     (11, lambda t: (1 - t * t) * 2.0, 4, 3),
    "Arched":   (11, lambda t: (1 - t * t) * 4.0, 4, 3),
    "Straight": (11, lambda t: 0.4 * t, 4, 4),
    "Thick":    (12, lambda t: (1 - t * t) * 1.8, 6, 5),
    "Thin":     (10, lambda t: (1 - t * t) * 2.4, 3, 2),
    "Angled":   (11, lambda t: 3.6 - abs(t - 0.35) * 4.0, 5, 3),
    "Worried":  (10, lambda t: -t * 3.0, 4, 3),
    "Dots":     (0, None, 0, 0),          # little round "maro" brows
}


def draw_brows(style):
    layer = np.zeros((PX, PX, 4), np.float32)

    def put(x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < PX and 0 <= y < PX:
            layer[y, x] = (*c, 1)

    half, curve, w_in, w_out = BROWS[style]
    for side in (-1, 1):
        cx = 64 + side * BROW_X
        if style == "Dots":
            for dy in range(-3, 4):
                for dx in range(-4, 5):
                    if (dx / 4.6) ** 2 + (dy / 3.2) ** 2 <= 1:
                        put(cx + dx * side - 3 * side, BROW_Y + 2 + dy, INK)
            continue
        for dx in range(-half, half + 1):
            t = dx / max(half, 1)
            y = BROW_Y + curve(t)
            thick = round(w_in + (w_out - w_in) * (t + 1) / 2)
            if abs(t) > 0.85:
                thick = max(1, thick - 1)            # tapered ends
            for k in range(thick):
                put(cx + dx * side, y - k, INK if k < thick - 1 or thick == 1 else INK_SOFT)
    return layer


SILVER = (0.92, 0.92, 0.94)
SHADOW = (0.55, 0.55, 0.58)
EXTRAS = ("Blush", "BlushLines", "HeartBlush", "Freckles", "Mole", "Sparkles", "UnderEye", "TearTattoo",
          "Bandaid", "NoseStud", "NoseRing", "Septum", "LipRing", "BrowPiercing")


def draw_extra(style):
    layer = np.zeros((PX, PX, 4), np.float32)

    def put(x, y, c=INK):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < PX and 0 <= y < PX:
            layer[y, x] = (*c, 1)

    def blob(cx, cy, rx, ry, c=INK):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1:
                    put(x, y, c)

    def ring(cx, cy, r):
        for a in range(0, 360, 10):   # shaded underside so the hoop reads against light skin
            y = math.sin(math.radians(a))
            put(cx + r * math.cos(math.radians(a)), cy + r * y, SILVER if y > 0 else SHADOW)
        put(cx - r * 0.5, cy + r * 0.7, (1, 1, 1))

    def stud(cx, cy):
        blob(cx, cy, 1.4, 1.4, SHADOW)
        blob(cx, cy + 0.4, 1.0, 1.0, SILVER)
        put(cx - 0.5, cy + 0.5, (1, 1, 1))

    for side in (-1, 1):                     # symmetric ones: both cheeks
        bx, by = 64 + side * 42, 42
        if style == "Blush":
            blob(bx, by, 7, 3.5)
        elif style == "BlushLines":
            for k in (-4, 0, 4):
                for j in range(3):
                    put(bx + k + j, by + j)
        elif style == "HeartBlush":
            for y in range(-3, 4):
                for x in range(-4, 5):
                    X, Y = x / 3.4, y / 3.0 - 0.15
                    if (X * X + Y * Y - 1) ** 3 - X * X * Y ** 3 <= 0:
                        put(bx + x, by + y)
        elif style == "Freckles":
            for x, y in ((18, 4), (14, 6), (11, 3), (7, 7), (4, 5), (16, 0), (9, 1)):
                put(64 + side * x, 36 + y)
            if side == 1:
                put(64, 39); put(62, 41)   # across the bridge too
        elif style == "Sparkles":       # little star stickers under the outer eye corners
            sx, sy = 64 + side * 46, 50
            for d in range(-3, 4):
                put(sx + d, sy); put(sx, sy + d)
            for d in (-1, 1):
                put(sx + d, sy + d); put(sx + d, sy - d)
            sx2, sy2 = sx + side * 6, sy - 6
            for d in range(-1, 2):
                put(sx2 + d, sy2); put(sx2, sy2 + d)
        elif style == "UnderEye":       # tired/grunge under-eye shadow
            for dx in range(-12, 13):
                put(64 + side * (34 + dx), 49 - (1 - (dx / 12) ** 2) * 1.5, INK_SOFT)
    if style == "Mole":
        blob(78, 22, 1.2, 1.2)
    elif style == "TearTattoo":
        for y in range(-2, 4):
            w = max(0, 2 - abs(y - 0.5) * 0.6) if y < 1 else (3 - y) * 0.6
            for x in range(-int(w), int(w) + 1):
                put(96 + x, 46 + y)
    elif style == "Bandaid":            # across the nose bridge, slightly tilted
        for dx in range(-12, 13):
            for dy in range(-3, 4):
                c = INK_SOFT if abs(dx) < 4 and abs(dy) < 2 else INK
                put(64 + dx, 40 + dy + dx * 0.12, c)
    elif style == "NoseStud":
        stud(68, 27)
    elif style == "NoseRing":
        ring(68, 25, 2.2)
    elif style == "Septum":         # just the lower half of a hoop, as if it comes out of the nose
        for a in range(190, 351, 8):
            x, y = 64 + 2.6 * math.cos(math.radians(a)), 25 + 2.6 * math.sin(math.radians(a))
            put(x, y, SILVER if y < 23.5 else SHADOW)
        put(63, 22.4, (1, 1, 1))
    elif style == "LipRing":
        ring(68, 8, 2.2)
    elif style == "BrowPiercing":
        stud(106, 102); stud(107, 93)   # through the outer end of the brow
    return layer
