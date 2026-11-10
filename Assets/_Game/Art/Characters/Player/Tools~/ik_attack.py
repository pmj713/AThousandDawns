# 남의 검술 동작에서 "칼 궤적"만 가져와 우리 캐릭터 오른팔이 IK로 따라가는 베기 동작을 만들어요.
#   animate_player.py가 불러 써요: bake(rig, src_fbx, t0, t1, ...)  → 지금 동작(action)에 키를 넣어요
#   혼자 돌려 미리보기: blender --background --factory-startup --python ik_attack.py -- <rig.blend> <src.fbx> <t0> <t1> <preview.png>
# 원본에서 프레임마다 손목 위치·칼날 방향·어깨선 회전을 읽어:
#   - 원본 몸이 돈 만큼은 빼고(몸통 기준 궤적) 우리 체형 비율(골반 높이)로 옮겨 오른팔 IK 목표로
#   - 원본 몸통 회전은 30%(최대 ±25°)만 따라 돌아 정면을 유지
#   - 손목은 엄지 쪽이 칼날 방향을 보게, 오른손은 칼 쥔 주먹
#   - 왼팔은 허리 옆 균형, 다리는 베는 동안 왼발을 내딛어요
import bpy, sys, os, math
from mathutils import Vector, Quaternion

Z = Vector((0, 0, 1))
X = Vector((1, 0, 0))
Y = Vector((0, 1, 0))
RIGHT_T, FWD_T = Vector((-1, 0, 0)), Vector((0, -1, 0))   # 우리 캐릭터: 앞 = -Y, 오른쪽 = -X
TWIST_RATIO, TWIST_MAX = 0.3, 25.0
FOREARM_TWIST_SHARE = 0.5   # 손목 비틀기 중 아래팔이 나눠 갖는 비율
CENTER_WRIST_X = -0.05   # 정면 세로베기 때 손목 자리 (몸 가운데에서 오른쪽으로 5cm)


def _set_rot(rig, name, *rots):
    pb = rig.pose.bones[name]
    q = Quaternion()
    for axis, deg in rots:
        q = Quaternion(axis, math.radians(deg)) @ q
    rest = pb.bone.matrix_local.to_quaternion()
    pb.rotation_quaternion = rest.inverted() @ q @ rest


def _arm_spread(rig, side):
    b = rig.data.bones[f"{side}UpperArm"]; d = b.tail_local - b.head_local
    return math.degrees(math.atan2(abs(d.x), -d.z))


def read_source(scene, src_fbx, t0, t1, fps):
    """원본 FBX를 잠깐 불러 프레임마다 값을 읽고 지워요."""
    before = set(bpy.data.objects)
    actions_before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=src_fbx)
    objs = [o for o in bpy.data.objects if o not in before]
    src = next(o for o in objs if o.type == 'ARMATURE')
    sword = next(o for o in objs if o.name.startswith("Sword") and o.type == 'EMPTY')
    blade = next(o for o in objs if o.type == 'MESH' and o.name.startswith("default"))
    act = src.animation_data.action
    f_start = act.frame_range[0]
    P = lambda n: src.matrix_world @ src.pose.bones["EEJANAIBot:" + n].head
    scene.frame_set(int(f_start))
    ev = blade.evaluated_get(bpy.context.evaluated_depsgraph_get())
    tip_idx = max(range(len(ev.data.vertices)),
                  key=lambda i: ((ev.matrix_world @ ev.data.vertices[i].co) - sword.matrix_world.translation).length)
    # 체형 비율용: 동작 시작(서 있는 자세)의 골반 높이. 구간이 점프 중이어도 비율이 틀어지지 않게
    scene.frame_set(int(f_start))
    stand_h = (P("LeftUpLeg").z + P("RightUpLeg").z) / 2 - min(P("LeftFoot").z, P("RightFoot").z)
    samples = []
    n = int(round((t1 - t0) * fps)) + 1
    for i in range(n):
        f = f_start + t0 * fps + i
        scene.frame_set(int(math.floor(f)), subframe=f - math.floor(f))
        ev = blade.evaluated_get(bpy.context.evaluated_depsgraph_get())
        tip = ev.matrix_world @ ev.data.vertices[tip_idx].co
        samples.append(dict(lp=P("LeftUpLeg"), rp=P("RightUpLeg"), ls=P("LeftArm"), rs=P("RightArm"),
                            wrist=P("RightHand"), blade=(tip - sword.matrix_world.translation).normalized(),
                            foot=min(P("LeftFoot").z, P("RightFoot").z)))
        samples[-1]["hip"] = (samples[-1]["lp"] + samples[-1]["rp"]) / 2
        samples[-1]["stand_h"] = stand_h
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    for a in [a for a in bpy.data.actions if a not in actions_before]:   # 원본 동작은 내보내지 않게 모두 지워요
        bpy.data.actions.remove(a)
    return samples


