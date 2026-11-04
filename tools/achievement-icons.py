"""Draws the Steam achievement icons: flat 2D badges in the logo's colours.

Run: python tools/achievement-icons.py
Writes docs/steam-store/achievements/<API_NAME>.jpg and <API_NAME>_locked.jpg (256x256, the files Steamworks wants),
plus docs/steam-store/achievement-icons-preview.png (every icon at 256 and 64 px, to check they read small).
API names match Assets/Scripts/Steam/Achievement.cs. Design: docs/superpowers/specs/2026-11-04-achievement-icons-design.md
"""
import glob
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "docs", "steam-store", "achievements")
PREVIEW = os.path.join(ROOT, "docs", "steam-store", "achievement-icons-preview.png")
FONT = os.path.join(ROOT, "Assets", "Plugins", "TextMesh Pro", "Examples & Extras", "Fonts", "Bangers.ttf")

S = 1024                 # drawing size; saved at 256
C = S // 2
BADGE_R = 452
RIM = 34

CORNER = (16, 10, 28)
BADGE_IN = (96, 54, 150)
BADGE_OUT = (38, 20, 66)
GREEN = (62, 232, 170)
INK = (28, 14, 42)
WHITE = (246, 244, 252)
LAVENDER = (206, 178, 252)
GOLD = (255, 198, 58)
RED = (232, 58, 72)
ORANGE = (255, 140, 56)
WATER = (52, 132, 226)
GREY = (214, 214, 228)


def font(size):
    return ImageFont.truetype(FONT, size)


def shade(rgb, k):
    """k > 1 lightens towards white, k < 1 darkens"""
    if k >= 1:
        return tuple(int(v + (255 - v) * (k - 1)) for v in rgb)
    return tuple(int(v * k) for v in rgb)


# ---------- badge ----------

def badge():
    img = Image.new("RGB", (S, S), CORNER)
    # soft green glow behind the rim
    glow = Image.new("L", (S, S), 0)
    ImageDraw.Draw(glow).ellipse([C - BADGE_R - 26, C - BADGE_R - 26, C + BADGE_R + 26, C + BADGE_R + 26], fill=200)
    glow = glow.filter(ImageFilter.GaussianBlur(22))
    img.paste(Image.new("RGB", (S, S), GREEN), (0, 0), glow)
    d = ImageDraw.Draw(img)
    d.ellipse([C - BADGE_R, C - BADGE_R, C + BADGE_R, C + BADGE_R], fill=GREEN)
    # radial purple inside the rim
    r_in = BADGE_R - RIM
    for i in range(r_in, 0, -4):
        t = i / r_in
        col = tuple(int(BADGE_IN[j] * (1 - t) + BADGE_OUT[j] * t) for j in range(3))
        d.ellipse([C - i, C - i, C + i, C + i], fill=col)
    d.ellipse([C - r_in, C - r_in, C + r_in, C + r_in], outline=INK, width=10)
    return img


def stamp(img, layer, outline=26, clip=False):
    """Pastes a symbol layer with one thick dark outline round its whole silhouette. clip trims it to the badge"""
    if clip:
        r = BADGE_R - RIM - outline
        mask = Image.new("L", (S, S), 0)
        ImageDraw.Draw(mask).ellipse([C - r, C - r, C + r, C + r], fill=255)
        layer.putalpha(Image.composite(layer.getchannel("A"), mask, mask))
    alpha = layer.getchannel("A")
    grown = alpha.filter(ImageFilter.GaussianBlur(outline / 2)).point(lambda v: 255 if v > 6 else 0)
    img.paste(Image.new("RGB", (S, S), INK), (0, 0), grown)
    img.paste(layer, (0, 0), layer)


def new_layer():
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    return layer, ImageDraw.Draw(layer)


def text(d, xy, s, size, fill, anchor="mm", stroke=0, stroke_fill=INK):
    d.text(xy, s, font=font(size), fill=fill, anchor=anchor, stroke_width=stroke, stroke_fill=stroke_fill)


# ---------- parts ----------

