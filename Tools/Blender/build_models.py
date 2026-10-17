"""
Zeldexile model builder for Blender 3.6+.

Builds every prototype model from code: low poly, flat shaded, every hard edge chamfered
(one-segment bevel, sized in meters so it stays uniform), then exports FBX for Unity and saves a
.blend per model for hand editing.

Run:
  blender.exe -b -P Tools/Blender/build_models.py -- <unity Resources/Models dir> <blend output dir>

Conventions:
  * Geometry is authored in Unity space (x right, y up, z forward, meters) and converted once on export.
  * Mesh object names end in "@slot" (skin, tunic, stone, copper...). Unity assigns the material by slot,
    so colors stay data-driven (character styles, elemental states) instead of baked into the FBX.
  * Character joints are empties with the exact names CharacterRig expects.
  * Everything is exported flat (no parenting) with absolute positions: the FBX exporter's axis baking
    breaks nested transforms. A mesh named "Joint~Part@slot" belongs to that joint; Unity rebuilds the tree.
  * Each mesh object's origin is its own center, so the outline shader grows evenly around it.
"""

import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODELS_DIR = os.path.abspath(ARGS[0]) if len(ARGS) > 0 else os.path.abspath("Models")
BLEND_DIR = os.path.abspath(ARGS[1]) if len(ARGS) > 1 else os.path.abspath("Blend")

# Unity (x, y, z) -> Blender (x, z, y). Verified by ModelImportTests: the FBX exporter + Unity importer read
# Blender Y as Unity Z. It is a mirror, so normals are recalculated after converting (see finish()).
TO_BLENDER = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))

# Preview colors for the .blend files only. Unity colors come from the slot name.
SLOT_COLORS = {
    "skin": (0.93, 0.70, 0.55), "hair": (0.86, 0.46, 0.18), "tunic": (0.16, 0.52, 0.62),
    "pants": (0.55, 0.42, 0.30), "boots": (0.36, 0.24, 0.18), "accent": (0.95, 0.78, 0.25),
    "dark": (0.08, 0.07, 0.10), "metal": (0.75, 0.75, 0.78), "bark": (0.45, 0.30, 0.20),
    "leaf": (0.42, 0.70, 0.26), "leafdark": (0.24, 0.52, 0.30), "stone": (0.62, 0.60, 0.56),
    "stonedark": (0.50, 0.48, 0.46), "copper": (0.78, 0.45, 0.25), "wood": (0.58, 0.38, 0.20),
    "grass": (0.40, 0.66, 0.28),
    "cream": (0.86, 0.79, 0.68), "leather": (0.45, 0.29, 0.18), "steel": (0.80, 0.82, 0.86),
    "shirt": (0.20, 0.20, 0.23), "glove": (0.22, 0.20, 0.20), "brass": (0.85, 0.66, 0.28),
    "grip": (0.25, 0.18, 0.14), "kneepad": (0.50, 0.48, 0.50), "seam": (0.22, 0.23, 0.28),
    "wooddark": (0.40, 0.25, 0.15), "iris": (0.62, 0.36, 0.16), "sole": (0.18, 0.13, 0.10),
    "bag": (0.68, 0.47, 0.30), "nose": (0.86, 0.64, 0.47),
}

REPORT = []
WORLD = {}  # object name -> absolute position in Unity space
AUTHORED_IN = {}  # bone name -> name of the joint whose space its parts are authored in


# ---------------------------------------------------------------- scene helpers

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    WORLD.clear()
    AUTHORED_IN.clear()


def material(slot):
    name = "slot_" + slot
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        c = SLOT_COLORS.get(slot, (1, 1, 1))
        mat.diffuse_color = (c[0], c[1], c[2], 1.0)
    return mat


def empty(name, parent=None, at=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_size = 0.08
    bpy.context.scene.collection.objects.link(obj)
    world = (WORLD[parent.name] if parent is not None else Vector((0, 0, 0))) + Vector(at)
    WORLD[name] = world
    obj.location = TO_BLENDER @ world
    return obj


def finish(bm, name, slot, parent=None, bevel=0.0):
    """Bevel, center the origin, convert to Blender space and link. Geometry is authored relative to
    the parent joint; the object is placed at its absolute position and named "Joint~Part@slot"."""
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type='OFFSET', segments=1,
                        profile=0.5, affect='EDGES', clamp_overlap=True)
    lo = Vector((min(v.co.x for v in bm.verts), min(v.co.y for v in bm.verts), min(v.co.z for v in bm.verts)))
    hi = Vector((max(v.co.x for v in bm.verts), max(v.co.y for v in bm.verts), max(v.co.z for v in bm.verts)))
    center = (lo + hi) / 2
    for v in bm.verts:
        v.co = TO_BLENDER @ (v.co - center)
    # Normals must be refreshed after moving vertices, then checked: every part is closed, so a
    # negative signed volume means the faces point inward.
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    if sum(f.calc_center_median().dot(f.normal) * f.calc_area() for f in bm.faces) < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = False
    mesh.materials.append(material(slot))

    owner = parent.name if parent is not None else ""
    obj = bpy.data.objects.new(f"{owner}~{name}@{slot}", mesh)
    bpy.context.scene.collection.objects.link(obj)
    # Elbow and knee bones reuse the coordinates of the limb above them (AUTHORED_IN), so moving a part
    # to the lower bone never changes where it sits.
    base = WORLD[AUTHORED_IN.get(owner, owner)] if parent is not None else Vector((0, 0, 0))
    obj.location = TO_BLENDER @ (base + center)
    return obj


