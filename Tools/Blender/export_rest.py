# Writes the rest pose (bind pose) of each FBX's armature next to it as <name>_repouso.json, for DirectClip.
# Unity imports an FBX without a mesh with its first frame as the default pose, so it cannot tell a clip's rest
# (T-pose) itself; Blender reads it from the file's bind pose.
#
# Each bone: its name without the "mixamorig:" prefix, its parent's, and its rest rotation and position relative to
# the parent, already in Unity's convention (Unity mirrors FBX's X axis: positions (-x, y, z), rotations (x, -y, -z, w)).
#
# blender -b --factory-startup -P Tools/Blender/export_rest.py -- a.fbx b.fbx ...
import json
import os
import sys

import bpy

for path in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not arms:
        print("REST", os.path.basename(path), "no armature")
        continue
    arm = arms[0]
    bones = []
    for b in arm.data.bones:
        rel = (b.parent.matrix_local.inverted() @ b.matrix_local) if b.parent else b.matrix_local
        q = rel.to_quaternion()
        t = rel.translation
        bones.append({
            "name": b.name.split(":")[-1],
            "parent": b.parent.name.split(":")[-1] if b.parent else "",
            "rot": [q.x, -q.y, -q.z, q.w],
            "pos": [-t.x, t.y, t.z],
        })
    out = os.path.splitext(path)[0] + "_repouso.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"bones": bones}, f, indent=1)
    print("REST", os.path.basename(path), len(bones), "bones ->", out)
