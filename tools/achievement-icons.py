"""Draws the Steam achievement icons: one flat shape per icon on a full-bleed night-purple square.

Run: python tools/achievement-icons.py
Writes docs/steam-store/achievements/<API_NAME>.jpg and <API_NAME>_locked.jpg (256x256, the files Steamworks wants),
plus docs/steam-store/achievement-icons-preview.png (every icon at 256 and 64 px, to check they read small).
API names match Assets/Scripts/Steam/Achievement.cs. Style: like most Steam games' icons, the art fills the square,
flat colours only (no rings, glows, gradients or outlines), three colours across the set: white, gold, green.
"""
import glob
import math
import os

from PIL import Image, ImageDraw, ImageFont, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "docs", "steam-store", "achievements")
PREVIEW = os.path.join(ROOT, "docs", "steam-store", "achievement-icons-preview.png")
FONT = os.path.join(ROOT, "Assets", "Plugins", "TextMesh Pro", "Examples & Extras", "Fonts", "Bangers.ttf")

S = 1024                 # drawing size; saved at 256
C = S // 2

BG = (54, 28, 96)
WHITE = (242, 238, 250)
GOLD = (255, 196, 56)
GREEN = (62, 232, 170)


def shade(rgb, k):
    return tuple(int(v * k) for v in rgb)


# ---------- shapes ----------

def parcel(d, cx, cy, w, colour):
    """A flat parcel seen from the front: lid, body and one tape strip"""
    h = int(w * 0.82)
    top = cy - h // 2
    lid = int(h * 0.26)
    gap = int(w * 0.035)
    d.rectangle([cx - w // 2 + gap, top + lid, cx + w // 2 - gap, top + h], fill=colour)
    d.rectangle([cx - w // 2, top, cx + w // 2, top + lid - gap], fill=colour)
    t = int(w * 0.075)
    tape = shade(colour, 0.78)
    d.rectangle([cx - t, top, cx + t, top + lid - gap], fill=tape)
    d.rectangle([cx - t, top + lid, cx + t, top + h], fill=tape)
    return top, h


def crown(d, cx, by, w, colour):
    h = w * 0.6
    d.polygon([(cx - w / 2, by), (cx - w / 2, by - h * 0.7), (cx - w / 4, by - h * 0.3), (cx, by - h),
               (cx + w / 4, by - h * 0.3), (cx + w / 2, by - h * 0.7), (cx + w / 2, by)], fill=colour)


def ghost(d, cx, cy, w, colour):
    """The game's horned ghost as a flat silhouette with two eyes"""
    h = w * 1.12
    top = cy - h / 2
    for side in (-1, 1):
        bx = cx + side * w * 0.3
        d.polygon([(bx - w * 0.11, top + w * 0.2), (bx + w * 0.11, top + w * 0.2),
                   (bx + side * w * 0.2, top - w * 0.14)], fill=colour)
    d.ellipse([cx - w / 2, top, cx + w / 2, top + w], fill=colour)
    d.rectangle([cx - w / 2, top + w / 2, cx + w / 2, top + h - w * 0.12], fill=colour)
    bw = w / 3
    for i in range(3):
        x = cx - w / 2 + i * bw
        d.ellipse([x, top + h - w * 0.24, x + bw, top + h], fill=colour)
    for side in (-1, 1):
        ex, ey, r = cx + side * w * 0.17, top + w * 0.48, w * 0.075
        d.ellipse([ex - r, ey - r * 1.3, ex + r, ey + r * 1.3], fill=BG)


def arrow_over(d, cx, cy, r, colour, width=70):
    """A thick arc over the top, left to right, ending in an arrowhead pointing down-right"""
    box = [cx - r, cy - r, cx + r, cy + r]
    start, end = 200, 335
    d.arc(box, start, end, fill=colour, width=width)
    a = math.radians(end)
    rm = r - width / 2
    ex, ey = cx + rm * math.cos(a), cy + rm * math.sin(a)
    tx, ty = -math.sin(a), math.cos(a)          # tangent, direction of travel
    nx, ny = math.cos(a), math.sin(a)           # outward
    head = width * 1.5
    d.polygon([(ex + tx * head, ey + ty * head), (ex + nx * head * 0.9, ey + ny * head * 0.9),
               (ex - nx * head * 0.9, ey - ny * head * 0.9)], fill=colour)


def skull(d, cx, cy, r, colour):
    d.ellipse([cx - r, cy - r, cx + r, cy + r * 0.92], fill=colour)
    d.rounded_rectangle([cx - r * 0.55, cy + r * 0.5, cx + r * 0.55, cy + r * 1.2], radius=r * 0.14, fill=colour)
    for side in (-1, 1):
        ex = cx + side * r * 0.4
        d.ellipse([ex - r * 0.26, cy - r * 0.1, ex + r * 0.26, cy + r * 0.42], fill=BG)
    d.polygon([(cx, cy + r * 0.48), (cx - r * 0.11, cy + r * 0.7), (cx + r * 0.11, cy + r * 0.7)], fill=BG)
    for i in (-1, 1):
        x = cx + i * r * 0.19
        d.line([(x, cy + r * 0.92), (x, cy + r * 1.2)], fill=BG, width=int(r * 0.09))


# ---------- the nine icons ----------

def first_delivery(d):
    parcel(d, C, C + 20, 560, WHITE)


def golden_win(d):
    top, h = parcel(d, C, C + 110, 500, GOLD)
    crown(d, C, top - 40, 360, GOLD)


def tutorial_done(d):
    ghost(d, C, C + 30, 500, WHITE)


def steal(d, colour=WHITE):
    parcel(d, C, C + 150, 400, colour)
    arrow_over(d, C, C + 120, 330, GREEN)


def golden_steal(d):
    steal(d, GOLD)


def hard_delivery(d):
    skull(d, C, C - 50, 290, WHITE)


def win(d):
    w, h = 420, 560
    top = C - h / 2 - 10
    d.ellipse([C - w / 2, top, C + w / 2, top + w], fill=WHITE)
    d.rectangle([C - w / 2, top + w / 2, C + w / 2, top + h], fill=WHITE)
    d.text((C, top + h * 0.5), "1", font=ImageFont.truetype(FONT, 330), fill=BG, anchor="mm")
    d.rectangle([C - w / 2 - 90, top + h, C + w / 2 + 90, top + h + 50], fill=GREEN)


def last_to_first(d):
    w = 170
    d.rectangle([C - w / 2, C - 60, C + w / 2, C + 330], fill=GREEN)
    d.polygon([(C, C - 340), (C - 290, C - 30), (C + 290, C - 30)], fill=GREEN)


def fell_in_water(d):
    fx, fy = C - 40, C - 90
    d.ellipse([fx - 250, fy - 150, fx + 200, fy + 150], fill=WHITE)
    d.polygon([(fx + 160, fy), (fx + 350, fy - 160), (fx + 350, fy + 160)], fill=WHITE)
    d.ellipse([fx - 160, fy - 50, fx - 90, fy + 20], fill=BG)
    for i, y in enumerate((C + 190, C + 320)):
        pts = [(x, y + 34 * math.sin((x + i * 60) / 70)) for x in range(C - 380, C + 381, 8)]
        d.line(pts, fill=GREEN, width=56, joint="curve")


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
    img = Image.new("RGB", (S, S), BG)
    ICONS[name](ImageDraw.Draw(img))
    return img


def locked(img):
    """Grey and darker, contrast kept, so the shape still shows (what Steam's own Generate button makes)"""
    return ImageOps.grayscale(img).point(lambda v: int(v * 0.62 + 8)).convert("RGB")


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
