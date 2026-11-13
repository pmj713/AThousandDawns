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
    elif w == "khakkhara":
        # 선장 (파계승): 주먹을 가로지르는 긴 쇠지팡이. 앞(칼끝 방향) 끝에 둥근 고리와 작은 고리들, 뒤 끝에 물미
        g = grip_of("Right")
        axis = Vector((0, -1, -0.15)).normalized()
        side = Vector((0, 0, -1)); side = (side - axis * side.dot(axis)).normalized()

        def build(bm, g=g, a=axis, e=side):
            box(bm, g + a * 0.4, a, e, 1.75, 0.035, 0.035)                  # 대 (주먹 뒤로 0.45, 앞으로 1.3)
            head = g + a * 1.36
            t = a.cross(e).normalized()
            n = 10
            for k in range(n):                                              # 둥근 고리 (자루 끝에서 세운 고리)
                a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
                p0 = head + (a * math.cos(a0) + e * math.sin(a0)) * 0.14
                p1 = head + (a * math.cos(a1) + e * math.sin(a1)) * 0.14
                box(bm, (p0 + p1) / 2, (p1 - p0).normalized(), t, (p1 - p0).length + 0.01, 0.025, 0.025)
            for k in (-1, 1):                                               # 매달린 작은 고리 둘
                box(bm, head + e * 0.15 * k - a * 0.02, e, t, 0.07, 0.05, 0.02)
            box(bm, g - a * 0.47, a, e, 0.08, 0.05, 0.05)                   # 물미
        make(f"{NAME}_Weapon_khakkhara", "RightHand", build)
    elif w == "axe":
        # 장작 패는 도끼 (마을 주민): 주먹을 가로지르는 나무 자루, 앞 끝에 쐐기 모양 쇠날
        g = grip_of("Right")
        axis = Vector((0, -1, -0.15)).normalized()
        edge = Vector((0, 0, -1)); edge = (edge - axis * edge.dot(axis)).normalized()

        def build(bm, g=g, a=axis, e=edge):
            box(bm, g + a * 0.22, a, e, 0.75, 0.035, 0.035)                 # 자루
            box(bm, g + a * 0.55 + e * 0.06, a, e, 0.12, 0.16, 0.03)        # 날 (칼날 쪽으로 넓게)
        make(f"{NAME}_Weapon_axe", "RightHand", build)
    elif w == "broom":
        # 싸리비 (마을 주민): 긴 대 끝에 묶은 싸리 다발
        g = grip_of("Right")
        axis = Vector((0, -1, -0.15)).normalized()
        side = Vector((0, 0, -1)); side = (side - axis * side.dot(axis)).normalized()

        def build(bm, g=g, a=axis, e=side):
            box(bm, g + a * 0.35, a, e, 1.25, 0.03, 0.03)                   # 대 (주먹 뒤로 0.27, 앞으로 0.98)
            t = a.cross(e).normalized()
            for k in range(5):                                              # 부채꼴로 벌어진 싸리 다발
                d = (a + e * (k - 2) * 0.12).normalized()
                box(bm, g + a * 0.98 + d * 0.22, d, t, 0.45, 0.05, 0.02)
        make(f"{NAME}_Weapon_broom", "RightHand", build)
    elif w == "moktak":
        # 목탁 (목탁승): 왼손에 둥근 나무 목탁(가운데 틈), 오른손에 짧은 채
        gl = grip_of("Left")

        def build_moktak(bm, g=gl):
            ret = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=1.0)
            bmesh.ops.scale(bm, verts=ret["verts"], vec=Vector((0.15, 0.13, 0.12)))
            bmesh.ops.translate(bm, verts=ret["verts"], vec=g + Vector((0.02, -0.12, 0.02)))
            box(bm, g + Vector((0.02, -0.12, -0.02)), Vector((1, 0, 0)), Vector((0, 0, 1)), 0.2, 0.02, 0.06)   # 손잡이 쪽 턱
        make(f"{NAME}_Weapon_moktak", "LeftHand", build_moktak)
        gr = grip_of("Right")
        axis = Vector((0, -1, -0.15)).normalized()

        def build_mallet(bm, g=gr, a=axis):
            box(bm, g + a * 0.12, a, Vector((1, 0, 0)), 0.32, 0.025, 0.025)
            ret = bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=6, radius=0.045)
            bmesh.ops.translate(bm, verts=ret["verts"], vec=g + a * 0.3)
        make(f"{NAME}_Weapon_mallet", "RightHand", build_mallet)
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
    elif w == "hooksword":
        # 갈고리 칼 (수적 졸개): 외날 칼 끝이 등 쪽으로 둥글게 말려 갈고리가 돼요
        g = grip_of("Right")
        axis = Vector((0, -1, -0.15)).normalized()
        edge = Vector((0, 0, -1)); edge = (edge - axis * edge.dot(axis)).normalized()

        def build(bm, g=g, a=axis, e=edge):
            blade(bm, g, a, e, 0.58, 0.06)
            t = a.cross(e).normalized()
            tip = g + a * (0.095 + 0.58)
            c = tip - e * 0.055
            pts = [c + (e * math.cos(k * math.pi / 5) + a * math.sin(k * math.pi / 5)) * 0.055 for k in range(6)]
            for p0, p1 in zip(pts[:-1], pts[1:]):
                box(bm, (p0 + p1) / 2, (p1 - p0).normalized(), t, (p1 - p0).length + 0.012, 0.022, 0.012)
        make(f"{NAME}_Weapon_hooksword", "RightHand", build)
    elif w == "harpoon":
        # 작살 (작살수): 긴 나무 자루(주먹 뒤 0.8, 앞 1.2) 끝에 미늘 둘 달린 쇠 작살촉, 꽁무니에 밧줄 고리
        g = grip_of("Right")
        axis = Vector((0, -1, -0.1)).normalized()
        side = Vector((1, 0, 0)); side = (side - axis * side.dot(axis)).normalized()

        def build(bm, g=g, a=axis, e=side):
            box(bm, g + a * 0.2, a, e, 2.0, 0.04, 0.04)                       # 자루
            head = g + a * 1.2
            box(bm, head + a * 0.14, a, e, 0.3, 0.035, 0.02)                  # 촉
            box(bm, head + a * 0.33, a, e, 0.08, 0.018, 0.012)                # 뾰족한 끝
            for k in (-1, 1):                                                 # 뒤로 젖힌 미늘
                d = (a * -0.6 + e * k).normalized()
                box(bm, head + a * 0.22 + d * 0.06, d, a.cross(e).normalized(), 0.13, 0.02, 0.012)
            t = a.cross(e).normalized()
            tail = g - a * 0.8
            for k in range(8):                                                # 꽁무니 밧줄 고리
                a0, a1 = 2 * math.pi * k / 8, 2 * math.pi * (k + 1) / 8
                p0 = tail - a * 0.06 + (a * math.cos(a0) + t * math.sin(a0)) * 0.06
                p1 = tail - a * 0.06 + (a * math.cos(a1) + t * math.sin(a1)) * 0.06
                box(bm, (p0 + p1) / 2, (p1 - p0).normalized(), e, (p1 - p0).length + 0.01, 0.02, 0.02)
        make(f"{NAME}_Weapon_harpoon", "RightHand", build)
    elif w == "anchor":
        # 쇠닻 (쇠닻 거한): 오른손에 쥔 고리 아래로 굵은 닻대가 늘어지고, 밑에 위로 휜 닻가지 둘, 위쪽에 가로 닻채
        g = grip_of("Right")
        down = Vector((0, 0.15, -1)).normalized()
        side = Vector((0, 1, 0)); side = (side - down * side.dot(down)).normalized()   # 닻가지는 앞뒤로 (다리에 박히지 않게)
        t = down.cross(side).normalized()

        def build(bm, g=g, dn=down, e=side, t=t):
            for k in range(10):                                               # 손에 쥔 고리
                a0, a1 = 2 * math.pi * k / 10, 2 * math.pi * (k + 1) / 10
                p0 = g + (dn * math.cos(a0) + e * math.sin(a0)) * 0.08
                p1 = g + (dn * math.cos(a1) + e * math.sin(a1)) * 0.08
                box(bm, (p0 + p1) / 2, (p1 - p0).normalized(), t, (p1 - p0).length + 0.01, 0.035, 0.035)
            box(bm, g + dn * 0.68, dn, e, 1.2, 0.09, 0.09)                    # 닻대
            box(bm, g + dn * 0.25, t, e, 0.55, 0.06, 0.06)                    # 닻채 (가로)
            bottom = g + dn * 1.26
            for s in (-1, 1):                                                 # 위로 휜 닻가지와 미늘
                pts = [bottom + e * s * 0.42 * math.sin(k * math.pi / 8) - dn * 0.42 * (1 - math.cos(k * math.pi / 8)) for k in range(5)]
                for p0, p1 in zip(pts[:-1], pts[1:]):
                    box(bm, (p0 + p1) / 2, (p1 - p0).normalized(), t, (p1 - p0).length + 0.03, 0.08, 0.08)
                fl = pts[-1]
                box(bm, fl - dn * 0.06 + e * s * 0.05, (e * s - dn).normalized(), t, 0.2, 0.15, 0.03)
        make(f"{NAME}_Weapon_anchor", "RightHand", build)
    elif w == "rattan":
        # 등패 (등패수): 왼팔 바깥에 둥글고 볼록한 등나무 방패, 가운데 쇠 배꼽 (등나무 빛 _Rattan_). 소매·몸에 묻히지 않게 팔에서 17cm 띄워요
        f = B["LeftForeArm"]
        c = f.head_local.lerp(f.tail_local, 0.55)
        out = Vector((1, -0.6, 0)).normalized()

        def build(bm, c=c, out=out):
            r = 0.44
            u = Vector((0, 0, 1)); v = out.cross(u).normalized()
            rings = []
            for i, rr in enumerate((0.0, 0.12, 0.24, 0.35, r)):
                dome = 0.16 * (1 - (rr / r) ** 2)                             # 가운데가 볼록
                if rr == 0:
                    rings.append([bm.verts.new(c + out * (0.17 + dome + 0.02))])
                else:
                    rings.append([bm.verts.new(c + out * (0.17 + dome) + (u * math.cos(k * math.pi / 8) + v * math.sin(k * math.pi / 8)) * rr) for k in range(16)])
            for k in range(16):
                bm.faces.new((rings[0][0], rings[1][k], rings[1][(k + 1) % 16]))
            for r0, r1 in zip(rings[1:-1], rings[2:]):
                for k in range(16):
                    j = (k + 1) % 16
                    bm.faces.new((r0[k], r1[k], r1[j], r0[j]))
            back = [bm.verts.new(c + out * 0.16 + (u * math.cos(k * math.pi / 8) + v * math.sin(k * math.pi / 8)) * r) for k in range(16)]
            for k in range(16):
                j = (k + 1) % 16
                bm.faces.new((rings[-1][k], back[k], back[j], rings[-1][j]))
            bm.faces.new(list(reversed(back)))
        make(f"{NAME}_Rattan_Shield", "LeftForeArm", build)
    elif w == "flagpole":
        # 큰 깃대 (깃발수): 오른손에 세워 쥔 긴 장대(아래 0.6, 위 2.6), 꼭대기 창날, 위쪽에 큰 붉은 깃발 (_Red_)
        g = grip_of("Right")
        up = Vector((0, 0, 1))
        side = Vector((1, 0, 0))

        def build_pole(bm, g=g):
            box(bm, g + up * 1.0, up, side, 3.2, 0.05, 0.05)
            box(bm, g + up * 2.7, up, side, 0.22, 0.04, 0.012)                 # 창날
            box(bm, g + up * 2.56, side, FWD, 0.14, 0.03, 0.03)                # 깃대 머리 가로대
        make(f"{NAME}_Weapon_flagpole", "RightHand", build_pole)

        def build_flag(bm, g=g):
            # 장대에서 바깥(+X 쪽, 몸 반대편)으로 뻗은 1.2 × 0.85 깃발, 끝이 두 갈래로 갈라져요 (양면은 셰이더가 아니라 두께로)
            top, bot = g + up * 2.5, g + up * 1.65
            ends = [(0.0, top, bot), (0.6, top + side * 0.6 - up * 0.04, bot + side * 0.6 + up * 0.03), (1.2, top + side * 1.2 - up * 0.1, bot + side * 1.2 + up * 0.1)]
            vs_ = []
            for _, a, b in ends:
                vs_.append((bm.verts.new(a + FWD * 0.01), bm.verts.new(b + FWD * 0.01), bm.verts.new(a - FWD * 0.01), bm.verts.new(b - FWD * 0.01)))
            notch = bm.verts.new((ends[2][1] + ends[2][2]) / 2 - side * 0.25)
            for (a0, b0, a1_, b1_), (a2, b2, a3, b3) in zip(vs_[:-1], vs_[1:]):
                bm.faces.new((a0, b0, b2, a2)); bm.faces.new((a1_, a3, b3, b1_))
                bm.faces.new((a0, a2, a3, a1_)); bm.faces.new((b0, b1_, b3, b2))
            a2, b2, a3, b3 = vs_[-1]
            bm.faces.new((a2, notch, b2)); bm.faces.new((a3, b3, notch))
        make(f"{NAME}_Red_Flag", "RightHand", build_flag)
    elif w == "meteor":
        # 유성추 (수채주 철룡): 오른손에 쥔 쇠사슬(고리 엮음)이 1.1 늘어지고 끝에 가시 철구 (사슬은 손에 붙어 굳은 채 따라 돌아요)
        g = grip_of("Right")
        down = Vector((0, 0.15, -1)).normalized()
        side = Vector((1, 0, 0)); side = (side - down * side.dot(down)).normalized()
        t = down.cross(side).normalized()

        def build(bm, g=g, dn=down, e=side, t=t):
            n = 12
            for k in range(n):                                                # 사슬 고리: 번갈아 90° 돌려 엮어요
                c = g + dn * (0.08 + k * 0.09)
                a1, a2 = (e, t) if k % 2 == 0 else (t, e)
                box(bm, c + a1 * 0.03, dn, a2, 0.1, 0.018, 0.018)
                box(bm, c - a1 * 0.03, dn, a2, 0.1, 0.018, 0.018)
            ball = g + dn * (0.08 + n * 0.09 + 0.2)
            ret = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.2)
            bmesh.ops.translate(bm, verts=ret["verts"], vec=ball)
            dirs = [Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0)), Vector((0, 0, 1)), Vector((0, 0, -1)),
                    Vector((1, 1, 1)), Vector((-1, 1, 1)), Vector((1, -1, 1)), Vector((-1, -1, 1)),
                    Vector((1, 1, -1)), Vector((-1, 1, -1)), Vector((1, -1, -1)), Vector((-1, -1, -1))]
            for d in dirs:                                                    # 가시
                d = d.normalized()
                o = d.orthogonal().normalized()
                box(bm, ball + d * 0.25, d, o, 0.13, 0.05, 0.05)
        make(f"{NAME}_Weapon_meteor", "RightHand", build)
    elif w == "towershield":
        # 큰 방패 (천마 근위): 왼팔 바깥 앞에 세운 길쭉한 철판 방패 (0.8 × 1.35, 앞으로 살짝 굽음), 가운데 마두 돋을새김
        f = B["LeftForeArm"]
        c = f.head_local.lerp(f.tail_local, 0.55)
        out = Vector((0.75, -0.65, 0)).normalized()

        def build(bm, c=c, out=out):
            up = Vector((0, 0, 1)); v = out.cross(up).normalized()
            cols, rows = 6, 6
            grid = []
            for i in range(rows + 1):
                row = []
                for j in range(cols + 1):
                    x = (j / cols - 0.5) * 0.8; z = (i / rows - 0.5) * 1.35 - 0.15
                    bulge = 0.08 * (1 - (2 * x / 0.8) ** 2)
                    row.append(bm.verts.new(c + out * (0.1 + bulge) + v * x + up * z))
                grid.append(row)
            back = [[bm.verts.new(p.co - out * 0.04) for p in row] for row in grid]
            for i in range(rows):
                for j in range(cols):
                    bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
                    bm.faces.new((back[i][j], back[i + 1][j], back[i + 1][j + 1], back[i][j + 1]))
            box(bm, c + out * 0.2 - up * 0.1, out, v, 0.08, 0.3, 0.3)         # 가운데 돋을새김
        make(f"{NAME}_Weapon_towershield", "LeftForeArm", build)
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
# 장강 수적 무기는 쥔 방향이 칼과 달라요: 깃대는 위로, 쇠닻대는 아래로, 작살은 거의 수평 앞으로
if "flagpole" in WEAPONS:
    BLADE_REST = Vector((0, 0, 1))