def M(translate=(0, 0, 0), scale=(1, 1, 1), rotate=(0, 0, 0)):
    """Transform in Unity space: scale, then rotate (degrees, X then Y then Z), then translate."""
    s = Matrix.Diagonal(Vector((scale[0], scale[1], scale[2], 1.0)))
    r = (Matrix.Rotation(math.radians(rotate[2]), 4, 'Z') @
         Matrix.Rotation(math.radians(rotate[1]), 4, 'Y') @
         Matrix.Rotation(math.radians(rotate[0]), 4, 'X'))
    return Matrix.Translation(Vector(translate)) @ r @ s


# The bmesh primitive generators build along their own Z axis; this turns that into Unity's Y.
Z_TO_Y = Matrix.Rotation(math.radians(-90), 4, 'X')


def prism(bm, sides, width, depth, height, top=1.0, y0=0.0, down=False, at=(0, 0, 0), rotate=(0, 0, 0)):
    """Prism (or cone with top=0) along +Y from y0 (or -Y when down). width/depth are diameters."""
    mid = y0 - height / 2 if down else y0 + height / 2
    base = Matrix.Translation(Vector((0, mid, 0))) @ Matrix.Diagonal(Vector((width, height, depth, 1)))
    if down:
        base = base @ Matrix.Rotation(math.pi, 4, 'X')
    m = M(at, rotate=rotate) @ base @ Z_TO_Y
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=sides,
                          radius1=0.5, radius2=0.5 * top, depth=1.0, matrix=m)


def box(bm, size, at=(0, 0, 0), rotate=(0, 0, 0)):
    bmesh.ops.create_cube(bm, size=1.0, matrix=M(at, size, rotate))


def ico(bm, size, at=(0, 0, 0), subdivisions=0, jitter=0.0, seed=0, rotate=(0, 0, 0)):
    tmp = bmesh.new()
    bmesh.ops.create_icosphere(tmp, subdivisions=subdivisions, radius=0.5)
    rng = random.Random(seed)
    for v in tmp.verts:
        v.co *= 1.0 + (rng.random() * 2 - 1) * jitter
    m = M(at, size, rotate)
    for v in tmp.verts:
        v.co = m @ v.co
    merge(bm, tmp)


def slab(bm, outline, thickness, y=0.0):
    """Extruded polygon in the XZ plane (outline is a list of (x, z), counter-clockwise from above)."""
    tmp = bmesh.new()
    top = [tmp.verts.new((x, y + thickness / 2, z)) for x, z in outline]
    bottom = [tmp.verts.new((x, y - thickness / 2, z)) for x, z in outline]
    tmp.faces.new(top)
    tmp.faces.new(list(reversed(bottom)))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        tmp.faces.new((bottom[i], bottom[j], top[j], top[i]))
    merge(bm, tmp)


def merge(bm, other):
    mesh = bpy.data.meshes.new("tmp")
    other.to_mesh(mesh)
    other.free()
    bm.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)


def part(parent, name, slot, build, bevel=0.0):
    bm = bmesh.new()
    build(bm)
    return finish(bm, name, slot, parent, bevel)


def export(category, name):
    out_dir = os.path.join(MODELS_DIR, category)
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(BLEND_DIR, exist_ok=True)

    tris = 0
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            tris += sum(len(p.vertices) - 2 for p in obj.data.polygons)
    REPORT.append(f"{category}/{name}: {tris} triangles")

    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, name + ".fbx"),
        use_selection=False,
        object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z', axis_up='Y',
        bake_space_transform=True,
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, name + ".blend"))


# ---------------------------------------------------------------- characters

def round_shield(prefix, parent, at, rotate, face_sign):
    """Round wooden shield: planks, iron rim, central boss and rivets. face_sign = +1/-1 along the disc
    normal (in the shield's own frame, Y) marks the outer face, where the boss and rivets sit."""
    def tr(local):
        return M(at, rotate=rotate) @ Vector((local[0], local[1], local[2], 1.0))

    def place(bm_build):
        return bm_build

    radius = 0.27
    part(parent, prefix, "wood", lambda bm: prism(bm, 16, radius * 2, radius * 2, 0.045, at=at, rotate=rotate), 0.008)
    part(parent, prefix + " Aro", "steel",
         lambda bm: prism(bm, 16, radius * 2 + 0.05, radius * 2 + 0.05, 0.03, at=at, rotate=rotate), 0.006)
    # Plank seams on the outer face.
    for i, x in enumerate((-0.15, -0.05, 0.05, 0.15)):
        part(parent, f"{prefix} Tabua {i}", "wooddark",
             lambda bm, x=x: box(bm, (0.012, 0.004, 2 * (radius * radius - x * x) ** 0.5 * 0.92),
                                 at=tuple(tr((x, 0.024 * face_sign, 0))[:3]), rotate=rotate), 0.0)
    part(parent, prefix + " Umbo", "steel",
         lambda bm: ico(bm, (0.11, 0.06, 0.11), at=tuple(tr((0, 0.03 * face_sign, 0))[:3]), subdivisions=1, rotate=rotate), 0.0)
    for i in range(8):
        a = i / 8 * math.tau
        part(parent, f"{prefix} Rebite {i}", "steel",
             lambda bm, a=a: ico(bm, (0.025, 0.02, 0.025),
                                 at=tuple(tr((math.cos(a) * (radius - 0.025), 0.026 * face_sign, math.sin(a) * (radius - 0.025)))[:3]),
                                 rotate=rotate), 0.0)
    # Strap on the inner face, so the back of the shield reads as worn on the arm.
    part(parent, prefix + " Correia", "leather",
         lambda bm: box(bm, (0.32, 0.02, 0.05), at=tuple(tr((0, -0.03 * face_sign, 0))[:3]), rotate=rotate), 0.004)


