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

# 붕산권(SkillQuake, 비급 · 시전 0.35초 ≈ 10프레임, 판정 0.12초 ≈ 4프레임째):
#   왼 주먹을 머리 위로 치켜들고(칼은 오른손에 쥔 채 뒤로) → 4프레임에 왼무릎을 굽혀 깊게 내려앉으며
#   왼 주먹으로 앞 땅을 내려쳐요 → 그 자세로 버티다가 → 준비 자세로 일어나요.
#   손·발이 갈 자리를 정하고 두 마디 팔다리 각도를 계산해요 (뼈대 공간 = 월드, 캐릭터는 -Y를 봐요)
def _aim(name, target, child):
    pb, b = pose[name], rig.data.bones[name]
    head = pb.head.copy()
    d = (rig.data.bones[child].head_local - b.head_local).normalized().rotation_difference((target - head).normalized())
    m = (d.to_matrix() @ b.matrix_local.to_3x3()).to_4x4()
    m.translation = head
    pb.matrix = m
    bpy.context.view_layer.update()


def _limb(upper, lower, end, target, pole):
    bpy.context.view_layer.update()
    B_ = rig.data.bones
    s = pose[upper].head.copy()
    l1 = (B_[lower].head_local - B_[upper].head_local).length
    l2 = (B_[end].head_local - B_[lower].head_local).length
    v = target - s
    dist = max(min(v.length, (l1 + l2) * 0.999), abs(l1 - l2) + 1e-3)
    n = v.normalized()
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    pp = pole - n * pole.dot(n)
    pp = pp.normalized() if pp.length > 1e-6 else Z
    _aim(upper, s + n * a + pp * h, lower)
    _aim(lower, s + n * dist, end)


rest_pose(); bpy.context.view_layer.update()
Q_FOOT = {s: pose[f"{s}Foot"].head.copy() for s in ("Left", "Right")}
Q_SH_L = pose["LeftUpperArm"].head.copy()
Q_HEAD = pose["Head"].head.copy()


def quake_pose(stage):
    rest_pose(); base_arms()
    hands(95, GRIP, GRIP_THUMB)                                         # 왼손도 주먹
    if stage == "up":
        set_rot("Spine", (X, -8)); set_rot("Chest", (X, -6), (Z, 12))   # 가슴을 젖히고 왼어깨를 뒤로 감아요
        set_hips_height(-0.03)
        bpy.context.view_layer.update()
        for s in ("Left", "Right"):
            _limb(f"{s}UpperLeg", f"{s}LowerLeg", f"{s}Foot", Q_FOOT[s], Vector((0, -1, 0)))
        _limb("LeftUpperArm", "LeftLowerArm", "LeftHand", Q_HEAD + Vector((0.12, 0.05, 0.42)), Vector((1, 0.3, 0)))   # 왼 주먹을 머리 위로
        set_rot("RightUpperArm", (Y, -(DOWN_R - 10)), (X, 40)); set_rot("RightLowerArm", (X, -10))
    else:
        deep = 1.0 if stage == "slam" else 0.9
        set_hips_height(-0.36 * deep)                                   # 깊게 내려앉아요
        set_rot("Hips", (X, 8 * deep))
        set_rot("Spine", (X, 26 * deep)); set_rot("Chest", (X, 14 * deep), (Z, -8))
        set_rot("Neck", (X, -28 * deep))                                # 고개는 앞을 봐요
        bpy.context.view_layer.update()
        _limb("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", Q_FOOT["Left"] + Vector((0.02, -0.3, 0)), Vector((0, -1, 0)))       # 왼발 앞으로 디디고 무릎 굽혀
        _limb("RightUpperLeg", "RightLowerLeg", "RightFoot", Q_FOOT["Right"] + Vector((0, 0.42, 0.04)), Vector((0, -1, -0.4)))  # 오른무릎이 땅 가까이
        fist = Vector((Q_SH_L.x * 0.55, Q_FOOT["Left"].y - 0.55, 0.1))  # 왼 주먹이 앞 땅에 닿아요
        _limb("LeftUpperArm", "LeftLowerArm", "LeftHand", fist, Vector((1, 0.5, 0)))
        set_rot("RightUpperArm", (Y, -(DOWN_R - 25)), (X, 55)); set_rot("RightLowerArm", (X, -8))   # 칼은 뒤로 뻗어 균형
    aim_sword(SWORD_DASH)


