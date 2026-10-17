"""
Zeldexile nature models for the open world (Blender 3.6+), in the same low-poly chamfered style as
build_models.py and with its helpers: fruit trees, broadleaf trees, cypresses, bushes, pale limestone rocks,
fallen logs, stumps, branches, ferns, reeds, flowers, single fruits and bridge parts.

Run:
  blender.exe -b --factory-startup -P Tools/Blender/build_nature.py -- <unity Resources/Models dir> <blend output dir>

Every model is exported to <Models>/Nature/<name>.fbx, origin at its base center (Unity space: x right, y up,
z forward, meters). Mesh names end in "@slot"; the game turns slots into vertex colors and merges each model
into one instanced mesh, so slots here only choose colors. Trees and bushes also export "<name>_lod", a few dozen
triangles with the same silhouette for distant views.
"""

import math
import os
import random
import sys

import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_models as bm_  # noqa: E402  (helpers: part, prism, box, ico, slab, export, reset_scene, empty...)

part, prism, box, ico, slab, merge = bm_.part, bm_.prism, bm_.box, bm_.ico, bm_.slab, bm_.merge
empty, export, reset_scene = bm_.empty, bm_.export, bm_.reset_scene

# Preview colors for the .blend files (the game colors come from ModelLibrary / NaturePalette by slot).
bm_.SLOT_COLORS.update({
    "bark": (0.47, 0.31, 0.20), "barkdark": (0.34, 0.22, 0.15), "leaf": (0.36, 0.66, 0.22),
    "leafdark": (0.20, 0.47, 0.22), "leaflight": (0.58, 0.80, 0.30), "cypress": (0.14, 0.38, 0.22),
    "orange": (0.98, 0.58, 0.16), "pear": (0.93, 0.80, 0.28), "fruitleaf": (0.30, 0.55, 0.20),
    "stone": (0.84, 0.81, 0.73), "stonelight": (0.92, 0.90, 0.83), "stonedark": (0.68, 0.65, 0.59),
    "moss": (0.45, 0.58, 0.25), "woodcut": (0.86, 0.68, 0.45), "fern": (0.30, 0.58, 0.26),
    "reed": (0.55, 0.66, 0.30), "cattail": (0.45, 0.30, 0.18), "stem": (0.35, 0.55, 0.22),
    "petal": (0.97, 0.97, 0.94), "petalyellow": (0.99, 0.84, 0.22), "flowercenter": (0.98, 0.76, 0.18),
    "wood": (0.62, 0.42, 0.24), "wooddark": (0.45, 0.30, 0.18), "rope": (0.80, 0.70, 0.50), "litter": (0.62, 0.46, 0.24),
})

CATEGORY = "Nature"


# ---------------------------------------------------------------- shape helpers

def tube(bm, points, radii, sides=6, cap=True):
    """Closed tube through points (Unity space) with a radius per point: trunks, limbs, roots, logs."""
    tmp = bmesh.new()
    rings = []
    for i, p in enumerate(points):
        p = Vector(p)
        a = Vector(points[max(0, i - 1)])
        b = Vector(points[min(len(points) - 1, i + 1)])
        d = (b - a).normalized()
        side = d.cross(Vector((0, 0, 1)))
        if side.length < 0.1:
            side = d.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(d).normalized()
        ring = []
        for k in range(sides):
            ang = k / sides * math.tau
            ring.append(tmp.verts.new(p + (side * math.cos(ang) + up * math.sin(ang)) * radii[i]))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            tmp.faces.new((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]))
    if cap:
        tmp.faces.new(list(reversed(rings[0])))
        tmp.faces.new(rings[-1])
    merge(bm, tmp)


def curve(start, direction, length, segments, bend, rng, wobble=0.15):
    """Points of a gently bending limb: starts along direction, drifts by bend (a vector) and random wobble."""
    pts = [Vector(start)]
    d = Vector(direction).normalized()
    step = length / segments
    for i in range(segments):
        d = (d + Vector(bend) * (1.0 / segments) + Vector((rng.uniform(-wobble, wobble), 0, rng.uniform(-wobble, wobble))) / segments).normalized()
        pts.append(pts[-1] + d * step)
    return [tuple(p) for p in pts]


def blob(root, name, slot, center, size, seed, sub=2, jitter=0.1):
    part(root, name, slot, lambda bm: ico(bm, size, at=center, subdivisions=sub, jitter=jitter, seed=seed), 0.0)


def canopy(root, rng, clusters, prefix="Copa", slots=("leaf", "leafdark", "leaflight")):
    """Canopy from blobs: (center, size). Underside blobs dark, top blobs light. Returns the blob list."""
    out = []
    for i, (c, s) in enumerate(clusters):
        top = c[1] + s[1] * 0.3
        mean_y = sum(cc[1] for cc, _ in clusters) / len(clusters)
        slot = slots[2] if c[1] > mean_y + 0.35 and i % 2 == 0 else (slots[1] if c[1] < mean_y - 0.25 else slots[0])
        blob(root, f"{prefix} {i}", slot, c, s, seed=rng.randint(1, 99999))
        out.append((Vector(c), Vector(s)))
    return out


