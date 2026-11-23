# VARCO 3D로 만든 옷(Source~/Outfit_B.glb, 몸 없는 고스트 마네킹 A자 포즈)을 우리 몸에 맞춰 입혀요.
# animate_player.py가 A자 기본 자세(T자로 바꾸기 전)에서 불러요. 그다음 T자로 바꿀 때 몸과 같이 바뀌어요.
#   1) 크기·위치: 발바닥 = 0, 깃 윗단 = 목 높이, 허리 둘레 = 몸 허리 + 여유
#   2) 몸을 뚫고 들어간 곳은 몸 밖으로 밀어내요
#   3) 색으로 나눠요 (텍스처 색): 붉은색 → Cloth_Red, 흰색 → Cloth_Light, 나머지 → Cloth_Dark.
#      한 메시(Cloth_Outfit)에 재질 칸 셋(Dark · Light · Red 순)으로 나눠요. 세 메시로 자르면 색 경계에서 옷이 갈라져 외곽선이 끊겼어요
#   4) 뼈 가중치는 가까운 몸에서 옮겨 오고, 도포 아랫단은 골반 가중치를 섞어 다리 사이가 찢어지지 않게
import bpy, bmesh, math, os
from mathutils import Vector, Quaternion
from mathutils.bvhtree import BVHTree

TARGET_TRIS = 16000
CLEARANCE = 0.006       # 몸 표면에서 최소 이만큼 띄워요
HEM_SMOOTH_STEPS = 120  # 옷 끝(열린 가장자리) 다듬기 횟수 (주인공 정리 2차: 14로는 찢긴 톱니가 남았어요)
HEM_EAR_PASSES = 4      # 다듬기 전에 가장자리에서 삐죽 튀어나온 면(두 변 이상이 열린 면)을 잘라 내는 횟수
CUFF_BAND = 0.07        # 흰 안쪽 소매: 소매 끝에서 이 거리 안의 소매 면만 흰 띠로
LEG_WEIGHT_SMOOTH = 6   # 바지·아랫자락의 골반·다리 가중치 고르기 횟수
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


def _texture(obj):
    for m in obj.data.materials:
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image:
                    return n.image
    return None


def _face_classes(obj):
    """면마다 색 종류를 정해요: 면 가운데와 꼭짓점 쪽 네 곳(가운데로 70% 당긴 자리) 텍셀을 읽어 다수결.
    예전엔 가운데 텍셀 하나만 읽어서 무늬·반사가 걸린 면이 빨강·흰 조각으로 떨어졌어요. 텍스처가 없으면 None."""
    me = obj.data
    img = _texture(obj)
    if img is None or not me.uv_layers:
        return None
    w, h = img.size
    px = img.pixels[:]
    uv = me.uv_layers.active.data
    out = []
    for p in me.polygons:
        us = [(uv[li].uv.x, uv[li].uv.y) for li in p.loop_indices]
        cu = sum(u for u, _ in us) / len(us); cv = sum(v for _, v in us) / len(us)
        samples = [(cu, cv)] + [(cu + (u - cu) * 0.7, cv + (v - cv) * 0.7) for u, v in us[:4]]
        votes = {}
        for u, v in samples:
            x = min(w - 1, max(0, int((u % 1.0) * w))); y = min(h - 1, max(0, int((v % 1.0) * h)))
            i = (y * w + x) * 4
            c = _classify((px[i], px[i + 1], px[i + 2]))
            votes[c] = votes.get(c, 0) + 1
        out.append(max(votes, key=lambda k: (votes[k], k == "Dark")))   # 비기면 도포(Dark)
    return out


def _absorb_small(bm, layer, min_faces):
    """같은 색 면이 이어진 덩어리 중 min_faces보다 작은 것은 둘레에 가장 많은 색으로 바꿔요 (도포 위 빨강·흰 점)."""
    bm.faces.ensure_lookup_table()
    seen = set(); changed = 0
    for f0 in bm.faces:
        if f0.index in seen:
            continue
        c0 = f0[layer]; isl = [f0]; seen.add(f0.index); stack = [f0]
        while stack:
            f = stack.pop()
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in seen and g[layer] == c0:
                        seen.add(g.index); isl.append(g); stack.append(g)
        if len(isl) >= min_faces:
            continue
        votes = {}
        ids = {f.index for f in isl}
        for f in isl:
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in ids:
                        votes[g[layer]] = votes.get(g[layer], 0) + 1
        if votes:
            best = max(votes, key=votes.get)
            for f in isl:
                f[layer] = best
            changed += len(isl)
    return changed