act = new_action("SkillQuake")
for f, pose_fn in ((0, ready_pose),
                   (2, lambda: quake_pose("up")),
                   (4, lambda: quake_pose("slam")),
                   (8, lambda: quake_pose("hold")),
                   (14, ready_pose)):
    pose_fn(); key_all(f)
actions.append((act, 0, 14))

# 죽림섬(SkillFlash, 비급 · 0.25초 간격으로 3번 순간이동 베기 = 7.5프레임마다):
#   번갈아 몸을 낮게 숙이고 칼을 가로로 쭉 뻗은 채 "지나간 뒤" 자세 — 1·3번째는 오른쪽 뒤로 베어 넘기고,
#   2번째는 왼쪽으로 되받아 넘겨요. 마지막엔 칼을 털고 준비 자세로
def flash_pose(stage):
    if stage == "ready":
        ready_pose(); return
    right = stage in ("cut1", "cut3")
    if right:   # 왼쪽 앞에서 오른쪽 뒤로 베어 넘긴 끝 (팔이 오른쪽 뒤로 쭉)
        swing_pose(-40, 62, -75, 4, lean=24, knee=55, left_pull=30)
        sword = Vector((-0.85, 0.45, -0.1))
    else:       # 오른쪽에서 왼쪽 앞으로 되받아 넘긴 끝 (팔이 가슴 앞을 지나 왼쪽으로)
        swing_pose(42, 60, 105, 10, lean=24, knee=55, left_pull=5)
        sword = Vector((0.75, -0.55, -0.1))
    set_hips_height(-0.2)
    bpy.context.view_layer.update()
    _limb("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", Q_FOOT["Left"] + Vector((0.03, -0.38, 0)), Vector((0, -1, 0)))     # 왼발을 앞으로 깊게 디뎌요
    _limb("RightUpperLeg", "RightLowerLeg", "RightFoot", Q_FOOT["Right"] + Vector((-0.03, 0.42, 0.02)), Vector((0, -1, 0)))  # 오른발은 뒤로 미끄러져요
    aim_sword(sword)


act = new_action("SkillFlash")
for f, stage in ((0, "cut1"), (4, "cut1"), (7, "cut2"), (11, "cut2"), (15, "cut3"), (19, "cut3"), (25, "ready")):
    flash_pose(stage); key_all(f)
actions.append((act, 0, 25))


