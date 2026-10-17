"""
Herói em alta resolução, estilo NPR (cel shading de anime), para Blender 3.6 + EEVEE.

Gera do zero o aventureiro de Art/Referencias/aventureiro-quatro-vistas.png: malha suave (lofts, esferas e
fitas com Subdivision Surface, sem facetamento), materiais toon de dois tons (Diffuse -> Shader to RGB ->
Color Ramp em degrau), contorno 2D por inverted hull (Solidify com normais invertidas e material preto com
backface culling) e uma esfera invisível que empresta suas normais ao rosto (Data Transfer), para que o nariz
e as bochechas não ganhem sombras realistas.

O modelo NÃO é o herói do jogo (esse continua em build_models.py / heroi.fbx): é arte de alta para render,
concept ou fonte de baking. Nenhum arquivo do jogo é tocado.

Uso em lote (gera, salva o .blend, renderiza as quatro vistas e monta a comparação):
  blender.exe -b -P Tools/Blender/build_hero_hd.py
  python Tools/Blender/build_hero_hd.py --compose      # só remonta a prancha de comparação (precisa de Pillow)

Opções (depois de "--" no Blender): --blend <arquivo.blend>  --render <pasta>  --ref <imagem>  --no-render

Uso na aba Scripting do Blender: abra este arquivo em Text > Open e clique em Run Script. Nesse modo o script
constrói o personagem na cena atual (numa coleção "Herói HD") e não salva nem renderiza nada: você fica com o
modelo, os materiais e os modificadores para editar à vontade.

Eixos (Blender): +Z para cima, frente do personagem = -Y, direita do personagem = -X. 1 unidade = 1 m.
As peças são autoradas no espaço local do seu "osso" (um empty); a pose de combate é dada rotacionando os
empties, como no herói do jogo (segmentos rígidos cobertos por esferas nas juntas).
"""

import math
import os
import random
import sys

try:
    import bmesh
    import bpy
    from mathutils import Euler, Matrix, Vector
    IN_BLENDER = True
except ImportError:  # modo --compose, rodado pelo Python do sistema
    IN_BLENDER = False

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]


def arg(flag, default):
    return ARGS[ARGS.index(flag) + 1] if flag in ARGS and ARGS.index(flag) + 1 < len(ARGS) else default


if "__file__" in globals():
    ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
else:  # aba Scripting
    ROOT = os.path.dirname(os.path.abspath(bpy.data.filepath)) if IN_BLENDER and bpy.data.filepath else os.getcwd()

BLEND_PATH = arg("--blend", os.path.join(ROOT, "Art", "Blender", "heroi_hd.blend"))
RENDER_DIR = arg("--render", os.path.join(ROOT, "Art", "Render"))
REF_PATH = arg("--ref", os.path.join(ROOT, "Art", "Referencias", "aventureiro-quatro-vistas.png"))
BATCH = IN_BLENDER and bpy.app.background
DO_RENDER = BATCH and "--no-render" not in ARGS

LOW = False                 # modo low-poly (build_hero_game.py): poucos segmentos, sem subdivisão nem contorno
POSE = True                 # False deixa o personagem em pose de descanso (braços caídos), para o rig do jogo
OUTLINE_THICKNESS = 0.003   # espessura do inverted hull, em metros
SUBDIV = 2                  # níveis de Subdivision Surface nas peças orgânicas
TOON_THRESHOLD = 0.42       # ponto de corte luz/sombra no Color Ramp
VIEWS = [  # (nome, yaw da câmera em graus; 0 = de frente, positivo gira para o lado esquerdo do personagem)
    ("frente", -28), ("costas", 165), ("perfil", -100), ("costas_3_4", 128),
]
VIEW_SIZE = (724, 543)      # um quadrante da referência

# Cores base tiradas das áreas chapadas da referência (não dos brilhos nem das sombras), em linear RGB.
COLORS = {
    "pele": (0.93, 0.70, 0.55), "cabelo": (0.46, 0.15, 0.10), "iris": (0.86, 0.50, 0.12),
    "pupila": (0.12, 0.06, 0.03), "branco": (0.97, 0.97, 0.96), "escuro": (0.07, 0.05, 0.05),
    "jaqueta": (0.09, 0.33, 0.40), "dourado": (0.80, 0.58, 0.16), "camisa": (0.12, 0.12, 0.15),
    "faixa": (0.88, 0.84, 0.74), "luva": (0.14, 0.11, 0.11), "couro": (0.42, 0.25, 0.13),
    "bolsa": (0.56, 0.36, 0.20), "calca": (0.22, 0.26, 0.36), "listra": (0.52, 0.54, 0.58),
    "aco": (0.66, 0.68, 0.72), "bota": (0.48, 0.30, 0.15), "cano": (0.64, 0.44, 0.24),
    "sola": (0.20, 0.14, 0.10), "lamina": (0.80, 0.82, 0.86), "cabo": (0.20, 0.14, 0.12),
    "madeira": (0.60, 0.38, 0.19), "madeira_escura": (0.40, 0.24, 0.12), "latao": (0.85, 0.65, 0.20),
}

MATS = {}
COLL = None
OUTLINE_MAT = None
REPORT = []


# ---------------------------------------------------------------- materiais NPR

