"""Paints the title screen's backdrop and its mist: a moonlit graveyard road in purple-blacks, all flat silhouettes
(far hills, a graveyard skyline with a chapel, dead trees and an iron fence, then the road the ghosts ride along),
and a soft band of mist the title scene drifts along the road.

Run: python tools/title-backdrop.py
Writes Assets/Sprites/Title/TitleBackdrop.png (1920x1080 RGB) and Assets/Sprites/Title/TitleFog.png (1024x512 RGBA,
white with the mist in alpha, tiles seamlessly left to right, clear at its top and bottom).

Both are imported uncompressed (TitleScreenBuilder sets it): block compression turns dark, smooth pictures like these
into visible squares. Shapes are drawn at twice the size and scaled down, so their edges are smooth.

Where things sit comes from the stage's camera (TitleStage: 2.6 up, 19 back, pitched 4 degrees, 26 degree view): the
true horizon is 35% of the way down the screen and the ghosts' wheels touch the ground between 61% and 67%.
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Assets", "Sprites", "Title")

W, H = 1920, 1080
SS = 2                      # drawn this many times bigger, then scaled down
SEED = 11

SKY_TOP = (0x07, 0x04, 0x10)
SKY_LOW = (0x30, 0x15, 0x55)
HORIZON = 0.40              # where the sky ends (down the screen)
MOON_AT = (0.80, 0.20)
MOON_R = 105
MOON = (0xEC, 0xE0, 0xFF)
HILLS = (0x1E, 0x0E, 0x38)
YARD = (0x13, 0x09, 0x25)   # the graveyard and everything standing in it
ROAD_TOP, ROAD_BOTTOM = 0.575, 0.735
ROAD = (0x1B, 0x0E, 0x30)
VERGE = (0x0B, 0x06, 0x16)
LINE = (0x3A, 0x24, 0x5E)
FOG_W, FOG_H = 1024, 512


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def sky(rng):
    y, x = np.mgrid[0:H, 0:W].astype(np.float64)
    t = np.clip(y / (H * HORIZON), 0, 1) ** 1.6
    img = np.array(SKY_TOP)[None, None, :] + (np.array(SKY_LOW) - np.array(SKY_TOP))[None, None, :] * t[..., None]

    # The moon's halo: wide and faint, then a tighter one
    mx, my = MOON_AT[0] * W, MOON_AT[1] * H
    d = np.sqrt((x - mx) ** 2 + (y - my) ** 2)
    img += np.array((48, 30, 78))[None, None, :] * np.exp(-(d / 520.0) ** 2)[..., None]
    img += np.array((90, 70, 120))[None, None, :] * np.exp(-(d / 190.0) ** 2)[..., None]

    # Half a grey level of noise, so the gradient never bands
    img += rng.uniform(-0.5, 0.5, (H, W, 1))
    return Image.fromarray(np.clip(np.round(img), 0, 255).astype(np.uint8), "RGB").resize((W * SS, H * SS), Image.BILINEAR)


def stars(img, rnd):
    draw = ImageDraw.Draw(img, "RGBA")
    mx, my = MOON_AT[0] * W * SS, MOON_AT[1] * H * SS
    for _ in range(260):
        x, y = rnd.uniform(0, W * SS), rnd.uniform(0, H * SS * HORIZON * 0.95)
        if math.hypot(x - mx, y - my) < MOON_R * SS * 1.9:
            continue
        fade = 1 - y / (H * SS * HORIZON)          # fewer and fainter towards the horizon
        if rnd.random() > 0.35 + 0.65 * fade:
            continue
        r = rnd.choice((1.2, 1.2, 1.6, 1.6, 2.2, 3.0))
        a = int(rnd.uniform(70, 210) * (0.4 + 0.6 * fade))
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(225, 210, 255, a))
        if r >= 3.0:                                # the few big ones get a little cross of light
            draw.line((x - 9, y, x + 9, y), fill=(225, 210, 255, a // 3), width=1)
            draw.line((x, y - 9, x, y + 9), fill=(225, 210, 255, a // 3), width=1)


def moon(img):
    mx, my, r = MOON_AT[0] * W * SS, MOON_AT[1] * H * SS, MOON_R * SS
    disc = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(disc).ellipse((mx - r, my - r, mx + r, my + r), fill=MOON + (255,))
    img.paste(disc, (0, 0), disc)
    # Seas: a few darker patches, soft-edged and kept inside the disc
    seas = Image.new("L", img.size, 0)
    draw = ImageDraw.Draw(seas)
    for dx, dy, cr in ((-0.30, -0.28, 0.30), (0.26, -0.34, 0.18), (0.30, 0.24, 0.32), (-0.24, 0.40, 0.20),
                       (-0.58, 0.10, 0.13)):
        cx, cy, rr = mx + dx * r, my + dy * r, cr * r
        draw.ellipse((cx - rr, cy - rr, cx + rr, cy + rr), fill=120)
    seas = seas.filter(ImageFilter.GaussianBlur(10 * SS))
    seas = Image.composite(seas, Image.new("L", img.size, 0), disc.getchannel("A"))
    img.paste(Image.new("RGB", img.size, (0xB9, 0xA2, 0xE4)), (0, 0), seas)


def clouds(img):
    """Two long thin clouds, one across the moon's lower half"""
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    for cx, cy, rx, ry, a in ((0.74, 0.245, 0.17, 0.016, 200), (0.88, 0.275, 0.12, 0.012, 170),
                              (0.60, 0.12, 0.10, 0.010, 120), (0.93, 0.10, 0.08, 0.009, 110)):
        x, y = cx * W * SS, cy * H * SS
        draw.ellipse((x - rx * W * SS, y - ry * H * SS, x + rx * W * SS, y + ry * H * SS), fill=(0x1A, 0x0C, 0x30, a))
    layer = layer.filter(ImageFilter.GaussianBlur(9 * SS))
    img.paste(layer, (0, 0), layer)


