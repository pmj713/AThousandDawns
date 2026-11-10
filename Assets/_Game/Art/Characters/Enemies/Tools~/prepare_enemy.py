# Blender(5.x) 백그라운드 스크립트: VARCO로 만들고 자동 리깅한 적(사람형)을 Unity용으로 정리해요.
#   blender --background --factory-startup --python prepare_enemy.py -- <Rigged.fbx> <out_dir> <Name> <height> <weapon,...> [preview.png]
#   - 키(m)·발밑 원점으로 맞춰요
#   - 색 텍스처를 <Name>_Albedo.png로 꺼내요 (Unity 수묵 재질의 _BaseMap)
#   - 무기를 손·팔 뼈에 통째로 붙여요 (<Name>_Weapon_*): dao(오른손 박도) dagger(오른손 단검) bow(왼손 활)
#     shield(왼팔 둥근 방패) bomb(오른손 화약 단지) none
#   - 특징: prop=<VARCO 소품.glb>@<자리> · banner(등 깃발) · scarf(붉은 목도리) · blowhorn(나팔 부는 동작, BlowHorn)
#   - <Name>_Rigged.fbx로 내보내요 (플레이어와 같은 축 설정)
import bpy, bmesh, sys, os, math
from mathutils import Vector, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_dir, NAME = os.path.abspath(argv[0]), os.path.abspath(argv[1]), argv[2]
HEIGHT = float(argv[3])
WEAPONS = [w for w in argv[4].split(",") if w and w != "none"]
preview = os.path.abspath(argv[5]) if len(argv) > 5 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.data.objects if o.type == 'MESH')
rig.name = f"{NAME}_Rig"; body.name = f"{NAME}_Body"

# 크기·원점
bpy.context.view_layer.update()
vs = [body.matrix_world @ v.co for v in body.data.vertices]
s = HEIGHT / (max(v.z for v in vs) - min(v.z for v in vs))
rig.scale = rig.scale * s
bpy.context.view_layer.update()
vs = [body.matrix_world @ v.co for v in body.data.vertices]
cx = (min(v.x for v in vs) + max(v.x for v in vs)) / 2; cy = (min(v.y for v in vs) + max(v.y for v in vs)) / 2
rig.location -= Vector((cx, cy, min(v.z for v in vs)))
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
print("ENEMY", NAME, "scale %.3f" % s)

# 텍스처
os.makedirs(out_dir, exist_ok=True)
img = next((n.image for m in body.data.materials if m and m.use_nodes for n in m.node_tree.nodes
            if n.type == 'TEX_IMAGE' and n.image), None)
if img is not None:
    img.filepath_raw = os.path.join(out_dir, f"{NAME}_Albedo.png"); img.file_format = 'PNG'; img.save()
    print("ENEMY texture", img.size[:])

B = rig.data.bones
weapon_objs = []
FWD = Vector((0, -1, 0))   # 몸 앞


def make(name, bone, build):
    bm = bmesh.new()
    build(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    o.vertex_groups.new(name=bone).add(list(range(len(me.vertices))), 1.0, 'REPLACE')
    o.parent = rig
    o.modifiers.new("Armature", 'ARMATURE').object = rig
    weapon_objs.append(o)


def box(bm, c, axis, side, length, w, th):
    up = axis.cross(side).normalized()
    vs_ = []
    for dz in (-length / 2, length / 2):
        for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs_.append(bm.verts.new(c + axis * dz + side * (w / 2 * a) + up * (th / 2 * b)))
    for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
        bm.faces.new([vs_[i] for i in f])


def grip_of(side):
    """쥐는 자리 = 그 손 뼈에 붙은 손 메시의 가운데 (FBX로 들어온 손 뼈 꼬리는 손가락 쪽을 가리키지 않을 때가 있어요)."""
    g = body.vertex_groups.get(f"{side}Hand")
    if g is not None:
        pts = [body.matrix_world @ v.co for v in body.data.vertices if any(e.group == g.index and e.weight > 0.5 for e in v.groups)]
        if len(pts) > 20:
            return sum(pts, Vector()) / len(pts)
    h = B[f"{side}Hand"]
    return h.head_local.lerp(h.tail_local, 0.6)


def blade(bm, g, axis, edge, L, wide):
    """손잡이·코등이·외날 칼날 (등은 곧고 날은 끝으로 넓어졌다 뾰족)."""
    box(bm, g, axis, edge, 0.15, 0.03, 0.028)
    box(bm, g + axis * 0.085, axis, edge, 0.02, 0.07, 0.035)
    flat = axis.cross(edge).normalized()
    rings = []
    for u, ww in ((0.0, wide * 0.65), (0.75, wide), (0.95, wide * 0.85), (1.0, 0.0)):
        c = g + axis * (0.095 + L * u)
        rings.append([bm.verts.new(c + edge * 0.02)] if ww == 0 else
                     [bm.verts.new(c + flat * 0.004), bm.verts.new(c + edge * ww), bm.verts.new(c - flat * 0.004)])
    for a, b in zip(rings[:-1], rings[1:]):
        for i in range(3):
            j = (i + 1) % 3
            if len(b) == 1:
                bm.faces.new((a[i], a[j], b[0]))
            else:
                bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(rings[0])))


for w in WEAPONS:
    if w in ("dao", "dagger", "bigdao"):
        g = grip_of("Right")
        axis = Vector((0, -1, -0.15)).normalized()
        edge = Vector((0, 0, -1)); edge = (edge - axis * edge.dot(axis)).normalized()
        L, wide = {"dao": (0.62, 0.07), "dagger": (0.26, 0.035), "bigdao": (1.15, 0.16)}[w]   # bigdao: 녹림왕의 대도
        make(f"{NAME}_Weapon_{w}", "RightHand", lambda bm, g=g, a=axis, e=edge, L=L, wd=wide: blade(bm, g, a, e, L, wd))
    elif w == "jian":
        # 곧은 양날 장검 (청죽객): 손잡이·코등이 + 마름모 단면 칼날이 끝으로 뾰족해요
        g = grip_of("Right")
        axis = Vector((0, -1, -0.15)).normalized()
        flat = Vector((0, 0, -1)); flat = (flat - axis * flat.dot(axis)).normalized()

        def build(bm, g=g, a=axis, e=flat):
            box(bm, g, a, e, 0.18, 0.03, 0.028)
            box(bm, g + a * 0.1, a, e, 0.02, 0.1, 0.03)
            t = a.cross(e).normalized()
            rings = []
            for u, ww in ((0.0, 0.022), (0.9, 0.018), (1.0, 0.0)):
                c = g + a * (0.11 + 0.85 * u)
                rings.append([bm.verts.new(c)] if ww == 0 else
                             [bm.verts.new(c + e * ww), bm.verts.new(c + t * 0.004), bm.verts.new(c - e * ww), bm.verts.new(c - t * 0.004)])
            for r0, r1 in zip(rings[:-1], rings[1:]):
                for i in range(4):
                    j = (i + 1) % 4
                    if len(r1) == 1:
                        bm.faces.new((r0[i], r0[j], r1[0]))
                    else:
                        bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
            bm.faces.new(list(reversed(rings[0])))
        make(f"{NAME}_Weapon_jian", "RightHand", build)
    elif w == "bow" and "shootbow" not in WEAPONS:   # 활 쏘는 동작이 있으면 그 자세에 맞춰 아래에서 만들어요
        g = grip_of("Left")

        def build(bm, g=g):
            # 위아래로 휜 활대 (몸 앞쪽으로 볼록) + 시위
            n, prev = 12, None
            for i in range(n + 1):
                t = i / n - 0.5
                c = g + Vector((0, 0, t * 1.2)) + FWD * (0.12 * (1 - (2 * t) ** 2))
                th = 0.02 * (1 - abs(t)) + 0.008
                ring = [bm.verts.new(c + Vector((math.cos(a) * th, math.sin(a) * th, 0))) for a in (0, 2.1, 4.2)]
                if prev:
                    for k in range(3):
                        bm.faces.new((prev[k], prev[(k + 1) % 3], ring[(k + 1) % 3], ring[k]))
                prev = ring
            box(bm, g, Vector((0, 0, 1)), Vector((1, 0, 0)), 1.2, 0.004, 0.004)
        make(f"{NAME}_Weapon_bow", "LeftHand", build)
    elif w == "shield":
        f = B["LeftForeArm"]
        c = f.head_local.lerp(f.tail_local, 0.55)
        out = Vector((1, -0.6, 0)).normalized()   # 바깥·앞쪽을 향해요

        def build(bm, c=c, out=out):
            r, th = 0.32, 0.04
            u = Vector((0, 0, 1)); v = out.cross(u).normalized()
            ang = [k * math.pi / 8 for k in range(16)]
            front = [bm.verts.new(c + out * (0.06 + th) + (u * math.cos(a) + v * math.sin(a)) * r) for a in ang]
            back = [bm.verts.new(c + out * 0.06 + (u * math.cos(a) + v * math.sin(a)) * r) for a in ang]
            bm.faces.new(front); bm.faces.new(list(reversed(back)))
            for k in range(16):
                j = (k + 1) % 16
                bm.faces.new((front[k], back[k], back[j], front[j]))
        make(f"{NAME}_Weapon_shield", "LeftForeArm", build)
    elif w == "bomb":
        g = grip_of("Right") + FWD * 0.04

        def build(bm, g=g):
            ret = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=7, radius=0.07)
            bmesh.ops.translate(bm, verts=ret["verts"], vec=g)
        make(f"{NAME}_Weapon_bomb", "RightHand", build)

