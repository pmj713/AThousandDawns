# 플레이어 옷 (컨셉 B안): 뼈를 따라 둥근 단면(고리)을 이어 붙여 만들어요. animate_player.py가 T자 기본 자세로 바꾼 뒤 불러요.
#   고리 크기 = 몸 표면까지 거리(바깥에서 안쪽으로 쏴서 잼) + 옷 여유 → 몸에 맞게 붙어요
#   뼈 가중치는 몸에서 옮겨 와서 지금 동작을 그대로 써요. 도포 아랫단은 골반·허벅지 가중치를 섞어 다리 사이가 안 찢어져요
# 재질 묶음(Unity가 이름으로 재질을 골라요): Cloth_Dark(먹색 도포·바지·각반·감개·짚신), Cloth_Light(흰 속소매·깃), Cloth_Red(허리띠·머리끈)
import bpy, bmesh, math, random
from mathutils import Vector
from mathutils.bvhtree import BVHTree

Z = Vector((0, 0, 1))
ANGLES = 24
import os as _os
DEBUG = _os.environ.get("CLOTHES_DEBUG") == "1"
rng = random.Random(7)


_SEAM = set()   # 파츠 경계 점 자리 (여러 파츠에 같은 자리로 있는 점). build()가 채워요


def _key(co):
    return (round(co.x, 4), round(co.y, 4), round(co.z, 4))


def _bvh(objs):
    """몸 표면 BVH. 파츠를 자른 자리를 막은 뚜껑 면(가운데 점의 이웃이 전부 경계 점인 부채꼴)은 빼요 —
    뚜껑이 팔다리 안쪽을 비스듬히 가로질러서, 표면까지 거리를 잴 때 엉뚱하게 부딪혀요."""
    verts, polys, off = [], [], 0
    for o in objs:
        m = o.matrix_world
        me = o.data
        co = [m @ v.co for v in me.vertices]
        seam = [_key(c) in _SEAM for c in co]
        nbr = [[] for _ in me.vertices]
        for e in me.edges:
            a, b = e.vertices
            nbr[a].append(b); nbr[b].append(a)
        cap_center = [not seam[i] and len(nbr[i]) >= 3 and all(seam[j] for j in nbr[i]) for i in range(len(co))]
        verts += co
        polys += [[i + off for i in p.vertices] for p in me.polygons if not any(cap_center[i] for i in p.vertices)]
        off += len(co)
    tree = BVHTree.FromPolygons(verts, polys)
    _VERTS[id(tree)] = verts
    return tree


_VERTS = {}   # BVH → 몸 표면 점들 (단면 가운데 찾기용)


def _basis(a):
    ref = Z if abs(a.dot(Z)) < 0.9 else Vector((0, -1, 0))
    u = (ref - a * a.dot(ref)).normalized()
    return u, a.cross(u).normalized()