def hills(draw):
    top = H * SS * (HORIZON - 0.045)
    pts = [(0, H * SS)]
    for x in range(0, W * SS + 8, 8):
        u = x / (W * SS)
        y = top + SS * (26 * math.sin(u * 5.1 + 0.6) + 14 * math.sin(u * 11.3 + 2.0) + 6 * math.sin(u * 23.0))
        pts.append((x, y))
    pts.append((W * SS, H * SS))
    draw.polygon(pts, fill=HILLS)


def yard_line(u):
    """How far down the graveyard's top edge is at u across (0 to 1), in drawn pixels"""
    return SS * (H * (HORIZON + 0.035) + 9 * math.sin(u * 4.0 + 1.0) + 4 * math.sin(u * 13.0))


def tree(draw, x, y, length, angle, width, rnd, depth=0):
    x2, y2 = x + length * math.sin(angle), y - length * math.cos(angle)
    draw.line((x, y, x2, y2), fill=YARD, width=max(1, int(width)))
    if depth >= 5 or length < 6 * SS:
        return
    for turn in (-1, 1):
        if depth > 1 and rnd.random() < 0.2:
            continue
        tree(draw, x2, y2, length * rnd.uniform(0.62, 0.78), angle + turn * rnd.uniform(0.35, 0.75),
             width * 0.66, rnd, depth + 1)


def stone(draw, x, base, w, h, kind):
    if kind == 0:       # round-topped
        draw.rectangle((x - w / 2, base - h + w / 2, x + w / 2, base + 4), fill=YARD)
        draw.ellipse((x - w / 2, base - h, x + w / 2, base - h + w), fill=YARD)
    elif kind == 1:     # a cross
        t = w * 0.28
        draw.rectangle((x - t / 2, base - h, x + t / 2, base + 4), fill=YARD)
        draw.rectangle((x - w / 2, base - h * 0.74, x + w / 2, base - h * 0.74 + t), fill=YARD)
    else:               # a leaning slab
        lean = w * 0.22
        draw.polygon(((x - w / 2, base + 4), (x - w / 2 + lean, base - h), (x + w / 2 + lean, base - h),
                      (x + w / 2, base + 4)), fill=YARD)