def _bake_samples(rig, samples, frame0, end_frame, hand_curl):
    """samples[i]: wrist_t(손목 목표), blade_t(칼날 방향), tw(몸통 비틀기°, + = 왼쪽), lean(앞숙임°), step(0~1 왼발 내딛기, 없으면 진행률).
    오른팔 IK로 굽고 손목·손가락을 맞춰 지금 동작에 키를 넣어요."""
    scene = bpy.context.scene
    ab = rig.data.bones
    down_l = _arm_spread(rig, "Left") - 12
    sh_r = ab["RightUpperArm"].head_local
    target = bpy.data.objects.new("IK_Wrist", None); scene.collection.objects.link(target)
    pole = bpy.data.objects.new("IK_Pole", None); scene.collection.objects.link(pole)
    pole.location = sh_r + Vector((-0.25, 0.45, -0.35))      # 팔꿈치는 뒤·아래로
    ik = rig.pose.bones["RightLowerArm"].constraints.new('IK')
    ik.target = target; ik.chain_count = 2; ik.pole_target = pole; ik.pole_angle = math.radians(-90)
    target.animation_data_create(); target.animation_data.action = bpy.data.actions.new("IK_Wrist_Path")

    n = len(samples)
    for i, s in enumerate(samples):
        frame = frame0 + i
        step = i / max(1, n - 1)
        for pb in rig.pose.bones:
            pb.rotation_quaternion = Quaternion(); pb.location = (0, 0, 0)
        tw = s["tw"]
        lean = s.get("lean", 0.0)
        step = s.get("step", step)
        _set_rot(rig, "Hips", (Z, tw * 0.4))
        _set_rot(rig, "Spine", (Z, tw * 0.3), (X, lean * 0.5 + 6 * step))
        _set_rot(rig, "Chest", (Z, tw * 0.3), (X, lean * 0.3 + 4 * step))
        _set_rot(rig, "Neck", (Z, -max(-30.0, min(30.0, tw * 0.8))), (X, -lean * 0.6 - 8 * step))
        _set_rot(rig, "LeftUpperArm", (Y, down_l - 6), (X, -12 + 20 * step))
        _set_rot(rig, "LeftLowerArm", (X, -35))
        # 내려치며 왼발을 내딛고 무게를 실어요
        _set_rot(rig, "LeftUpperLeg", (X, -8 - 30 * step)); _set_rot(rig, "LeftLowerLeg", (X, 14 + 26 * step))
        _set_rot(rig, "RightUpperLeg", (X, -8 + 24 * step)); _set_rot(rig, "RightLowerLeg", (X, 14 + 6 * step))
        _set_rot(rig, "RightFoot", (X, 12 * step))
        rig.pose.bones["Hips"].location = (0, -0.012 - 0.045 * step, 0)
        for pb in rig.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=frame); pb.keyframe_insert("location", frame=frame)
        target.location = s["wrist_t"]
        target.keyframe_insert("location", frame=frame)

    # IK를 키로 굽기
    last = frame0 + n - 1
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True); bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.nla.bake(frame_start=frame0, frame_end=last, only_selected=False, visual_keying=True,
                     clear_constraints=False, use_current_action=True, bake_types={'POSE'})
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.pose.bones["RightLowerArm"].constraints.remove(ik)

    # IK는 팔을 목표로 뻗게 하면서 위팔·아래팔을 뼈 길이 축으로도 비틀어요 → 팔꿈치 피부가 꼬여요.
    # 뼈 회전을 "방향 바꾸기(swing)"와 "길이 축 비틀기(twist)"로 나눠 비틀기만 빼요. 팔 방향·손 위치는 그대로
    twists = []
    for i in range(len(samples)):
        scene.frame_set(frame0 + i)
        for name in ("RightUpperArm", "RightLowerArm"):
            pb = rig.pose.bones[name]
            q = pb.rotation_quaternion.normalized()
            tw = Quaternion((q.w, 0.0, q.y, 0.0))
            if tw.magnitude < 1e-6:
                continue
            tw.normalize()
            twists.append(abs(math.degrees(tw.angle if tw.angle <= math.pi else 2 * math.pi - tw.angle)))
            pb.rotation_quaternion = q @ tw.inverted()
            pb.keyframe_insert("rotation_quaternion", frame=frame0 + i)
    print("IK_ARM_TWIST removed deg max %.0f avg %.0f" % (max(twists), sum(twists) / len(twists)))

    # 손목: 엄지 쪽이 칼날 방향을 보게
    hb = ab["RightHand"]
    hand_dir = (hb.tail_local - hb.head_local).normalized()
    thumb = ab["RightThumbProximal"].head_local - ab["RightLittleProximal"].head_local
    thumb = (thumb - hand_dir * thumb.dot(hand_dir)).normalized()
    errs = []
    for i, s in enumerate(samples):
        scene.frame_set(frame0 + i)
        pb = rig.pose.bones["RightHand"]
        m = pb.matrix.copy()
        cur = (m.to_3x3() @ hb.matrix_local.to_3x3().inverted()) @ thumb
        rot = (cur.rotation_difference(s["blade_t"]).to_matrix() @ m.to_3x3()).to_4x4()
        rot.translation = m.translation
        pb.matrix = rot
        bpy.context.view_layer.update()
        # 손목에 몰린 비틀기(칼 방향 맞추느라 생긴)의 절반은 아래팔이 나눠 가져요 (실제 팔도 팔꿈치~손목 사이에서 돌아요).
        # 손목이 가늘게 꼬이지 않게. 손의 최종 방향은 그대로
        q = pb.rotation_quaternion.normalized()
        tw = Quaternion((q.w, 0.0, q.y, 0.0))
        if tw.magnitude > 1e-6:
            tw.normalize()
            lower = rig.pose.bones["RightLowerArm"]
            lower.rotation_quaternion = lower.rotation_quaternion @ Quaternion().slerp(tw, FOREARM_TWIST_SHARE)
            lower.keyframe_insert("rotation_quaternion", frame=frame0 + i)
            bpy.context.view_layer.update()
            pb.matrix = rot
            bpy.context.view_layer.update()
        pb.keyframe_insert("rotation_quaternion", frame=frame0 + i)
        if hand_curl is not None:
            hand_curl("Right", 88, 45)
            hand_curl("Left", 40, None)
            for b in rig.pose.bones:
                if "Thumb" in b.name or "Index" in b.name or "Middle" in b.name or "Ring" in b.name or "Little" in b.name:
                    b.keyframe_insert("rotation_quaternion", frame=frame0 + i)
        errs.append(((rig.matrix_world @ pb.head) - s["wrist_t"]).length * 100)

    # 마무리: 마지막 자세를 end_frame까지 붙잡아 둬요 (게임 한 타 길이까지 칼을 거두지 않음)
    if end_frame is not None and end_frame > last:
        scene.frame_set(last)
        for pb in rig.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=end_frame); pb.keyframe_insert("location", frame=end_frame)

    bpy.data.actions.remove(target.animation_data.action)
    bpy.data.objects.remove(target, do_unlink=True)
    bpy.data.objects.remove(pole, do_unlink=True)
    return max(errs), sum(errs) / len(errs), [s["blade_t"] for s in samples]


