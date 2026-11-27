# 서리 길 바닥 자국 (Fx/Resources/FxArt/FrostTrail.png, 256×512 투명): 가로 = 길 폭, 세로 = 길 방향.
#   가장자리가 부드럽게 사라지는 얼음 붓결 띠 — 길 방향으로 긴 붓 자국 몇 줄 + 비스듬히 뻗은 성에 가지(깃털 결) + 흰 얼음 심.
#   흰색에 가까운 얼음빛으로 그려 두고 색 · 투명도는 코드(InkRuntimeDress)가 물들여요.
# 사용: blender -b --factory-startup --python frost_trail.py -- <out.png>
import bpy, sys, math, numpy as np

out = sys.argv[sys.argv.index("--") + 1]
W, H = 256, 512
rng = np.random.default_rng(11)
y, x = np.mgrid[0:H, 0:W].astype(float)
u = (x - W / 2) / (W / 2)            # -1..1 길 폭
v = y / H                            # 0..1 길 방향

def noise(fy, fx, seed):
    r = np.random.default_rng(seed)
    f = np.fft.fft2(r.normal(size=(H, W)))
    ky = np.fft.fftfreq(H)[:, None] * H; kx = np.fft.fftfreq(W)[None, :] * W
    f *= np.exp(-((ky / fy) ** 2 + (kx / fx) ** 2))
    n = np.real(np.fft.ifft2(f)); return (n - n.mean()) / n.std()

# 띠 모양: 폭 방향은 부드러운 가장자리 (붓 자국처럼 들쭉날쭉), 길 끝은 가늘게
edge = 0.78 + 0.12 * noise(6, 2, 1)
band = np.clip((edge - np.abs(u)) / 0.35, 0, 1)
ends = np.clip(np.minimum(v, 1 - v) / 0.12, 0, 1)
shape = band * ends

# 길 방향 붓결 (가는 밝은 줄) + 비스듬한 성에 가지 (양쪽으로 45°)
streak = np.clip(noise(2.5, 40, 2) * 0.6 + 0.2, 0, 1)
fern = np.zeros((H, W))
for k in range(60):
    cx, cy = rng.uniform(-0.6, 0.6), rng.uniform(0.05, 0.95)
    ang = math.radians(rng.choice([40, 140]) + rng.uniform(-12, 12))
    L = rng.uniform(0.05, 0.12)
    du, dv = math.cos(ang), math.sin(ang) * 0.5
    px, py = (u - cx), (v - cy)
    t = np.clip(px * du + py * dv, 0, L) / (du * du + dv * dv)
    d = np.sqrt((px - t * du) ** 2 + ((py - t * dv) * 2) ** 2)
    fern = np.maximum(fern, np.clip(1 - d / 0.018, 0, 1) * (1 - t / max(L, 1e-3)) * 0.9)
core = np.exp(-(u / 0.18) ** 2) * 0.35        # 가운데 얼음 심

alpha = np.clip(shape * (0.32 + 0.35 * streak + 0.55 * fern + core), 0, 1)
light = np.clip(0.78 + 0.22 * np.maximum(fern, streak), 0, 1)
img = np.zeros((H, W, 4))
img[..., 0] = light * 0.9; img[..., 1] = light * 0.97; img[..., 2] = light
img[..., 3] = alpha
o = bpy.data.images.new("frost", W, H, alpha=True); o.pixels = img[::-1].ravel()
o.filepath_raw = out; o.file_format = 'PNG'; o.save(); print("frost ok")
