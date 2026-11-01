"""Seamless stylised stone-block tile for the fountain (1 tile = 1 unit, 2 blocks x 4 courses).
Writes <out>_BaseColor.png and <out>_Normal.png."""
import sys, math
import numpy as np
from PIL import Image, ImageFilter

out = sys.argv[1]; P = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
BW, CH = 0.5, 0.25                       # block width / course height in tile units
GROUT_W, LINE_W, BEVEL, ROUND = 0.010, 0.005, 0.03, 0.025

def hexc(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], float) / 255
STONE = hexc('#b2aa9b'); GROUT = hexc('#6a645b'); LINE = hexc('#36322d'); MOSS = hexc('#62793a')

def periodic_noise(cell, seed):
    """Value noise that wraps at the tile edge."""
    n = max(2, P // cell); g = np.random.default_rng(seed).random((n, n))
    big = Image.fromarray((np.tile(g, (3, 3)) * 255).astype(np.uint8)).resize((3 * P, 3 * P), Image.BICUBIC)
    return np.asarray(big, float)[P:2 * P, P:2 * P] / 255

def hash2(a, b, s):
    x = np.sin(a * 127.1 + b * 311.7 + s * 74.7) * 43758.5453
    return x - np.floor(x)

# pixel centres in tile units; v up = image rows down (flipped at save)
v, u = (np.mgrid[0:P, 0:P] + 0.5) / P
course = np.floor(v / CH)
off = (course % 2) * BW / 2
bi = np.floor((u + off) / BW) % round(1 / BW)
lx = np.mod(u + off, BW); ly = v - course * CH
j = 0.012 * np.stack([hash2(bi, course, k) for k in range(4)])
x0, x1, y0, y1 = j[0], BW - j[1], j[2], CH - j[3]
qx = np.abs(lx - (x0 + x1) / 2) - ((x1 - x0) / 2 - ROUND)
qy = np.abs(ly - (y0 + y1) / 2) - ((y1 - y0) / 2 - ROUND)
edge = -(np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - ROUND)
top_d, bot_d = y1 - ly, ly - y0

tone = hash2(bi, course, 9)
col = STONE[None, None] * (0.88 + 0.22 * tone[..., None])
inside = (edge > 0)[..., None]
col = col * (1 + 0.16 * np.clip(1 - top_d / BEVEL, 0, 1)[..., None] * inside) \
          * (1 - 0.2 * np.clip(1 - bot_d / BEVEL, 0, 1)[..., None] * inside)
# painted grain, a touch of moss in the joints, soft dirt toward each block's lower edge
grain = (periodic_noise(48, 1) - 0.5) * 0.06 + (periodic_noise(160, 2) - 0.5) * 0.10
col = col * (1 + grain[..., None]) * (1 - 0.08 * np.clip(1 - bot_d / 0.08, 0, 1)[..., None])
moss = np.clip((periodic_noise(96, 3) - 0.62) * 4, 0, 1) * np.clip(1 - edge / 0.03, 0, 1) * (edge > 0)
col = col * (1 - 0.6 * moss[..., None]) + MOSS * 0.6 * moss[..., None]
col[edge < GROUT_W / 2] = GROUT
col[(edge >= GROUT_W / 2) & (edge < GROUT_W / 2 + LINE_W)] = LINE

# a few cracks, drawn with wrap-around so they stay seamless
rng = np.random.default_rng(5)
for _ in range(3):
    y, x = rng.uniform(0, P, 2); a = rng.uniform(0, 2 * math.pi)
    for _ in range(rng.integers(15, 30)):
        a += rng.normal(0, 0.45); ny, nx = y + 3 * math.sin(a), x + 3 * math.cos(a)
        if edge[int(ny) % P, int(nx) % P] < 0.012: break
        for t in np.linspace(0, 1, 4):
            yy, xx = int(y + (ny - y) * t) % P, int(x + (nx - x) * t) % P
            col[yy, xx] = col[(yy + 1) % P, xx] = LINE
        y, x = ny, nx

col = np.clip(col, 0, 1)
Image.fromarray((col[::-1] * 255).astype(np.uint8)).save(out + '_BaseColor.png')

height = np.clip((edge - GROUT_W / 2) / BEVEL, 0, 1)
h = np.asarray(Image.fromarray((np.tile(height, (3, 3)) * 255).astype(np.uint8))
               .filter(ImageFilter.GaussianBlur(1.2)), float)[P:2 * P, P:2 * P] / 255   # blur with wrap
s = 3.0 * P / 1024
dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * s       # rows here already run with v
dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * s
nrm = np.dstack([-dx, -dy, np.ones_like(dx)]); nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
Image.fromarray((((nrm * 0.5 + 0.5) * 255).astype(np.uint8))[::-1]).save(out + '_Normal.png')
print('painted', out, P)
