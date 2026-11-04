"""Draws the Steam achievement icons: one shape per icon on a full-bleed night-purple square, using the game's own
2D sprites where it has one (HUD package icons, the scroll UI's ghost, the cyan arrow) and flat drawn shapes elsewhere.

Run: python tools/achievement-icons.py
Writes docs/steam-store/achievements/<API_NAME>.jpg and <API_NAME>_locked.jpg (256x256, the files Steamworks wants),
plus docs/steam-store/achievement-icons-preview.png (every icon at 256 and 64 px, to check they read small).
API names match Assets/Scripts/Steam/Achievement.cs. Style: like most Steam games' icons, the art fills the square,
no rings, glows or gradients added; drawn shapes use white and the game arrow's cyan.
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
GREEN = (0, 255, 230)
LOCKED_BG = 30    # the game arrow's cyan (doa_newarrow.png)

SPRITES = os.path.join(ROOT, "Assets", "Sprites")
EASY_PACKAGE = os.path.join(SPRITES, "UI", "PackageIcon_Small.png")
MEDIUM_PACKAGE = os.path.join(SPRITES, "UI", "PackageIcon_Medium.png")
HARD_PACKAGE = os.path.join(SPRITES, "UI", "PackageIcon_Large.png")
GOLD_PACKAGE = os.path.join(SPRITES, "UI", "GoldPackageIcon 1.png")
GHOST = os.path.join(SPRITES, "GhostScroll.png")
ARROW = os.path.join(SPRITES, "UI", "doa_newarrow.png")


def shade(rgb, k):
    return tuple(int(v * k) for v in rgb)


# ---------- shapes ----------

def sprite(img, path, cx, cy, size):
    """Pastes a game sprite, trimmed to its pixels and scaled to fit a size x size box centred on (cx, cy)"""
    art = Image.open(path).convert("RGBA")
    art = art.crop(art.getbbox())
    k = size / max(art.size)
    art = art.resize((max(1, round(art.width * k)), max(1, round(art.height * k))), Image.LANCZOS)
    img.paste(art, (round(cx - art.width / 2), round(cy - art.height / 2)), art)


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


def first_delivery(img, d):
    sprite(img, EASY_PACKAGE, C, C, 640)


def golden_win(img, d):
    sprite(img, GOLD_PACKAGE, C, C, 700)


def tutorial_done(img, d):
    sprite(img, GHOST, C, C, 620)


def steal(img, d, path=MEDIUM_PACKAGE):
    sprite(img, path, C, C + 150, 440)
    arrow_over(d, C, C + 120, 340, GREEN)


def golden_steal(img, d):
    steal(img, d, GOLD_PACKAGE)


def hard_delivery(img, d):
    sprite(img, HARD_PACKAGE, C, C, 700)


def win(img, d):
    w, h = 420, 560
    top = C - h / 2 - 10
    d.ellipse([C - w / 2, top, C + w / 2, top + w], fill=WHITE)
    d.rectangle([C - w / 2, top + w / 2, C + w / 2, top + h], fill=WHITE)
    d.text((C, top + h * 0.5), "1", font=ImageFont.truetype(FONT, 330), fill=BG, anchor="mm")
    d.rectangle([C - w / 2 - 90, top + h, C + w / 2 + 90, top + h + 50], fill=GREEN)


def last_to_first(img, d):
    sprite(img, ARROW, C, C, 680)


def fell_in_water(img, d):
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
    ICONS[name](img, ImageDraw.Draw(img))
    return img


def locked(img):
    """Grey version: the same dark grey background on every icon, the shape lifted above it so it still shows.
    Takes the full-size colour render (its background is exactly BG there)"""
    mask = Image.new("L", img.size, 0)
    mask.putdata([255 if p == BG else 0 for p in img.getdata()])
    grey = ImageOps.grayscale(img).point(lambda v: int(64 + v * 0.55))
    grey.paste(LOCKED_BG, (0, 0), mask)
    return grey.convert("RGB")


def main():
    os.makedirs(OUT, exist_ok=True)
    for old in glob.glob(os.path.join(OUT, "*.jpg")):
        os.remove(old)
    sheet = Image.new("RGB", (len(ICONS) * 264 + 8, 264 + 2 * 72 + 8), (40, 40, 48))
    for i, name in enumerate(ICONS):
        full = render(name)
        big = full.resize((256, 256), Image.LANCZOS)
        grey = locked(full).resize((256, 256), Image.LANCZOS)
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
