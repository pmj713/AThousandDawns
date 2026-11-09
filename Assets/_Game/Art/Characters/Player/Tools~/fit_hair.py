# Blender(5.x) 백그라운드 스크립트: 머리카락(GLB)을 기본 바디 머리에 맞춰 얹고 Player_Hair.fbx로 내보내요.
#   blender --background --factory-startup --python fit_hair.py -- <body.glb> <hair.glb> <output_dir> [preview.png] [z_adjust] [y_adjust] [scale_adjust]
# 바디는 split_body_parts.py와 같은 방식으로 맞춰요 (키 1.85, 발밑 원점). 기준점도 발밑 원점이라 다른 파츠와 같이 조립돼요.
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
body_path, hair_path, out_dir = argv[0], argv[1], argv[2]
preview = argv[3] if len(argv) > 3 and argv[3] != "-" else None
z_adj = float(argv[4]) if len(argv) > 4 else -0.028  # 맞춤값 (2026-11-09 확인: 정면 이마선·옆·뒤 덮임)
y_adj = float(argv[5]) if len(argv) > 5 else 0.012
s_adj = float(argv[6]) if len(argv) > 6 else 1.12
HEIGHT = 1.85

bpy.ops.wm.read_factory_settings(use_empty=True)

def import_joined(path, name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in new:
        if o != obj and o.name in bpy.data.objects:
            bpy.data.objects.remove(o, do_unlink=True)
    obj.name = name
    # glTF는 경계마다 점을 나눠 저장해서 겹친 점을 하나로 합쳐요
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bm.to_mesh(obj.data); bm.free()
    return obj

def bounds(obj, pred=None):
    cs = [v.co for v in obj.data.vertices if pred is None or pred(v.co)]
    xs = [c.x for c in cs]; ys = [c.y for c in cs]; zs = [c.z for c in cs]
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))

body = import_joined(body_path, "Body")
lo, hi = bounds(body)
s = HEIGHT / (hi.z - lo.z)
for v in body.data.vertices:
    v.co *= s
lo, hi = bounds(body)
off = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
for v in body.data.vertices:
    v.co -= off

# 머리: 목 위 (0.87H 위)
head_lo, head_hi = bounds(body, lambda c: c.z > 0.87 * HEIGHT and abs(c.x) < 0.15)
head_w = head_hi.x - head_lo.x
head_center = (head_lo + head_hi) / 2

hair = import_joined(hair_path, "Player_Hair")
EDIT = os.environ.get("HAIR_EDIT") == "1"
# 기본은 VARCO 원본 메시 그대로 (자리·크기만 맞춰요). HAIR_EDIT=1이면 아래 손질(줄이기·두피 맞춤·상투 다시 만들기)을 해요
if EDIT:
    # 폴리곤 줄이기: 머리카락은 얇은 열린 면 다발이라 AutoRemesher(닫힌 덩어리용)로 짜면 상투·관자놀이에 구멍이 나요.
    # 그래서 기본은 Decimate로 줄여요. HAIR_RETOPO=1이면 AutoRemesher로 시험해 볼 수 있어요
    HAIR_TRIS = 5000
    tris_now = sum(len(p.vertices) - 2 for p in hair.data.polygons)
    how = "DECIMATE"
    if os.environ.get("HAIR_RETOPO") == "1":
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from autoremesh import autoremesh
        old = hair.data
        hair.data = autoremesh(old, int(os.environ.get("HAIR_QUADS", "2500")), preserve_thin=True, island_detail=10)
        bpy.data.meshes.remove(old)
        how = "RETOPO"
    elif tris_now > HAIR_TRIS:
        dec = hair.modifiers.new("Decimate", 'DECIMATE')
        dec.decimate_type = 'COLLAPSE'
        dec.ratio = HAIR_TRIS / tris_now
        bpy.context.view_layer.objects.active = hair
        bpy.ops.object.modifier_apply(modifier=dec.name)
    hair.data.shade_smooth()
    print("HAIR_" + how, tris_now, "->", sum(len(p.vertices) - 2 for p in hair.data.polygons))
hlo, hhi = bounds(hair)
hs = head_w * 1.18 / (hhi.x - hlo.x) * s_adj
for v in hair.data.vertices:
    v.co *= hs