def bake(rig, src_fbx, t0, t1, frame0=0, end_frame=None, hand_curl=None,
         twist_ratio=TWIST_RATIO, twist_max=TWIST_MAX, face_at=0.0, center=0.0):
    """지금 rig에 걸린 동작에 frame0부터 IK 베기 키를 넣어요. end_frame까지는 마지막 자세로 마무리를 이어요.
    hand_curl(word, curl, thumb): 손가락 굽히는 함수 (animate_player.py의 hand). 반환: 손목 오차(cm) 최대·평균
    몸통 회전: 원본 어깨선 회전의 twist_ratio배(최대 ±twist_max°)만 따라 돌고, face_at(0~1, 구간 안 위치) 순간을 정면으로 맞춰요.
      세로베기: 0.3배·25°·처음(0) → 거의 정면 유지 / 가로베기: 1배·80°·칼이 지나가는 순간 → 몸이 돌며 쓸고 그 순간 공격 범위를 봐요
    center(0~1): 손목과 칼날을 몸 가운데 선 쪽으로 당기는 정도. 세로베기가 몸 옆이 아니라 정면을 가르게 해요"""
    scene = bpy.context.scene
    fps = scene.render.fps / scene.render.fps_base
    samples = read_source(scene, src_fbx, t0, t1, fps)
    s0 = samples[0]
    d0 = s0["rp"] - s0["lp"]
    right_s = Vector((d0.x, d0.y, 0)).normalized()
    fwd_s = Z.cross(right_s).normalized()
    to_local = lambda v: Vector((v.dot(right_s), v.dot(fwd_s), v.dot(Z)))

    def yaw_of(a, b):
        d = to_local(b - a)
        return math.degrees(math.atan2(d.y, d.x))

    def body_local(v, yaw):
        l = to_local(v); c, sn = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
        return Vector((l.x * c + l.y * sn, -l.x * sn + l.y * c, l.z))

    ab = rig.data.bones
    hip_t = (ab["LeftUpperLeg"].head_local + ab["RightUpperLeg"].head_local) / 2
    k = hip_t.z / s0["stand_h"]

    def to_target(local, tw):
        r = Quaternion(Z, math.radians(tw))
        return hip_t + (r @ RIGHT_T) * local.x * k + (r @ FWD_T) * local.y * k + Z * local.z * k

    def to_dir(local, tw):
        r = Quaternion(Z, math.radians(tw))
        return ((r @ RIGHT_T) * local.x + (r @ FWD_T) * local.y + Z * local.z).normalized()

    yaw0 = yaw_of(s0["ls"], s0["rs"])
    for s in samples:
        s["yaw"] = yaw_of(s["ls"], s["rs"]) - yaw0
    face_yaw = samples[round(face_at * (len(samples) - 1))]["yaw"]
    for s in samples:
        s["tw"] = max(-twist_max, min(twist_max, (s["yaw"] - face_yaw) * twist_ratio))
        s["wrist_t"] = to_target(body_local(s["wrist"] - s["hip"], s["yaw"]), s["tw"])
        s["blade_t"] = to_dir(body_local(s["blade"], s["yaw"]), s["tw"])
        if center > 0.0:
            # 오른손에 쥔 칼날이 가운데 선에 오도록 손목은 가운데보다 살짝 오른쪽(-X)으로, 칼날은 좌우 성분을 줄여요
            w = s["wrist_t"]
            s["wrist_t"] = Vector((w.x + (CENTER_WRIST_X - w.x) * center, w.y, w.z))
            b = s["blade_t"]
            s["blade_t"] = Vector((b.x * (1.0 - center), b.y, b.z)).normalized()

        sv = body_local(((s["ls"] + s["rs"]) / 2) - s["hip"], s["yaw"])
        s["lean"] = max(-15.0, min(25.0, math.degrees(math.atan2(sv.y, sv.z))))
    return _bake_samples(rig, samples, frame0, end_frame, hand_curl)