# 금강장(SkillPalm, 혜광의 비급 · 시전 0.35초, 판정 0.12초 ≈ 4프레임째):
#   왼손바닥을 허리춤으로 당겨 몸을 틀었다가 → 왼발을 내디디며 허리째 돌려 왼손바닥을 앞으로 곧게 내뻗어요 (손가락은 위, 손바닥은 앞).
#   칼은 오른손에 쥔 채 뒤로 당겨 균형을 잡아요
def palm_pose(stage):
    rest_pose(); base_arms()
    hands(8, GRIP, GRIP_THUMB)                                          # 왼손은 손가락을 편 손바닥
    sh = pose["LeftUpperArm"].head.copy()
    if stage == "load":
        set_rot("Spine", (Z, -18)); set_rot("Chest", (Z, -12))          # 왼어깨를 뒤로 감아요
        set_hips_height(-0.04)
        bpy.context.view_layer.update()
        for s in ("Left", "Right"):
            _limb(f"{s}UpperLeg", f"{s}LowerLeg", f"{s}Foot", Q_FOOT[s], Vector((0, -1, 0)))
        _limb("LeftUpperArm", "LeftLowerArm", "LeftHand", pose["LeftUpperArm"].head + Vector((0.05, 0.12, -0.32)), Vector((1, 0.5, -0.3)))   # 허리춤으로
    else:
        deep = 1.0 if stage == "strike" else 0.9
        set_rot("Spine", (Z, 22 * deep), (X, 6)); set_rot("Chest", (Z, 14 * deep))   # 허리째 돌려 내뻗어요
        set_hips_height(-0.12 * deep)
        bpy.context.view_layer.update()
        _limb("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", Q_FOOT["Left"] + Vector((0.02, -0.32, 0)), Vector((0, -1, 0)))
        _limb("RightUpperLeg", "RightLowerLeg", "RightFoot", Q_FOOT["Right"] + Vector((0, 0.2, 0)), Vector((0, -1, 0)))
        s = pose["LeftUpperArm"].head.copy()
        _limb("LeftUpperArm", "LeftLowerArm", "LeftHand", s + Vector((-0.12, -0.62, 0.0)), Vector((1, 0.3, -0.6)))   # 앞으로 곧게
    # 손목을 젖혀 손가락은 위, 손바닥은 앞으로
    bpy.context.view_layer.update()
    w = pose["LeftHand"].head.copy()
    _aim("LeftHand", w + Vector((0, -0.25 if stage == "load" else -0.05, 1)).normalized() * 0.1, "LeftMiddleProximal")
    set_rot("RightUpperArm", (Y, -(DOWN_R - 15)), (X, 35)); set_rot("RightLowerArm", (X, -20))
    aim_sword(SWORD_RUN)


act = new_action("SkillPalm")
for f, stage in ((0, None), (2, "load"), (4, "strike"), (8, "hold"), (14, None)):
    if stage is None:
        ready_pose()
    else:
        palm_pose(stage)
    key_all(f)
actions.append((act, 0, 14))



# 탁랑참(SkillWave, 탁랑객의 비급 · 회선참과 같은 시전 0.35초, 몸은 돌지 않아요):
#   칼을 머리 위 뒤로 크게 치켜들었다가 → 왼발을 깊게 내디디며 앞으로 길게 내베어 파도 검기를 보내요 → 잠깐 버티다 준비 자세로
act = new_action("SkillWave")
for f, args in ((0, None),
                (3, (175, 100, -12, 0.1)),    # 머리 위 뒤로 크게 치켜들기
                (5, (80, 0, 22, 1.0)),        # 앞으로 길게 내베기 (판정)
                (8, (62, 0, 24, 1.0)),
                (12, None)):
    if args is None:
        ready_pose()
    else:
        chop_pose(*args)
    key_all(f)
actions.append((act, 0, 12))


# 탁류 소용돌이(SkillVortex, 비도 + 탁랑참 · 2초 반복 · 윗몸만 덮어써요):
#   왼손바닥을 앞으로 뻗어 소용돌이를 붙잡고 손을 천천히 둥글게 돌리며 조종해요. 오른손은 검결(두 손가락)로 가슴 앞에
def vortex_pose(t):
    rest_pose(); base_arms()
    hands(8, 70, GRIP_THUMB)
    a = t * 2 * math.pi
    set_rot("Spine", (Z, 12 + 4 * math.sin(a)), (X, 6)); set_rot("Chest", (Z, 8))
    bpy.context.view_layer.update()
    s = pose["LeftUpperArm"].head.copy()
    _limb("LeftUpperArm", "LeftLowerArm", "LeftHand", s + Vector((-0.1 + 0.07 * math.cos(a), -0.58, 0.02 + 0.07 * math.sin(a))), Vector((1, 0.3, -0.6)))
    bpy.context.view_layer.update()
    w = pose["LeftHand"].head.copy()
    _aim("LeftHand", w + Vector((0.15 * math.cos(a), -0.1, 1)).normalized() * 0.1, "LeftMiddleProximal")   # 손바닥은 앞, 손끝이 따라 돌아요
    r = pose["RightUpperArm"].head.copy()
    _limb("RightUpperArm", "RightLowerArm", "RightHand", r + Vector((0.18, -0.25, -0.12)), Vector((-1, 0.5, -0.6)))   # 검결: 가슴 앞