def fruits(root, rng, blobs, count, kind):
    """Fruit on the outside of the crown, mostly in small clusters, never buried in another leaf mass."""
    def inside(q, skip):
        for k, (c, sz) in enumerate(blobs):
            if k == skip:
                continue
            d = q - c
            if (d.x / (sz.x * 0.5)) ** 2 + (d.y / (sz.y * 0.5)) ** 2 + (d.z / (sz.z * 0.5)) ** 2 < 0.92:
                return True
        return False

    placed = 0
    attempts = 0
    while placed < count and attempts < count * 40:
        attempts += 1
        k = rng.randrange(len(blobs))
        c, s = blobs[k]
        a = rng.uniform(0, math.tau)
        y = rng.uniform(-0.6, 0.45)
        r = math.sqrt(max(0.0, 1 - y * y))
        d = Vector((math.cos(a) * r, y, math.sin(a) * r))
        p = c + Vector((d.x * s.x * 0.5, d.y * s.y * 0.5, d.z * s.z * 0.5)) * 1.02
        if inside(p, k):
            continue
        group = 1 if rng.random() < 0.35 else rng.randint(2, 3)
        for g in range(group):
            if placed >= count:
                break
            q = p + Vector((rng.uniform(-0.2, 0.2), -0.12 * g, rng.uniform(-0.2, 0.2)))
            fruit(root, f"Fruta {placed}", kind, q, rng)
            placed += 1


def fruit(root, name, kind, at, rng, detail=1):
    at = tuple(at)
    if kind == "orange":
        part(root, name, "orange", lambda bm: ico(bm, (0.3, 0.29, 0.3), at=at, subdivisions=detail, jitter=0.02, seed=rng.randint(1, 9999)), 0.0)
    else:
        # A small pear: round bottom, tapered neck.
        part(root, name, "pear", lambda bm: (ico(bm, (0.23, 0.22, 0.23), at=(at[0], at[1] - 0.06, at[2]), subdivisions=detail, seed=rng.randint(1, 9999)),
                                             prism(bm, 7, 0.15, 0.15, 0.17, top=0.35, y0=at[1] - 0.02, at=(at[0], 0, at[2]))), 0.0)


def trunk(root, rng, base_r, height, lean=(0, 0, 0), name="Tronco", slot="bark", segs=4):
    pts = curve((0, -0.25, 0), (lean[0], 1, lean[2]), height + 0.25, segs, (rng.uniform(-0.2, 0.2), 0, rng.uniform(-0.2, 0.2)), rng, 0.25)
    radii = [base_r * (1.0 - 0.45 * i / segs) for i in range(segs + 1)]
    radii[0] *= 1.25
    part(root, name, slot, lambda bm: tube(bm, pts, radii, 7), 0.0)
    return pts, radii


def limb(root, name, rng, start, direction, length, r0, slot="bark"):
    pts = curve(start, direction, length, 3, (0, 0.6, 0), rng, 0.3)
    radii = [r0, r0 * 0.75, r0 * 0.55, r0 * 0.35]
    part(root, name, slot, lambda bm: tube(bm, pts, radii, 5), 0.0)
    return pts[-1]


def roots(root, rng, base_r, count, spread=1.0, slot="bark"):
    for i in range(count):
        a = i / count * math.tau + rng.uniform(-0.3, 0.3)
        d = Vector((math.cos(a), 0, math.sin(a)))
        pts = [(d.x * base_r * 0.5, 0.35, d.z * base_r * 0.5),
               (d.x * base_r * (1.1 + 0.4 * spread), 0.12, d.z * base_r * (1.1 + 0.4 * spread)),
               (d.x * base_r * (1.7 + 0.9 * spread), -0.05, d.z * base_r * (1.7 + 0.9 * spread)),
               (d.x * base_r * (2.1 + 1.0 * spread), -0.4, d.z * base_r * (2.1 + 1.0 * spread))]
        radii = [base_r * 0.45, base_r * 0.3, base_r * 0.2, base_r * 0.14]
        part(root, f"Raiz {i}", slot, lambda bm, p=pts, r=radii: tube(bm, p, r, 5), 0.0)


def lod_tree(name, height, canopy_center, canopy_size, trunk_r, slot="leaf", extra=None, lean=(0, 0, 0)):
    """Distant version: a tapered trunk and two or three coarse canopy blobs."""
    reset_scene()
    root = empty(name + "_lod")
    top = (lean[0] * height * 0.6, canopy_center[1] - canopy_size[1] * 0.3, lean[2] * height * 0.6)
    part(root, "Tronco", "bark", lambda bm: tube(bm, [(0, -0.2, 0), top], [trunk_r, trunk_r * 0.6], 4), 0.0)
    part(root, "Copa", slot, lambda bm: ico(bm, canopy_size, at=canopy_center, subdivisions=0, jitter=0.08, seed=3), 0.0)
    for i, (c, s, sl) in enumerate(extra or []):
        part(root, f"Copa {i}", sl, lambda bm, c=c, s=s: ico(bm, s, at=c, subdivisions=0, jitter=0.08, seed=5 + i), 0.0)
    export(CATEGORY, name + "_lod")