hlo, hhi = bounds(hair)
hair_h = hhi.z - hlo.z
# 상투가 머리 위로 솟게: 머리카락 맨 위 = 정수리 + 머리카락 키의 30%
target_top = head_hi.z + hair_h * 0.30 + z_adj
move = Vector((head_center.x - (hlo.x + hhi.x) / 2,
               head_center.y - (hlo.y + hhi.y) / 2 + 0.01 + y_adj,
               target_top - hhi.z))
for v in hair.data.vertices:
    v.co += move

from mathutils.bvhtree import BVHTree
def gap_report(tag):
    # 두피(눈썹 위) 점에서 바깥으로 쏴서 머리카락 안쪽 면까지 틈, 바깥 면까지 두께를 재요
    hb = BVHTree.FromPolygons([v.co for v in hair.data.vertices], [p.vertices[:] for p in hair.data.polygons])
    gaps, thick = [], []
    for p in body.data.polygons:
        if p.center.z < head_center.z or abs(p.center.x) > 0.12:
            continue
        o = p.center + p.normal * 1e-4
        hit, _, _, g = hb.ray_cast(o, p.normal, 0.2)
        if hit is None:
            continue
        gaps.append(g)
        hit2, _, _, g2 = hb.ray_cast(hit + p.normal * 1e-4, p.normal, 0.2)
        if hit2 is not None:
            thick.append(g2)
    gaps.sort(); thick.sort()
    q = lambda a, t: round(a[int(t * (len(a) - 1))] * 100, 2) if a else None
    print("HAIR_GAP", tag, "cm  p10/p50/p90:", q(gaps, .1), q(gaps, .5), q(gaps, .9), " thickness p50/p90:", q(thick, .5), q(thick, .9))