def parcel(d, cx, cy, w, colour, tape=True):
    """A box seen from the front-right: front, top and side faces, with a tape strip"""
    k = int(w * 0.34)
    x0 = cx - (w + k) // 2
    y0 = cy - (w + k) // 2 + k
    front = [(x0, y0), (x0 + w, y0), (x0 + w, y0 + w), (x0, y0 + w)]
    top = [(x0, y0), (x0 + w, y0), (x0 + w + k, y0 - k), (x0 + k, y0 - k)]
    side = [(x0 + w, y0), (x0 + w + k, y0 - k), (x0 + w + k, y0 + w - k), (x0 + w, y0 + w)]
    d.polygon(front, fill=colour)
    d.polygon(top, fill=shade(colour, 1.25))
    d.polygon(side, fill=shade(colour, 0.78))
    if tape:
        t = w // 9
        m = x0 + w // 2
        tc = shade(colour, 0.66)
        d.polygon([(m - t, y0), (m + t, y0), (m + t + k, y0 - k), (m - t + k, y0 - k)], fill=tc)
        d.rectangle([m - t, y0, m + t, y0 + w], fill=tc)
    for a, b in [((x0, y0), (x0 + w, y0)), ((x0 + w, y0), (x0 + w, y0 + w)), ((x0 + w, y0), (x0 + w + k, y0 - k))]:
        d.line([a, b], fill=INK, width=12)
    return x0, y0, w, k


def tag(d, cx, cy, r, colour, label, size, ink=INK):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=colour, outline=INK, width=14)
    text(d, (cx, cy + size * 0.04), label, size, ink)


def ghost(d, cx, cy, w, colour):
    """The game's horned ghost: round head, wavy hem, two horns, sleepy eyes"""
    h = int(w * 1.15)
    top = cy - h // 2
    # horns
    hw = w * 0.2
    for side in (-1, 1):
        bx = cx + side * w * 0.3
        d.polygon([(bx - hw / 2, top + w * 0.16), (bx + hw / 2, top + w * 0.16),
                   (bx + side * w * 0.2, top - w * 0.16)], fill=shade(colour, 1.15))
    d.ellipse([cx - w / 2, top, cx + w / 2, top + w], fill=colour)
    d.rectangle([cx - w / 2, top + w / 2, cx + w / 2, top + h - w * 0.12], fill=colour)
    bumps = 3
    bw = w / bumps
    for i in range(bumps):
        x = cx - w / 2 + i * bw
        d.ellipse([x, top + h - w * 0.25, x + bw, top + h], fill=colour)
    # eyes: white, pupils, heavy lids
    ey = top + w * 0.45
    for side in (-1, 1):
        ex = cx + side * w * 0.17
        er = w * 0.12
        d.ellipse([ex - er, ey - er, ex + er, ey + er], fill=WHITE, outline=INK, width=8)
        d.ellipse([ex - er * 0.45, ey - er * 0.1, ex + er * 0.45, ey + er * 0.8], fill=INK)
        d.chord([ex - er, ey - er, ex + er, ey + er], 180, 360, fill=shade(colour, 0.82), outline=INK, width=8)
    return top, h


def hand(d, x, cy, size, colour):
    """A ghost hand reaching right from x: palm plus four fingers curled forward"""
    palm_r = size * 0.42
    d.ellipse([x - palm_r, cy - palm_r, x + palm_r, cy + palm_r], fill=colour)
    fw = size * 0.2
    for i in range(4):
        fy = cy - size * 0.45 + i * fw * 1.12
        d.rounded_rectangle([x, fy, x + size * 0.85 - abs(i - 1.5) * size * 0.1, fy + fw], radius=fw / 2, fill=colour,
                            outline=shade(colour, 0.7), width=6)
    d.rounded_rectangle([x - size * 0.2, cy + palm_r * 0.55, x + size * 0.38, cy + palm_r * 0.55 + fw],
                        radius=fw / 2, fill=colour, outline=shade(colour, 0.7), width=6)


def speed_lines(d, x, cy, n=3, length=150, gap=70):
    for i in range(n):
        y = cy + (i - (n - 1) / 2) * gap
        d.rounded_rectangle([x - length + abs(i - 1) * 40, y - 12, x, y + 12], radius=12, fill=GREEN)