act = new_action("SkillVortex")
for f in range(0, 25, 3):
    vortex_pose(f / 24); key_all(f)
actions.append((act, 0, 24))

# 비도 순간이동(DaggerFlash, 비도 + 죽림섬 · 반복): 비도 자리로 0.12초마다 순간이동하며 베어 넘겨요.
#   죽림섬의 베어 넘긴 자세를 4프레임(≈0.13초)마다 좌우로 번갈아 (8프레임 한 바퀴)
act = new_action("DaggerFlash")
for f, stage in ((0, "cut1"), (4, "cut2"), (8, "cut1")):
    flash_pose(stage); key_all(f)
actions.append((act, 0, 8))


# 비도 호신륜(SkillHalo, 비도 + 금강장 · 3초 반복 · 윗몸만 덮어써요):
#   두 팔을 가슴 높이에서 양옆 앞으로 벌려 손바닥을 아래로 펴고, 비도 고리를 두른 채 손을 천천히 돌려 이끌어요
def halo_pose(t):
    rest_pose(); base_arms()
    hands(8, 8, GRIP_THUMB)
    a = t * 2 * math.pi
    set_rot("Spine", (Z, 8 * math.sin(a)), (X, 4)); set_rot("Chest", (Z, 6 * math.sin(a)))
    bpy.context.view_layer.update()
    for s, sx in (("Left", 1), ("Right", -1)):
        sh = pose[f"{s}UpperArm"].head.copy()
        k = math.sin(a + (0 if s == "Left" else math.pi))
        _limb(f"{s}UpperArm", f"{s}LowerArm", f"{s}Hand", sh + Vector((sx * 0.22, -0.4 - 0.05 * k, -0.2 + 0.05 * k)), Vector((sx * 0.4, 0.6, -0.6)))   # 팔을 몸 앞 비스듬히 아래로 벌려요
        bpy.context.view_layer.update()
        w = pose[f"{s}Hand"].head.copy()
        _aim(f"{s}Hand", w + Vector((sx * 0.6, -0.6, -0.2)).normalized() * 0.1, f"{s}MiddleProximal")   # 손바닥은 아래, 손끝은 바깥 앞


act = new_action("SkillHalo")
for f in range(0, 25, 4):
    halo_pose(f / 24); key_all(f)
actions.append((act, 0, 24))

# 혈영참(SkillDash, 혈영검마의 비급 · 회선참과 같은 시전 0.35초, 몸은 돌지 않아요):
#   낮게 웅크려 칼을 왼쪽 허리로 감았다가 → 앞으로 4칸 쏜살같이 지나가며 베어, 칼이 오른쪽 뒤로 쭉 뻗은 "지나간 뒤" 자세로 멈춰요 → 준비 자세로
act = new_action("SkillDash")
for f, stage in ((0, None), (2, "cut2"), (5, "cut1"), (9, "cut1"), (12, None)):
    if stage is None:
        ready_pose()
    else:
        flash_pose(stage)
    key_all(f)
actions.append((act, 0, 12))