# ---------------------------------------------------------------- fruit trees

FRUIT_TREES = [
    # (variant, height, trunk radius, canopy radius, lean, fruit count, shape)
    (0, 3.4, 0.14, 1.05, (0.0, 0, 0.05), 10, "young"),
    (1, 5.0, 0.21, 1.6, (0.05, 0, -0.04), 34, "round"),
    (2, 4.6, 0.23, 1.5, (0.0, 0, 0.0), 26, "open"),
    (3, 5.2, 0.2, 1.4, (0.3, 0, 0.12), 18, "leaning"),
]


def fruit_tree(kind, variant, height, trunk_r, crown, lean, count, shape):
    """Orchard tree of the mockups: short trunk, full rounded crown low over it, bright fruit all around."""
    name = ("laranjeira" if kind == "orange" else "pereira") + f"_{variant}"
    rng = random.Random((0 if kind == "orange" else 100) + variant)
    reset_scene()
    root = empty(name)
    pts, _ = trunk(root, rng, trunk_r, height * 0.38, lean)
    top = Vector(pts[-1])
    pear = kind == "pear"
    sy = 0.82 if not pear else 1.0
    centre = top + Vector((lean[0] * crown * 0.5, crown * (0.55 if not pear else 0.7), lean[2] * crown * 0.5))
    # Limbs fork from the trunk top into the crown; an open tree also has a low climbable branch.
    n = 2 if shape == "young" else 3
    for i in range(n):
        a = i / n * math.tau + rng.uniform(-0.4, 0.4)
        limb(root, f"Galho {i}", rng, tuple(top - Vector((0, 0.15, 0))), (math.cos(a), 1.3 if pear else 0.9, math.sin(a)), crown * 0.75, trunk_r * 0.6)
    low_end = None
    if shape == "open":
        low_end = Vector(limb(root, "Galho baixo", rng, (0, height * 0.22, 0), (1.0, 0.45, 0.25), crown * 1.15, trunk_r * 0.5))
    clusters = [(tuple(centre), (crown * 1.75, crown * 1.45 * sy, crown * 1.75))]
    ring = {"young": 3, "round": 5, "open": 6, "leaning": 4}[shape]
    for i in range(ring):
        a = i / ring * math.tau + rng.uniform(-0.3, 0.3)
        out = crown * (0.62 if shape != "open" else 0.85)
        c = centre + Vector((math.cos(a) * out, rng.uniform(-0.35, 0.15) * crown, math.sin(a) * out))
        sz = crown * rng.uniform(0.95, 1.2)
        clusters.append((tuple(c), (sz, sz * 0.85 * sy, sz)))
    clusters.append((tuple(centre + Vector((rng.uniform(-0.2, 0.2), crown * 0.6 * sy, rng.uniform(-0.2, 0.2)))), (crown * 1.25, crown * 0.95 * sy, crown * 1.25)))
    if low_end is not None:
        clusters.append((tuple(low_end + Vector((0.2, 0.35, 0))), (crown * 1.05, crown * 0.8, crown * 1.05)))
    blobs = canopy(root, rng, clusters)
    fruits(root, rng, blobs, count, kind)
    export(CATEGORY, name)

    mid = sum((b[0] for b in blobs), Vector()) / len(blobs)
    reach = max((b[0] - mid).length + b[1].x * 0.5 for b in blobs)
    lod_tree(name, height, tuple(mid), (reach * 2.0, reach * 1.6 * sy, reach * 2.0), trunk_r, "leaf",
             [((mid.x, mid.y + reach * 0.45, mid.z), (reach * 1.3, reach * 0.9, reach * 1.3), "leaflight")], lean)


# ---------------------------------------------------------------- broadleaf, cypress, ancient tree, bushes

def broadleaf(variant):
    """The big shade tree of the mockups: thick trunk, sprawling limbs, a wide lumpy crown."""
    rng = random.Random(500 + variant)
    height, trunk_r, crown = [(9.0, 0.42, 2.8), (10.5, 0.5, 3.3), (12.5, 0.62, 3.9)][variant]
    name = f"arvore_copa_{variant}"
    reset_scene()
    root = empty(name)
    lean = (rng.uniform(-0.12, 0.12), 0, rng.uniform(-0.12, 0.12))
    pts, _ = trunk(root, rng, trunk_r, height * 0.45, lean, segs=5)
    roots(root, rng, trunk_r, 5, 0.6)
    top = Vector(pts[-1])
    ends = []
    for i in range(5):
        a = i / 5 * math.tau + rng.uniform(-0.3, 0.3)
        start = top - Vector((0, rng.uniform(0.2, 1.4), 0))
        ends.append(limb(root, f"Galho {i}", rng, tuple(start), (math.cos(a), 0.9, math.sin(a)), crown * 1.2, trunk_r * 0.5))
    clusters = []
    for e in ends:
        s = crown * rng.uniform(0.9, 1.15)
        clusters.append(((e[0], e[1] + 0.4, e[2]), (s * 1.3, s, s * 1.3)))
    for i in range(4):
        a = i / 4 * math.tau + 0.4
        clusters.append(((top.x + math.cos(a) * crown * 0.6, top.y + crown * 1.2, top.z + math.sin(a) * crown * 0.6), (crown * 1.3, crown * 0.95, crown * 1.3)))
    clusters.append(((top.x, top.y + crown * 1.7, top.z), (crown * 1.4, crown, crown * 1.4)))
    blobs = canopy(root, rng, clusters)
    export(CATEGORY, name)
    centre = sum((b[0] for b in blobs), Vector()) / len(blobs)
    reach = max((b[0] - centre).length + b[1].x * 0.45 for b in blobs)
    lod_tree(name, height, tuple(centre), (reach * 2.0, reach * 1.35, reach * 2.0), trunk_r, "leaf",
             [((centre.x, centre.y + reach * 0.5, centre.z), (reach * 1.3, reach * 0.8, reach * 1.3), "leaflight"),
              ((centre.x, centre.y - reach * 0.35, centre.z), (reach * 1.6, reach * 0.6, reach * 1.6), "leafdark")], lean)


