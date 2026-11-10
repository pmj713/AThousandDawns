# Blender(5.x) 백그라운드 스크립트: VARCO로 만든 동물(리깅 없음 .glb)에 뼈를 직접 심고 동작을 만들어 Unity용 FBX로 내보내요.
#   blender --background --factory-startup --python prepare_animal.py -- <in.glb> <out_dir> <Name> <kind> <length> <yaw> [preview.png]
#   kind: quad(네 발 짐승: 멧돼지·들개) / snake(뱀)
#   length: 몸길이(m, 머리~꼬리 끝). yaw: 가져온 모델을 Z축으로 돌릴 각도(도) — 머리가 -Y(앞)를 보도록
#   - 뼈: quad는 엉덩이·등·가슴·목·머리·꼬리 2마디 + 다리 4개(허벅지·정강이·발), snake는 머리부터 꼬리까지 12마디 사슬
#   - 무게: 다리는 배 아래 다리 기둥의 정점만, 나머지는 몸통 뼈 마디까지 거리로 나눠요
#   - 동작(Generic으로 가져와요): Idle(숨쉬기), Run(뛰기 반복), Ready(공격 준비, 예고 동안 멈춤), Attack(덤비기)
import bpy, bmesh, sys, os, math
from mathutils import Vector, Quaternion, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_dir, NAME, KIND = os.path.abspath(argv[0]), os.path.abspath(argv[1]), argv[2], argv[3]
LENGTH, YAW = float(argv[4]), float(argv[5])
preview = os.path.abspath(argv[6]) if len(argv) > 6 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = f"{NAME}_Body"
for o in [o for o in bpy.data.objects if o.type != 'MESH']:
    bpy.data.objects.remove(o, do_unlink=True)
body.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# 방향·크기·원점: 머리가 -Y, 발밑이 z=0, 몸길이 LENGTH
body.rotation_mode = "XYZ"                     # glTF로 가져온 물체는 쿼터니언 모드라 오일러 각을 무시해요
body.rotation_euler = (0, 0, math.radians(YAW))
bpy.ops.object.transform_apply(rotation=True)
vs = [v.co.copy() for v in body.data.vertices]
s = LENGTH / (max(v.y for v in vs) - min(v.y for v in vs))
body.scale = (s, s, s)
bpy.ops.object.transform_apply(scale=True)
vs = [v.co.copy() for v in body.data.vertices]
cx = (min(v.x for v in vs) + max(v.x for v in vs)) / 2
cy = (min(v.y for v in vs) + max(v.y for v in vs)) / 2
off = Vector((cx, cy, min(v.z for v in vs)))
for v in body.data.vertices:
    v.co -= off
vs = [v.co.copy() for v in body.data.vertices]
Y0, Y1 = min(v.y for v in vs), max(v.y for v in vs)      # 앞(머리) · 뒤(꼬리)
TOP = max(v.z for v in vs)
W = max(v.x for v in vs) - min(v.x for v in vs)
print("ANIMAL", NAME, "scale %.3f" % s, "len %.2f top %.2f width %.2f" % (Y1 - Y0, TOP, W))

# 텍스처
os.makedirs(out_dir, exist_ok=True)
img = next((n.image for m in body.data.materials if m and m.node_tree for n in m.node_tree.nodes
            if n.type == 'TEX_IMAGE' and n.image), None)
if img is not None:
    img.filepath_raw = os.path.join(out_dir, f"{NAME}_Albedo.png"); img.file_format = 'PNG'; img.save()

# ── 뼈
arm_data = bpy.data.armatures.new(f"{NAME}_Armature")
rig = bpy.data.objects.new(f"{NAME}_Rig", arm_data)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
EB = arm_data.edit_bones
BONES = {}   # 이름 → (머리, 꼬리) 무게 계산용


def bone(name, head, tail, parent=None, connect=False):
    b = EB.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    b.roll = 0
    if parent:
        b.parent = EB[parent]; b.use_connect = connect
    BONES[name] = (Vector(head), Vector(tail))
    return b