def crown(d, cx, by, w, colour):
    h = w * 0.62
    pts = [(cx - w / 2, by), (cx - w / 2, by - h * 0.55), (cx - w / 4, by - h * 0.25), (cx, by - h),
           (cx + w / 4, by - h * 0.25), (cx + w / 2, by - h * 0.55), (cx + w / 2, by)]
    d.polygon(pts, fill=colour)
    for px, py in [(cx - w / 2, by - h * 0.55), (cx, by - h), (cx + w / 2, by - h * 0.55)]:
        d.ellipse([px - 22, py - 22, px + 22, py + 22], fill=colour)
    d.rectangle([cx - w / 2, by - 34, cx + w / 2, by], fill=shade(colour, 0.8))


def skull(d, cx, cy, r, colour, bg):
    d.ellipse([cx - r, cy - r, cx + r, cy + r * 0.9], fill=colour)
    d.rounded_rectangle([cx - r * 0.55, cy + r * 0.5, cx + r * 0.55, cy + r * 1.15], radius=r * 0.15, fill=colour)
    for side in (-1, 1):
        ex = cx + side * r * 0.42
        d.ellipse([ex - r * 0.27, cy - r * 0.12, ex + r * 0.27, cy + r * 0.42], fill=bg)
    d.polygon([(cx, cy + r * 0.45), (cx - r * 0.12, cy + r * 0.68), (cx + r * 0.12, cy + r * 0.68)], fill=bg)
    for i in (-1, 0, 1):
        d.line([(cx + i * r * 0.22, cy + r * 0.85), (cx + i * r * 0.22, cy + r * 1.15)], fill=bg, width=int(r * 0.08))


# ---------- the nine icons ----------

def first_delivery(img):
    layer, d = new_layer()
    parcel(d, C - 20, C + 20, 380, WHITE)
    tag(d, C + 200, C + 200, 108, GREEN, "1", 190)
    stamp(img, layer)


def golden_win(img):
    layer, d = new_layer()
    parcel(d, C, C + 90, 340, GOLD)
    crown(d, C + 30, C - 170, 300, GOLD)
    stamp(img, layer)


def tutorial_done(img):
    layer, d = new_layer()
    ghost(d, C - 30, C + 10, 390, LAVENDER)
    # learner plate
    px, py, pw = C + 150, C + 140, 190
    d.rounded_rectangle([px - pw / 2, py - pw / 2, px + pw / 2, py + pw / 2], radius=22, fill=WHITE, outline=INK, width=14)
    text(d, (px, py + 6), "L", 180, RED)
    stamp(img, layer)


def steal(img, colour=WHITE):
    layer, d = new_layer()
    x0, y0, w, k = parcel(d, C + 80, C + 10, 330, colour)
    hand(d, x0 - 40, y0 + w * 0.35, 230, LAVENDER)
    speed_lines(d, x0 - 160, y0 + w * 0.35)
    stamp(img, layer)


def golden_steal(img):
    steal(img, GOLD)


def hard_delivery(img):
    layer, d = new_layer()
    x0, y0, w, k = parcel(d, C - 30, C + 30, 380, WHITE, tape=False)
    skull(d, x0 + w / 2, y0 + w * 0.36, w * 0.24, INK, WHITE)
    tag(d, C + 200, C - 170, 118, RED, "80", 150, ink=WHITE)
    stamp(img, layer)


def win(img):
    layer, d = new_layer()
    w, h = 330, 420
    top = C - h // 2 - 10
    d.ellipse([C - w / 2, top, C + w / 2, top + w], fill=GREY)
    d.rectangle([C - w / 2, top + w / 2, C + w / 2, top + h], fill=GREY)
    d.rectangle([C - w / 2 + w * 0.78, top + w / 2, C + w / 2, top + h], fill=shade(GREY, 0.82))
    text(d, (C - 14, top + h * 0.5), "1", 280, INK)
    # grass at the foot
    gy = top + h
    d.rounded_rectangle([C - w / 2 - 70, gy - 30, C + w / 2 + 70, gy + 40], radius=34, fill=GREEN)
    for gx in range(int(C - w / 2 - 40), int(C + w / 2 + 60), 52):
        d.polygon([(gx - 20, gy - 20), (gx + 20, gy - 20), (gx + 4, gy - 80)], fill=GREEN)
    stamp(img, layer)


