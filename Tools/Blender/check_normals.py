"""Lists meshes whose faces point inward (negative signed volume). Run on a .blend:
blender.exe -b <file.blend> -P Tools/Blender/check_normals.py
"""
import bpy

bad = []
for obj in bpy.data.objects:
    if obj.type != 'MESH':
        continue
    me = obj.data
    signed = sum(p.center.dot(p.normal) * p.area for p in me.polygons)
    if signed <= 0:
        bad.append(f"{obj.name}: {signed:.5f}")
print("INWARD:", len(bad))
for line in bad:
    print("  ", line)