def ylen(t):
    return Y0 + (Y1 - Y0) * t


LEGS = {}
if KIND == "quad":
    # 배 높이: 아래에서부터 높이별 정점 수를 세어, 가는 다리 구간을 지나 몸통이 시작되며 수가 확 늘어나는 높이
    bins = 40
    counts = [0] * bins
    for v in vs:
        counts[min(bins - 1, int(v.z / TOP * bins))] += 1
    peak = max(counts)
    belly = next(i for i in range(bins) if i > 3 and counts[i] > peak * 0.45) / bins * TOP
    mid = [v for v in vs if v.z > belly]
    back = max(v.z for v in mid)
    spine_z = belly + (back - belly) * 0.55
    # 다리 기둥: 배 아래 정점을 좌우로 나누고, 앞뒤는 y를 정렬해 가장 큰 틈에서 나눠요
    low = [v for v in vs if v.z < belly * 0.8]
    for side, sx in (("L", 1), ("R", -1)):
        pts = sorted((v for v in low if v.x * sx > 0), key=lambda v: v.y)
        gaps = [(pts[i + 1].y - pts[i].y, i) for i in range(len(pts) - 1)]
        cut = max(gaps)[1]
        for end, grp in (("F", pts[:cut + 1]), ("B", pts[cut + 1:])):
            LEGS[f"{end}{side}"] = sum(grp, Vector()) / max(1, len(grp))
    print("ANIMAL belly %.2f spine %.2f" % (belly, spine_z), {k: tuple(round(x, 2) for x in v) for k, v in LEGS.items()}, "Y0 %.2f Y1 %.2f" % (Y0, Y1))
    front_y = (LEGS["FL"].y + LEGS["FR"].y) / 2
    rear_y = (LEGS["BL"].y + LEGS["BR"].y) / 2
    head_vs = [v for v in vs if v.y < front_y - 0.05]
    head_z = sum(v.z for v in head_vs) / max(1, len(head_vs))
    bone("Root", (0, 0, 0), (0, 0, 0.2))
    bone("Hips", (0, rear_y, spine_z), (0, (rear_y + front_y) / 2, spine_z + 0.02), "Root")
    bone("Spine", (0, (rear_y + front_y) / 2, spine_z + 0.02), (0, front_y, spine_z + 0.03), "Hips", True)
    neck_base = Vector((0, front_y, spine_z + 0.03))
    head_base = Vector((0, front_y + (Y0 - front_y) * 0.45, max(head_z, spine_z * 0.9)))
    bone("Neck", neck_base, head_base, "Spine", True)
    bone("Head", head_base, (0, Y0 + 0.02, head_z * 0.95), "Neck", True)
    bone("Tail1", (0, rear_y, spine_z), (0, rear_y + (Y1 - rear_y) * 0.5, spine_z * 0.95), "Hips")
    bone("Tail2", (0, rear_y + (Y1 - rear_y) * 0.5, spine_z * 0.95), (0, Y1, spine_z * 0.85), "Tail1", True)
    for key, c in LEGS.items():
        top = Vector((c.x * 0.9, c.y, belly + (spine_z - belly) * 0.3))
        knee = Vector((c.x, c.y + (0.03 if key[0] == "F" else -0.03), belly * 0.5))
        ankle = Vector((c.x, c.y, belly * 0.12))
        toe = Vector((c.x, c.y - 0.06, 0.0))
        parent = "Spine" if key[0] == "F" else "Hips"
        bone(f"Leg{key}1", top, knee, parent)
        bone(f"Leg{key}2", knee, ankle, f"Leg{key}1", True)
        bone(f"Leg{key}3", ankle, toe, f"Leg{key}2", True)
    BELLY = belly