def last_to_first(img):
    layer, d = new_layer()
    pts = [(C - 270, C + 190), (C - 70, C + 20), (C + 10, C + 110), (C + 180, C - 90)]
    d.line(pts, fill=GREEN, width=64, joint="curve")
    for p in pts[:-1]:
        d.ellipse([p[0] - 32, p[1] - 32, p[0] + 32, p[1] + 32], fill=GREEN)
    # arrowhead pointing up-right
    tip = (C + 255, C - 175)
    d.polygon([tip, (C + 70, C - 140), (C + 220, C + 10)], fill=GREEN)
    text(d, (C - 220, C - 40), "4", 210, WHITE)
    text(d, (C + 130, C + 220), "1", 260, GOLD)
    stamp(img, layer)


def fell_in_water(img):
    layer, d = new_layer()
    # waves
    wy = C + 150
    d.rectangle([C - 330, wy + 40, C + 330, C + 330], fill=WATER)
    for i in range(-4, 5):
        x = C + i * 90
        d.ellipse([x - 60, wy, x + 60, wy + 100], fill=WATER)
    for i in range(-3, 4):
        x = C + i * 90 + 45
        d.arc([x - 40, wy + 10, x + 40, wy + 70], 200, 340, fill=WHITE, width=16)
    # fish, asleep
    fx, fy = C - 40, C - 10
    d.ellipse([fx - 190, fy - 115, fx + 150, fy + 115], fill=ORANGE)
    d.polygon([(fx + 120, fy), (fx + 270, fy - 120), (fx + 270, fy + 120)], fill=ORANGE)
    d.polygon([(fx - 40, fy - 100), (fx + 60, fy - 170), (fx + 80, fy - 90)], fill=shade(ORANGE, 0.85))
    d.arc([fx - 130, fy - 50, fx - 60, fy + 10], 20, 160, fill=INK, width=16)   # closed eye
    d.arc([fx - 200, fy + 10, fx - 130, fy + 60], 270, 360, fill=INK, width=12)  # mouth
    text(d, (C + 70, C - 200), "Z", 120, WHITE)
    text(d, (C + 150, C - 258), "z", 90, WHITE)
    text(d, (C + 210, C - 295), "z", 64, WHITE)
    stamp(img, layer, clip=True)


ICONS = {
    "FIRST_DELIVERY": first_delivery,
    "GOLDEN_WIN": golden_win,
    "TUTORIAL_DONE": tutorial_done,
    "STEAL": steal,
    "GOLDEN_STEAL": golden_steal,
    "HARD_DELIVERY": hard_delivery,
    "WIN": win,
    "LAST_TO_FIRST": last_to_first,
    "FELL_IN_WATER": fell_in_water,
}


def render(name):
    img = badge()
    ICONS[name](img)
    return img


def locked(img):
    """Grey and darker, contrast kept, so the symbol still shows"""
    grey = ImageOps.grayscale(img).point(lambda v: int(v * 0.62 + 8))
    return grey.convert("RGB")


def main():
    os.makedirs(OUT, exist_ok=True)
    for old in glob.glob(os.path.join(OUT, "*.jpg")):
        os.remove(old)
    sheet = Image.new("RGB", (len(ICONS) * 264 + 8, 264 + 2 * 72 + 8), (40, 40, 48))
    for i, name in enumerate(ICONS):
        big = render(name).resize((256, 256), Image.LANCZOS)
        grey = locked(big)
        big.save(os.path.join(OUT, name + ".jpg"), quality=95, subsampling=0)
        grey.save(os.path.join(OUT, name + "_locked.jpg"), quality=95, subsampling=0)
        x = 8 + i * 264
        sheet.paste(big, (x, 8))
        sheet.paste(big.resize((64, 64), Image.LANCZOS), (x + 96, 272))
        sheet.paste(grey.resize((64, 64), Image.LANCZOS), (x + 96, 344))
    sheet.save(PREVIEW)
    print("wrote", len(ICONS) * 2, "icons to", OUT)


if __name__ == "__main__":
    main()