def bake_arc(rig, keys, frame0=0, end_frame=None, hand_curl=None, twist_k=0.35, twist_max=45.0, lag=18.0, radius=0.6):
    """칼 궤적을 직접 그려요 (가로베기용). keys: [(프레임, 방위각°, 높이 오프셋m, 왼발 내딛기 0~1)]
    방위각: 0 = 정면, + = 오른쪽, - = 왼쪽. 손목은 가슴 높이 둘레를 돌고, 칼날은 바깥쪽(조금 뒤처지게 lag°)을 봐요.
    몸통은 손이 가는 쪽으로 twist_k배 비틀어요."""
    ab = rig.data.bones
    chest = Vector((0.0, 0.0, ab["RightUpperArm"].head_local.z - 0.12))

    def ease(a, b, u):
        u = u * u * (3 - 2 * u)
        return a + (b - a) * u

    samples = []
    for f in range(keys[0][0], keys[-1][0] + 1):
        k = next(k for k in range(1, len(keys)) if f <= keys[k][0])
        f0, a0, h0, s0 = keys[k - 1]; f1, a1, h1, s1 = keys[k]
        u = 0.0 if f1 == f0 else (f - f0) / (f1 - f0)
        th, dz, st = ease(a0, a1, u), ease(h0, h1, u), ease(s0, s1, u)
        tw = max(-twist_max, min(twist_max, -th * twist_k))
        r = math.radians(th)
        radial = (RIGHT_T * math.sin(r) + FWD_T * math.cos(r))
        bl = math.radians(th + lag * (1 if a1 < a0 else -1))
        blade = (RIGHT_T * math.sin(bl) + FWD_T * math.cos(bl) + Z * 0.05).normalized()
        # 왼쪽(몸 건너편)으로 갈수록 오른팔이 덜 닿아서 반경을 줄여요
        reach = radius * (1.0 - 0.3 * max(0.0, -math.sin(r)))
        samples.append(dict(wrist_t=chest + radial * reach + Z * dz, blade_t=blade, tw=tw, lean=6.0, step=st))
    return _bake_samples(rig, samples, frame0, end_frame, hand_curl)


