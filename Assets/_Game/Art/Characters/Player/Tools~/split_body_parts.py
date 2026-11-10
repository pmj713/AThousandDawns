# Blender(5.x) 백그라운드 스크립트: VARCO에서 만든 맨몸 기본 바디(GLB, A자 포즈)를 파츠로 나눠 FBX로 내보내요.
#   blender --background --factory-startup --python split_body_parts.py -- <input.glb> <output_dir> [preview.png]
# 파츠: Head(머리-목), Torso(몸통), Arm_L/R(손목~어깨), Hand_L/R, Thigh_L/R, Calf_L/R, Foot_L/R
# 모든 파츠의 기준점은 캐릭터 발밑 원점이라 Unity에서 같은 자리에 두면 그대로 조립돼요.
import bpy, bmesh, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_dir = argv[0], argv[1]
preview = argv[2] if len(argv) > 2 else None
HEIGHT = 1.85  # 게임 키 (플레이어 캡슐 2 안에 맞게)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in bpy.context.scene.objects:
    o.select_set(o.type == 'MESH')
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in [o for o in bpy.context.scene.objects if o != body]:
    bpy.data.objects.remove(o, do_unlink=True)

# glTF는 경계마다 점을 나눠 저장해서, 겹친 점을 먼저 하나로 합쳐요 (안 합치면 줄일 때 틈이 생기고 단면 찾기가 안 돼요)
_bm = bmesh.new(); _bm.from_mesh(body.data)
bmesh.ops.remove_doubles(_bm, verts=_bm.verts, dist=1e-4)
_bm.to_mesh(body.data); _bm.free()

# 키 1.85, 발바닥 z=0, 몸 가운데 x=y=0
me = body.data
zs = [v.co.z for v in me.vertices]
s = HEIGHT / (max(zs) - min(zs))
for v in me.vertices:
    v.co *= s
xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
offset = Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, min(zs)))
for v in me.vertices:
    v.co -= offset
H = HEIGHT

# 폴리곤 줄이기: URPLab Auto Retopology(AutoRemesher)로 몸 전체를 고른 사각면 위주로 다시 짜요.
# 자르기 전에 한 번에 해야 이웃 파츠끼리 경계 점이 정확히 맞아요 (따로 하면 틈이 생김)
TARGET_QUADS = int(os.environ.get("TARGET_QUADS", "20000"))   # 확장의 Target Quads 값. 몸이 고르게 줄어 실제로는 약 7,900 삼각형
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from autoremesh import autoremesh
tris_now = sum(len(p.vertices) - 2 for p in me.polygons)
old_me = me
hi_verts = [v.co.copy() for v in old_me.vertices]   # 손가락 찾기용 원본 고해상도 점
me = autoremesh(old_me, TARGET_QUADS, preserve_thin=True, island_detail=10)
body.data = me
hi_mesh = old_me   # 가중치가 실패하면 다시 리메시할 원본 (끝에 지워요)
print("RETOPO", tris_now, "->", sum(len(p.vertices) - 2 for p in me.polygons))

# 관절 위치 (A자 포즈): 손끝 = 좌우로 가장 바깥, 나머지는 몸 비율 + 메시로 보정
verts = [v.co.copy() for v in me.vertices]
tipL = max(verts, key=lambda c: c.x)   # +x 쪽
tipR = min(verts, key=lambda c: c.x)
def band_half_width(z0, z1):
    xs = [abs(c.x) for c in verts if z0 <= c.z <= z1]
    return max(xs) if xs else 0.2
shoulder_z = 0.815 * H
neck_z = 0.845 * H
hip_z = 0.50 * H
knee_z = 0.285 * H
ankle_z = 0.05 * H
shoulder_x = band_half_width(shoulder_z - 0.02 * H, shoulder_z + 0.02 * H) * 0.78
hip_x = 0.085 * H
joints = {}
for side, tip in (("L", tipL), ("R", tipR)):
    sgn = 1 if side == "L" else -1
    sh = Vector((sgn * shoulder_x, 0, shoulder_z))
    arm_dir = (tip - sh)
    wrist = sh + arm_dir * 0.73   # 손(손목~손끝) ≈ 팔 전체의 27%
    hip = Vector((sgn * hip_x, 0, hip_z))
    # 무릎·발목은 그 높이의 다리 단면 가운데
    def leg_center(z):
        pts = [c for c in verts if abs(c.z - z) < 0.01 * H and sgn * c.x > 0.01 and abs(c.x) < 0.25 * H]
        if not pts:
            return Vector((sgn * hip_x, 0, z))
        return Vector((sum(p.x for p in pts) / len(pts), sum(p.y for p in pts) / len(pts), z))
    knee, ankle = leg_center(knee_z), leg_center(ankle_z + 0.02 * H)
    toe = Vector((ankle.x, min(c.y for c in verts if c.z < ankle_z and sgn * c.x > 0), 0.02 * H))
    joints[side] = dict(shoulder=sh, wrist=wrist, tip=tip, hip=hip, knee=knee, ankle=ankle, toe=toe)

