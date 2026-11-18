"""Paint flame sprites (RGBA PNG) for the key art: the stolen package's purple flame, like the game's
PurpleFlame flipbook (deep 0.45,0.05,0.95 -> hot 0.97,0.7,1.0), as a teardrop of fire with licking tongues.

  python tools/keyart/flame_sprite.py <out_dir> [count]
"""
import sys, os
import numpy as np
from PIL import Image

DEEP = np.array([0.45, 0.05, 0.95])
HOT = np.array([0.97, 0.70, 1.00])


def value_noise(h, w, cells, rng):
    g = rng.random((cells + 2, cells + 2))
    ys = np.linspace(0, cells, h)
    xs = np.linspace(0, cells, w)
    y0 = ys.astype(int); x0 = xs.astype(int)
    fy = (ys - y0)[:, None]; fx = (xs - x0)[None, :]
    fy = fy * fy * (3 - 2 * fy); fx = fx * fx * (3 - 2 * fx)
    a = g[y0][:, x0]; b = g[y0][:, x0 + 1]; c = g[y0 + 1][:, x0]; d = g[y0 + 1][:, x0 + 1]
    return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy


def flame(seed, W=256, H=384):
    rng = np.random.default_rng(seed)
    n = sum(value_noise(H, W, c, rng) * a for c, a in ((4, 0.5), (9, 0.3), (19, 0.2)))
    n2 = sum(value_noise(H, W, c, rng) * a for c, a in ((3, 0.6), (7, 0.4)))
    yy, xx = np.mgrid[0:H, 0:W]
    x = (xx / (W - 1)) * 2 - 1                    # -1..1
    y = 1 - yy / (H - 1)                          # 0 bottom .. 1 top
    # sideways sway growing with height, and tongues: noise pushes the edge in and out
    x = x + (n2 - 0.5) * 0.9 * y ** 1.3
    base = 0.30                                    # the round belly's centre height
    half_w = np.where(y < base, np.sqrt(np.clip(1 - ((base - y) / base) ** 2, 0, 1)) * 0.78,
                      0.78 * np.clip(1 - (y - base) / (1 - base), 0, 1) ** 0.8)
    half_w = half_w * (0.75 + 0.6 * n)
    d = np.abs(x) / np.maximum(half_w, 1e-3)       # 0 centre .. 1 edge
    a = np.clip(1.4 - d * 1.4, 0, 1) ** 0.7
    a = a * np.clip((y - 0.01) / 0.08, 0, 1) * np.clip((1 - y) / 0.3, 0, 1) ** 0.8
    heat = np.clip(1 - d, 0, 1) * np.clip(1 - (y - 0.2) * 1.1, 0, 1)
    heat = np.clip(heat * 1.5 - 0.15 + (n - 0.5) * 0.4, 0, 1)
    rgb = DEEP[None, None, :] * (1 - heat[..., None]) + HOT[None, None, :] * heat[..., None]
    img = np.dstack([rgb, a])
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), "RGBA")


if __name__ == "__main__":
    out = sys.argv[1]
    count = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    os.makedirs(out, exist_ok=True)
    for i in range(count):
        flame(11 + i * 7).save(os.path.join(out, f"flame_{i}.png"))
    print("wrote", count, "flames to", out)