def cypress(variant):
    """Tall, slender evergreen in stacked flame-shaped masses (the dark verticals of the mockups)."""
    rng = random.Random(700 + variant)
    height = [8.5, 11.0][variant]
    name = f"cipreste_{variant}"
    reset_scene()
    root = empty(name)
    part(root, "Tronco", "bark", lambda bm: prism(bm, 5, 0.32, 0.32, height * 0.35, top=0.7, y0=-0.2), 0.02)
    y = height * 0.18
    n = 4
    for i in range(n):
        t = i / (n - 1)
        w = (1.9 - 1.2 * t) * (1.0 if variant == 0 else 1.1)
        h = height * (0.36 - 0.06 * t)
        slot = "cypress" if i % 2 == 0 else "leafdark"
        part(root, f"Copa {i}", slot, lambda bm, y=y, w=w, h=h, i=i: ico(bm, (w, h, w), at=(rng.uniform(-0.08, 0.08), y + h * 0.45, rng.uniform(-0.08, 0.08)), subdivisions=1, jitter=0.1, seed=900 + i + variant * 10), 0.0)
        y += h * 0.55
    part(root, "Ponta", "cypress", lambda bm: prism(bm, 6, 0.8, 0.8, height * 0.2, top=0.0, y0=y), 0.0)
    export(CATEGORY, name)
    lod_tree(name, height, (0, height * 0.5, 0), (1.9, height * 0.95, 1.9), 0.18, "cypress")


def ancient_tree():
    """Árvore Anciã: an enormous old orange tree with a vast crown and exposed roots (landmark)."""
    rng = random.Random(1234)
    reset_scene()
    root = empty("arvore_ancia")
    pts, _ = trunk(root, rng, 1.05, 5.5, (0.05, 0, 0.03), segs=5)
    roots(root, rng, 1.05, 8, 1.4)
    top = Vector(pts[-1])
    ends = []
    for i in range(6):
        a = i / 6 * math.tau + rng.uniform(-0.25, 0.25)
        start = top - Vector((0, rng.uniform(0.3, 2.2), 0))
        ends.append(limb(root, f"Galho {i}", rng, tuple(start), (math.cos(a) * 1.3, 0.8, math.sin(a) * 1.3), 6.5, 0.48))
    clusters = []
    for e in ends:
        clusters.append(((e[0], e[1] + 0.5, e[2]), (5.2, 3.6, 5.2)))
        clusters.append(((e[0] * 0.6, e[1] + 2.0, e[2] * 0.6), (4.2, 3.0, 4.2)))
    clusters.append(((top.x, top.y + 5.5, top.z), (7.0, 4.2, 7.0)))
    blobs = canopy(root, rng, clusters)
    fruits(root, rng, blobs, 60, "orange")
    export(CATEGORY, "arvore_ancia")
    lod_tree("arvore_ancia", 16, (top.x, top.y + 3.5, top.z), (17, 9, 17), 1.0, "leaf",
             [((top.x, top.y + 6, top.z), (11, 5, 11), "leaflight")])


def bush(variant):
    rng = random.Random(1500 + variant)
    name = f"arbusto_{variant}"
    reset_scene()
    root = empty(name)
    n = [3, 4, 5][variant]
    clusters = []
    for i in range(n):
        a = i / n * math.tau + rng.uniform(-0.4, 0.4)
        r = 0.35 if i else 0.0
        s = rng.uniform(0.8, 1.15) * (1.0 if i else 1.25)
        clusters.append(((math.cos(a) * r, 0.42 * s, math.sin(a) * r), (s * 1.1, s * 0.85, s * 1.1)))
    canopy(root, rng, clusters, "Folhagem")
    if variant == 2:
        # Small white blossoms on one bush type.
        for i in range(9):
            a = rng.uniform(0, math.tau)
            p = (math.cos(a) * 0.55, rng.uniform(0.45, 0.95), math.sin(a) * 0.55)
            part(root, f"Flor {i}", "petal", lambda bm, p=p: ico(bm, (0.09, 0.06, 0.09), at=p), 0.0)
    export(CATEGORY, name)
    reset_scene()
    root = empty(name + "_lod")
    part(root, "Folhagem", "leaf", lambda bm: ico(bm, (1.6, 1.0, 1.6), at=(0, 0.45, 0), subdivisions=0, jitter=0.1, seed=variant), 0.0)
    export(CATEGORY, name + "_lod")