elif "anchor" in WEAPONS or "meteor" in WEAPONS:
    BLADE_REST = Vector((0, 0.15, -1)).normalized()
elif "harpoon" in WEAPONS:
    BLADE_REST = Vector((0, -1, -0.1)).normalized()
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
    poses, locs = [], []
    for f, val in keys:
        reset_pose()
        pose(val)
        poses.append((f, {p.name: p.rotation_quaternion.copy() for p in pb}, pb["Hips"].location.copy()))
        locs.append({p.name: p.location.copy() for p in pb})
    # 같은 회전도 q와 -q 두 값이 있어요. 키마다 앞 키와 같은 쪽 값으로 맞춰야 사이를 이을 때 먼 길로 빙글 돌지 않아요
    for (_, prev, _), (_, cur, _) in zip(poses[:-1], poses[1:]):
        for n, q in cur.items():
            if q.dot(prev[n]) < 0:
                cur[n] = -q
    act = bpy.data.actions.new(name); rig.animation_data.action = act
    moved = [p.name for p in pb if p.name != "Hips" and any(l[p.name].length > 1e-5 for l in locs)]   # 늘이려고 밀어낸 뼈(팔)만 위치도 키로
    for (f, quats, hips), lc in zip(poses, locs):
        for n in moved:
            pb[n].location = lc[n]
            pb[n].keyframe_insert("location", frame=f)
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
    # TripleShot (혈궁수 혈시 3연발, 1.4초 = 42프레임): 0.8초에 첫 발, 0.25초마다 재빨리 다시 메겨 두 발 더
    make_action("TripleShot", [(0, nock), (4, nock), (22, cheek), (24, flick), (26, nock), (30, cheek), (31, flick), (33, nock), (37, cheek), (39, flick), (42, flick)], shoot_pose)
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
# 폐사찰 몬스터가 대기·달리기·공격 내내 유지하는 팔 자세
def prayer_arms(drop=0.0):
    """합장: 두 손을 가슴 앞에 모아요 (석상 나한)."""
    chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
    for s, sx in (("Left", 0.035), ("Right", -0.035)):
        arm(s, chest + Vector((sx, -0.26, -0.12 - drop)), Vector((sx * 30, 0.4, -1)))


def hold_moktak():
    """목탁을 왼손에 들고 가슴 앞에 받쳐요 (목탁승)."""
    chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
    arm("Left", chest + Vector((0.1, -0.3, -0.18)), Vector((1, 0.3, -0.6)))


HAS_BLADE = bool({"dao", "bigdao", "jian", "hooksword", "axe", "broom"} & EXTRAS)   # 도끼·싸리비(마을 주민)도 칼처럼 손에서 뻗어요
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
        if "harpoon" in WEAPONS or "flagpole" in WEAPONS:
            point_blade(Vector((0, -0.2, 1)), wrist_bend=90)                 # 작살·깃대는 세워 들고 걸어요
        if "anchor" in WEAPONS or "meteor" in WEAPONS:
            point_blade(Vector((0, 0.25, -1)), wrist_bend=90)                # 쇠닻은 아래로 늘어뜨려 끌어요
        if "khakkhara" in WEAPONS:
            point_blade(Vector((0, -0.15, 1)), wrist_bend=90)                 # 선장은 세워 들고 걸어요
        if "prayer" in EXTRAS:
            prayer_arms()                                                    # 석상 나한: 걸을 때도 합장
        if "moktak" in WEAPONS:
            hold_moktak()                                                    # 목탁승: 목탁을 가슴 앞에

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
    TWIST = 0 if "prayer" in EXTRAS else 30
    charge_arms = (lambda: prayer_arms(0.05)) if "prayer" in EXTRAS else (lambda: fists_up())   # 석상 나한: 합장한 채 들이받아요
    def fists_up():
        for s, inward in (("Left", -0.15), ("Right", 0.15)):
            sh = pb[f"{s}Arm"].head.copy()
            arm(s, sh + Vector((inward, -0.22, 0.12)), Vector((-inward * 4, 0.3, -1)))

    def shoulder_ready(_):
        lower_hips(-0.28, FEET, dy=0.06)
        bend("Spine", Vector((1, 0, 0)), 28)
        bend("Spine", UP, TWIST)                                            # 오른 어깨를 앞으로 (나한은 정면으로)
        charge_arms()
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
        bend("Spine", UP, TWIST + (5 if lead == "Left" else -5))
        charge_arms()
    make_action("ShoulderRun", [(0, 0), (4, 1), (8, 2), (12, 3), (16, 0)], shoulder_run)
    rig.animation_data.action = None
    reset_pose()

# 석상 나한 (statue): 방에 석상처럼 서 있다가(Statue, 멈춘 한 자세) 플레이어가 다가오면 깨어나요(Wake).
#   Statue: 고개를 숙이고 합장한 채 굳은 자세. Wake: 몸을 떨며 어깨를 움츠렸다가 → 고개를 들고 두 팔을 떨쳐 돌가루를 털어내고 → 대기 자세
if "statue" in EXTRAS:
    def statue_pose(stage):
        if stage == "idle":
            idle_end_pose(); return
        if stage == "statue":
            lower_hips(0.0, FEET)                                            # 다리를 쭉 편 채 굳어 있어요
            bend("Spine", Vector((1, 0, 0)), 6)
            bend("Neck", Vector((1, 0, 0)), 22)                              # 고개를 숙여요
            prayer_arms()
        elif stage == "tremble":
            lower_hips(-0.02, FEET)
            bend("Spine", Vector((1, 0, 0)), 10)
            bend("Neck", Vector((1, 0, 0)), 14)
            for s in ("LeftShoulder", "RightShoulder"):
                bend(s, Vector((0, 1, 0)), 10 if s.startswith("Left") else -10)   # 어깨를 움츠려요
            prayer_arms(0.03)
        else:   # burst: 고개를 들고 가슴을 펴며 두 팔을 아래로 떨쳐 내려요
            lower_hips(0.0, FEET)                                            # 다리는 쭉 편 채로
            bend("Spine", Vector((1, 0, 0)), -4)
            bend("Neck", Vector((1, 0, 0)), -12)
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, sh + Vector((sx * 0.22, -0.18, -0.5)), Vector((sx * 0.3, 1, -0.5)))   # 몸 옆 조금 앞으로 (너무 벌리지 않게)
    make_action("Statue", [(0, "statue"), (10, "statue")], statue_pose)
    make_action("Wake", [(0, "statue"), (5, "tremble"), (8, "tremble"), (13, "burst"), (19, "burst"), (28, "idle")], statue_pose)
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

# ── 폐사찰 몬스터 동작
# 망령 (float·cast): 땅에서 떠서 발끝을 늘어뜨리고 둥실거려요. Idle·Run을 직접 만들고(플레이어 걷기 대신),
#   혼불(Cast)은 오른손을 가슴께로 당겼다가(예고 동안 멈춤) 손바닥을 앞으로 쭉 내밀어요
if "float" in EXTRAS:
    def float_base(bob, lean=0.0, trail=0.0):
        hover = 0.22 + bob
        feet = {s: FEET[s] + Vector((0, 0.06 + trail, hover - 0.03)) for s in FEET}   # 다리를 곧게 늘어뜨린 채 떠 있어요 (발은 엉덩이만큼 떠요)
        lower_hips(hover, feet)
        if lean:
            bend("Spine", Vector((1, 0, 0)), lean)
        bend("Neck", Vector((1, 0, 0)), 12)                                  # 고개를 떨궈요

    def float_arms(trail=0.0):
        for s, sx in (("Left", 0.06), ("Right", -0.06)):
            sh = pb[f"{s}Arm"].head.copy()
            arm(s, sh + Vector((sx, -0.12 + trail, -0.5)), Vector((sx * 10, 0.5, -0.4)))

    def float_idle(t):
        float_base(0.05 * math.sin(2 * math.pi * t))
        float_arms()
    make_action("Idle", [(f, f / 40) for f in range(0, 41, 5)], float_idle)

    def float_run(t):
        float_base(0.04 * math.sin(4 * math.pi * t), lean=22, trail=0.35)
        float_arms(trail=0.35)                                               # 팔이 뒤로 끌려요
    make_action("Run", [(f, f / 20) for f in range(0, 21, 5)], float_run)

    def cast_pose(stage):
        float_base(0.0, lean=8)
        float_arms()
        sh = pb["RightArm"].head.copy()
        if stage == "load":
            arm("Right", sh + Vector((0.12, -0.1, -0.15)), Vector((-0.6, 0.6, -0.5)))    # 손을 가슴께로 당겨요
        elif stage == "push":
            arm("Right", sh + Vector((0.08, -1, 0.05)).normalized() * REACH * 1.0, Vector((-0.5, 0, -1)))   # 손바닥을 앞으로
            aim("RightHand", None, delta=Quaternion(Vector((1, 0, 0)), math.radians(-60)) @ (pb["RightHand"].matrix.to_quaternion() @ B["RightHand"].matrix_local.to_quaternion().inverted()))
    make_action("Attack", [(0, "load"), (4, "push"), (14, "push"), (24, "rest")], cast_pose)

    # 등장 (원귀, 28프레임): 바닥에 가슴께까지 잠긴 채 고개를 떨구고 → 스르르 떠올라 → 떠오른 끝에 고개를 젖히고 팔을 벌려
    #   한 번 부르르 떨고 → 고개를 떨군 대기
    def float_spawn(stage):
        if stage == "idle":
            float_idle(0.0); return
        depth = {"sunk": -1.15, "half": -0.55, "up": 0.3, "shiver": 0.26}[stage]
        feet = {s: FEET[s] + Vector((0, 0.06, depth - 0.03)) for s in FEET}
        lower_hips(depth, feet)
        if stage in ("sunk", "half"):
            bend("Spine", Vector((1, 0, 0)), 18 if stage == "sunk" else 10)
            bend("Neck", Vector((1, 0, 0)), 35 if stage == "sunk" else 22)       # 고개를 깊이 떨궈요
            for s, sx in (("Left", 0.05), ("Right", -0.05)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, sh + Vector((sx, -0.18, -0.45)), Vector((sx * 10, 0.5, -0.4)))
        else:
            bend("Spine", Vector((1, 0, 0)), -10)
            bend("Neck", Vector((1, 0, 0)), -24 if stage == "up" else -16)      # 고개를 젖혀요
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                spread = 0.42 if stage == "up" else 0.36                        # 떨 때 팔이 살짝 오므라들어요
                arm(s, sh + Vector((sx * spread, -0.18, -0.3)), Vector((sx, 0.4, -0.5)))
    make_action("Spawn", [(0, "sunk"), (4, "sunk"), (12, "half"), (18, "up"), (21, "shiver"), (23, "up"), (28, "idle")], float_spawn)
    rig.animation_data.action = None
    reset_pose()