# ── 알아보기 쉬운 특징: VARCO로 만든 소품(prop=파일@자리)과 직접 그린 장식(banner·scarf), 나팔 부는 동작(blowhorn)
#    prop 자리: shield(왼팔 바깥) hat(머리 위) back(등) hand_r(오른손) horn(왼손, 나팔 부는 자세에 맞춰 입에 대요)
PROPS = [w for w in argv[4].split(",") if w.startswith("prop=")]
EXTRAS = set(WEAPONS)
mw = rig.matrix_world


def import_prop(path):
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
    o = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for x in new:
        if x != o and x.name in bpy.data.objects:
            bpy.data.objects.remove(x, do_unlink=True)
    return o


def bbox(o):
    vs_ = [v.co for v in o.data.vertices]
    lo = Vector((min(v.x for v in vs_), min(v.y for v in vs_), min(v.z for v in vs_)))
    hi = Vector((max(v.x for v in vs_), max(v.y for v in vs_), max(v.z for v in vs_)))
    return lo, hi


def recenter(o):
    lo, hi = bbox(o)
    c = (lo + hi) / 2
    for v in o.data.vertices:
        v.co -= c
    return hi - lo


def thin_to(o, axis):
    """가장 얇은 축을 axis(0=x,1=y,2=z)로 돌려요 (방패 면·삿갓 높이·등짐 두께)."""
    d = recenter(o)
    thin = min(range(3), key=lambda i: d[i])
    if thin == axis:
        return
    for v in o.data.vertices:
        c = list(v.co); c[thin], c[axis] = c[axis], c[thin]
        v.co = Vector(c)


def align_normal(o, target, prefer=None):
    """판처럼 생긴 소품(방패·삿갓)의 면 방향을 점 분포의 가장 얇은 방향(주성분)으로 찾아 target을 보게 돌려요.
    VARCO 소품은 3/4 각도로 기울어 나와서 축 맞추기만으로는 비스듬해요. 가운데가 볼록한 쪽(방패 손잡이 반대·삿갓 꼭대기)이 target 쪽"""
    import numpy as np
    recenter(o)
    pts = np.array([v.co[:] for v in o.data.vertices])
    _, _, vt = np.linalg.svd(pts - pts.mean(axis=0), full_matrices=False)
    n = Vector(vt[2].tolist()).normalized()
    # 가운데(축에서 가까운 점)가 테두리보다 어느 쪽으로 튀어나왔는지
    d = pts @ np.array(n[:])
    r = np.linalg.norm(pts - np.outer(d, np.array(n[:])), axis=1)
    core, rim = d[r < np.percentile(r, 20)], d[r > np.percentile(r, 80)]
    if prefer is not None:          # 방향을 알 때(삿갓은 생성된 그대로 위쪽): 그쪽을 따라요
        if n.dot(prefer) < 0:
            n = -n
    elif core.mean() - rim.mean() < 0:
        n = -n
    q = n.rotation_difference(target)
    for v in o.data.vertices:
        v.co = q @ v.co
    recenter(o)


def long_to(o, axis):
    d = recenter(o)
    lng = max(range(3), key=lambda i: d[i])
    if lng != axis:
        for v in o.data.vertices:
            c = list(v.co); c[lng], c[axis] = c[axis], c[lng]
            v.co = Vector(c)


def scale_to(o, size):
    d = recenter(o)
    k = size / max(d)
    for v in o.data.vertices:
        v.co *= k


def bind(o, bone, name, key=None):
    """지금 자리(뼈대 기본 자세 기준)에서 bone에 통째로 붙이고, 텍스처를 꺼내요."""
    o.name = name
    if key is not None:
        img = next((n.image for m in o.data.materials if m and m.use_nodes for n in m.node_tree.nodes
                    if n.type == 'TEX_IMAGE' and n.image), None)
        if img is not None:
            img.filepath_raw = os.path.join(out_dir, f"{NAME}_Prop_{key}_Albedo.png"); img.file_format = 'PNG'; img.save()
    o.vertex_groups.new(name=bone).add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
    o.parent = rig
    o.modifiers.new("Armature", 'ARMATURE').object = rig
    weapon_objs.append(o)


# strip_shield: VARCO 몸에 같이 생긴 왼팔 방패를 잘라내요 (따로 만든 소품 방패만 남겨 부서지면 없앨 수 있게).
# 아래팔 뼈에서 팔 두께보다 멀리 떨어진 점 = 방패
if "strip_shield" in set(WEAPONS):
    import bmesh as _bm
    f = B["LeftForeArm"]; hnd = B["LeftHand"]
    a0, a1 = f.head_local, hnd.tail_local

    def seg_d(p):
        ab = a1 - a0
        t = max(0.0, min(1.0, (p - a0).dot(ab) / ab.length_squared))
        return (p - (a0 + ab * t)).length, t
    bm = _bm.new(); bm.from_mesh(body.data)
    kill = []
    for v in bm.verts:
        d, t = seg_d(v.co)
        if 0.02 < t < 1.0 and 0.1 < d < 0.55 and v.co.x > a0.x - 0.05:
            kill.append(v)
    _bm.ops.delete(bm, geom=kill, context='VERTS')
    bm.to_mesh(body.data); bm.free()
    print("ENEMY stripped built-in shield verts", len(kill))

head_top = max((body.matrix_world @ v.co).z for v in body.data.vertices)
body_back = max((body.matrix_world @ v.co).y for v in body.data.vertices
                if abs((body.matrix_world @ v.co).z - B["Spine2"].head_local.z) < 0.12)