# ---------------------------------------------------------------- rocks

def strata_outline(rng, rx, rz, points=8, rough=0.18):
    out = []
    for i in range(points):
        a = i / points * math.tau + rng.uniform(-0.15, 0.15)
        r = 1.0 + rng.uniform(-rough, rough)
        out.append((math.cos(a) * rx * r, math.sin(a) * rz * r))
    return out


def layered_rock(root, rng, prefix, base, layers, width, depth, height, shrink=0.82, moss=False, offset=0.12):
    """Pale limestone in horizontal strata: stacked slabs, each a little smaller and shifted, chamfered."""
    y = base[1]
    w, d = width, depth
    h = height / layers
    for i in range(layers):
        ox = base[0] + rng.uniform(-offset, offset) * width
        oz = base[2] + rng.uniform(-offset, offset) * depth
        outline = [(ox + x, oz + z) for x, z in strata_outline(rng, w * 0.5, d * 0.5)]
        slot = "stonedark" if i == 0 else ("stonelight" if i == layers - 1 else "stone")
        thick = h * rng.uniform(0.9, 1.1)
        part(root, f"{prefix} {i}", slot, lambda bm, o=outline, t=thick, yy=y: slab(bm, o, t, yy + t / 2), min(0.06, thick * 0.15))
        y += thick * 0.97
        w *= shrink * rng.uniform(0.92, 1.05)
        d *= shrink * rng.uniform(0.92, 1.05)
    if moss:
        outline = [(base[0] + x, base[2] + z) for x, z in strata_outline(rng, w * 0.45, d * 0.45, 7, 0.3)]
        part(root, f"{prefix} Musgo", "moss", lambda bm, o=outline, yy=y: slab(bm, o, 0.05, yy + 0.01), 0.01)
    return y


def boulder(bm, size, at=(0, 0, 0), seed=0, flat_top=0.55, flat_side=0.0, jitter=0.16, rotate=(0, 0, 0)):
    """Chunky pale boulder: a jittered sphere whose top (and optionally one side) is sheared flat,
    the worn limestone look of the mockups."""
    tmp = bmesh.new()
    bmesh.ops.create_icosphere(tmp, subdivisions=2, radius=0.5)
    rng = random.Random(seed)
    # Low-frequency lumps: displace along a few random directions, not per vertex, so faces stay broad.
    dirs = [Vector((rng.uniform(-1, 1), rng.uniform(-0.6, 0.6), rng.uniform(-1, 1))).normalized() for _ in range(5)]
    amps = [rng.uniform(-jitter, jitter) for _ in dirs]
    for v in tmp.verts:
        n = v.co.normalized()
        k = 1.0 + sum(a * max(0.0, n.dot(d)) ** 2 for a, d in zip(amps, dirs))
        v.co = v.co * k
        if v.co.y > 0.5 * flat_top:
            v.co.y = 0.5 * flat_top + (v.co.y - 0.5 * flat_top) * 0.08
        if flat_side > 0 and v.co.x > 0.5 * flat_side:
            v.co.x = 0.5 * flat_side + (v.co.x - 0.5 * flat_side) * 0.1
    m = bm_.M(at, size, rotate)
    for v in tmp.verts:
        v.co = m @ v.co
    merge(bm, tmp)


