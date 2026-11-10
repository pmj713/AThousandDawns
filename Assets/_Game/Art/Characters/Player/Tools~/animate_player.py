# Blender(5.x) 백그라운드 스크립트: 뼈대 든 바디(Player_Body_Rig.blend) + 머리카락에 기본 동작을 붙여 Player_Rigged.fbx로 내보내요.
#   blender --background --factory-startup --python animate_player.py -- <rig.blend> <hair.fbx> <output.fbx> [preview.png]
# 동작: Idle(대기)·Run(달리기) 반복, Dash(대시)·Attack1~3(평타 3연타) 한 번. 손가락 마디까지 움직여요 (오른손은 칼 쥔 손)
# 캐릭터는 Blender에서 -Y를 바라봐요 (왼쪽 = +X). 회전은 "뼈대 기준 축 + 각도"로 적어서 뼈 방향(roll)과 상관없이 읽혀요.
import bpy, sys, os, math
from mathutils import Vector, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
rig_blend, hair_fbx, out_fbx = (os.path.abspath(p) for p in argv[:3])  # blend를 열면 상대 경로 기준이 바뀌어서 절대 경로로
preview = os.path.abspath(argv[3]) if len(argv) > 3 else None
FPS = 30

bpy.ops.wm.open_mainfile(filepath=rig_blend)
scene = bpy.context.scene
scene.render.fps = FPS
rig = bpy.data.objects["Player_Rig"]
parts = [o for o in scene.objects if o.type == 'MESH' and o.parent == rig]

# 머리카락: Head 뼈에 통째로 붙여요
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=hair_fbx)
hair = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')
hair.name = "Player_Hair"
for o in [o for o in bpy.data.objects if o not in before and o != hair]:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.objects.active = hair
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
hair.vertex_groups.new(name="Head").add(list(range(len(hair.data.vertices))), 1.0, 'REPLACE')
hair.parent = rig
hair.modifiers.new("Armature", 'ARMATURE').object = rig
meshes = parts + [hair]

# 옷 (컨셉 B안): VARCO 3D로 만든 옷(Source~/Outfit_B.glb)을 A자 기본 자세의 몸에 맞춰 입혀요 (outfit_fit.py).
# 아래에서 T자로 바꿀 때 몸과 같이 바뀌어요. 파일이 없으면 직접 그린 옷(clothes.py)을 써요
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
OUTFIT_GLB = os.path.join(os.path.dirname(rig_blend), "Outfit_B.glb")
cloth = []
if os.path.isfile(OUTFIT_GLB):
    import outfit_fit
    cloth = outfit_fit.fit(rig, parts, OUTFIT_GLB)
    meshes += cloth

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
pose = rig.pose.bones
for pb in pose:
    pb.rotation_mode = 'QUATERNION'


def arm_q(*rots):
    """뼈대 기준 (축, 각도°)들을 차례로 돌린 회전."""
    q = Quaternion()
    for axis, deg in rots:
        q = Quaternion(axis, math.radians(deg)) @ q
    return q


def set_rot(name, *rots):
    pb = pose[name]
    rest = pb.bone.matrix_local.to_quaternion()
    pb.rotation_quaternion = rest.inverted() @ arm_q(*rots) @ rest


def set_hips_height(dz):
    # Hips 뼈는 위(+Z)를 향해서 뼈 로컬 Y = 위쪽
    pose["Hips"].location = (0, dz, 0)


# 기본 자세를 A자 → T자로 바꿔요. Unity Humanoid는 T자를 기준으로 동작을 옮겨 입혀서, A자 그대로면 남의 동작에서 팔이 어긋나요.
# 위팔을 수평 바깥으로 돌린 자세를 메시에 굳히고(모디파이어 적용) 그 자세를 뼈대 기본 자세로 삼아요
for word, sgn in (("Left", 1), ("Right", -1)):
    b = rig.data.bones[f"{word}UpperArm"]
    d = (b.tail_local - b.head_local).normalized()
    q = d.rotation_difference(Vector((sgn, 0, 0)))
    set_rot(f"{word}UpperArm", (q.axis, math.degrees(q.angle)))
    hb = rig.data.bones[f"{word}Hand"]
    if "palm" in hb:
        hb["palm"] = list(q @ Vector(hb["palm"]))   # 손바닥 방향도 같이 돌아가요
bpy.context.view_layer.update()
for o in meshes:
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier="Armature")
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode='OBJECT')
for o in meshes:
    o.modifiers.new("Armature", 'ARMATURE').object = rig
