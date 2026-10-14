# Rescue Ops modding hub - Emergency lights tutorial
# Adds a soft "glow shell" to a flat lighthead mesh and exports it as FBX for Unreal.
#
# What it does:
#   1. Imports the game's lighthead (.glb exported from FModel).
#   2. Keeps one UV map and renames the lens material to the game's slot name.
#   3. Adds a flattened sphere in front of the lens as a second material slot, "Glow".
#   4. Writes a fade value into the glow's UV map (U): 1 over the lens, fading to 0
#      at MARGIN past the lens edges. The glow material reads it (TexCoord R).
#   5. Exports an FBX next to nothing else, ready to import.
#
# The lens must face +X (true for the CCF's S_CCF_BeaconLightx2_01 / x4_01).
#
# Run from a command prompt (one line):
#   "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" -b --factory-startup
#     --python beacon_glow_shell.py -- <input.glb> <output.fbx> <MaterialSlotName>
# Example:
#   ... -- S_CCF_BeaconLightx4_01.glb S_CCF_BeaconLightx4_01.fbx M_LPs_Precis26

import bpy, sys, math

MARGIN = 0.06   # metres: how far the glow reaches past the lens edges (6 cm)
DEPTH = 0.03    # metres: how far the glow bulges out in front of the lens (3 cm)

args = sys.argv[sys.argv.index('--') + 1:]
src, out, slot = args[0], args[1], args[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for x in list(bpy.context.scene.objects):
    if x.type != 'MESH':
        bpy.data.objects.remove(x)
meshes = [x for x in bpy.context.scene.objects if x.type == 'MESH']
if len(meshes) != 1:
    sys.exit("Expected exactly one mesh in the .glb")
lens = meshes[0]
while len(lens.data.uv_layers) > 1:
    lens.data.uv_layers.remove(lens.data.uv_layers[-1])
lens.material_slots[0].material.name = slot

vs = [v.co for v in lens.data.vertices]
half_w = max(abs(v.y) for v in vs)
half_h = max(abs(v.z) for v in vs)
front = max(v.x for v in vs)

bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1, location=(front, 0, 0))
glow = bpy.context.active_object
glow.scale = (DEPTH, half_w + MARGIN, half_h + MARGIN)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bpy.ops.object.shade_smooth()
glow.data.uv_layers[0].name = lens.data.uv_layers[0].name
uv = glow.data.uv_layers[0].data
for poly in glow.data.polygons:
    for li in poly.loop_indices:
        c = glow.data.vertices[glow.data.loops[li].vertex_index].co
        d = math.hypot(max(abs(c.y) - half_w, 0), max(abs(c.z) - half_h, 0))
        uv[li].uv = (max(0.0, min(1.0, 1 - d / MARGIN)), 0.0)
glow.data.materials.append(bpy.data.materials.new("Glow"))

bpy.ops.object.select_all(action='DESELECT')
glow.select_set(True)
lens.select_set(True)
bpy.context.view_layer.objects.active = lens
bpy.ops.object.join()
lens.name = lens.data.name = bpy.path.display_name_from_filepath(out)

bpy.ops.export_scene.fbx(filepath=out, object_types={'MESH'}, mesh_smooth_type='FACE',
                         add_leaf_bones=False, bake_anim=False)
print("Wrote", out, "with slots", [m.name for m in lens.data.materials])