def rocks():
    # Medium rocks (scaled 0.7..1.8 in the world).
    reset_scene()
    root = empty("rocha_0")
    part(root, "Pedra", "stone", lambda bm: boulder(bm, (1.5, 1.0, 1.2), at=(0, 0.3, 0), seed=1), 0.0)
    part(root, "Musgo", "moss", lambda bm: boulder(bm, (0.8, 0.12, 0.6), at=(-0.15, 0.6, 0.05), seed=2, flat_top=0.2, jitter=0.1), 0.0)
    export(CATEGORY, "rocha_0")

    reset_scene()
    root = empty("rocha_1")
    part(root, "Pedra", "stone", lambda bm: boulder(bm, (1.3, 1.1, 1.1), at=(0, 0.35, 0), seed=3, flat_top=0.7, flat_side=0.6), 0.0)
    part(root, "Lasca", "stonedark", lambda bm: boulder(bm, (0.6, 0.5, 0.5), at=(0.6, 0.12, 0.35), seed=4), 0.0)
    export(CATEGORY, "rocha_1")

    reset_scene()
    root = empty("rocha_2")
    # A low slab with a step: the layered side of the limestone.
    part(root, "Laje", "stone", lambda bm: boulder(bm, (1.8, 0.6, 1.1), at=(0, 0.15, 0), seed=5, flat_top=0.35), 0.0)
    part(root, "Degrau", "stonelight", lambda bm: boulder(bm, (1.0, 0.55, 0.8), at=(0.3, 0.42, -0.05), seed=6, flat_top=0.4), 0.0)
    export(CATEGORY, "rocha_2")

    # Big outcrops (scaled 2.6..5.5): flat-topped masses with a lighter top shelf, a dark base and moss.
    shapes = [
        [((0, 0.35, 0), (1.6, 1.25, 1.3), "stone", 0.6, 0.0), ((0.75, 0.2, 0.45), (0.9, 0.8, 0.8), "stonedark", 0.5, 0.0)],
        [((0, 0.45, 0), (1.5, 1.5, 1.2), "stone", 0.7, 0.55), ((-0.8, 0.25, 0.2), (1.0, 0.9, 1.0), "stonelight", 0.55, 0.0),
         ((0.55, 0.1, -0.6), (0.7, 0.5, 0.6), "stonedark", 0.5, 0.0)],
        [((0, 0.6, 0), (1.3, 1.9, 1.1), "stone", 0.8, 0.5), ((0.9, 0.35, 0.2), (1.1, 1.1, 1.0), "stonelight", 0.6, 0.0),
         ((-0.8, 0.2, -0.3), (1.0, 0.7, 0.9), "stonedark", 0.5, 0.0), ((0.2, 0.08, 0.85), (0.6, 0.4, 0.5), "stone", 0.5, 0.0)],
    ]
    for v, blocks in enumerate(shapes):
        reset_scene()
        root = empty(f"rocha_grande_{v}")
        for i, (at, size, slot, ft, fs) in enumerate(blocks):
            part(root, f"Bloco {i}", slot, lambda bm, at=at, size=size, ft=ft, fs=fs, i=i: boulder(bm, size, at=at, seed=300 + v * 10 + i, flat_top=ft, flat_side=fs, rotate=(0, i * 37, 0)), 0.0)
        top = blocks[0]
        part(root, "Musgo", "moss", lambda bm: boulder(bm, (top[1][0] * 0.55, 0.12, top[1][2] * 0.5), at=(top[0][0] - 0.1, top[0][1] + top[1][1] * 0.5 * top[3] + 0.01, top[0][2]), seed=399 + v, flat_top=0.2, jitter=0.12), 0.0)
        export(CATEGORY, f"rocha_grande_{v}")

    # Pebbles: a few small stones together.
    for v in range(3):
        rng = random.Random(600 + v)
        reset_scene()
        root = empty(f"seixo_{v}")
        for i in range(3 + v):
            a = rng.uniform(0, math.tau)
            r = rng.uniform(0.05, 0.4)
            sz = rng.uniform(0.18, 0.42)
            slot = ("stone", "stonelight", "stonedark")[i % 3]
            part(root, f"Seixo {i}", slot, lambda bm, a=a, r=r, sz=sz, i=i: ico(bm, (sz * 1.2, sz * 0.6, sz), at=(math.cos(a) * r, sz * 0.15, math.sin(a) * r), jitter=0.18, seed=40 + i + v * 7), 0.0)
        export(CATEGORY, f"seixo_{v}")


# ---------------------------------------------------------------- wood on the ground