else:
    # 뱀: 몸 가운데 선을 따라 12마디 (각 마디의 높이·옆 위치는 그 구간 정점 평균)
    n = 12
    pts = []
    for i in range(n + 1):
        t = i / n
        band = [v for v in vs if abs(v.y - ylen(t)) < (Y1 - Y0) / n * 0.6]
        if not band:
            band = vs
        pts.append(Vector((sum(v.x for v in band) / len(band), ylen(t), sum(v.z for v in band) / len(band))))
    # 사슬은 꼬리에서 머리 쪽으로 (Seg0 꼬리 끝 … Seg11 머리): 머리 쪽 마디를 돌리면 머리가 들려요
    pts = pts[::-1]
    bone("Root", (0, 0, 0), (0, 0, 0.1))
    for i in range(n):
        bone(f"Seg{i}", pts[i], pts[i + 1], "Root" if i == 0 else f"Seg{i - 1}", i > 0)
bpy.ops.object.mode_set(mode='OBJECT')

# ── 무게: 정점마다 가장 가까운 뼈 마디 둘을 거리로 나눠요 (다리 뼈는 다리 기둥 정점에만)


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length


groups = {n: body.vertex_groups.new(name=n) for n in BONES if n != "Root"}
for v in body.data.vertices:
    p = v.co
    cands = []
    for n, (a, b) in BONES.items():
        if n == "Root":
            continue
        if n.startswith("Leg"):
            c = LEGS[n[3:5]]
            if p.z > BELLY * 1.05 or (Vector((p.x, p.y)) - Vector((c.x, c.y))).length > 0.13 * LENGTH:
                continue
        cands.append((seg_dist(p, a, b), n))
    cands.sort()
    best = cands[:2]
    ws = [1.0 / max(d, 1e-3) ** 4 for d, _ in best]
    tot = sum(ws)
    for (d, n), w in zip(best, ws):
        groups[n].add([v.index], w / tot, 'REPLACE')
body.parent = rig
body.modifiers.new("Armature", 'ARMATURE').object = rig

# ── 동작
scene = bpy.context.scene
scene.render.fps = 30
pb = rig.pose.bones
for p in pb:
    p.rotation_mode = 'QUATERNION'
X, Z = Vector((1, 0, 0)), Vector((0, 0, 1))


def reset_pose():
    for p in pb:
        p.rotation_quaternion = Quaternion(); p.location = (0, 0, 0)
    bpy.context.view_layer.update()


def turn(name, axis, deg):
    """뼈를 월드 축 둘레로 deg 더 돌려요 (자식은 따라와요)."""
    p = pb[name]
    b = rig.data.bones[name]
    cur = p.matrix.to_quaternion()
    m = (Quaternion(axis, math.radians(deg)) @ cur).to_matrix().to_4x4()
    m.translation = p.head.copy()
    p.matrix = m
    bpy.context.view_layer.update()


def lift(dz, dy=0.0):
    p = pb["Root"]
    m = p.matrix.copy(); m.translation.z += dz; m.translation.y += dy
    p.matrix = m
    bpy.context.view_layer.update()


def make_action(name, keys, pose):
    rig.animation_data_create(); rig.animation_data.action = None
    poses = []
    for f, val in keys:
        reset_pose()
        pose(val)
        poses.append((f, {p.name: p.rotation_quaternion.copy() for p in pb}, pb["Root"].location.copy()))
    for (_, prev, _), (_, cur, _) in zip(poses[:-1], poses[1:]):
        for n, q in cur.items():
            if q.dot(prev[n]) < 0:
                cur[n] = -q
    act = bpy.data.actions.new(name); rig.animation_data.action = act
    for f, quats, root in poses:
        for p in pb:
            p.rotation_quaternion = quats[p.name]
            p.keyframe_insert("rotation_quaternion", frame=f)
        pb["Root"].location = root
        pb["Root"].keyframe_insert("location", frame=f)
    act.use_fake_user = True
    rig.animation_data.action = None
    reset_pose()


def cyc(t, phase=0.0):
    return math.sin(2 * math.pi * (t + phase))