horn_prop = None
for spec in PROPS:
    path, mount = spec[5:].split("@")
    key = os.path.splitext(os.path.basename(path))[0].replace("Prop_", "")
    o = import_prop(os.path.abspath(path))
    if mount == "shield":
        align_normal(o, Vector((0, -1, 0))); scale_to(o, 0.78)
        f = B["LeftForeArm"]
        mid = f.head_local.lerp(f.tail_local, 0.5)
        # 아래팔 앞면(몸 앞쪽 -Y)에서 2cm 띄워 방패 뒷면을 대요 (팔뚝에 박히지 않게)
        arm_pts = [body.matrix_world @ v.co for v in body.data.vertices]
        arm_pts = [p for p in arm_pts if abs(p.x - mid.x) < 0.08 and abs(p.z - mid.z) < 0.08]
        front = min(p.y for p in arm_pts) if arm_pts else mid.y - 0.05
        lo_, hi_ = bbox(o)
        c = Vector((mid.x, front - 0.02 - hi_.y, mid.z - 0.02))
        for v in o.data.vertices:
            v.co += c
        bind(o, "LeftForeArm", f"{NAME}_Prop_{key}", key)
    elif mount == "hat":
        align_normal(o, Vector((0, 0, 1)), prefer=Vector((0, 0, 1))); scale_to(o, 0.78)
        # 챙(가장 넓은 높이)을 찾아 머리 꼭대기 조금 아래에 대고, 챙 아래로 늘어진 끈은 잘라요 (얼굴을 뚫지 않게)
        import bmesh as _bm
        rim_z = max(o.data.vertices, key=lambda v: v.co.x * v.co.x + v.co.y * v.co.y).co.z
        bm = _bm.new(); bm.from_mesh(o.data)
        _bm.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < rim_z - 0.04], context='VERTS')
        bm.to_mesh(o.data); bm.free()
        h = B["Head"]
        c = Vector((h.head_local.x, h.head_local.y, head_top - 0.06 - rim_z))
        for v in o.data.vertices:
            v.co += c
        bind(o, "Head", f"{NAME}_Prop_{key}", key)
    elif mount == "back":
        thin_to(o, 1); scale_to(o, 0.7)
        lo, hi = bbox(o)
        c = Vector((0, body_back - lo.y + 0.02, B["Spine2"].head_local.z))
        for v in o.data.vertices:
            v.co += c
        bind(o, "Spine2", f"{NAME}_Prop_{key}", key)
    elif mount == "back3":   # 같은 소품 셋을 등에 묶어 짊어져요 (화약 산적 등짐)
        scale_to(o, 0.3)
        import bmesh as _bm
        src_me = o.data.copy()
        bm = _bm.new()
        for off in (Vector((-0.12, 0, 0.12)), Vector((0.12, 0, 0.12)), Vector((0, 0.04, -0.12))):
            t = src_me.copy()
            for v in t.vertices:
                v.co += off + Vector((0, body_back + 0.11, B["Spine2"].head_local.z))
            bm.from_mesh(t); bpy.data.meshes.remove(t)
        bm.to_mesh(o.data); bm.free(); bpy.data.meshes.remove(src_me)
        bind(o, "Spine2", f"{NAME}_Prop_{key}", key)
    elif mount == "hand_r":
        scale_to(o, 0.34)
        g = grip_of("Right") + FWD * 0.12
        for v in o.data.vertices:
            v.co += g
        bind(o, "RightHand", f"{NAME}_Prop_{key}", key)
    elif mount == "horn":
        long_to(o, 1); scale_to(o, 0.6)
        horn_prop = (o, key)

# 직접 그린 장식 (붉은 천은 이름에 Red → Unity가 붉은 수묵 재질을 입혀요)
if "banner" in EXTRAS:   # 나팔수: 등에 꽂은 신호 깃발
    sp = B["Spine2"].head_local
    base = Vector((0.08, body_back + 0.03, sp.z - 0.25))
    top = base + Vector((0, 0.05, 1.45))

    def pole(bm):
        box(bm, (base + top) / 2, (top - base).normalized(), Vector((1, 0, 0)), (top - base).length, 0.03, 0.03)
    make(f"{NAME}_Weapon_Pole", "Spine2", pole)

    def flag(bm):
        rows = 6
        prev = None
        for i in range(rows + 1):
            t = i / rows
            z = top.z - 0.05 - t * 0.55
            a = bm.verts.new(Vector((top.x, top.y, z)))
            b_ = bm.verts.new(Vector((top.x + 0.42 - 0.05 * math.sin(t * 3), top.y + 0.06 * math.sin(t * 4), z - 0.04 * t)))
            if prev:
                bm.faces.new((prev[0], prev[1], b_, a))
            prev = (a, b_)
    make(f"{NAME}_Red_Banner", "Spine2", flag)
if "scarf" in EXTRAS:   # 살수: 목에서 뒤로 길게 날리는 붉은 목도리
    n = B["Neck"].head_local

    def scarf(bm):
        prev = None
        for i in range(10):
            t = i / 9
            c = Vector((0.06 * math.sin(t * 5), body_back * 0.6 + 0.05 + 0.75 * t, n.z - 0.03 - 0.35 * t * t))
            a = bm.verts.new(c + Vector((-0.09 * (1 - 0.4 * t), 0, 0)))
            b_ = bm.verts.new(c + Vector((0.09 * (1 - 0.4 * t), 0, 0)))
            if prev:
                bm.faces.new((prev[0], prev[1], b_, a))
            prev = (a, b_)
        # 목을 두른 고리
        r = 0.085
        ring = [[bm.verts.new(n + Vector((math.cos(k * math.pi / 6) * r, math.sin(k * math.pi / 6) * r, dz))) for k in range(12)] for dz in (0.02, -0.04)]
        for k in range(12):
            j = (k + 1) % 12
            bm.faces.new((ring[0][k], ring[0][j], ring[1][j], ring[1][k]))
    make(f"{NAME}_Red_Scarf", "Neck", scarf)