# 목탁승 (knock): 예고 1.5초(45프레임) 동안 왼손 목탁을 오른손 채로 다섯 번 두드려요 (그대로 재생)
if "knock" in EXTRAS:
    def knock_pose(hit):
        bend("Spine", Vector((1, 0, 0)), 6)
        bend("Neck", Vector((1, 0, 0)), 10)
        hold_moktak()
        chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
        target = chest + (Vector((0.02, -0.33, -0.08)) if hit else Vector((-0.12, -0.25, 0.14)))   # 목탁 위 · 위로 든 채
        arm("Right", target, Vector((-1, 0.3, -0.4)))
    keys = [(0, False)]
    for k in range(5):
        keys += [(4 + k * 8, True), (8 + k * 8, False)]
    keys.append((45, False))
    make_action("Knock", keys, knock_pose)
    rig.animation_data.action = None
    reset_pose()

# 파계승 (staffmoves): 선장 찌르기(Thrust)와 금강불괴(Stance)
#   Thrust: 선장을 허리춤으로 당겨 겨눴다가(예고 동안 멈춤) 무릎을 굽혀 앞으로 쭉 찔러요. 왼손은 대를 받쳐요
#   Stance: 말 타듯 다리를 벌려 앉고, 오른손은 선장을 세워 들고 왼손은 가슴 앞에 세워 반합장 (2초 동안 그대로)
if "staffmoves" in EXTRAS:
    def staff_support():
        g, d = blade_now()
        arm("Left", g + d * 0.35, Vector((1, 0.2, -1)))                      # 왼손으로 대를 받쳐요

    def thrust_pose(stage):
        # 찌르기: 선장을 허리 높이에서 앞으로 수평하게 겨눈 준비 자세 그대로, 같은 선을 따라 일직선으로 쭉 찔러요
        #   (몸통은 틀지 않고, 엉덩이 높이도 같게 — 손과 선장 끝이 한 줄 위에서만 움직여요)
        if stage == "idle":
            idle_end_pose()
            return
        line_x, line_z = SH_R.x + 0.06, SH_R.z - 0.42                      # 찌르는 선 (월드)
        if stage == "load":
            lower_hips(-0.16, FEET, dy=0.06)
            wrist = Vector((line_x, SH_R.y + 0.12, line_z))                  # 허리춤으로 당겨 겨눠요
        else:
            lower_hips(-0.16, FEET, dy=-0.16)                                # 앞으로 내딛어요
            wrist = Vector((line_x, SH_R.y - 0.72, line_z))                  # 같은 선을 따라 쭉
        bend("Spine", Vector((1, 0, 0)), 6)
        arm("Right", wrist, Vector((-1, 0.3, -0.6)))
        point_blade(Vector((0, -1, 0)), wrist_bend=90)                       # 선장은 늘 정면 수평
        staff_support()
    make_action("Thrust", [(0, "load"), (4, "hit"), (14, "hit"), (24, "idle")], thrust_pose)

    def stance_pose(_):
        feet = {"Left": FEET["Left"] + Vector((0.12, 0, 0)), "Right": FEET["Right"] + Vector((-0.12, 0, 0))}
        lower_hips(-0.28, feet)
        sh = pb["RightArm"].head.copy()
        arm("Right", sh + Vector((-0.12, -0.15, -0.3)), Vector((-1, 0.3, -0.5)))
        point_blade(Vector((0, -0.1, 1)), wrist_bend=90)                     # 선장을 곧게 세워요
        chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
        arm("Left", chest + Vector((0.02, -0.28, 0.05)), Vector((1, 0.4, -1)))   # 반합장
    make_action("Stance", [(0, None), (10, None)], stance_pose)
    rig.animation_data.action = None
    reset_pose()

# 파계승 혜광 (abbotmoves): 선장 휘두르기 · 금강불괴 · 염주 던지기
#   StaffSweep: 선장을 오른쪽 뒤로 감아 겨눴다가(예고 동안 멈춤) 허리째 돌려 앞을 넓게(150°) 가로로 휩쓸어요
#   Vajra: 예고 3초 동안 말 탄 자세로 선장을 세워 반합장(첫 자세로 멈춤) → 판정에 선장을 땅에 내리꽂으며 두 팔을 벌려요
#   Beads: 왼손으로 염주를 뒤로 당겼다가(예고 동안 멈춤) 앞으로 휙 던져요
if "abbotmoves" in EXTRAS:
    def staff_sweep_pose(stage):
        if stage == "idle":
            idle_end_pose()
            return
        twist, wrist, lift, dy, staff = {
            "load": (-55, Vector((-0.7, 0.35, 0.0)), -0.14, 0.06, Vector((-0.3, 1, 0.1))),
            "hit": (0, Vector((-0.25, -0.9, -0.05)), -0.22, -0.1, Vector((-0.4, -1, 0))),
            "follow": (55, Vector((0.6, -0.6, -0.05)), -0.22, -0.1, Vector((1, -0.35, 0))),
            "end": (60, Vector((0.75, 0.0, -0.2)), -0.16, -0.05, Vector((0.8, 0.6, -0.1))),
        }[stage]
        # 2칸 내디디며 휘둘러요: 예고 동안 왼발을 살짝 들고 있다가 판정에 앞으로 크게 내딛고(엉덩이도 앞으로), 오른발은 뒤에 버텨요
        step = {"load": (Vector((0.02, 0.06, 0.14)), 0.0), "hit": (Vector((0.04, -0.5, 0)), 0.12), "follow": (Vector((0.04, -0.5, 0)), 0.12), "end": (Vector((0.02, -0.4, 0)), 0.08)}[stage]
        feet = {"Left": FEET["Left"] + step[0], "Right": FEET["Right"] + Vector((0, step[1], 0))}
        lower_hips(lift - (0.04 if stage != "load" else 0), feet, dy=dy - (0.22 if stage != "load" else 0))
        bend("Spine", UP, twist * 0.6); bend("Spine1", UP, twist * 0.4)
        sh = pb["RightArm"].head.copy()
        arm("Right", sh + wrist.normalized() * REACH * 0.85, Vector((0.2, 0.5, -1)))
        point_blade(staff, wrist_bend=60)
        g, d = blade_now()
        arm("Left", g + d * 0.4, Vector((1, 0.3, -0.8)))                    # 왼손으로 대를 함께 잡아요
    make_action("StaffSweep", [(0, "load"), (4, "hit"), (7, "follow"), (11, "end"), (16, "end"), (26, "idle")], staff_sweep_pose)

    def staff_two_hands(right_wrist, staff_dir, pole=Vector((-1, 0.3, -0.5))):
        arm("Right", right_wrist, pole)
        point_blade(staff_dir, wrist_bend=90)
        g, d = blade_now()
        arm("Left", g + d * 0.35, Vector((1, 0.3, -0.6)))                   # 왼손은 오른손 위로 대를 함께 잡아요

    def vajra_pose(stage):
        # 금강불괴: 예고 3초 동안 말 탄 자세로 선장을 세워 반합장(첫 자세로 멈춤) →
        #   판정 순간 선장을 두 손으로 머리 위로 번쩍 들었다가 → 무릎을 깊게 굽히며 앞 땅에 내리꽂아 충격파를 터뜨려요
        if stage == "idle":
            idle_end_pose()
            return
        feet = {"Left": FEET["Left"] + Vector((0.14, 0, 0)), "Right": FEET["Right"] + Vector((-0.14, 0, 0))}
        if stage == "stance":
            lower_hips(-0.3, feet)
            sh = pb["RightArm"].head.copy()
            arm("Right", sh + Vector((-0.12, -0.15, -0.3)), Vector((-1, 0.3, -0.5)))
            point_blade(Vector((0, -0.1, 1)), wrist_bend=90)                # 선장을 곧게 세워요
            chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
            arm("Left", chest + Vector((0.02, -0.28, 0.05)), Vector((1, 0.4, -1)))   # 반합장
        elif stage == "lift":
            lower_hips(-0.08, feet)
            bend("Spine", Vector((1, 0, 0)), -10)                           # 가슴을 젖혀요
            chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
            staff_two_hands(chest + Vector((-0.04, -0.22, 0.35)), Vector((0, 0.1, 1)))    # 두 손으로 선장을 번쩍 들어 올려요 (고리는 위)
        else:
            deep = 1.0 if stage == "slam" else 0.92
            lower_hips(-0.42 * deep, feet, dy=-0.06)
            bend("Spine", Vector((1, 0, 0)), 30 * deep)                     # 깊게 숙여 내리꽂아요
            chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2
            staff_two_hands(chest + Vector((-0.04, -0.4, -0.5)), Vector((0, 0.28, 1)))   # 선장 밑동을 앞 땅에 내리꽂아요 (고리는 위로 서요)
    make_action("Vajra", [(0, "stance"), (2, "lift"), (4, "slam"), (14, "hold"), (24, "idle")], vajra_pose)

    def beads_pose(stage):
        # 염주 던지기: 왼손으로 염주를 머리 뒤로 높이 치켜 감았다가(예고 동안 멈춤) 몸을 앞으로 숙이며 위에서 아래로 내던져요
        if stage == "idle":
            idle_end_pose()
            return
        sh = pb["RightArm"].head.copy()
        if stage == "load":
            lower_hips(-0.08, FEET, dy=0.06)
            bend("Spine", Vector((1, 0, 0)), -12); bend("Spine", UP, 28)    # 젖히며 왼어깨를 뒤로
            sl = pb["LeftArm"].head.copy()
            arm("Left", sl + Vector((0.12, 0.3, 0.4)), Vector((1, 0.6, 0.2)))        # 염주를 머리 뒤 위로
        else:
            lower_hips(-0.2, FEET, dy=-0.14)
            bend("Spine", Vector((1, 0, 0)), 22); bend("Spine", UP, -22)    # 숙이며 왼어깨를 앞으로
            sl = pb["LeftArm"].head.copy()
            arm("Left", sl + Vector((-0.12, -1, -0.35)).normalized() * REACH * 1.02, Vector((1, 0, -1)))   # 앞 아래로 쭉
        arm("Right", pb["RightArm"].head + Vector((-0.12, -0.12, -0.38)), Vector((-1, 0.3, -0.5)))
        point_blade(Vector((0, -0.1, 1)), wrist_bend=90)                    # 선장은 오른손에 세운 채
    make_action("Beads", [(0, "load"), (3, "throw"), (12, "throw"), (22, "idle")], beads_pose)
    rig.animation_data.action = None
    reset_pose()

# 혈불 (buddhamoves · sixarms): 피 흘리는 거대 불상. 느리고 무거운 움직임
#   sixarms: 2페이즈에 갈라져 나오는 팔 넷 — 몸의 두 팔 메시를 복사해 가슴(Spine2) 뒤에서 위·아래로 부채처럼 펼쳐 붙여요.
#            이름에 "_Phase2_"가 있어 Unity가 2페이즈 전까지 숨겨요
#   Attack(목탁승 부르기): 오른손을 들어 손짓해 불러요
#   Palm(손바닥 내려찍기): 오른손바닥을 머리 위로 치켜든 채(예고 1초 동안 멈춤) → 앞 땅으로 내려찍어요
#   Chant(염불·반복): 합장하고 고개를 숙여 읊조려요 (판정 동안 되풀이)
#   Rain(피의 비·반복): 두 팔을 하늘로 벌려 들어요
#   Arms(팔 휘두르기, 2페이즈): 허리째 좌→우→좌로 세 번 휘둘러요 (0.45초 = 13.5프레임 간격)
if "sixarms" in EXTRAS:
    def arm_piece(side):
        """그 쪽 위팔·아래팔·손에 붙은 몸 메시 조각을 떼어 복사해요 (기본 자세 그대로)."""
        names = {f"{side}Arm", f"{side}ForeArm", f"{side}Hand"}
        idx = {body.vertex_groups[n].index for n in names if n in body.vertex_groups}
        keep = set()
        for v in body.data.vertices:
            w = sum(g.weight for g in v.groups if g.group in idx)
            if w > 0.5:
                keep.add(v.index)
        bm = bmesh.new(); bm.from_mesh(body.data)
        bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
        me = bpy.data.meshes.new(f"{NAME}_arm_{side}"); bm.to_mesh(me); bm.free()
        me.materials.clear()
        for m in body.data.materials:
            me.materials.append(m)
        return me

    chest = pb["Spine2"].head.copy()
    for side, sx in (("Left", 1), ("Right", -1)):
        sh = B[f"{side}Arm"].head_local.copy()
        src = arm_piece(side)
        for tier, (lift, back) in enumerate(((40, 0.18), (-35, 0.12))):   # 위로 하나, 아래로 하나
            o = bpy.data.objects.new(f"{NAME}_Phase2_Arm_{side}_{tier}", src.copy())
            bpy.context.scene.collection.objects.link(o)
            # 어깨를 축으로 위/아래로 돌리고 조금 뒤·안쪽으로 붙여요 (원래 팔과 겹치지 않게)
            rot = Quaternion(Vector((0, 1, 0)), math.radians(-sx * lift))
            for v in o.data.vertices:
                v.co = sh + rot @ (v.co - sh) + Vector((-sx * 0.04, back, 0.05 if lift > 0 else -0.05))
            o.vertex_groups.new(name="Spine2").add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
            o.parent = rig
            o.modifiers.new("Armature", 'ARMATURE').object = rig
            weapon_objs.append(o)
    print("ENEMY six arms added")