if KIND == "quad":
    def legs(swing):
        """swing: 다리별 앞뒤 흔들기(도, +는 앞으로). X축 +회전은 아래를 향한 뼈를 뒤로 보내서 부호를 뒤집어요.
        앞다리는 뒤로 찰 때 발목이 뒤로 접히고, 뒷다리는 앞으로 올 때 발이 앞으로 접혀요."""
        for key, deg in swing.items():
            turn(f"Leg{key}1", X, -deg)
            if key[0] == "F":
                turn(f"Leg{key}2", X, 10 + max(0.0, -deg) * 1.2)
            else:
                turn(f"Leg{key}2", X, -(10 + max(0.0, deg) * 1.2))

    def idle_pose(t):
        lift(-0.01 * (1 + cyc(t)) * LENGTH)
        turn("Spine", X, 1.5 * cyc(t))
        turn("Head", X, 4 * cyc(t, 0.25)); turn("Head", Z, 6 * cyc(t * 0.5))
        turn("Tail1", Z, 12 * cyc(t))
    make_action("Idle", [(f, f / 40) for f in range(0, 41, 5)], idle_pose)

    def run_pose(t):
        a = 32
        legs({"FL": a * cyc(t), "BR": a * cyc(t), "FR": a * cyc(t, 0.5), "BL": a * cyc(t, 0.5)})   # 대각선 짝 (속보)
        lift(0.03 * LENGTH * abs(cyc(t)))
        turn("Spine", X, 3 * cyc(t, 0.25))
        turn("Head", X, -5 * cyc(t, 0.25))
        turn("Tail1", X, 10 + 8 * cyc(t)); turn("Tail1", Z, 10 * cyc(t, 0.5))
    make_action("Run", [(f, f / 16) for f in range(0, 17, 2)], run_pose)

    def ready_pose(_):
        # 공격 준비: 몸을 낮추고 머리를 숙여 앞다리를 버텨요 (멧돼지는 엄니를 앞으로, 들개는 웅크려요)
        lift(-0.03 * LENGTH)
        turn("Hips", X, 2)
        turn("Neck", X, 18); turn("Head", X, 12)                    # X축 +는 머리를 숙여요
        legs({"FL": 18, "FR": 18, "BL": -14, "BR": -14})
        turn("Tail1", X, 25)
    make_action("Ready", [(0, None), (10, None)], ready_pose)

    def attack_pose(stage):
        if stage == 0:
            ready_pose(None)
        elif stage == 1:
            # 쭉 뻗어 덤벼요 (앞다리는 앞으로, 뒷다리는 뒤로, 머리를 들이밀어요)
            lift(0.08 * LENGTH, -0.05 * LENGTH)
            turn("Hips", X, -4)
            turn("Neck", X, 4); turn("Head", X, -6)
            legs({"FL": 45, "FR": 40, "BL": -40, "BR": -45})
        elif stage == 2:
            lift(-0.03 * LENGTH)
            turn("Neck", X, 10); turn("Head", X, 12)
            legs({"FL": -10, "FR": -12, "BL": 15, "BR": 12})
        else:
            pass
    make_action("Attack", [(0, 0), (4, 1), (9, 2), (18, 3)], attack_pose)