def logs():
    for v in range(2):
        rng = random.Random(800 + v)
        reset_scene()
        root = empty(f"tronco_caido_{v}")
        length = [4.2, 5.6][v]
        r = [0.28, 0.36][v]
        pts = curve((-length / 2, r * 0.85, 0), (1, 0.02, 0), length, 5, (0, 0, rng.uniform(-0.3, 0.3)), rng, 0.1)
        radii = [r * (1.0 - 0.25 * i / 5) for i in range(6)]
        part(root, "Tronco", "bark", lambda bm: tube(bm, pts, radii, 7), 0.0)
        # Cut end (pale wood) and a broken, splintered end.
        a = Vector(pts[0])
        part(root, "Corte", "woodcut", lambda bm: tube(bm, [tuple(a + Vector((-0.02, 0, 0))), tuple(a + Vector((0.03, 0, 0)))], [r * 0.92, r * 0.92], 7), 0.0)
        e = Vector(pts[-1])
        for k in range(4):
            ang = k / 4 * math.tau
            off = Vector((0, math.cos(ang), math.sin(ang))) * radii[-1] * 0.5
            part(root, f"Lasca {k}", "barkdark", lambda bm, o=off: prism(bm, 3, 0.14, 0.1, rng.uniform(0.25, 0.5), top=0, y0=0, at=tuple(e + o), rotate=(0, 0, -90)), 0.0)
        # Stubs of old branches and moss on top.
        for k in range(2 + v):
            t = rng.uniform(0.25, 0.75)
            p = Vector(pts[int(t * 5)])
            part(root, f"Toco de galho {k}", "bark", lambda bm, p=p: tube(bm, [tuple(p), tuple(p + Vector((rng.uniform(-0.2, 0.2), 0.5, rng.uniform(-0.3, 0.3))))], [0.09, 0.05], 5), 0.0)
        for k in range(2):
            p = Vector(pts[1 + k * 2]) + Vector((0, r * 0.85, 0))
            part(root, f"Musgo {k}", "moss", lambda bm, p=p: ico(bm, (0.7, 0.14, 0.42), at=tuple(p), seed=k + v), 0.0)
        export(CATEGORY, f"tronco_caido_{v}")

    rng = random.Random(850)
    reset_scene()
    root = empty("toco")
    part(root, "Toco", "bark", lambda bm: tube(bm, [(0, -0.2, 0), (0, 0.55, 0)], [0.42, 0.36], 7), 0.0)
    part(root, "Corte", "woodcut", lambda bm: prism(bm, 7, 0.68, 0.68, 0.04, y0=0.55), 0.0)
    roots(root, rng, 0.42, 4, 0.5)
    export(CATEGORY, "toco")

    for v in range(3):
        rng = random.Random(870 + v)
        reset_scene()
        root = empty(f"galho_{v}")
        length = [1.3, 1.8, 2.4][v]
        pts = curve((-length / 2, 0.05, 0), (1, 0.03, 0), length, 3, (0, 0, rng.uniform(-0.5, 0.5)), rng, 0.2)
        part(root, "Galho", "bark", lambda bm: tube(bm, pts, [0.05, 0.045, 0.035, 0.025], 4), 0.0)
        for k in range(1 + v):
            p = Vector(pts[1 + k % 2])
            twig = [tuple(p), tuple(p + Vector((rng.uniform(0.1, 0.4), 0.03, rng.choice((-1, 1)) * rng.uniform(0.2, 0.45))))]
            part(root, f"Graveto {k}", "bark", lambda bm, t=twig: tube(bm, t, [0.025, 0.012], 4), 0.0)
        if v == 2:
            for k in range(3):
                p = Vector(pts[2]) + Vector((rng.uniform(-0.3, 0.3), 0.08, rng.uniform(-0.25, 0.25)))
                part(root, f"Folhas {k}", "leafdark", lambda bm, p=p: ico(bm, (0.3, 0.12, 0.26), at=tuple(p), seed=k), 0.0)
        export(CATEGORY, f"galho_{v}")


# ---------------------------------------------------------------- small plants

def fern(variant):
    rng = random.Random(1100 + variant)
    reset_scene()
    root = empty(f"samambaia_{variant}")
    n = [7, 10][variant]
    for i in range(n):
        a = i / n * 360 + rng.uniform(-15, 15)
        tilt = rng.uniform(40, 62)
        length = rng.uniform(0.6, 0.95) * (1.0 if variant == 0 else 1.2)
        slot = "fern" if i % 3 else "leafdark"
        part(root, f"Fronde {i}", slot, lambda bm, a=a, t=tilt, l=length: prism(bm, 4, 0.22, 0.03, l, top=0.0, y0=0.0, rotate=(t, a, 0)), 0.0)
    export(CATEGORY, f"samambaia_{variant}")


def reeds(variant):
    rng = random.Random(1200 + variant)
    reset_scene()
    root = empty(f"junco_{variant}")
    n = [10, 14][variant]
    for i in range(n):
        x, z = rng.uniform(-0.35, 0.35), rng.uniform(-0.35, 0.35)
        h = rng.uniform(0.9, 1.6)
        rx, rz = rng.uniform(-10, 10), rng.uniform(-10, 10)
        part(root, f"Folha {i}", "reed", lambda bm, x=x, z=z, h=h, rx=rx, rz=rz: prism(bm, 3, 0.07, 0.04, h, top=0.0, y0=-0.05, at=(x, 0, z), rotate=(rx, rng.uniform(0, 180), rz)), 0.0)
    for i in range(2 + variant):
        x, z = rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25)
        h = rng.uniform(1.1, 1.5)
        part(root, f"Haste {i}", "stem", lambda bm, x=x, z=z, h=h: prism(bm, 3, 0.025, 0.025, h, y0=-0.05, at=(x, 0, z)), 0.0)
        part(root, f"Espiga {i}", "cattail", lambda bm, x=x, z=z, h=h: prism(bm, 6, 0.07, 0.07, 0.22, top=0.8, y0=h - 0.05, at=(x, 0, z)), 0.0)
    export(CATEGORY, f"junco_{variant}")


def flowers(name, petal, center, variant):
    rng = random.Random(1300 + variant + (50 if petal == "petalyellow" else 0))
    reset_scene()
    root = empty(name)
    n = [6, 9][variant]
    for i in range(n):
        x, z = rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3)
        h = rng.uniform(0.16, 0.34)
        part(root, f"Haste {i}", "stem", lambda bm, x=x, z=z, h=h: prism(bm, 3, 0.022, 0.022, h, y0=-0.03, at=(x, 0, z)), 0.0)
        size = rng.uniform(0.1, 0.15)
        part(root, f"Pétalas {i}", petal, lambda bm, x=x, z=z, h=h, s=size: prism(bm, 6, s, s, 0.022, top=1.0, y0=h - 0.03, at=(x, 0, z), rotate=(rng.uniform(-15, 15), rng.uniform(0, 60), rng.uniform(-15, 15))), 0.0)
        part(root, f"Miolo {i}", center, lambda bm, x=x, z=z, h=h: ico(bm, (0.045, 0.03, 0.045), at=(x, h, z)), 0.0)
    for i in range(3):
        a = rng.uniform(0, 360)
        part(root, f"Folha {i}", "stem", lambda bm, a=a: prism(bm, 4, 0.1, 0.02, 0.22, top=0.0, rotate=(65, a, 0)), 0.0)
    export(CATEGORY, name)


