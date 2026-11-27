# 미니맵 방 아이콘 (Hud/Resources/HudArt/MapPouch · MapGate, 64×64 투명): 굵고 단순한 먹색 실루엣 + 얇은 한지색 테두리
#   미니맵 칸(34px, 옅은 회색 · 지금 방은 붉은색) 위에 28px로 그려져요 — 둘 다에서 읽히게 짙은 먹 + 밝은 가장자리
#   MapPouch: 끈으로 조인 복주머니 (보상 방) · MapGate: 소용돌이 문 (관문 = 순간이동)
# 사용: blender -b --python minimap_icons.py -- <out_dir>
import bpy, sys, os, math, numpy as np

out = sys.argv[sys.argv.index("--") + 1]
N, SS = 64, 4                     # 64px, 4배로 그려 줄여요 (매끈한 가장자리)
M = N * SS
y, x = np.mgrid[0:M, 0:M] / SS    # 위가 0인 좌표 (픽셀 단위 0~64)
INK = np.array([0.10, 0.09, 0.11]); PAPER = np.array([0.96, 0.92, 0.82]); GOLD = np.array([0.86, 0.68, 0.30])

def ellipse(cx, cy, rx, ry): return ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1
def seg(ax, ay, bx, by, r):
    px, py = x - ax, y - ay; dx, dy = bx - ax, by - ay
    t = np.clip((px * dx + py * dy) / (dx * dx + dy * dy), 0, 1)
    return (px - t * dx) ** 2 + (py - t * dy) ** 2 <= r * r
def dilate(m, r):
    k = int(r * SS); o = m.copy()
    for dy in range(-k, k + 1):
        for dx in range(-k, k + 1):
            if dx * dx + dy * dy <= k * k: o |= np.roll(np.roll(m, dy, 0), dx, 1)
    return o

def save(name, shape, accent):
    rim = dilate(shape, 2.2) & ~shape                 # 한지색 테두리
    img = np.zeros((M, M, 4))
    img[rim, :3] = PAPER; img[rim, 3] = 1
    img[shape, :3] = INK; img[shape, 3] = 1
    img[accent, :3] = GOLD; img[accent, 3] = 1
    small = img.reshape(N, SS, N, SS, 4).mean(axis=(1, 3))
    small[..., :3] = np.where(small[..., 3:4] > 0, small[..., :3] / np.maximum(small[..., 3:4], 1e-6), 0)
    o = bpy.data.images.new(name, N, N, alpha=True); o.pixels = small[::-1].ravel()
    o.filepath_raw = os.path.join(out, name + ".png"); o.file_format = 'PNG'; o.save(); print("icon", name)

# 복주머니: 둥근 몸통 + 조인 목 + 위로 벌어진 주둥이 + 양옆 끈 고리, 몸통에 금빛 끈 매듭
body = ellipse(32, 41, 20, 17)
neck = (np.abs(x - 32) <= 7) & (y >= 19) & (y <= 27)
mouth = ellipse(32, 17, 13, 6) & (y <= 20)
loops = seg(24, 25, 15, 13, 2.6) | seg(40, 25, 49, 13, 2.6)
pouch = body | neck | mouth | loops
tie = (np.abs(y - 26) <= 1.6) & (np.abs(x - 32) <= 9) | ellipse(32, 26, 3.2, 3.2)
save("MapPouch", pouch, tie & pouch)

# 소용돌이 문: 둥근 아치 문틀(두꺼운 먹) 안에 금빛 나선
arch_outer = ((x - 32) ** 2 + (y - 26) ** 2 <= 23 ** 2) & (y <= 26) | (np.abs(x - 32) <= 23) & (y > 26) & (y <= 58)
arch_inner = ((x - 32) ** 2 + (y - 26) ** 2 <= 15 ** 2) & (y <= 26) | (np.abs(x - 32) <= 15) & (y > 26) & (y <= 58)
door = arch_outer
spiral = np.zeros((M, M), bool)
for i in range(400):                       # 굵은 나선 한 바퀴 반 (28px에서도 소용돌이로 읽히게)
    t = i / 400 * 1.7 * 2 * math.pi
    r = 1.5 + 11 * t / (1.7 * 2 * math.pi)
    sx, sy = 32 + r * math.cos(t), 38 + r * math.sin(t) * 1.15
    spiral |= (x - sx) ** 2 + (y - sy) ** 2 <= (2.3 + 0.8 * t / 11) ** 2
save("MapGate", door, spiral & arch_inner)
