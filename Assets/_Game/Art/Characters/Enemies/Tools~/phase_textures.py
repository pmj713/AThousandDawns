# 천마 페이즈 텍스처: 원래 색 텍스처(Albedo)에서 2·3페이즈 모습을 만들어요.
#   2페이즈: 전체를 검붉게 가라앉히고, 마기가 핏줄처럼 번진 무늬(물결 줄무늬를 겹쳐 가는 선만 남김)를 검붉게 새겨요.
#   3페이즈: 2페이즈 위에 몸이 갈라진 금(보로노이 칸의 경계)을 그리고, 금 안쪽은 붉게 달아올라요.
#   blender --background --factory-startup --python phase_textures.py -- <albedo.png> <out_dir> <name>
import bpy, sys, os
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_dir, name = argv[0], argv[1], argv[2]
img = bpy.data.images.load(src)
w, h = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
rgb = px[..., :3]
rng = np.random.default_rng(7)

# 줄무늬 겹침으로 핏줄 같은 가는 선: |sin|이 0에 가까운 곳만 남겨요
yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
u, v = xx / w, yy / h
veins = np.zeros((h, w), np.float32)
for _ in range(10):
    a, b, c, f = rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0, 6.28), rng.uniform(9, 22)
    warp = 0.06 * np.sin(u * rng.uniform(15, 30) + v * rng.uniform(10, 25))
    line = np.abs(np.sin((u * a + v * b + warp) * f + c))
    veins = np.maximum(veins, np.clip(1.0 - line / 0.13, 0, 1))
veins *= np.clip(np.sin(u * 7.3 + 1.1) * np.sin(v * 5.1 + 0.4) + 0.75, 0, 1)   # 군데군데 더 짙게 번져요

dark = rgb * np.array([0.62, 0.45, 0.47], np.float32)                           # 검붉게 가라앉힌 바탕
vein_col = np.array([0.62, 0.04, 0.05], np.float32)   # 핏빛 마기
p2 = dark * (1 - veins[..., None] * 0.85) + vein_col * veins[..., None] * 0.85

# 갈라진 금: 무작위 점들의 보로노이 칸 경계 (가장 가까운 두 점 거리 차가 작은 곳)
pts = rng.uniform(0, 1, (70, 2)).astype(np.float32)
d1 = np.full((h, w), 9.0, np.float32); d2 = np.full((h, w), 9.0, np.float32)
for px_, py_ in pts:
    d = np.hypot(u - px_, v - py_)
    closer = d < d1
    d2 = np.where(closer, d1, np.minimum(d2, d))
    d1 = np.where(closer, d, d1)
edge = d2 - d1
crack = np.clip(1.0 - edge / 0.006, 0, 1)                                       # 금
glow = np.clip(1.0 - edge / 0.03, 0, 1) ** 2                                     # 금 둘레로 번지는 열기
glow_col = np.array([0.95, 0.22, 0.06], np.float32)
p3 = p2 * (1 - glow[..., None] * 0.5) + glow_col * glow[..., None] * 0.5
p3 = p3 * (1 - crack[..., None]) + np.array([1.0, 0.45, 0.12], np.float32) * crack[..., None]

os.makedirs(out_dir, exist_ok=True)
for tag, arr in (("P2", p2), ("P3", p3)):
    out = np.concatenate([np.clip(arr, 0, 1), px[..., 3:]], axis=2)
    im = bpy.data.images.new(f"{name}_{tag}", w, h, alpha=True)
    im.pixels = out.ravel().tolist()
    im.filepath_raw = os.path.join(out_dir, f"{name}_{tag}.png"); im.file_format = 'PNG'; im.save()
    print("PHASE_TEX", im.filepath_raw)