def toon_material(name, color, shadeless=False, threshold=TOON_THRESHOLD):
    """Cel shading de dois tons: a luz da cena entra num Diffuse, vira cor (Shader to RGB) e passa por um
    Color Ramp em modo Constant, que decide luz ou sombra sem degradê. O resultado escolhe entre a cor base e
    a cor de sombra (base escurecida e puxada para o azul) e sai por um Emission, para nada mais iluminar."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1.0)
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (600, 0)
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.location = (400, 0)
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    if shadeless:
        emit.inputs["Color"].default_value = (*color, 1.0)
        return mat
    diffuse = nt.nodes.new("ShaderNodeBsdfDiffuse")
    diffuse.location = (-600, 0)
    to_rgb = nt.nodes.new("ShaderNodeShaderToRGB")
    to_rgb.location = (-400, 0)
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.location = (-200, 0)
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0, 0, 0, 1)
    ramp.color_ramp.elements[1].position = threshold
    ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
    mix = nt.nodes.new("ShaderNodeMixRGB")
    mix.location = (150, 0)
    mix.blend_type = "MIX"
    shadow = (color[0] * 0.64, color[1] * 0.56, min(1.0, color[2] * 0.66 + 0.03))
    mix.inputs["Color1"].default_value = (*shadow, 1.0)
    mix.inputs["Color2"].default_value = (*color, 1.0)
    nt.links.new(diffuse.outputs["BSDF"], to_rgb.inputs["Shader"])
    nt.links.new(to_rgb.outputs["Color"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], mix.inputs["Fac"])
    nt.links.new(mix.outputs["Color"], emit.inputs["Color"])
    return mat


def outline_material():
    """Preto chapado só nas faces de trás: com o Solidify invertendo as normais, o casco expandido só aparece
    pela borda, como um contorno desenhado."""
    mat = bpy.data.materials.new("Contorno")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    mat.use_backface_culling = True
    mat.shadow_method = "NONE"
    mat.diffuse_color = (0, 0, 0, 1)
    return mat


def add_outline(ob, thickness=OUTLINE_THICKNESS):
    """Inverted hull: Solidify para fora, normais invertidas, sem borda, material do slot seguinte (preto)."""
    ob.data.materials.append(OUTLINE_MAT)
    mod = ob.modifiers.new("Contorno", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.use_quality_normals = True
    mod.material_offset = 1
    return mod


# ---------------------------------------------------------------- geometria (bmesh)

def xform(at=(0, 0, 0), rotate=(0, 0, 0), scale=(1, 1, 1)):
    if isinstance(rotate, Matrix):
        rot = rotate.to_4x4() if len(rotate) == 3 else rotate
    else:
        rot = Euler([math.radians(a) for a in rotate], "XYZ").to_matrix().to_4x4()
    return Matrix.Translation(Vector(at)) @ rot @ Matrix.Diagonal((*scale, 1.0))


def aim(direction):
    """Rotação que leva +Z para a direção dada."""
    return Vector((0, 0, 1)).rotation_difference(Vector(direction).normalized()).to_matrix().to_4x4()


def ring_xy(ring, k, n, gap):
    """Ponto k de um anel (z, rx, ry[, cx, cy]) no plano XY; com gap, o anel fica aberto na frente (-Y)."""
    z, rx, ry = ring[:3]
    cx, cy = (ring[3], ring[4]) if len(ring) > 4 else (0.0, 0.0)
    if gap:
        a = math.radians(270 + gap + (360 - 2 * gap) * k / n)
    else:
        a = 2 * math.pi * k / n
    return Vector((cx + rx * math.cos(a), cy + ry * math.sin(a), z))


def loft(bm, rings, n=16, at=(0, 0, 0), rotate=(0, 0, 0), caps=(True, True), gap=None):
    """Tubo por anéis elípticos empilhados em Z. ring = (z, rx, ry) ou (z, rx, ry, cx, cy).
    gap: abertura (meio-ângulo, graus) na frente, por anel (lista) ou para todos; anéis abertos não têm tampa."""
    if LOW:
        n = min(n, 12 if n >= 20 else 8)
    M = xform(at, rotate)
    loops = []
    for i, ring in enumerate(rings):
        g = (gap[i] if isinstance(gap, (list, tuple)) else gap) if gap else 0
        count = n + 1 if gap else n
        loops.append([bm.verts.new(M @ ring_xy(ring, k, n, g)) for k in range(count)])
    for a, b in zip(loops, loops[1:]):
        m = len(a)
        for k in range(m if not gap else m - 1):
            bm.faces.new((a[k], a[(k + 1) % m], b[(k + 1) % m], b[k]))
    if not gap:
        for ring, verts, cap, flip in ((rings[0], loops[0], caps[0], True), (rings[-1], loops[-1], caps[1], False)):
            if not cap:
                continue
            cx, cy = (ring[3], ring[4]) if len(ring) > 4 else (0.0, 0.0)
            c = bm.verts.new(M @ Vector((cx, cy, ring[0])))
            for k in range(n):
                tri = (c, verts[k], verts[(k + 1) % n])
                bm.faces.new(tri[::-1] if flip else tri)
    return loops


def sphere(bm, r, at=(0, 0, 0), rotate=(0, 0, 0), seg=16, rings=12):
    r = (r, r, r) if isinstance(r, (int, float)) else r
    if LOW:
        seg, rings = min(seg, 12 if seg >= 20 else 8), min(rings, 8 if rings >= 14 else 6)
        if max(r) < 0.02:
            seg, rings = 6, 4
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0, matrix=xform(at, rotate, r))


def box(bm, size, at=(0, 0, 0), rotate=(0, 0, 0)):
    bmesh.ops.create_cube(bm, size=1.0, matrix=xform(at, rotate, size))


def disc(bm, rx, ry, at, normal, roll=0.0, n=24):
    """Elipse plana (rx, ry) centrada em `at`, virada para `normal`; roll gira no próprio plano."""
    M = xform(at, aim(normal) @ Euler((0, 0, math.radians(roll))).to_matrix().to_4x4(), (rx, ry, 1))
    bmesh.ops.create_circle(bm, cap_ends=True, radius=1.0, segments=8 if LOW else n, matrix=M)


def ribbon(bm, pts, nrms, width, thick):
    """Fita de seção retangular ao longo de uma polilinha; nrms = normal (para fora) em cada ponto."""
    pts = [Vector(p) for p in pts]
    nrms = [Vector(v).normalized() for v in nrms]
    rings = []
    for i, (p, nrm) in enumerate(zip(pts, nrms)):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        s = t.cross(nrm).normalized()
        nn = s.cross(t).normalized()
        rings.append([bm.verts.new(p + s * (width / 2) + nn * (thick / 2)), bm.verts.new(p - s * (width / 2) + nn * (thick / 2)),
                      bm.verts.new(p - s * (width / 2) - nn * (thick / 2)), bm.verts.new(p + s * (width / 2) - nn * (thick / 2))])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(4):
            bm.faces.new((r0[k], r0[(k + 1) % 4], r1[(k + 1) % 4], r1[k]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])


# ---------------------------------------------------------------- objetos

def joint(name, parent, at):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "SPHERE"
    ob.empty_display_size = 0.04
    ob.location = at
    ob.parent = parent
    COLL.objects.link(ob)
    return ob


def make(name, parent, mat, build, subsurf=SUBDIV, bevel=None, solidify=None, outline=True, smooth=True):
    """Cria uma malha no espaço local de `parent`. Peças orgânicas usam Subdivision; peças duras, Bevel.
    Ordem dos modificadores: espessura (solidify da peça) -> subdivisão/chanfro -> contorno (inverted hull)."""
    bm = bmesh.new()
    build(bm)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    ob = bpy.data.objects.new(name, me)
    ob.parent = parent
    ob.data.materials.append(MATS[mat] if isinstance(mat, str) else mat)
    COLL.objects.link(ob)
    if LOW:
        # Low-poly: a espessura continua geometria; subdivisão, chanfro e contorno viram um chanfro de 1
        # segmento aplicado por build_hero_game.py (0 nos detalhes chapados, que não levam contorno).
        if solidify:
            # Para dentro e mais grossa que o contorno do jogo (ToonOutline cresce 2,2 cm a partir do centro
            # da peça): senão o casco da face interna sai pela frente e a jaqueta fica preta.
            mod = ob.modifiers.new("Espessura", "SOLIDIFY")
            mod.thickness = max(solidify, 0.04)
            mod.offset = -1.0
            mod.use_even_offset = True
        ob["chamfer"] = bevel if bevel else 0.0
        ob["slot"] = mat if isinstance(mat, str) else ""
        return ob
    if solidify:
        mod = ob.modifiers.new("Espessura", "SOLIDIFY")
        mod.thickness = solidify
        mod.offset = 0.0
        mod.use_even_offset = True
    if subsurf:
        mod = ob.modifiers.new("Subdivisão", "SUBSURF")
        mod.levels = subsurf
        mod.render_levels = subsurf
    if bevel:
        mod = ob.modifiers.new("Chanfro", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = "ANGLE"
    if outline:
        add_outline(ob)
    return ob


# ---------------------------------------------------------------- o personagem

def body_front(rings, x, z, back=False, lift=0.004):
    """Ponto (e normal) na superfície de um loft de anéis, na frente (ou atrás) do corpo, para fitas."""
    rings = sorted(rings)
    lo = max([r for r in rings if r[0] <= z], default=rings[0])
    hi = min([r for r in rings if r[0] >= z], default=rings[-1])
    t = 0.0 if hi[0] == lo[0] else (z - lo[0]) / (hi[0] - lo[0])
    rx = lo[1] + (hi[1] - lo[1]) * t
    ry = lo[2] + (hi[2] - lo[2]) * t
    x = max(-rx * 0.97, min(rx * 0.97, x))
    y = ry * math.sqrt(max(0.0, 1 - (x / rx) ** 2)) * (1 if back else -1)
    nrm = Vector((x / rx ** 2, y / ry ** 2, 0)).normalized()
    return Vector((x, y, z)) + nrm * lift, nrm


def build_hero():
    global COLL, OUTLINE_MAT
    COLL = bpy.data.collections.new("Herói HD")
    bpy.context.scene.collection.children.link(COLL)
    OUTLINE_MAT = outline_material()
    for key, color in COLORS.items():
        MATS[key] = toon_material(key, color)
    for key in ("iris", "pupila", "branco", "escuro"):
        MATS[key] = toon_material(key + " (chapado)", COLORS[key], shadeless=True)

    rnd = random.Random(7)
    J = {}

    # ---- quadril e tronco
    hips = J["Quadril"] = joint("Quadril", None, (0, 0, 0.93))
    make("Pelve", hips, "calca", lambda bm: loft(bm, [(-0.15, 0.17, 0.12), (-0.06, 0.175, 0.125), (0.06, 0.165, 0.11)]))

    torso = J["Tronco"] = joint("Tronco", hips, (0, 0, 0.05))
    waist_rings = [(-0.05, 0.165, 0.115), (0.02, 0.155, 0.105), (0.07, 0.150, 0.100), (0.14, 0.158, 0.105), (0.21, 0.172, 0.112)]
    make("Cintura", torso, "camisa", lambda bm: loft(bm, waist_rings))
    make("Cinto", torso, "couro", lambda bm: loft(bm, [(-0.035, 0.174, 0.124), (0.025, 0.170, 0.120)]), subsurf=1)
    make("Fivela", torso, "latao", lambda bm: box(bm, (0.055, 0.014, 0.045), at=(0, -0.127, -0.005)), subsurf=0, bevel=0.004)
    make("Fivela Pino", torso, "latao", lambda bm: box(bm, (0.008, 0.016, 0.03), at=(0, -0.128, -0.005)), subsurf=0, bevel=0.002)

    chest = J["Tórax"] = joint("Tórax", torso, (0, 0, 0.18))
    chest_rings = [(-0.03, 0.160, 0.105), (0.06, 0.175, 0.115), (0.14, 0.190, 0.125), (0.22, 0.195, 0.120), (0.27, 0.170, 0.105), (0.30, 0.095, 0.080)]
    make("Peito", chest, "camisa", lambda bm: loft(bm, chest_rings))
    # Cordão do decote: duas tirinhas cruzadas sobre a camisa.
    for i, rz in enumerate((28, -28)):
        make(f"Cordão {i}", chest, "faixa", lambda bm, rz=rz: loft(bm, [(-0.035, 0.004, 0.004), (0.035, 0.004, 0.004)], n=8, at=(0, -0.122, 0.12), rotate=(0, 0, rz)), subsurf=1, outline=False)

    # Jaqueta: casca aberta na frente (V que alarga para cima), com espessura; lapelas, barra e gola douradas.
    jacket_rings = [(-0.14, 0.176, 0.126), (-0.05, 0.178, 0.124), (0.06, 0.195, 0.135), (0.14, 0.210, 0.145), (0.22, 0.212, 0.138), (0.265, 0.185, 0.118)]
    jacket_gap = [8, 12, 20, 30, 40, 46]
    make("Jaqueta", chest, "jaqueta", lambda bm: loft(bm, jacket_rings, n=24, gap=jacket_gap), solidify=0.014)

    def lapel(bm, side):
        pts, nrms = [], []
        for ring, g in zip(jacket_rings, jacket_gap):
            k = 0 if side < 0 else 24
            p = ring_xy(ring, k, 24, g)
            pts.append(p)
            nrms.append(Vector((p.x / ring[1] ** 2, p.y / ring[2] ** 2, 0)).normalized())
        ribbon(bm, pts, nrms, 0.034, 0.012)

    for side, label in ((-1, "D"), (1, "E")):
        make(f"Lapela {label}", chest, "dourado", lambda bm, s=side: lapel(bm, s), subsurf=1)

    def hem(bm):
        ring, g = jacket_rings[0], jacket_gap[0]
        pts = [ring_xy(ring, k, 24, g) for k in range(25)]
        nrms = [Vector((p.x / ring[1] ** 2, p.y / ring[2] ** 2, 0)).normalized() for p in pts]
        ribbon(bm, pts, nrms, 0.03, 0.016)

    make("Barra", chest, "dourado", hem, subsurf=1)
    collar_rings = [(0.25, 0.105, 0.088), (0.31, 0.118, 0.100), (0.36, 0.132, 0.112)]
    make("Gola", chest, "jaqueta", lambda bm: loft(bm, collar_rings, n=20, gap=[36, 44, 52]), solidify=0.012)

    def collar_edge(bm):
        ring = collar_rings[-1]
        pts = [ring_xy(ring, k, 20, 52) for k in range(21)]
        nrms = [Vector((p.x / ring[1] ** 2, p.y / ring[2] ** 2, 0)).normalized() for p in pts]
        ribbon(bm, pts, nrms, 0.022, 0.014)

    make("Gola Borda", chest, "dourado", collar_edge, subsurf=1)

    # Alça da bolsa: do ombro esquerdo (+X) ao quadril direito (-X), pela frente e pelas costas, por cima da jaqueta.
    def strap(bm, back):
        pts, nrms = [], []
        steps = 9
        for i in range(steps + 1):
            t = i / steps
            x = 0.15 - 0.33 * t
            z = 0.24 - 0.40 * t
            p, nrm = body_front(jacket_rings, x, z, back=back, lift=0.012 if z > -0.14 else 0.004)
            pts.append(p)
            nrms.append(nrm)
        ribbon(bm, pts, nrms, 0.045, 0.012)

    make("Alça", chest, "couro", lambda bm: strap(bm, False), subsurf=1)
    make("Alça Costas", chest, "couro", lambda bm: strap(bm, True), subsurf=1)
    make("Alça Ombro", chest, "couro", lambda bm: ribbon(
        bm, [(0.13, -0.11, 0.245), (0.15, -0.05, 0.27), (0.155, 0.0, 0.275), (0.15, 0.05, 0.27), (0.13, 0.11, 0.245)],
        [(0.3, -0.5, 0.8), (0.3, -0.2, 0.9), (0.3, 0, 0.95), (0.3, 0.2, 0.9), (0.3, 0.5, 0.8)], 0.045, 0.012), subsurf=1)

    bag = J["Bolsa"] = joint("Bolsa", torso, (-0.19, -0.02, -0.09))
    make("Bolsa", bag, "bolsa", lambda bm: box(bm, (0.075, 0.17, 0.15), rotate=(0, 0, 10)), subsurf=0, bevel=0.02)
    make("Bolsa Tampa", bag, "couro", lambda bm: box(bm, (0.085, 0.18, 0.07), at=(0, 0, 0.055), rotate=(0, 0, 10)), subsurf=0, bevel=0.015)
    make("Bolsa Fecho", bag, "latao", lambda bm: box(bm, (0.02, 0.035, 0.03), at=(-0.045, -0.02, 0.01), rotate=(0, 0, 10)), subsurf=0, bevel=0.004)

    # ---- pescoço e cabeça
    neck = J["Pescoço"] = joint("Pescoço", chest, (0, 0, 0.27))
    make("Pescoço Pele", neck, "pele", lambda bm: loft(bm, [(-0.03, 0.052, 0.048), (0.05, 0.05, 0.046), (0.10, 0.058, 0.052)]))
    head = J["Cabeça"] = joint("Cabeça", neck, (0, 0, 0.05))
    hc = Vector((0, 0.01, 0.128))
    skull_r = (0.124, 0.13, 0.134)
    skull = make("Crânio", head, "pele", lambda bm: sphere(bm, skull_r, at=hc, seg=24, rings=16))
    jaw = make("Mandíbula", head, "pele", lambda bm: loft(
        bm, [(0.012, 0.045, 0.042, 0, -0.025), (0.05, 0.092, 0.088, 0, -0.012), (0.10, 0.113, 0.108, 0, 0.0), (0.14, 0.118, 0.118, 0, 0.008)], n=20))
    nose = make("Nariz", head, "pele", lambda bm: loft(bm, [(0, 0.013, 0.011), (0.022, 0.004, 0.004)], n=10, at=(0, -0.112, 0.098), rotate=(90, 0, 0)), subsurf=1, outline=False)
    # Esfera invisível cujas normais o rosto copia: sombras do rosto ficam lisas como as de uma esfera.
    normal_sphere = bpy.data.objects.new("Rosto Normais", bpy.data.meshes.new("Rosto Normais"))
    bm = bmesh.new()
    sphere(bm, (0.13, 0.14, 0.15), at=hc + Vector((0, 0, -0.01)), seg=32, rings=24)
    bm.to_mesh(normal_sphere.data)
    bm.free()
    for p in normal_sphere.data.polygons:
        p.use_smooth = True
    normal_sphere.parent = head
    normal_sphere.display_type = "WIRE"
    normal_sphere.hide_render = True
    COLL.objects.link(normal_sphere)
    for ob in () if LOW else (skull, jaw, nose):
        ob.data.use_auto_smooth = True
        ob.data.auto_smooth_angle = math.pi
        mod = ob.modifiers.new("Normais do Rosto", "DATA_TRANSFER")
        mod.object = normal_sphere
        mod.use_loop_data = True
        mod.data_types_loops = {"CUSTOM_NORMAL"}
        mod.loop_mapping = "POLYINTERP_NEAREST"
        # Antes do contorno: o casco preto não precisa das normais emprestadas.
        idx = [m.name for m in ob.modifiers].index("Contorno") if "Contorno" in ob.modifiers else len(ob.modifiers) - 1
        bpy.context.view_layer.objects.active = ob
        if idx < len(ob.modifiers) - 1:
            bpy.ops.object.modifier_move_to_index(modifier=mod.name, index=idx)

    def on_skull(az, el, r_scale=1.0):
        az, el = math.radians(az), math.radians(el)
        u = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))
        return hc + Vector((u.x * skull_r[0], u.y * skull_r[1], u.z * skull_r[2])) * r_scale, u

    # Olhos de anime: esclera alta, íris âmbar, pupila, brilho, pálpebra superior grossa e sobrancelha.
    for side, label in ((-1, "D"), (1, "E")):
        p, u = on_skull(22 * side, -8)
        tilt = -8 * side
        make(f"Olho {label} Branco", head, "branco", lambda bm, p=p, u=u, t=tilt: disc(bm, 0.028, 0.040, p + u * 0.003, u, roll=t), subsurf=1, outline=False)
        make(f"Olho {label} Íris", head, "iris", lambda bm, p=p, u=u, t=tilt: disc(bm, 0.019, 0.031, p + u * 0.005, u, roll=t), subsurf=1, outline=False)
        make(f"Olho {label} Pupila", head, "pupila", lambda bm, p=p, u=u, t=tilt: disc(bm, 0.011, 0.015, p + u * 0.007, u, roll=t), subsurf=1, outline=False)
        make(f"Olho {label} Brilho", head, "branco", lambda bm, p=p, u=u, s=side: disc(bm, 0.006, 0.008, p + u * 0.009 + Vector((-0.008 * s, 0, 0.012)), u), subsurf=1, outline=False)
        pl, ul = on_skull(22 * side, -1)
        make(f"Pálpebra {label}", head, "escuro", lambda bm, p=pl, u=ul, t=tilt: disc(bm, 0.034, 0.0075, p + u * 0.009, u, roll=t), subsurf=1, outline=False)
        pb, ub = on_skull(23 * side, 10)
        # Olhar determinado: a ponta interna da sobrancelha fica mais baixa que a externa.
        make(f"Sobrancelha {label}", head, "cabelo", lambda bm, p=pb, u=ub, s=side: disc(bm, 0.036, 0.007, p + u * 0.006, u, roll=14 * s), subsurf=1, outline=False)
        pe, ue = on_skull(92 * side, -2)
        make(f"Orelha {label}", head, "pele", lambda bm, p=pe, s=side: sphere(bm, (0.02, 0.034, 0.042), at=p + Vector((-0.006 * s, 0, 0)), rotate=(0, 0, 0)))
    pm, um = on_skull(0, -22)
    make("Boca", head, "escuro", lambda bm: disc(bm, 0.018, 0.0045, pm + um * 0.004 + Vector((0.004, 0, 0)), um, roll=6), subsurf=1, outline=False)

    # Cabelo: touca de volume (recuada, para não cobrir o rosto) e mechas largas em cunha que acompanham o
    # crânio: coroa em pé, laterais varridas para trás, franja fragmentada caindo até as sobrancelhas, nuca.
    hair_c = Vector((0, 0.045, 0.195))
    hair_r = (0.13, 0.128, 0.105)
    make("Cabelo", head, "cabelo", lambda bm: sphere(bm, hair_r, at=hair_c, seg=24, rings=16))

    def tuft(bm, az, el, w, length, flow, bend):
        az_r, el_r = math.radians(az), math.radians(el)
        u = Vector((math.sin(az_r) * math.cos(el_r), -math.cos(az_r) * math.cos(el_r), math.sin(el_r)))
        base = hair_c + Vector((u.x * hair_r[0], u.y * hair_r[1], u.z * hair_r[2])) * 0.78
        d = (u * 0.45 + Vector(flow)).normalized()
        loft(bm, [(0, w, w * 0.4), (length * 0.5, w * 0.8, w * 0.32, bend * 0.4, 0), (length, 0.004, 0.003, bend, bend * 0.3)],
             n=6 if LOW else 10, at=base, rotate=aim(d), caps=(True, True))

    tufts = []
    # Coroa: mechas largas e baixas, inclinadas para a frente e para os lados (volume bagunçado, não espinhos).
    for i in range(7):
        az = 12 + 360 / 7 * i
        side = 1 if (az % 360) < 180 else -1
        tufts.append((az, 56, 0.07, 0.10 + 0.015 * (i % 2), (0.3 * side, -0.15, 0.4), rnd.uniform(-0.05, 0.05)))
    # Meio: laterais varridas para trás e para fora, nuca mais longa.
    for i in range(9):
        az = 20 + 40 * i
        back = abs(((az + 180) % 360) - 180) > 100
        flow = (0, 0.5, 0.2) if back else (0.5 * (1 if az % 360 < 180 else -1), 0.35, 0.1)
        tufts.append((az, 24, 0.06, 0.10 + (0.03 if back else 0), flow, rnd.uniform(-0.05, 0.05)))
    # Franja fragmentada em duas fileiras, caindo até as sobrancelhas.
    for az, b in ((-44, 0.03), (-26, -0.02), (-9, 0.025), (9, -0.03), (26, 0.02), (44, -0.025)):
        tufts.append((az, 22, 0.05, 0.095, (0, -0.3, -0.6), b))
    for az, b in ((-34, -0.02), (-17, 0.02), (0, -0.015), (17, 0.02), (34, -0.02)):
        tufts.append((az, 36, 0.055, 0.11, (0, -0.45, -0.45), b))
    for az in (150, 180, 210):
        tufts.append((az, -6, 0.045, 0.09, (0, 0.3, -0.8), rnd.uniform(-0.02, 0.02)))
    for i, spec in enumerate(tufts):
        make(f"Mecha {i}", head, "cabelo", lambda bm, s=spec: tuft(bm, *s), subsurf=1)

    # ---- braços
    for side, label in ((-1, "D"), (1, "E")):
        shoulder = J[f"Braço {label}"] = joint(f"Braço {label}", chest, (0.215 * side, 0, 0.23))
        make(f"Ombro {label}", shoulder, "jaqueta", lambda bm: sphere(bm, (0.088, 0.084, 0.084), at=(0, 0, 0.005)))
        make(f"Manga Camisa {label}", shoulder, "camisa", lambda bm: loft(bm, [(0.02, 0.068, 0.068), (-0.14, 0.064, 0.064), (-0.285, 0.056, 0.056)]))
        make(f"Manga Jaqueta {label}", shoulder, "jaqueta", lambda bm: loft(bm, [(0.03, 0.08, 0.08), (-0.08, 0.079, 0.079), (-0.15, 0.076, 0.076)], caps=(True, False)), solidify=0.012)
        make(f"Punho {label}", shoulder, "dourado", lambda bm: loft(bm, [(-0.165, 0.082, 0.082), (-0.14, 0.082, 0.082)]), subsurf=1)
        elbow = J[f"Cotovelo {label}"] = joint(f"Cotovelo {label}", shoulder, (0, 0, -0.285))
        make(f"Cotovelo {label}", elbow, "camisa", lambda bm: sphere(bm, 0.058))
        make(f"Antebraço {label}", elbow, "pele", lambda bm: loft(bm, [(0.0, 0.056, 0.054), (-0.14, 0.052, 0.05), (-0.27, 0.044, 0.042)]))
        wraps = []
        for i in range(7):
            z = -0.035 - 0.028 * i
            wraps.append((z, 0.062 - 0.0025 * i + (0.004 if i % 2 else 0), 0.060 - 0.0025 * i + (0.004 if i % 2 else 0)))
        wraps.append((-0.24, 0.047, 0.046))
        make(f"Faixa {label}", elbow, "faixa", lambda bm, w=wraps: loft(bm, w))
        wrist = J[f"Pulso {label}"] = joint(f"Pulso {label}", elbow, (0, 0, -0.27))
        make(f"Luva {label}", wrist, "luva", lambda bm: loft(bm, [(0.01, 0.046, 0.044), (-0.03, 0.05, 0.04), (-0.075, 0.047, 0.036)]))
        make(f"Punho Fechado {label}", wrist, "luva", lambda bm: sphere(bm, (0.048, 0.04, 0.05), at=(0, -0.005, -0.09)))
        for i in range(4):
            x = (-0.03 + 0.02 * i)
            make(f"Dedo {label} {i}", wrist, "pele", lambda bm, x=x: sphere(bm, (0.011, 0.016, 0.012), at=(x, -0.038, -0.1 - 0.004 * abs(i - 1.5))), subsurf=1, outline=False)
        make(f"Polegar {label}", wrist, "pele", lambda bm: sphere(bm, (0.012, 0.018, 0.012), at=(0.04 * side, -0.02, -0.075)), subsurf=1, outline=False)

    # ---- pernas
    for side, label in ((-1, "D"), (1, "E")):
        leg = J[f"Perna {label}"] = joint(f"Perna {label}", hips, (0.095 * side, 0, -0.03))
        make(f"Coxa {label}", leg, "calca", lambda bm: loft(bm, [(0.05, 0.106, 0.108), (-0.15, 0.096, 0.098), (-0.30, 0.082, 0.084), (-0.43, 0.074, 0.074)]))
        make(f"Listra Coxa {label}", leg, "listra", lambda bm: loft(bm, [(-0.012, 0.094, 0.096), (0.012, 0.094, 0.096)], at=(0, 0, -0.2), rotate=(14, 0, 0)), subsurf=1)
        knee = J[f"Joelho {label}"] = joint(f"Joelho {label}", leg, (0, 0, -0.42))
        make(f"Joelho {label}", knee, "calca", lambda bm: sphere(bm, 0.072))
        make(f"Joelheira {label}", knee, "aco", lambda bm: sphere(bm, (0.06, 0.032, 0.066), at=(0, -0.058, 0.0)))
        make(f"Joelheira Tira {label}", knee, "listra", lambda bm: loft(bm, [(-0.012, 0.076, 0.077), (0.012, 0.076, 0.077)], at=(0, 0, -0.07)), subsurf=1)
        make(f"Canela {label}", knee, "calca", lambda bm: loft(bm, [(0.0, 0.07, 0.07), (-0.16, 0.064, 0.064), (-0.30, 0.058, 0.058), (-0.36, 0.056, 0.056)]))
        make(f"Bota Cano {label}", knee, "bota", lambda bm: loft(bm, [(-0.19, 0.08, 0.08), (-0.30, 0.078, 0.078), (-0.40, 0.08, 0.082)]))
        make(f"Bota Dobra {label}", knee, "cano", lambda bm: loft(bm, [(-0.20, 0.094, 0.094), (-0.13, 0.093, 0.093), (-0.115, 0.084, 0.084)]), subsurf=1)
        make(f"Bota Tira {label}", knee, "couro", lambda bm: loft(bm, [(-0.33, 0.084, 0.084), (-0.31, 0.084, 0.084)]), subsurf=1)
        make(f"Bota Fivela {label}", knee, "latao", lambda bm: box(bm, (0.02, 0.012, 0.028), at=(0.082 * side, -0.01, -0.32)), subsurf=0, bevel=0.003)
        ankle = J[f"Tornozelo {label}"] = joint(f"Tornozelo {label}", knee, (0, 0, -0.40))
        foot_rings = [(-0.09, 0.058, 0.05, 0, 0.06), (0.0, 0.076, 0.065, 0, 0.066), (0.11, 0.072, 0.055, 0, 0.055), (0.21, 0.062, 0.04, 0, 0.042), (0.26, 0.048, 0.026, 0, 0.032)]
        make(f"Bota Pé {label}", ankle, "bota", lambda bm, r=foot_rings: loft(bm, r, at=(0, 0, -0.08), rotate=(90, 0, 0)))
        make(f"Sola {label}", ankle, "sola", lambda bm: loft(
            bm, [(-0.1, 0.064, 0.012, 0, 0.012), (0.0, 0.082, 0.012, 0, 0.012), (0.11, 0.078, 0.012, 0, 0.012), (0.22, 0.066, 0.012, 0, 0.012), (0.27, 0.05, 0.012, 0, 0.012)],
            at=(0, 0, -0.08), rotate=(90, 0, 0)), subsurf=1)
        for i in range(3):
            make(f"Cadarço {label} {i}", ankle, "couro", lambda bm, i=i: loft(bm, [(-0.03, 0.004, 0.004), (0.03, 0.004, 0.004)], n=6, at=(0, -0.045 - 0.028 * i, -0.0 - 0.012 * i), rotate=(0, 90, 20 * (1 if i % 2 else -1))), subsurf=1, outline=False)

    # ---- espada (mão direita) e escudo (antebraço esquerdo)
    hand_r = J["Pulso D"]
    # Nomes com prefixo "Arma"/"Escudo": é assim que CharacterRig.Classify reconhece o equipamento no jogo.
    sword = J["Espada"] = joint("Espada", hand_r, (0, -0.01, -0.09))
    make("Arma Lâmina", sword, "lamina", lambda bm: loft(bm, [(-0.11, 0.03, 0.007), (-0.55, 0.026, 0.006), (-0.74, 0.013, 0.004), (-0.80, 0.001, 0.001)], n=4), subsurf=0, bevel=0.0015)
    make("Arma Guarda", sword, "latao", lambda bm: box(bm, (0.17, 0.02, 0.028), at=(0, 0, -0.098)), subsurf=0, bevel=0.006)
    make("Arma Cabo", sword, "cabo", lambda bm: loft(bm, [(-0.085, 0.016, 0.016), (0.07, 0.015, 0.015)], n=12))
    make("Arma Pomo", sword, "latao", lambda bm: sphere(bm, 0.024, at=(0, 0, 0.085)))
    shield = J["Escudo"] = joint("Escudo", J["Cotovelo E"], (0, 0, 0))
    make("Escudo Tábuas", shield, "madeira", lambda bm: loft(bm, [(-0.006, 0.27, 0.27), (0.014, 0.268, 0.268), (0.03, 0.2, 0.2), (0.042, 0.08, 0.08)], n=32), subsurf=1)
    # Aro sem tampas: só a faixa de aço na borda, senão ele cobre a madeira.
    make("Escudo Aro", shield, "aco", lambda bm: loft(bm, [(-0.012, 0.262, 0.262), (-0.012, 0.272, 0.272), (0.0, 0.282, 0.282), (0.022, 0.282, 0.282), (0.034, 0.266, 0.266), (0.034, 0.25, 0.25)], n=32, caps=(False, False)), subsurf=1)
    make("Escudo Umbo", shield, "aco", lambda bm: sphere(bm, (0.075, 0.075, 0.05), at=(0, 0, 0.035)))
    for i in range(8):
        a = math.radians(45 * i + 22.5)
        make(f"Escudo Rebite {i}", shield, "aco", lambda bm, a=a: sphere(bm, 0.013, at=(0.245 * math.cos(a), 0.245 * math.sin(a), 0.03)), subsurf=1, outline=False)
    for i, x in enumerate((-0.15, -0.05, 0.05, 0.15)):
        make(f"Escudo Tábua {i}", shield, "madeira_escura", lambda bm, x=x: box(bm, (0.007, 2 * math.sqrt(max(0.01, 0.25 ** 2 - x ** 2)) - 0.01, 0.006), at=(x, 0, 0.03)), subsurf=0, outline=False)
    make("Escudo Braçadeira", shield, "couro", lambda bm: box(bm, (0.05, 0.22, 0.03), at=(0.06, 0, -0.02)), subsurf=0, bevel=0.006)
    make("Escudo Pegada", shield, "couro", lambda bm: box(bm, (0.05, 0.14, 0.03), at=(-0.1, 0, -0.02)), subsurf=0, bevel=0.006)

    if POSE:
        pose(J)
    else:
        rest_gear(J)
    return J


def rest_gear(J):
    """Pose de descanso (braços caídos): espada 30° à frente do antebraço, escudo no lado de fora do
    antebraço esquerdo com a correia ao longo dele. É o que o rig do jogo espera (ele só gira os ossos)."""
    J["Espada"].rotation_euler = (math.radians(-30), 0, 0)
    J["Escudo"].location = (0.1, 0, -0.2)
    J["Escudo"].rotation_euler = (Matrix.Rotation(math.radians(90), 3, "Y") @ Matrix.Rotation(math.radians(90), 3, "Z")).to_euler()
    bpy.context.view_layer.update()


def set_world_rotation(ob, rot3):
    parent_rot = ob.parent.matrix_world.to_3x3() if ob.parent else Matrix.Identity(3)
    ob.rotation_euler = (parent_rot.inverted() @ rot3).to_euler("XYZ")


def pose(J):
    """Pose de combate da referência: pernas abertas, tronco virado para a direita, espada estendida para o
    lado direito e para baixo, escudo erguido na frente do peito, olhar para a direita."""
    deg = math.radians
    J["Quadril"].rotation_euler = (deg(4), 0, deg(-12))
    J["Tronco"].rotation_euler = (deg(3), 0, deg(-4))
    J["Tórax"].rotation_euler = (deg(2), 0, deg(-8))
    J["Pescoço"].rotation_euler = (0, 0, deg(-10))
    J["Cabeça"].rotation_euler = (deg(-3), 0, deg(-26))
    J["Bolsa"].rotation_euler = (0, deg(-8), 0)
    # Braço direito (-X): estendido para fora, um pouco para trás e para baixo; a espada continua a linha.
    J["Braço D"].rotation_euler = (deg(12), deg(46), 0)
    J["Cotovelo D"].rotation_euler = (deg(-6), deg(6), 0)
    J["Pulso D"].rotation_euler = (0, deg(-4), deg(10))
    # Braço esquerdo (+X): cotovelo para fora e à frente, antebraço subindo em diagonal; o escudo fica ao lado
    # do peito, não na frente dele.
    J["Braço E"].rotation_euler = (deg(-30), deg(-40), deg(-10))
    J["Cotovelo E"].rotation_euler = (deg(-110), 0, 0)
    J["Pulso E"].rotation_euler = (deg(-20), 0, 0)
    # Pernas: afastadas, a esquerda à frente, joelhos flexionados; pés chatos no chão.
    J["Perna D"].rotation_euler = (deg(-6), deg(34), 0)
    J["Joelho D"].rotation_euler = (deg(22), 0, 0)
    J["Perna E"].rotation_euler = (deg(-18), deg(-34), 0)
    J["Joelho E"].rotation_euler = (deg(28), 0, 0)
    bpy.context.view_layer.update()
    for label, toe_out in (("D", -25), ("E", 10)):
        set_world_rotation(J[f"Tornozelo {label}"], Euler((0, 0, deg(toe_out))).to_matrix())
    bpy.context.view_layer.update()
    lowest = min(J[f"Tornozelo {label}"].matrix_world.translation.z for label in "DE") - 0.08
    J["Quadril"].location.z -= lowest
    bpy.context.view_layer.update()
    # Escudo preso ao antebraço esquerdo, face virada para a frente (para a ameaça), um pouco para a direita.
    fw = J["Cotovelo E"].matrix_world
    axis = (fw.to_3x3() @ Vector((0, 0, -1))).normalized()
    normal = Vector((-0.3, -0.95, 0.05))
    normal = (normal - axis * normal.dot(axis)).normalized()  # o disco contém o eixo do antebraço: a mão fica atrás
    center = fw @ Vector((0, 0, -0.2)) + normal * 0.105
    J["Escudo"].matrix_world = Matrix.Translation(center) @ normal.to_track_quat("Z", "Y").to_matrix().to_4x4()
    bpy.context.view_layer.update()


# ---------------------------------------------------------------- cena, render, relatório

def setup_scene():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.eevee.use_soft_shadows = False
    scene.eevee.shadow_cube_size = "2048"
    scene.eevee.shadow_cascade_size = "4096"
    scene.view_settings.view_transform = "Standard"
    scene.render.film_transparent = True
    scene.render.resolution_x, scene.render.resolution_y = VIEW_SIZE
    scene.render.resolution_percentage = 200  # renderiza em 2x; a prancha reduz (supersampling do contorno)
    scene.render.image_settings.color_mode = "RGBA"
    world = bpy.data.worlds.new("Mundo") if scene.world is None else scene.world
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.35, 0.37, 0.42, 1)
        bg.inputs["Strength"].default_value = 0.6
    sun_data = bpy.data.lights.new("Sol", "SUN")
    sun_data.energy = 4.0
    sun_data.angle = 0.0
    sun_data.use_contact_shadow = True
    sun = bpy.data.objects.new("Sol", sun_data)
    # A luz viaja na direção dada: vem da frente (-Y), de cima e do lado direito do personagem (-X).
    sun.rotation_euler = Vector((0, 0, -1)).rotation_difference(Vector((0.35, 0.8, -0.5))).to_euler()
    COLL.objects.link(sun)
    cam_data = bpy.data.cameras.new("Câmera")
    cam_data.lens = 55
    cam = bpy.data.objects.new("Câmera", cam_data)
    COLL.objects.link(cam)
    scene.camera = cam
    return cam


def place_camera(cam, yaw_deg, distance=4.3, height=0.92):
    yaw = math.radians(yaw_deg)
    target = Vector((0, 0, height))
    cam.location = target + Vector((distance * math.sin(yaw), -distance * math.cos(yaw), 0.18))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()


def poly_report():
    dg = bpy.context.evaluated_depsgraph_get()
    base_tris = eval_tris = outline_tris = 0
    for ob in COLL.objects:
        if ob.type != "MESH" or ob.hide_render:
            continue
        me = ob.data
        me.calc_loop_triangles()
        base_tris += len(me.loop_triangles)
        ev = ob.evaluated_get(dg).data
        ev.calc_loop_triangles()
        tris = len(ev.loop_triangles)
        eval_tris += tris
        if "Contorno" in ob.modifiers:
            outline_tris += tris // 2
    return base_tris, eval_tris, outline_tris


def main_blender():
    if BATCH:
        bpy.ops.wm.read_homefile(use_empty=True)
    elif "Herói HD" in bpy.data.collections:
        old = bpy.data.collections["Herói HD"]
        for ob in list(old.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.collections.remove(old)
    build_hero()
    cam = setup_scene()
    place_camera(cam, VIEWS[0][1])
    base, evaluated, outline = poly_report()
    lines = [
        "Herói HD (NPR) — contagem de triângulos",
        f"  malha base (antes dos modificadores): {base:,}",
        f"  avaliada (subdivisão + espessura + contorno): {evaluated:,}",
        f"  dos quais o contorno (inverted hull): {outline:,}  ->  sem contorno: {evaluated - outline:,}",
        f"  objetos de malha: {sum(1 for o in COLL.objects if o.type == 'MESH')}",
    ]
    print("\n".join(lines))
    if BATCH:
        os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
        print("salvo:", BLEND_PATH)
        os.makedirs(RENDER_DIR, exist_ok=True)
        with open(os.path.join(RENDER_DIR, "heroi_hd_relatorio.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
    if DO_RENDER:
        for name, yaw in VIEWS:
            place_camera(cam, yaw)
            bpy.context.scene.render.filepath = os.path.join(RENDER_DIR, f"heroi_hd_{name}.png")
            bpy.ops.render.render(write_still=True)
            print("render:", bpy.context.scene.render.filepath)


def main_compose():
    """Prancha: referência (2x2) à esquerda, as quatro vistas renderizadas (2x2) à direita, sobre branco."""
    from PIL import Image
    ref = Image.open(REF_PATH).convert("RGB")
    w, h = VIEW_SIZE
    renders = [Image.open(os.path.join(RENDER_DIR, f"heroi_hd_{name}.png")).convert("RGBA") for name, _ in VIEWS]
    sheet = Image.new("RGB", (ref.width + 24 + 2 * w, max(ref.height, 2 * h)), "white")
    sheet.paste(ref, (0, 0))
    for i, img in enumerate(renders):
        tile = Image.new("RGB", img.size, "white")
        tile.paste(img, (0, 0), img)
        sheet.paste(tile.resize((w, h), Image.LANCZOS), (ref.width + 24 + (i % 2) * w, (i // 2) * h))
    out = os.path.join(RENDER_DIR, "heroi_hd_comparacao.png")
    sheet.save(out)
    print("prancha:", out)


if __name__ == "__main__":
    if IN_BLENDER and "--compose" not in ARGS:
        main_blender()
    elif not IN_BLENDER:
        main_compose()