def single_fruits():
    rng = random.Random(1400)
    reset_scene()
    root = empty("fruta_laranja")
    fruit(root, "Fruta", "orange", (0, 0.15, 0), rng, 2)
    part(root, "Folha", "fruitleaf", lambda bm: prism(bm, 4, 0.1, 0.02, 0.12, top=0, y0=0.22, rotate=(35, 20, 0)), 0.0)
    export(CATEGORY, "fruta_laranja")
    reset_scene()
    root = empty("fruta_pera")
    fruit(root, "Fruta", "pear", (0, 0.2, 0), rng, 2)
    part(root, "Cabo", "bark", lambda bm: prism(bm, 4, 0.025, 0.025, 0.08, y0=0.24), 0.0)
    export(CATEGORY, "fruta_pera")


def leaf_litter():
    rng = random.Random(1450)
    reset_scene()
    root = empty("folhas")
    part(root, "Folhas", "litter", lambda bm: slab(bm, strata_outline(rng, 0.8, 0.6, 9, 0.3), 0.04, 0.0), 0.0)
    for i in range(6):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0.2, 0.9)
        part(root, f"Folha {i}", ("litter", "leafdark", "orange")[i % 3], lambda bm, a=a, r=r: prism(bm, 4, 0.14, 0.02, 0.18, top=0, y0=0.01, at=(math.cos(a) * r, 0, math.sin(a) * r), rotate=(85, rng.uniform(0, 360), 0)), 0.0)
    export(CATEGORY, "folhas")


# ---------------------------------------------------------------- bridge parts (unit pieces, assembled in Unity)

def bridge_parts():
    for v in range(3):
        rng = random.Random(1600 + v)
        reset_scene()
        root = empty(f"ponte_tabua_{v}")
        # Plank across the deck: 1 m long (scaled to the deck width), 0.3 m along the bridge.
        warp = rng.uniform(-0.015, 0.015)
        part(root, "Tábua", "wood" if v != 1 else "wooddark", lambda bm: box(bm, (1.0, 0.09, 0.29), at=(0, 0.045, 0), rotate=(0, rng.uniform(-1.5, 1.5), warp * 40)), 0.012)
        export(CATEGORY, f"ponte_tabua_{v}")
    reset_scene()
    root = empty("ponte_viga")
    part(root, "Viga", "wooddark", lambda bm: box(bm, (0.22, 0.26, 1.0), at=(0, 0.13, 0)), 0.02)
    export(CATEGORY, "ponte_viga")
    reset_scene()
    root = empty("ponte_poste")
    part(root, "Poste", "wood", lambda bm: prism(bm, 6, 0.2, 0.2, 1.3, top=0.92), 0.02)
    part(root, "Capa", "wooddark", lambda bm: prism(bm, 6, 0.18, 0.18, 0.08, top=0.4, y0=1.3), 0.0)
    part(root, "Amarra", "rope", lambda bm: prism(bm, 6, 0.24, 0.24, 0.1, y0=1.05), 0.0)
    export(CATEGORY, "ponte_poste")
    reset_scene()
    root = empty("ponte_corda")
    part(root, "Corda", "rope", lambda bm: prism(bm, 5, 0.055, 0.055, 1.0, y0=-0.5, rotate=(90, 0, 0)), 0.0)
    export(CATEGORY, "ponte_corda")
    reset_scene()
    root = empty("ponte_pilar")
    # Support pile standing in the water, from the bed up to the deck beams (1 m, scaled to the height).
    part(root, "Pilar", "wooddark", lambda bm: prism(bm, 6, 0.3, 0.3, 1.0, top=0.95), 0.02)
    export(CATEGORY, "ponte_pilar")


def main():
    for v, h, r, c, lean, n, shape in FRUIT_TREES:
        fruit_tree("orange", v, h, r, c, lean, n, shape)
        fruit_tree("pear", v, h * 1.05, r, c * 0.9, lean, n, shape)
    for v in range(3):
        broadleaf(v)
    for v in range(2):
        cypress(v)
    ancient_tree()
    for v in range(3):
        bush(v)
    rocks()
    logs()
    for v in range(2):
        fern(v)
        reeds(v)
        flowers(f"flor_branca_{v}", "petal", "flowercenter", v)
        flowers(f"flor_amarela_{v}", "petalyellow", "flowercenter", v)
    single_fruits()
    leaf_litter()
    bridge_parts()
    print("\n=== Zeldexile nature models ===")
    for line in bm_.REPORT:
        print(line)
    print(f"FBX: {bm_.MODELS_DIR}/{CATEGORY}\nBlend: {bm_.BLEND_DIR}")


if __name__ == "__main__":
    main()
