# VARCO 3D로 만든 옷(Source~/Outfit_B.glb, 몸 없는 고스트 마네킹 A자 포즈)을 우리 몸에 맞춰 입혀요.
# animate_player.py가 A자 기본 자세(T자로 바꾸기 전)에서 불러요. 그다음 T자로 바꿀 때 몸과 같이 바뀌어요.
#   1) 크기·위치: 발바닥 = 0, 깃 윗단 = 목 높이, 허리 둘레 = 몸 허리 + 여유
#   2) 몸을 뚫고 들어간 곳은 몸 밖으로 밀어내요
#   3) 색으로 나눠요 (텍스처 색): 붉은색 → Cloth_Red, 흰색 → Cloth_Light, 나머지 → Cloth_Dark (Unity가 이름으로 재질을 골라요)
#   4) 뼈 가중치는 가까운 몸에서 옮겨 오고, 도포 아랫단은 골반 가중치를 섞어 다리 사이가 찢어지지 않게
import bpy, bmesh, math, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree

TARGET_TRIS = 16000
CLEARANCE = 0.006       # 몸 표면에서 최소 이만큼 띄워요
WAIST_EASE = 0.035      # 허리 둘레 여유 (반지름)


def _import(glb):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=glb)
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
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bm.to_mesh(obj.data); bm.free()
    return obj


def _face_colors(obj):
    """면마다 텍스처 색(면 가운데 UV)을 읽어요. 텍스처가 없으면 None."""
    me = obj.data
    img = None
    for m in me.materials:
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image:
                    img = n.image
                    break
        if img:
            break
    if img is None or not me.uv_layers:
        return None
    w, h = img.size
    px = img.pixels[:]
    uv = me.uv_layers.active.data
    cols = []
    for p in me.polygons:
        u = sum(uv[li].uv.x for li in p.loop_indices) / p.loop_total
        v = sum(uv[li].uv.y for li in p.loop_indices) / p.loop_total
        x = min(w - 1, max(0, int((u % 1.0) * w))); y = min(h - 1, max(0, int((v % 1.0) * h)))
        i = (y * w + x) * 4
        cols.append((px[i], px[i + 1], px[i + 2]))
    return cols


def _classify(c):
    r, g, b = c
    if r > 0.12 and r > g * 1.8 and r > b * 1.8:
        return "Red"
    if (r + g + b) / 3 > 0.45 and max(r, g, b) - min(r, g, b) < 0.18:
        return "Light"
    return "Dark"


def _xy_radius(obj_verts, z, band=0.02):
    pts = [v for v in obj_verts if abs(v.z - z) < band and abs(v.x) < 0.22]
    if not pts:
        return None
    return sum(math.hypot(p.x, p.y) for p in pts) / len(pts)


