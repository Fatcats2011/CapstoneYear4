"""Paints the title screen's backdrop and the textures of its stage.

The backdrop is a moonlit graveyard in purple-blacks, all flat silhouettes: far hills, a graveyard skyline with a
chapel, dead trees and an iron fence, and the dark ground running down to the bottom of the screen. The road the
ghosts ride along is not painted: it is real, on the title stage, and so is the mist that drifts over it.

Run: python tools/title-backdrop.py
Writes, in Assets/Sprites/Title:
  TitleBackdrop.png  1920x1080 RGB, the picture behind the stage
  TitleRoad.png      1024x512 RGB, 8 by 6.2 units of road: kerbs at the top and bottom, a dashed line in the middle;
                     tiles left to right
  TitleMistPuff.png  256x256 RGBA, white with one soft lumpy puff of mist in alpha, clear at its edges
  TitleShadow.png    128x128 RGBA, black with a soft round shadow in alpha

They are imported uncompressed (TitleScreenBuilder sets it): block compression turns dark, smooth pictures like these
into visible squares. Shapes are drawn at twice the size and scaled down, so their edges are smooth.

Where things sit comes from the stage's camera (TitleStage: 2.6 up, 19 back, pitched -0.5 degrees (looking a little up), 26 degree view): the
true horizon is 48% of the way down the screen, the road (z -2.6 to 3.6 on the stage) covers 77% to 86% of the way
down, and the ghosts' wheels touch it between 78% and 84%.
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
ROAD_FAR = 0.768                    # where the stage's road's far kerb meets the screen
ROAD_W, ROAD_H = 1024, 512          # 8 units of road along it, 6.2 across
ROAD_UNITS = (8.0, 6.2)
PUFF = 256
BLOB = 128


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


def ground(img):
    """Under the graveyard the ground runs on to the bottom of the screen, darkening"""
    px = np.asarray(img).astype(np.float64)
    y = np.arange(H * SS, dtype=np.float64)[:, None] / (H * SS)
    fall = np.clip((y - ROAD_FAR) / (1 - ROAD_FAR), 0, 1)
    px *= (1 - 0.5 * fall)[..., None]
    return Image.fromarray(np.clip(np.round(px), 0, 255).astype(np.uint8), "RGB")


def periodic_noise(rng, h, w, scale, power=1.0):
    """Smooth noise that wraps left to right and top to bottom, from -1 to 1: white noise low-passed in the frequency
    domain"""
    fy = np.fft.fftfreq(h)[:, None] * h
    fx = np.fft.fftfreq(w)[None, :] * w
    r = np.sqrt(fx ** 2 + fy ** 2)
    spectrum = np.exp(-(r / scale) ** 2) / (1 + r) ** power
    n = np.fft.ifft2(np.fft.fft2(rng.standard_normal((h, w))) * spectrum).real
    n -= n.mean()
    return n / np.abs(n).max()


def road_texture():
    """Worn asphalt at night, purple-grey, with stone kerbs along both edges and a dashed line down the middle. The top
    of the picture is the far kerb. It wraps left to right, so the road can run as long as it likes. The road is unlit,
    like the painted backdrop, so these are the colours it shows"""
    rng = np.random.default_rng(SEED + 2)
    rnd = random.Random(SEED + 2)
    ppu_x, ppu_y = ROAD_W / ROAD_UNITS[0], ROAD_H / ROAD_UNITS[1]

    base = np.array((46, 38, 64), np.float64)
    img = np.ones((ROAD_H, ROAD_W, 3)) * base
    img += 9 * periodic_noise(rng, ROAD_H, ROAD_W, 6.0)[..., None]         # patches
    img += 5 * periodic_noise(rng, ROAD_H, ROAD_W, 60.0, 0.2)[..., None]   # grain
    img += rng.normal(0, 3, (ROAD_H, ROAD_W, 1))                           # grit

    # Two worn lanes, smoother and a touch darker where the wheels go
    v = np.arange(ROAD_H, dtype=np.float64)[:, None] / ROAD_H
    for lane in (0.30, 0.70):
        img -= 5 * np.exp(-((v - lane) / 0.07) ** 2)[..., None]

    out = Image.fromarray(np.clip(np.round(img), 0, 255).astype(np.uint8), "RGB")
    draw = ImageDraw.Draw(out, "RGBA")

    # Cracks: short jagged dark lines, drawn three times so they wrap
    for _ in range(14):
        x, y = rnd.uniform(0, ROAD_W), rnd.uniform(0.12, 0.88) * ROAD_H
        pts = [(x, y)]
        for _ in range(rnd.randint(3, 6)):
            x += rnd.uniform(-22, 22)
            y += rnd.uniform(-10, 10)
            pts.append((x, y))
        for dx in (-ROAD_W, 0, ROAD_W):
            draw.line([(px + dx, py) for px, py in pts], fill=(22, 17, 32, 170), width=2)

    # The dashed line: 1.8 long every 4, a little worn
    mid = ROAD_H / 2
    half = 0.06 * ppu_y
    for start in (0.4, 4.4):
        x0, x1 = start * ppu_x, (start + 1.8) * ppu_x
        for dx in (-ROAD_W, 0, ROAD_W):
            draw.rectangle((x0 + dx, mid - half, x1 + dx, mid + half), fill=(150, 140, 178, 235))
    line = np.asarray(out).astype(np.float64)
    wear = periodic_noise(rng, ROAD_H, ROAD_W, 40.0, 0.3)
    worn = (wear > 0.25)[..., None] & (np.abs(np.arange(ROAD_H) - mid) <= half + 1)[:, None, None]
    line = np.where(worn, line * 0.55 + base * 0.45, line)
    out = Image.fromarray(np.clip(np.round(line), 0, 255).astype(np.uint8), "RGB")
    draw = ImageDraw.Draw(out, "RGBA")

    # The kerbs: pale stone blocks with dark joints, a gutter on the road's side
    kerb = int(0.30 * ppu_y)
    gutter = int(0.06 * ppu_y)
    block = 0.7 * ppu_x
    blocks = int(round(ROAD_W / block))
    block = ROAD_W / blocks                 # a whole number of blocks, so the joints wrap too
    for top, gutter_at in ((0, kerb), (ROAD_H - kerb, ROAD_H - kerb - gutter)):
        draw.rectangle((0, top, ROAD_W, top + kerb), fill=(84, 74, 106))
        draw.rectangle((0, gutter_at, ROAD_W, gutter_at + gutter), fill=(24, 19, 34))
        for i in range(blocks):
            x = i * block
            shade = rnd.randint(-10, 10)
            draw.rectangle((x + 2, top + 2, x + block - 2, top + kerb - 2), fill=(84 + shade, 74 + shade, 106 + shade))
            draw.line((x, top, x, top + kerb), fill=(30, 24, 42), width=3)
    # The kerbs' top edges catch the light
    draw.line((0, kerb - 3, ROAD_W, kerb - 3), fill=(112, 102, 138), width=2)
    draw.line((0, ROAD_H - kerb + 2, ROAD_W, ROAD_H - kerb + 2), fill=(112, 102, 138), width=2)
    return out


def puff():
    """One lumpy puff of mist: a soft round falloff broken up by cloud noise"""
    rng = np.random.default_rng(SEED + 3)
    y, x = np.mgrid[0:PUFF, 0:PUFF].astype(np.float64)
    r = np.sqrt((x - PUFF / 2 + 0.5) ** 2 + (y - PUFF / 2 + 0.5) ** 2) / (PUFF / 2)
    fall = np.clip(1 - r, 0, 1) ** 1.6
    n = periodic_noise(rng, PUFF, PUFF, 3.5, 1.2)   # a few broad lumps, not cauliflower
    a = np.clip(fall * (0.8 + 0.3 * n), 0, 1)
    a[r >= 0.98] = 0
    rgba = np.empty((PUFF, PUFF, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.round(a / a.max() * 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def blob():
    """A soft round shadow, black, for under a scooter"""
    y, x = np.mgrid[0:BLOB, 0:BLOB].astype(np.float64)
    r = np.sqrt((x - BLOB / 2 + 0.5) ** 2 + (y - BLOB / 2 + 0.5) ** 2) / (BLOB / 2)
    a = np.clip(1 - r * r, 0, 1) ** 2
    rgba = np.zeros((BLOB, BLOB, 4), np.uint8)
    rgba[..., 3] = np.round(a * 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


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
    img = ground(img)
    return vignette(img.resize((W, H), Image.LANCZOS))


def main():
    os.makedirs(OUT, exist_ok=True)
    b = backdrop()
    b.save(os.path.join(OUT, "TitleBackdrop.png"))
    road_texture().save(os.path.join(OUT, "TitleRoad.png"))
    puff().save(os.path.join(OUT, "TitleMistPuff.png"))
    blob().save(os.path.join(OUT, "TitleShadow.png"))
    px = np.asarray(b).reshape(-1, 3)
    print("backdrop min", px.min(axis=0), "max", px.max(axis=0), "mean", px.mean(axis=0).round(1))
    road = np.asarray(Image.open(os.path.join(OUT, "TitleRoad.png"))).astype(int)
    print("road seam jump L/R", np.abs(road[:, 0] - road[:, -1]).mean().round(2),
          "typical step", np.abs(np.diff(road, axis=1)).mean().round(2))


if __name__ == "__main__":
    main()