# 게임 평타 가로베기 궤적 (30fps 프레임, 방위각°, 높이m, 왼발 내딛기). 게임: 2타 선딜 0.08초·판정 0.06초·한 타 0.32초,
# 3타 선딜 0.14초·판정 0.08초·한 타 0.5초 (부채꼴 100° / 160°)
ARC2 = dict(keys=[(0, 40, 0.05, 0.0), (3, 115, 0.08, 0.2), (5, -45, -0.12, 0.7), (7, -75, -0.18, 1.0)], end_frame=10,
            twist_k=0.35, twist_max=40.0)
# 회선참: 게임이 몸(Visual)을 0.2초에 한 바퀴 돌려요. 그동안 칼을 오른쪽 옆으로 수평으로 뻗고 있어 칼이 360°를 벱니다.
# 시작에 팔을 왼쪽으로 감았다가(0.07초) 오른쪽으로 뿌려 펴고, 시전 끝(0.35초)까지 뻗은 채 유지
SPIN = dict(keys=[(0, -35, 0.0, 0.0), (2, -75, 0.03, 0.1), (4, 95, -0.05, 0.4), (11, 95, -0.05, 0.4)], end_frame=11,
            twist_k=0.15, twist_max=20.0, lag=10.0, radius=0.66)
ARC3 = dict(keys=[(0, 40, 0.05, 0.0), (4, 140, 0.12, 0.2), (7, -70, -0.10, 0.8), (9, -125, -0.18, 1.0)], end_frame=15,
            twist_k=0.45, twist_max=60.0)


