# 나무 줄기·가지를 한 몸으로 (PrototypeSceneBuilder.Trees.cs가 읽어요): 뼈대(점·선) → 마디 공·원뿔 → Voxel Remesh로 이음매를 녹이고
#   → 조금 다듬고 → 세로 껍질 골 → 약 3,200 삼각형으로 줄여 JSON (Unity 좌표: x, z, y). tips = 잎이 붙는 가지 끝 (자리·방향·크기·종류)
# 사용: blender -b --python treeskel.py -- out.json kind seed
#   지금 쓰는 것: pine 1~4 (pine_1..4.json), pineoak 7, willowfir 3, deadfir 5
import bpy, bmesh, sys, json, math, random
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
out, kind, seed = a[0], a[1], int(a[2])
rnd = random.Random(seed)
R = lambda lo, hi: lo + rnd.random() * (hi - lo)

bpy.ops.wm.read_factory_settings(use_empty=True)
verts, edges, radii = [], [], []
tips = []   # (위치, 방향, 크기, 종류)

def chain(pts, rs, parent=None):
    idx = []
    for p, r in zip(pts, rs):
        verts.append(Vector(p)); radii.append(r); idx.append(len(verts) - 1)
    if parent is not None: edges.append((parent, idx[0]))
    for i in range(len(idx) - 1): edges.append((idx[i], idx[i + 1]))
    return idx

def bez(p0, p1, p2, n):
    return [((1 - t) ** 2) * p0 + 2 * (1 - t) * t * p1 + t * t * p2 for t in [i / (n - 1) for i in range(n)]]

def lerp(a, b, t): return a + (b - a) * t

def trunk_s(height, lean, sway, r0, r1, n=18):
    pts, rs = [], []
    for i in range(n):
        t = i / (n - 1)
        kink = Vector((R(-0.05, 0.05), R(-0.05, 0.05), 0)) if 0 < i < n - 1 else Vector()
        pts.append(Vector((lean * math.sin(math.pi * t * 0.85) - lean * 0.55 * t ** 3, sway * math.sin(2 * math.pi * t), height * t)) + kink)
        rs.append(lerp(r0, r1, t) * (1 + 0.15 * (1 - t) ** 6) * R(0.92, 1.08))
    return pts, rs

def roots(idx0, base, r):
    for k in range(4):
        ang = k * math.pi / 2 + R(-0.4, 0.4)
        d = Vector((math.cos(ang), math.sin(ang), 0))
        pts = [base + d * 0.12 + Vector((0, 0, 0.1)), base + d * 0.3 + Vector((0, 0, 0.0)), base + d * 0.45 + Vector((0, 0, -0.06))]
        chain(pts, [r * 0.42, r * 0.25, r * 0.08], idx0)

def branch(at_idx, d, L, r0, droop=0.15, rise=0.35, n=6):
    s0 = verts[at_idx].copy()
    side = Vector((-d.y, d.x, 0))
    pts = [s0 + d * L * u + side * R(-0.08, 0.08) * L * math.sin(math.pi * u) + Vector((0, 0, -droop * math.sin(math.pi * u) * L + rise * u * u * L)) for u in [i / (n - 1) for i in range(n)]]
    rs = [lerp(r0, r0 * 0.15, (i / (n - 1)) ** 0.8) for i in range(n)]
    return chain(pts[1:], rs[1:], at_idx), pts[-1], (pts[-1] - pts[-2]).normalized()