pelvis = Vector((0, 0, hip_z))
neck = Vector((0, 0, neck_z))
head_top = Vector((0, 0, H))

def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length

# 큰 덩어리: 가장 가까운 뼈 구간
coarse = [("Head", neck, head_top), ("Torso", pelvis, neck)]
for side in ("L", "R"):
    j = joints[side]
    coarse += [(f"Arm_{side}", j["shoulder"], j["tip"]), (f"Leg_{side}", j["hip"], j["toe"] if False else j["ankle"])]
    coarse += [(f"Leg_{side}", j["ankle"], j["toe"])]

def classify(p):
    best, name = 1e9, None
    for n, a, b in coarse:
        d = seg_dist(p, a, b)
        if n == "Torso":
            d *= 0.75   # 몸통은 두꺼워서 거리를 덜어 줘요
        if n == "Head":
            d *= 0.9
        if d < best:
            best, name = d, n
    # 세부: 손목·무릎·발목 평면으로 자르기
    if name.startswith("Arm_"):
        j = joints[name[-1]]
        axis = (j["tip"] - j["shoulder"]).normalized()
        return f"Hand_{name[-1]}" if (p - j["wrist"]).dot(axis) > 0 else name
    if name.startswith("Leg_"):
        j = joints[name[-1]]
        if p.z < j["ankle"].z + 0.01 * H:
            return f"Foot_{name[-1]}"
        return f"Calf_{name[-1]}" if p.z < j["knee"].z else f"Thigh_{name[-1]}"
    return name

def cap_holes(pbm):
    """열린 테두리를 고리로 묶어 고리마다 가운데 점 + 부채꼴 삼각형으로 메워요 (몇 번 반복)."""
    for _ in range(4):
        open_edges = set(e for e in pbm.edges if e.is_boundary)
        if not open_edges:
            return
        while open_edges:
            start = open_edges.pop()
            loop = [start.verts[0], start.verts[1]]
            used = {start}
            while True:
                tail = loop[-1]
                nxt = next((e for e in tail.link_edges if e in open_edges and e not in used), None)
                if nxt is None:
                    break
                used.add(nxt)
                open_edges.discard(nxt)
                other = nxt.other_vert(tail)
                if other == loop[0]:
                    break
                loop.append(other)
            if len(loop) < 3:
                continue
            center = pbm.verts.new(sum((v.co for v in loop), Vector()) / len(loop))
            dl = pbm.verts.layers.deform.active
            if dl is not None:   # 뚜껑 가운데 점은 테두리 점들의 뼈 가중치 평균
                acc = {}
                for v in loop:
                    for g, w in v[dl].items():
                        acc[g] = acc.get(g, 0.0) + w / len(loop)
                for g, w in acc.items():
                    center[dl][g] = w
            for i in range(len(loop)):
                a, b = loop[i], loop[(i + 1) % len(loop)]
                if a == b:
                    continue
                try:
                    f = pbm.faces.new((a, b, center))
                    f[pbm.faces.layers.int["cap"]] = 1
                except ValueError:
                    pass  # 이미 있는 면