# 나팔 부는 동작 (예고 1.5초): 두 손으로 나팔을 입에 대고 고개를 젖혀 불어요. 나팔은 이 자세에서 입에 맞춰 왼손에 붙여요
if "blowhorn" in EXTRAS:
    scene = bpy.context.scene
    scene.render.fps = 30
    hd = B["Head"].head_local
    mouth = hd + Vector((0, -0.11, 0.06))
    t_l = bpy.data.objects.new("IK_L", None); scene.collection.objects.link(t_l)
    t_r = bpy.data.objects.new("IK_R", None); scene.collection.objects.link(t_r)
    p_l = bpy.data.objects.new("Pole_L", None); scene.collection.objects.link(p_l)
    p_r = bpy.data.objects.new("Pole_R", None); scene.collection.objects.link(p_r)
    p_l.location = B["LeftArm"].head_local + Vector((0.35, 0.2, -0.5))
    p_r.location = B["RightArm"].head_local + Vector((-0.35, 0.2, -0.5))
    pb = rig.pose.bones
    for side, tgt, pole_ in (("Left", t_l, p_l), ("Right", t_r, p_r)):
        c = pb[f"{side}ForeArm"].constraints.new('IK')
        c.target = tgt; c.chain_count = 2; c.pole_target = pole_; c.pole_angle = math.radians(-90)
    for p in pb:
        p.rotation_mode = 'QUATERNION'
    act = bpy.data.actions.new("BlowHorn"); rig.animation_data_create(); rig.animation_data.action = act
    chest_hold = Vector((0, -0.28, -0.32))
    hold_l = mouth + Vector((0.05, -0.07, -0.03))
    hold_r = mouth + Vector((0.12, -0.28, 0.02))
    keys = [(0, 0.0, 0.0), (7, 1.0, 0.0), (15, 1.0, 1.0), (25, 1.0, 0.6), (35, 1.0, 1.0), (45, 1.0, 0.8)]
    for f, lift, breath in keys:
        t_l.location = (mouth + chest_hold).lerp(hold_l, lift)
        t_r.location = (mouth + chest_hold + Vector((0, -0.1, 0))).lerp(hold_r, lift)
        t_l.keyframe_insert("location", frame=f); t_r.keyframe_insert("location", frame=f)
        for p in pb:
            p.rotation_quaternion = Quaternion()
        # 고개를 젖히고 가슴을 펴 불어요 (Head·Spine2 뼈 축이 위쪽이라 X축 회전이 앞뒤 숙임)
        pb["Head"].rotation_quaternion = Quaternion(Vector((1, 0, 0)), math.radians(-22 * lift))
        pb["Spine2"].rotation_quaternion = Quaternion(Vector((1, 0, 0)), math.radians(-6 * lift - 4 * breath))
        for p in pb:
            p.keyframe_insert("rotation_quaternion", frame=f)
    scene.frame_start, scene.frame_end = 0, 45
    bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.nla.bake(frame_start=0, frame_end=45, only_selected=False, visual_keying=True, clear_constraints=True,
                     use_current_action=True, bake_types={'POSE'})
    bpy.ops.object.mode_set(mode='OBJECT')
    for o in (t_l, t_r, p_l, p_r):
        if o.animation_data and o.animation_data.action:
            bpy.data.actions.remove(o.animation_data.action)   # IK 목표 동작은 내보내지 않게
        bpy.data.objects.remove(o, do_unlink=True)
    act.use_fake_user = True
    if horn_prop is not None:
        o, key = horn_prop
        scene.frame_set(20)
        bpy.context.view_layer.update()
        # 좁은 끝(입대)이 -Y 쪽이 되게: 양 끝 10% 단면의 폭(가로·세로 범위)을 비교해요. 휜 뿔이라 끝 단면 가운데를 입대로 써요
        ys = [v.co.y for v in o.data.vertices]
        ymin, ymax = min(ys), max(ys)

        def end_slice(lo_end):
            cut = ymin + (ymax - ymin) * 0.08 if lo_end else ymax - (ymax - ymin) * 0.08
            pts = [v.co.copy() for v in o.data.vertices if (v.co.y < cut if lo_end else v.co.y > cut)]
            w_ = (max(p.x for p in pts) - min(p.x for p in pts)) + (max(p.z for p in pts) - min(p.z for p in pts))
            return w_, sum(pts, Vector()) / len(pts)
        w_lo, c_lo = end_slice(True)
        w_hi, c_hi = end_slice(False)
        if w_lo > w_hi:   # 넓은 쪽(나팔 끝)이 -Y면 뒤집어요
            for v in o.data.vertices:
                v.co.y = -v.co.y; v.co.x = -v.co.x
            c_hi = Vector((-c_lo.x, -c_lo.y, c_lo.z)); c_lo = Vector((-c_hi.x, -c_hi.y, c_hi.z)) if False else c_lo
            w_lo, c_lo = end_slice(True)
            w_hi, c_hi = end_slice(False)
        tip = c_lo
        # 휜 뿔의 입대→나팔 방향이 dir_를 보게 (몸 앞·위·바깥으로)
        body_axis = (c_hi - c_lo).normalized()
        dir_ = Vector((0.25, -1, 0.45)).normalized()
        rot = body_axis.rotation_difference(dir_)
        hand_pb, hand_b = pb["LeftHand"], B["LeftHand"]
        # 이 자세의 실제 입 자리: 머리 뼈가 젖혀진 만큼 기본 자세 입 위치를 같이 옮겨요
        head_pb, head_b = pb["Head"], B["Head"]
        mouth_now = (head_pb.matrix @ head_b.matrix_local.inverted()) @ mouth + Vector((0, -0.02, 0))
        to_rest = hand_b.matrix_local @ hand_pb.matrix.inverted()
        for v in o.data.vertices:
            world = mouth_now + rot @ (v.co - tip)
            v.co = to_rest @ world
        bind(o, "LeftHand", f"{NAME}_Prop_{key}", key)
        scene.frame_set(0)
    rig.animation_data.action = None
    for p in pb:
        p.rotation_quaternion = Quaternion(); p.location = (0, 0, 0)

# ── 직접 만드는 동작 (활 쏘기·주먹·붕산권). 단순하게 관절 위치를 정하고 두 마디 팔·다리 풀이로 각도를 계산해요.
#    FBX로 들어온 뼈 꼬리는 다음 관절을 가리키지 않아서 관절 위치로 방향을 재요.
scene = bpy.context.scene
scene.render.fps = 30
pb = rig.pose.bones
UP = Vector((0, 0, 1))


def reset_pose():
    for p in pb:
        p.rotation_mode = 'QUATERNION'; p.rotation_quaternion = Quaternion(); p.location = (0, 0, 0)
    bpy.context.view_layer.update()


def aim(name, to_pt, child=None, delta=None):
    """뼈 머리는 그대로 두고 다음 관절(child 머리)이 to_pt를 향하도록 가장 작게 돌려요. delta를 주면 그 회전을 그대로 써요."""
    b, p = B[name], pb[name]
    head = p.head.copy()
    if delta is None:
        rest_dir = (B[child].head_local - b.head_local).normalized()
        delta = rest_dir.rotation_difference((to_pt - head).normalized())
    m = (delta.to_matrix() @ b.matrix_local.to_3x3()).to_4x4()
    m.translation = head
    p.matrix = m
    bpy.context.view_layer.update()
    return delta


def two_bone(upper, lower, end, target, pole):
    s = pb[upper].head.copy()
    l1 = (B[lower].head_local - B[upper].head_local).length
    l2 = (B[end].head_local - B[lower].head_local).length
    v = target - s
    dist = max(min(v.length, (l1 + l2) * 0.999), abs(l1 - l2) + 1e-3)
    n = v.normalized()
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    pp = pole - n * pole.dot(n)
    pp = pp.normalized() if pp.length > 1e-6 else UP
    joint = s + n * a + pp * h
    aim(upper, joint, lower)
    return aim(lower, s + n * dist, end)


def arm(side, wrist, pole):
    d = two_bone(f"{side}Arm", f"{side}ForeArm", f"{side}Hand", wrist, pole)
    aim(f"{side}Hand", None, delta=d)                       # 손목은 곧게 (아래팔과 같이 돌려요)


def lower_hips(dz, feet, dy=0.0):
    """엉덩이를 dz만큼 내리고(dy만큼 앞뒤로 옮기고) 발은 제자리에 두도록 무릎을 굽혀요."""
    p = pb["Hips"]
    m = p.matrix.copy(); m.translation.z += dz; m.translation.y += dy; p.matrix = m
    bpy.context.view_layer.update()
    for side, foot in feet.items():
        two_bone(f"{side}UpLeg", f"{side}Leg", f"{side}Foot", foot, Vector((0, -1, 0)))
        aim(f"{side}Foot", None, delta=Quaternion())        # 발은 기본 방향 그대로


def bend(name, axis, deg):
    """지금 자세에서 뼈를 축(아마추어 공간) 둘레로 더 돌려요. 자식 뼈는 따라와요."""
    cur = pb[name].matrix.to_quaternion() @ B[name].matrix_local.to_quaternion().inverted()
    aim(name, None, delta=Quaternion(axis, math.radians(deg)) @ cur)


BLADE_REST = Vector((0, -1, -0.15)).normalized()   # 기본 자세에서 오른손 칼(dao·bigdao·jian)이 뻗는 방향
MAX_TWIST = math.pi if "generic" in EXTRAS else math.radians(85)   # Humanoid는 넘으면 Unity가 잘라서 칼이 튀어요 (Generic은 한계 없음)


def point_blade(want, wrist_bend=0.0):
    """손목을 꺾지 않고 아래팔 축 둘레로만 비틀어, 칼끝이 want 쪽(아래팔에 수직인 면 안에서 가장 가까운 방향)을 향하게 해요.
    칼은 주먹을 가로질러 똑바로 쥔 채예요."""
    p = pb["RightHand"]
    a = (p.head - pb["RightForeArm"].head).normalized()
    cur = p.matrix.to_quaternion() @ B["RightHand"].matrix_local.to_quaternion().inverted()
    c = cur @ BLADE_REST
    c = c - a * c.dot(a)
    w = want.normalized() - a * want.normalized().dot(a)
    if c.length > 1e-4 and w.length > 1e-4:
        ang = math.atan2(a.dot(c.cross(w)), c.dot(w))
        ang = max(-MAX_TWIST, min(MAX_TWIST, ang))       # Unity Humanoid 아래팔 비틀기 한계(약 ±90°) 안에서만
        aim("RightHand", None, delta=Quaternion(a, ang) @ cur)
    if wrist_bend > 0:
        # 남은 차이는 손목을 젖혀서 (wrist_bend 도까지, Unity 손목 한계 안)
        cur = p.matrix.to_quaternion() @ B["RightHand"].matrix_local.to_quaternion().inverted()
        c = (cur @ BLADE_REST).normalized()
        axis = c.cross(want.normalized())
        if axis.length > 1e-4:
            bend_ang = min(c.angle(want.normalized()), math.radians(wrist_bend))
            aim("RightHand", None, delta=Quaternion(axis.normalized(), bend_ang) @ cur)