# 피격(Hurt, 맞아서 경직되는 동안 · 10프레임): 맞은 순간 윗몸이 뒤로 젖혀지고 고개가 꺾이며 두 팔이 벌어졌다가 → 준비 자세로 돌아와요
def hurt_pose(k):
    ready_pose()
    set_rot("Spine", (X, -16 * k)); set_rot("Chest", (X, -10 * k)); set_rot("Neck", (X, -14 * k))
    set_rot("RightUpperArm", (Y, -DOWN_R + 10 + 25 * k), (X, -20 + 30 * k))
    set_rot("LeftUpperArm", (Y, DOWN_L - 25 * k), (X, 20 * k))
    for side in ("Left", "Right"):
        set_rot(f"{side}UpperLeg", (X, -8 - 10 * k)); set_rot(f"{side}LowerLeg", (X, 14 + 20 * k))
    set_hips_height(-0.012 - 0.05 * k)


act = new_action("Hurt")
for f, k in ((0, 0.0), (2, 1.0), (5, 0.8), (10, 0.0)):
    hurt_pose(k); key_all(f)
actions.append((act, 0, 10))


# 쓰러짐(Die · 40프레임, 끝 자세로 멈춰요): 무릎이 꺾여 털썩 꿇어앉고 → 앞으로 무너지며 왼손으로 땅을 짚고 고개를 떨궈요. 칼은 늘어뜨린 채
def die_pose(stage):
    rest_pose(); base_arms()
    hands(40, GRIP, GRIP_THUMB)
    drop = {"buckle": 0.35, "kneel": 0.62, "slump": 0.7}[stage]
    set_hips_height(-drop)
    bpy.context.view_layer.update()
    for s in ("Left", "Right"):   # 무릎을 땅에 대고 발은 뒤로
        back = 0.25 if stage == "buckle" else 0.45
        _limb(f"{s}UpperLeg", f"{s}LowerLeg", f"{s}Foot", Q_FOOT[s] + Vector((0, back, 0.0)), Vector((0, -1, 0)))
    lean = {"buckle": 10, "kneel": 4, "slump": 38}[stage]
    set_rot("Spine", (X, lean)); set_rot("Chest", (X, lean * 0.4)); set_rot("Neck", (X, 25 if stage == "slump" else 8))
    bpy.context.view_layer.update()
    if stage == "slump":
        s = pose["LeftUpperArm"].head.copy()
        _limb("LeftUpperArm", "LeftLowerArm", "LeftHand", Vector((s.x + 0.05, s.y - 0.35, 0.05)), Vector((1, 0.4, -0.3)))   # 왼손으로 땅을 짚어요
    set_rot("RightUpperArm", (Y, -(DOWN_R - 5)), (X, 10)); set_rot("RightLowerArm", (X, -10))
    aim_sword(Vector((0.1, -0.2, -1)))                                    # 칼끝이 땅을 향해 늘어져요


act = new_action("Die")
for f, stage in ((0, None), (6, "buckle"), (14, "kneel"), (26, "slump"), (40, "slump")):
    if stage is None:
        hurt_pose(1.0)
    else:
        die_pose(stage)
    key_all(f)
actions.append((act, 0, 40))

# 전투 대기(CombatIdle · 40프레임 반복): 적이 가까이 있을 때. 무게를 낮추고 왼발을 반 걸음 내밀어,
#   칼끝을 앞 위로 겨눈 채 왼손은 검결로 가슴 앞에. 숨에 맞춰 칼끝이 살짝 오르내려요
def combat_idle_pose(t):
    ready_pose()
    b = math.sin(t * 2 * math.pi)
    set_rot("Spine", (X, 6 + 1.5 * b), (Z, -12)); set_rot("Chest", (Z, -6))
    set_hips_height(-0.07 - 0.01 * b)
    bpy.context.view_layer.update()
    _limb("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", Q_FOOT["Left"] + Vector((0.02, -0.22, 0)), Vector((0, -1, 0)))
    _limb("RightUpperLeg", "RightLowerLeg", "RightFoot", Q_FOOT["Right"] + Vector((-0.02, 0.12, 0)), Vector((0, -1, 0)))
    r = pose["RightUpperArm"].head.copy()
    _limb("RightUpperArm", "RightLowerArm", "RightHand", r + Vector((0.08, -0.38, -0.22 + 0.01 * b)), Vector((-1, 0.4, -0.6)))
    aim_sword(Vector((0.15, -0.85, 0.5 + 0.03 * b)))                        # 칼끝을 앞 위로 겨눠요
    l = pose["LeftUpperArm"].head.copy()
    _limb("LeftUpperArm", "LeftLowerArm", "LeftHand", l + Vector((-0.14, -0.3, -0.12)), Vector((1, 0.4, -0.6)))   # 검결: 가슴 앞