if "buddhamoves" in EXTRAS:
    def heavy(lift=0.0, lean=0.0):
        lower_hips(lift, FEET)
        if lean:
            bend("Spine", Vector((1, 0, 0)), lean)

    def attack_pose(stage):
        if stage == "idle":
            idle_end_pose(); return
        heavy(-0.04)
        sh = pb["RightArm"].head.copy()
        arm("Right", sh + Vector((-0.1, -0.35, 0.35 if stage == "up" else 0.15)), Vector((-1, 0, -0.5)))
    make_action("Attack", [(0, "up"), (8, "wave"), (16, "up"), (28, "idle")], attack_pose)

    def palm_pose(stage):
        if stage == "idle":
            idle_end_pose(); return
        sh = pb["RightArm"].head.copy()
        if stage == "raise":
            heavy(0.0, -10)
            arm("Right", sh + Vector((-0.05, -0.15, 1)).normalized() * REACH * 0.98, Vector((-1, 0.3, 0)))   # 머리 위로 치켜들어요
        else:
            deep = 1.0 if stage == "slam" else 0.9
            heavy(-0.25 * deep, 30 * deep)
            arm("Right", Vector((sh.x * 0.6, sh.y - 1.1, 0.25)), Vector((-1, 0.3, 0.3)))   # 앞 땅을 내려찍어요
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((0.15, -0.1, -0.6)), Vector((1, 0.3, -0.3)))
    make_action("Palm", [(0, "raise"), (4, "slam"), (14, "hold"), (26, "idle")], palm_pose)

    # Palm3 (2페이즈 손바닥 연타): 오른손 → 왼손 → 오른손으로 0.4초(12프레임) 간격 세 번 내려찍어요.
    #   첫 자세(오른손 치켜듦)로 예고를 버티다가 판정부터 재생 — 게임의 연타 원(첫 손바닥 뒤 0.4·0.8초)과 박자가 같아요
    def palm3_pose(val):
        stage, side = val
        if stage == "idle":
            idle_end_pose(); return
        sx = 1 if side == "Right" else -1                       # 오른쪽 = -X
        other = "Left" if side == "Right" else "Right"
        sh = pb[f"{side}Arm"].head.copy()
        if stage == "raise":
            heavy(0.0, -10)
            bend("Spine", UP, sx * 10)                                  # 치켜드는 쪽 어깨를 뒤로
            arm(side, sh + Vector((-0.05 * sx, -0.15, 1)).normalized() * REACH * 0.98, Vector((-sx, 0.3, 0)))
        else:
            heavy(-0.25, 30)
            bend("Spine", UP, -sx * 12)                                 # 내려찍는 쪽 어깨가 앞으로
            arm(side, Vector((sh.x * 0.6, sh.y - 1.1, 0.25)), Vector((-sx, 0.3, 0.3)))
        so = pb[f"{other}Arm"].head.copy()
        arm(other, so + Vector((0.15 * sx, -0.1, -0.6)), Vector((sx, 0.3, -0.3)))
    make_action("Palm3", [(0, ("raise", "Right")), (4, ("slam", "Right")), (10, ("raise", "Left")), (16, ("slam", "Left")),
                          (22, ("raise", "Right")), (28, ("slam", "Right")), (38, ("slam", "Right")), (50, ("idle", "Right"))], palm3_pose)

    def chant_pose(t):
        heavy(-0.03 * (1 + math.sin(2 * math.pi * t)), 8)
        bend("Neck", Vector((1, 0, 0)), 14)
        chest_ = (pb["LeftArm"].head + pb["RightArm"].head) / 2
        for s, sx in (("Left", 0.04), ("Right", -0.04)):
            arm(s, chest_ + Vector((sx, -0.32, -0.1)), Vector((sx * 30, 0.4, -1)))       # 합장
    make_action("Chant", [(f, f / 30) for f in range(0, 31, 5)], chant_pose)

    def rain_pose(t):
        heavy(-0.02 * math.sin(2 * math.pi * t), -12)
        bend("Neck", Vector((1, 0, 0)), -18)                                # 하늘을 올려다봐요
        for s, sx in (("Left", 1), ("Right", -1)):
            sh = pb[f"{s}Arm"].head.copy()
            arm(s, sh + Vector((sx * 0.55, -0.1, 0.85)).normalized() * REACH * 0.98, Vector((sx, 0.3, 0)))
    make_action("Rain", [(f, f / 30) for f in range(0, 31, 5)], rain_pose)

    def arms_pose(side):
        if side == "idle":
            idle_end_pose(); return
        sign = 1 if side == "L" else -1                                     # L: 왼쪽으로 휘둘러 나간 끝
        heavy(-0.15, 10)
        bend("Spine", UP, 40 * sign); bend("Spine1", UP, 20 * sign)
        for s, sx in (("Left", 1), ("Right", -1)):
            sh = pb[f"{s}Arm"].head.copy()
            arm(s, sh + Vector((sx * 0.8 + sign * 0.3, -0.6, -0.05)).normalized() * REACH * 0.98, Vector((0, 0.3, -1)))
    make_action("Arms", [(0, "R"), (7, "L"), (14, "L"), (20, "R"), (27, "R"), (34, "L"), (41, "L"), (52, "idle")], arms_pose)

    # 혈수(Grab, 2페이즈): 예고(약 0.6초) 내내 오른팔을 혈불 앞(조준 방향)으로 곧게 쭉 뻗고 손바닥을 앞으로 펼친 자세로 버티다가
    #   (첫 자세로 멈춤) → 판정 뒤 잠깐 그대로 → 움켜쥐고 가슴께로 확 끌어당겨요.
    #   손바닥(RightHand 뼈)에서 앞으로 나가는 소용돌이는 Unity가 그려요 (아트2의 InkBloodBuddhaFx)
    ARM_STRETCH = (0.3, 0.45)   # 위팔·아래팔을 이만큼(m, 키 3.2 기준) 더 늘여요 → 팔이 원래의 약 1.8배
    def grab_pose(stage):
        if stage == "idle":
            idle_end_pose(); return
        sh = pb["RightArm"].head.copy()
        if stage == "reach":
            heavy(-0.16, 24)                                                # 상체를 앞으로 크게 숙여요
            bend("RightShoulder", UP, 38)                                   # 오른 어깨부터 앞으로 크게 내밀고
            bpy.context.view_layer.update()
            sh = pb["RightArm"].head.copy()
            arm("Right", sh + Vector((0.08, -1, 0)).normalized() * REACH * 1.1, Vector((-1, 0, -1)))   # 팔꿈치를 다 펴서 정면으로, 땅과 수평
            bend("RightHand", Vector((1, 0, 0)), -75)                       # 손목을 젖혀 손바닥이 앞을 봐요 (손가락은 위)
            # 핏빛 기운으로 팔이 몸 길이만큼 쭈욱 늘어나요: 팔꿈치·손목 뼈를 팔 방향으로 밀어내요 (뼈 축이 팔 방향이 아니라 크기로는 못 늘여요)
            bpy.context.view_layer.update()
            along = (pb["RightHand"].head - pb["RightArm"].head).normalized()
            for n, extra in (("RightForeArm", ARM_STRETCH[0]), ("RightHand", ARM_STRETCH[1])):
                m = pb[n].matrix.copy()
                m.translation += along * extra
                pb[n].matrix = m
                bpy.context.view_layer.update()
        else:
            heavy(-0.1, 4)
            bend("Spine", UP, -15)
            arm("Right", sh + Vector((-0.08, -0.15, -0.15)), Vector((-1, 0.3, -0.6)))   # 움켜쥐고 가슴께로 끌어당겨요
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((0.2, -0.05, -0.55)), Vector((1, 0.3, -0.3)))
    make_action("Grab", [(0, "reach"), (9, "reach"), (14, "pull"), (22, "pull"), (32, "idle")], grab_pose)
    rig.animation_data.action = None
    reset_pose()