def make_action(name, keys, pose):
    """keys의 각 프레임 값으로 pose(값)를 세우고 회전(엉덩이는 위치도)을 키로 넣어요.
    자세는 동작 없이 계산해 두고 (동작이 걸려 있으면 갱신할 때마다 키 값으로 덮여요), 나중에 한꺼번에 키를 넣어요."""
    rig.animation_data_create(); rig.animation_data.action = None
    poses = []
    for f, val in keys:
        reset_pose()
        pose(val)
        poses.append((f, {p.name: p.rotation_quaternion.copy() for p in pb}, pb["Hips"].location.copy()))
    # 같은 회전도 q와 -q 두 값이 있어요. 키마다 앞 키와 같은 쪽 값으로 맞춰야 사이를 이을 때 먼 길로 빙글 돌지 않아요
    for (_, prev, _), (_, cur, _) in zip(poses[:-1], poses[1:]):
        for n, q in cur.items():
            if q.dot(prev[n]) < 0:
                cur[n] = -q
    act = bpy.data.actions.new(name); rig.animation_data.action = act
    for f, quats, hips in poses:
        for p in pb:
            p.rotation_quaternion = quats[p.name]
            p.keyframe_insert("rotation_quaternion", frame=f)
        pb["Hips"].location = hips
        pb["Hips"].keyframe_insert("location", frame=f)
    act.use_fake_user = True
    return act


reset_pose()
SH_L, SH_R, HD = pb["LeftArm"].head.copy(), pb["RightArm"].head.copy(), pb["Head"].head.copy()
FEET = {s: pb[f"{s}Foot"].head.copy() for s in ("Left", "Right")}
REACH = ((B["LeftForeArm"].head_local - B["LeftArm"].head_local).length + (B["LeftHand"].head_local - B["LeftForeArm"].head_local).length) * 0.97

# 활 쏘기 (예고 0.7초 = 21프레임 동안 시위를 당겨 판정 순간 놓아요):
#   왼팔은 처음부터 앞으로 곧게 활을 들고, 오른손은 시위(활 바로 뒤)에서 볼까지 당겼다가 놓으면 뒤로 살짝 튕겨요.
#   활은 다 당긴 자세에서 세로로 서고 시위가 몸 쪽에 오도록 만든 뒤, 왼손 기본 자세로 되돌려 붙여요
if "shootbow" in EXTRAS:
    bow_wrist = SH_L + Vector((-0.15, -1, 0.03)).normalized() * REACH      # 왼팔 앞으로 곧게 (조금 안쪽)
    nock = bow_wrist + Vector((-0.02, 0.16, 0))                             # 시위 (활 바로 뒤)
    cheek = HD + Vector((-0.08, -0.07, 0.04))                               # 오른 볼 (다 당긴 끝)
    flick = cheek + Vector((-0.08, 0.1, 0.02))                              # 놓은 뒤 손이 뒤로 튕겨요

    def shoot_pose(wrist_r):
        arm("Left", bow_wrist, Vector((0.4, 0, -1)))                        # 팔꿈치는 바깥·아래
        arm("Right", wrist_r, Vector((-0.5, 0.7, 0.25)))                    # 당기는 팔꿈치는 옆·뒤·위로
    make_action("ShootBow", [(0, nock), (4, nock), (19, cheek), (21, cheek), (23, flick), (30, flick)], shoot_pose)
    scene.frame_start, scene.frame_end = 0, 30

    scene.frame_set(19); bpy.context.view_layer.update()
    ph = pb["LeftHand"]
    delta_inv = (ph.matrix @ B["LeftHand"].matrix_local.inverted()).inverted()
    grip = ph.head.lerp(ph.tail, 0.45)
    to_body = Vector((SH_R.x - grip.x, SH_R.y - grip.y, 0)).normalized()

    def build_bow(bm, g=grip, bk=to_body):
        n_, prev = 12, None
        side = bk.cross(UP).normalized()
        for i in range(n_ + 1):
            t = i / n_ - 0.5
            c = g + UP * (t * 1.15) + bk * (0.14 * (2 * t) ** 2)            # 가운데(손잡이)는 앞, 끝은 몸 쪽으로 휘어요
            th = 0.022 * (1 - abs(t)) + 0.008
            ring = [bm.verts.new(delta_inv @ (c + (side * math.cos(a) + bk * math.sin(a)) * th)) for a in (0, 2.1, 4.2)]
            if prev:
                for k in range(3):
                    bm.faces.new((prev[k], prev[(k + 1) % 3], ring[(k + 1) % 3], ring[k]))
            prev = ring
        tip_hi, tip_lo = g + UP * 0.575 + bk * 0.14, g - UP * 0.575 + bk * 0.14  # 시위 (가는 세모 기둥)
        sr = [[bm.verts.new(delta_inv @ (tip + (side * math.cos(a) + bk * math.sin(a)) * 0.004)) for a in (0, 2.1, 4.2)] for tip in (tip_lo, tip_hi)]
        for k in range(3):
            bm.faces.new((sr[0][k], sr[0][(k + 1) % 3], sr[1][(k + 1) % 3], sr[1][k]))
    make(f"{NAME}_Weapon_bow", "LeftHand", build_bow)
    rig.animation_data.action = None
    reset_pose()

# 맨손 고수의 공통 자세: 두 주먹을 가슴 앞에 (가드)
GUARD = {"Left": SH_L + Vector((-0.12, -0.3, -0.12)), "Right": SH_R + Vector((0.12, -0.3, -0.12))}
POLE_GUARD = {"Left": Vector((0.6, 0.2, -1)), "Right": Vector((-0.6, 0.2, -1))}


def guard(skip=()):
    for s in ("Left", "Right"):
        if s not in skip:
            arm(s, pb[f"{s}Arm"].head + (GUARD[s] - (SH_L if s == "Left" else SH_R)), POLE_GUARD[s])


# 주먹 2연타 (Punch2, 판정 0.35초 간격): 몸 전체로 치는 묵직한 원투.
#   0 오른 주먹을 허리춤으로 당기고 몸을 뒤로 튼 자세 (예고 동안 여기서 멈춰요) → 4 무릎을 굽히고 앞으로 실으며 오른 주먹 쭉
#   → 8 왼 주먹을 당기며 반대로 틀기 → 13 왼 주먹 쭉 → 22 가드
if "punch" in EXTRAS:
    def chamber(side):
        sh = pb[f"{side}Arm"].head.copy()
        out = 1 if side == "Left" else -1
        arm(side, sh + Vector((out * 0.08, 0.18, -0.38)), Vector((out, 0.6, -0.2)))   # 주먹을 옆구리로 (팔꿈치는 뒤로)

    def strike(side):
        sh = pb[f"{side}Arm"].head.copy()
        inward = 0.22 if side == "Right" else -0.22
        arm(side, sh + Vector((inward, -1, 0.0)).normalized() * REACH * 1.05, Vector((-inward * 3, 0.2, -1)))

    def punch_pose(stage):
        if stage == "guard":
            lower_hips(-0.08, FEET)
            guard()
            return
        side, phase = stage
        sign = 1 if side == "Right" else -1                                  # 오른쪽을 칠 때 +: 오른 어깨가 앞으로
        other = "Left" if side == "Right" else "Right"
        if phase == "load":
            lower_hips(-0.1, FEET, dy=0.05)
            bend("Spine", UP, -22 * sign); bend("Spine1", UP, -12 * sign)   # 치는 쪽 어깨를 뒤로 감아요
            chamber(side)
            guard(skip=(side,))
        else:
            lower_hips(-0.16, FEET, dy=-0.14)                               # 무릎을 굽혀 앞으로 실어요
            bend("Spine", Vector((1, 0, 0)), 10)
            bend("Spine", UP, 30 * sign); bend("Spine1", UP, 15 * sign)     # 허리째 돌려 내질러요
            strike(side)
            chamber(other)
    make_action("Punch2", [(0, ("Right", "load")), (4, ("Right", "hit")), (8, ("Left", "load")), (13, ("Left", "hit")),
                           (17, ("Left", "hit")), (24, "guard")], punch_pose)
    rig.animation_data.action = None
    reset_pose()