def fit(rig, parts, glb):
    scene = bpy.context.scene
    B = rig.data.bones
    outfit = _import(glb)
    me = outfit.data

    # 앞 방향: 몸은 -Y를 봐요. VARCO 결과도 같은 방향으로 들어와요 (미리보기로 확인)
    vs = [v.co for v in me.vertices]
    zmin = min(v.z for v in vs); zmax = max(v.z for v in vs)
    cx = (min(v.x for v in vs) + max(v.x for v in vs)) / 2
    cy = (min(v.y for v in vs) + max(v.y for v in vs)) / 2
    neck_z = (B["Neck"].head_local.z + B["Neck"].tail_local.z) / 2
    s = neck_z / (zmax - zmin)
    for v in me.vertices:
        v.co = Vector(((v.co.x - cx) * s, (v.co.y - cy) * s, (v.co.z - zmin) * s))

    # 허리 둘레 맞추기 (가로·앞뒤만 늘이고 줄여요)
    body_verts = [o.matrix_world @ v.co for o in parts if "Torso" in o.name for v in o.data.vertices]
    waist_z = B["Spine"].head_local.z
    body_r = _xy_radius(body_verts, waist_z)
    out_r = _xy_radius([v.co for v in me.vertices], waist_z)
    k = (body_r + WAIST_EASE) / out_r if body_r and out_r else 1.0
    k = max(0.8, min(1.25, k))
    for v in me.vertices:
        v.co.x *= k; v.co.y *= k
    # 윗몸 늘이기: 옷 어깨선이 몸 어깨보다 낮아서 목·어깨가 비어요. 허리 위만 위로 늘여 어깨선을 맞춰요
    body_sh = B["LeftUpperArm"].head_local.z + 0.04
    sh_pts = [v.co.z for v in me.vertices if 0.12 < abs(v.co.x) < 0.2 and abs(v.co.y) < 0.12]
    out_sh = max(sh_pts) if sh_pts else body_sh
    if out_sh < body_sh:
        f = (body_sh - waist_z) / max(0.05, out_sh - waist_z)
        f = min(f, 1.35)
        for v in me.vertices:
            if v.co.z > waist_z:
                v.co.z = waist_z + (v.co.z - waist_z) * f
        print("OUTFIT upper stretch %.3f (outfit shoulder %.3f -> %.3f)" % (f, out_sh, body_sh))
    print("OUTFIT scale %.3f waist k %.3f (body r %.3f, outfit r %.3f)" % (s, k, body_r or 0, out_r or 0))

    # 색 나누기 (줄이기 전에 읽어요)
    cols = _face_colors(outfit)
    if cols is not None:
        cls = [_classify(c) for c in cols]
        # 이웃 면 다수결로 3번 다듬어요 (먹색 천 위 밝은 반사·얼룩이 흰 조각으로 떨어지지 않게)
        nb = [[] for _ in me.polygons]
        by_edge = {}
        for p in me.polygons:
            for ek in p.edge_keys:
                by_edge.setdefault(ek, []).append(p.index)
        for fl in by_edge.values():
            for a_ in fl:
                nb[a_] += [b_ for b_ in fl if b_ != a_]
        for _ in range(3):
            new = []
            for i, c in enumerate(cls):
                votes = {}
                for j in nb[i]:
                    votes[cls[j]] = votes.get(cls[j], 0) + 1
                best = max(votes, key=votes.get) if votes else c
                new.append(best if votes.get(best, 0) >= 2 and votes.get(c, 0) <= 1 else c)
            cls = new
        a = me.attributes.new("cls", 'INT', 'FACE')
        a.data.foreach_set("value", [{"Dark": 0, "Light": 1, "Red": 2}[c] for c in cls])
        print("OUTFIT classes", {c: cls.count(c) for c in ("Dark", "Light", "Red")})

    # 폴리곤 줄이기
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    if tris > TARGET_TRIS:
        dec = outfit.modifiers.new("Dec", 'DECIMATE'); dec.ratio = TARGET_TRIS / tris
        dec.use_symmetry = False
        bpy.ops.object.select_all(action='DESELECT'); outfit.select_set(True); bpy.context.view_layer.objects.active = outfit
        bpy.ops.object.modifier_apply(modifier=dec.name)
        me = outfit.data

    # 몸 밖으로 밀어내기
    verts, polys, off = [], [], 0
    for o in parts:
        verts += [o.matrix_world @ v.co for v in o.data.vertices]
        polys += [[i + off for i in p.vertices] for p in o.data.polygons]
        off += len(o.data.vertices)
    body_bvh = BVHTree.FromPolygons(verts, polys)
    pushed = 0
    for _ in range(2):
        for v in me.vertices:
            loc, n, _, d = body_bvh.find_nearest(v.co)
            if loc is None:
                continue
            h = (v.co - loc).dot(n)
            if h < CLEARANCE and d < 0.08:
                v.co += n * (CLEARANCE - h); pushed += 1
    print("OUTFIT pushed", pushed)

    # 가중치: A자 자세에선 손이 도포 자락 옆에 있어서, 가까운 몸에서 그냥 옮기면 자락이 손을 따라가 찢어져요.
    # 소매(팔 뼈가 몸통·다리보다 가까운 점)는 팔·손에서만, 나머지는 몸통·다리·발에서만 옮겨요
    def seg_d(p, a_, b_):
        ab = b_ - a_
        t = max(0.0, min(1.0, (p - a_).dot(ab) / max(ab.length_squared, 1e-9)))
        return (p - (a_ + ab * t)).length
    arm_segs = [(B[f"{w}UpperArm"].head_local, B[f"{w}LowerArm"].tail_local) for w in ("Left", "Right")]
    spine = (Vector((0, 0, B["Hips"].head_local.z)), Vector((0, 0, B["Neck"].head_local.z)))
    leg_segs = [(B[f"{w}UpperLeg"].head_local, B[f"{w}LowerLeg"].tail_local) for w in ("Left", "Right")]
    # 몸통 반폭(높이별): 이보다 바깥 + 허리 위 = 소매. 넓게 늘어진 소매 자락도 팔을 따라가요
    torso = [o for o in parts if o.name.endswith("Torso")][0]
    tv = [torso.matrix_world @ v.co for v in torso.data.vertices]
    def half_w(z):
        xs = [abs(p.x) for p in tv if abs(p.z - z) < 0.025]
        return max(xs) if xs else 0.17
    hw = {}
    sleeve_v, rest_v = [], []
    waist = B["Spine"].head_local.z
    for v in me.vertices:
        zk = round(v.co.z, 2)
        if zk not in hw:
            hw[zk] = half_w(zk)
        da = min(seg_d(v.co, *sg) for sg in arm_segs)
        is_sleeve = (v.co.z > waist and (abs(v.co.x) > hw[zk] - 0.01 or da < 0.06)) or da < 0.05
        (sleeve_v if is_sleeve else rest_v).append(v.index)
    print("OUTFIT sleeve verts", len(sleeve_v), "rest", len(rest_v))

    def joined(names):
        objs = [o for o in parts if any(o.name.endswith(n) for n in names)]
        src = bpy.data.objects.new("_W", bpy.data.meshes.new("_W")); scene.collection.objects.link(src)
        bm = bmesh.new()
        for o in objs:
            t = o.data.copy(); t.transform(o.matrix_world); bm.from_mesh(t); bpy.data.meshes.remove(t)
        bm.to_mesh(src.data); bm.free()
        base = 0
        for o in objs:
            for v in o.data.vertices:
                for g in v.groups:
                    nm = o.vertex_groups[g.group].name
                    vg = src.vertex_groups.get(nm) or src.vertex_groups.new(name=nm)
                    vg.add([base + v.index], g.weight, 'REPLACE')
            base += len(o.data.vertices)
        return src

    # 1차: 뼈대 자동 가중치(Bone Heat)를 옷 자체에 계산해요. 옷 모양 그대로 뼈를 따라가서 소매 자락도 팔과 같이 움직여요
    bpy.ops.object.select_all(action='DESELECT')
    outfit.select_set(True); rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    for m in list(outfit.modifiers):
        outfit.modifiers.remove(m)
    outfit.parent = None
    deform = {b.name for b in rig.data.bones if b.use_deform}
    def has_w(v):
        return any(outfit.vertex_groups[g.group].name in deform and g.weight > 0.01 for g in v.groups)
    missing = [v.index for v in me.vertices if not has_w(v)]
    print("OUTFIT bone-heat unweighted", len(missing), "/", len(me.vertices))
    # 2차: 자동 가중치가 못 닿은 점만 몸에서 옮겨요 (소매는 팔에서, 나머지는 몸통·다리에서)
    if missing:
        arm_src = joined(["Arm_L", "Arm_R", "Hand_L", "Hand_R"])
        body_src = joined(["Torso", "Head", "Thigh_L", "Thigh_R", "Calf_L", "Calf_R", "Foot_L", "Foot_R"])
        for src in (arm_src, body_src):
            for g in src.vertex_groups:
                if g.name not in outfit.vertex_groups:
                    outfit.vertex_groups.new(name=g.name)
        ms = set(missing)
        outfit.vertex_groups.new(name="_sleeve").add([i for i in sleeve_v if i in ms] or [0], 1.0, 'REPLACE')
        outfit.vertex_groups.new(name="_rest").add([i for i in rest_v if i in ms] or [0], 1.0, 'REPLACE')
        for src, mask in ((arm_src, "_sleeve"), (body_src, "_rest")):
            mod = outfit.modifiers.new("W", 'DATA_TRANSFER')
            mod.object = src; mod.use_vert_data = True; mod.data_types_verts = {'VGROUP_WEIGHTS'}
            mod.vert_mapping = 'POLYINTERP_NEAREST'; mod.layers_vgroup_select_src = 'ALL'; mod.layers_vgroup_select_dst = 'NAME'
            mod.vertex_group = mask
            bpy.ops.object.select_all(action='DESELECT'); outfit.select_set(True); bpy.context.view_layer.objects.active = outfit
            bpy.ops.object.modifier_apply(modifier=mod.name)
            bpy.data.objects.remove(src, do_unlink=True)
        for nm in ("_sleeve", "_rest"):
            outfit.vertex_groups.remove(outfit.vertex_groups[nm])

    # 허리 아래 천은 팔·손 가중치를 빼요 (A자 자세에서 손이 자락 옆이라 자동 가중치가 섞여 자락이 튀어요)
    arm_names = {g.name for g in outfit.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Thumb", "Index", "Middle", "Ring", "Little", "Shoulder"))}
    cleaned = 0
    sleeve_set = set(sleeve_v)
    for v in me.vertices:
        if v.co.z >= waist_z or v.index in sleeve_set:   # 소매 아랫자락은 팔을 따라가야 해요
            continue
        bad = [g for g in v.groups if outfit.vertex_groups[g.group].name in arm_names and g.weight > 0]
        if not bad:
            continue
        cleaned += 1
        for g in bad:
            outfit.vertex_groups[g.group].remove([v.index])
        if not any(g.weight > 0.01 for g in v.groups):
            outfit.vertex_groups["Hips"].add([v.index], 1.0, 'REPLACE')
    print("OUTFIT cleaned arm weights below waist", cleaned)
    # VARCO 옷은 A자 자세에서 손목 쪽 소매 끝과 도포 옆자락이 한 덩어리로 붙어 있어요. 팔을 펴면 그 사이 면이 길게 늘어나요.
    # 아래팔·손을 주로 따라가는 점과 몸통·다리를 따라가는 점을 잇는 면을 잘라 떼어 내요
    def dominant(v):
        best = max(v.groups, key=lambda g: g.weight, default=None)
        return outfit.vertex_groups[best.group].name if best else ""
    fore = lambda n: any(k in n for k in ("LowerArm", "Hand", "Thumb", "Index", "Middle", "Ring", "Little"))
    kind = [1 if fore(dominant(v)) else (0 if dominant(v) not in arm_names else 2) for v in me.vertices]
    bm = bmesh.new(); bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    cut = [f for f in bm.faces if {kind[v.index] for v in f.verts} >= {0, 1}]
    bmesh.ops.delete(bm, geom=cut, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    dl = bm.verts.layers.deform.active
    kind_after = {}
    bm.verts.index_update()
    for v in bm.verts:
        best = max(v[dl].items(), key=lambda t: t[1], default=(None, 0))[0] if dl else None
        nm = outfit.vertex_groups[best].name if best is not None else ""
        kind_after[v.index] = 1 if fore(nm) else (0 if nm not in arm_names else 2)
    # 잘라 내고 남은 작은 조각 중 몸통·다리를 따라가는 것(손목에서 떨어져 허공에 남는 소매 끝)은 지워요
    bm.verts.ensure_lookup_table()
    seen, small = set(), []
    for v0 in bm.verts:
        if v0.index in seen:
            continue
        isl, stack = [], [v0]
        seen.add(v0.index)
        while stack:
            v = stack.pop(); isl.append(v)
            for e in v.link_edges:
                o = e.other_vert(v)
                if o.index not in seen:
                    seen.add(o.index); stack.append(o)
        if len(isl) < 300 and kind_after[isl[0].index] == 0 and abs(isl[0].co.x) > 0.2:
            small += isl
    bmesh.ops.delete(bm, geom=small, context='VERTS')
    print("OUTFIT removed loose scraps verts", len(small))
    bm.to_mesh(me); bm.free()
    print("OUTFIT cut fused cuff faces", len(cut))

    # 도포 아랫단(가랑이 아래, 다리 사이 공간에 걸친 천): 골반 가중치를 섞어 다리를 벌려도 안 찢어지게
    crotch_z = (B["LeftUpperLeg"].head_local.z + B["RightUpperLeg"].head_local.z) / 2 - 0.06
    knee_z = (B["LeftLowerLeg"].head_local.z + B["RightLowerLeg"].head_local.z) / 2
    legs = [B["LeftUpperLeg"].head_local, B["RightUpperLeg"].head_local]
    for v in me.vertices:
        if v.co.z >= crotch_z or v.co.z < knee_z - 0.1:
            continue
        # 다리에서 멀리 떨어진 천(치마 자락)일수록 골반 쪽으로
        dl = min(math.hypot(v.co.x - l.x, v.co.y - l.y) for l in legs)
        loose = max(0.0, min(1.0, (dl - 0.09) / 0.08))
        if loose <= 0:
            continue
        side = max(0.0, min(1.0, 0.5 + v.co.x / 0.16))
        for g in list(v.groups):
            outfit.vertex_groups[g.group].remove([v.index])
        t = min(1.0, (crotch_z - v.co.z) / max(0.01, crotch_z - knee_z))
        leg = 0.5 * t
        for nm, w in (("Hips", 1 - leg), ("LeftUpperLeg", leg * side), ("RightUpperLeg", leg * (1 - side))):
            if w > 0.001:
                outfit.vertex_groups[nm].add([v.index], w, 'REPLACE')

    # 색별로 나눠 이름 붙이기
    made = []
    if "cls" in me.attributes:
        bm = bmesh.new(); bm.from_mesh(me)
        layer = bm.faces.layers.int.get("cls")
        for idx, name in ((0, "Cloth_Dark_Outfit"), (1, "Cloth_Light_Outfit"), (2, "Cloth_Red_Outfit")):
            pbm = bm.copy()
            kill = [f for f in pbm.faces if f[pbm.faces.layers.int.get("cls")] != idx]
            bmesh.ops.delete(pbm, geom=kill, context='FACES')
            if not pbm.faces:
                pbm.free(); continue
            pme = bpy.data.meshes.new(name); pbm.to_mesh(pme); pbm.free()
            o = bpy.data.objects.new(name, pme); scene.collection.objects.link(o)
            for g in outfit.vertex_groups:
                o.vertex_groups.new(name=g.name)
            made.append(o)
        bm.free()
        # 나눈 조각에 원본 가중치를 옮겨요 (같은 자리 점이라 가장 가까운 점 그대로)
        for o in made:
            m = o.modifiers.new("W", 'DATA_TRANSFER')
            m.object = outfit; m.use_vert_data = True; m.data_types_verts = {'VGROUP_WEIGHTS'}
            m.vert_mapping = 'NEAREST'; m.layers_vgroup_select_src = 'ALL'; m.layers_vgroup_select_dst = 'NAME'
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier=m.name)
        bpy.data.objects.remove(outfit, do_unlink=True)
    else:
        outfit.name = "Cloth_Dark_Outfit"; made = [outfit]
    for o in made:
        o.data.materials.clear()
        o.data.shade_smooth()
        o.parent = rig
        o.modifiers.new("Armature", 'ARMATURE').object = rig
    print("OUTFIT pieces", [(o.name, len(o.data.polygons)) for o in made])
    return made