def _rings(bvh, centers, offsets, reach=0.45, flare=None, keep_widening=False, clamp=1.35, center_fit=True):
    """centers[i]에서 둘레 ANGLES방향으로 몸 바깥 표면까지 반지름을 재요. offsets[i] = 옷 여유.
    flare[i] = 추가로 넓힐 양, keep_widening = 아래로 갈수록 좁아지지 않게(치마)."""
    orig = [c.copy() for c in centers]
    axes = [(orig[min(i + 1, len(orig) - 1)] - orig[max(i - 1, 0)]).normalized() for i in range(len(orig))]
    # 1) 단면 가운데 찾기: 뼈 축이 팔다리 가운데를 지나지 않아요 (팔은 뼈가 윗면 쪽). 고리 높이 근처 몸 점들의 가운데로
    if center_fit:
        pts_all = _VERTS.get(id(bvh), [])
        for i, a in enumerate(axes):
            c = orig[i]
            for _ in range(2):
                near = [p for p in pts_all if abs((p - c).dot(a)) < 0.02 and ((p - c) - a * (p - c).dot(a)).length < reach]
                if len(near) < 4:
                    break
                mid = sum(near, Vector()) / len(near)
                c = c + (mid - c) - a * (mid - c).dot(a)   # 축 방향은 그대로, 단면 안에서만 옮겨요
            centers[i] = c
        sm = [c.copy() for c in centers]                    # 고리 가운데가 들쭉날쭉하지 않게 이웃과 평균
        for i in range(len(centers)):
            centers[i] = (sm[max(i - 1, 0)] + sm[i] * 2 + sm[min(i + 1, len(sm) - 1)]) / 4
    # 2) 둘레 반지름: 바깥에서 안쪽으로 쏴 몸 표면까지. 못 맞으면 이전 고리의 "잰 값"을 써요 (여유를 겹쳐 더하지 않게)
    rings, prev, prev_raw = [], None, None
    for i, c in enumerate(centers):
        a = axes[i]
        u, v = _basis(a)
        raw = []
        for k in range(ANGLES):
            th = 2 * math.pi * k / ANGLES
            d = u * math.cos(th) + v * math.sin(th)
            hit = bvh.ray_cast(c + d * reach, -d, reach)[0]
            raw.append((hit - c).length if hit is not None else (prev_raw[k] if prev_raw else None))
        known = [r for r in raw if r is not None]
        fill = sorted(known)[len(known) // 2] if known else 0.08
        raw = [r if r is not None else fill for r in raw]
        med = sorted(raw)[ANGLES // 2]
        raw = [min(r, med * clamp) for r in raw]   # 겨드랑이처럼 옆으로 붙은 덩어리는 무시
        raw = [(raw[k - 1] + 2 * raw[k] + raw[(k + 1) % ANGLES]) / 4 for k in range(ANGLES)]   # 울퉁불퉁 다듬기
        prev_raw = raw
        radii = [r + offsets[i] + (flare[i] if flare else 0.0) for r in raw]
        if keep_widening and prev:
            radii = [max(r, p) for r, p in zip(radii, prev)]
        prev = radii
        if DEBUG:
            print("RINGDBG c(%.2f,%.2f,%.2f) r %.3f..%.3f med %.3f" % (c.x, c.y, c.z, min(radii), max(radii), med))
        pts = []
        for k in range(ANGLES):
            th = 2 * math.pi * k / ANGLES
            d = u * math.cos(th) + v * math.sin(th)
            pts.append(c + d * radii[k])
        rings.append(pts)
    return rings


def _tube(bm, rings, jitter_last=0.0):
    vs = [[bm.verts.new(p) for p in ring] for ring in rings]
    if jitter_last > 0:   # 해진 아랫단: 끝 고리를 들쭉날쭉하게
        for v in vs[-1]:
            v.co.z += rng.uniform(-jitter_last, jitter_last * 0.3)
    for a, b in zip(vs[:-1], vs[1:]):
        for k in range(ANGLES):
            j = (k + 1) % ANGLES
            bm.faces.new((a[k], a[j], b[j], b[k]))
    return vs


def _strip(bm, pts, width, side):
    """리본·끈: 점들을 따라 폭 width의 납작한 띠 (side = 폭 방향)."""
    prev = None
    for i, p in enumerate(pts):
        w = width * (1.0 - 0.35 * i / max(1, len(pts) - 1))
        pair = (bm.verts.new(p - side * w / 2), bm.verts.new(p + side * w / 2))
        if prev:
            bm.faces.new((prev[0], prev[1], pair[1], pair[0]))
        prev = pair


def _line(a, b, n):
    return [a.lerp(b, i / (n - 1)) for i in range(n)]


def _finish(name, bm, scene, thickness=0.004):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if thickness > 0:   # 얇은 두께: 소매·치마 안쪽에서 봐도 뚫려 보이지 않게
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=thickness)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    me.shade_smooth()
    obj = bpy.data.objects.new(name, me)
    scene.collection.objects.link(obj)
    return obj


def _transfer_weights(obj, body_src):
    """몸(파츠를 합친 복사본)에서 가장 가까운 면의 뼈 가중치를 옮겨 와요."""
    for g in body_src.vertex_groups:
        if g.name not in obj.vertex_groups:
            obj.vertex_groups.new(name=g.name)
    mod = obj.modifiers.new("Weights", 'DATA_TRANSFER')
    mod.object = body_src
    mod.use_vert_data = True
    mod.data_types_verts = {'VGROUP_WEIGHTS'}
    mod.vert_mapping = 'POLYINTERP_NEAREST'
    mod.layers_vgroup_select_src = 'ALL'
    mod.layers_vgroup_select_dst = 'NAME'
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)