GRIP_R = grip_of("Right")   # 기본 자세에서 오른손이 칼자루를 쥔 자리


def blade_now():
    """지금 자세의 칼자루 자리와 칼끝 방향."""
    p = pb["RightHand"]
    m = p.matrix @ B["RightHand"].matrix_local.inverted()
    return m @ GRIP_R, (m.to_quaternion() @ BLADE_REST).normalized()


def left_on_hilt(back=0.16):
    """왼손으로 칼자루 끝(오른손 아래쪽)을 함께 잡아요 (양손 잡기)."""
    g, d = blade_now()
    arm("Left", g - d * back, Vector((1, 0.4, -1)))

def fit_right(base, offsets, poles, want, wrist_bend=0.0):
    """오른손을 base+offset 후보들에 두어 보고, 아래팔 비틀기 한계 안에서 칼끝이 want에 가장 가까운 자리·팔꿈치 방향으로 세워요."""
    want = want.normalized()
    best = None
    for off in offsets:
        for pole in poles:
            arm("Right", base + off, pole)
            point_blade(want, wrist_bend)
            score = blade_now()[1].dot(want)
            if best is None or score > best[0]:
                best = (score, off, pole)
    arm("Right", base + best[1], best[2])
    point_blade(want, wrist_bend)
    return best[0]


ALL_POLES = (Vector((-0.5, 0.4, -1)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((-0.5, -0.5, 0)), Vector((0, 0.5, 1)), Vector((-1, 0.3, 0.6)))



# 회전베기 자세 (Whirl): 대도를 양손으로 잡고 오른쪽 앞으로 칼끝을 뻗은 채 무릎을 굽혀요 (힐윈드).
#   빙글빙글 도는 것은 Unity(InkEnemyAnimator)가 모델을 판정 동안 시계 반대 방향으로 돌려서 해요
if "whirl" in EXTRAS:
    def whirl_pose(_):
        lower_hips(-0.2, FEET)
        bend("Spine", Vector((1, 0, 0)), 10)
        bend("Spine", UP, -15)
        chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
        want = Vector((-1, -0.35, 0.05)).normalized()                        # 칼끝은 오른쪽 바깥으로 눕혀요
        # 아래팔 비틀기 한계 안에서 칼끝이 want에 가장 가깝게 눕는 손 자리·팔꿈치 방향을 골라요
        best = None
        for x in (-0.45, -0.3, -0.15, 0.0):
            for y in (-0.55, -0.4, -0.25):
                for z in (-0.4, -0.25, -0.1):
                    for pole in (Vector((-0.5, 0.3, -1)), Vector((-1, 0, 0)), Vector((0, 0.5, 1)), Vector((-0.5, -0.5, 0.5))):
                        wrist = chest + Vector((x, y, z))
                        arm("Right", wrist, pole)
                        point_blade(want)
                        score = blade_now()[1].dot(want)
                        if best is None or score > best[0]:
                            best = (score, wrist, pole)
        arm("Right", best[1], best[2])
        point_blade(want)
        print("ENEMY whirl blade fit %.2f" % best[0])
        left_on_hilt()
    make_action("Whirl", [(0, None), (10, None)], whirl_pose)
    rig.animation_data.action = None
    reset_pose()

# ── 플레이어 대기(Idle)·달리기(Run)를 이 뼈대로 옮겨 구워요 (Generic 적: 녹림왕·청죽객·철권 마웅).
#    Generic이라 Unity에서 플레이어 동작을 바로 못 쓰기 때문이에요.
#    팔다리는 뼈 방향(관절→다음 관절)을 맞추고, 엉덩이·몸통·목·머리·발은 기본 자세에서 돈 만큼(월드 기준)을 그대로 옮겨요.
#    armspread=<배수>: 몸통이 두꺼우면 팔이 옆구리에 붙어서 바깥으로 벌려요 (기본 1, 날씬하면 작게)
HAS_BLADE = bool({"dao", "bigdao", "jian"} & EXTRAS)
SPREAD = float(next((w.split("=")[1] for w in WEAPONS if w.startswith("armspread=")), "1"))
player_objs, player_acts = [], []
if "playeranim" in EXTRAS or "sweep" in EXTRAS:
    player_fbx = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Player", "Parts", "Player_Rigged.fbx"))
    before = set(bpy.data.objects); before_act = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=player_fbx)
    prig = next(o for o in bpy.data.objects if o not in before and o.type == 'ARMATURE')
    player_objs = [o for o in bpy.data.objects if o not in before]
    player_acts = [a for a in bpy.data.actions if a not in before_act]
    PB, PBN = prig.pose.bones, prig.data.bones
    pmw = prig.matrix_world
    rot_map = [("Hips", "Hips"), ("Spine", "Spine"), ("Spine1", "Chest"), ("Neck", "Neck"), ("Head", "Head"),
               ("LeftShoulder", "LeftShoulder"), ("RightShoulder", "RightShoulder")]
    limb_map = [(f"{s}{a}", f"{s}{b}", f"{s}{ca}", f"{s}{cb}") for s in ("Left", "Right") for a, b, ca, cb in
                (("UpLeg", "UpperLeg", "Leg", "LowerLeg"), ("Leg", "LowerLeg", "Foot", "Foot"),
                 ("Arm", "UpperArm", "ForeArm", "LowerArm"), ("ForeArm", "LowerArm", "Hand", "Hand"))]
    end_map = [(f"{s}{n}", f"{s}{n}") for s in ("Left", "Right") for n in ("Hand", "Foot")]
    hip_scale = B["Hips"].head_local.z / (pmw @ PBN["Hips"].head_local).z

    def pdelta(pn):
        """플레이어 뼈가 기본 자세에서 월드 기준으로 돈 회전."""
        posed = (pmw @ PB[pn].matrix).to_quaternion()
        rest = (pmw @ PBN[pn].matrix_local).to_quaternion()
        return posed @ rest.inverted()

    def use_player(act):
        prig.animation_data_create(); prig.animation_data.action = act
        if hasattr(prig.animation_data, "action_slot") and act.slots:
            prig.animation_data.action_slot = act.slots[0]

    def player_pose(frame):
        scene.frame_set(frame)
        prest = pmw @ PBN["Hips"].head_local
        pnow = pmw @ PB["Hips"].head
        hp = pb["Hips"]
        m = (pdelta("Hips").to_matrix() @ B["Hips"].matrix_local.to_3x3()).to_4x4()
        m.translation = B["Hips"].head_local + Vector((0, 0, (pnow - prest).z * hip_scale))
        hp.matrix = m
        bpy.context.view_layer.update()
        for n, pn in rot_map[1:]:
            if n in pb and pn in PB:
                aim(n, None, delta=pdelta(pn))
        fore = {}
        for n, pn, cn, cpn in limb_map:
            d = ((pmw @ PB[cpn].head) - (pmw @ PB[pn].head)).normalized()
            out = Vector((1, 0, 0)) if n.startswith("Left") else Vector((-1, 0, 0))
            spread = {"Arm": 0.55, "ForeArm": 0.3}.get(n[4:] if n.startswith("Left") else n[5:], 0.0) * SPREAD
            fore[n] = aim(n, pb[n].head + (d + out * spread).normalized(), cn)
        for n, pn in end_map:
            if n.endswith("Hand"):
                # 손은 아래팔과 일직선 (플레이어 손 각도를 옮기면 벌린 팔에서 손목이 꺾여 보여요)
                aim(n, None, delta=fore[n.replace("Hand", "ForeArm")])
            else:
                aim(n, None, delta=pdelta(pn))
        if HAS_BLADE:
            fa = (pb["RightHand"].head - pb["RightForeArm"].head).normalized()
            point_blade((fa + Vector((0, -0.12, 0))).normalized(), wrist_bend=90)   # 칼은 팔과 일직선으로 늘어뜨려요

    def bake_player(name):
        act = next(a for a in player_acts if a.name.split("|")[-1] == name)
        use_player(act)
        s, e = int(act.frame_range[0]), int(act.frame_range[1])
        make_action(name, [(f - s, f) for f in range(s, e + 1)], player_pose)
        rig.animation_data.action = None
        reset_pose()
        return act, s
    idle_act, idle_start = bake_player("Idle")
    bake_player("Run")

    def idle_end_pose():
        """공격이 끝나면 대기(플레이어 대기) 첫 자세 그대로 (넘어갈 때 칼·팔이 휙 돌지 않게)."""
        use_player(idle_act)
        player_pose(idle_start)

