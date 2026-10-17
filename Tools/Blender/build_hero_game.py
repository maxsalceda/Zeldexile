"""
Versão para o jogo do herói NPR: low-poly chanfrado, FBX plano com os ossos do CharacterRig.

Reaproveita o construtor de build_hero_hd.py em modo LOW (poucos segmentos, sem subdivisão nem contorno) e
em pose de descanso, e converte o resultado para as convenções de build_models.py:
  * cada malha vira "Osso~Peça@slot" com origem no próprio centro, faces chapadas, arestas chanfradas
    (1 segmento, em metros) e material "slot_<slot>" (o Unity dá a cor pelo slot: RigStyle.ColorFor);
  * os ossos são empties soltos (sem parentesco) nas posições absolutas; o Unity remonta a hierarquia;
  * eixos do jogo: frente = +Y no Blender (Unity +Z), direita = +X; o modelo HD é construído com a frente
    em -Y, então tudo gira 180° em Z;
  * equipamento duplicado: na mão ("Arma ...", "Escudo ...", "Arco") e guardado ("... Costas ...").

Saída: <Resources/Models/Characters>/heroi_npr.fbx e Art/Blender/heroi_npr.blend. Nada substitui o heroi.fbx
atual: para o jogo usar este modelo, mude RigStyle.Hero.Model para "heroi_npr".

Uso:
  blender.exe -b -P Tools/Blender/build_hero_game.py -- [<Resources/Models dir>] [<blend dir>] [<render dir>]
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import build_hero_hd as hd  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODELS_DIR = os.path.abspath(ARGS[0]) if len(ARGS) > 0 else os.path.join(ROOT, "Zeldexile", "Assets", "Zeldexile", "Resources", "Models")
BLEND_DIR = os.path.abspath(ARGS[1]) if len(ARGS) > 1 else os.path.join(ROOT, "Art", "Blender")
RENDER_DIR = os.path.abspath(ARGS[2]) if len(ARGS) > 2 else os.path.join(ROOT, "Art", "Render")
NAME = "heroi_npr"

# Ossos que o CharacterRig conhece. Malhas sob "Espada"/"Escudo" pertencem ao osso acima deles.
BONES = {
    "Quadril", "Tronco", "Tórax", "Bolsa", "Pescoço", "Cabeça", "Mandíbula", "Clavícula E", "Clavícula D",
    "Braço E", "Braço D", "Cotovelo E", "Cotovelo D", "Pulso E", "Pulso D", "Dedos E", "Dedos D",
    "Perna E", "Perna D", "Joelho E", "Joelho D", "Tornozelo E", "Tornozelo D", "Ponta do pé E", "Ponta do pé D", "Planador",
}

# Material do modelo HD -> slot do jogo (RigStyle.ColorFor).
SLOTS = {
    "pele": "skin", "cabelo": "hair", "iris": "iris", "pupila": "dark", "branco": "cream", "escuro": "dark",
    "jaqueta": "tunic", "dourado": "accent", "camisa": "shirt", "faixa": "cream", "luva": "glove",
    "couro": "leather", "bolsa": "bag", "calca": "pants", "listra": "seam", "aco": "steel", "bota": "boots",
    "cano": "boots", "sola": "sole", "lamina": "steel", "cabo": "grip", "madeira": "wood",
    "madeira_escura": "wooddark", "latao": "brass",
}
SLOT_BY_PART = {"Joelheira": "kneepad", "Nariz": "nose", "Sobrancelha": "dark"}
# Discos chapados do olho: sem contorno no jogo (ModelLibrary.NoOutline), senão viram óculos pretos.
SLOT_BY_NAME = {"Branco": "eyewhite", "Brilho": "eyewhite"}
# Peças grandes levam o chanfro de 1 cm da regra de arte; detalhes, esferas e mechas ficam sem (como no herói atual).
CHAMFERED = {
    "Pelve", "Cintura", "Peito", "Jaqueta", "Cinto", "Coxa", "Canela", "Bota Cano", "Bota Dobra",
    "Manga Camisa", "Antebraço", "Luva", "Pescoço Pele",
}

# Cores de visualização do .blend; no jogo a cor vem do slot.
SLOT_COLORS = {
    "skin": (0.93, 0.70, 0.52), "hair": (0.50, 0.22, 0.15), "tunic": (0.18, 0.42, 0.48), "pants": (0.30, 0.31, 0.37),
    "boots": (0.50, 0.32, 0.20), "accent": (0.80, 0.62, 0.28), "steel": (0.80, 0.82, 0.88), "dark": (0.08, 0.07, 0.10),
    "cream": (0.86, 0.79, 0.68), "leather": (0.45, 0.29, 0.18), "wood": (0.58, 0.37, 0.22), "wooddark": (0.40, 0.25, 0.15),
    "shirt": (0.20, 0.20, 0.23), "glove": (0.22, 0.20, 0.20), "brass": (0.85, 0.66, 0.28), "grip": (0.25, 0.18, 0.14),
    "kneepad": (0.50, 0.48, 0.50), "seam": (0.22, 0.22, 0.27), "iris": (0.62, 0.36, 0.16), "sole": (0.18, 0.13, 0.10),
    "bag": (0.68, 0.47, 0.30), "nose": (0.86, 0.64, 0.48), "eyewhite": (1.0, 1.0, 1.0),
}

TURN = Matrix.Rotation(math.pi, 4, "Z")  # frente -Y (modelo HD) -> frente +Y (jogo)


def slot_material(slot):
    name = "slot_" + slot
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        c = SLOT_COLORS.get(slot, (1, 1, 1))
        mat.diffuse_color = (c[0], c[1], c[2], 1.0)
    return mat


def bone_of(ob):
    p = ob.parent
    while p is not None and p.name not in BONES:
        p = p.parent
    return p.name if p is not None else "Quadril"


def finish(bm, part, slot, bone, chamfer):
    """Como build_models.finish: chanfro, origem no centro, normais para fora, faces chapadas, nome e slot."""
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    if chamfer > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=chamfer, offset_type="OFFSET", segments=1,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    lo = Vector((min(v.co.x for v in bm.verts), min(v.co.y for v in bm.verts), min(v.co.z for v in bm.verts)))
    hi = Vector((max(v.co.x for v in bm.verts), max(v.co.y for v in bm.verts), max(v.co.z for v in bm.verts)))
    center = (lo + hi) / 2
    for v in bm.verts:
        v.co -= center
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    if sum(f.calc_center_median().dot(f.normal) * f.calc_area() for f in bm.faces) < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(part)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = False
    mesh.materials.append(slot_material(slot))
    ob = bpy.data.objects.new(f"{bone}~{part}@{slot}", mesh)
    ob.location = center
    bpy.context.scene.collection.objects.link(ob)
    return ob


def world_bmesh(ob, dg, extra=Matrix.Identity(4)):
    """Malha avaliada (com a espessura aplicada) em coordenadas do jogo."""
    bm = bmesh.new()
    bm.from_mesh(ob.evaluated_get(dg).data)
    m = extra @ TURN @ ob.matrix_world
    for v in bm.verts:
        v.co = m @ v.co
    return bm


def convert(J):
    """Converte a coleção "Herói HD" (modo LOW) em malhas soltas no padrão do jogo e apaga a original."""
    dg = bpy.context.evaluated_depsgraph_get()
    hd_objects = [ob for ob in hd.COLL.objects if ob.type == "MESH" and not ob.hide_render]
    sword_frame = TURN @ J["Espada"].matrix_world
    shield_frame = TURN @ J["Escudo"].matrix_world
    chest = TURN @ J["Tórax"].matrix_world.translation
    wrist_l = TURN @ J["Pulso E"].matrix_world.translation
    # Guardado nas costas: bainha do ombro esquerdo ao quadril direito, escudo centrado nas costas (como o
    # herói atual). Matrizes que levam o equipamento de onde está (na mão) para lá.
    sword_axis = Vector((-0.32, 0, 0.95)).normalized()  # pomo para cima, à esquerda
    sword_back = Matrix.Translation(chest + Vector((-0.06, -0.17, 0.2)) + sword_axis * 0.45) @ hd.aim(sword_axis)
    shield_back = Matrix.Translation(chest + Vector((0.03, -0.215, 0.22))) @ hd.aim(Vector((0, -1, 0)))
    to_sword_back = sword_back @ sword_frame.inverted()
    to_shield_back = shield_back @ shield_frame.inverted()

    made = []
    for ob in hd_objects:
        part = ob.name
        slot = SLOT_BY_PART.get(part.split(" ")[0], SLOTS.get(ob.get("slot", ""), "accent"))
        if part.startswith("Olho"):
            slot = SLOT_BY_NAME.get(part.split(" ")[-1], slot)
        chamfer = float(ob.get("chamfer", 0.0)) or (0.01 if part.rstrip(" ED0123456789") in CHAMFERED else 0.0)
        bone = bone_of(ob)
        made.append(finish(world_bmesh(ob, dg), part, slot, bone, chamfer))
        if part.startswith("Arma"):
            made.append(finish(world_bmesh(ob, dg, to_sword_back), part.replace("Arma", "Arma Costas", 1), slot, "Tórax", chamfer))
        elif part.startswith("Escudo"):
            made.append(finish(world_bmesh(ob, dg, to_shield_back), part.replace("Escudo", "Escudo Costas", 1), slot, "Tórax", chamfer))

    # Bainha de couro em volta da lâmina guardada; arco simples na mão esquerda e nas costas.
    bm = bmesh.new()
    hd.loft(bm, [(-0.10, 0.04, 0.016), (-0.70, 0.03, 0.012), (-0.78, 0.012, 0.006)], n=8, rotate=sword_back.to_3x3(), at=sword_back.translation)
    made.append(finish(bm, "Arma Costas Bainha", "leather", "Tórax", 0.006))
    bm = bmesh.new()
    hd.box(bm, (0.035, 0.05, 1.1), at=wrist_l + Vector((0, 0, -0.09)))
    made.append(finish(bm, "Arco", "wood", "Pulso E", 0.006))
    bm = bmesh.new()
    hd.box(bm, (0.008, 0.008, 1.02), at=wrist_l + Vector((0.04, 0, -0.09)))
    made.append(finish(bm, "Arco Corda", "cream", "Pulso E", 0.0))
    bm = bmesh.new()
    hd.box(bm, (0.032, 0.045, 1.0), at=chest + Vector((0.14, -0.27, 0.2)), rotate=(0, 24, 0))
    made.append(finish(bm, "Arco Costas", "wood", "Tórax", 0.006))
    bm = bmesh.new()
    hd.box(bm, (0.008, 0.008, 0.94), at=chest + Vector((0.16, -0.295, 0.2)), rotate=(0, 24, 0))
    made.append(finish(bm, "Arco Costas Corda", "cream", "Tórax", 0.0))

    # Ossos: empties soltos nas posições absolutas. Clavículas, dedos e pontas dos pés vêm do herói atual.
    positions = {name: TURN @ ob.matrix_world.translation for name, ob in J.items() if name in BONES}
    for label in "ED":
        arm = positions[f"Braço {label}"]
        positions[f"Clavícula {label}"] = Vector((arm.x * 0.3, arm.y, arm.z))
        positions[f"Dedos {label}"] = positions[f"Pulso {label}"] + Vector((0, 0, -0.06))
        positions[f"Ponta do pé {label}"] = positions[f"Tornozelo {label}"] + Vector((0, 0.11, -0.06))
    positions["Mandíbula"] = positions["Cabeça"] + Vector((0, -0.02, 0.13))
    positions["Planador"] = positions["Tórax"] + Vector((0, 0.05, 0.83))

    # O modelo HD sai antes dos empties entrarem: ele usa os mesmos nomes (empties e esferas de junta), e um
    # "Joelho D.001" não é um osso para o CharacterRig.
    for ob in list(hd.COLL.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.collections.remove(hd.COLL)
    for name, pos in positions.items():
        ob = bpy.data.objects.new(name, None)
        ob.empty_display_size = 0.08
        ob.location = pos
        bpy.context.scene.collection.objects.link(ob)
    for mat in list(bpy.data.materials):
        if not mat.name.startswith("slot_"):
            bpy.data.materials.remove(mat)
    return made


def export():
    out_dir = os.path.join(MODELS_DIR, "Characters")
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(BLEND_DIR, exist_ok=True)
    tris = sum(sum(len(p.vertices) - 2 for p in ob.data.polygons) for ob in bpy.context.scene.objects if ob.type == "MESH")
    meshes = sum(1 for ob in bpy.context.scene.objects if ob.type == "MESH")
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, NAME + ".fbx"),
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y",
        bake_space_transform=True,
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, NAME + ".blend"))
    return tris, meshes


def preview():
    """Render Workbench (cores por slot, chapado) de frente 3/4 e de costas, para conferência."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_object_outline = True
    scene.render.film_transparent = True
    scene.render.resolution_x, scene.render.resolution_y = 724, 1086
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    cam = bpy.data.objects.new("Câmera", bpy.data.cameras.new("Câmera"))
    cam.data.lens = 55
    scene.collection.objects.link(cam)
    scene.camera = cam
    os.makedirs(RENDER_DIR, exist_ok=True)
    for view, yaw in (("frente", 35), ("costas", 200)):
        # Frente do jogo = +Y: a câmera fica em +Y para a vista de frente.
        target = Vector((0, 0, 0.95))
        a = math.radians(yaw)
        cam.location = target + Vector((4.2 * math.sin(a), 4.2 * math.cos(a), 0.2))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(RENDER_DIR, f"{NAME}_{view}.png")
        bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_homefile(use_empty=True)
    hd.LOW = True
    hd.POSE = False
    J = hd.build_hero()
    convert(J)
    tris, meshes = export()
    print(f"{NAME}: {tris} triângulos, {meshes} malhas, {sum(1 for o in bpy.context.scene.objects if o.type == 'EMPTY')} ossos")
    for ob in bpy.context.scene.objects:
        if ob.type == "MESH" and ("~" not in ob.name or "@" not in ob.name):
            print("AVISO: nome fora do padrão:", ob.name)
    preview()


if __name__ == "__main__":
    main()
