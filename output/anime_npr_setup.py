"""Base NPR para EEVEE. Execute no Text Editor do Blender, em Object Mode.

Aplica um material toon uniforme a todas as faces das malhas selecionadas.
Os materiais anteriores continuam nos slots. Reexecutar redefine os materiais
ANIME_Toon/ANIME_Outline e atualiza o modificador ANIME_Outline.
"""

import bpy


# Cores RGBA em espaco linear; ajuste tambem pelo Color Ramp no Shader Editor.
LIT_COLOR = (0.80, 0.52, 0.36, 1.0)
SHADOW_COLOR = (0.32, 0.14, 0.16, 1.0)
SHADOW_THRESHOLD = 0.45
OUTLINE_THICKNESS = 0.003


def enable_eevee(scene):
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = engine
            return
        except TypeError:
            continue
    raise RuntimeError("Esta versao do Blender nao disponibiliza EEVEE.")


def material_tree(name):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    material.node_tree.nodes.clear()
    return material, material.node_tree


def node(tree, node_type, location):
    result = tree.nodes.new(node_type)
    result.location = location
    return result


def create_toon():
    material, tree = material_tree("ANIME_Toon")
    material.diffuse_color = LIT_COLOR

    diffuse = node(tree, "ShaderNodeBsdfDiffuse", (-600, 0))
    diffuse.inputs["Color"].default_value = (1, 1, 1, 1)
    diffuse.inputs["Roughness"].default_value = 0.0
    rgb = node(tree, "ShaderNodeShaderToRGB", (-380, 0))
    ramp = node(tree, "ShaderNodeValToRGB", (-160, 0))
    ramp.label = "Limiar entre sombra e luz"
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = SHADOW_COLOR
    ramp.color_ramp.elements[1].position = SHADOW_THRESHOLD
    ramp.color_ramp.elements[1].color = LIT_COLOR
    emission = node(tree, "ShaderNodeEmission", (160, 0))
    emission.inputs["Strength"].default_value = 1.0
    output = node(tree, "ShaderNodeOutputMaterial", (380, 0))

    tree.links.new(diffuse.outputs["BSDF"], rgb.inputs["Shader"])
    tree.links.new(rgb.outputs["Color"], ramp.inputs["Fac"])
    tree.links.new(ramp.outputs["Color"], emission.inputs["Color"])
    tree.links.new(emission.outputs[0], output.inputs["Surface"])
    return material


def create_outline():
    material, tree = material_tree("ANIME_Outline")
    material.diffuse_color = (0, 0, 0, 1)
    material.use_backface_culling = True

    black = node(tree, "ShaderNodeEmission", (-220, 80))
    black.inputs["Color"].default_value = (0, 0, 0, 1)
    black.inputs["Strength"].default_value = 1.0
    transparent = node(tree, "ShaderNodeBsdfTransparent", (-220, -80))
    transparent.inputs["Color"].default_value = (1, 1, 1, 1)
    light_path = node(tree, "ShaderNodeLightPath", (-460, 240))
    mix = node(tree, "ShaderNodeMixShader", (40, 80))
    output = node(tree, "ShaderNodeOutputMaterial", (260, 80))

    # A casca fica preta na imagem e transparente para raios de sombra.
    tree.links.new(light_path.outputs["Is Shadow Ray"], mix.inputs[0])
    tree.links.new(black.outputs[0], mix.inputs[1])
    tree.links.new(transparent.outputs[0], mix.inputs[2])
    tree.links.new(mix.outputs[0], output.inputs["Surface"])

    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"
    if hasattr(material, "shadow_method"):
        material.shadow_method = "NONE"  # EEVEE antigo.
    return material


def material_slot(obj, material):
    for index, slot in enumerate(obj.material_slots):
        if slot.material == material:
            return index
    obj.data.materials.append(material)
    return len(obj.material_slots) - 1


def setup_mesh(obj, toon, outline):
    if obj.data.users > 1:
        obj.data = obj.data.copy()

    toon_index = material_slot(obj, toon)
    outline_index = material_slot(obj, outline)
    for polygon in obj.data.polygons:
        polygon.material_index = toon_index
        polygon.use_smooth = True
    obj.active_material_index = toon_index

    modifier = obj.modifiers.get("ANIME_Outline")
    if modifier is None:
        modifier = obj.modifiers.new("ANIME_Outline", "SOLIDIFY")
    elif modifier.type != "SOLIDIFY":
        raise RuntimeError(f"{obj.name}: ANIME_Outline ja existe com outro tipo.")

    modifier.solidify_mode = "EXTRUDE"
    modifier.thickness = OUTLINE_THICKNESS
    modifier.offset = 1.0
    modifier.use_flip_normals = True
    modifier.use_even_offset = True
    modifier.use_quality_normals = True
    modifier.use_rim = False
    modifier.use_rim_only = False
    modifier.material_offset = outline_index - toon_index
    modifier.material_offset_rim = outline_index - toon_index
    modifier.show_viewport = True
    modifier.show_render = True

    # Contorno depois de Armature, Subdivision e Data Transfer existentes.
    with bpy.context.temp_override(object=obj, active_object=obj):
        bpy.ops.object.modifier_move_to_index(
            modifier=modifier.name, index=len(obj.modifiers) - 1
        )
    obj.data.update()


def main():
    if bpy.context.mode != "OBJECT":
        raise RuntimeError("Mude para Object Mode antes de executar.")
    meshes = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("Selecione pelo menos uma malha do personagem.")

    enable_eevee(bpy.context.scene)
    toon = create_toon()
    outline = create_outline()
    for obj in meshes:
        setup_mesh(obj, toon, outline)
    print(f"NPR configurado em {len(meshes)} malha(s). Use a visualizacao Rendered.")


if __name__ == "__main__":
    main()
