"""
More body types for the Dreamer character, as blend shapes next to "Masc" (see build_dreamer.py):
  Slim   - narrower everywhere: torso, arms, legs
  Curvy  - wider hips and thighs, a fuller chest, a defined waist
  Plus   - fuller and softer everywhere, a rounder tummy, thicker arms, legs and neck
  Muscle - big shoulders and arms, a broad chest, strong thighs
Each is ONE smooth reshaping of space applied to the body and every clothing/hair mesh, so outfits stay fitted
at any mix (Unity sets the weights from the creator's body type). The face (above the chin) isn't changed.
The character stands in a T-pose: arms along x at z 1.28, the front is -y, legs at x = +-0.066.
"""
import math

from mathutils import Vector

TORSO_HALF = 0.105   # past this |x| (arms, hands) points shift with the shoulder instead of scaling


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def interp(table, z):
    if z <= table[0][0]:
        return table[0][1]
    for (z0, v0), (z1, v1) in zip(table, table[1:]):
        if z <= z1:
            return v0 + (v1 - v0) * smooth((z - z0) / (z1 - z0))
    return table[-1][1]


ONE = [(0.0, 1.0), (2.0, 1.0)]

# per shape: torso width / front depth / back depth by height, arm and leg thickness, neck
SHAPES = {
    # every woman's body type gets Fem: a cinched waist, rounded hips and seat, and a bust
    "Fem": dict(
        width=[(0.62, 1.0), (0.72, 1.06), (0.84, 1.2), (0.92, 1.1), (0.99, 0.9), (1.07, 0.94), (1.15, 1.02), (1.30, 1.0)],
        front=[(0.6, 1.0), (0.84, 1.04), (0.99, 0.96), (1.08, 1.0), (1.3, 1.0)],
        back=[(0.6, 1.0), (0.80, 1.24), (0.88, 1.12), (0.99, 0.98), (1.3, 1.0)],
        arm=1.0, leg=1.06, neck=0.97, bust=0.03),
    "Slim": dict(
        width=[(0.62, 1.0), (0.76, 0.84), (0.99, 0.84), (1.29, 0.86), (1.34, 1.0)],
        front=[(0.62, 1.0), (0.76, 0.84), (1.29, 0.84), (1.34, 1.0)],
        back=[(0.62, 1.0), (0.76, 0.86), (1.29, 0.86), (1.34, 1.0)],
        arm=0.78, leg=0.8, neck=0.9),
    "Curvy": dict(   # on top of Fem: even more hourglass
        width=[(0.62, 1.0), (0.72, 1.08), (0.84, 1.18), (0.92, 1.08), (0.99, 0.96), (1.15, 1.06), (1.3, 1.0)],
        front=[(0.6, 1.0), (0.84, 1.06), (1.3, 1.0)],
        back=[(0.6, 1.0), (0.80, 1.2), (0.9, 1.08), (0.99, 1.0), (1.3, 1.0)],
        arm=1.06, leg=1.18, neck=1.0, bust=0.022),
    "Plus": dict(
        width=[(0.45, 1.0), (0.62, 1.22), (0.84, 1.42), (0.99, 1.55), (1.10, 1.45), (1.24, 1.3), (1.30, 1.22), (1.35, 1.0)],
        front=[(0.5, 1.0), (0.80, 1.35), (0.95, 1.85), (1.04, 1.75), (1.155, 1.45), (1.28, 1.18), (1.34, 1.0)],
        back=[(0.5, 1.0), (0.84, 1.38), (1.0, 1.32), (1.28, 1.18), (1.34, 1.0)],
        arm=1.5, leg=1.45, neck=1.32, bust=0.012),
    "Muscle": dict(   # balanced: a strong chest and arms, not a triangle
        width=[(0.66, 1.0), (0.84, 1.02), (0.99, 1.08), (1.15, 1.12), (1.23, 1.15), (1.29, 1.16), (1.34, 1.0)],
        front=[(0.7, 1.0), (0.99, 1.05), (1.10, 1.14), (1.2, 1.22), (1.28, 1.1), (1.34, 1.0)],
        back=[(0.7, 1.0), (1.0, 1.06), (1.2, 1.18), (1.30, 1.06), (1.34, 1.0)],
        arm=1.55, leg=1.25, neck=1.3),
}