def humanoid(name, kind):
    """kind: 'hero' or 'cascudo'. Joint names and positions match CharacterRig (the animator is unchanged).

    Hero: the original young adventurer of output/imagegen/aventureiro-anime-seis-poses.png. Human round
    ears, auburn spiky hair with a broken fringe, amber eyes; cropped teal jacket with ochre trim and a
    standing collar over a charcoal shirt; cloth wraps on the forearms and dark fingerless gloves; slate
    trousers with seams and round knee pads; brown boots with a folded cuff, buckle straps and a sole.
    Sides follow the character's own anatomy: sword in the right hand, shield on the left arm, satchel on
    the right hip, its strap crossing from the left shoulder. Front = +Z (Unity), right = +X.
    Gear exists twice: stowed on the back ("... Costas") and in the hands; the game shows one or the other.
    """
    reset_scene()
    hero = kind == "hero"

    hips = empty("Quadril", None, (0, 0.9, 0))
    part(hips, "Pelve", "pants", lambda bm: prism(bm, 8, 0.34, 0.25, 0.2, top=0.88, y0=-0.12), 0.02)

    torso = empty("Tronco", hips, (0, 0.06, 0))
    # Spine in two segments: Tronco (lower back, belt, satchel) and Tórax (chest, collar, shoulders, back gear).
    chest = empty("Tórax", torso, (0, 0.22, 0))
    AUTHORED_IN[chest.name] = torso.name
    # Satchel: a secondary bone hanging from the belt, so it swings behind the motion.
    bag = empty("Bolsa", torso, (0.22, -0.02, 0.02))
    AUTHORED_IN[bag.name] = torso.name
    if hero:
        # Waist and chest: an athletic V taper with a defined waist.
        part(torso, "Cintura", "shirt", lambda bm: prism(bm, 8, 0.30, 0.22, 0.16, top=1.08, y0=-0.02), 0.015)
        part(chest, "Peito", "tunic", lambda bm: prism(bm, 8, 0.36, 0.25, 0.36, top=1.2, y0=0.12), 0.02)
        # Open front: the charcoal shirt shows in a V down the chest, framed by ochre lapels.
        part(chest, "Camisa", "shirt", lambda bm: prism(bm, 4, 0.03, 0.03, 0.34, top=4.5, y0=0.14, at=(0, 0, 0.118)), 0.0)
        for side, label in ((-1, "E"), (1, "D")):
            part(chest, f"Lapela {label}", "accent",
                 lambda bm, s=side: box(bm, (0.024, 0.36, 0.022), at=(0.05 * s, 0.3, 0.128), rotate=(0, 0, -11 * s)), 0.0)
            part(chest, f"Ombreira {label}", "tunic",
                 lambda bm, s=side: ico(bm, (0.15, 0.11, 0.17), at=(0.2 * s, 0.43, 0), subdivisions=1), 0.0)
        # Ochre hem at the waist, standing collar with a trimmed edge.
        part(torso, "Barra", "accent", lambda bm: prism(bm, 8, 0.375, 0.262, 0.03, top=1.0, y0=0.105), 0.0)
        part(chest, "Gola", "tunic", lambda bm: prism(bm, 8, 0.25, 0.21, 0.1, top=1.12, y0=0.47), 0.012)
        part(chest, "Gola Borda", "accent", lambda bm: prism(bm, 8, 0.285, 0.24, 0.022, y0=0.565), 0.0)
        part(chest, "Detalhe", "accent", lambda bm: box(bm, (0.05, 0.05, 0.02), at=(-0.12, 0.35, 0.13)), 0.004)
        # Brown belt with a brass buckle.
        part(torso, "Cinto", "leather", lambda bm: prism(bm, 8, 0.335, 0.245, 0.06, y0=-0.04), 0.01)
        part(torso, "Fivela", "brass", lambda bm: box(bm, (0.07, 0.055, 0.02), at=(0, -0.01, 0.125)), 0.006)
        # Satchel strap from the left shoulder to the right hip, front and back, and the satchel itself.
        part(chest, "Alça", "leather", lambda bm: box(bm, (0.045, 0.62, 0.02), at=(0.0, 0.2, 0.13), rotate=(0, 0, 40)), 0.004)
        part(chest, "Alça Costas", "leather", lambda bm: box(bm, (0.045, 0.62, 0.02), at=(0.0, 0.2, -0.13), rotate=(0, 0, 40)), 0.004)
        part(bag, "Bolsa", "bag", lambda bm: box(bm, (0.08, 0.15, 0.19), at=(0.22, -0.1, 0.02)), 0.02)
        part(bag, "Bolsa Tampa", "leather", lambda bm: box(bm, (0.09, 0.07, 0.2), at=(0.225, -0.04, 0.02)), 0.01)
        part(bag, "Bolsa Fecho", "brass", lambda bm: box(bm, (0.02, 0.03, 0.03), at=(0.27, -0.06, 0.02)), 0.0)
        # Stowed gear (the reference never shows it stowed): scabbard and shield on the back, bow beside them.
        part(chest, "Arma Costas Bainha", "leather", lambda bm: box(bm, (0.065, 0.72, 0.04), at=(-0.06, 0.2, -0.16), rotate=(0, 0, -20)), 0.008)
        part(chest, "Arma Costas Guarda", "brass", lambda bm: box(bm, (0.15, 0.03, 0.045), at=(-0.185, 0.57, -0.16), rotate=(0, 0, -20)), 0.004)
        part(chest, "Arma Costas Punho", "grip", lambda bm: box(bm, (0.035, 0.15, 0.035), at=(-0.215, 0.66, -0.16), rotate=(0, 0, -20)), 0.004)
        part(chest, "Arma Costas Pomo", "brass", lambda bm: ico(bm, (0.045, 0.045, 0.045), at=(-0.245, 0.745, -0.16)), 0.0)
        round_shield("Escudo Costas", chest, (0.03, 0.22, -0.215), (90, 0, 0), -1)
        part(chest, "Arco Costas", "wood", lambda bm: box(bm, (0.032, 1.0, 0.045), at=(0.14, 0.2, -0.27), rotate=(0, 0, 24)), 0.006)
        part(chest, "Arco Costas Corda", "cream", lambda bm: box(bm, (0.008, 0.94, 0.008), at=(0.16, 0.2, -0.295), rotate=(0, 0, 24)), 0.0)
    else:
        part(chest, "Peito", "tunic", lambda bm: prism(bm, 6, 0.38, 0.27, 0.5, top=1.18), 0.025)
        part(torso, "Cinto", "accent", lambda bm: prism(bm, 6, 0.41, 0.30, 0.07, y0=-0.02), 0.012)
        part(torso, "Fivela", "metal", lambda bm: box(bm, (0.10, 0.075, 0.03), at=(0, 0.015, 0.155)), 0.01)
        part(chest, "Gola", "accent", lambda bm: prism(bm, 5, 0.31, 0.25, 0.09, top=0.8, y0=0.44), 0.015)
        part(chest, "Casco", "hair", lambda bm: ico(bm, (0.64, 0.56, 0.46), at=(0, 0.3, -0.15), jitter=0.12, seed=7), 0.03)
        for i, (x, y) in enumerate(((0, 0.55), (-0.18, 0.4), (0.18, 0.4), (0, 0.25))):
            part(chest, f"Espinho {i}", "accent", lambda bm, x=x, y=y: prism(bm, 4, 0.1, 0.1, 0.16, top=0, y0=0, at=(x, y, -0.36), rotate=(-70, 0, 0)), 0.006)

    neck = empty("Pescoço", chest, (0, 0.28 if hero else 0.25, 0))
    AUTHORED_IN[neck.name] = torso.name
    if hero:
        part(neck, "Pele Pescoço", "skin", lambda bm: prism(bm, 6, 0.1, 0.1, 0.1, y0=0.5), 0.0)
    head = empty("Cabeça", neck, (0, 0.05, 0))
    # Jaw: chin and mouth open together (shouts on heavy blows, flinch).
    jaw = empty("Mandíbula", head, (0, 0.13, -0.02))
    AUTHORED_IN[jaw.name] = head.name
    if hero:
        # Anime proportions: a slightly narrow jaw under a rounder skull.
        part(head, "Rosto", "skin", lambda bm: ico(bm, (0.30, 0.34, 0.30), at=(0, 0.17, 0.005), subdivisions=2), 0.0)
        part(jaw, "Queixo", "skin", lambda bm: prism(bm, 6, 0.17, 0.14, 0.09, top=0.45, y0=0.02, down=True, at=(0, 0.06, 0.03)), 0.0)
        for side, label in ((-1, "E"), (1, "D")):
            # Expressive eyes: amber iris, dark crisp upper lid, a small highlight; brows that follow.
            part(head, f"Olho {label}", "iris", lambda bm, s=side: box(bm, (0.052, 0.062, 0.02), at=(0.066 * s, 0.175, 0.142), rotate=(0, 12 * s, 0)), 0.0)
            part(head, f"Pálpebra {label}", "dark", lambda bm, s=side: box(bm, (0.068, 0.016, 0.022), at=(0.068 * s, 0.208, 0.145), rotate=(0, 12 * s, -8 * s)), 0.0)
            part(head, f"Brilho {label}", "cream", lambda bm, s=side: box(bm, (0.014, 0.016, 0.01), at=(0.074 * s, 0.19, 0.155)), 0.0)
            part(head, f"Sobrancelha {label}", "hair", lambda bm, s=side: box(bm, (0.07, 0.016, 0.02), at=(0.066 * s, 0.24, 0.142), rotate=(0, 12 * s, -10 * s)), 0.0)
            # Human, rounded ears.
            part(head, f"Orelha {label}", "skin", lambda bm, s=side: ico(bm, (0.035, 0.07, 0.05), at=(0.152 * s, 0.17, -0.005)), 0.0)
        part(head, "Nariz", "nose", lambda bm: prism(bm, 3, 0.022, 0.02, 0.03, top=0.3, at=(0, 0.13, 0.148), rotate=(90, 0, 0)), 0.0)
        part(jaw, "Boca", "dark", lambda bm: box(bm, (0.045, 0.008, 0.01), at=(0, 0.085, 0.142)), 0.0)

        # Hair: a cap for volume on top, then readable clumps of irregular pointed locks.
        part(head, "Cabelo", "hair", lambda bm: ico(bm, (0.36, 0.30, 0.36), at=(0, 0.27, -0.03), subdivisions=1, jitter=0.06, seed=11), 0.0)
        tufts = [
            # (x, y, z, rot x, rot y, rot z, width, length): short irregular clumps; volume on top.
            (0.0, 0.39, -0.01, -15, 0, 4, 0.13, 0.11), (0.08, 0.385, 0.02, -5, 0, -34, 0.12, 0.10),
            (-0.08, 0.385, 0.02, -8, 0, 30, 0.12, 0.10), (0.05, 0.38, -0.09, -50, 0, -18, 0.13, 0.11),
            (-0.06, 0.38, -0.09, -48, 0, 20, 0.13, 0.11), (0.0, 0.35, -0.16, -95, 0, 6, 0.15, 0.11),
            (0.11, 0.35, -0.12, -80, 0, -45, 0.11, 0.09), (-0.11, 0.35, -0.12, -80, 0, 45, 0.11, 0.09),
            # sides over the ears
            (0.145, 0.29, 0.02, 0, 0, -100, 0.1, 0.08), (-0.145, 0.29, 0.02, 0, 0, 100, 0.1, 0.08),
            (0.14, 0.23, -0.04, 15, 0, -150, 0.09, 0.08), (-0.14, 0.23, -0.04, 15, 0, 150, 0.09, 0.08),
            # nape
            (0.06, 0.18, -0.14, -150, 0, -12, 0.1, 0.08), (-0.06, 0.18, -0.14, -150, 0, 12, 0.1, 0.08),
        ]
        for i, (x, y, z, rx, ry, rz, w, l) in enumerate(tufts):
            part(head, f"Mecha {i}", "hair", lambda bm, x=x, y=y, z=z, rx=rx, ry=ry, rz=rz, w=w, l=l:
                 prism(bm, 4, w, w * 0.7, l, top=0, at=(x, y, z), rotate=(rx, ry, rz)), 0.0)
        # Broken fringe over the forehead: uneven points that stop above the brows, keeping the eyes clear.
        fringe = [(-0.085, 0.305, 0.135, 0.09, 0.075, 10), (-0.03, 0.31, 0.15, 0.08, 0.085, -4),
                  (0.03, 0.31, 0.15, 0.08, 0.07, 8), (0.085, 0.305, 0.135, 0.09, 0.08, -12)]
        for i, (x, y, z, w, l, rz) in enumerate(fringe):
            part(head, f"Franja {i}", "hair", lambda bm, x=x, y=y, z=z, w=w, l=l, rz=rz:
                 prism(bm, 4, w, 0.045, l, top=0, at=(x, y, z), rotate=(165, 0, rz)), 0.0)
    else:
        part(head, "Rosto", "skin", lambda bm: ico(bm, (0.34, 0.36, 0.33), at=(0, 0.19, 0), subdivisions=1))
        for side, label in ((-1, "E"), (1, "D")):
            part(head, f"Olho {label}", "dark", lambda bm, s=side: box(bm, (0.05, 0.075, 0.03), at=(0.075 * s, 0.2, 0.158)), 0.01)
            part(head, f"Orelha {label}", "skin", lambda bm, s=side: prism(bm, 4, 0.06, 0.04, 0.1, top=0.3, at=(0.17 * s, 0.18, 0), rotate=(0, 0, -70 * s)), 0.008)
            part(head, f"Chifre {label}", "accent", lambda bm, s=side: prism(bm, 5, 0.09, 0.09, 0.24, top=0, at=(0.12 * s, 0.32, 0.02), rotate=(-20, 0, -25 * s)), 0.008)
            part(jaw, f"Presa {label}", "accent", lambda bm, s=side: prism(bm, 4, 0.04, 0.04, 0.07, top=0, at=(0.06 * s, 0.1, 0.15), rotate=(0, 0, 180)), 0.004)
        part(head, "Nariz", "skin", lambda bm: prism(bm, 4, 0.05, 0.05, 0.06, top=0.4, at=(0, 0.14, 0.165), rotate=(90, 0, 0)), 0.006)

    for side, label in ((-1, "E"), (1, "D")):
        # Clavicle: lifts and pulls the shoulder forward when the arm reaches up or ahead.
        clavicle = empty(f"Clavícula {label}", chest, (0.06 * side, 0.2, 0))
        AUTHORED_IN[clavicle.name] = torso.name
        arm = empty(f"Braço {label}", clavicle, (0.17 * side, 0.01, 0))
        # Elbow bone: forearm, hand and everything held move with it.
        elbow = empty(f"Cotovelo {label}", arm, (0, -0.29 if hero else -0.27, 0))
        AUTHORED_IN[elbow.name] = arm.name
        # Wrist: hand and everything held; finger bone: the fingers close around a grip.
        wrist = empty(f"Pulso {label}", elbow, (0, -0.3 if hero else -0.29, 0))
        AUTHORED_IN[wrist.name] = arm.name
        fingers = empty(f"Dedos {label}", wrist, (0, -0.06, 0.0))
        AUTHORED_IN[fingers.name] = arm.name
        if hero:
            # Short jacket sleeve with ochre cuff, charcoal shirt sleeve to the elbow, cloth wraps, fingerless glove.
            part(arm, "Manga", "tunic", lambda bm: prism(bm, 8, 0.15, 0.15, 0.17, top=0.9, down=True), 0.012)
            part(arm, "Punho Manga", "accent", lambda bm: prism(bm, 8, 0.15, 0.15, 0.025, y0=-0.155, down=True), 0.0)
            part(arm, "Manga Camisa", "shirt", lambda bm: prism(bm, 8, 0.12, 0.12, 0.14, top=0.92, y0=-0.16, down=True), 0.0)
            part(elbow, "Junta Cotovelo", "shirt", lambda bm: ico(bm, (0.105, 0.1, 0.105), at=(0, -0.29, 0), subdivisions=1), 0.0)
            part(elbow, "Antebraço", "cream", lambda bm: prism(bm, 8, 0.1, 0.1, 0.24, top=0.84, y0=-0.29, down=True), 0.0)
            for j, y in enumerate((-0.33, -0.39, -0.45)):
                part(elbow, f"Faixa {j}", "cream", lambda bm, y=y: prism(bm, 8, 0.108, 0.108, 0.012, y0=y, down=True, rotate=(0, 0, 6)), 0.0)
            part(elbow, "Luva", "glove", lambda bm: prism(bm, 8, 0.095, 0.095, 0.08, y0=-0.52, down=True), 0.004)
            part(wrist, "Mão", "glove", lambda bm: ico(bm, (0.1, 0.11, 0.09), at=(0, -0.62, 0.005)), 0.0)
            part(fingers, "Dedos", "skin", lambda bm: box(bm, (0.075, 0.035, 0.07), at=(0, -0.665, 0.02)), 0.006)
            if side > 0:
                # One-handed sword in the right fist: brass guard, dark grip, small pommel, straight steel blade.
                # The blade leaves the fist 30 degrees ahead of the forearm axis: hanging, it points down and
                # forward as in the reference's rest pose; with the arm raised it reaches nearly level.
                d = Vector((0.0, -math.cos(math.radians(30)), math.sin(math.radians(30))))
                hand = Vector((0.0, -0.62, 0.0))
                def along(t):
                    v = hand + d * t
                    return (v.x, v.y, v.z)
                tilt = (-30, 0, 0)
                part(wrist, "Arma Pomo", "brass", lambda bm: ico(bm, (0.045, 0.045, 0.045), at=along(-0.1)), 0.0)
                part(wrist, "Arma Punho", "grip", lambda bm: box(bm, (0.035, 0.16, 0.035), at=along(0.0), rotate=tilt), 0.0)
                part(wrist, "Arma Guarda", "brass", lambda bm: box(bm, (0.17, 0.035, 0.035), at=along(0.095), rotate=tilt), 0.004)
                part(wrist, "Arma", "steel", lambda bm: box(bm, (0.05, 0.72, 0.014), at=along(0.475), rotate=tilt), 0.003)
                part(wrist, "Arma Ponta", "steel", lambda bm: prism(bm, 4, 0.05, 0.014, 0.1, top=0, y0=0.0, down=True, at=along(0.835), rotate=tilt), 0.0)
            else:
                # Round shield worn on the left forearm; its outer face points away from the body (-X).
                round_shield("Escudo", elbow, (-0.07, -0.42, 0.0), (0, 0, 90), 1)
                part(wrist, "Arco", "wood", lambda bm: box(bm, (0.035, 0.05, 1.1), at=(0, -0.62, 0)), 0.006)
                part(wrist, "Arco Corda", "cream", lambda bm: box(bm, (0.008, 0.008, 1.02), at=(0.04, -0.62, 0)), 0.0)
        else:
            part(arm, "Manga", "tunic", lambda bm: prism(bm, 6, 0.14, 0.14, 0.3, top=0.85, down=True), 0.015)
            part(elbow, "Antebraço", "skin", lambda bm: prism(bm, 6, 0.105, 0.105, 0.3, top=0.85, y0=-0.27, down=True), 0.012)
            part(elbow, "Bracelete", "boots", lambda bm: prism(bm, 6, 0.125, 0.125, 0.045, y0=-0.33, down=True), 0.008)
            part(wrist, "Mão", "skin", lambda bm: ico(bm, (0.13, 0.13, 0.13), at=(0, -0.6, 0)), 0.008)
            for i in (-1, 0, 1):
                part(fingers, f"Garra {i}", "accent", lambda bm, i=i: prism(bm, 3, 0.03, 0.03, 0.07, top=0, y0=-0.64, down=True, at=(0.03 * i, 0, 0.03)), 0.0)

        leg = empty(f"Perna {label}", hips, (0.1 * side, -0.08, 0))
        # Knee bone: shin and boot move with it.
        knee = empty(f"Joelho {label}", leg, (0, -0.44 if hero else -0.43, 0))
        AUTHORED_IN[knee.name] = leg.name
        # Ankle: the foot stays flat on the ground; ball of the foot: the toes bend when the heel lifts.
        ankle = empty(f"Tornozelo {label}", knee, (0, -0.38, 0))
        AUTHORED_IN[ankle.name] = leg.name
        toes = empty(f"Ponta do pé {label}", ankle, (0, -0.06, 0.11))
        AUTHORED_IN[toes.name] = leg.name
        if hero:
            part(leg, "Coxa", "pants", lambda bm: prism(bm, 8, 0.17, 0.17, 0.44, top=0.8, down=True), 0.012)
            part(knee, "Canela", "pants", lambda bm: prism(bm, 8, 0.135, 0.135, 0.3, top=0.88, y0=-0.42, down=True), 0.01)
            part(leg, "Costura", "seam", lambda bm, s=side: box(bm, (0.012, 0.36, 0.012), at=(0.075 * s, -0.24, 0.0)), 0.0)
            part(leg, "Costura Joelho", "seam", lambda bm: box(bm, (0.13, 0.012, 0.012), at=(0, -0.3, 0.07), rotate=(0, 0, 12)), 0.0)
            part(knee, "Junta Joelho", "pants", lambda bm: ico(bm, (0.14, 0.12, 0.14), at=(0, -0.44, 0), subdivisions=1), 0.0)
            part(knee, "Joelheira", "kneepad", lambda bm: ico(bm, (0.12, 0.13, 0.07), at=(0, -0.44, 0.065), subdivisions=1), 0.0)
            # Brown boot: shaft, folded cuff, two buckle straps, foot and a darker sole.
            part(knee, "Cano da bota", "boots", lambda bm: prism(bm, 8, 0.15, 0.15, 0.2, y0=-0.64, down=True), 0.01)
            part(knee, "Dobra da bota", "boots", lambda bm: prism(bm, 8, 0.175, 0.175, 0.06, top=1.06, y0=-0.62, down=True), 0.008)
            for j, y in enumerate((-0.72, -0.79)):
                part(knee, f"Tira {j}", "leather", lambda bm, y=y: prism(bm, 8, 0.158, 0.158, 0.022, y0=y, down=True), 0.0)
                part(knee, f"Fivela {j}", "brass", lambda bm, y=y, s=side: box(bm, (0.02, 0.025, 0.03), at=(0.07 * s, y - 0.011, 0.03)), 0.0)
            part(ankle, "Bota", "boots", lambda bm: prism(bm, 8, 0.17, 0.22, 0.1, top=0.85, y0=-0.85, at=(0, 0, 0.02)), 0.018)
            part(ankle, "Sola", "sole", lambda bm: prism(bm, 8, 0.18, 0.23, 0.035, y0=-0.885, at=(0, 0, 0.02)), 0.006)
            part(toes, "Biqueira", "boots", lambda bm: prism(bm, 8, 0.165, 0.11, 0.085, top=0.8, y0=-0.85, at=(0, 0, 0.15)), 0.015)
            part(toes, "Sola Bico", "sole", lambda bm: prism(bm, 8, 0.172, 0.115, 0.035, y0=-0.885, at=(0, 0, 0.15)), 0.006)
        else:
            part(leg, "Coxa", "pants", lambda bm: prism(bm, 6, 0.17, 0.17, 0.46, top=0.85, down=True), 0.015)
            part(knee, "Canela", "pants", lambda bm: prism(bm, 6, 0.145, 0.145, 0.32, top=0.85, y0=-0.42, down=True), 0.012)
            part(knee, "Cano da bota", "boots", lambda bm: prism(bm, 6, 0.165, 0.165, 0.16, y0=-0.69, down=True), 0.012)
            part(ankle, "Bota", "boots", lambda bm: prism(bm, 6, 0.19, 0.28, 0.14, top=0.85, y0=-0.84, at=(0, 0, 0.045)), 0.02)

    glider = empty("Planador", chest, (0, 0.83, 0.05))
    if hero:
        wing = [(0.0, 0.62), (-0.55, 0.05), (-1.05, -0.28), (-0.45, -0.2), (0.0, -0.42),
                (0.45, -0.2), (1.05, -0.28), (0.55, 0.05)]
        part(glider, "Asa", "accent", lambda bm: slab(bm, list(reversed(wing)), 0.035), 0.01)
        part(glider, "Haste", "wood", lambda bm: prism(bm, 6, 0.04, 0.04, 0.55, down=True), 0.006)
        part(glider, "Longarina", "wood", lambda bm: box(bm, (1.9, 0.03, 0.04), at=(0, -0.03, -0.12)), 0.006)

    export("Characters", name)