def _red_only_at_belt(bm, layer, belt_z, band):
    """빨강은 허리띠(와 거기서 늘어진 띠 끝)만: 허리 높이 띠에 닿지 않는 빨강 덩어리는 도포(Dark)로 (등 뒤 자락의 붉은 얼룩)."""
    bm.faces.ensure_lookup_table()
    seen = set(); changed = 0
    for f0 in bm.faces:
        if f0.index in seen or f0[layer] != 2:
            continue
        isl = [f0]; seen.add(f0.index); stack = [f0]
        while stack:
            f = stack.pop()
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in seen and g[layer] == 2:
                        seen.add(g.index); isl.append(g); stack.append(g)
        if any(abs(f.calc_center_median().z - belt_z) < band for f in isl):
            continue
        for f in isl:
            f[layer] = 0
        changed += len(isl)
    return changed


def paint_tone_texture(obj, out_png):
    """VARCO 옷 텍스처에서 붓결 명암 그림을 만들어요 (대나무 줄기처럼 큰 덩어리 2~3톤, 잔무늬 없이).
    색은 재질(_BaseColor)이 정하고, 이 그림은 밝기만 곱해요 (회색, 1 근처). 원본이 그려 둔 주름 명암을 따라가요."""
    import numpy as np
    img = _texture(obj)
    if img is None:
        return False
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[..., :3]
    lum = a @ np.array([0.3, 0.59, 0.11], dtype=np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    red = (r > 0.12) & (r > g * 1.8) & (r > b * 1.8)
    light = ~red & (lum > 0.45) & (a.max(-1) - a.min(-1) < 0.18)
    rel = np.ones_like(lum)
    for m in (red, light, ~red & ~light):   # 색 종류마다 평균 밝기로 나눠 1 근처로
        if m.any():
            rel[m] = lum[m] / max(1e-3, float(np.median(lum[m])))
    def blur(x, s):
        ky = np.fft.fftfreq(x.shape[0])[:, None]; kx = np.fft.fftfreq(x.shape[1])[None, :]
        return np.real(np.fft.ifft2(np.fft.fft2(x) * np.exp(-2 * (np.pi * s) ** 2 * (kx ** 2 + ky ** 2))))
    sm = blur(np.clip(rel, 0.3, 1.8), 6.0)                      # 잔무늬를 지우고 큰 주름 명암만
    def step(x, e, k): return 1.0 / (1.0 + np.exp(-(x - e) / k))
    t = (step(sm, 0.85, 0.03) + step(sm, 1.12, 0.03)) / 2       # 어두움 · 중간 · 밝음 세 톤
    tone = 0.8 + 0.34 * t
    tone = blur(tone, 1.2)                                      # 톤 경계만 살짝 부드럽게
    out = np.ones((h, w, 4), dtype=np.float32)
    out[..., :3] = np.clip(tone, 0, 1)[..., None]
    im = bpy.data.images.new("ClothPaint", w, h, alpha=True)
    im.pixels = out.ravel()
    im.filepath_raw = out_png; im.file_format = 'PNG'; im.save()
    bpy.data.images.remove(im)
    return True


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


def fit(rig, parts, glb, paint_png=None):
    scene = bpy.context.scene
    # 주인공 정리 2차: 찢김 없는 새 옷(Outfit_C)은 단이 이미 곧아서, 찢긴 옷(Outfit_B)용 손질(톱니 자르기·많이 다듬기·흰 소매 띠·신발 한 색)은 건너뛰어요
    torn = "Outfit_C" not in os.path.basename(glb)
    hem_steps = HEM_SMOOTH_STEPS if torn else 14
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

    # 붓결 명암 그림 (옷 텍스처에서, 줄이기 전에)
    if paint_png:
        print("OUTFIT paint", paint_tone_texture(outfit, paint_png), paint_png)

    # 색 나누기 (줄이기 전에 읽어요)
    cls = _face_classes(outfit)
    if cls is not None:
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
        bm = bmesh.new(); bm.from_mesh(me)
        print("OUTFIT absorbed small color islands (full res)", _absorb_small(bm, bm.faces.layers.int.get("cls"), 120))
        bm.to_mesh(me); bm.free()
        print("OUTFIT classes", {c: cls.count(c) for c in ("Dark", "Light", "Red")})

    # 폴리곤 줄이기
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    if tris > TARGET_TRIS:
        dec = outfit.modifiers.new("Dec", 'DECIMATE'); dec.ratio = TARGET_TRIS / tris
        dec.use_symmetry = False
        bpy.ops.object.select_all(action='DESELECT'); outfit.select_set(True); bpy.context.view_layer.objects.active = outfit
        bpy.ops.object.modifier_apply(modifier=dec.name)
        me = outfit.data

    # 줄인 뒤 다시 한 번: 줄이며 생긴 작은 색 조각을 둘레 색으로
    if "cls" in me.attributes:
        bm = bmesh.new(); bm.from_mesh(me)
        lay = bm.faces.layers.int.get("cls")
        print("OUTFIT absorbed small color islands (decimated)", _absorb_small(bm, lay, 30))
        print("OUTFIT red away from belt -> dark", _red_only_at_belt(bm, lay, B["Spine"].head_local.z, 0.09))
        bm.to_mesh(me); bm.free()

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
        elif len(isl) < 40:   # 어디에도 붙지 않고 떠 있는 작은 조각 (단추·실밥·찢긴 끝)
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
        loose = max(0.0, min(1.0, (dl - 0.15) / 0.08))   # 8차: 0.09면 통 넓은 바지까지 골반을 따라가 다리를 내디디면 앞이 찢어져 보였어요 → 바지보다 바깥 자락만
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

    # 바지·아랫자락 가중치 고르기 (플레이테스트 8차: 비도를 던지며 다리를 벌리고 골반을 틀면 바지 엉덩이·가랑이가 찢어져 보였어요).
    #   이웃한 점이 왼다리 · 오른다리 · 골반에 딱 갈려 있어서 그 사이 면이 길게 늘어났어요. 허리 아래 점만, 골반·두 넓적다리 가중치를
    #   이웃 점 평균 쪽으로 여러 번 섞어 다리 사이를 부드럽게 이어요 (팔·소매 가중치는 건드리지 않아요)
    LEGW = ("Hips", "LeftUpperLeg", "RightUpperLeg")
    gi = {nm: outfit.vertex_groups[nm].index for nm in LEGW if nm in outfit.vertex_groups}
    nbrs = {v.index: [] for v in me.vertices}
    for e in me.edges:
        a_, b_ = e.vertices
        nbrs[a_].append(b_); nbrs[b_].append(a_)

    def legw(v):
        return {nm: next((g.weight for g in v.groups if g.group == i), 0.0) for nm, i in gi.items()}
    lower = [v.index for v in me.vertices if knee_z - 0.05 < v.co.z < waist_z - 0.04 and sum(legw(v).values()) > 0.5]
    for _ in range(LEG_WEIGHT_SMOOTH):
        cur = {i: legw(me.vertices[i]) for i in set(lower) | {n for i in lower for n in nbrs[i]}}
        for i in lower:
            ns = nbrs[i]
            if not ns:
                continue
            tot = sum(cur[i].values())
            mix = {nm: cur[i][nm] * 0.5 + 0.5 * sum(cur[n][nm] for n in ns) / len(ns) for nm in gi}
            s = sum(mix.values()) or 1.0
            for nm, w in mix.items():
                outfit.vertex_groups[nm].add([i], w * tot / s, 'REPLACE')
    print("OUTFIT smoothed leg weights verts", len(lower))

    # 옷 끝 다듬기 (플레이테스트 8차: 도포 아랫단·소매 끝이 뾰족뾰족 튀어나와 지저분했어요). 가중치를 다 정한 뒤에 해요 (먼저 하면 소매 끝 점이 몸통 가중치를 받아 길게 늘어났어요)
    #   열린 가장자리(천 끝) 점만 양옆 가장자리 점의 가운데로 조금씩 당기기를 되풀이해요 → 삐죽한 끝이 줄어들고 단이 매끈한 곡선이 돼요
    import bmesh as _bm
    bm = _bm.new(); bm.from_mesh(me)
    # 주인공 정리 2차: 찢긴 끝(톱니)부터 잘라요 — 가장자리 변이 둘 이상인 면 = 밖으로 삐죽 튀어나온 조각
    ears = 0
    for _ in range(HEM_EAR_PASSES if torn else 0):
        cut = [f for f in bm.faces if sum(1 for e in f.edges if e.is_boundary) >= 2]
        if not cut:
            break
        ears += len(cut)
        bmesh.ops.delete(bm, geom=cut, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    print("OUTFIT trimmed hem spikes faces", ears)
    # 발목 아래(신발)는 한 가지 색으로: 흰 버선·끈 조각이 얼룩덜룩해서 짙은 천신 한 덩어리로 (지우면 맨발이 돼요)
    ankle_z = max(B["LeftFoot"].head_local.z, B["RightFoot"].head_local.z) + 0.01
    clay = bm.faces.layers.int.get("cls")
    low = [f for f in bm.faces if f.calc_center_median().z < ankle_z]
    if clay is not None and torn:
        for f in low:
            f[clay] = 0
    print("OUTFIT shoes one colour faces", len(low))
    edge_verts = [v for v in bm.verts if v.is_boundary]
    for _ in range(hem_steps):
        moved = {}
        for v in edge_verts:
            nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
            if len(nb) == 2:
                moved[v] = v.co.lerp((nb[0].co + nb[1].co) * 0.5, 0.5)
        for v, c in moved.items():
            v.co = c
    # 흰 안쪽 소매: 찢긴 조각처럼 흩어진 흰 면 대신, 소매 끝(손목 쪽 열린 가장자리)에서 CUFF_BAND 안의 소매 면만 고른 흰 띠로
    lay = bm.faces.layers.int.get("cls")
    dl = bm.verts.layers.deform.active
    if lay is not None and dl is not None and not torn:
        # 깔끔한 옷: 흰색은 소매(팔 뼈를 따라가는 면) · 깃 · 버선(발목 아래)에만. 등·어깨의 밝은 천 결이 흰 조각으로 떨어진 것은 도포로
        arm_ids = {g.index for g in outfit.vertex_groups if any(k in g.name for k in ("UpperArm", "LowerArm", "Hand"))}
        def dom(v):
            items = v[dl].items()
            return max(items, key=lambda t: t[1])[0] if items else -1
        neck = B["Neck"].head_local
        fixed = 0
        for f in bm.faces:
            if f[lay] != 1:
                continue
            z = f.calc_center_median().z
            c = f.calc_center_median()
            collar = z > neck.z - 0.25 and math.hypot(c.x - neck.x, c.y - neck.y) < 0.1   # 목 둘레 깃 (등·어깨의 밝은 결은 아니에요)
            if z < ankle_z + 0.04 or collar or all(dom(v) in arm_ids for v in f.verts):
                continue
            f[lay] = 0; fixed += 1
        print("OUTFIT stray white -> dark", fixed)
    if lay is not None and dl is not None and torn:
        fore_ids = {g.index for g in outfit.vertex_groups if any(k in g.name for k in ("LowerArm", "Hand"))}
        arm_ids = {g.index for g in outfit.vertex_groups if any(k in g.name for k in ("UpperArm", "LowerArm", "Hand", "Shoulder"))}
        def dom(v):
            items = v[dl].items()
            return max(items, key=lambda t: t[1])[0] if items else -1
        cuff = [v.co.copy() for v in bm.verts if v.is_boundary and dom(v) in fore_ids]
        band = 0
        for f in bm.faces:
            if not all(dom(v) in arm_ids for v in f.verts):
                if f[lay] == 1 and f.calc_center_median().z < B["Neck"].head_local.z - 0.12:   # 깃 아래 몸통의 흰 조각도 도포로
                    f[lay] = 0
                continue
            c = f.calc_center_median()
            near = any((c - q).length < CUFF_BAND for q in cuff)
            if near and f[lay] != 2:
                f[lay] = 1; band += 1
            elif f[lay] == 1:
                f[lay] = 0
        print("OUTFIT cuff band faces", band, "cuff verts", len(cuff))
    bm.to_mesh(me); bm.free()
    print("OUTFIT smoothed hem verts", len(edge_verts))


    # 한 메시에 색별 재질 칸 셋 (Dark · Light · Red 순). 정점을 같이 써서 색 경계에서도 법선이 이어져 외곽선이 갈라지지 않아요
    outfit.name = "Cloth_Outfit"; outfit.data.name = "Cloth_Outfit"
    outfit.data.materials.clear()
    for nm in ("Cloth_Dark", "Cloth_Light", "Cloth_Red"):
        outfit.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
    if "cls" in me.attributes:
        vals = [0] * len(me.polygons)
        me.attributes["cls"].data.foreach_get("value", vals)
        me.polygons.foreach_set("material_index", vals)
        # 면을 재질 칸 순서로 정렬해요. FBX를 가져온 Unity는 서브메시를 면이 처음 나오는 순서로 매겨서, 빨간 면이 먼저면 도포가 빨갛게 칠해졌어요
        bpy.ops.object.select_all(action='DESELECT'); outfit.select_set(True); bpy.context.view_layer.objects.active = outfit
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.sort_elements(type='MATERIAL', elements={'FACE'})
        bpy.ops.object.mode_set(mode='OBJECT')
        print("OUTFIT faces by slot", {i: vals.count(i) for i in range(3)})
    made = [outfit]
    for o in made:
        o.data.shade_smooth()
        o.parent = rig
        o.modifiers.new("Armature", 'ARMATURE').object = rig
    print("OUTFIT pieces", [(o.name, len(o.data.polygons)) for o in made])
    hide_covered_skin(parts, made)
    return made


COVER_REACH = 0.15      # 피부에서 바깥으로 이 안에 옷이 있으면 덮인 곳
SKIN_PARTS = ("Torso", "Arm_", "Thigh_", "Calf_")   # 머리·손·발은 드러나는 곳이라 그대로


def hide_covered_skin(parts, cloth):
    """옷에 덮인 피부를 지워요. 움직일 때 몸이 옷을 뚫고 나와 어깨·허벅지·종아리 살이 보이지 않게.
    피부 점마다 바깥(법선) 쪽으로 광선을 쏴서 COVER_REACH 안에 옷이 있으면 덮인 점이에요.
    목·손목처럼 옷 밖으로 드러난 곳은 광선이 옷에 안 닿아서 남아요. 기본 자세(A자)에서 재요."""
    verts, polys = [], []
    for o in cloth:
        base = len(verts)
        verts += [o.matrix_world @ v.co for v in o.data.vertices]
        polys += [[base + i for i in p.vertices] for p in o.data.polygons]
    if not polys:
        return
    bvh = BVHTree.FromPolygons(verts, polys)
    for o in parts:
        name = o.name.replace("Player_", "")
        if not name.startswith(SKIN_PARTS):
            continue
        n3 = o.matrix_world.to_3x3()
        bm = bmesh.new(); bm.from_mesh(o.data)
        bm.normal_update()
        covered = []
        for v in bm.verts:
            p = o.matrix_world @ v.co
            nrm = (n3 @ v.normal).normalized()
            # 법선 한 줄과 둘레로 35° 기운 여섯 줄: 옷의 작은 구멍·깃 틈은 한 줄로는 빠져나가서, 절반 넘게 옷에 닿으면 덮인 점
            side = nrm.orthogonal().normalized()
            rays = [nrm] + [(nrm + (Quaternion(nrm, k * math.pi / 3) @ side) * 0.7).normalized() for k in range(6)]
            hits = sum(1 for d in rays if bvh.ray_cast(p + nrm * 0.002, d, COVER_REACH)[0] is not None)
            # 피부가 옷 밖으로 튀어나온 점: 바로 안쪽(3cm 안)에 옷이 있어요 (어깨 위처럼 옷이 몸에 딱 붙는 곳)
            poking = bvh.ray_cast(p - nrm * 0.002, -nrm, 0.03)[0] is not None
            if hits * 2 > len(rays) or poking:
                covered.append(v)
        bmesh.ops.delete(bm, geom=covered, context='VERTS')
        bm.to_mesh(o.data); bm.free()
        print("SKIN hidden", o.name, len(covered))