# 뼈대: 관절 위치로 사람 뼈(Unity Humanoid 이름)를 세우고, 자르기 전 몸 전체에 자동 가중치를 줘요.
# 몸 전체에서 계산한 가중치를 파츠가 그대로 나눠 가져서, 굽혀도 파츠 사이 경계가 벌어지지 않아요
# 손가락: 손 메시에서 손가락 끝을 찾아 마디(뿌리·가운데·끝마디) 위치를 잡아요.
# 손 좌표: a = 손목→손끝, b = 앞뒤(손가락이 벌어진 방향, +면 뒤), c = 손바닥 쪽(몸 안쪽)
FINGERS = ("Index", "Middle", "Ring", "Little")
def find_fingers(side):
    j = joints[side]
    W, T = j["wrist"], j["tip"]
    a = (T - W).normalized()
    b = (Vector((0, 1, 0)) - a * a.y).normalized()
    c = a.cross(b).normalized()
    if c.x * (1 if side == "L" else -1) > 0:   # 손바닥은 몸 안쪽
        c = -c
    L = (T - W).dot(a)
    sgn = 1 if side == "L" else -1
    pts = [p for p in hi_verts if sgn * p.x > 0.15 and p.z < W.z + 0.05 and classify(p) == f"Hand_{side}"]
    loc = [((p - W).dot(a), (p - W).dot(b), (p - W).dot(c), p) for p in pts]
    # 손가락은 서로 붙어 있어 틈으로는 못 나눠요. 대신 손가락 끝 = 둘레 8mm 안에서 손목→손끝 방향(u)으로 가장 멀리 나온 점.
    # 엄지 끝은 손바닥 앞쪽(-b)에서 가장 앞으로 나온 점이라 먼저 찾아 빼요
    from mathutils.kdtree import KDTree
    vw = KDTree(len(loc))
    for i, (u, v, w, _) in enumerate(loc):
        vw.insert(Vector((v, w, 0)), i)
    vw.balance()
    front = [t for t in loc if 0.3 * L < t[0] < 0.75 * L]
    t_front = min(front, key=lambda t: t[1])[:3]; t_back = max(front, key=lambda t: t[1])[:3]
    tips = []
    for u, v, w, _ in loc:
        if u < 0.6 * L:
            continue
        if all(loc[i][0] <= u for _, i, _ in vw.find_range(Vector((v, w, 0)), 0.008)):
            if all((Vector((v, w)) - Vector((tv_, tw_))).length > 0.012 for _, tv_, tw_ in tips):
                tips.append((u, v, w))
    # 엄지가 앞(-b)·뒤(+b) 어느 쪽인지: 손가락 끝 무리에서 더 멀리 떨어진 쪽
    fv = sorted(t[1] for t in tips)
    thumb = t_front if (fv and fv[0] - t_front[1] > t_back[1] - fv[-1]) else t_back
    tips = [t for t in tips if (Vector(t[1:]) - Vector(thumb[1:])).length > 0.015]
    tips = sorted(tips, key=lambda t: -t[0])[:4]
    index_first = thumb is t_front
    tips.sort(key=lambda t: t[1] if index_first else -t[1])   # 검지(엄지 쪽)부터
    print("FINGER_TIPS", side, len(tips), [tuple(round(x, 3) for x in t) for t in tips], "thumb", tuple(round(x, 3) for x in thumb))
    to_world = lambda u, v, w: W + a * u + b * v + c * w

    def center(u0, v0, w0):
        # 그 자리 손가락 단면 가운데: 둘레 점들의 (v, w) 평균
        near = [(v, w) for u, v, w, _ in loc if abs(u - u0) < 0.004 and abs(v - v0) < 0.006]
        if len(near) < 3:
            return to_world(u0, v0, w0)
        return to_world(u0, sum(v for v, _ in near) / len(near), sum(w for _, w in near) / len(near))

    chains = {}
    for name, (tu, tv_, tw_) in zip(FINGERS, tips):
        u0 = 0.47 * L
        pts_ = []
        for t in (0.0, 0.45, 0.75):
            uu = u0 + (tu - 0.006 - u0) * t
            pts_.append(center(uu, tv_, tw_))
        pts_.append(to_world(tu - 0.004, tv_, tw_))
        chains[name] = pts_
    # 엄지: 손목 쪽 뿌리(검지 뿌리 옆, 손바닥 쪽) → 엄지 끝
    idx0 = chains[FINGERS[0]][0] if FINGERS[0] in chains else W
    root = W.lerp(idx0, 0.35) + c * 0.006
    tip_w = to_world(thumb[0] - 0.004, thumb[1], thumb[2])
    chains["Thumb"] = [root, root.lerp(tip_w, 0.42), root.lerp(tip_w, 0.72), tip_w]
    print("FINGERS", side, "L", round(L, 3), {k: round((v[3] - v[0]).length, 3) for k, v in chains.items()})
    return chains, c
fingers = {side: find_fingers(side) for side in ("L", "R")}

arm_data = bpy.data.armatures.new("Player_Rig")
rig = bpy.data.objects.new("Player_Rig", arm_data)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
def bone(name, head, tail, parent=None, connect=False):
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = head, tail
    if parent:
        b.parent = arm_data.edit_bones[parent]
        b.use_connect = connect
    return b
