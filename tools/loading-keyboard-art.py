"""Draws the loading screen's keyboard version, Resources/Loading/DOA_LoadingScreen_Keyboard.png, from DOA_LoadingScreenNEW.png: the
controller and its labels give way to the keyboard's keys (KeyboardControls), in the art's own lettering. The logo,
the headline and the orders' values stay. SceneManager shows it when every player here is on a keyboard.
Usage: python tools/loading-keyboard-art.py (needs Pillow; keeps the .meta, so re-running keeps the GUID)
"""
import importlib.util
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
UI = os.path.join(ROOT, "Assets", "Sprites", "UI")
SOURCE = os.path.join(UI, "DOA_LoadingScreenNEW.png")
OUT = os.path.join(ROOT, "Assets", "Resources", "Loading", "DOA_LoadingScreen_Keyboard.png")
BANGERS = os.path.join(ROOT, "Assets", "Plugins", "TextMesh Pro", "Examples & Extras", "Fonts", "Bangers.ttf")

# The controller and its labels; the background either side of them fills it (sampled left and right, per row). Below
# the logo; the right edge stops short of the orders' icons, further left beside the red one
AREA = (70, 325, 1500, 890)
RIGHT_LOW, LOW_FROM = 1440, 500
PANEL = (100, 340, 1430, 890)
# Right of the logo, the controller reaches higher (its trigger): cleared the same way
AREA_TOP = (560, 285, 1440, 340)
WHITE = (255, 255, 255, 255)
OUTLINE = (40, 6, 70, 255)

spec = importlib.util.spec_from_file_location("button_art", os.path.join(ROOT, "tools", "button-art.py"))
art = importlib.util.module_from_spec(spec)
spec.loader.exec_module(art)


def keycap(label, width=1, size=150):
    """A key from button-art.py's keycap, 256 px tall (width in key widths)"""
    w = int(art.SIZE * width)
    img = art.canvas(w, art.SIZE)
    d = ImageDraw.Draw(img)
    pad = 14 * art.SS
    S = art.SS
    d.rounded_rectangle((pad, pad + 18 * S, w * S - pad, art.SIZE * S - pad), radius=40 * S, fill=art.DEEP)
    d.rounded_rectangle((pad + 10 * S, pad + 10 * S, w * S - pad - 10 * S, art.SIZE * S - pad - 22 * S), radius=32 * S, fill=art.KEY_SIDE)
    d.rounded_rectangle((pad + 22 * S, pad + 16 * S, w * S - pad - 22 * S, art.SIZE * S - pad - 46 * S), radius=26 * S, fill=art.KEY)
    art.text_on(d, (0, 0, w * S, art.SIZE * S - 30 * S), label, size, art.DEEP)
    return art.down(img)


def clear(img):
    """Fills AREA (and AREA_TOP) with the background, blended from its left and right edges row by row"""
    fill(img, AREA_TOP, AREA_TOP[2])
    fill(img, AREA, RIGHT_LOW)

    # A framed panel over the patch, in the game's deep purple and slime green: the keys sit on it
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).rounded_rectangle(PANEL, radius=44, fill=(45, 8, 85, 175), outline=art.SLIME + (255,), width=8)
    img.alpha_composite(layer)


def fill(img, area, right_low):
    px = img.load()
    x0, y0, x1, y1 = area
    for y in range(y0, y1):
        xr = x1 if y < LOW_FROM else right_low
        left, right = px[x0, y], px[xr, y]
        for x in range(x0, xr):
            t = (x - x0) / (xr - x0)
            px[x, y] = tuple(int(left[c] + (right[c] - left[c]) * t) for c in range(4))


def word(img, xy, text, size=64):
    """The art's lettering: white, slanted, with a dark outline and drop shadow"""
    font = ImageFont.truetype(BANGERS, size)
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.text((xy[0] + 5, xy[1] + 6), text, font=font, fill=(20, 0, 40, 160), anchor="lm")
    d.text(xy, text, font=font, fill=WHITE, anchor="lm", stroke_width=5, stroke_fill=OUTLINE)
    img.alpha_composite(layer)


def keys(img, x, y, labels, height=110):
    """Keycaps side by side from (x, y) (their middle), each height tall; returns the x after the last"""
    for label, width, size in labels:
        cap = keycap(label, width, size)
        cap = cap.resize((int(cap.width * height / cap.height), height), Image.LANCZOS)
        img.alpha_composite(cap, (int(x), int(y - height / 2)))
        x += cap.width + 6
    return x


def main():
    img = Image.open(SOURCE).convert("RGBA")
    clear(img)

    # Two columns of rows: the key(s), then what they do
    rows_left = [
        ([("W", 1, 150)], "ACCELERATE"),
        ([("S", 1, 150)], "BRAKE"),
        ([("A", 1, 150), ("D", 1, 150)], "STEER"),
    ]
    rows_right = [
        ([("SPACE", 1.5, 110)], "BOOST"),
        ([("SHIFT", 1.5, 110)], "DRIFT"),
        ([("Q", 1, 150), ("E", 1, 150)], "MOVE CAMERA"),
    ]
    top, step = 445, 160
    for i, (caps, text) in enumerate(rows_left):
        after = keys(img, 170, top + i * step, caps)
        word(img, (after + 24, top + i * step), text)
    for i, (caps, text) in enumerate(rows_right):
        after = keys(img, 820, top + i * step, caps)
        word(img, (after + 24, top + i * step), text)

    img.save(OUT, optimize=True)
    print(os.path.relpath(OUT, ROOT), img.size)


if __name__ == "__main__":
    main()