for pb in pose:
    pb.rotation_quaternion = Quaternion()


# 오른손 칼(직검): 주먹 쥔 손 안을 지나 엄지 쪽으로 칼날이 나가요 (IK 베기에서 맞추는 칼날 방향과 같음).
# 칼날 폭은 손등→손가락 방향(날이 그쪽을 향함), 두께는 손바닥 방향. 오른손 뼈에 통째로 붙여 함께 움직여요
def build_sword():
    import bmesh
    b = rig.data.bones
    hb = b["RightHand"]
    hand_dir = (hb.tail_local - hb.head_local).normalized()
    palm = Vector(hb["palm"]).normalized()
    knuckles = (b["RightIndexProximal"].head_local + b["RightLittleProximal"].head_local) / 2
    t = b["RightIndexProximal"].head_local - b["RightLittleProximal"].head_local
    t = (t - hand_dir * t.dot(hand_dir)).normalized()                  # 칼 축 (엄지 쪽 = 칼날)
    edge = hand_dir                                                       # 날 방향
    flat = t.cross(edge).normalized()                                     # 칼 두께 방향
    grip_c = knuckles - hand_dir * 0.02 + palm * 0.03                     # 주먹 안 가운데
    bm = bmesh.new()

    def box(center_along, length, w, th, start=None):
        c = grip_c + t * center_along
        vs = []
        for dz in (-length / 2, length / 2):
            for dw, dt in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                vs.append(bm.verts.new(c + t * dz + edge * (w / 2 * dw) + flat * (th / 2 * dt)))
        for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
            bm.faces.new([vs[i] for i in f])

    box(-0.02, 0.17, 0.026, 0.024)          # 손잡이
    box(-0.115, 0.03, 0.04, 0.034)          # 손잡이 끝(폼멜)
    box(0.075, 0.018, 0.11, 0.026)          # 코등이
    # 칼날: 마름모 단면, 끝으로 갈수록 좁아져 뾰족
    L, w0, th0 = 0.74, 0.034, 0.008
    rings = []
    for u, scale in ((0.0, 1.0), (0.85, 0.85), (1.0, 0.0)):
        c = grip_c + t * (0.084 + L * u)
        if scale == 0.0:
            rings.append([bm.verts.new(c)])
            continue
        rings.append([bm.verts.new(c + edge * (w0 / 2 * scale)), bm.verts.new(c + flat * (th0 / 2 * scale)),
                      bm.verts.new(c - edge * (w0 / 2 * scale)), bm.verts.new(c - flat * (th0 / 2 * scale))])
    for a_, b_ in zip(rings[:-1], rings[1:]):
        for i in range(4):
            j = (i + 1) % 4
            if len(b_) == 1:
                bm.faces.new((a_[i], a_[j], b_[0]))
            else:
                bm.faces.new((a_[i], a_[j], b_[j], b_[i]))
    bm.faces.new(list(reversed(rings[0])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("Player_Sword")
    bm.to_mesh(me); bm.free()
    obj = bpy.data.objects.new("Player_Sword", me)
    scene.collection.objects.link(obj)
    obj.vertex_groups.new(name="RightHand").add(list(range(len(me.vertices))), 1.0, 'REPLACE')
    obj.parent = rig
    obj.modifiers.new("Armature", 'ARMATURE').object = rig
    return obj


sword = build_sword()
meshes.append(sword)

# 옷 (컨셉 B안): 먹색 도포·소매·바지·각반·짚신, 흰 속소매·깃, 붉은 허리띠·머리끈 (clothes.py)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
if not cloth:
    import clothes
    cloth = clothes.build(rig, parts, hair)
    meshes += cloth


# 팔을 몸 옆으로 내리는 각도 (T자 수평에서 내려 → 아래에서 12°만 벌어지게)
def arm_spread(side):
    b = rig.data.bones[f"{side}UpperArm"]
    d = (b.tail_local - b.head_local)
    return math.degrees(math.atan2(abs(d.x), -d.z))


DOWN_L = arm_spread("Left") - 12
DOWN_R = arm_spread("Right") - 12


def rest_pose():
    for pb in pose:
        pb.rotation_quaternion = Quaternion()
        pb.location = (0, 0, 0)


FINGER_NAMES = ("Thumb", "Index", "Middle", "Ring", "Little")


def hand(word, curl, thumb=None):
    """손가락 굽힘: curl° 를 마디마다 (뿌리는 조금 덜). 엄지는 thumb°. 굽히는 축 = 마디 방향 × 손바닥 방향."""
    hb = rig.data.bones.get(f"{word}Hand")
    if hb is None or "palm" not in hb:
        return
    palm = Vector(hb["palm"])
    thumb = curl * 0.5 if thumb is None else thumb
    for f in FINGER_NAMES:
        for k, part in enumerate(("Proximal", "Intermediate", "Distal")):
            b = rig.data.bones.get(f"{word}{f}{part}")
            if b is None:
                continue
            d = (b.tail_local - b.head_local).normalized()
            ax = d.cross(palm).normalized()
            deg = thumb if f == "Thumb" else curl * (0.8 if k == 0 else 1.0)
            set_rot(b.name, (ax, deg))


def hands(left, right, right_thumb=None):
    hand("Left", left)
    hand("Right", right, right_thumb)


GRIP = 88          # 칼 쥔 오른손
# 칼끝 방향 (뼈대 기준: 앞 = -Y, 오른쪽 = -X). 손목을 돌려 칼날(주먹의 엄지 쪽)이 이 방향을 보게 해요
SWORD_IDLE = Vector((-0.25, -0.45, -0.86))   # 대기: 앞 아래로 늘어뜨림
SWORD_RUN = Vector((-0.15, 0.35, -0.92))     # 달리기: 뒤 아래로 끌림
SWORD_DASH = Vector((-0.10, 0.70, -0.70))    # 대시: 뒤로 젖힘


def aim_sword(direction):
    bpy.context.view_layer.update()
    b = rig.data.bones
    hb, pb = b["RightHand"], pose["RightHand"]
    hand_dir = (hb.tail_local - hb.head_local).normalized()
    thumb = b["RightIndexProximal"].head_local - b["RightLittleProximal"].head_local
    thumb = (thumb - hand_dir * thumb.dot(hand_dir)).normalized()
    m = pb.matrix.copy()
    cur = (m.to_3x3() @ hb.matrix_local.to_3x3().inverted()) @ thumb
    rot = (cur.rotation_difference(direction.normalized()).to_matrix() @ m.to_3x3()).to_4x4()
    rot.translation = m.translation
    pb.matrix = rot
GRIP_THUMB = 45


def base_arms(elbow=12):
    # 왼팔(+X)은 +Y축, 오른팔(-X)은 -Y축으로 돌려야 내려와요. 팔꿈치는 앞으로 살짝
    set_rot("LeftUpperArm", (Y, DOWN_L))
    set_rot("RightUpperArm", (Y, -DOWN_R))
    set_rot("LeftLowerArm", (X, -elbow))
    set_rot("RightLowerArm", (X, -elbow))


def key_all(frame):
    for pb in pose:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)


def new_action(name):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    return act


actions = []

# ── Idle: 숨쉬기 (2초 반복). 오른손은 칼 쥔 손
act = new_action("Idle")
for f, breath in ((0, 0.0), (30, 1.0), (60, 0.0)):
    rest_pose(); base_arms()
    set_rot("Chest", (X, -1.5 * breath))          # 가슴이 살짝 펴짐
    set_rot("Neck", (X, 1.0 * breath))
    set_rot("LeftUpperArm", (Y, DOWN_L - 1.5 * breath))
    set_rot("RightUpperArm", (Y, -(DOWN_R - 1.5 * breath)))
    set_rot("LeftLowerArm", (X, -12))
    set_rot("RightLowerArm", (X, -12))
    hands(22 + 4 * breath, GRIP, GRIP_THUMB)
    set_hips_height(-0.004 * breath)
    aim_sword(SWORD_IDLE)
    key_all(f)
actions.append((act, 0, 60))

# ── Run: 달리기 (20프레임 = 0.67초에 두 걸음)
act = new_action("Run")


def run_pose(lead, t):
    """lead: 앞으로 나간 다리 'Left'/'Right'. t: 0 = 앞발 닿음, 1 = 다리 교차."""
    back = "Right" if lead == "Left" else "Left"
    rest_pose()
    # 다리: 앞(-Y)으로 차려면 X축 음수, 무릎 굽힘(발이 뒤로)은 X축 양수
    thigh_f = -38 + 33 * t     # 앞다리: -38 → -5
    knee_f = 12 + 10 * t
    thigh_b = 32 - 42 * t      # 뒷다리: 32 → -10 (앞으로 끌려옴)
    knee_b = 55 + 45 * t       # 뒤로 접힘
    set_rot(f"{lead}UpperLeg", (X, thigh_f)); set_rot(f"{lead}LowerLeg", (X, knee_f))
    set_rot(f"{back}UpperLeg", (X, thigh_b)); set_rot(f"{back}LowerLeg", (X, knee_b))
    set_rot(f"{lead}Foot", (X, -8 + 10 * t)); set_rot(f"{back}Foot", (X, 20))
    # 팔: 앞다리 반대쪽 팔이 앞으로, 팔꿈치 80° 굽힘
    swing = 35 * (1 - t)
    sl, sr = (swing, -swing) if lead == "Left" else (-swing, swing)   # +면 뒤로
    set_rot("LeftUpperArm", (Y, DOWN_L + 4), (X, sl))
    set_rot("RightUpperArm", (Y, -(DOWN_R + 4)), (X, sr))
    set_rot("LeftLowerArm", (X, -80)); set_rot("RightLowerArm", (X, -80))

    hands(55, GRIP, GRIP_THUMB)
    # 몸: 앞으로 숙이고, 골반·가슴이 반대로 비틀림
    twist = 8 * (1 - t) * (1 if lead == "Left" else -1)
    set_rot("Hips", (Z, -twist))
    set_rot("Spine", (X, 6))
    set_rot("Chest", (X, 4), (Z, twist * 1.6))
    set_rot("Neck", (X, -6))
    set_hips_height(-0.035 if t == 0 else 0.015)
    aim_sword(SWORD_RUN)


for f, lead, t in ((0, "Left", 0), (5, "Left", 1), (10, "Right", 0), (15, "Right", 1), (20, "Left", 0)):
    run_pose(lead, t)
    key_all(f)
actions.append((act, 0, 20))

# ── Dash: 0.18초에 4칸. 낮게 숙여 앞으로 쏘아지고 팔은 뒤로 (6프레임, 한 번)
act = new_action("Dash")


def dash_pose(k):
    """k: 0 = 출발, 1 = 가장 길게 뻗은 자세."""
    rest_pose()
    set_rot("Hips", (X, 6 * k))
    set_rot("Spine", (X, 18 * k))
    set_rot("Chest", (X, 10 * k))
    set_rot("Neck", (X, -22 * k))                       # 고개는 앞을 봐요
    set_rot("LeftUpperArm", (Y, DOWN_L - 4), (X, 15 + 45 * k))    # 팔을 뒤로 젖혀요 (+X = 뒤)
    set_rot("RightUpperArm", (Y, -(DOWN_R - 4)), (X, 15 + 50 * k))
    set_rot("LeftLowerArm", (X, -10)); set_rot("RightLowerArm", (X, -15))
    hands(35, GRIP, GRIP_THUMB)
    set_rot("LeftUpperLeg", (X, -10 - 45 * k)); set_rot("LeftLowerLeg", (X, 20 + 35 * k))
    set_rot("RightUpperLeg", (X, 10 + 30 * k)); set_rot("RightLowerLeg", (X, 15 + 30 * k))
    set_rot("RightFoot", (X, 25 * k))
    set_hips_height(-0.07 * k)
    aim_sword(SWORD_DASH)


for f, k in ((0, 0.4), (2, 1.0), (4, 1.0), (6, 0.6)):
    dash_pose(k)
    key_all(f)
actions.append((act, 0, 6))

# ── 평타 3연타. 오른손에 칼을 쥐었다고 보고 휘둘러요 (게임 기준: 1·2타 0.32초, 3타 0.5초 + 반 칸 전진)


def ready_pose():
    """공격 사이 준비 자세: 무릎 살짝, 오른손 앞으로."""
    rest_pose(); base_arms()
    set_rot("RightUpperArm", (Y, -DOWN_R + 10), (X, -20))
    set_rot("RightLowerArm", (X, -35))
    set_rot("LeftLowerArm", (X, -40))
    hands(40, GRIP, GRIP_THUMB)
    for side in ("Left", "Right"):
        set_rot(f"{side}UpperLeg", (X, -8)); set_rot(f"{side}LowerLeg", (X, 14))
    set_hips_height(-0.012)


def swing_pose(twist, arm_raise, arm_sweep, elbow, lean=4, knee=18, left_pull=15):
    """가로 베기 자세. twist: 몸 비틀기(+ = 왼쪽), arm_raise: 오른팔을 옆으로 든 각, arm_sweep: Z축 쓸기(- 뒤·오른쪽, + 앞·왼쪽)."""
    rest_pose(); base_arms()
    set_rot("Hips", (Z, twist * 0.35))
    set_rot("Spine", (Z, twist * 0.3), (X, lean))
    set_rot("Chest", (Z, twist * 0.35))
    set_rot("Neck", (Z, -twist * 0.4))                    # 시선은 앞에 고정
    set_rot("RightUpperArm", (Y, -DOWN_R + arm_raise), (Z, arm_sweep))
    set_rot("RightLowerArm", (X, -elbow))
    set_rot("RightHand", (Z, arm_sweep * 0.2))
    set_rot("LeftUpperArm", (Y, DOWN_L - 10), (X, -left_pull - twist * 0.2))
    set_rot("LeftLowerArm", (X, -60))
    hands(45, GRIP, GRIP_THUMB)
    for side in ("Left", "Right"):
        set_rot(f"{side}UpperLeg", (X, -knee * 0.5))
        set_rot(f"{side}LowerLeg", (X, knee))
    set_hips_height(-0.012 * knee / 20)


# 1타: 오른쪽 뒤 → 왼쪽 앞 가로 베기 (10프레임)
act = new_action("Attack1")
for f, pose_fn in ((0, ready_pose),
                   (2, lambda: swing_pose(-28, 70, -45, 35)),       # 당기기 (선딜 0.08초)
                   (4, lambda: swing_pose(30, 80, 70, 8, lean=8)),  # 베기 (판정)
                   (6, lambda: swing_pose(38, 72, 95, 15, lean=6)),
                   (10, ready_pose)):
    pose_fn(); key_all(f)
actions.append((act, 0, 10))

# 2타: 왼쪽 → 오른쪽 되베기 (손등 쪽으로, 10프레임)
act = new_action("Attack2")
for f, pose_fn in ((0, ready_pose),
                   (2, lambda: swing_pose(32, 72, 100, 70, left_pull=5)),     # 팔을 가슴 앞 왼쪽으로 접어 모으기
                   (4, lambda: swing_pose(-30, 82, -30, 6, lean=8)),          # 오른쪽으로 쓸어 베기
                   (6, lambda: swing_pose(-40, 74, -60, 14, lean=6)),
                   (10, ready_pose)):
    pose_fn(); key_all(f)
actions.append((act, 0, 10))

# 3타: 머리 위에서 내려찍기 + 앞발 내딛기 (15프레임)
act = new_action("Attack3")


def chop_pose(lift, elbow, lean, step):
    """lift: 오른팔을 앞으로 들어 올린 각 (170 = 머리 위). step: 0~1 왼발 내딛기."""
    rest_pose(); base_arms()
    set_rot("Spine", (X, lean))
    set_rot("Chest", (X, lean * 0.5), (Z, 10 * step))
    set_rot("Neck", (X, -lean * 0.8))
    set_rot("RightUpperArm", (Y, -DOWN_R + 6), (X, -lift))     # 앞(-X)으로 들어 올려요
    set_rot("RightLowerArm", (X, -elbow))
    set_rot("LeftUpperArm", (Y, DOWN_L - 5), (X, 25 * step))
    set_rot("LeftLowerArm", (X, -45))
    hands(40, GRIP, GRIP_THUMB)
    set_rot("LeftUpperLeg", (X, -8 - 34 * step)); set_rot("LeftLowerLeg", (X, 14 + 30 * step))
    set_rot("RightUpperLeg", (X, -8 + 30 * step)); set_rot("RightLowerLeg", (X, 14 + 8 * step))
    set_rot("RightFoot", (X, 15 * step))
    set_hips_height(-0.012 - 0.055 * step)


for f, args in ((0, None),
                (4, (165, 95, -8, 0.15)),     # 머리 위로 치켜들기 (선딜 0.14초)
                (6, (75, 5, 16, 0.9)),        # 내려찍기 (판정): 팔을 앞 아래로 뻗어 멈춰요
                (8, (55, 5, 20, 1.0)),
                (11, (50, 15, 14, 1.0)),
                (15, None)):
    if args is None:
        ready_pose()
    else:
        chop_pose(*args)
    key_all(f)
actions.append((act, 0, 15))

# ── 평타 1타 (IK): 에셋 스토어 Free Sword Animation의 slash5 내려찍기 "칼 궤적"만 가져와 우리 오른팔이 따라가요 (ik_attack.py).
#    칼을 등 뒤로 넘겨 치켜들었다가 정면 가운데로 허리 높이까지 내려찍어요. 원본은 점프 공격이지만 골반 기준 궤적이라 몸은 안 떠요
#    에셋이 없으면 건너뛰어요. Unity는 IKAttack1이 있으면 평타 1타로 써요
IK_SRC = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..", "..",
                                       "EEJANAI_Team", "FreeSwordAnimations", "FBX", "slash5.fbx"))
