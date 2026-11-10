# Blender(5.x) 백그라운드 스크립트: VARCO로 만들고 자동 리깅한 졸개(Source~/Grunt_Rigged.fbx)를 Unity용으로 정리해요.
#   blender --background --factory-startup --python prepare_grunt.py -- <Grunt_Rigged.fbx> <out_dir> [preview.png]
#   - 키 1.8m, 발밑 원점으로 맞춰요
#   - 색 텍스처를 Grunt_Albedo.png로 꺼내요 (Unity 수묵 재질의 _BaseMap)
#   - 오른손에 박도(넓은 외날 칼)를 쥐여요 (Grunt_Dao: 오른손 뼈에 통째로 붙음)
import bpy, bmesh, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_dir = os.path.abspath(argv[0]), os.path.abspath(argv[1])
preview = os.path.abspath(argv[2]) if len(argv) > 2 else None
HEIGHT = 1.8

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.data.objects if o.type == 'MESH')
rig.name = "Grunt_Rig"; body.name = "Grunt_Body"

# 크기·원점
bpy.context.view_layer.update()
vs = [body.matrix_world @ v.co for v in body.data.vertices]
zmin = min(v.z for v in vs); zmax = max(v.z for v in vs)
s = HEIGHT / (zmax - zmin)
cx = (min(v.x for v in vs) + max(v.x for v in vs)) / 2; cy = (min(v.y for v in vs) + max(v.y for v in vs)) / 2
rig.scale = rig.scale * s
bpy.context.view_layer.update()
vs = [body.matrix_world @ v.co for v in body.data.vertices]
zmin = min(v.z for v in vs)
cx = (min(v.x for v in vs) + max(v.x for v in vs)) / 2; cy = (min(v.y for v in vs) + max(v.y for v in vs)) / 2
rig.location -= Vector((cx, cy, zmin))
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
print("GRUNT scale %.3f" % s)

# 텍스처 꺼내기
os.makedirs(out_dir, exist_ok=True)
img = None
for m in body.data.materials:
    if m and m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image:
                img = n.image; break
if img is not None:
    img.filepath_raw = os.path.join(out_dir, "Grunt_Albedo.png"); img.file_format = 'PNG'; img.save()
    print("GRUNT texture", img.size[:])
else:
    print("GRUNT no texture")

# 박도: 오른손에서 앞(-Y)으로 뻗은 넓은 외날 칼. 손잡이는 주먹 안
hand = rig.data.bones["RightHand"]
grip = hand.head_local.lerp(hand.tail_local, 0.6)
axis = Vector((0, -1, -0.15)).normalized()          # 칼 방향: 앞, 살짝 아래
edge = Vector((0, 0, -1)).cross(axis).cross(axis).normalized() * -1   # 날 방향 (아래쪽)
flat = axis.cross(edge).normalized()
bm = bmesh.new()


def box(c, length, w, th):
    vs_ = []
    for dz in (-length / 2, length / 2):
        for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs_.append(bm.verts.new(c + axis * dz + edge * (w / 2 * a) + flat * (th / 2 * b)))
    for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
        bm.faces.new([vs_[i] for i in f])


box(grip, 0.16, 0.03, 0.028)                         # 손잡이
box(grip + axis * 0.09, 0.02, 0.08, 0.035)           # 코등이
# 칼날: 등은 곧고 날은 끝으로 갈수록 넓어지는 박도
L = 0.62
ring = []
for u, w in ((0.0, 0.045), (0.75, 0.07), (0.95, 0.06), (1.0, 0.0)):
    c = grip + axis * (0.1 + L * u)
    back = c - edge * 0.0
    if w == 0.0:
        ring.append([bm.verts.new(c + edge * 0.03)])
        continue
    ring.append([bm.verts.new(back + flat * 0.004), bm.verts.new(back + edge * w), bm.verts.new(back - flat * 0.004)])
for a, b in zip(ring[:-1], ring[1:]):
    for i in range(3):
        j = (i + 1) % 3
        if len(b) == 1:
            bm.faces.new((a[i], a[j], b[0]))
        else:
            bm.faces.new((a[i], a[j], b[j], b[i]))
bm.faces.new(list(reversed(ring[0])))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
me = bpy.data.meshes.new("Grunt_Dao"); bm.to_mesh(me); bm.free()
dao = bpy.data.objects.new("Grunt_Dao", me); bpy.context.scene.collection.objects.link(dao)
dao.vertex_groups.new(name="RightHand").add(list(range(len(me.vertices))), 1.0, 'REPLACE')
dao.parent = rig
dao.modifiers.new("Armature", 'ARMATURE').object = rig

# 내보내기 (플레이어와 같은 축 설정)
bpy.ops.object.select_all(action='DESELECT')
for o in (rig, body, dao):
    o.select_set(True)
bpy.context.view_layer.objects.active = rig
out = os.path.join(out_dir, "Grunt_Rigged.fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=False, mesh_smooth_type='OFF', add_leaf_bones=False,
                         primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
                         bake_anim=False, path_mode='STRIP')
print("GRUNT exported", out, "bones", len(rig.data.bones), "faces", len(body.data.polygons))

if preview:
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x, sc.render.resolution_y = 900, 700
    sc.world = bpy.data.worlds.new("W")
    cd = bpy.data.cameras.new("C"); cd.type = 'ORTHO'; cd.ortho_scale = 2.4
    cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam); sc.camera = cam
    d = Vector((0.5, -1, 0.3)).normalized(); cam.location = Vector((0, 0, 0.95)) + d * 6
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = preview; bpy.ops.render.render(write_still=True)