else:
    SEGS = [f"Seg{i}" for i in range(12)]          # 꼬리 → 머리 (부모 → 자식)

    def wave(t, amp, raise_head=0.0):
        # 머리에서 꼬리로 흐르는 옆 물결 (머리 쪽은 덜 흔들어 앞을 봐요)
        for i, n in enumerate(SEGS):
            k = 11 - i                              # 머리에서 몇 번째 마디인지
            turn(n, Z, amp * math.sin(2 * math.pi * (t - k / 6.0)) * (0.4 if k == 0 else 1.0))
        if raise_head:
            turn("Seg10", X, -raise_head * 0.5); turn("Seg11", X, -raise_head)   # X축 −는 머리를 들어요

    # 실제 뱀처럼 기어가기: 머리는 앞을 보고, 몸은 머리에서 꼬리로 흘러가는 S자 길(사인 곡선) 위에 놓여요.
    #   길 위의 관절 자리를 마디 길이만큼씩 머리부터 꼬리로 짚어 가고, 각 마디를 다음 관절로 향하게 해요.
    #   물결은 머리 쪽에서 작게 시작해 몸통에서 커지고, 시간이 지나면 꼬리 쪽으로 흘러가요.
    REST = [rig.data.bones[n].head_local.copy() for n in SEGS] + [rig.data.bones[SEGS[-1]].tail_local.copy()]   # 꼬리 끝 … 머리 끝
    SEG_LEN = [(REST[i + 1] - REST[i]).length for i in range(12)]
    HEAD_TIP = REST[-1]

    def aim_to(name, target):
        p, b = pb[name], rig.data.bones[name]
        head = p.head.copy()
        d = (b.tail_local - b.head_local).normalized().rotation_difference((target - head).normalized())
        m = (d.to_matrix() @ b.matrix_local.to_3x3()).to_4x4()
        m.translation = head
        p.matrix = m
        bpy.context.view_layer.update()

    def slither(t, amp, wavelength, head_raise=0.0):
        k = 2 * math.pi / wavelength

        def path(s):
            a = amp * min(1.0, s / (0.25 * LENGTH))                        # 머리 쪽은 작게
            return Vector((a * math.sin(k * s - 2 * math.pi * t), HEAD_TIP.y + s, HEAD_TIP.z))

        # 머리 끝에서 꼬리 쪽으로 마디 길이만큼씩 길 위를 짚어요
        joints = [path(0.0)]
        s = 0.0
        for i in range(11, -1, -1):
            target = SEG_LEN[i]
            prev = joints[-1]
            step = target / 20
            while True:
                s += step
                if (path(s) - prev).length >= target:
                    break
            joints.append(path(s))
        joints[0].z += head_raise
        joints = joints[::-1]                                                 # 꼬리 끝 … 머리 끝
        root = pb["Root"]
        m = root.matrix.copy(); m.translation += joints[0] - REST[0]
        root.matrix = m
        bpy.context.view_layer.update()
        for i, n in enumerate(SEGS):
            aim_to(n, joints[i + 1])

    def idle_pose(t):
        slither(t, 0.025 * LENGTH, 0.55 * LENGTH, 0.03)                     # 숨 쉬듯 살짝
    make_action("Idle", [(f, f / 40) for f in range(0, 41, 4)], idle_pose)

    def run_pose(t):
        slither(t, 0.08 * LENGTH, 0.55 * LENGTH, 0.025)                     # 크게 S자로 기어요
    make_action("Run", [(f, f / 20) for f in range(0, 21, 2)], run_pose)

    def ready_pose(_):
        # 머리 쪽 몸을 S자로 감아 뒤로 당기고 머리를 쳐들어요
        for k, deg in ((4, 30), (3, -45), (2, 45), (1, -30)):
            turn(f"Seg{11 - k}", Z, deg)
        turn("Seg9", X, -25); turn("Seg10", X, -20); turn("Seg11", X, 15)
    make_action("Ready", [(0, None), (10, None)], ready_pose)

    def attack_pose(stage):
        if stage == 0:
            ready_pose(None)
        elif stage == 1:
            lift(0.0, -0.12 * LENGTH)              # 쭉 뻗어 물어요
            turn("Seg10", X, -8)
        elif stage == 2:
            wave(0.2, 8, 10)
        else:
            wave(0.0, 4, 8)
    make_action("Attack", [(0, 0), (3, 1), (8, 2), (16, 3)], attack_pose)

# 내보내기 (Generic, 플레이어·사람형 적과 같은 축 설정)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = rig
out = os.path.join(out_dir, f"{NAME}_Rigged.fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=False, mesh_smooth_type='OFF', add_leaf_bones=False,
                         primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
                         bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='STRIP')
print("ANIMAL exported", out, "bones", len(rig.data.bones), "faces", len(body.data.polygons))

if preview:
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x, sc.render.resolution_y = 700, 450
    cd = bpy.data.cameras.new("C"); cd.type = 'ORTHO'; cd.ortho_scale = LENGTH * 1.4
    cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = (6, -3, 2.5)
    cam.rotation_euler = (Vector((0, 0, TOP / 2)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = preview; bpy.ops.render.render(write_still=True)