def slab_cape(bm):
    # Short cape hanging behind the shoulders.
    tmp = bmesh.new()
    pts = [(-0.2, 0.47), (0.2, 0.47), (0.24, 0.05), (0.0, -0.03), (-0.24, 0.05)]
    front = [tmp.verts.new((x, y, -0.16)) for x, y in pts]
    back = [tmp.verts.new((x, y, -0.2)) for x, y in pts]
    tmp.faces.new(front)
    tmp.faces.new(list(reversed(back)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        tmp.faces.new((back[i], back[j], front[j], front[i]))
    merge(bm, tmp)


# ---------------------------------------------------------------- props

def pine():
    reset_scene()
    root = empty("Pinheiro")
    part(root, "Tronco", "bark", lambda bm: prism(bm, 6, 0.45, 0.45, 2.6, top=0.6), 0.03)
    for i, (w, slot) in enumerate(((2.6, "leafdark"), (1.95, "leaf"), (1.3, "leafdark"))):
        part(root, f"Copa {i}", slot, lambda bm, w=w, i=i: prism(bm, 7, w, w, 1.8, top=0, y0=1.5 + i * 1.05, rotate=(0, i * 20, 0)), 0.05)
    export("Props", "pinheiro")


def broadleaf():
    reset_scene()
    root = empty("Árvore")
    part(root, "Tronco", "bark", lambda bm: prism(bm, 6, 0.5, 0.5, 2.6, top=0.65), 0.03)
    part(root, "Galho", "bark", lambda bm: prism(bm, 5, 0.2, 0.2, 1.0, top=0.5, y0=0, at=(0.15, 1.9, 0), rotate=(0, 0, -45)), 0.02)
    part(root, "Copa", "leaf", lambda bm: ico(bm, (3.0, 2.4, 3.0), at=(0, 3.4, 0), jitter=0.12, seed=11), 0.08)
    part(root, "Copa lateral", "leafdark", lambda bm: ico(bm, (1.8, 1.5, 1.8), at=(0.95, 2.8, 0.35), jitter=0.12, seed=12), 0.06)
    part(root, "Copa traseira", "leaf", lambda bm: ico(bm, (1.6, 1.4, 1.6), at=(-0.9, 3.0, -0.4), jitter=0.12, seed=13), 0.06)
    export("Props", "arvore")


def rocks():
    for variant in range(3):
        reset_scene()
        root = empty("Pedra")
        part(root, "Pedra", "stone", lambda bm, v=variant: ico(bm, (1.0, 0.7, 1.0), at=(0, 0.25, 0), jitter=0.28, seed=100 + v), 0.04)
        if variant > 0:
            part(root, "Lasca", "stonedark", lambda bm, v=variant: ico(bm, (0.45, 0.35, 0.45), at=(0.55, 0.1, 0.25), jitter=0.3, seed=200 + v), 0.025)
        export("Props", f"pedra_{variant}")


def chest():
    reset_scene()
    root = empty("Baú")
    part(root, "Caixa", "wood", lambda bm: box(bm, (1.2, 0.5, 0.8), at=(0, 0.25, 0)), 0.035)
    part(root, "Tampa", "wood", lambda bm: prism(bm, 6, 1.24, 0.84, 0.28, top=0.82, y0=0.5, rotate=(0, 0, 0)), 0.03)
    for x in (-0.42, 0.42):
        part(root, f"Faixa {x}", "metal", lambda bm, x=x: box(bm, (0.08, 0.8, 0.86), at=(x, 0.4, 0)), 0.012)
    part(root, "Fechadura", "metal", lambda bm: box(bm, (0.16, 0.2, 0.06), at=(0, 0.48, 0.42)), 0.015)
    part(root, "Pedra preciosa", "copper", lambda bm: ico(bm, (0.07, 0.07, 0.04), at=(0, 0.48, 0.455)), 0.0)
    export("Props", "bau")


def copper_post():
    reset_scene()
    root = empty("Pilar")
    part(root, "Base", "stone", lambda bm: prism(bm, 8, 1.05, 1.05, 0.32), 0.04)
    part(root, "Coluna", "copper", lambda bm: prism(bm, 8, 0.72, 0.72, 2.75, top=0.88, y0=0.32), 0.03)
    for i, y in enumerate((0.9, 1.9)):
        part(root, f"Anel {i}", "stonedark", lambda bm, y=y: prism(bm, 8, 0.82, 0.82, 0.12, y0=y), 0.02)
    part(root, "Capitel", "stone", lambda bm: prism(bm, 8, 0.95, 0.95, 0.22, top=0.8, y0=3.05), 0.03)
    part(root, "Cristal", "copper", lambda bm: prism(bm, 4, 0.32, 0.32, 0.36, top=0, y0=3.22), 0.02)
    export("Props", "pilar_cobre")


def brick_wall(width, height, depth, seed, name, slots=("stone", "stonedark")):
    """Wall of chamfered bricks with staggered joints. Origin at the bottom center."""
    reset_scene()
    root = empty(name)
    rng = random.Random(seed)
    row_h = 0.5
    rows = max(1, round(height / row_h))
    row_h = height / rows
    along_x = width >= depth
    length = width if along_x else depth
    thick = depth if along_x else width
    for r in range(rows):
        x = -length / 2
        first = True
        while x < length / 2 - 1e-3:
            brick = rng.uniform(0.8, 1.25) if not (first and r % 2) else rng.uniform(0.4, 0.6)
            first = False
            brick = min(brick, length / 2 - x)
            cx = x + brick / 2
            y = r * row_h + row_h / 2
            inset = rng.uniform(0.0, 0.04)
            size = (brick - 0.03, row_h - 0.03, thick - inset)
            at = (cx, y, 0) if along_x else (0, y, cx)
            dims = size if along_x else (size[2], size[1], size[0])
            slot = slots[0] if rng.random() > 0.25 else slots[1]
            part(root, f"Pedra {r}-{cx:.2f}", slot, lambda bm, d=dims, a=at: box(bm, d, at=a), 0.035)
            x += brick
    export("Props", name.lower())


def gate():
    reset_scene()
    root = empty("Portao")
    part(root, "Laje", "stone", lambda bm: box(bm, (6.0, 4.0, 0.5), at=(0, 2.0, 0)), 0.06)
    for i, (x, y, w, h) in enumerate(((0, 3.4, 4.6, 0.12), (0, 0.6, 4.6, 0.12), (-2.3, 2.0, 0.12, 2.9), (2.3, 2.0, 0.12, 2.9))):
        part(root, f"Friso {i}", "copper", lambda bm, x=x, y=y, w=w, h=h: box(bm, (w, h, 0.08), at=(x, y, -0.27)), 0.02)
    part(root, "Emblema", "copper", lambda bm: prism(bm, 6, 0.9, 0.9, 0.12, at=(0, 2.0, -0.27), rotate=(90, 0, 0)), 0.03)
    part(root, "Núcleo", "accent", lambda bm: prism(bm, 3, 0.4, 0.4, 0.14, at=(0, 2.0, -0.3), rotate=(90, 0, 0)), 0.02)
    export("Props", "portao")


def roof():
    reset_scene()
    root = empty("Teto")
    part(root, "Laje", "stonedark", lambda bm: box(bm, (7.0, 0.35, 7.0), at=(0, 0.175, 0)), 0.05)
    part(root, "Borda", "stone", lambda bm: box(bm, (7.3, 0.15, 7.3), at=(0, 0.42, 0)), 0.04)
    export("Props", "teto")


def grass_tile():
    reset_scene()
    root = empty("Grama")
    # Origin matches the 2 x 0.3 x 2 trigger box center.
    part(root, "Leito", "grass", lambda bm: box(bm, (1.94, 0.12, 1.94), at=(0, -0.09, 0)), 0.03)
    rng = random.Random(5)
    for i in range(7):
        x, z = rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7)
        h = rng.uniform(0.25, 0.42)
        part(root, f"Tufo {i}", "grass", lambda bm, x=x, z=z, h=h, i=i: prism(bm, 3, 0.22, 0.22, h, top=0, y0=-0.04, at=(x, 0, z), rotate=(rng.uniform(-12, 12), i * 47, rng.uniform(-12, 12))), 0.0)
    export("Props", "grama")


# ---------------------------------------------------------------- main

def main():
    humanoid("heroi", "hero")
    humanoid("cascudo", "cascudo")
    pine()
    broadleaf()
    rocks()
    chest()
    copper_post()
    brick_wall(0.5, 4.0, 6.5, 1, "Muro_lateral")
    brick_wall(7.0, 4.0, 0.5, 2, "Muro_fundo")
    gate()
    roof()
    grass_tile()
    print("\n=== Zeldexile models ===")
    for line in REPORT:
        print(line)
    print(f"FBX: {MODELS_DIR}\nBlend: {BLEND_DIR}")


if __name__ == "__main__":
    main()