chest_z = 0.795 * H
bone("Hips", pelvis, Vector((0, 0, 0.56 * H)))
bone("Spine", Vector((0, 0, 0.56 * H)), Vector((0, 0, 0.66 * H)), "Hips", True)
bone("Chest", Vector((0, 0, 0.66 * H)), Vector((0, 0, chest_z)), "Spine", True)
bone("Neck", Vector((0, 0, chest_z)), Vector((0, 0, 0.862 * H)), "Chest", True)
bone("Head", Vector((0, 0, 0.862 * H)), Vector((0, 0, 0.99 * H)), "Neck", True)
for side, word in (("L", "Left"), ("R", "Right")):
    j = joints[side]
    sgn = 1 if side == "L" else -1
    elbow = j["shoulder"] + (j["wrist"] - j["shoulder"]) * 0.5
    bone(f"{word}Shoulder", Vector((sgn * 0.02, 0, chest_z - 0.01 * H)), j["shoulder"], "Chest")
    bone(f"{word}UpperArm", j["shoulder"], elbow, f"{word}Shoulder", True)
    bone(f"{word}LowerArm", elbow, j["wrist"], f"{word}UpperArm", True)
    chains, palm = fingers[side]
    bone(f"{word}Hand", j["wrist"], chains["Middle"][0], f"{word}LowerArm", True)
    for fname, pts in chains.items():
        prev = f"{word}Hand"
        for k, part in enumerate(("Proximal", "Intermediate", "Distal")):
            n = f"{word}{fname}{part}"
            bone(n, pts[k], pts[k + 1], prev, k > 0)
            prev = n
    bone(f"{word}UpperLeg", j["hip"], j["knee"], "Hips")
    bone(f"{word}LowerLeg", j["knee"], j["ankle"], f"{word}UpperLeg", True)
    bone(f"{word}Foot", j["ankle"], Vector((j["ankle"].x, j["toe"].y * 0.7, 0.025 * H)), f"{word}LowerLeg", True)
bpy.ops.object.mode_set(mode='OBJECT')
for side, word in (("L", "Left"), ("R", "Right")):
    arm_data.bones[f"{word}Hand"]["palm"] = list(fingers[side][1])   # 손바닥 방향: 손가락 굽히는 축 계산용 (animate_player.py)
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True); rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
# AutoRemesher 결과가 매번 조금씩 달라서, 가끔 자동 가중치(Bone Heat)가 몸 상당 부분에서 실패해요.
# 가중치 없는 점이 1% 넘으면 리메시부터 다시 해요 (최대 5번)
for attempt in range(5):
    unweighted = sum(1 for v in me.vertices if not v.groups)
    if unweighted <= len(me.vertices) * 0.01:
        break
    print("WEIGHTS_RETRY", attempt + 1, "unweighted", unweighted)
    old = me
    me = autoremesh(hi_mesh, TARGET_QUADS, preserve_thin=True, island_detail=10)
    body.data = me
    bpy.data.meshes.remove(old)
    body.vertex_groups.clear()
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True); rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
bpy.data.meshes.remove(hi_mesh)
# 자동 가중치가 못 닿은 점(작은 조각 등)은 가장 가까운 뼈에 통째로 붙여요
segments = [(b.name, b.head_local, b.tail_local) for b in arm_data.bones]
missing = [v for v in me.vertices if not v.groups]
for v in missing:
    name = min(segments, key=lambda s: seg_dist(v.co, s[1], s[2]))[0]
    vg = body.vertex_groups.get(name) or body.vertex_groups.new(name=name)
    vg.add([v.index], 1.0, 'REPLACE')
group_names = [g.name for g in body.vertex_groups]
print("WEIGHTS groups", len(group_names), "filled unweighted verts", len(missing))

bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
face_part = {f.index: classify(f.calc_center_median()) for f in bm.faces}
# 몸 전체의 부드러운 점 법선을 기억해 두고 파츠마다 그대로 써요 → 조립했을 때 자른 자리에 음영 줄이 안 생겨요
bm.normal_update()
def _key(co):
    return (round(co.x, 5), round(co.y, 5), round(co.z, 5))
body_normals = {_key(v.co): v.normal.copy() for v in bm.verts}
parts = sorted(set(face_part.values()))
os.makedirs(out_dir, exist_ok=True)