# 장강 수적 (rivermoves): 무기에 따라 동작을 만들어요
#   갈고리 칼(수적 졸개) DoubleSlash: 치켜든 채 버티다가(예고) 앞으로 내려베고 0.3초(9프레임) 뒤 반대로 한 번 더
#   등패수(dao+rattan) Attack: 방패를 앞에 세운 채 한 번 내디디며 베기 · ShieldRoll: 왼팔을 몸 앞으로 감았다가 앞으로 뿌려 방패를 굴려요
#   작살수 HarpoonThrust: 두 손으로 작살을 허리춤에 겨눴다가 앞으로 쭉 찔러 뻗고 → 잡아채듯 뒤로 당겨요
#   쇠닻 거한 AnchorThrow: 닻을 머리 뒤로 치켜 감았다가 앞으로 던지고, 두 팔을 앞으로 뻗어 날아가는 자세로 버텨요 · AnchorSwing: 앞을 반원으로 휩쓸어요
#   수귀 Emerge: 땅속에서 두 팔을 뻗으며 솟구쳐 붙잡아요 · Drag: 붙잡은 채 뒷걸음질 (반복)
#   깃발수 Wave: 깃대를 두 손으로 들고 좌우로 크게 세 번 흔들어요 (예고 1.5초 동안 그대로 재생)
if "rivermoves" in EXTRAS:
    def free_left_back():
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((0.25, 0.12, -0.5)), Vector((1, 0.3, -0.3)))

    def shield_guard():
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((0.12, -0.5, -0.14)), Vector((1, 0.3, -0.5)))   # 방패를 몸 앞으로 내밀어요

    SLASH = {   # twist, wrist, reach, lift, dy, blade
        "load": (-35, Vector((-0.45, 0.15, 0.9)), 0.9, -0.06, 0.05, Vector((-0.2, 0.3, 1))),
        "cut1": (25, Vector((0.35, -0.75, -0.6)), 0.95, -0.2, -0.12, Vector((0.6, -0.5, -1))),     # 오른 위에서 왼 아래로 내려베요
        "reload": (30, Vector((0.4, -0.15, 0.8)), 0.85, -0.14, -0.1, Vector((0.3, 0.3, 1))),
        "cut2": (-25, Vector((-0.45, -0.7, -0.45)), 0.95, -0.22, -0.2, Vector((-0.9, -0.5, -0.6))),   # 왼 위에서 오른 아래로 되베요
    }

    def slash_pose(val):
        stage, left = val
        if stage == "idle":
            idle_end_pose(); return
        twist, wrist, reach, lift, dy, blade_dir = SLASH[stage]
        step = stage != "load"
        feet = {"Left": FEET["Left"] + (Vector((0, -0.35, 0)) if step else Vector()), "Right": FEET["Right"]}
        lower_hips(lift, feet, dy=dy)
        bend("Spine", UP, twist * 0.6); bend("Spine1", UP, twist * 0.4)
        sh = pb["RightArm"].head.copy()
        pole = Vector((-0.6, 0.3, 0)) if wrist.z > 0.3 else Vector((0.2, 0.5, -1))
        arm("Right", sh + wrist.normalized() * REACH * reach, pole)
        point_blade(blade_dir, wrist_bend=80)
        left()

    if "hooksword" in WEAPONS:
        make_action("DoubleSlash", [(0, ("load", free_left_back)), (3, ("cut1", free_left_back)), (7, ("reload", free_left_back)),
                                    (12, ("cut2", free_left_back)), (18, ("cut2", free_left_back)), (28, ("idle", None))], slash_pose)
    if "rattan" in WEAPONS:
        make_action("Attack", [(0, ("load", shield_guard)), (4, ("cut1", shield_guard)), (14, ("cut1", shield_guard)), (24, ("idle", None))], slash_pose)

        def roll_pose(stage):
            if stage == "idle":
                idle_end_pose(); return
            sl = pb["LeftArm"].head.copy()
            if stage == "wind":
                lower_hips(-0.14, FEET, dy=0.04)
                bend("Spine", UP, 35)                                         # 왼 어깨를 안으로 감아요
                arm("Left", sl + Vector((-0.4, -0.05, -0.3)), Vector((1, 0.5, -0.5)))
            else:
                feet = {"Left": FEET["Left"] + Vector((0, -0.35, 0)), "Right": FEET["Right"]}
                lower_hips(-0.2, feet, dy=-0.12)
                bend("Spine", UP, -30)
                arm("Left", sl + Vector((0.3, -0.6, -0.15)), Vector((1, 0.2, -0.6)))   # 앞으로 뿌려요
            sh = pb["RightArm"].head.copy()
            arm("Right", sh + Vector((-0.2, 0.1, -0.55)), Vector((-1, 0.3, -0.3)))
            point_blade(Vector((-0.2, -1, -0.5)), wrist_bend=60)
        make_action("ShieldRoll", [(0, "wind"), (4, "throw"), (14, "throw"), (24, "idle")], roll_pose)

        # 대기: 플레이어 대기를 옮긴 것은 왼팔이 방패를 허리 앞에 쟁반처럼 눕혀 몸을 가로질렀어요.
        #   방패를 몸 앞에 세워 버티고(막는 자세와 같은 왼팔), 칼은 오른 허리춤에 낮게, 숨 쉬며 조금 흔들려요
        def rattan_idle(t):
            breath = math.sin(2 * math.pi * t)
            feet = {"Left": FEET["Left"] + Vector((0, -0.12, 0)), "Right": FEET["Right"] + Vector((0, 0.08, 0))}
            lower_hips(-0.1 - 0.012 * breath, feet, dy=0.02)
            bend("Spine", Vector((1, 0, 0)), 5 + 1.5 * breath)
            sl = pb["LeftArm"].head.copy()
            arm("Left", sl + Vector((0.12, -0.5, -0.14 + 0.01 * breath)), Vector((1, 0.3, -0.5)))
            sh = pb["RightArm"].head.copy()
            arm("Right", sh + Vector((-0.12, -0.15, -0.5)), Vector((-1, 0.3, -0.3)))
            point_blade(Vector((-0.1, -0.6, -1)), wrist_bend=60)                    # 칼끝은 앞 아래로
        old_idle = bpy.data.actions.get("Idle")
        if old_idle is not None:
            bpy.data.actions.remove(old_idle)
        make_action("Idle", [(f, f / 60) for f in range(0, 61, 6)], rattan_idle)

    if "harpoon" in WEAPONS:
        def harpoon_pose(stage):
            if stage == "idle":
                idle_end_pose(); return
            sh = pb["RightArm"].head.copy()
            if stage == "load":
                lower_hips(-0.12, FEET, dy=0.08)
                bend("Spine", UP, -20)
                arm("Right", sh + Vector((-0.05, 0.22, -0.42)), Vector((-1, 0.3, -0.5)))   # 허리춤 뒤
                point_blade(Vector((0, -1, 0.08)), wrist_bend=90)
            elif stage == "thrust":
                # 팔을 멀리 뻗어 찔러요 (작살이 늘어나는 게 아니라): 앞발을 크게 내딛고 허리를 낮춰 몸을 앞으로 밀고, 오른팔을 어깨높이에서 끝까지 펴요
                feet = {"Left": FEET["Left"] + Vector((0, -0.7, 0)), "Right": FEET["Right"] + Vector((0, 0.15, 0))}
                lower_hips(-0.3, feet, dy=-0.42)
                bend("Spine", Vector((1, 0, 0)), 24); bend("Spine", UP, 12)                       # 오른 어깨를 앞으로 밀어 넣어요
                sh = pb["RightArm"].head.copy()                                                  # 숙이고 돌린 뒤의 어깨에서
                arm("Right", sh + Vector((0.1, -REACH * 1.02, 0.0)), Vector((-1, 0.3, -0.5)))
                point_blade(Vector((0, -1, -0.05)), wrist_bend=180)                                # 작살촉이 곧장 앞을 봐요
            else:   # yank: 잡아채 뒤로 당겨요
                lower_hips(-0.14, FEET, dy=0.12)
                bend("Spine", Vector((1, 0, 0)), -8)
                arm("Right", sh + Vector((-0.05, 0.3, -0.3)), Vector((-1, 0.3, -0.5)))
                point_blade(Vector((0, -1, 0.15)), wrist_bend=90)
            g, d = blade_now()
            arm("Left", g + d * 0.5, Vector((1, 0.3, -0.5)))               # 왼손은 자루 앞쪽
        make_action("HarpoonThrust", [(0, "load"), (4, "thrust"), (12, "thrust"), (17, "yank"), (22, "yank"), (32, "idle")], harpoon_pose)

    if "anchor" in WEAPONS:
        def anchor_throw(stage):
            if stage == "idle":
                idle_end_pose(); return
            sh = pb["RightArm"].head.copy()
            sl = pb["LeftArm"].head.copy()
            if stage == "wind":
                lower_hips(-0.16, FEET, dy=0.1)
                bend("Spine", Vector((1, 0, 0)), -12); bend("Spine", UP, -25)
                arm("Right", sh + Vector((-0.05, 0.35, 0.75)), Vector((-1, 0.3, 0)))   # 머리 뒤로 치켜 감아요
                point_blade(Vector((0, 0.8, -0.6)), wrist_bend=60)                     # 닻이 등 뒤로 늘어져요
                arm("Left", sl + Vector((0.2, -0.3, 0.05)), Vector((1, 0.3, -0.5)))
            elif stage == "throw":
                feet = {"Left": FEET["Left"] + Vector((0, -0.5, 0)), "Right": FEET["Right"]}
                lower_hips(-0.22, feet, dy=-0.25)
                bend("Spine", Vector((1, 0, 0)), 22); bend("Spine", UP, 20)
                arm("Right", sh + Vector((0.05, -0.85, 0.05)), Vector((-1, 0, -0.5)))
                point_blade(Vector((0, -1, 0.2)), wrist_bend=60)
                arm("Left", sl + Vector((0.3, 0.2, -0.4)), Vector((1, 0.3, -0.3)))
            else:   # fly: 사슬에 끌려 날아가요 — 두 팔을 앞으로 뻗고 몸을 숙여 다리는 뒤로
                feet = {"Left": FEET["Left"] + Vector((0, 0.45, 0.35)), "Right": FEET["Right"] + Vector((0, 0.3, 0.15))}
                lower_hips(0.05, feet, dy=-0.1)
                bend("Spine", Vector((1, 0, 0)), 30)
                arm("Right", sh + Vector((0.05, -0.85, -0.05)), Vector((-1, 0, -0.5)))
                point_blade(Vector((0, -1, 0)), wrist_bend=60)
                arm("Left", sl + Vector((-0.05, -0.85, -0.05)), Vector((1, 0, -0.5)))
        make_action("AnchorThrow", [(0, "wind"), (4, "throw"), (8, "fly"), (30, "fly"), (40, "idle")], anchor_throw)

        def anchor_swing(stage):
            if stage == "idle":
                idle_end_pose(); return
            twist, wrist, lift, want = {
                "load": (-55, Vector((-0.75, 0.4, -0.2)), -0.16, Vector((-0.6, 0.6, -0.4))),
                "hit": (0, Vector((-0.2, -0.95, -0.2)), -0.24, Vector((0, -1, -0.25))),
                "follow": (55, Vector((0.65, -0.55, -0.2)), -0.24, Vector((1, -0.4, -0.25))),
                "end": (60, Vector((0.7, 0.1, -0.4)), -0.16, Vector((0.6, 0.5, -0.8))),
            }[stage]
            lower_hips(lift, FEET, dy=-0.05)
            bend("Spine", UP, twist * 0.6); bend("Spine1", UP, twist * 0.4)
            sh = pb["RightArm"].head.copy()
            arm("Right", sh + wrist.normalized() * REACH * 0.9, Vector((0.2, 0.5, -1)))
            point_blade(want, wrist_bend=60)
            g, d = blade_now()
            arm("Left", g + d * 0.25, Vector((1, 0.3, -0.8)))              # 왼손도 닻대를 잡아요
        make_action("AnchorSwing", [(0, "load"), (4, "hit"), (7, "follow"), (11, "end"), (16, "end"), (26, "idle")], anchor_swing)

    if "ghostmoves" in EXTRAS:
        def ghost_pose(val):
            stage, sink = val
            if stage == "idle":
                idle_end_pose(); return
            down = Vector((0, 0, -sink))
            if stage == "drag":
                lower_hips(-0.06 - sink, {"Left": FEET["Left"] + down, "Right": FEET["Right"] + down}, dy=0.06)
            else:
                lower_hips(-sink, {"Left": FEET["Left"] + down, "Right": FEET["Right"] + down})
            bend("Spine", Vector((1, 0, 0)), 18 if stage != "drag" else -10)
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                if stage == "rise":
                    arm(s, sh + Vector((sx * 0.1, -0.35, 0.75)), Vector((sx, 0.3, 0)))      # 하늘로 손을 뻗으며 솟구쳐요
                else:
                    arm(s, sh + Vector((-sx * 0.05, -0.75, -0.15)), Vector((sx, 0.3, -0.6)))   # 앞을 움켜쥐어요
        make_action("Emerge", [(0, ("rise", 1.9)), (5, ("rise", 0.4)), (8, ("grab", 0.0)), (12, ("grab", 0.0)), (20, ("grab", 0.0))], ghost_pose)

        def drag_pose(phase):
            # 붙잡은 채 뒷걸음질: 한 발씩 뒤로 (16프레임 반복)
            back = "Left" if phase in (0, 1) else "Right"
            k = (0.0, 1.0, 0.0, 1.0)[phase]
            feet = {}
            for s in ("Left", "Right"):
                if s == back:
                    feet[s] = FEET[s] + Vector((0, 0.3 * k, 0.15 * k))
                else:
                    feet[s] = FEET[s] + Vector((0, -0.15, 0))
            lower_hips(-0.07, feet, dy=0.08)
            bend("Spine", Vector((1, 0, 0)), -12)
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, sh + Vector((-sx * 0.05, -0.7, -0.2)), Vector((sx, 0.3, -0.6)))
        make_action("Drag", [(0, 0), (4, 1), (8, 2), (12, 3), (16, 0)], drag_pose)

    if "flagpole" in WEAPONS:
        def wave_pose(stage):
            if stage == "idle":
                idle_end_pose(); return
            tilt = {"up": 0.0, "left": 0.7, "right": -0.7}[stage]
            lower_hips(-0.08, FEET)
            bend("Spine", UP, -tilt * 15)
            sh = pb["RightArm"].head.copy()
            arm("Right", sh + Vector((0.15 + tilt * 0.15, -0.25, 0.5)), Vector((-1, 0.3, -0.3)))   # 오른손을 높이
            point_blade(Vector((tilt, -0.15, 1)), wrist_bend=90)
            g, d = blade_now()
            arm("Left", g - d * 0.45, Vector((1, 0.3, -0.5)))              # 왼손은 장대 아래쪽
        make_action("Wave", [(0, "up"), (6, "left"), (14, "right"), (22, "left"), (30, "right"), (38, "up"), (46, "idle")], wave_pose)
    rig.animation_data.action = None
    reset_pose()


# 탁랑객 (takrangmoves, 장강 고수 · 장검):
#   Slash(베기): 오른쪽 위로 감았다가 앞을 가로로 베어 넘겨요
#   WaveCut(물결 베기): 깊게 웅크려 칼을 오른쪽 뒤 낮게 감았다가 허리째 크게 휘둘러 왼쪽 위로 뿌려요 (검기가 날아가요)
#   Glide(탁류 보법): 낮게 웅크려 칼을 허리춤에 겨눈 채(예고) → 미끄러져 들어오며 앞으로 쭉 찔러요
#   Whirl(소용돌이, 반복): 머리 위에서 칼을 수평으로 빙빙 돌려요 → WhirlEnd: 칼끝을 앞 땅에 내리꽂아 물살을 터뜨려요
if "takrangmoves" in EXTRAS:
    TK = {   # twist, wrist, reach, lift, dy, blade, step(왼발 앞으로)
        "s_load": (-40, Vector((-0.5, 0.2, 0.8)), 0.9, -0.08, 0.05, Vector((-0.3, 0.5, 1)), 0.0),
        "s_cut": (40, Vector((0.6, -0.75, 0.0)), 0.95, -0.18, -0.12, Vector((1, -0.35, 0.0)), 0.35),
        "w_load": (-60, Vector((-0.7, 0.45, -0.3)), 0.9, -0.3, 0.1, Vector((-0.4, 0.85, -0.3)), 0.1),
        "w_mid": (0, Vector((0.0, -1, -0.1)), 0.95, -0.28, -0.12, Vector((0, -1, 0.0)), 0.45),
        "w_swing": (50, Vector((0.65, -0.6, 0.1)), 0.95, -0.26, -0.15, Vector((1, -0.25, 0.1)), 0.5),
        "w_end": (60, Vector((0.7, -0.2, 0.5)), 0.9, -0.2, -0.1, Vector((0.6, 0.3, 0.8)), 0.5),
        "g_ready": (-15, Vector((-0.2, 0.25, -0.35)), 0.8, -0.32, 0.08, Vector((0, -1, 0.05)), 0.2),
        "g_thrust": (10, Vector((0.05, -1, -0.05)), 1.0, -0.3, -0.3, Vector((0, -1, -0.02)), 0.55),
        "e_stab": (5, Vector((0.05, -0.55, -0.85)), 0.95, -0.35, -0.15, Vector((0, -0.3, -1)), 0.4),
    }

    def tk_pose(stage):
        if stage == "idle":
            idle_end_pose(); return
        twist, wrist, reach, lift, dy, blade_dir, step = TK[stage]
        feet = {"Left": FEET["Left"] + Vector((0, -step, 0)), "Right": FEET["Right"] + Vector((0, step * 0.3, 0))}
        lower_hips(lift, feet, dy=dy)
        bend("Spine", UP, twist * 0.6); bend("Spine1", UP, twist * 0.4)
        sh = pb["RightArm"].head.copy()
        pole = Vector((-0.6, 0.3, 0)) if wrist.z > 0.3 else Vector((0.2, 0.5, -1))
        fit_right(sh, [wrist.normalized() * REACH * reach, wrist.normalized() * REACH * reach * 0.9 + Vector((0, 0, 0.05))],
                  (pole, Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((-0.5, 0.4, -1))), blade_dir, wrist_bend=80)
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((0.3, 0.15, -0.45)), Vector((1, 0.3, -0.3)))   # 왼팔은 뒤로 뻗어 균형

    make_action("Slash", [(0, "s_load"), (4, "s_cut"), (12, "s_cut"), (22, "idle")], tk_pose)
    make_action("WaveCut", [(0, "w_load"), (3, "w_mid"), (5, "w_swing"), (9, "w_end"), (18, "w_end"), (28, "idle")], tk_pose)
    make_action("Glide", [(0, "g_ready"), (4, "g_thrust"), (12, "g_thrust"), (22, "idle")], tk_pose)
    make_action("WhirlEnd", [(0, "e_stab"), (3, "e_stab"), (12, "e_stab"), (22, "idle")], tk_pose)

    def whirl_pose(t):
        # 머리 위에서 손이 작은 원을 그리고 칼날은 수평으로 그 원을 따라 돌아요 (12프레임 한 바퀴, 반복)
        a = t * 2 * math.pi
        lower_hips(-0.12, FEET)
        sh = pb["RightArm"].head.copy()
        hand_at = sh + Vector((0.06 + 0.07 * math.cos(a), 0.07 * math.sin(a), REACH * 0.9))   # 손을 어깨 바로 위로 곧게 (아래팔이 서면 칼은 비틀기만으로 수평)
        arm("Right", hand_at, Vector((-1, 0.3, 0)))
        point_blade(Vector((-math.sin(a), math.cos(a), 0.0)), wrist_bend=0)
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((0.12, -0.32, 0.05)), Vector((1, 0.3, -0.6)))   # 왼손은 가슴 앞 검결
    make_action("Whirl", [(f, f / 12) for f in range(0, 13, 2)], whirl_pose)
    make_action("Ascend", [(0, "g_ready"), (18, "g_ready"), (30, "s_load"), (50, "s_load"), (60, "idle")], tk_pose)   # 2페이즈 전환: 웅크렸다가 칼을 치켜들고 일어서요
    rig.animation_data.action = None
    reset_pose()