# 대도 휘두르기 (Sweep): 오른쪽 뒤로 대도를 치켜 감았다가(예고 동안 여기서 멈춰요) 허리째 돌려 칼이 수평 반원을 그려요.
if "sweep" in EXTRAS:
    def sweep_pose(stage):
        if stage == "idle":
            idle_end_pose()
            return
        twist, wrist, lift, dy, blade = {
            "load": (-50, Vector((-0.65, 0.45, -0.05)), -0.14, 0.06, Vector((-0.2, 1, 0.12))),   # 오른쪽 뒤로 감아 칼끝은 뒤로
            "hit": (0, Vector((-0.3, -0.85, -0.12)), -0.2, -0.1, Vector((-0.35, -1, 0))),       # 오른쪽 앞을 휩쓸어요
            "follow": (45, Vector((0.45, -0.75, -0.15)), -0.2, -0.1, Vector((1, -0.45, 0))),     # 왼쪽 앞으로 휘둘러 나가요
            "end": (55, Vector((0.75, -0.1, -0.25)), -0.14, -0.05, Vector((0.85, 0.5, -0.1))),   # 왼쪽 뒤까지 돌아 나가요
        }[stage]
        lower_hips(lift, FEET, dy=dy)
        bend("Spine", UP, twist * 0.6); bend("Spine1", UP, twist * 0.4)
        sh = pb["RightArm"].head.copy()
        arm("Right", sh + wrist.normalized() * REACH * 0.82, Vector((0.2, 0.5, -1)))   # 팔꿈치를 살짝 굽혀요
        point_blade(blade)
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((-0.1, -0.35, -0.3)) if stage == "load" else sl + Vector((0.25, 0.1, -0.4)), Vector((1, 0.3, -0.5)))
    make_action("Sweep", [(0, "load"), (4, "hit"), (7, "follow"), (11, "end"), (16, "end"), (26, "idle")], sweep_pose)
    rig.animation_data.action = None
    reset_pose()

# 청죽객 장검 (jianmoves): 빠르고 가벼운 검술. 왼손은 검지·중지를 세운 검결처럼 가슴 앞에 둬요
#   Combo(쾌검 3연격, 0.3초 간격): 오른쪽 위에서 왼쪽 아래로 내려베기 → 되받아 오른쪽으로 가로베기 → 허리에서 앞으로 찌르기
#   Flash(죽림섬, 0.25초 간격): 오른쪽·왼쪽으로 번갈아 가로베기 4번
#   Chop(대나무 베기): 머리 위로 들었다가(예고 1초 동안 멈춤) 앞으로 크게 내려베기
JIAN = {
    # (허리 틀기, 손 방향, 팔 뻗기, 엉덩이 높이, 앞뒤, 칼끝 방향)
    "c_load": (-30, Vector((-0.4, 0.1, 0.5)), 0.9, -0.06, 0.0, Vector((-0.3, 0.3, 1))),
    "c1": (25, Vector((0.4, -0.6, -0.4)), 0.88, -0.14, -0.06, Vector((0.6, -0.6, -0.5))),
    "c2load": (30, Vector((0.55, -0.35, 0.0)), 0.8, -0.1, -0.03, Vector((1, 0.3, 0))),
    "c2": (-35, Vector((-0.6, -0.5, 0.0)), 0.9, -0.12, -0.06, Vector((-1, -0.4, 0))),
    "c3load": (-20, Vector((-0.3, 0.25, -0.3)), 0.7, -0.1, 0.03, Vector((0, -1, 0))),
    "c3": (20, Vector((0.05, -1, 0.0)), 1.0, -0.18, -0.16, Vector((0, -1, 0))),
    "f_r": (-35, Vector((-0.6, -0.45, 0.0)), 0.9, -0.12, -0.05, Vector((-1, -0.3, 0))),
    "f_l": (35, Vector((0.55, -0.5, 0.0)), 0.9, -0.12, -0.05, Vector((1, -0.4, 0))),
    "ch_up": (-10, Vector((0.0, 0.1, 1.0)), 0.95, -0.04, 0.03, Vector((0, 0.5, 1))),
    "ch_down": (5, Vector((0.0, -0.8, -0.35)), 0.95, -0.2, -0.12, Vector((0, -1, -0.6))),
}
if "jianmoves" in EXTRAS:
    def jian_pose(stage):
        if stage == "idle":
            idle_end_pose()
            return
        twist, wrist, reach, lift, dy, blade = JIAN[stage]
        lower_hips(lift, FEET, dy=dy)
        bend("Spine", UP, twist * 0.6); bend("Spine1", UP, twist * 0.4)
        sh = pb["RightArm"].head.copy()
        pole = Vector((-0.6, 0.3, 0)) if wrist.z > 0.3 else Vector((0.2, 0.5, -1))
        arm("Right", sh + wrist.normalized() * REACH * reach, pole)
        point_blade(blade, wrist_bend=80)                                  # 찌르기·내려베기는 칼이 팔을 따라 뻗도록 손목을 젖혀요
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((-0.12, -0.28, -0.12)), Vector((1, 0.3, -0.6)))   # 검결: 왼손을 가슴 앞에
    make_action("Combo", [(0, "c_load"), (3, "c1"), (7, "c2load"), (10, "c2"), (14, "c3load"), (18, "c3"), (22, "c3"), (30, "idle")], jian_pose)
    make_action("Flash", [(0, "f_r"), (4, "f_l"), (11, "f_r"), (19, "f_l"), (26, "f_r"), (34, "idle")], jian_pose)
    make_action("Chop", [(0, "ch_up"), (4, "ch_down"), (16, "ch_down"), (26, "idle")], jian_pose)
    rig.animation_data.action = None
    reset_pose()

# 철권 마웅 어깨 돌진 (shouldercharge): 준비는 낮게 웅크려 오른 어깨를 앞세우고 두 주먹으로 얼굴을 가린 자세,
#   돌진은 그 자세로 크게 숙여 들이받으며 내달리는 반복 동작 (16프레임)
if "shouldercharge" in EXTRAS:
    def fists_up():
        for s, inward in (("Left", -0.15), ("Right", 0.15)):
            sh = pb[f"{s}Arm"].head.copy()
            arm(s, sh + Vector((inward, -0.22, 0.12)), Vector((-inward * 4, 0.3, -1)))

    def shoulder_ready(_):
        lower_hips(-0.28, FEET, dy=0.06)
        bend("Spine", Vector((1, 0, 0)), 28)
        bend("Spine", UP, 30)                                               # 오른 어깨를 앞으로
        fists_up()
    make_action("ShoulderReady", [(0, None), (10, None)], shoulder_ready)
    rig.animation_data.action = None
    reset_pose()

    def shoulder_run(phase):
        lead = "Left" if phase in (0, 3) else "Right"
        feet = {}
        for s in ("Left", "Right"):
            base = FEET[s]
            if phase in (0, 2):
                feet[s] = base + (Vector((0, -0.5, 0)) if s == lead else Vector((0, 0.45, 0.26)))
            else:
                feet[s] = base + (Vector((0, -0.1, 0.4)) if s != lead else Vector((0, 0.15, 0.0)))
        lower_hips(-0.2 if phase in (0, 2) else -0.1, feet, dy=-0.05)
        bend("Spine", Vector((1, 0, 0)), 32)
        bend("Spine", UP, 30 + (5 if lead == "Left" else -5))
        fists_up()
    make_action("ShoulderRun", [(0, 0), (4, 1), (8, 2), (12, 3), (16, 0)], shoulder_run)
    rig.animation_data.action = None
    reset_pose()