def chapel(draw, x, base):
    s = SS
    draw.rectangle((x - 62 * s, base - 62 * s, x + 62 * s, base + 4), fill=YARD)              # the nave
    draw.polygon(((x - 72 * s, base - 62 * s), (x, base - 112 * s), (x + 72 * s, base - 62 * s)), fill=YARD)
    tx = x - 86 * s                                                                             # the tower
    draw.rectangle((tx - 24 * s, base - 118 * s, tx + 24 * s, base + 4), fill=YARD)
    draw.polygon(((tx - 30 * s, base - 118 * s), (tx, base - 196 * s), (tx + 30 * s, base - 118 * s)), fill=YARD)
    draw.line((tx, base - 196 * s, tx, base - 218 * s), fill=YARD, width=3 * s)
    draw.line((tx - 8 * s, base - 208 * s, tx + 8 * s, base - 208 * s), fill=YARD, width=3 * s)
    # One lit window, the only warm thing in the picture
    wx, wy = x + 6 * s, base - 44 * s
    draw.rectangle((wx - 8 * s, wy - 4 * s, wx + 8 * s, wy + 22 * s), fill=(0x6B, 0x4A, 0x8E))
    draw.ellipse((wx - 8 * s, wy - 12 * s, wx + 8 * s, wy + 4 * s), fill=(0x6B, 0x4A, 0x8E))


def fence(draw, x0, x1, base):
    s = SS
    draw.line((x0, base - 20 * s, x1, base - 20 * s), fill=YARD, width=2 * s)
    draw.line((x0, base - 7 * s, x1, base - 7 * s), fill=YARD, width=2 * s)
    x = x0
    while x <= x1:
        draw.line((x, base + 2 * s, x, base - 30 * s), fill=YARD, width=2 * s)
        draw.polygon(((x - 3 * s, base - 30 * s), (x, base - 38 * s), (x + 3 * s, base - 30 * s)), fill=YARD)
        x += 15 * s


def graveyard(draw, rnd):
    # The ground it all stands on, down to the road
    pts = [(0, H * SS)]
    for x in range(0, W * SS + 8, 8):
        pts.append((x, yard_line(x / (W * SS))))
    pts.append((W * SS, H * SS))
    draw.polygon(pts, fill=YARD)

    def base(u):
        return yard_line(u) + 2 * SS

    chapel(draw, 0.615 * W * SS, base(0.615))
    for u, size, lean in ((0.50, 150, -0.10), (0.715, 120, 0.12), (0.955, 170, -0.06), (0.07, 140, 0.08)):
        tree(draw, u * W * SS, base(u), size * SS * 0.36, lean, 9 * SS, rnd)
    fence(draw, 0.745 * W * SS, 0.925 * W * SS, base(0.83))
    u = 0.02
    while u < 0.99:
        if not (0.535 < u < 0.70) and not (0.74 < u < 0.93):       # not through the chapel or the fence
            w = rnd.uniform(15, 26) * SS
            stone(draw, u * W * SS, base(u), w, w * rnd.uniform(1.2, 1.9), rnd.choice((0, 0, 1, 2)))
        u += rnd.uniform(0.018, 0.045)