# 수채주 철룡 (dragonmoves, 장강 보스 · 유성추):
#   MeteorSwing(유성추 휘두르기): 철구를 머리 위로 치켜 감은 채 버티다가(예고) 허리째 돌려 앞을 크게 가로로 휩쓸어요
#   MeteorSlam(유성추 내려찍기): 철구를 하늘로 높이 들어 올렸다가 앞 땅으로 내려찍어요
#   ChainThrow(사슬 당기기): 오른팔을 뒤로 당겨 겨눴다가 앞으로 던지듯 뻗고 → 두 손으로 잡아채 당겨요
#   Command(수하 부르기): 왼팔을 앞 위로 치켜들어 호령해요 (그대로 재생)
#   Stomp(물기둥): 오른 무릎을 높이 들었다가 땅을 굴러요
#   Roar(2페이즈 전환 · 배가 부서져요): 휘청 뒤로 물러났다가 가슴을 펴고 두 팔을 벌려 포효해요
if "dragonmoves" in EXTRAS:
    # 철구 사슬은 손에 붙은 곧은 막대라, 손 자리를 동작마다 정해 두고 손목을 끝까지 돌려 사슬 방향을 맞춰요
    # (Generic이라 손목·아래팔 한계가 없어요). 예전처럼 후보 중에서 고르면 손이 얼굴 앞에 와서 사슬이 얼굴을 지나갔어요
    GRIP_POLE = Vector((-1, 0.3, -0.5))

    def hold(off, want):
        arm("Right", pb["RightArm"].head.copy() + off, GRIP_POLE)
        point_blade(want, wrist_bend=180)

    def dragon_idle(t):
        # 대기: 다리를 벌리고 무릎을 조금 굽혀 버티고, 오른손은 옆구리에서 사슬을 쥐고(철구는 발 옆 땅에), 숨 쉬며 철구가 살짝 흔들려요
        breath = math.sin(2 * math.pi * t)
        feet = {"Left": FEET["Left"] + Vector((0.08, -0.05, 0)), "Right": FEET["Right"] + Vector((-0.08, 0.05, 0))}
        lower_hips(-0.12 - 0.015 * breath, feet, dy=0.02)
        bend("Spine", Vector((1, 0, 0)), 6 + 1.5 * breath)
        bend("Neck", Vector((1, 0, 0)), -4)
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((0.18, -0.12, -0.55)), Vector((1, 0.3, -0.3)))            # 왼팔은 허리 앞에 느슨히
        hold(Vector((-0.2, -0.08, -0.5)), Vector((-0.12 + 0.06 * math.sin(2 * math.pi * t + 1.0), -0.15, -1)))   # 사슬이 발 옆으로 늘어져요

    def dragon_pose(stage):
        if stage == "idle":
            dragon_idle(0.0); return
        sl = pb["LeftArm"].head.copy()
        left_guard = lambda: arm("Left", sl + Vector((0.25, -0.3, -0.3)), Vector((1, 0.3, -0.5)))
        if stage == "swing_load":
            lower_hips(-0.18, FEET, dy=0.08)
            bend("Spine", UP, -40); bend("Spine1", UP, -20)
            hold(Vector((-0.12, 0.2, 0.55)), Vector((-0.35, 0.6, 0.7)))                     # 철구를 머리 뒤 위로 치켜 감아요
            left_guard()
        elif stage in ("swing_hit", "swing_follow"):
            follow = stage == "swing_follow"
            feet = {"Left": FEET["Left"] + Vector((0, -0.4, 0)), "Right": FEET["Right"]}
            lower_hips(-0.26, feet, dy=-0.15)
            bend("Spine", UP, 10 + (45 if follow else 0)); bend("Spine1", UP, 5 + (20 if follow else 0))
            if follow:
                hold(Vector((0.45, -0.45, -0.12)), Vector((0.85, -0.5, -0.05)))            # 앞을 가로질러 왼쪽까지 휩쓸어요
            else:
                hold(Vector((-0.2, -0.55, -0.08)), Vector((-0.35, -1, -0.02)))             # 어깨높이로 앞을 향해 사슬이 뻗어요
            left_guard()
        elif stage == "slam_up":
            lower_hips(-0.06, FEET, dy=0.06)
            bend("Spine", Vector((1, 0, 0)), -14)
            hold(Vector((0.02, 0.0, 0.62)), Vector((0, 0.15, 1)))                          # 하늘로 높이
            arm("Left", sl + Vector((0.25, -0.3, 0.1)), Vector((1, 0.3, -0.5)))
        elif stage == "slam_down":
            feet = {"Left": FEET["Left"] + Vector((0, -0.45, 0)), "Right": FEET["Right"]}
            lower_hips(-0.32, feet, dy=-0.2)
            bend("Spine", Vector((1, 0, 0)), 28)
            hold(Vector((0.05, -0.55, -0.4)), Vector((0, -0.6, -1)))                       # 앞 땅으로 내려찍어요
            left_guard()
        elif stage == "chain_load":
            lower_hips(-0.14, FEET, dy=0.1)
            bend("Spine", UP, -30)
            hold(Vector((-0.22, 0.35, 0.0)), Vector((-0.2, 1, 0.05)))                      # 뒤로 당겨 겨눠요
            arm("Left", sl + Vector((0.1, -0.5, 0.0)), Vector((1, 0.3, -0.5)))             # 왼손은 앞을 가리켜요
        elif stage == "chain_throw":
            feet = {"Left": FEET["Left"] + Vector((0, -0.4, 0)), "Right": FEET["Right"]}
            lower_hips(-0.22, feet, dy=-0.18)
            bend("Spine", UP, 20); bend("Spine", Vector((1, 0, 0)), 12)
            hold(Vector((0.05, -0.6, 0.0)), Vector((0, -1, 0.05)))                         # 앞으로 던지듯 뻗어요
            left_guard()
        elif stage == "chain_pull":
            lower_hips(-0.24, FEET, dy=0.15)
            bend("Spine", Vector((1, 0, 0)), -12); bend("Spine", UP, -15)
            hold(Vector((-0.05, 0.15, -0.3)), Vector((0, -1, -0.1)))
            g, d = blade_now()
            arm("Left", g + d * 0.25, Vector((1, 0.3, -0.5)))                               # 두 손으로 잡아채요
        elif stage == "command":
            lower_hips(-0.05, FEET)
            bend("Spine", Vector((1, 0, 0)), -8); bend("Neck", Vector((1, 0, 0)), -15)
            arm("Left", sl + Vector((0.15, -0.45, 0.6)), Vector((1, 0.3, 0)))               # 왼팔을 치켜 호령
            hold(Vector((-0.18, 0.0, -0.52)), Vector((-0.1, 0, -1)))                        # 사슬은 늘어뜨린 채
        elif stage in ("stomp_up", "stomp_down"):
            up = stage == "stomp_up"
            feet = {"Left": FEET["Left"], "Right": FEET["Right"] + (Vector((0, -0.15, 0.55)) if up else Vector((0, -0.2, 0)))}
            lower_hips(-0.05 if up else -0.34, feet, dy=-0.05)
            bend("Spine", Vector((1, 0, 0)), -6 if up else 18)
            arm("Left", sl + Vector((0.4, 0.0, 0.1 if up else -0.3)), Vector((1, 0.3, -0.3)))
            hold(Vector((-0.4, 0.0, 0.1 if up else -0.3)), Vector((-0.3, 0.2, -1)))
        elif stage == "stagger":
            feet = {"Left": FEET["Left"] + Vector((0, 0.35, 0)), "Right": FEET["Right"] + Vector((0, 0.1, 0))}
            lower_hips(-0.2, feet, dy=0.25)
            bend("Spine", Vector((1, 0, 0)), -18); bend("Spine", UP, 15)
            arm("Left", sl + Vector((0.45, -0.1, 0.05)), Vector((1, 0.3, -0.3)))
            hold(Vector((-0.45, 0.0, -0.2)), Vector((-0.5, 0.3, -1)))
        else:   # roar
            lower_hips(-0.18, FEET, dy=-0.05)
            bend("Spine", Vector((1, 0, 0)), -10); bend("Neck", Vector((1, 0, 0)), -25)
            arm("Left", sl + Vector((0.55, -0.15, 0.35)), Vector((1, 0.3, 0)))
            hold(Vector((-0.55, -0.15, 0.35)), Vector((-0.6, 0.2, -1)))

    # 대기는 플레이어 대기를 옮긴 것 대신 철룡 고유 대기로 바꿔요 (굳은 채 팔이 벌어지고 사슬이 손끝에 걸려 있었어요)
    old_idle = bpy.data.actions.get("Idle")
    if old_idle is not None:
        bpy.data.actions.remove(old_idle)
    make_action("Idle", [(f, f / 60) for f in range(0, 61, 6)], dragon_idle)

    make_action("MeteorSwing", [(0, "swing_load"), (4, "swing_hit"), (8, "swing_follow"), (16, "swing_follow"), (28, "idle")], dragon_pose)
    make_action("MeteorSlam", [(0, "slam_up"), (4, "slam_down"), (14, "slam_down"), (26, "idle")], dragon_pose)
    make_action("ChainThrow", [(0, "chain_load"), (3, "chain_throw"), (10, "chain_throw"), (15, "chain_pull"), (22, "chain_pull"), (32, "idle")], dragon_pose)
    make_action("Command", [(0, "idle"), (6, "command"), (18, "command"), (24, "idle")], dragon_pose)
    make_action("Stomp", [(0, "stomp_up"), (4, "stomp_down"), (14, "stomp_down"), (24, "idle")], dragon_pose)
    make_action("Roar", [(0, "idle"), (6, "stagger"), (16, "stagger"), (24, "roar"), (52, "roar"), (60, "idle")], dragon_pose)
    rig.animation_data.action = None
    reset_pose()