colors = {}
part_objects = []
for i, part in enumerate(parts):
    pbm = bm.copy()
    pbm.faces.ensure_lookup_table()
    kill = [f for f in pbm.faces if face_part[f.index] != part]
    bmesh.ops.delete(pbm, geom=kill, context='FACES')
    loose = [v for v in pbm.verts if not v.link_faces]
    bmesh.ops.delete(pbm, geom=loose, context='VERTS')
    # 단면 막기: 잘린 구멍 테두리를 고리 단위로 따라가 고리 가운데 점에서 부채꼴 삼각형으로 메워요.
    # 경계가 들쭉날쭉해 한 점에서 8자로 꼬여도 고리를 나눠 따라가니 빠짐없이 막혀요. 마지막에 법선을 바깥으로 맞춰요
    pbm.faces.layers.int.new("cap")
    cap_holes(pbm)
    bmesh.ops.recalc_face_normals(pbm, faces=pbm.faces[:])
    pme = bpy.data.meshes.new(f"Player_{part}")
    pbm.to_mesh(pme)
    pme.shade_smooth()  # 리토폴로지 면은 부드럽게 (각진 면 음영 방지)
    caps = [0] * len(pme.polygons)
    pme.attributes["cap"].data.foreach_get("value", caps)
    loop_normals = [None] * len(pme.loops)
    for poly in pme.polygons:
        for li in poly.loop_indices:
            if caps[poly.index]:
                loop_normals[li] = poly.normal.copy()   # 단면 뚜껑은 평평하게 (안쪽이라 안 보임)
            else:
                co = pme.vertices[pme.loops[li].vertex_index].co
                loop_normals[li] = body_normals.get(_key(co), poly.normal)
    pme.normals_split_custom_set(loop_normals)
    pme.attributes.remove(pme.attributes["cap"])
    pbm.free()
    obj = bpy.data.objects.new(f"Player_{part}", pme)
    bpy.context.scene.collection.objects.link(obj)
    for g in group_names:
        obj.vertex_groups.new(name=g)
    obj.parent = rig
    obj.modifiers.new("Armature", 'ARMATURE').object = rig
    part_objects.append(obj)
    palette = {"Head": 0.62, "Torso": 0.0, "Arm": 0.08, "Hand": 0.5, "Thigh": 0.78, "Calf": 0.3, "Foot": 0.16}
    hue = palette.get(part.split("_")[0], 0.9)
    import colorsys
    colors[obj.name] = colorsys.hsv_to_rgb(hue, 0.6 if part.endswith("_R") else 0.45, 0.95 if part.endswith("_R") else 0.8) + (1.0,)

bpy.data.objects.remove(body, do_unlink=True)

def export(objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='OFF')

for o in part_objects:
    export([o], os.path.join(out_dir, f"{o.name}.fbx"))
export(part_objects, os.path.join(out_dir, "Player_BaseBody_All.fbx"))

# 리깅·동작 단계(animate_player.py)가 이어받을 파일: 뼈대 + 가중치 든 파츠
rig_blend = os.path.join(os.path.dirname(os.path.abspath(out_dir)), "Source~", "Player_Body_Rig.blend")
os.makedirs(os.path.dirname(rig_blend), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=rig_blend, compress=True)
print("RIG_BLEND", rig_blend)

report = {o.name: len(o.data.polygons) for o in part_objects}
print("PARTS", report)
print("JOINTS", {k: {n: tuple(round(x, 3) for x in v) for n, v in j.items()} for k, j in joints.items()})

# 확인용 미리보기: 파츠마다 색을 칠해 정면·옆면 렌더 (Workbench)
if preview:
    for o in part_objects:
        o.color = colors[o.name]
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'OBJECT'
    scene.display.shading.light = 'STUDIO'
    scene.render.resolution_x, scene.render.resolution_y = 1500, 1000
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("W"); scene.world = world
    cam_data = bpy.data.cameras.new("Cam"); cam_data.type = 'ORTHO'; cam_data.ortho_scale = H * 1.15
    cam = bpy.data.objects.new("Cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
    # 정면 왼쪽, 옆면 오른쪽에 보이게 복제본을 옆으로 돌려 세워요
    side_objs = []
    for o in part_objects:
        c = o.copy(); c.data = o.data; scene.collection.objects.link(c)
        c.rotation_euler = (0, 0, math.radians(90)); c.location.x += H * 0.55
        side_objs.append(c)
    for o in part_objects:
        o.location.x -= H * 0.3
    cam.location = (H * 0.12, -10, H * 0.5); cam.rotation_euler = (math.radians(90), 0, 0)
    cam_data.ortho_scale = H * 1.6
    scene.render.filepath = preview
    bpy.ops.render.render(write_still=True)