act = new_action("CombatIdle")
for f in range(0, 41, 8):
    combat_idle_pose(f / 40); key_all(f)
actions.append((act, 0, 40))


# 비도 전투 대기(CombatIdleDagger): 무게를 낮추고 몸을 반쯤 튼 채, 손가락을 반쯤 펴 비도를 끼운 듯
#   왼손은 앞으로 내밀어 비도를 겨눠요 (칼은 오른손에 쥔 채 허리 뒤로 늘어뜨려요)
def dagger_idle_pose(t):
    ready_pose()
    b = math.sin(t * 2 * math.pi)
    set_rot("Spine", (X, 5 + 1.5 * b), (Z, -18)); set_rot("Chest", (Z, -8))
    set_hips_height(-0.08 - 0.01 * b)
    bpy.context.view_layer.update()
    _limb("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", Q_FOOT["Left"] + Vector((0.02, -0.24, 0)), Vector((0, -1, 0)))
    _limb("RightUpperLeg", "RightLowerLeg", "RightFoot", Q_FOOT["Right"] + Vector((-0.02, 0.14, 0)), Vector((0, -1, 0)))
    hands(30, 55, GRIP_THUMB)                                                # 손가락을 반쯤 펴 비도를 끼운 듯
    r = pose["RightUpperArm"].head.copy()
    _limb("RightUpperArm", "RightLowerArm", "RightHand", r + Vector((-0.12, 0.12, -0.38 + 0.01 * b)), Vector((-1, 0.3, -0.4)))   # 칼은 허리 뒤로 늘어뜨려요
    aim_sword(Vector((-0.1, 0.45, -0.88)))
    l = pose["LeftUpperArm"].head.copy()
    _limb("LeftUpperArm", "LeftLowerArm", "LeftHand", l + Vector((-0.05, -0.5, -0.08)), Vector((1, 0.3, -0.6)))   # 앞으로 겨눠요


act = new_action("CombatIdleDagger")
for f in range(0, 41, 8):
    dagger_idle_pose(f / 40); key_all(f)
actions.append((act, 0, 40))

# 등장(Spawn · 18프레임, 플레이어 동작을 옮겨 입는 사람형 적이 방에 나타날 때): 낮게 웅크려 고개 숙였다가 → 일어서며 칼을 앞으로 고쳐 잡고 → 준비 자세
def spawn_pose(low):
    ready_pose()
    set_hips_height(-0.32 if low else -0.1)
    bpy.context.view_layer.update()
    for s in ("Left", "Right"):
        _limb(f"{s}UpperLeg", f"{s}LowerLeg", f"{s}Foot", Q_FOOT[s] + Vector((0, 0.05 if low else 0.0, 0)), Vector((0, -1, 0)))
    set_rot("Spine", (X, 28 if low else 8)); set_rot("Neck", (X, 18 if low else -4))
    if low:
        set_rot("RightUpperArm", (Y, -(DOWN_R - 5)), (X, 25)); set_rot("LeftUpperArm", (Y, DOWN_L - 5), (X, 25))
        aim_sword(Vector((0.0, -0.3, -1)))
    else:
        aim_sword(Vector((0.15, -0.9, -0.3)))


act = new_action("Spawn")
for f, low in ((0, True), (6, True), (12, False)):
    spawn_pose(low); key_all(f)
ready_pose(); key_all(18)
actions.append((act, 0, 18))
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