# 천마총 (tombmoves): rivermoves의 베기 자세(slash_pose)를 같이 써요
#   마교도(dao) Attack: 곡도를 치켜들었다가 내려베기 · Frenzy: 광폭 — 몸을 낮추고 칼을 뒤로 끌며 으르렁 (반복)
#   천마 근위(dao+towershield) Attack: 방패를 앞에 세운 채 되받아 베기 · Guard: 방패를 내밀고 웅크린 자세 (반복)
#   혈인(shaman) Raise: 두 팔을 하늘로 들어 쓰러진 자를 일으켜요 (그대로 재생) · Attack: 두 손바닥을 앞으로 뻗어 피를 뿜어요
#   혈폭자(bomber) Attack: 핏덩이를 머리 위로 들었다가 앞으로 내던져요
if "tombmoves" in EXTRAS:
    def guard_left():
        sl = pb["LeftArm"].head.copy()
        arm("Left", sl + Vector((0.1, -0.42, -0.1)), Vector((1, 0.3, -0.5)))   # 방패를 앞에

    if "dao" in WEAPONS and "rattan" not in WEAPONS:
        left = guard_left if "towershield" in WEAPONS else free_left_back
        make_action("Attack", [(0, ("load", left)), (4, ("cut1", left)), (14, ("cut1", left)), (24, ("idle", None))], slash_pose)

        def low_pose(t):
            a = t * 2 * math.pi
            lower_hips(-0.25 - 0.03 * math.sin(a * 2), FEET, dy=0.05)
            bend("Spine", Vector((1, 0, 0)), 18 + 4 * math.sin(a * 2))
            sh = pb["RightArm"].head.copy()
            if "towershield" in WEAPONS:
                guard_left()
                arm("Right", sh + Vector((-0.1, -0.2, -0.45)), Vector((-1, 0.3, -0.3)))
                point_blade(Vector((0, -1, 0.3)), wrist_bend=80)              # 방패 옆으로 칼끝을 겨눠요
            else:
                arm("Right", sh + Vector((-0.25, 0.25, -0.45)), Vector((-1, 0.3, -0.3)))
                point_blade(Vector((-0.3, 1, -0.5)), wrist_bend=80)           # 칼을 뒤로 끌어요
                sl = pb["LeftArm"].head.copy()
                arm("Left", sl + Vector((0.15, -0.4, -0.25 + 0.05 * math.sin(a))), Vector((1, 0.3, -0.5)))   # 손톱을 세운 듯 앞으로
        make_action("Guard" if "towershield" in WEAPONS else "Frenzy", [(f, f / 16) for f in range(0, 17, 4)], low_pose)

    if "shaman" in EXTRAS:
        def shaman_pose(stage):
            if stage == "idle":
                idle_end_pose(); return
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                if stage == "raise":
                    lower_hips(-0.04, FEET)
                    bend("Spine", Vector((1, 0, 0)), -12); bend("Neck", Vector((1, 0, 0)), -20)
                    arm(s, sh + Vector((sx * 0.3, -0.15, 0.75)), Vector((sx, 0.3, 0)))
                elif stage == "gather":
                    lower_hips(-0.12, FEET)
                    bend("Spine", Vector((1, 0, 0)), 10)
                    arm(s, sh + Vector((-sx * 0.12, -0.3, -0.25)), Vector((sx, 0.4, -0.6)))
                else:   # cast: 두 손바닥을 앞으로
                    lower_hips(-0.14, {"Left": FEET["Left"] + Vector((0, -0.3, 0)), "Right": FEET["Right"]}, dy=-0.12)
                    bend("Spine", Vector((1, 0, 0)), 14)
                    arm(s, sh + Vector((-sx * 0.1, -0.7, 0.0)), Vector((sx, 0.3, -0.6)))
        make_action("Raise", [(0, "idle"), (8, "raise"), (36, "raise"), (44, "idle")], shaman_pose)
        make_action("Attack", [(0, "gather"), (4, "cast"), (12, "cast"), (22, "idle")], shaman_pose)

    if "bomber" in EXTRAS:
        def bomber_pose(stage):
            if stage == "idle":
                idle_end_pose(); return
            sh = pb["RightArm"].head.copy()
            sl = pb["LeftArm"].head.copy()
            if stage == "lift":
                lower_hips(-0.08, FEET, dy=0.06)
                bend("Spine", Vector((1, 0, 0)), -14)
                arm("Right", sh + Vector((-0.05, 0.2, 0.75)), Vector((-1, 0.3, 0)))   # 머리 위 뒤로 치켜들어요
                arm("Left", sl + Vector((0.3, -0.3, 0.1)), Vector((1, 0.3, -0.5)))
            else:
                feet = {"Left": FEET["Left"] + Vector((0, -0.35, 0)), "Right": FEET["Right"]}
                lower_hips(-0.2, feet, dy=-0.15)
                bend("Spine", Vector((1, 0, 0)), 24)
                arm("Right", sh + Vector((0.1, -0.75, -0.2)), Vector((-1, 0, -0.5)))   # 앞으로 내던져요
                arm("Left", sl + Vector((0.3, 0.2, -0.45)), Vector((1, 0.3, -0.3)))
        make_action("Attack", [(0, "lift"), (4, "throw"), (12, "throw"), (22, "idle")], bomber_pose)
    rig.animation_data.action = None
    reset_pose()


# 혈영검마·천마 (demonmoves, 장검):
#   혈영검마 — ShadowRush: 낮게 웅크렸다가 번개처럼 오른쪽·왼쪽·오른쪽 세 번 베어 넘기며 파고들어요 (잔상은 이펙트)
#              Lunge: 몸을 바닥까지 낮췄다가 튀어나가며 일직선으로 찔러요 · Slash: 가볍게 한 번 가로베기
#   천마 — Slash1: 곧게 선 채 단정한 일검 (1페이즈) · DemonSweep: 칼을 머리 위로 치켜들었다가 허리째 크게 휘둘러요 (2페이즈)
#          Burst: 칼끝을 땅으로 내리고 두 팔을 펼쳐 가슴을 젖혀 마기를 터뜨려요 (3페이즈, 그대로 재생)
#          Ascend: 페이즈가 바뀔 때 무릎을 꿇었다가 천천히 일어서며 두 팔을 펼쳐요
if "demonmoves" in EXTRAS:
    DM = {   # twist, wrist, reach, lift, dy, blade, step(왼발 앞으로), left(왼팔: back 뒤로 · sign 검결 · wide 펼침)
        "r_ready": (-30, Vector((-0.4, 0.3, -0.3)), 0.85, -0.38, 0.1, Vector((-0.3, 1, 0.2)), 0.25, "back"),
        "r_cut1": (45, Vector((0.65, -0.7, 0.05)), 0.95, -0.3, -0.3, Vector((1, -0.25, 0.0)), 0.6, "back"),
        "r_cut2": (-45, Vector((-0.6, -0.7, 0.1)), 0.95, -0.3, -0.3, Vector((-1, -0.25, 0.05)), 0.6, "back"),
        "l_ready": (-10, Vector((-0.15, 0.3, -0.35)), 0.8, -0.45, 0.12, Vector((0, -1, 0.05)), 0.35, "sign"),
        "l_thrust": (8, Vector((0.05, -1, 0.25)), 1.0, -0.32, -0.38, Vector((0, -1, 0.5)), 0.65, "back"),   # 몸을 숙인 만큼 위로 겨눠 칼끝이 수평
        "s_load": (-35, Vector((-0.5, 0.15, 0.6)), 0.9, -0.1, 0.05, Vector((-0.3, 0.5, 1)), 0.1, "sign"),
        "s_cut": (35, Vector((0.6, -0.75, 0.0)), 0.95, -0.18, -0.12, Vector((1, -0.35, 0.0)), 0.35, "sign"),
        "h_ready": (0, Vector((0.0, -0.6, -0.2)), 0.8, -0.04, 0.0, Vector((0, -0.6, 0.8)), 0.0, "sign"),
        "h_cut": (5, Vector((0.05, -0.9, -0.3)), 0.95, -0.12, -0.12, Vector((0.1, -0.7, -0.7)), 0.3, "sign"),
        "d_up": (-20, Vector((-0.1, 0.1, 1)), 0.95, -0.1, 0.05, Vector((-0.2, 0.6, 0.8)), 0.1, "wide"),
        "d_sweep": (50, Vector((0.7, -0.6, -0.05)), 0.95, -0.3, -0.2, Vector((1, -0.3, -0.05)), 0.5, "wide"),
        "d_end": (65, Vector((0.7, 0.0, 0.3)), 0.9, -0.24, -0.15, Vector((0.6, 0.5, 0.5)), 0.5, "wide"),
        "b_burst": (0, Vector((0.35, -0.25, -0.75)), 0.95, -0.2, 0.05, Vector((0.1, -0.2, -1)), 0.2, "wide"),
        "a_kneel": (0, Vector((0.15, -0.35, -0.75)), 0.9, -0.7, 0.05, Vector((0, -0.1, -1)), 0.35, "down"),
        "g_reach": (-25, Vector((-0.2, 0.3, -0.5)), 0.85, -0.16, -0.12, Vector((-0.2, 0.6, -0.8)), 0.4, "reach"),
        "g_pull": (20, Vector((-0.2, 0.25, -0.5)), 0.85, -0.22, 0.12, Vector((-0.2, 0.6, -0.8)), 0.0, "pull"),
    }

    def dm_pose(stage):
        if stage == "idle":
            idle_end_pose(); return
        twist, wrist, reach, lift, dy, blade_dir, step, left = DM[stage]
        feet = {"Left": FEET["Left"] + Vector((0, -step, 0)), "Right": FEET["Right"] + Vector((0, step * 0.3, 0))}
        lower_hips(lift, feet, dy=dy)
        bend("Spine", UP, twist * 0.6); bend("Spine1", UP, twist * 0.4)
        if stage == "b_burst":
            bend("Spine", Vector((1, 0, 0)), -16); bend("Neck", Vector((1, 0, 0)), -20)      # 가슴을 젖히고 하늘을 봐요
        sh = pb["RightArm"].head.copy()
        pole = Vector((-0.6, 0.3, 0)) if wrist.z > 0.3 else Vector((0.2, 0.5, -1))
        fit_right(sh, [wrist.normalized() * REACH * reach, wrist.normalized() * REACH * reach * 0.9 + Vector((0, 0, 0.05))],
                  (pole, Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((-0.5, 0.4, -1))), blade_dir, wrist_bend=80)
        sl = pb["LeftArm"].head.copy()
        if left == "back":
            arm("Left", sl + Vector((0.3, 0.2, -0.4)), Vector((1, 0.3, -0.3)))
        elif left == "sign":
            arm("Left", sl + Vector((-0.12, -0.32, -0.05)), Vector((1, 0.3, -0.6)))          # 검결: 가슴 앞
        elif left == "wide":
            arm("Left", sl + Vector((0.6, -0.2, 0.25)), Vector((1, 0.3, 0)))                 # 크게 펼쳐요
        elif left == "reach":
            arm("Left", sl + Vector((-0.05, -0.85, 0.05)), Vector((1, 0, -0.5)))              # 왼손을 앞으로 쭉 뻗어 마기로 붙잡아요
            bend("LeftHand", Vector((1, 0, 0)), -60)                                         # 손바닥이 앞을 봐요
        elif left == "pull":
            arm("Left", sl + Vector((0.05, -0.25, -0.15)), Vector((1, 0.3, -0.5)))            # 움켜쥐고 가슴께로 끌어당겨요
        else:
            arm("Left", sl + Vector((0.15, -0.3, -0.6)), Vector((1, 0.3, -0.5)))             # 무릎 위에

    if "bloodshadow" in EXTRAS:
        make_action("ShadowRush", [(0, "r_ready"), (3, "r_cut1"), (7, "r_cut2"), (11, "r_cut1"), (18, "r_cut1"), (28, "idle")], dm_pose)
        make_action("Lunge", [(0, "l_ready"), (3, "l_thrust"), (12, "l_thrust"), (22, "idle")], dm_pose)
        make_action("Slash", [(0, "s_load"), (4, "s_cut"), (12, "s_cut"), (22, "idle")], dm_pose)
    if "heavenly" in EXTRAS:
        make_action("Slash1", [(0, "h_ready"), (4, "h_cut"), (14, "h_cut"), (24, "idle")], dm_pose)
        make_action("DemonSweep", [(0, "d_up"), (4, "d_sweep"), (8, "d_end"), (18, "d_end"), (30, "idle")], dm_pose)
        make_action("Burst", [(0, "idle"), (8, "b_burst"), (36, "b_burst"), (46, "idle")], dm_pose)
        make_action("Ascend", [(0, "a_kneel"), (20, "a_kneel"), (40, "b_burst"), (52, "b_burst"), (60, "idle")], dm_pose)
        make_action("Grip", [(0, "g_reach"), (6, "g_reach"), (11, "g_pull"), (20, "g_pull"), (30, "idle")], dm_pose)   # 마기 손아귀: 뻗었다가 끌어당기기
        # Defeat (천마 격파 · 엔딩 내내 이 자세로 남아요): 휘청이다 털썩 무릎을 꿇고, 두 손을 무릎에 얹은 채 고개를 깊이 숙여요 (검은 Unity가 손에서 떨궈요)
        def defeat_pose(stage):
            if stage == "idle":
                idle_end_pose(); return
            kneel = stage == "kneel"
            feet = {s: FEET[s] + (Vector((0, 0.45, 0)) if kneel else Vector((0, 0.15 if s == "Right" else -0.1, 0))) for s in ("Left", "Right")}
            lower_hips(-0.7 if kneel else -0.25, feet, dy=0.1 if kneel else 0.05)
            bend("Spine", Vector((1, 0, 0)), 22 if kneel else -10); bend("Neck", Vector((1, 0, 0)), 38 if kneel else -15)
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                if kneel:
                    arm(s, sh + Vector((sx * 0.12, -0.32, -0.62)), Vector((sx, 0.3, -0.3)))   # 무릎 위에 손
                else:
                    arm(s, sh + Vector((sx * 0.45, 0.05, -0.35)), Vector((sx, 0.3, -0.3)))    # 휘청이며 팔이 벌어져요
        make_action("Defeat", [(0, "stagger"), (10, "stagger"), (22, "kneel"), (40, "kneel")], defeat_pose)
    rig.animation_data.action = None
    reset_pose()

