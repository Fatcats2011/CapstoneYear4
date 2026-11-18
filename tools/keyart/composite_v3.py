"""Post pass for the v3 capsule concepts: bloom on the bright parts, vignette, a touch of contrast, then
the game logo on top (never rendered in Blender).

  python tools/keyart/composite_v3.py <render.png> <out.png> <logo_cx> <logo_cy> <logo_width> [vignette] [small.png]

logo_cx/cy are the logo's centre and logo_width its width, as fractions of the frame. With [small.png] it also
writes a 231x87-ish thumbnail strip check (how the capsule reads when Steam shows it small).
"""
import sys, os
from PIL import Image, ImageFilter, ImageChops, ImageEnhance, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
src, out = sys.argv[1], sys.argv[2]
cx, cy, lwf = float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5])
vig = float(sys.argv[6]) if len(sys.argv) > 6 else 0.6
small = sys.argv[7] if len(sys.argv) > 7 and sys.argv[7] != "-" else None
GROUND = float(sys.argv[8]) if len(sys.argv) > 8 else 0.0   # how much the near pavement darkens (0 = off)
SIZE = tuple(int(v) for v in sys.argv[9].split("x")) if len(sys.argv) > 9 else (1232, 706)   # output size; logo width 0 = no logo

def ground_shade(w, h, amount):
    """Multiply layer: white above 55% height, darkening toward the bottom, more so toward the left (the empty
    pavement), so the near road stops competing with the riders."""
    g = Image.new("L", (w, h), 255)
    px = g.load()
    for y in range(h):
        t = min(max((y / h - 0.55) / 0.45, 0.0), 1.0) ** 1.3
        for x in range(w):
            px[x, y] = int(255 * (1 - amount * t * (1.0 - 0.55 * x / w)))
    return Image.merge("RGB", (g, g, g))


im = Image.open(src).convert("RGB")
if im.size != SIZE:
    im = im.resize(SIZE, Image.LANCZOS)
k = w0 = im.size[0] / 1232.0           # blur radii were tuned at 1232 wide
w, h = im.size

bright = im.point(lambda v: max(0, (v - 170)) * 255 // 85 if v > 170 else 0)
glow = Image.new("RGB", im.size)
for radius, gain in ((5 * k, 0.5), (18 * k, 0.45), (45 * k, 0.3)):
    glow = ImageChops.add(glow, ImageEnhance.Brightness(bright.filter(ImageFilter.GaussianBlur(radius))).enhance(gain))
res = ImageChops.screen(im, glow)

mask = Image.new("L", (w, h), 0)
ImageDraw.Draw(mask).ellipse((-w * 0.2, -h * 0.3, w * 1.2, h * 1.3), fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) * 0.22))
res = Image.composite(res, ImageEnhance.Brightness(res).enhance(vig), mask)
if GROUND > 0:
    res = ImageChops.multiply(res, ground_shade(w, h, GROUND))
res = ImageEnhance.Contrast(res).enhance(1.06)
res = ImageEnhance.Color(res).enhance(1.08)

if lwf <= 0:                          # no logo (e.g. the Library Hero: Steam lays the logo on separately)
    res.save(out)
    print("wrote", out, "(no logo)")
    sys.exit(0)
logo = Image.open(os.path.join(ROOT, "Assets", "Sprites", "DoA-Logo_02-08-24.png")).convert("RGBA")
bb = logo.getbbox()
logo = logo.crop(bb)
lw = int(w * lwf)
logo = logo.resize((lw, int(lw * logo.height / logo.width)), Image.LANCZOS)
# soft dark shadow behind the logo so it lifts off busy backgrounds
sh = Image.new("RGBA", logo.size, (0, 0, 0, 0))
sh.putalpha(logo.getchannel("A").point(lambda a: int(a * 0.75)))
pad = 40
shadow = Image.new("RGBA", (logo.width + pad * 2, logo.height + pad * 2), (0, 0, 0, 0))
shadow.alpha_composite(sh, (pad, pad + 6))
shadow = shadow.filter(ImageFilter.GaussianBlur(14))
res = res.convert("RGBA")
x0, y0 = int(w * cx - logo.width / 2), int(h * cy - logo.height / 2)
res.alpha_composite(shadow, (x0 - pad, y0 - pad)) if x0 - pad >= 0 and y0 - pad >= 0 else None
res.alpha_composite(logo, (max(0, x0), max(0, y0)))
res = res.convert("RGB")
res.save(out)
print("wrote", out, "logo box", (x0, y0, x0 + logo.width, y0 + logo.height))
if small:
    # Steam also shows capsules small; check readability at ~1/4 size next to full
    sm = res.resize((w // 4, h // 4), Image.LANCZOS)
    sm.save(small)
