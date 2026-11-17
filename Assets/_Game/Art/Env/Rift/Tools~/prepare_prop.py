# VARCO 소품(glb) → Unity용 정적 소품 FBX + 색 텍스처 (blender -b --factory-startup --python prepare_prop.py -- <glb> <out_dir> <Name> <height> [max_faces] [rot_x])
#   메시를 하나로 합치고 변환을 굳힌 뒤, 높이를 height(미터)에 맞추고 밑면을 0 · 가운데를 원점에 둬요. 면이 max_faces(기본 12000)보다 많으면 줄여요.
#   앞(모델이 보는 쪽)은 VARCO 그대로 -Y → Unity +Z. 텍스처는 <Name>_Albedo.png로 꺼내요
import bpy, sys, os

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_dir, NAME, HEIGHT = os.path.abspath(argv[0]), os.path.abspath(argv[1]), argv[2], float(argv[3])
MAX_FACES = int(argv[4]) if len(argv) > 4 else 12000
ROT_X = float(argv[5]) if len(argv) > 5 else 0.0   # 누워서 나온 모델을 세워요 (도, X축). VARCO glb는 보통 이미 서 있어서 0
os.makedirs(out_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(bpy.data.objects):
    if o != obj:
        bpy.data.objects.remove(o, do_unlink=True)

from mathutils import Vector, Matrix
if ROT_X:
    R = Matrix.Rotation(__import__("math").radians(ROT_X), 4, "X")
    for v in obj.data.vertices:
        v.co = R @ v.co
vs = [v.co for v in obj.data.vertices]
lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
k = HEIGHT / max(1e-6, hi.z - lo.z)
c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
for v in obj.data.vertices:
    v.co = (v.co - c) * k

faces = len(obj.data.polygons)
if faces > MAX_FACES:
    m = obj.modifiers.new("Decimate", 'DECIMATE')
    m.ratio = MAX_FACES / faces
    bpy.ops.object.modifier_apply(modifier=m.name)

img = next((n.image for mat in obj.data.materials if mat and mat.use_nodes for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image), None)
if img is not None:
    img.filepath_raw = os.path.join(out_dir, f"{NAME}_Albedo.png"); img.file_format = 'PNG'; img.save()

obj.name = NAME
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
out = os.path.join(out_dir, f"{NAME}.fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
print("PROP exported", out, "faces", len(obj.data.polygons), "height", HEIGHT)