if os.path.isfile(IK_SRC):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import ik_attack
    act = new_action("IKAttack1")
    rest_pose()
    err_max, err_avg, _ = ik_attack.bake(rig, IK_SRC, 0.18, 0.40, frame0=0, end_frame=10, hand_curl=hand,
                                        center=1.0)   # 칼이 몸 옆이 아니라 정면 가운데를 세로로 가르게
    actions.append((act, 0, 10))
    print("IK_ATTACK1 wrist err cm max %.1f avg %.1f" % (err_max, err_avg))
else:
    print("IK_ATTACK1 skipped: no source", IK_SRC)

# ── 평타 2·3타 (IK): 가로베기는 원본 동작에서 몸 회전과 팔 궤적을 떼기 어려워서, 칼 궤적을 직접 그려요 (ik_attack.ARC2/ARC3).
#    가슴 높이로 오른쪽 뒤 → 정면 → 왼쪽으로 쓸고, 몸통이 따라 비틀려요. 정면 = 게임 공격 범위 방향
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ik_attack
# 회선참(SkillSpin): 칼을 옆으로 뻗어 게임이 몸을 돌리는 동안 한 바퀴 베요
for name, preset in (("IKAttack2", ik_attack.ARC2), ("IKAttack3", ik_attack.ARC3), ("SkillSpin", ik_attack.SPIN)):
    act = new_action(name)
    rest_pose()
    err_max, err_avg, _ = ik_attack.bake_arc(rig, hand_curl=hand, **preset)
    actions.append((act, 0, preset["end_frame"]))
    print(name, "wrist err cm max %.1f avg %.1f" % (err_max, err_avg))

