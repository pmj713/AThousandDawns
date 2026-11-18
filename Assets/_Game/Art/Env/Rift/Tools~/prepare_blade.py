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

if len(argv) > 5 and argv[5] == "culm":
    # 줄기만 남겨요 (대나무 잎·잔가지가 옆으로 삐져나와 있어요, 플레이테스트 9차): 잎이 없는 가운데 40% 구간에서 줄기 축(가운데)과 굵기를 재고,
    #   어디서든 그 축에서 굵기의 1.2배보다 먼 점은 지워요
    import bmesh, statistics
    vs0 = [v.co.copy() for v in obj.data.vertices]
    lo0 = [min(v[i] for v in vs0) for i in range(3)]; hi0 = [max(v[i] for v in vs0) for i in range(3)]
    ax0 = max(range(3), key=lambda i: hi0[i] - lo0[i]); o0 = [i for i in range(3) if i != ax0]
    a0, a1 = lo0[ax0] + (hi0[ax0] - lo0[ax0]) * 0.3, lo0[ax0] + (hi0[ax0] - lo0[ax0]) * 0.7
    mid = [v for v in vs0 if a0 < v[ax0] < a1]
    cx = [statistics.median(v[i] for v in mid) for i in o0]
    dist = lambda v: ((v[o0[0]] - cx[0]) ** 2 + (v[o0[1]] - cx[1]) ** 2) ** 0.5
    rad = statistics.median(dist(v) for v in mid)   # 줄기 겉면까지 거리 (잎이 섞여도 가운데값은 줄기)
    bm = bmesh.new(); bm.from_mesh(obj.data)
    kill = [v for v in bm.verts if dist(v.co) > rad * 1.2]
    # 잎이 몰린 끝(지운 점이 많은 쪽)은 끝 마디 너머 8%를 잘라요 — 축 가까이 붙은 잎 밑동까지 떼요
    span0 = hi0[ax0] - lo0[ax0]
    near_hi = sum(1 for v in kill if v.co[ax0] > (lo0[ax0] + hi0[ax0]) / 2)
    cut_hi = near_hi > len(kill) / 2
    kill += [v for v in bm.verts if v not in kill and ((v.co[ax0] > hi0[ax0] - span0 * 0.08) if cut_hi else (v.co[ax0] < lo0[ax0] + span0 * 0.08))]
    bmesh.ops.delete(bm, geom=kill, context='VERTS')
    for v in bm.verts:   # 줄기 축이 원점을 지나게
        v.co[o0[0]] -= cx[0]; v.co[o0[1]] -= cx[1]
    bm.to_mesh(obj.data); bm.free()
    print("BLADE culm radius", round(rad, 3), "removed", len(kill))

if len(argv) > 5 and argv[5] == "largest":
    # 가장 큰 덩어리(줄기)만 남겨요 — 대나무 잎·잔가지처럼 따로 붙은 조각을 떼어 내고, 가운데를 줄기 축에 맞춰요 (플레이테스트 9차)
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    left, parts = set(bm.verts), []
    while left:
        seed = left.pop(); part = {seed}; stack = [seed]
        while stack:
            v = stack.pop()
            for e in v.link_edges:
                o = e.other_vert(v)
                if o in left:
                    left.discard(o); part.add(o); stack.append(o)
        parts.append(part)
    keep = max(parts, key=len)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v not in keep], context='VERTS')
    bm.to_mesh(obj.data); bm.free()
    print("BLADE kept largest part", len(keep), "of", len(parts), "parts")

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
