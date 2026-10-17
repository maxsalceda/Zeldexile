# Lists the armature of each FBX: bone count, key bone lengths and which fingers it has, to tell whether an
# animation was made on the same skeleton as the hero.
# blender -b --factory-startup -P Tools/Blender/rig_report.py -- a.fbx b.fbx ...
import os
import sys

import bpy

KEY = ["Spine", "Spine1", "Spine2", "Neck", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftUpLeg", "LeftLeg", "LeftFoot"]

for path in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=path)
    except Exception as e:  # noqa: BLE001
        print("RIG", os.path.basename(path), "error", e)
        continue
    meshes = sum(1 for o in bpy.data.objects if o.type == "MESH")
    for arm in (o for o in bpy.data.objects if o.type == "ARMATURE"):
        bones = {b.name.split(":")[-1]: b for b in arm.data.bones}
        lengths = " ".join(f"{k}={bones[k].length * arm.scale[0]:.4f}" if k in bones else f"{k}=-" for k in KEY)
        fingers = sorted({n[len("LeftHand"):-1] for n in bones if n.startswith("LeftHand") and n[-1].isdigit()})
        action = arm.animation_data.action if arm.animation_data else None
        frames = tuple(action.frame_range) if action else None
        print("RIG", os.path.basename(path), f"bones={len(bones)} meshes={meshes} frames={frames}", lengths,
              "fingers=" + ",".join(fingers))

    # --rest: rest-pose direction of each key bone (armature space) and its rest rotation relative to its parent.
    if "--rest" in os.environ.get("ZX_RIG", ""):
        for arm in (o for o in bpy.data.objects if o.type == "ARMATURE"):
            for b in arm.data.bones:
                n = b.name.split(":")[-1]
                if "Hand" in n and n not in ("LeftHand", "RightHand"):
                    continue
                d = (b.tail_local - b.head_local).normalized()
                rel = (b.parent.matrix_local.inverted() @ b.matrix_local) if b.parent else b.matrix_local
                q = rel.to_quaternion()
                print("REST", os.path.basename(path), n, f"dir=({d.x:.3f},{d.y:.3f},{d.z:.3f})",
                      f"rel=({q.w:.4f},{q.x:.4f},{q.y:.4f},{q.z:.4f})")