def make_field(sp):
    def field(p):
        x, y, z = p.x, p.y, p.z
        if z > 1.36:                                   # head and hair above the chin: untouched
            return Vector((x, y, z))
        hip = smooth((z - 0.70) / 0.09)                # torso shaping fades out at the top of the legs
        sx = 1 + (interp(sp["width"], z) - 1) * hip
        sf = 1 + (interp(sp["front"], z) - 1) * hip
        sb = 1 + (interp(sp["back"], z) - 1) * hip
        nx, ny, nz = x, y, z
        if abs(x) <= TORSO_HALF or z < 1.12:
            nx = x * sx
            ny = y * (sf if y < 0 else sb)
        else:                                          # arms + hands: ride the shoulder out and get thicker/thinner
            nx = x + math.copysign(TORSO_HALF * (sx - 1.0), x)
            k = smooth((abs(x) - TORSO_HALF) / 0.04) * smooth((1.40 - z) / 0.04) * smooth((z - 1.14) / 0.04)
            a = 1 + (sp["arm"] - 1) * k * (1.0 if abs(x) < 0.39 else 0.75)   # forearms change a bit less
            ny = y * a
            nz = 1.28 + (z - 1.28) * a
        # legs: thicker/thinner around each leg's own axis (smooth in x so skirts don't tear)
        if z < 0.80:
            leg = smooth((0.80 - z) / 0.08) * smooth((z - 0.03) / 0.06) * smooth((0.16 - abs(x)) / 0.05)
            if leg > 0:
                cx = math.copysign(0.066, x) * (1 + (sx - 1))   # each leg's own center (only shifted where the hips are)
                s = 1 + (sp["leg"] - 1) * leg
                nx = cx + (nx - cx) * s
                ny = ny * s
        # bust: two rounded forms on the front of the chest (clothes over it get the same push, so they fit)
        if sp.get("bust") and y < 0.02 and 1.03 < z < 1.28 and abs(x) < 0.15:
            ax = abs(x)
            g = math.exp(-((ax - 0.05) ** 2) / (2 * 0.03 ** 2) - ((z - 1.15) ** 2) / (2 * 0.036 ** 2))
            front = smooth((-y + 0.02) / 0.05)
            ny = ny - sp["bust"] * g * front
            nz = nz - sp["bust"] * 0.25 * g * front      # a little natural weight
        # neck below the chin
        neck = smooth((1.36 - z) / 0.02) * smooth((z - 1.29) / 0.02) * smooth((0.05 - abs(x)) / 0.02)
        if neck > 0:
            s = 1 + (sp["neck"] - 1) * neck
            nx = nx * s
            ny = 0.007 + (ny - 0.007) * s
        return Vector((nx, ny, nz))
    return field


def add_body_shape_keys(meshes):
    """Adds Slim/Curvy/Plus/Muscle shape keys to every mesh (they already have Basis from the Masc pass)."""
    fields = {name: make_field(sp) for name, sp in SHAPES.items()}
    for obj in meshes:
        mw, inv = obj.matrix_world, obj.matrix_world.inverted()
        if obj.data.shape_keys is None:
            obj.shape_key_add(name="Basis", from_mix=False)
        for name, f in fields.items():
            if name in obj.data.shape_keys.key_blocks:
                continue
            key = obj.shape_key_add(name=name, from_mix=False)
            for i, v in enumerate(obj.data.vertices):
                key.data[i].co = inv @ f(mw @ v.co)
    print("body shape keys:", len(meshes), "meshes x", len(fields))
