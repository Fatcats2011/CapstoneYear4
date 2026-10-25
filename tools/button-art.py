"""Draws the PlayStation and keyboard versions of the game's button prompts, matching MenUI_Buttons.png's Xbox ones (a
green disc with a white mark), for ButtonGlyph to swap in by device.
Usage: python tools/button-art.py
Writes PNGs into Assets/Sprites/UI/Buttons (the .meta files are kept, so re-running keeps the GUIDs and the scenes'
links). Needs Pillow. ButtonGlyphSetup (the editor) maps each Xbox sprite to these.
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "Assets", "Sprites", "UI", "Buttons")
BANGERS = os.path.join(ROOT, "Assets", "Plugins", "TextMesh Pro", "Examples & Extras", "Fonts", "Bangers.ttf")
SS = 4          # supersampling
SIZE = 256      # a face button's sprite

# MenUI_Buttons.png's colours
SLIME = (37, 181, 115)
SLIME_EDGE = (15, 138, 92)
SLIME_LIGHT = (31, 227, 154)
WHITE = (255, 255, 255)
KEY = (241, 241, 241)
KEY_SIDE = (190, 186, 200)
DEEP = (59, 13, 110)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".png")
    img.save(path, optimize=True)
    print(os.path.relpath(path, ROOT), img.size)


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def down(img):
    return img.resize((img.width // SS, img.height // SS), Image.LANCZOS)


def disc():
    """The green button: an edge ring, the face, a light rim at the top"""
    img = canvas(SIZE, SIZE)
    d = ImageDraw.Draw(img)
    s = SIZE * SS
    pad = 8 * SS
    d.ellipse((pad, pad, s - pad, s - pad), fill=SLIME_EDGE)
    edge = 14 * SS
    d.ellipse((pad + edge, pad + edge, s - pad - edge, s - pad - edge), fill=SLIME)
    light = Image.new("L", img.size, 0)
    ImageDraw.Draw(light).ellipse((pad + edge, pad + edge, s - pad - edge, s - pad - edge), fill=110)
    ImageDraw.Draw(light).ellipse((pad + edge, pad + edge + 10 * SS, s - pad - edge, s - pad - edge + 10 * SS), fill=0)
    light = light.filter(ImageFilter.GaussianBlur(3 * SS))
    img.paste(Image.new("RGBA", img.size, SLIME_LIGHT + (255,)), (0, 0), light)
    return img, d


def cross():
    img, d = disc()
    c, r, w = SIZE * SS / 2, 50 * SS, 24 * SS
    d.line((c - r, c - r, c + r, c + r), fill=WHITE, width=w)
    d.line((c - r, c + r, c + r, c - r), fill=WHITE, width=w)
    save(down(img), "PS_Cross")


def circle():
    img, d = disc()
    c, r, w = SIZE * SS / 2, 52 * SS, 22 * SS
    d.ellipse((c - r, c - r, c + r, c + r), outline=WHITE, width=w)
    save(down(img), "PS_Circle")


def square():
    img, d = disc()
    c, r, w = SIZE * SS / 2, 46 * SS, 22 * SS
    d.rectangle((c - r, c - r, c + r, c + r), outline=WHITE, width=w)
    save(down(img), "PS_Square")


def triangle():
    img, d = disc()
    c, r, w = SIZE * SS / 2, 60 * SS, 22 * SS
    pts = [(c + r * math.cos(math.radians(a)), c + 8 * SS + r * math.sin(math.radians(a))) for a in (-90, 30, 150)]
    d.line(pts + [pts[0], pts[1]], fill=WHITE, width=w, joint="curve")
    save(down(img), "PS_Triangle")


def text_on(d, box, label, size, colour):
    font = ImageFont.truetype(BANGERS, size * SS)
    l, t, r, b = d.textbbox((0, 0), label, font=font)
    x = (box[0] + box[2]) / 2 - (r - l) / 2 - l
    y = (box[1] + box[3]) / 2 - (b - t) / 2 - t
    d.text((x, y), label, font=font, fill=colour)


def shoulder(name, label):
    """L1 / R1: the bumper's rounded bar in green, its label in white"""
    w, h = 384, SIZE
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    pad = 20 * SS
    d.rounded_rectangle((pad, 50 * SS, w * SS - pad, h * SS - 50 * SS), radius=70 * SS, fill=SLIME_EDGE)
    d.rounded_rectangle((pad + 14 * SS, 64 * SS, w * SS - pad - 14 * SS, h * SS - 64 * SS), radius=56 * SS, fill=SLIME)
    text_on(d, (0, 0, w * SS, h * SS), label, 120, WHITE)
    save(down(img), name)


def key(name, label, width=SIZE, size=150):
    """A keycap: a pale key on a darker side, its label in the game's deep purple"""
    img = canvas(width, SIZE)
    d = ImageDraw.Draw(img)
    pad = 14 * SS
    d.rounded_rectangle((pad, pad + 18 * SS, width * SS - pad, SIZE * SS - pad), radius=40 * SS, fill=DEEP)
    d.rounded_rectangle((pad + 10 * SS, pad + 10 * SS, width * SS - pad - 10 * SS, SIZE * SS - pad - 22 * SS),
                        radius=32 * SS, fill=KEY_SIDE)
    d.rounded_rectangle((pad + 22 * SS, pad + 16 * SS, width * SS - pad - 22 * SS, SIZE * SS - pad - 46 * SS),
                        radius=26 * SS, fill=KEY)
    text_on(d, (0, 0, width * SS, SIZE * SS - 30 * SS), label, size, DEEP)
    save(down(img), name)


def write_metas():
    """Import settings for new files only, like the other UI sprites (Local.png's)"""
    template = open(os.path.join(ROOT, "Assets", "Sprites", "UI", "Local.png.meta"), encoding="utf-8").read()
    if not os.path.exists(OUT + ".meta"):
        _write(OUT + ".meta", "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % os.urandom(16).hex())
    for name in sorted(os.listdir(OUT)):
        path = os.path.join(OUT, name)
        if not name.endswith(".png") or os.path.exists(path + ".meta"):
            continue
        meta = template.replace("guid: a9e290a073d07804a9ae1e73bbb6724f", "guid: " + os.urandom(16).hex())
        meta = meta.replace("spriteID: 5e97eb03825dee720800000000000000", "spriteID: " + os.urandom(16).hex())
        _write(path + ".meta", meta)


def _write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(os.path.relpath(path, ROOT))


if __name__ == "__main__":
    cross()
    circle()
    square()
    triangle()
    shoulder("PS_L1", "L1")
    shoulder("PS_R1", "R1")
    key("Key_Space", "SPACE", width=384, size=110)
    key("Key_Backspace", "BKSP", width=384, size=110)
    key("Key_Q", "Q")
    key("Key_Tab", "TAB", size=120)
    key("Key_F", "F")
    key("Key_R", "R")
    write_metas()
