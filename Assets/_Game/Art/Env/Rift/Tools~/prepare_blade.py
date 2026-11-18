# VARCO 칼붙이(glb, 눕혀 놓은 비도 같은 것) → Unity용 FBX + 색 텍스처 (blender -b --factory-startup --python prepare_blade.py -- <glb> <out_dir> <Name> <length> [max_faces])
#   prepare_prop.py와 같지만 높이 대신 "길이"를 맞춰요: 가장 긴 축을 Y로 눕히고 칼끝이 -Y(소품 앞 = VarcoProp yaw 0에서 카메라 쪽)를 보게,
#   가운데를 원점에 둬요. 칼끝 = 끝에서 6% 안쪽 단면이 더 가는 쪽 (손잡이 끝은 고리·술이 달려 굵어요)
import bpy, sys, os, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_dir, NAME, LENGTH = os.path.abspath(argv[0]), os.path.abspath(argv[1]), argv[2], float(argv[3])
MAX_FACES = int(argv[4]) if len(argv) > 4 else 4000
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

vs = [v.co.copy() for v in obj.data.vertices]
lo = Vector((min(v[i] for v in vs) for i in range(3)))
hi = Vector((max(v[i] for v in vs) for i in range(3)))
ax = max(range(3), key=lambda i: hi[i] - lo[i])
others = [i for i in range(3) if i != ax]


def girth(at):   # 긴 축 위 at 근처 단면의 굵기
    band = (hi[ax] - lo[ax]) * 0.03
    pts = [v for v in vs if abs(v[ax] - at) < band]
    return max((max(p[i] for p in pts) - min(p[i] for p in pts) for i in others), default=0.0) if pts else 0.0


span = hi[ax] - lo[ax]
tip_at_max = girth(hi[ax] - span * 0.06) < girth(lo[ax] + span * 0.06)
c = (lo + hi) / 2
k = LENGTH / span
# 긴 축 → Y, 칼끝 → -Y
R = Matrix.Identity(3)
if ax == 0:
    R = Matrix.Rotation(math.radians(90), 3, 'Z')     # +X → +Y
elif ax == 2:
    R = Matrix.Rotation(math.radians(-90), 3, 'X')    # +Z → +Y
flip = Matrix.Rotation(math.radians(180), 3, 'Z') if tip_at_max else Matrix.Identity(3)   # 칼끝이 +쪽이면 뒤집어 -Y로
for v in obj.data.vertices:
    v.co = flip @ (R @ ((v.co - c) * k))
print("BLADE long axis", "XYZ"[ax], "tip at max", tip_at_max)

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
print("BLADE exported", out, "faces", len(obj.data.polygons), "length", LENGTH)
