# 나무 줄기 붓결 텍스처 (Art/Env/BurningBamboo/Tex_TrunkBrush.png): 대나무 줄기 그림처럼 세로로 긴 붓 자국 — 큰 세로 띠 2~3톤 + 가는 밝은 결, 마디 없음
# 사용: blender -b --python trunk_texture.py -- out.png
import bpy, sys, numpy as np

out = sys.argv[sys.argv.index("--") + 1]
rng = np.random.default_rng(7)


def noise(n, freq, aniso=(1, 1)):   # 주기적(이어 붙는) 저주파 잡음
    f = np.fft.fft2(rng.normal(size=(n, n)))
    ky = np.fft.fftfreq(n)[:, None] * n * aniso[0]; kx = np.fft.fftfreq(n)[None, :] * n * aniso[1]
    f *= np.exp(-(np.sqrt(kx ** 2 + ky ** 2) / freq) ** 2)
    r = np.real(np.fft.ifft2(f)); return (r - r.mean()) / r.std()


def blur(img, s):   # 주기적 가우시안
    n = img.shape[0]; ky = np.fft.fftfreq(n)[:, None]; kx = np.fft.fftfreq(img.shape[1])[None, :]
    g = np.exp(-2 * (np.pi * s) ** 2 * (kx ** 2 + ky ** 2))
    return np.stack([np.real(np.fft.ifft2(np.fft.fft2(img[..., c]) * g)) for c in range(img.shape[2])], -1)


def bands(v, edges, soft):   # 2~3톤 큰 덩어리: 부드러운 계단
    o = np.zeros_like(v)
    for e in edges: o += 1 / (1 + np.exp(-(v - e) / soft))
    return o / len(edges)


n = 512
big = noise(n, 5, (1, 0.08))            # 세로로 늘어진 큰 덩어리
fine = noise(n, 30, (1, 0.04))          # 가는 세로 결
t = bands(big * 0.9 + fine * 0.2, [-0.5, 0.45], 0.12)[..., None]
dark = np.array([0.24, 0.15, 0.08]); mid = np.array([0.40, 0.26, 0.13]); light = np.array([0.58, 0.40, 0.21])
col = np.where(t < 0.5, dark + (mid - dark) * (t * 2), mid + (light - mid) * ((t - 0.5) * 2))
col = np.clip(col + np.clip(fine - 1.3, 0, 1)[..., None] * 0.35 * (light - mid + 0.1), 0, 1)   # 몇 줄 밝은 붓결
px = np.ones((n, n, 4)); px[..., :3] = blur(col, 0.8)
o = bpy.data.images.new("o", n, n, alpha=True); o.pixels = px.ravel(); o.filepath_raw = out; o.file_format = "PNG"; o.save()
print("trunk texture ok")