if EDIT:
    gap_report('before')
    # 머리에 붙이기: 머리카락 안쪽 면과 두피 사이 틈을 GAP만 남기고 좁혀요.
    # 두피의 가장 가까운 점에서 바깥(두피 법선)으로 광선을 쏴 머리카락 안쪽 면까지 거리를 재고,
    # 그만큼 그 자리 머리카락(안쪽·바깥 면 모두)을 두피 쪽으로 당겨요 → 머리카락 두께는 그대로, 상투도 같이 내려와요
    GAP = 0.006
    MIN_CLEAR = 0.004   # 두피가 머리카락 밖으로 비치지 않게 남기는 최소 거리 (줄인 바디와의 오차 포함)
    head_bvh = BVHTree.FromPolygons([v.co for v in body.data.vertices],
                                    [p.vertices[:] for p in body.data.polygons if p.center.z > 0.8 * HEIGHT])
    nbrs = [[] for _ in hair.data.vertices]
    for e in hair.data.edges:
        a, b = e.vertices
        nbrs[a].append(b); nbrs[b].append(a)
    total = [Vector() for _ in hair.data.vertices]
    for _pass in range(4):   # 고르게 펴면 덜 당겨지는 곳이 남아서 몇 번 되풀이해요
        hair_bvh = BVHTree.FromPolygons([v.co for v in hair.data.vertices], [p.vertices[:] for p in hair.data.polygons])
        disp = []
        for v in hair.data.vertices:
            p, n, _, d = head_bvh.find_nearest(v.co)
            if p is None or d > 0.15:
                disp.append(Vector()); continue
            hit, _, _, g = hair_bvh.ray_cast(p + n * 1e-4, n, 0.15)
            disp.append(-n * (g - GAP) if hit is not None and g > GAP else Vector())
        # 이웃끼리 당기는 양을 고르게 (찢어짐 방지)
        for _ in range(4):
            disp = [(disp[i] + sum((disp[j] for j in nbrs[i]), Vector())) / (1 + len(nbrs[i])) for i in range(len(disp))]
        for i, (v, d) in enumerate(zip(hair.data.vertices, disp)):
            v.co += d
            total[i] += d
    disp = total
    # 두피를 뚫고 들어간 점은 두피 바깥 MIN_CLEAR까지 밀어내요
    pushed = 0
    for _ in range(3):
        for v in hair.data.vertices:
            p, n, _, d = head_bvh.find_nearest(v.co)
            if p is None:
                continue
            h = (v.co - p).dot(n)
            if h < MIN_CLEAR:
                v.co += n * (MIN_CLEAR - h)
                pushed += 1
    print("HAIR_PUSHED", pushed)
    gap_report('after')
    print("HAIR_SNAP max", round(max(d.length for d in disp), 4), "avg", round(sum(d.length for d in disp) / len(disp), 4))

    # 상투 다시 만들기: VARCO 상투는 위가 컵처럼 파이고 안쪽 면이 구겨지거나 뒤집혀 있어 게임에서 윗부분이 비어 보여요.
    # 정수리 위로 솟은 원래 상투를 잘라내고, 둥근 상투 + 밑동 묶음을 새로 붙여요
    import bmesh
    hm = hair.data
    top = max(v.co.z for v in hm.vertices)
    tip = [v.co for v in hm.vertices if v.co.z > top - 0.02]
    axis = Vector((sum(c.x for c in tip) / len(tip), sum(c.y for c in tip) / len(tip), 0))
    radial = sorted(math.hypot(c.x - axis.x, c.y - axis.y) for c in tip)
    bun_r = min(0.038, max(0.03, radial[int(0.8 * (len(radial) - 1))]))
    crown, _, _, _ = head_bvh.ray_cast(Vector((axis.x, axis.y, top + 0.1)), Vector((0, 0, -1)), 1.0)
    cut_z = crown.z + 0.012           # 머리카락 덮개(틈+두께)높이쯤
    bm = bmesh.new(); bm.from_mesh(hm)
    def _bun_face(f):
        c = f.calc_center_median()
        if math.hypot(c.x - axis.x, c.y - axis.y) > bun_r * 2.5:
            return False
        if c.z > cut_z:
            return True
        # 머리 덮개보다 두피에서 더 솟은 면 = 원래 상투 밑동 찌꺼기
        _, _, _, d = head_bvh.find_nearest(c)
        return d is not None and d > 0.022
    kill = [f for f in bm.faces if _bun_face(f)]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    removed = len(kill)
    base = Vector((axis.x, axis.y, cut_z - 0.008))
    # 밑동 묶음: 아래가 넓고 위가 좁은 짧은 원통 (잘린 자리를 덮어요)
    ring = bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=bun_r * 1.15, radius2=bun_r * 0.75, depth=0.016)
    bmesh.ops.translate(bm, verts=ring["verts"], vec=base + Vector((0, 0, 0.008)))
    # 상투 덩어리: 위아래로 살짝 눌린 공
    ball = bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=bun_r)
    bmesh.ops.scale(bm, verts=ball["verts"], vec=Vector((1.0, 1.0, 0.8)))
    bmesh.ops.translate(bm, verts=ball["verts"], vec=base + Vector((0, 0, 0.012 + bun_r * 0.6)))
    bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if all(v in set(ring["verts"]) | set(ball["verts"]) for v in f.verts)])
    bm.to_mesh(hm); bm.free()
    hm.shade_smooth()
    print("HAIR_BUN radius", round(bun_r, 3), "cut_z", round(cut_z, 3), "removed faces", removed, "faces now", len(hm.polygons))


os.makedirs(out_dir, exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
hair.select_set(True)
bpy.context.view_layer.objects.active = hair
bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, "Player_Hair.fbx"), use_selection=True, object_types={'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True)
print("HAIR", {"faces": len(hair.data.polygons), "scale": round(hs, 4), "head_w": round(head_w, 3)})

if preview:
    body.color = (0.75, 0.75, 0.78, 1); hair.color = (0.12, 0.12, 0.14, 1)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'OBJECT'
    scene.render.resolution_x, scene.render.resolution_y = 1400, 800
    scene.world = bpy.data.worlds.new("W")
    # 머리 확대: 정면 / 옆 / 뒤 세 방향
    copies = []
    for i, angle in enumerate((0, 90, 180)):
        for o in (body, hair):
            c = o.copy(); c.data = o.data; scene.collection.objects.link(c)
            c.rotation_mode = 'XYZ'  # glTF로 가져온 물체는 쿼터니언 회전이라 오일러 각이 안 먹어요
            c.rotation_euler = (0, 0, math.radians(angle)); c.location.x = (i - 1) * 0.42
            copies.append(c)
    body.hide_render = True; hair.hide_render = True
    cam_data = bpy.data.cameras.new("Cam"); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 1.3
    cam = bpy.data.objects.new("Cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
    cam.location = (0, -10, head_center.z + 0.05); cam.rotation_euler = (math.radians(90), 0, 0)
    scene.render.filepath = preview
    bpy.ops.render.render(write_still=True)
