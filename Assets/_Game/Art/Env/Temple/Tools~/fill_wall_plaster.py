# 폐사찰 담 회벽 메우기 (플레이테스트 2차 11-27: 붉은 회벽 칸마다 칠이 벗겨진 자리가 구멍처럼 보였어요)
#   VARCO 담 한 칸(TempleWallSegment)의 앞뒤 회벽 면(기단 위 · 지붕 아래 · 양끝 나무 기둥 사이)이 쓰는 텍스처 자리만 골라,
#   그 안에서 붉은 칠이 아닌 픽셀(벗겨진 갈색 · 잿빛)을 둘레 붉은 칠 색으로 메워요. 기단 돌 · 기둥 · 기와 쪽 텍스처는 그대로예요.
# 사용: blender -b --factory-startup --python fill_wall_plaster.py -- <TempleWallSegment.fbx> <Albedo.png> [mask.png]
import bpy, sys, numpy as np

a = sys.argv[sys.argv.index("--") + 1:]
fbx, albedo = a[0], a[1]
mask_out = a[2] if len(a) > 2 else None
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
ob = next(o for o in bpy.data.objects if o.type == 'MESH')
me = ob.data
vs = [v.co for v in me.vertices]
xs, ys, zs = [v.x for v in vs], [v.y for v in vs], [v.z for v in vs]
x0, x1, y0, y1, z0, z1 = min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)
# 모델 축: 폭 · 두께 · 높이 중 가장 짧은 축이 두께 (prepare_prop 기준 X 폭 · Y 두께 · Z 높이)
ext = {"x": x1 - x0, "y": y1 - y0, "z": z1 - z0}
print("WALL ext", {k: round(v, 3) for k, v in ext.items()})
uvl = me.uv_layers.active.data
im = bpy.data.images.load(albedo); W, H = im.size
px = np.array(im.pixels[:]).reshape(H, W, 4)
mask = np.zeros((H, W), bool)
yy, xx = np.mgrid[0:H, 0:W]

def raster(tri):
    (ax, ay), (bx, by), (cx, cy) = [(u * W, v * H) for u, v in tri]
    mnx, mxx = int(max(0, min(ax, bx, cx))), int(min(W - 1, max(ax, bx, cx) + 1))
    mny, mxy = int(max(0, min(ay, by, cy))), int(min(H - 1, max(ay, by, cy) + 1))
    if mxx <= mnx or mxy <= mny: return
    X, Y = xx[mny:mxy + 1, mnx:mxx + 1] + 0.5, yy[mny:mxy + 1, mnx:mxx + 1] + 0.5
    d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(d) < 1e-9: return
    l1 = ((by - cy) * (X - cx) + (cx - bx) * (Y - cy)) / d
    l2 = ((cy - ay) * (X - cx) + (ax - cx) * (Y - cy)) / d
    inside = (l1 >= -0.002) & (l2 >= -0.002) & (1 - l1 - l2 >= -0.002)
    mask[mny:mxy + 1, mnx:mxx + 1] |= inside

faces = 0
for p in me.polygons:
    n = p.normal; c = p.center
    if abs(n.y) < 0.75: continue                                        # 앞뒤 면만
    if not (z0 + ext["z"] * 0.22 < c.z < z0 + ext["z"] * 0.86): continue   # 기단 위 · 지붕 아래
    if not (x0 + ext["x"] * 0.07 < c.x < x1 - ext["x"] * 0.07): continue   # 양끝 기둥 빼고
    idx = list(p.loop_indices)
    for k in range(1, len(idx) - 1):
        raster([tuple(uvl[idx[0]].uv), tuple(uvl[idx[k]].uv), tuple(uvl[idx[k + 1]].uv)])
    faces += 1
print("WALL plaster faces", faces, "mask px", int(mask.sum()))

rgb = px[..., :3]
mx, mn = rgb.max(-1), rgb.min(-1)
sat = (mx - mn) / np.maximum(mx, 1e-4)
red = (rgb[..., 0] > rgb[..., 1] * 1.25) & (rgb[..., 0] > rgb[..., 2] * 1.35) & (sat > 0.42) & (mx > 0.35)   # 붉은 칠
paint = rgb[mask & red]
base = np.median(paint, axis=0) if len(paint) else np.array([0.75, 0.33, 0.22])
# 붉은 칠과 색이 많이 다른 픽셀은 모두 메워요 (벗겨진 갈색 · 잿빛뿐 아니라 칠이 들뜬 옅은 살구빛 · 구멍 테의 짙은 선까지)
fill = mask & (np.linalg.norm(rgb - base, axis=-1) > 0.16)
print("WALL plaster colour", np.round(base, 3), "fill px", int(fill.sum()))
# 메울 색: 붉은 칠 중앙값에 낮은 주파수 얼룩 (평평한 단색 땜빵이 안 보이게)
rng = np.random.default_rng(5)
f = np.fft.fft2(rng.normal(size=(H, W)))
ky = np.fft.fftfreq(H)[:, None] * H; kx = np.fft.fftfreq(W)[None, :] * W
f *= np.exp(-((np.sqrt(kx ** 2 + ky ** 2)) / 60.0) ** 2)
nz = np.real(np.fft.ifft2(f)); nz = (nz - nz.mean()) / nz.std()
col = base[None, None, :] * (1 + nz[..., None] * 0.06)
out = px.copy()
out[fill, :3] = np.clip(col[fill], 0, 1)
o = bpy.data.images.new("o", W, H, alpha=False); o.pixels = out.ravel(); o.filepath_raw = albedo; o.file_format = 'PNG'; o.save()
if mask_out:
    m = np.zeros((H, W, 4)); m[..., 3] = 1; m[mask, 0] = 1; m[fill, 1] = 1
    mo = bpy.data.images.new("m", W, H); mo.pixels = m.ravel(); mo.filepath_raw = mask_out; mo.file_format = 'PNG'; mo.save()
print("WALL done")