# 반복 동작은 처음·끝이 같은 키라 이어 붙어요. 보간은 부드럽게(베지어 기본)
rig.animation_data.action = None
rest_pose()

# 내보내기: 뼈대 + 파츠 + 머리카락, 동작 전부
bpy.ops.object.select_all(action='DESELECT')
for o in [rig] + meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = rig
os.makedirs(os.path.dirname(out_fbx), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=False,
                         add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
                         armature_nodetype='NULL', bake_anim=True, bake_anim_use_all_actions=True,
                         bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         bake_anim_simplify_factor=0.0)
print("RIGGED", out_fbx, [a.name for a, _, _ in actions], "bones", len(rig.data.bones))

# 미리보기: 동작별 대표 자세를 3/4 각도에서 찍어 한 장으로 이어 붙여요
if preview:
    import numpy as np
    for o in meshes:
        o.color = ((0.12, 0.12, 0.14, 1) if o in (hair, sword) or "Cloth_Dark" in o.name else
                   (0.7, 0.12, 0.1, 1) if "Cloth_Red" in o.name else
                   (0.85, 0.84, 0.8, 1) if "Cloth_Light" in o.name else (0.8, 0.74, 0.68, 1))
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'OBJECT'
    scene.render.resolution_x, scene.render.resolution_y = 360, 520
    scene.world = bpy.data.worlds.new("W")
    cam_data = bpy.data.cameras.new("Cam"); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 2.3
    cam = bpy.data.objects.new("Cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
    target = Vector((0, 0, 0.95))
    cam.location = target + Vector((3.2, -5.5, 2.6))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    shots = [("Idle", 0), ("Run", 0), ("Dash", 3), ("Attack1", 2), ("Attack1", 4), ("Attack2", 2), ("Attack2", 4), ("Attack3", 4), ("Attack3", 7)]
    tiles = []
    tmp = os.path.join(os.path.dirname(preview), "_anim_tile.png")
    for name, f in shots:
        rig.animation_data.action = bpy.data.actions[name]
        scene.frame_set(f)
        scene.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(tmp, check_existing=False)
        px = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)
        tiles.append(px)
        bpy.data.images.remove(img)
    sheet = np.concatenate(tiles, axis=1)
    out = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels = sheet.ravel()
    out.filepath_raw = preview; out.file_format = 'PNG'; out.save()
    os.remove(tmp)