if kind in ("pine", "pineoak"):
    lean = R(0.7, 1.0) * (1 if rnd.random() > 0.5 else -1)
    pts, rs = trunk_s(4.3, lean, R(-0.3, 0.3), 0.24 if kind == "pine" else 0.27, 0.08)
    tr = chain(pts, rs)
    roots(tr[0], verts[tr[0]].copy(), rs[0])
    tips.append((verts[tr[-1]] + Vector((0, 0, 0.1)), Vector((0, 0, 1)), 1.8, "pad"))
    side = -1 if lean > 0 else 1
    for (ti, sd, w) in ((8, side, 1.5), (10, -side, 1.35), (13, side * 0.6, 1.2), (15, -side * 0.5, 1.0)):
        ang = math.atan2(R(-0.6, 0.6), sd)
        d = Vector((math.cos(ang) * (1 if sd > 0 else 1), math.sin(ang), 0)).normalized()
        if sd < 0: d = Vector((-abs(d.x), d.y, 0)).normalized()
        else: d = Vector((abs(d.x), d.y, 0)).normalized()
        bi, end, bd = branch(tr[ti], d, 0.9 + w * 0.4, rs[ti] * 0.45)
        tips.append((end + Vector((0, 0, 0.08)), bd, w, "pad"))
        # 잔가지 하나 (층 아래쪽으로 갈라져 층을 넓혀요)
        if w > 1.1:
            mid = bi[len(bi) // 2]
            d2 = (d + Vector((d.y, -d.x, 0)) * R(-0.9, 0.9)).normalized()
            _, e2, bd2 = branch(mid, d2, 0.6, rs[ti] * 0.22, rise=0.5, n=4)
            tips.append((e2 + Vector((0, 0, 0.05)), bd2, w * 0.7, "pad"))

elif kind == "willowfir":
    # 장강: 전나무 원뿔 실루엣 (곧은 줄기 + 층층이 돌려난 가지, 위로 갈수록 짧게). 가지 안쪽엔 바늘잎, 바깥 끝에선 버들가지가 늘어져요
    n, H = 18, 5.8
    ts = [i / (n - 1) for i in range(n)]
    pts = [Vector((0.06 * math.sin(t * 5), 0.05 * math.cos(t * 4), H * t)) for t in ts]
    rs = [lerp(0.26, 0.04, t) * (1 + 0.25 * (1 - t) ** 6) * R(0.92, 1.06) for t in ts]
    tr = chain(pts, rs); roots(tr[0], verts[tr[0]].copy(), rs[0])
    tips.append((verts[tr[-1]].copy(), Vector((0, 0, 1)), 1.0, "top"))
    whorl = 0
    for ti in range(5, n - 2, 2):
        f = 1 - (pts[ti].z - pts[5].z) / (H - pts[5].z)
        L = 0.45 + 1.5 * f
        m = 5 if f > 0.4 else 4
        for k in range(m):
            ang = k * 2 * math.pi / m + whorl * 0.6 + R(-0.2, 0.2)
            d = Vector((math.cos(ang), math.sin(ang), -0.25)).normalized()
            bi, end, bd = branch(tr[ti], d, L, rs[ti] * 0.4, droop=0.1, rise=-0.05, n=5)
            tips.append((verts[bi[len(bi) // 2]].copy(), d, L, "needle"))
            tips.append((end, bd, L, "weep"))
        whorl += 1

elif kind == "deadfir":
    # 천마총: 굵고 비틀린 고목이 서너 갈래 → 잔가지로 갈라지고, 잔가지 끝 일부에만 짙은 바늘잎 술
    n, H = 12, 2.6
    pts = []
    for i in range(n):
        t = i / (n - 1)
        pts.append(Vector((0.18 * math.sin(t * 5.5) * t + R(-0.04, 0.04), 0.15 * math.cos(t * 4.2) * t + R(-0.04, 0.04), H * t)))
    rs = [lerp(0.4, 0.24, i / (n - 1)) * (1 + 0.15 * (1 - i / (n - 1)) ** 6) * R(0.94, 1.06) for i in range(n)]
    tr = chain(pts, rs); roots(tr[0], verts[tr[0]].copy(), rs[0])
    def limb(at, d, L, r0, depth):
        bi, end, bd = branch(at, d, L, r0, droop=R(-0.1, 0.2), rise=R(0.0, 0.3), n=5 if depth else 4)
        if depth == 0:
            if rnd.random() < 0.6: tips.append((end, bd, L, "tuft"))
            return
        for j in range(2 + (1 if rnd.random() > 0.5 else 0)):
            nd = (bd + Vector((R(-1, 1), R(-1, 1), R(-0.2, 0.6))) * 0.8).normalized()
            limb(bi[-1], nd, L * 0.65, r0 * 0.55, depth - 1)
        if depth == 2:   # 가지 중간의 짧은 잔가지 (고목 실루엣이 잔잔하게)
            nd = (bd + Vector((R(-1, 1), R(-1, 1), 0.2))).normalized()
            limb(bi[len(bi) // 2], nd, L * 0.4, r0 * 0.4, 0)
    for k in range(4):
        ang = k * math.pi / 2 + R(-0.3, 0.3)
        d = Vector((math.cos(ang), math.sin(ang), 0.75 - 0.25 * (k % 2))).normalized()
        limb(tr[-1 - (k % 2) * 2], d, R(1.3, 1.7), rs[-1] * 0.8, 2)

# 메시: 뼈대 → 마디마다 공 + 마디 사이마다 원뿔 (닫힌 덩어리들) → Voxel Remesh로 하나로 녹여요 → 조금 다듬기 → 껍질 골 → 줄이기
#   (Skin은 굵은 줄기·가지 이음매에 구멍을 남겨 Remesh가 줄기를 통째로 지웠어요 — 닫힌 덩어리끼리 녹이면 구멍이 안 생겨요)
from mathutils import Matrix
bm0 = bmesh.new()
for (i, j) in edges:
    p0, p1 = verts[i], verts[j]; L = (p1 - p0).length
    if L < 1e-4: continue
    rot = (p1 - p0).normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    mt = Matrix.Translation((p0 + p1) * 0.5) @ rot
    bmesh.ops.create_cone(bm0, cap_ends=True, segments=10, radius1=radii[i], radius2=radii[j], depth=L, matrix=mt)
for i, p0 in enumerate(verts):
    bmesh.ops.create_uvsphere(bm0, u_segments=10, v_segments=6, radius=radii[i] * 0.9, matrix=Matrix.Translation(p0))
me2 = bpy.data.meshes.new("parts"); bm0.to_mesh(me2); bm0.free()
ob = bpy.data.objects.new("tree2", me2); bpy.context.scene.collection.objects.link(ob)
bpy.context.view_layer.objects.active = ob
rm =ob.modifiers.new("rem", "REMESH"); rm.mode = "VOXEL"; rm.voxel_size = 0.025
sm = ob.modifiers.new("smo", "SMOOTH"); sm.iterations = 2; sm.factor = 0.5
tex = bpy.data.textures.new("bark", "CLOUDS"); tex.noise_scale = 0.08; tex.noise_depth = 1
emp = bpy.data.objects.new("barkspace", None); bpy.context.scene.collection.objects.link(emp)
emp.scale = (1.0, 1.0, 9.0)   # 세로로 길게 늘인 잡음 = 세로 껍질 골
ds = ob.modifiers.new("bark", "DISPLACE"); ds.texture = tex; ds.texture_coords = "OBJECT"; ds.texture_coords_object = emp; ds.strength = 0.045; ds.mid_level = 0.5
dc = ob.modifiers.new("dec", "DECIMATE"); dc.ratio = 1.0
dg = bpy.context.evaluated_depsgraph_get()
tris0 = len(ob.evaluated_get(dg).data.polygons)
dc.ratio = min(1.0, 1600.0 / max(1, tris0))   # 약 3,200 삼각형
dg = bpy.context.evaluated_depsgraph_get()
ev = ob.evaluated_get(dg); m2 = ev.to_mesh()
bm = bmesh.new(); bm.from_mesh(m2); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); bm.normal_update(); bm.verts.ensure_lookup_table()
# 매끈한 법선 (정점마다 면 법선 평균)
V = [(v.co.x, v.co.z, v.co.y) for v in bm.verts]
N = [(v.normal.x, v.normal.z, v.normal.y) for v in bm.verts]
T = []
for f in bm.faces:
    i0, i1, i2 = [v.index for v in f.verts]
    T += [i0, i2, i1]   # Z-up 오른손 → Y-up 왼손: 감는 방향을 뒤집어요
data = {"v": [c for p in V for c in p], "n": [c for p in N for c in p], "t": T,
        "tips": [{"p": [p.x, p.z, p.y], "d": [d.x, d.z, d.y], "w": w, "k": k} for p, d, w, k in tips]}
json.dump(data, open(out, "w"))
print("TREE", kind, seed, "verts", len(V), "tris", len(T) // 3, "tips", len(tips), "(before decimate", tris0, ")")