# 보스 최후 (defeat=<녹림|혈불|철룡>, 40프레임): 쓰러질 때 넘어가지 않고 이 자세로 무너진 뒤 Unity가 무기를 떨구고 몸을 먹으로 흩어 지워요
#   녹림(bandit): 크게 휘청이다 한쪽 무릎을 꿇고, 왼손으로 무릎을 짚고 오른팔은 축 늘어져요 (고개는 앞으로 떨궈요)
#   혈불(buddha): 뒤로 한 번 흔들리다 그대로 주저앉아 가부좌처럼 낮게 앉고, 두 손을 가슴 앞에 모으며 고개를 숙여요
#   철룡(dragon): 뒤로 비틀거리다 두 무릎을 꿇고, 두 팔을 옆으로 늘어뜨린 채 하늘을 올려다봐요
BOSS_DEFEAT = next((e.split("=", 1)[1] for e in EXTRAS if e.startswith("defeat=")), None)
if BOSS_DEFEAT:
    def boss_defeat_pose(stage):
        if stage == "stagger":
            back = BOSS_DEFEAT != "bandit"
            feet = {s: FEET[s] + Vector((0, (0.18 if s == "Right" else -0.08) * (1 if back else -1), 0)) for s in ("Left", "Right")}
            lower_hips(-0.22, feet, dy=0.1 if back else -0.04)
            bend("Spine", Vector((1, 0, 0)), -14 if back else 12); bend("Neck", Vector((1, 0, 0)), -18 if back else 10)
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, sh + Vector((sx * 0.5, 0.08, -0.3)), Vector((sx, 0.3, -0.3)))        # 휘청이며 팔이 벌어져요
            return
        if BOSS_DEFEAT == "bandit":
            # 한쪽 무릎: 오른 무릎이 땅에 닿게 오른발은 뒤로, 왼발은 앞에 세워요
            feet = {"Left": FEET["Left"] + Vector((0, -0.25, 0)), "Right": FEET["Right"] + Vector((0, 0.5, 0.0))}
            lower_hips(-0.55, feet, dy=0.12)
            bend("Spine", Vector((1, 0, 0)), 26); bend("Neck", Vector((1, 0, 0)), 34)
            shL = pb["LeftArm"].head.copy(); shR = pb["RightArm"].head.copy()
            arm("Left", shL + Vector((0.1, -0.4, -0.45)), Vector((1, 0.3, -0.3)))            # 왼손으로 무릎을 짚고
            arm("Right", shR + Vector((-0.12, -0.08, -0.62)), Vector((-1, 0.3, -0.2)))       # 칼 쥔 팔은 축 늘어져요
        elif BOSS_DEFEAT in ("sword", "chest"):
            # 한쪽 무릎 (녹림과 같은 다리). sword: 오른손 검을 앞 땅에 꽂아 짚고 기대요 · chest: 왼손으로 가슴을 움켜쥐고 오른팔은 늘어져요
            feet = {"Left": FEET["Left"] + Vector((0, -0.25, 0)), "Right": FEET["Right"] + Vector((0, 0.5, 0.0))}
            lower_hips(-0.55, feet, dy=0.1)
            bend("Spine", Vector((1, 0, 0)), 18 if BOSS_DEFEAT == "sword" else 28); bend("Neck", Vector((1, 0, 0)), 30)
            shL = pb["LeftArm"].head.copy(); shR = pb["RightArm"].head.copy()
            if BOSS_DEFEAT == "sword":
                arm("Left", shL + Vector((0.1, -0.4, -0.45)), Vector((1, 0.3, -0.3)))        # 왼손은 무릎에
                arm("Right", shR + Vector((0.02, -0.5, -0.32)), Vector((-1, 0.3, -0.4)))    # 검 쥔 손은 앞으로 내밀어
                if HAS_BLADE:
                    point_blade(Vector((0.05, -0.15, -1)), wrist_bend=40)                  # 칼끝을 땅에 꽂아요
            else:
                arm("Left", shL + Vector((-0.2, -0.22, -0.18)), Vector((1, 0.3, -0.5)))     # 가슴을 움켜쥐어요
                arm("Right", shR + Vector((-0.1, -0.06, -0.62)), Vector((-1, 0.3, -0.2)))
        elif BOSS_DEFEAT == "fists":
            feet = {s: FEET[s] + Vector((0, 0.45, 0)) for s in ("Left", "Right")}
            lower_hips(-0.72, feet, dy=0.1)
            bend("Spine", Vector((1, 0, 0)), 42); bend("Neck", Vector((1, 0, 0)), 28)      # 앞으로 크게 숙여
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, Vector((sh.x + sx * 0.05, sh.y - 0.42, 0.06)), Vector((sx, 0.4, 0.2)))   # 두 주먹으로 땅을 짚어요
        elif BOSS_DEFEAT == "buddha":
            feet = {s: FEET[s] + Vector((-sx * 0.12, -0.15, 0)) for s, sx in (("Left", -1), ("Right", 1))}
            lower_hips(-0.85, feet, dy=0.05)
            bend("Spine", Vector((1, 0, 0)), 16); bend("Neck", Vector((1, 0, 0)), 30)
            prayer_arms(drop=0.12)                                                           # 가슴 앞에 두 손을 모아요
        else:
            feet = {s: FEET[s] + Vector((0, 0.45, 0)) for s in ("Left", "Right")}
            lower_hips(-0.7, feet, dy=0.08)
            bend("Spine", Vector((1, 0, 0)), -12); bend("Neck", Vector((1, 0, 0)), -32)   # 하늘을 올려다봐요
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, sh + Vector((sx * 0.28, 0.05, -0.62)), Vector((sx, 0.2, -0.3)))      # 두 팔은 옆으로 늘어뜨려요
    make_action("Defeat", [(0, "stagger"), (10, "stagger"), (22, "final"), (40, "final")], boss_defeat_pose)
    rig.animation_data.action = None
    reset_pose()


# 갇힌 무림맹 고수 (captive, 천마총 이벤트): Captive — 무릎 꿇고 고개를 떨군 채 두 손이 등 뒤로 묶여 있어요 (숨 쉬듯 반복)
#   Freed — 사슬이 풀리면 손을 앞으로 풀고 천천히 일어나 포권(주먹을 손바닥으로 감싸 가슴 앞)으로 인사해요
if "captive" in EXTRAS:
    def captive_pose(val):
        stage, t = val
        feet_back = {s: FEET[s] + Vector((0, 0.42, 0.0)) for s in ("Left", "Right")}   # 무릎 꿇으면 발은 뒤로
        if stage == "kneel":
            lower_hips(-0.62 + 0.01 * math.sin(t * 2 * math.pi), feet_back, dy=0.12)
            bend("Spine", Vector((1, 0, 0)), 18 + 2 * math.sin(t * 2 * math.pi)); bend("Neck", Vector((1, 0, 0)), 30)
            back = (pb["LeftArm"].head + pb["RightArm"].head) / 2 + Vector((0, 0.3, -0.45))
            for s, sx in (("Left", 1), ("Right", -1)):
                arm(s, back + Vector((sx * 0.06, 0, 0)), Vector((sx, -0.3, -0.5)))       # 두 손목이 등 뒤에서 모여요
        elif stage == "rise":
            lower_hips(-0.3, {s: FEET[s] + Vector((0, 0.15, 0)) for s in ("Left", "Right")}, dy=0.05)
            bend("Spine", Vector((1, 0, 0)), 14); bend("Neck", Vector((1, 0, 0)), 10)
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, sh + Vector((sx * 0.15, -0.2, -0.5)), Vector((sx, 0.3, -0.3)))
        else:   # salute: 포권 — 오른 주먹을 왼 손바닥으로 감싸 가슴 앞, 고개를 살짝 숙여요
            lower_hips(0.0, FEET)
            bend("Spine", Vector((1, 0, 0)), 8); bend("Neck", Vector((1, 0, 0)), 12)
            chest = (pb["LeftArm"].head + pb["RightArm"].head) / 2 + Vector((0, -0.32, -0.12))
            arm("Right", chest + Vector((-0.02, 0, 0)), Vector((-1, 0.4, -1)))
            arm("Left", chest + Vector((0.05, -0.04, 0.02)), Vector((1, 0.4, -1)))
    make_action("Captive", [(f, ("kneel", f / 40)) for f in range(0, 41, 8)], captive_pose)
    make_action("Freed", [(0, ("kneel", 0)), (12, ("rise", 0)), (24, ("salute", 0)), (44, ("salute", 0))], captive_pose)
    rig.animation_data.action = None
    reset_pose()


# 등장(Spawn, 모든 playeranim 사람형 · 18프레임): 방에 나타날 때 낮게 웅크린 채 고개를 숙였다가 → 일어서며 고개를 들고
#   무기 쥔 손을 앞으로 한 번 고쳐 잡아요 → 대기 자세 (InkEnemyAnimator가 처음 한 번 틀어요)
if "playeranim" in EXTRAS:
    def spawn_pose(stage):
        if stage == "idle":
            idle_end_pose(); return
        low = stage == "crouch"
        lower_hips(-0.45 if low else -0.18, FEET, dy=0.06 if low else 0.02)
        bend("Spine", Vector((1, 0, 0)), 30 if low else 8); bend("Neck", Vector((1, 0, 0)), 20 if low else -6)
        for s, sx in (("Left", 1), ("Right", -1)):
            sh = pb[f"{s}Arm"].head.copy()
            if low:
                arm(s, sh + Vector((sx * 0.12, -0.25, -0.55)), Vector((sx, 0.3, -0.3)))           # 팔을 늘어뜨려 땅을 짚을 듯
                if s == "Right" and HAS_BLADE:
                    point_blade(Vector((0, -0.3, -1)), wrist_bend=60)                           # 칼끝은 땅으로
            elif s == "Right":
                arm(s, sh + Vector((0.05, -0.42, -0.3)), Vector((-1, 0.3, -0.5)))                # 무기 쥔 손을 앞으로 고쳐 잡아요
                if HAS_BLADE:
                    point_blade(Vector((0.2, -1, -0.35)), wrist_bend=60)                         # 칼끝을 앞으로 겨눠요
            else:
                arm(s, sh + Vector((0.25, -0.1, -0.45)), Vector((1, 0.3, -0.3)))
    make_action("Spawn", [(0, "crouch"), (6, "crouch"), (12, "rise"), (18, "idle")], spawn_pose)
    rig.animation_data.action = None
    reset_pose()

# 마을 주민 생활 동작 (villager=<well|chop|sweep|sit>, 반복 "Life" 하나): 무기 대신 생활 소품을 들고 같은 일을 되풀이해요
#   well: 우물 두레박 줄을 두 손 번갈아 당겨요 (소품 없음) · chop: 도끼를 머리 위로 치켜들었다 모탕에 내려찍어요 (axe)
#   sweep: 허리를 숙여 싸리비로 좌우로 쓸어요 (broom) · sit: 걸터앉아 두 손을 무릎에 얹고 숨 쉬며 고개를 천천히 돌려요
VILLAGER = next((e.split("=", 1)[1] for e in EXTRAS if e.startswith("villager=")), None)
if VILLAGER and "playeranim" in EXTRAS:
    def villager_pose(v):
        stage, k = v
        if VILLAGER == "well":
            lower_hips(-0.08, FEET, dy=0.04)
            bend("Spine", Vector((1, 0, 0)), -6 + 4 * k)                                   # 당길 때 몸을 살짝 젖혀요
            for s, sx, up in (("Left", 0.08, k), ("Right", -0.08, 1 - k)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, sh + Vector((-sx * 0.6, -0.36, -0.12 + 0.38 * up)), Vector((sx * 6, 0.3, -0.5)))   # 번갈아 위·아래로
        elif VILLAGER == "chop":
            up = stage == "up"
            lower_hips(-0.12 if up else -0.22, FEET, dy=0.02 if up else 0.06)
            bend("Spine", Vector((1, 0, 0)), -10 if up else 26)
            shR = pb["RightArm"].head.copy(); shL = pb["LeftArm"].head.copy()
            wrist = shR + (Vector((0.12, 0.08, 0.5)) if up else Vector((0.15, -0.5, -0.45)))
            arm("Right", wrist, Vector((-1, 0.3, -0.5)))
            arm("Left", wrist + Vector((-0.06, 0.06, -0.08)), Vector((1, 0.3, -0.5)))       # 왼손은 자루 아래쪽
            if HAS_BLADE:
                point_blade(Vector((0.1, 0.25, 1)) if up else Vector((0.05, -1, -0.5)), wrist_bend=40)
        elif VILLAGER == "sweep":
            sway = math.sin(k * 2 * math.pi)
            lower_hips(-0.14, FEET, dy=0.05)
            bend("Spine", Vector((1, 0, 0)), 24); bend("Spine", Vector((0, 0, 1)), 14 * sway)  # 숙여 허리째 좌우로
            shR = pb["RightArm"].head.copy(); shL = pb["LeftArm"].head.copy()
            wrist = shR + Vector((0.12 + 0.1 * sway, -0.38, -0.32))
            arm("Right", wrist, Vector((-1, 0.3, -0.5)))
            arm("Left", wrist + Vector((-0.02, 0.05, 0.22)), Vector((1, 0.3, -0.5)))         # 왼손은 대 위쪽
            if HAS_BLADE:
                point_blade(Vector((0.35 * sway, -0.55, -1)), wrist_bend=30)                # 비 끝이 바닥을 좌우로 쓸어요
        else:  # sit
            feet = {s: FEET[s] + Vector((0, -0.38, 0)) for s in FEET}                       # 발을 앞으로 내밀고
            lower_hips(-0.5, feet, dy=0.18)                                                 # 뒤로 걸터앉아요
            bend("Spine", Vector((1, 0, 0)), 6 + 2 * math.sin(k * 2 * math.pi))
            bend("Neck", Vector((0, 0, 1)), 18 * math.sin(k * 2 * math.pi))                # 고개를 천천히 돌려요
            for s, sx in (("Left", 1), ("Right", -1)):
                sh = pb[f"{s}Arm"].head.copy()
                arm(s, sh + Vector((sx * 0.1, -0.42, -0.42)), Vector((sx, 0.3, -0.3)))      # 두 손을 무릎에
    if VILLAGER == "chop":
        keys = [(0, ("up", 0)), (10, ("up", 0)), (16, ("down", 0)), (28, ("down", 0)), (40, ("up", 0))]
    else:
        n = {"well": 40, "sweep": 48, "sit": 120}[VILLAGER]
        keys = [(f, ("loop", f / n)) for f in range(0, n + 1, n // 8)]
    make_action("Life", keys, villager_pose)
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