def road(img):
    """The road is a band a touch lighter than the graveyard, soft at its edges, with a worn dashed line, and the
    verge below it falls away to near black"""
    y = np.arange(H * SS, dtype=np.float64)[:, None] / (H * SS)
    px = np.asarray(img).astype(np.float64)

    def smooth(a, b, v):
        t = np.clip((v - a) / (b - a), 0, 1)
        return t * t * (3 - 2 * t)

    on_road = smooth(ROAD_TOP - 0.006, ROAD_TOP + 0.006, y) * (1 - smooth(ROAD_BOTTOM - 0.006, ROAD_BOTTOM + 0.006, y))
    below = smooth(ROAD_BOTTOM - 0.006, ROAD_BOTTOM + 0.006, y)
    # Lighter down the middle of the road, where the moon catches it
    mid = (ROAD_TOP + ROAD_BOTTOM) / 2
    sheen = np.exp(-((y - mid) / 0.05) ** 2)
    road_colour = np.array(ROAD)[None, None, :] * (1 + 0.28 * sheen)[..., None]
    fall = np.clip((y - ROAD_BOTTOM) / (1 - ROAD_BOTTOM), 0, 1)
    verge = np.array(VERGE)[None, None, :] * (1 - 0.55 * fall)[..., None]
    px = px * (1 - on_road)[..., None] + road_colour * on_road[..., None]
    px = px * (1 - below)[..., None] + verge * below[..., None]
    out = Image.fromarray(np.clip(np.round(px), 0, 255).astype(np.uint8), "RGB")

    draw = ImageDraw.Draw(out, "RGBA")
    ly = mid * H * SS
    x = -40 * SS
    while x < W * SS:
        draw.rounded_rectangle((x, ly - 2.5 * SS, x + 70 * SS, ly + 2.5 * SS), radius=2 * SS, fill=LINE + (150,))
        x += 150 * SS
    # The kerbs: a hair of light along each edge
    for edge in (ROAD_TOP, ROAD_BOTTOM):
        draw.line((0, edge * H * SS, W * SS, edge * H * SS), fill=LINE + (70,), width=SS)
    return out


def vignette(img):
    px = np.asarray(img).astype(np.float64)
    y, x = np.mgrid[0:H, 0:W].astype(np.float64)
    cx, cy = (x / W - 0.5) * 2, (y / H - 0.5) * 2
    v = 1 - 0.30 * np.clip((cx * cx + cy * cy) / 2, 0, 1) ** 1.5
    return Image.fromarray(np.clip(np.round(px * v[..., None]), 0, 255).astype(np.uint8), "RGB")


def backdrop():
    rng = np.random.default_rng(SEED)
    rnd = random.Random(SEED)
    img = sky(rng)
    stars(img, rnd)
    moon(img)
    clouds(img)
    draw = ImageDraw.Draw(img)
    hills(draw)
    graveyard(draw, rnd)
    img = road(img)
    return vignette(img.resize((W, H), Image.LANCZOS))


def mist():
    """Cloud noise made in the frequency domain, so it wraps left to right by construction, faded out towards the top
    and bottom so it lies along the road as a band"""
    rng = np.random.default_rng(SEED + 1)
    fx = np.fft.fftfreq(FOG_W)[None, :] * FOG_W
    fy = np.fft.fftfreq(FOG_H)[:, None] * FOG_H
    r = np.sqrt(fx ** 2 + (fy * 2.2) ** 2)      # stretched sideways: long wisps
    spectrum = np.exp(-(r / 5.0) ** 2) / (1 + r) ** 0.6
    n = np.fft.ifft2(np.fft.fft2(rng.standard_normal((FOG_H, FOG_W))) * spectrum).real
    n = (n - n.min()) / (n.max() - n.min())
    v = np.arange(FOG_H, dtype=np.float64)[:, None] / (FOG_H - 1)
    band = np.exp(-((v - 0.5) / 0.2) ** 2)
    band[v[:, 0] < 0.02] = 0
    band[v[:, 0] > 0.98] = 0
    a = np.clip(n * 1.25 - 0.1, 0, 1) * band
    rgba = np.empty((FOG_H, FOG_W, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.round(a * 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    b = backdrop()
    b.save(os.path.join(OUT, "TitleBackdrop.png"))
    f = mist()
    f.save(os.path.join(OUT, "TitleFog.png"))
    px = np.asarray(b).reshape(-1, 3)
    print("backdrop min", px.min(axis=0), "max", px.max(axis=0), "mean", px.mean(axis=0).round(1))
    al = np.asarray(f)[..., 3].astype(int)
    print("mist alpha range", al.min(), al.max(), "mean", al.mean().round(1),
          "seam jump L/R", np.abs(al[:, 0] - al[:, -1]).mean().round(2), "top/bottom rows", al[0].max(), al[-1].max())


if __name__ == "__main__":
    main()