if __name__ == "__main__" and "--" in sys.argv:
    import numpy as np
    argv = sys.argv[sys.argv.index("--") + 1:]
    rig_blend, src_fbx = (os.path.abspath(p) for p in argv[:2])
    t0, t1 = float(argv[2]), float(argv[3])
    preview = os.path.abspath(argv[4])
    extra = dict(zip(("twist_ratio", "twist_max", "face_at", "center"), map(float, argv[5:9])))
    bpy.ops.wm.open_mainfile(filepath=rig_blend)
    scene = bpy.context.scene
    rig = bpy.data.objects["Player_Rig"]
    parts = [o for o in scene.objects if o.type == 'MESH' and o.parent == rig]
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    rig.animation_data_create(); rig.animation_data.action = bpy.data.actions.new("IKAttack")
    fps = scene.render.fps / scene.render.fps_base
    preset = {"arc2": ARC2, "arc3": ARC3, "spin": SPIN}.get(os.path.basename(src_fbx))
    if preset is not None:   # 직접 그린 가로베기
        end = preset["end_frame"]
        mx, avg, blades = bake_arc(rig, **preset)
    else:
        end = int(round(0.32 * fps))
        mx, avg, blades = bake(rig, src_fbx, t0, t1, frame0=0, end_frame=end, **extra)
    print("IK_REACH_ERR cm max %.1f avg %.1f" % (mx, avg))
    # 확인용 막대 칼 + 게임 카메라(뒤 위)·옆에서
    bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=0.9)
    stick = bpy.context.active_object; stick.rotation_mode = 'QUATERNION'
    for f in range(0, end + 1):
        scene.frame_set(f)
        pb = rig.pose.bones["RightHand"]
        b = blades[min(f, len(blades) - 1)]
        stick.location = rig.matrix_world @ (pb.head + (pb.tail - pb.head) * 0.5) + b * 0.45
        stick.rotation_quaternion = Z.rotation_difference(b)
        stick.keyframe_insert("location", frame=f); stick.keyframe_insert("rotation_quaternion", frame=f)
    for o in parts:
        o.color = (0.8, 0.74, 0.68, 1)
    stick.color = (0.1, 0.1, 0.12, 1)
    scene.render.engine = 'BLENDER_WORKBENCH'; scene.display.shading.color_type = 'OBJECT'
    scene.render.resolution_x, scene.render.resolution_y = 240, 300
    scene.world = bpy.data.worlds.new("W")
    cd = bpy.data.cameras.new("C"); cd.type = 'ORTHO'; cd.ortho_scale = 3.0
    cam = bpy.data.objects.new("C", cd); scene.collection.objects.link(cam); scene.camera = cam
    rows = []; tmp = preview + "_t.png"; N = 8
    for dv in (Vector((0, 0.57, 0.82)).normalized(), Vector((1, 0, 0.1)).normalized()):
        tiles = []
        for j in range(N):
            scene.frame_set(round(end * j / (N - 1)))
            cam.location = Vector((0, 0, 0.9)) + dv * 6
            cam.rotation_euler = (-dv).to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = tmp; bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(tmp, check_existing=False)
            tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(300, 240, 4)); bpy.data.images.remove(img)
        rows.append(np.concatenate(tiles, axis=1))
    sheet = np.concatenate(rows[::-1], axis=0)
    o = bpy.data.images.new("o", sheet.shape[1], sheet.shape[0], alpha=True); o.pixels = sheet.ravel()
    o.filepath_raw = preview; o.file_format = 'PNG'; o.save(); os.remove(tmp)
    print("IK_PREVIEW", preview)