# 돌진 (ChargeReady·ChargeRun·Slam): 준비는 몸을 낮추고 앞으로 숙여 대도를 오른 어깨 위로 치켜든 자세,
#   돌진은 크게 앞으로 숙인 채 다리를 크게 내딛고 왼팔을 휘저으며 대도를 치켜든 채 달리는 반복 동작 (16프레임),
#   끝에서 양손으로 내려찍고(Slam) 그 자세로 멈춰요
if "charge" in EXTRAS:
    def sword_up(lift=0.0):
        # 세로베기 직전처럼 오른팔을 곧게 위로 쭉 뻗어 대도를 치켜들어요 (칼끝은 위로, 손목을 젖혀서)
        sh = pb["RightArm"].head.copy()
        offs = [Vector((x, y, 1.0)).normalized() * REACH * (0.99 - lift) for x in (-0.06, 0.0) for y in (-0.08, 0.0, 0.06)]
        fit_right(sh, offs, ALL_POLES, Vector((0, 0.3, 1)), wrist_bend=70)   # 칼은 팔을 따라 위로 서요

    def ready_pose(_):
        lower_hips(-0.32, FEET, dy=0.08)                                    # 깊게 앉아요
        bend("Spine", Vector((1, 0, 0)), 30)                                # 앞으로 숙여요
        bend("Spine", UP, -15)
        sword_up()
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((-0.05, -0.5, -0.35)), Vector((1, 0, -1)))  # 왼손은 앞으로 내밀어 균형
    make_action("ChargeReady", [(0, None), (10, None)], ready_pose)
    rig.animation_data.action = None
    reset_pose()

    stride = 0.55

    def run_pose(phase):
        # phase 0: 왼발 앞 디딤 · 오른발 뒤로 들림 / 1: 지나감(오른 무릎 높이) / 2: 오른발 앞 / 3: 지나감(왼 무릎 높이)
        lead = "Left" if phase in (0, 3) else "Right"
        feet = {}
        for s in ("Left", "Right"):
            base = FEET[s]
            if phase in (0, 2):
                fwd = s == lead
                feet[s] = base + (Vector((0, -stride, 0)) if fwd else Vector((0, stride * 0.9, 0.28)))
            else:
                passing = s != lead                                         # 앞으로 나오는 다리는 무릎을 높이
                feet[s] = base + (Vector((0, -0.1, 0.42)) if passing else Vector((0, 0.15, 0.0)))
        lower_hips(-0.2 if phase in (0, 2) else -0.1, feet, dy=-0.05)
        bend("Spine", Vector((1, 0, 0)), 32)                                # 크게 숙여요
        bend("Spine", UP, 8 if lead == "Left" else -8)
        sword_up(0.03 if phase in (1, 3) else 0.0)
        sl = pb["LeftArm"].head.copy()
        swing = Vector((0.0, -0.45, -0.25)) if lead == "Right" else Vector((0.05, 0.35, -0.4))   # 왼팔은 반대 다리와 엇갈려 휘저어요
        arm("Left", sl + swing, Vector((1, 0.2, -0.6)))
    make_action("ChargeRun", [(0, 0), (4, 1), (8, 2), (12, 3), (16, 0)], run_pose)
    rig.animation_data.action = None
    reset_pose()

    # 내려찍기 (Slam, 돌진 끝 빈틈 동안): 0 양손으로 머리 위로 치켜든 자세 → 5 무릎을 굽히며 앞 땅으로 내려찍기 → 그대로 멈춤
    def slam_pose(stage):
        chest = None
        if stage == "up":
            lower_hips(-0.08, FEET)
            bend("Spine", Vector((1, 0, 0)), -8)
            chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
            offs = [Vector((x, y, z)) for x in (-0.15, 0.0) for y in (-0.15, 0.0, 0.1) for z in (0.45, 0.6)]
            fit_right(chest, offs, ALL_POLES, Vector((0, 0.6, 1)))          # 칼끝은 위·뒤로
        else:
            lower_hips(-0.3, FEET, dy=-0.1)
            bend("Spine", Vector((1, 0, 0)), 30)
            chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
            offs = [Vector((x, y, z)) for x in (-0.15, 0.0) for y in (-0.55, -0.4) for z in (-0.45, -0.3, -0.15)]
            fit_right(chest, offs, ALL_POLES, Vector((0, -1, -0.7)))        # 칼끝은 앞 땅으로
        left_on_hilt()
    make_action("Slam", [(0, "up"), (5, "down"), (20, "down")], slam_pose)
    rig.animation_data.action = None
    reset_pose()

# 붕산권 (Quake, 예고 0.8초 = 24프레임 동안 그대로 재생): 두 주먹을 머리 위로 치켜들었다가 24프레임에 무릎을 굽히며 땅을 내려쳐요
if "quake" in EXTRAS:
    def quake_pose(stage):
        if stage == "up":
            bend("Spine1", Vector((1, 0, 0)), -10)                          # 가슴을 젖혀요
            for s, sx in (("Left", -0.1), ("Right", 0.1)):
                arm(s, pb[f"{s}Arm"].head + Vector((sx, -0.08, 0.5)), Vector((-sx * 4, 0.3, 0.3)))
        elif stage == "slam":
            lower_hips(-0.3, FEET)
            bend("Spine", Vector((1, 0, 0)), 35)                            # 앞으로 숙여요
            for s, sx in (("Left", -0.12), ("Right", 0.12)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, Vector((sx * 0.6, sh.y - 0.6, 0.45)), Vector((-sx * 4, 0.3, 0.5)))   # 두 주먹을 모아 앞 땅으로
        else:
            guard()
    make_action("Quake", [(0, "guard"), (14, "up"), (19, "up"), (24, "slam"), (34, "slam"), (44, "guard")], quake_pose)
    rig.animation_data.action = None
    reset_pose()

# 옮겨 굽는 데 쓴 플레이어 뼈대·메시·동작은 지워요 (내보낼 때 같이 나가지 않게)
for o in player_objs:
    bpy.data.objects.remove(o, do_unlink=True)
for a in player_acts:
    bpy.data.actions.remove(a)
bpy.context.scene.frame_set(0)

bpy.ops.object.select_all(action='DESELECT')
for o in [rig, body] + weapon_objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = rig
out = os.path.join(out_dir, f"{NAME}_Rigged.fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=False, mesh_smooth_type='OFF', add_leaf_bones=False,
                         primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
                         bake_anim=len(bpy.data.actions) > 0, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='STRIP')
print("ENEMY exported", out, "bones", len(rig.data.bones), "faces", len(body.data.polygons), "weapons", WEAPONS)

if preview:
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x, sc.render.resolution_y = 600, 700
    sc.world = bpy.data.worlds.new("W")
    cd = bpy.data.cameras.new("C"); cd.type = 'ORTHO'; cd.ortho_scale = 2.4
    cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam); sc.camera = cam
    d = Vector((0.5, -1, 0.3)).normalized(); cam.location = Vector((0, 0, HEIGHT / 2)) + d * 6
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = preview; bpy.ops.render.render(write_still=True)
