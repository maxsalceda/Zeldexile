"""
Renders a Mixamo/Meshy animation FBX *with its skin* as a reference sheet, to compare a clip as the game plays it
(Logs/preview-idle.png, from VisualPreviewTests.RenderIdleSheet) with the source animation.

The sheet matches the game's: 8 frames spread evenly over one loop, front row then side row (the hero facing
screen right), orthographic, 240x360 per frame, the hero 1.75 m tall in a 2.1 m tall view centred 0.95 m above
the soles.

  blender.exe -b -P Tools/Blender/render_clip_reference.py -- <clip_with_skin.fbx> <out.png> [frames]
"""

import math
import os
import sys

import bpy
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
SOURCE, OUT = ARGS[0], os.path.abspath(ARGS[1])
FRAMES = int(ARGS[2]) if len(ARGS) > 2 else 8
CELL_W, CELL_H = 240, 360
HERO_HEIGHT, VIEW_HEIGHT, VIEW_CENTER = 1.75, 2.1, 0.95

bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
scene = bpy.context.scene
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
action = arm.animation_data.action
first, last = action.frame_range
loop = last - first  # the last key repeats the first: a seamless loop


def head(name):
    return arm.matrix_world @ arm.pose.bones["mixamorig:" + name].head


# Facing as the game reads it (MotionClip): square to the hips, from the thigh line averaged over the loop.
# A fighting stance turns the feet and the chest away from the hips; the game's forward is the hips'.
hips_right = Vector()
for f in range(FRAMES):
    scene.frame_set(int(round(first + loop * f / FRAMES)))
    hips_right += head("RightUpLeg") - head("LeftUpLeg")
hips_right.z = 0
right = hips_right.normalized()
facing = Vector((0, 0, 1)).cross(right)
# Size from the first frame: soles to the top of the head.
scene.frame_set(int(first))
soles = min(head("LeftToe_End").z, head("RightToe_End").z, head("LeftFoot").z, head("RightFoot").z)
height = head("HeadTop_End").z - soles
k = height / HERO_HEIGHT
# Centred on the hips, as the game holds them over the hero's origin.
centre = Vector((head("Hips").x, head("Hips").y, soles + VIEW_CENTER * k))

cam = bpy.data.objects.new("Câmera", bpy.data.cameras.new("Câmera"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = VIEW_HEIGHT * k
cam.data.clip_start = 0.001 * k
cam.data.clip_end = 100 * k
scene.collection.objects.link(cam)
scene.camera = cam
sun = bpy.data.objects.new("Sol", bpy.data.lights.new("Sol", "SUN"))
sun.data.energy = 3
sun.rotation_euler = (math.radians(40), 0, math.radians(30))
scene.collection.objects.link(sun)
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.render.resolution_x, scene.render.resolution_y = CELL_W, CELL_H
scene.render.resolution_percentage = 100
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new("Fundo")
scene.world.color = (0.86, 0.86, 0.84)
scene.view_settings.view_transform = "Standard"

tmp = os.path.join(os.path.dirname(OUT), "_quadro.png")
cells = []
for row, view in enumerate((facing, right)):
    cam.location = centre + view * 6 * k
    cam.rotation_euler = (-view).to_track_quat("-Z", "Y").to_euler()
    for f in range(FRAMES):
        scene.frame_set(int(round(first + loop * f / FRAMES)))
        scene.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(tmp)
        cells.append((row, f, list(img.pixels)))
        bpy.data.images.remove(img)

sheet = bpy.data.images.new("Referência", FRAMES * CELL_W, 2 * CELL_H)
pixels = [0.0] * (FRAMES * CELL_W * 2 * CELL_H * 4)
for row, f, px in cells:
    y0 = (1 - row) * CELL_H  # Blender images start at the bottom: the front row goes on top
    for y in range(CELL_H):
        src = y * CELL_W * 4
        dst = ((y0 + y) * FRAMES * CELL_W + f * CELL_W) * 4
        pixels[dst:dst + CELL_W * 4] = px[src:src + CELL_W * 4]
sheet.pixels = pixels
sheet.filepath_raw = OUT
sheet.file_format = "PNG"
sheet.save()
os.remove(tmp)
print("referência:", OUT, f"({loop / scene.render.fps:.2f} s de loop)")
