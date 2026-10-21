"""Draws the Dispatch lobby's art: the UI sprites and the back room's tiling textures.

Usage: python tools/dispatch-art.py
Writes PNGs into Assets/Sprites/UI/Dispatch and Assets/Textures/Dispatch (the .meta files are kept, so re-running
keeps the GUIDs and the scene's links). Needs Pillow and numpy. See EDITOR-TODO.md 1c for where each one goes.
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
UI = os.path.join(ROOT, "Assets", "Sprites", "UI", "Dispatch")
TEX = os.path.join(ROOT, "Assets", "Textures", "Dispatch")
BANGERS = os.path.join(ROOT, "Assets", "Plugins", "TextMesh Pro", "Examples & Extras", "Fonts", "Bangers.ttf")

SS = 4  # supersampling for the vector shapes

# The game's palette (the menu, the loading screen's buttons)
WHITE = (241, 241, 241)  # the Local / Multiplayer wordmarks
PAPER = (255, 251, 233)
INK = (42, 26, 58)
DEEP = (59, 13, 110)
PURPLE = (99, 45, 145)
SLIME = (37, 181, 115)
SLIME_EDGE = (15, 138, 92)
SLIME_LIGHT = (31, 227, 154)
STAMP_RED = (216, 27, 60)
GOLD = (255, 205, 60)


def rgba(c, a=255):
    return (c[0], c[1], c[2], a)


def save(img, folder, name):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name)
    img.save(path, optimize=True)
    print(os.path.relpath(path, ROOT), img.size)


def down(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def noise(w, h, scale, seed, octaves=3, tile=False):
    """Smooth value noise in 0..1 (tiling when asked)"""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cells = max(1, int(scale * (2 ** o)))
        grid = rng.random((cells + 1, cells + 1))
        if tile:
            grid[-1, :] = grid[0, :]
            grid[:, -1] = grid[:, 0]
        layer = Image.fromarray((grid * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        out += np.asarray(layer, dtype=float) / 255 * amp
        total += amp
        amp *= 0.5
    return out / total


def specks(w, h, count, size, seed, tile=False):
    """A mask of small round dots (paper fibres, ink holes, cork grain)"""
    rng = random.Random(seed)
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    for _ in range(count):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(size * 0.4, size)
        shade = rng.randint(120, 255)
        for dx in ([-w, 0, w] if tile else [0]):
            for dy in ([-h, 0, h] if tile else [0]):
                d.ellipse((x + dx - r, y + dy - r, x + dx + r, y + dy + r), fill=shade)
    return m


def wobble(mask, amount, scale, seed):
    """Pushes a mask's edges about a little, so the shape looks drawn by hand"""
    w, h = mask.size
    dx = (noise(w, h, scale, seed) - 0.5) * 2 * amount
    dy = (noise(w, h, scale, seed + 1) - 0.5) * 2 * amount
    ys, xs = np.mgrid[0:h, 0:w]
    sx = np.clip(xs + dx, 0, w - 1).astype(int)
    sy = np.clip(ys + dy, 0, h - 1).astype(int)
    a = np.asarray(mask)
    return Image.fromarray(a[sy, sx])


def soften(mask, radius):
    """Rounds a mask's corners: blur, then cut back to a hard edge"""
    b = mask.filter(ImageFilter.GaussianBlur(radius))
    return b.point(lambda v: 255 if v > 128 else 0)


