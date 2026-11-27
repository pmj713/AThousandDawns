# 고블린 궁수 활 잡는 자세 고치기 (플레이테스트 11-27: 활을 이상하게 잡았어요 — 대기·달리기에선 활이 허리께에 가로로 눕고,
#   쏘기에선 비스듬히 몸을 가로지르고, 곡사에선 머리 위에 가로로 누웠어요).
#   활은 왼손에 통째로 붙은 메시라, 동작마다 왼손 방향만 프레임별로 다시 잡아요 (팔·몸 자세는 그대로):
#     쏘는 동안 (왼팔을 뻗은 프레임): 활대는 화살 방향(어깨→손)에 수직이면서 위쪽, 시위는 몸 쪽
#     그 밖 (대기·달리기·등장·놓은 뒤): 활을 옆구리에 세워 들어요 — 활대는 위로, 위 끝이 조금 앞으로, 시위는 몸 쪽(안쪽)
#   이미 내보낸 <Name>_Rigged.fbx를 읽어 고친 뒤 같은 설정으로 다시 내보내요 (원본 리깅 fbx가 남아 있지 않아요)
# 사용: blender -b --factory-startup --python fix_bow.py -- <Rigged.fbx> [dry]
import bpy, sys, math
from mathutils import Vector, Matrix, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
src = argv[0]; DRY = len(argv) > 1 and argv[1] == "dry"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
bow = next(o for o in bpy.data.objects if o.type == 'MESH' and o.name.endswith("Weapon_bow"))
W = rig.matrix_world
print("BOW rig", rig.name, "rot", tuple(round(a, 3) for a in rig.rotation_euler), "scale", tuple(round(a, 3) for a in rig.scale), "bow", bow.name, "parent", bow.parent and bow.parent.name, "verts", len(bow.data.vertices))

# 활의 축·시위 쪽·손잡이를 왼손 뼈 기준(휴지 자세)으로: 마지막 여섯 점이 시위 (아래 셋 · 위 셋), 그 앞 고리 13개가 활대
vw = [bow.matrix_world @ v.co for v in bow.data.vertices]
lo = sum(vw[-6:-3], Vector()) / 3; hi = sum(vw[-3:], Vector()) / 3
rings = [sum(vw[i * 3:i * 3 + 3], Vector()) / 3 for i in range(13)]
grip = rings[6]
axis_w = (hi - lo).normalized()
mid_string = (hi + lo) / 2
side_w = (mid_string - grip); side_w = (side_w - axis_w * side_w.dot(axis_w)).normalized()   # 손잡이 → 시위 (몸 쪽)
bone = rig.data.bones["LeftHand"]
rest = W @ bone.matrix_local
R0 = rest.to_3x3().normalized().inverted()
axis_l, side_l = (R0 @ axis_w).normalized(), (R0 @ side_w).normalized()
grip_l = rest.inverted() @ grip
print("BOW axis_w", tuple(round(a, 2) for a in axis_w), "side_w", tuple(round(a, 2) for a in side_w), "len", round((hi - lo).length, 2))

def frame3(a, s):
    a = a.normalized(); s = (s - a * s.dot(a)).normalized()
    return Matrix((a, s, a.cross(s))).transposed()

UPW = Vector((0, 0, 1))
pb = rig.pose.bones["LeftHand"]
sh = rig.pose.bones["LeftArm"]
spine = rig.pose.bones.get("Spine") or rig.pose.bones["Hips"]
for act in [a for a in bpy.data.actions]:
    rig.animation_data.action = act
    if hasattr(rig.animation_data, "action_slot") and act.slots:
        rig.animation_data.action_slot = act.slots[0]
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    shoot = any(k in act.name for k in ("ShootBow", "TripleShot", "LobShot"))
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f); bpy.context.view_layer.update()
        Mw = W @ pb.matrix
        hand = Mw.translation
        shoulder = (W @ sh.matrix).translation
        fwd = Vector((0, -1, 0))   # 몸 앞 (prepare_enemy.py와 같이 -Y)
        reach = hand - shoulder
        d = reach.normalized()
        extended = shoot and reach.length > 0.6 * (bone.length + rig.data.bones["LeftForeArm"].length + rig.data.bones["LeftArm"].length) and d.dot(UPW) > -0.6
        if extended:   # 쏘는 동안: 활대는 화살에 수직·위쪽, 시위는 몸 쪽
            ta = UPW - d * d.dot(UPW)
            ts = -d
        else:          # 옆구리에 세워 들기
            flat_f = Vector((fwd.x, fwd.y, 0)).normalized()
            ta = (UPW + flat_f * 0.35).normalized()
            inward = (shoulder - hand); inward.z = 0
            ts = inward if inward.length > 1e-3 else -flat_f
        target = frame3(ta, ts)
        cur = Mw.to_3x3().normalized()
        Rnow = frame3(cur @ axis_l, cur @ side_l)
        Rc = target @ Rnow.inverted()                         # 지금 활 방향 → 바라는 활 방향 (월드)
        gw = Mw @ grip_l                                      # 손잡이 자리는 그대로 (손 회전 중심을 손잡이로)
        Mnew = Matrix.Translation(gw) @ Rc.to_4x4() @ Matrix.Translation(-gw) @ Mw
        if f == f0 and not DRY or DRY and f in (f0, (f0 + f1) // 2):
            print("BOW", act.name, f, "shoot" if extended else "carry", "d", tuple(round(a, 2) for a in d), "turn %.0f" % math.degrees(Rc.to_quaternion().angle))
        if DRY: continue
        pb.matrix = W.inverted() @ Mnew
        bpy.context.view_layer.update()
        pb.keyframe_insert("rotation_quaternion", frame=f)
        pb.keyframe_insert("location", frame=f)

if not DRY:
    for act in bpy.data.actions:   # 읽을 때 붙은 "<뼈대>|" 앞머리를 떼요 (다시 내보내면 테이크 이름이 원래대로 "<뼈대>|<동작>" — Unity 클립 연결이 그대로)
        act.name = act.name.split("|")[-1]
    bpy.context.scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.data.objects:
        if o.type in ('ARMATURE', 'MESH'): o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=src, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=False, mesh_smooth_type='OFF', add_leaf_bones=False,
                             primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='STRIP')
    print("BOW exported", src)