def _set_weights(obj, fn):
    """fn(좌표) → {뼈 이름: 가중치}로 직접 정해요."""
    for v in obj.data.vertices:
        for g in list(v.groups):
            obj.vertex_groups[g.group].remove([v.index])
        for name, w in fn(v.co).items():
            if w <= 0:
                continue
            vg = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
            vg.add([v.index], w, 'REPLACE')


def build(rig, parts, hair):
    scene = bpy.context.scene
    B = rig.data.bones
    P = {o.name.replace("Player_", ""): o for o in parts}
    H = lambda n: B[n].head_local.copy()
    T = lambda n: B[n].tail_local.copy()

    counts = {}
    for o in parts:
        for k in {_key(o.matrix_world @ v.co) for v in o.data.vertices}:
            counts[k] = counts.get(k, 0) + 1
    _SEAM.clear()
    _SEAM.update(k for k, n in counts.items() if n >= 2)
    torso_bvh = _bvh([P["Torso"]])
    legs_bvh = _bvh([P["Thigh_L"], P["Thigh_R"], P["Calf_L"], P["Calf_R"], P["Torso"]])
    hips = H("Hips")
    neck = H("Neck")
    crotch_z = (H("LeftUpperLeg").z + H("RightUpperLeg").z) / 2 - 0.08
    knee_z = (H("LeftLowerLeg").z + H("RightLowerLeg").z) / 2
    made = []

    # 가중치 옮기기 원본: 옷마다 덮는 몸 파츠만 합쳐 써요 (소매는 팔만 → 팔을 내리면 소매도 같이 내려와요)
    temps = []

    def src_of(*names):
        objs = [P[n] for n in names]
        src = bpy.data.objects.new("_W_" + "_".join(names), bpy.data.meshes.new("_W"))
        scene.collection.objects.link(src)
        bm_ = bmesh.new()
        for o in objs:
            tmp = o.data.copy(); tmp.transform(o.matrix_world); bm_.from_mesh(tmp); bpy.data.meshes.remove(tmp)
        bm_.to_mesh(src.data); bm_.free()
        base = 0
        for o in objs:
            for v in o.data.vertices:
                for g in v.groups:
                    gname = o.vertex_groups[g.group].name
                    vg = src.vertex_groups.get(gname) or src.vertex_groups.new(name=gname)
                    vg.add([base + v.index], g.weight, 'REPLACE')
            base += len(o.data.vertices)
        temps.append(src)
        return src

    # ── 먹색 도포: 몸통(목 아래 ~ 골반) + 아랫단(무릎까지 퍼지며 해짐)
    bm = bmesh.new()
    top_z, waist_z = neck.z - 0.02, hips.z - 0.02
    upper = [Vector((0, 0, z)) for z in _frange(top_z, waist_z, 16)]
    up_r = _rings(torso_bvh, upper, [0.016] * len(upper), reach=0.4, clamp=3.0, center_fit=False)
    skirt_c = [Vector((0, 0, z)) for z in _frange(waist_z, knee_z - 0.06, 12)]
    flare = [0.01 + 0.03 * (i / 11) ** 1.3 for i in range(12)]
    sk_r = _rings(legs_bvh, skirt_c, [0.012] * 12, reach=0.45, flare=flare, keep_widening=True, clamp=3.0, center_fit=False)
    _tube(bm, up_r[:-1] + sk_r, jitter_last=0.05)
    robe = _finish("Cloth_Dark_Robe", bm, scene)
    _transfer_weights(robe, src_of("Torso", "Thigh_L", "Thigh_R"))

    def skirt_w(co):
        # 골반 아래: 다리 따라 너무 늘어나지 않게 골반 가중치를 섞고, 좌우는 부드럽게 나눠요
        if co.z >= crotch_z + 0.05:
            return None
        t = min(1.0, (crotch_z + 0.05 - co.z) / max(0.01, crotch_z + 0.05 - (knee_z - 0.06)))
        side = max(0.0, min(1.0, 0.5 + co.x / 0.16))      # +X = 왼쪽
        leg = 0.55 * t
        return {"Hips": 1.0 - leg, "LeftUpperLeg": leg * side, "RightUpperLeg": leg * (1 - side)}
    for v in robe.data.vertices:
        w = skirt_w(v.co)
        if w is None:
            continue
        for g in list(v.groups):
            robe.vertex_groups[g.group].remove([v.index])
        for name, val in w.items():
            if val > 0.001:
                robe.vertex_groups[name].add([v.index], val, 'REPLACE')
    made.append(robe)

    # ── 소매(먹색, 어깨~팔꿈치 아래로 넓게) + 흰 속소매(아래팔) + 아래팔 감개(먹색)
    for side, word in (("L", "Left"), ("R", "Right")):
        arm_bvh = _bvh([P[f"Arm_{side}"]])
        sh, el, wr = H(f"{word}UpperArm"), H(f"{word}LowerArm"), H(f"{word}Hand")
        cs = _line(sh.lerp(el, 0.05), el.lerp(wr, 0.15), 10)
        sleeve = _rings(arm_bvh, cs, [0.016] * 10, reach=0.25, flare=[0.03 * (i / 9) ** 1.5 for i in range(10)])
        bm = bmesh.new(); _tube(bm, sleeve, jitter_last=0.02)
        arm_src = src_of(f"Arm_{side}")
        o = _finish(f"Cloth_Dark_Sleeve_{side}", bm, scene); _transfer_weights(o, arm_src); made.append(o)
        cs = _line(el.lerp(wr, 0.05), el.lerp(wr, 0.55), 6)
        inner = _rings(arm_bvh, cs, [0.012] * 6, reach=0.2)
        bm = bmesh.new(); _tube(bm, inner)
        o = _finish(f"Cloth_Light_Sleeve_{side}", bm, scene); _transfer_weights(o, arm_src); made.append(o)
        cs = _line(el.lerp(wr, 0.5), wr.lerp(T(f"{word}Hand"), 0.08), 7)
        wrap = _rings(arm_bvh, cs, [0.007] * 7, reach=0.2)
        bm = bmesh.new(); _tube(bm, wrap)
        o = _finish(f"Cloth_Dark_Wrap_{side}", bm, scene, thickness=0.0); _transfer_weights(o, src_of(f"Arm_{side}", f"Hand_{side}")); made.append(o)

    # ── 바지(헐렁하게, 무릎까지) + 각반(무릎~발목, 몸에 붙게) + 짚신
    for side, word in (("L", "Left"), ("R", "Right")):
        leg_bvh = _bvh([P[f"Thigh_{side}"], P[f"Calf_{side}"]])
        hip, knee, ankle = H(f"{word}UpperLeg"), H(f"{word}LowerLeg"), H(f"{word}Foot")
        cs = _line(hip.lerp(knee, 0.1), knee.lerp(ankle, 0.15), 10)
        pants = _rings(leg_bvh, cs, [0.03 + 0.012 * math.sin(math.pi * i / 9) for i in range(10)], reach=0.3)
        bm = bmesh.new(); _tube(bm, pants)
        leg_src = src_of(f"Thigh_{side}", f"Calf_{side}")
        o = _finish(f"Cloth_Dark_Pants_{side}", bm, scene); _transfer_weights(o, leg_src); made.append(o)
        cs = _line(knee.lerp(ankle, 0.12), ankle.lerp(knee, 0.03), 8)
        wrap = _rings(leg_bvh, cs, [0.012] * 8, reach=0.25)
        bm = bmesh.new(); _tube(bm, wrap)
        o = _finish(f"Cloth_Dark_LegWrap_{side}", bm, scene, thickness=0.0); _transfer_weights(o, leg_src); made.append(o)
        foot_bvh = _bvh([P[f"Foot_{side}"]])
        toe = T(f"{word}Foot")
        cs = _line(ankle + Vector((0, 0, -0.01)), toe + (toe - ankle).normalized() * 0.04, 7)
        shoe = _rings(foot_bvh, cs, [0.006] * 7, reach=0.2)
        bm = bmesh.new(); _tube(bm, shoe)
        o = _finish(f"Cloth_Dark_Shoe_{side}", bm, scene); _transfer_weights(o, src_of(f"Foot_{side}", f"Calf_{side}")); made.append(o)

    # ── 흰 깃 (목에 붙는 좁은 띠: 도포 윗단 위로 살짝)
    neck_bvh = _bvh([P["Head"], P["Torso"]])
    head_z = H("Head").z
    cs = [Vector((0, 0, z)) for z in _frange(head_z - 0.03, head_z - 0.075, 3)]   # 목 (어깨선 위)
    collar = _rings(neck_bvh, cs, [0.010, 0.012, 0.015], reach=0.11)
    bm = bmesh.new(); _tube(bm, collar)
    o = _finish("Cloth_Light_Collar", bm, scene); _transfer_weights(o, src_of("Head", "Torso")); made.append(o)

    # ── 붉은 허리띠: 허리 띠 + 왼쪽 앞으로 늘어진 매듭 끈 두 가닥
    sash_z = hips.z + 0.06
    cs = [Vector((0, 0, z)) for z in _frange(sash_z + 0.045, sash_z - 0.045, 3)]
    band = _rings(torso_bvh, cs, [0.032] * 3, reach=0.4, clamp=3.0, center_fit=False)
    bm = bmesh.new(); _tube(bm, band)
    knot = Vector((0.07, -0.13, sash_z))          # 몸 왼쪽 앞 (+X = 왼쪽, -Y = 앞)
    for dx, length in ((0.0, 0.42), (0.05, 0.34)):
        pts = [knot + Vector((dx + 0.02 * i / 7, -0.015 * math.sin(i), -length * i / 7)) for i in range(8)]
        _strip(bm, pts, 0.07, Vector((1, 0, 0)))
    sash = _finish("Cloth_Red_Sash", bm, scene)
    _set_weights(sash, lambda co: {"Hips": 1.0})
    made.append(sash)

    # ── 붉은 머리끈: 상투 밑동 고리 + 뒤로 흩날리는 긴 끈 두 가닥 (머리 뼈에 붙음)
    hv = [hair.matrix_world @ v.co for v in hair.data.vertices]
    top = max(hv, key=lambda c: c.z)
    bun = [c for c in hv if c.z > top.z - 0.06]
    bc = sum(bun, Vector()) / len(bun)
    ring_c = Vector((bc.x, bc.y, top.z - 0.075))
    bm = bmesh.new()
    rr = 0.034
    ring = [[ring_c + Vector((math.cos(2 * math.pi * k / ANGLES) * rr, math.sin(2 * math.pi * k / ANGLES) * rr, dz)) for k in range(ANGLES)]
            for dz in (0.012, -0.012)]
    _tube(bm, ring)
    for i, (dx, length) in enumerate(((0.03, 0.62), (-0.02, 0.48))):
        pts = []
        for s in range(10):
            f = s / 9
            # 상투 뒤로 늘어져 어깨 뒤까지 흘러내려요 (+Y = 뒤)
            pts.append(ring_c + Vector((dx + 0.06 * f + 0.02 * math.sin(f * 6 + i), 0.05 + 0.16 * f, -0.01 - length * f ** 1.2)))
        _strip(bm, pts, 0.05, Vector((0, 0, 1)))
    ribbon = _finish("Cloth_Red_Ribbon", bm, scene)
    _set_weights(ribbon, lambda co: {"Head": 1.0})
    made.append(ribbon)

    for src in temps:
        bpy.data.objects.remove(src, do_unlink=True)
    for o in made:
        o.parent = rig
        o.modifiers.new("Armature", 'ARMATURE').object = rig
    print("CLOTHES", len(made), "pieces,", sum(len(o.data.polygons) for o in made), "faces")
    return made


def _frange(a, b, n):
    return [a + (b - a) * i / (n - 1) for i in range(n)]