def shadow(mask, offset, blur, alpha):
    w, h = mask.size
    s = Image.new("L", (w, h), 0)
    s.paste(mask, offset)
    s = s.filter(ImageFilter.GaussianBlur(blur))
    return s.point(lambda v: v * alpha // 255)


def layer(base, colour, mask):
    fill = Image.new("RGBA", base.size, rgba(colour))
    base.alpha_composite(Image.composite(fill, Image.new("RGBA", base.size, (0, 0, 0, 0)), mask))


# ---------------------------------------------------------------- the wordmark

def wordmark():
    """DISPATCH in the hand-cut, off-kilter style of Local.png and Multiplayer.png"""
    text = "DISPATCH"
    size = 520
    font = ImageFont.truetype(BANGERS, size)
    # Each letter tilts and bobs a little, like the cut-out letters of the other two
    tilt = [4, -3, 5, -2, 3, -4, 2, -3]
    bob = [10, -14, 6, -10, 14, -6, 8, -12]
    grow = [1.06, 0.96, 1.0, 1.04, 0.97, 1.05, 0.98, 1.08]
    letters = []
    for i, ch in enumerate(text):
        f = ImageFont.truetype(BANGERS, int(size * grow[i]))
        l, t, r, b = f.getbbox(ch)
        g = Image.new("L", (r - l + 160, b - t + 160), 0)
        ImageDraw.Draw(g).text((80 - l, 80 - t), ch, font=f, fill=255)
        g = g.rotate(tilt[i], resample=Image.BICUBIC, expand=True)
        letters.append(g)
    gap = -150
    w = sum(g.width for g in letters) + gap * (len(letters) - 1) + 300
    h = max(g.height for g in letters) + 300
    m = Image.new("L", (w, h), 0)
    x = 150
    for i, g in enumerate(letters):
        y = (h - g.height) // 2 + bob[i]
        m = ImageChops.lighter(m, _placed(g, (w, h), (x, y)))
        x += g.width + gap
    # Fatter strokes: the other wordmarks' letters are chunky blobs, Bangers alone is too thin
    m = m.filter(ImageFilter.MaxFilter(15)).filter(ImageFilter.MaxFilter(9))
    # The slant the whole word has (the other wordmarks lean right)
    lean = 0.18
    m = m.transform((w + int(h * lean), h), Image.AFFINE, (1, lean, -h * lean, 0, 1, 0), Image.BICUBIC)
    m = wobble(m, 5, 6, 11)
    m = soften(m, 2.5)
    m = m.crop(m.getbbox())
    pad = 20
    out = Image.new("RGBA", (m.width + pad * 2, m.height + pad * 2), (0, 0, 0, 0))
    full = Image.new("L", out.size, 0)
    full.paste(m, (pad, pad))
    layer(out, WHITE, full)
    # Same height as Multiplayer.png's letters relative to its width, about 2.4k wide
    scale = 2400 / out.width
    out = out.resize((2400, int(out.height * scale)), Image.LANCZOS)
    save(out, UI, "Dispatch_Wordmark.png")


def _placed(glyph, size, at):
    m = Image.new("L", size, 0)
    m.paste(glyph, at)
    return m


# ---------------------------------------------------------------- the ticket

TW, TH = 512, 640  # the ticket sprites' size (9-sliced in Unity, so it stretches)
NOTCH = 22  # the notches cut into the ticket's sides


def ticket_shape(w, h, notch, teeth=True):
    """A delivery ticket: rounded top, a notch each side at the tear line, a zig-zag torn bottom"""
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    r = notch
    d.rounded_rectangle((0, 0, w - 1, h - 1), radius=r, fill=255)
    if teeth:
        tooth = notch * 1.4
        n = int(w / tooth)
        tooth = w / n
        y0 = h - notch
        d.rectangle((0, y0, w, h), fill=0)
        pts = [(0, y0)]
        for i in range(n):
            pts += [(i * tooth + tooth / 2, h - 1), ((i + 1) * tooth, y0)]
        pts += [(w, y0 - 1), (0, y0 - 1)]
        d.polygon(pts, fill=255)
    return m


def ticket_paper():
    w, h, s = TW * SS, TH * SS, SS
    shape = ticket_shape(w - 40 * s, h - 40 * s, NOTCH * s)
    m = Image.new("L", (w, h), 0)
    m.paste(shape, (12 * s, 12 * s))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    # The hard drop shadow of the mockup (down and right)
    out.alpha_composite(Image.merge("RGBA", [Image.new("L", (w, h), 0)] * 3 + [shadow(m, (10 * s, 16 * s), 3 * s, 95)]))
    layer(out, PAPER, m)
    out = down(out, TW, TH)
    m = down(m, TW, TH)
    # Paper grain: faint fibres and a warm edge
    px = np.asarray(out).astype(float)
    grain = noise(TW, TH, 40, 3, octaves=2)
    fibre = np.asarray(specks(TW, TH, 900, 1.2, 5)) / 255
    inside = np.asarray(m) / 255
    dark = (grain - 0.5) * 10 + fibre * 10
    edge = np.asarray(m.filter(ImageFilter.GaussianBlur(10))) / 255
    burn = (1 - edge) * 40 * inside
    for c, k in zip(range(3), (1.0, 1.1, 1.6)):
        px[..., c] -= (dark + burn * k) * inside
    out = Image.fromarray(np.clip(px, 0, 255).astype(np.uint8))
    save(out, UI, "Ticket_Paper.png")


def ticket_hiring():
    """The empty seat's ticket: a see-through blank with a dashed outline"""
    w, h, s = TW * SS, TH * SS, SS
    shape = ticket_shape(w - 40 * s, h - 40 * s, NOTCH * s)
    m = Image.new("L", (w, h), 0)
    m.paste(shape, (12 * s, 12 * s))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.alpha_composite(Image.merge("RGBA", [Image.new("L", (w, h), 0)] * 3 + [shadow(m, (10 * s, 16 * s), 3 * s, 40)]))
    layer(out, PAPER, m.point(lambda v: v * 75 // 255))
    # A dashed line just inside the edge
    inner = _shrink(m, 16 * s)
    ring = ImageChops.subtract(inner, _shrink(inner, 7 * s))
    dash = Image.new("L", (w, h), 0)
    dd = ImageDraw.Draw(dash)
    step = 46 * s
    for i in range(-h // step, (w + h) // step + 1):
        x = i * step
        dd.polygon([(x, 0), (x + step * 0.55, 0), (x + step * 0.55 - h, h), (x - h, h)], fill=255)
    # Dashes cut across the ring along the diagonal, which reads as a dashed outline all the way round
    layer(out, PAPER, ImageChops.multiply(ring, dash).point(lambda v: v * 200 // 255))
    save(down(out, TW, TH), UI, "Ticket_Hiring.png")


def _shrink(mask, by):
    b = mask.filter(ImageFilter.GaussianBlur(by / 2))
    return b.point(lambda v: 255 if v > 250 else 0) if by > 0 else mask


def ticket_header():
    """The company strip at the top of the ticket. White, so TicketView's header.color tints it the company colour"""
    w, h, s = 480, 96, SS
    W, H = w * s, h * s
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, W - 1, H - 1), radius=14 * s, fill=255)
    m = wobble(m, 2 * s, 5, 21)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    layer(out, (255, 255, 255), m)
    # A slightly darker band at the bottom gives it a printed, inked look once tinted
    band = Image.new("L", (W, H), 0)
    ImageDraw.Draw(band).rectangle((0, H - 14 * s, W, H), fill=255)
    layer(out, (215, 215, 215), ImageChops.multiply(band, m))
    save(down(out, w, h), UI, "Ticket_Header.png")


def ticket_pin():
    """A push pin. The head is near-white so its Image colour can tint it (the company colour, as in the mockup)"""
    n = 128 * SS
    s = SS
    out = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    cx, cy, r = n * 0.46, n * 0.42, n * 0.30
    head = Image.new("L", (n, n), 0)
    ImageDraw.Draw(head).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    out.alpha_composite(Image.merge("RGBA", [Image.new("L", (n, n), 0)] * 3 + [shadow(head, (int(n * 0.07), int(n * 0.10)), 4 * s, 110)]))
    ring = Image.new("L", (n, n), 0)
    o = 6 * s
    ImageDraw.Draw(ring).ellipse((cx - r - o, cy - r - o, cx + r + o, cy + r + o), fill=255)
    layer(out, (34, 34, 34), ring)
    layer(out, (225, 225, 225), head)
    # Shading: a lighter dome and a highlight (stays readable after tinting)
    dome = Image.new("L", (n, n), 0)
    rr = r * 0.78
    ImageDraw.Draw(dome).ellipse((cx - rr - r * 0.12, cy - rr - r * 0.12, cx + rr - r * 0.12, cy + rr - r * 0.12), fill=255)
    layer(out, (255, 255, 255), ImageChops.multiply(dome, head).filter(ImageFilter.GaussianBlur(5 * s)))
    hl = Image.new("L", (n, n), 0)
    hr = r * 0.22
    hx, hy = cx - r * 0.38, cy - r * 0.40
    ImageDraw.Draw(hl).ellipse((hx - hr, hy - hr * 0.7, hx + hr, hy + hr * 0.7), fill=230)
    layer(out, (255, 255, 255), hl)
    save(down(out, 128, 128), UI, "Ticket_Pin.png")


def ticket_stamp():
    """The DISPATCHED stamp's inked frame. The word itself is the stamp object's TMP_Text (TicketView sets it)"""
    w, h, s = 512, 160, SS
    W, H = w * s, h * s
    outer = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(outer)
    d.rounded_rectangle((6 * s, 6 * s, W - 6 * s, H - 6 * s), radius=20 * s, fill=255)
    hole = Image.new("L", (W, H), 0)
    ImageDraw.Draw(hole).rounded_rectangle((18 * s, 18 * s, W - 18 * s, H - 18 * s), radius=12 * s, fill=255)
    inner = Image.new("L", (W, H), 0)
    ImageDraw.Draw(inner).rounded_rectangle((26 * s, 26 * s, W - 26 * s, H - 26 * s), radius=8 * s, fill=255)
    hole2 = Image.new("L", (W, H), 0)
    ImageDraw.Draw(hole2).rounded_rectangle((31 * s, 31 * s, W - 31 * s, H - 31 * s), radius=6 * s, fill=255)
    frame = ImageChops.lighter(ImageChops.subtract(outer, hole), ImageChops.subtract(inner, hole2))
    frame = wobble(frame, 3 * s, 7, 31)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # A faint wash of ink inside, as a rubber stamp leaves
    layer(out, STAMP_RED, hole.point(lambda v: v * 28 // 255))
    layer(out, STAMP_RED, frame)
    out = down(out, w, h)
    # Patchy ink: holes where the stamp didn't take
    a = np.asarray(out).astype(float)
    patch = noise(w, h, 9, 41, octaves=3)
    holes = np.asarray(specks(w, h, 260, 2.4, 43)) / 255
    keep = np.clip((patch - 0.30) * 3.0, 0.35, 1.0) * (1 - holes * 0.85)
    a[..., 3] *= keep
    save(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)), UI, "Ticket_Stamp.png")


def ticket_stamp_ink():
    """A grunge mask for the stamp's text: put it over the word, coloured the paper, to knock holes in the ink"""
    w, h = 512, 160
    a = np.zeros((h, w, 4), dtype=np.uint8)
    a[..., :3] = PAPER
    patch = noise(w, h, 9, 51, octaves=3)
    holes = np.asarray(specks(w, h, 300, 2.2, 53)) / 255
    alpha = np.clip((0.42 - patch) * 3.0, 0, 0.7) + holes * 0.8
    a[..., 3] = np.clip(alpha * 255, 0, 255).astype(np.uint8)
    save(Image.fromarray(a), UI, "Ticket_StampInk.png")


def ticket_mine():
    """This machine's own ticket: a slime-green star with a deep purple edge"""
    n, s = 128, SS
    N = n * s
    def star(r_out, r_in):
        pts = []
        for i in range(10):
            r = r_out if i % 2 == 0 else r_in
            t = -math.pi / 2 + i * math.pi / 5
            pts.append((N / 2 + r * math.cos(t), N / 2 + r * math.sin(t) + N * 0.02))
        m = Image.new("L", (N, N), 0)
        ImageDraw.Draw(m).polygon(pts, fill=255)
        return soften(m, 3 * s)
    edge = star(N * 0.48, N * 0.22)
    body = star(N * 0.40, N * 0.17)
    out = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    layer(out, DEEP, edge)
    layer(out, SLIME, body)
    shine = star(N * 0.26, N * 0.11)
    layer(out, SLIME_LIGHT, shine.filter(ImageFilter.GaussianBlur(6 * s)))
    save(down(out, n, n), UI, "Ticket_Mine.png")


def ticket_glow():
    """The warm light a ticket gets when it changes (TicketView's highlight). Animate its alpha up, then back"""
    w, h = 640, 768
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle((80, 80, w - 80, h - 80), radius=40, fill=255)
    m = m.filter(ImageFilter.GaussianBlur(36))
    out = Image.new("RGBA", (w, h), rgba((255, 240, 180), 0))
    out.putalpha(m.point(lambda v: v * 170 // 255))
    save(out, UI, "Ticket_Glow.png")


# ---------------------------------------------------------------- the banner and the counter

def banner():
    """ALL ORDERS DISPATCHED: a slime-green ribbon with folded tails (the words are the banner's TMP_Text)"""
    w, h, s = 1600, 300, SS
    W, H = w * s, h * s
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    tail = 190 * s
    band_t, band_b = 40 * s, 230 * s
    tail_t, tail_b = 90 * s, 280 * s
    def poly(pts):
        m = Image.new("L", (W, H), 0)
        ImageDraw.Draw(m).polygon(pts, fill=255)
        return m
    # Tails behind, with their fold
    left = poly([(0, tail_t), (tail + 60 * s, tail_t), (tail + 60 * s, tail_b), (0, tail_b), (70 * s, (tail_t + tail_b) / 2)])
    right = poly([(W, tail_t), (W - tail - 60 * s, tail_t), (W - tail - 60 * s, tail_b), (W, tail_b), (W - 70 * s, (tail_t + tail_b) / 2)])
    fold_l = poly([(tail, band_b), (tail + 60 * s, band_b), (tail + 60 * s, tail_b)])
    fold_r = poly([(W - tail, band_b), (W - tail - 60 * s, band_b), (W - tail - 60 * s, tail_b)])
    band = poly([(tail, band_t), (W - tail, band_t), (W - tail, band_b), (tail, band_b)])
    everything = ImageChops.lighter(ImageChops.lighter(left, right), band)
    out.alpha_composite(Image.merge("RGBA", [Image.new("L", (W, H), 0)] * 3 + [shadow(everything, (8 * s, 12 * s), 2 * s, 110)]))
    edge = 9 * s
    for m, colour in ((left, SLIME_EDGE), (right, SLIME_EDGE)):
        layer(out, DEEP, m.filter(ImageFilter.MaxFilter(_odd(edge))))
        layer(out, colour, m)
    layer(out, DEEP, ImageChops.lighter(fold_l, fold_r))
    layer(out, DEEP, band.filter(ImageFilter.MaxFilter(_odd(edge))))
    layer(out, SLIME, band)
    # A lighter top half, like the buttons' rim light
    top = poly([(tail, band_t), (W - tail, band_t), (W - tail, band_t + 22 * s), (tail, band_t + 22 * s)])
    layer(out, SLIME_LIGHT, ImageChops.multiply(top, band).point(lambda v: v * 170 // 255))
    save(down(out, w, h), UI, "Banner.png")


def _odd(n):
    return n + 1 if n % 2 == 0 else n


def pill():
    """The dark pill behind the X / Y READY counter and the matchmaking line"""
    w, h, s = 256, 96, SS
    W, H = w * s, h * s
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, W - 1, H - 1), radius=H // 2, fill=255)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.putalpha(m.point(lambda v: v * 105 // 255))
    t = 4 * s
    inner = Image.new("L", (W, H), 0)
    ImageDraw.Draw(inner).rounded_rectangle((t, t, W - 1 - t, H - 1 - t), radius=H // 2 - t, fill=255)
    rim = ImageChops.subtract(m, inner)
    layer(out, SLIME_LIGHT, rim.point(lambda v: v * 120 // 255))
    save(down(out, w, h), UI, "Pill.png")


# ---------------------------------------------------------------- the back room (tiling textures)

def cork():
    n = 1024
    rng = np.random.default_rng(61)
    base = np.array([150, 98, 56], dtype=float)
    lo = noise(n, n, 6, 62, octaves=4, tile=True)
    grain = np.asarray(specks(n, n, 26000, 2.6, 63, tile=True)) / 255
    pits = np.asarray(specks(n, n, 5200, 2.0, 64, tile=True)) / 255
    light = np.asarray(specks(n, n, 7000, 1.8, 65, tile=True)) / 255
    px = np.ones((n, n, 3)) * base
    px *= (0.82 + lo[..., None] * 0.30)
    px *= (1 - grain[..., None] * 0.18)
    px *= (1 - pits[..., None] * 0.42)
    px += light[..., None] * np.array([40, 30, 18])
    px += rng.normal(0, 4, (n, n, 1))
    save(Image.fromarray(np.clip(px, 0, 255).astype(np.uint8)), TEX, "Cork.png")


def wood_frame():
    """Dark stained wood for the board's frame (grain runs along U)"""
    w, h = 1024, 256
    ys = np.linspace(0, 1, h)[:, None]
    xs = np.linspace(0, 1, w)[None, :]
    warp = noise(w, h, 3, 71, octaves=3, tile=True)
    rings = np.sin((ys * 18 + warp * 3.5) * math.pi * 2) * 0.5 + 0.5
    fine = noise(w, h, 64, 72, octaves=2, tile=True)
    base = np.array([74, 42, 20], dtype=float)
    px = base * (0.75 + rings[..., None] * 0.35 + (fine[..., None] - 0.5) * 0.25)
    save(Image.fromarray(np.clip(px, 0, 255).astype(np.uint8)), TEX, "Wood_Frame.png")


def depot_wall():
    """Painted purple planks for the depot's back wall (planks run along V; 4 per tile)"""
    n = 1024
    planks = 4
    px = np.zeros((n, n, 3))
    base = np.array([106, 31, 192], dtype=float)  # the mockup's wall, before the room's dim light
    warp = noise(n, n, 4, 81, octaves=3, tile=True)
    fine = noise(n, n, 48, 82, octaves=2, tile=True)
    xs = np.arange(n)[None, :].repeat(n, 0)
    plank = xs // (n // planks)
    shade = np.array([1.0, 0.94, 1.03, 0.97])[plank]
    grain = np.sin((xs / n * planks * 6 + warp * 2.5) * math.pi * 2) * 0.5 + 0.5
    px[:] = base
    px *= (shade * (0.92 + grain * 0.06 + (fine - 0.5) * 0.10))[..., None]
    # The seams between planks, and a nail top and bottom
    seam = (xs % (n // planks)) < 6
    px[seam] = px[seam] * 0.45
    img = Image.fromarray(np.clip(px, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    for p in range(planks):
        cx = p * (n // planks) + (n // planks) // 2
        for cy in (60, n - 60 + 0):
            d.ellipse((cx - 9, cy - 9, cx + 9, cy + 9), fill=(60, 20, 100))
            d.ellipse((cx - 6, cy - 7, cx + 4, cy + 3), fill=(150, 110, 200))
    # A scuffed skirting of grime at the bottom would break the tiling; keep the tile even, light it in the scene
    save(img, TEX, "Depot_Wall.png")


def depot_floor():
    """Dark purple concrete with painted guide lines (2 x 2 slabs per tile)"""
    n = 1024
    base = np.array([58, 20, 112], dtype=float)
    lo = noise(n, n, 5, 91, octaves=4, tile=True)
    fine = noise(n, n, 90, 92, octaves=2, tile=True)
    px = np.ones((n, n, 3)) * base
    px *= (0.80 + lo[..., None] * 0.30 + (fine[..., None] - 0.5) * 0.12)
    stains = np.asarray(specks(n, n, 50, 40, 93, tile=True).filter(ImageFilter.GaussianBlur(20))) / 255
    px *= (1 - stains[..., None] * 0.25)
    xs = np.arange(n)
    joint = (xs % (n // 2)) < 4
    px[:, joint] *= 0.55
    px[joint, :] *= 0.55
    save(Image.fromarray(np.clip(px, 0, 255).astype(np.uint8)), TEX, "Depot_Floor.png")


# ---------------------------------------------------------------- import settings

# 9-slice borders (left, bottom, right, top) for the sprites that stretch; the rest keep their shape
BORDERS = {
    "Ticket_Header.png": (24, 16, 24, 16),
    "Ticket_Stamp.png": (40, 40, 40, 40),
    "Banner.png": (260, 0, 260, 0),
    "Pill.png": (48, 0, 48, 0),
}


def write_metas():
    """Import settings for new files only: UI sprites like Local.png's, the room's textures repeating with mipmaps"""
    template = open(os.path.join(ROOT, "Assets", "Sprites", "UI", "Local.png.meta"), encoding="utf-8").read()
    for folder, sprite in ((UI, True), (TEX, False)):
        if not os.path.exists(folder + ".meta"):
            _write(folder + ".meta", _folder_meta())
        for name in sorted(os.listdir(folder)):
            path = os.path.join(folder, name)
            if not name.endswith(".png") or os.path.exists(path + ".meta"):
                continue
            meta = template.replace("guid: a9e290a073d07804a9ae1e73bbb6724f", "guid: " + os.urandom(16).hex())
            meta = meta.replace("spriteID: 5e97eb03825dee720800000000000000", "spriteID: " + os.urandom(16).hex())
            if sprite:
                l, b, r, t = BORDERS.get(name, (0, 0, 0, 0))
                meta = meta.replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}", f"spriteBorder: {{x: {l}, y: {b}, z: {r}, w: {t}}}")
                if Image.open(path).width > 2048:
                    meta = meta.replace("maxTextureSize: 2048", "maxTextureSize: 4096")
            else:
                for a, b in (("enableMipMap: 0", "enableMipMap: 1"), ("wrapU: 1", "wrapU: 0"), ("wrapV: 1", "wrapV: 0"),
                             ("spriteMode: 1", "spriteMode: 0"), ("textureType: 8", "textureType: 0"),
                             ("alphaIsTransparency: 1", "alphaIsTransparency: 0"), ("aniso: 1", "aniso: 4")):
                    meta = meta.replace(a, b)
            _write(path + ".meta", meta)


def _folder_meta():
    return ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n" % os.urandom(16).hex())


def _write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(os.path.relpath(path, ROOT))


if __name__ == "__main__":
    wordmark()
    ticket_paper()
    ticket_hiring()
    ticket_header()
    ticket_pin()
    ticket_stamp()
    ticket_stamp_ink()
    ticket_mine()
    ticket_glow()
    banner()
    pill()
    cork()
    wood_frame()
    depot_wall()
    depot_floor()
    write_metas()
