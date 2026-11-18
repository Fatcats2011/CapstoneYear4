"""Split the capsule's post pass (composite_v3.py) into separate full-canvas layers for Photoshop, then
build a layered PSD from them with to_photoshop.jsx (run by Photoshop over COM).

  python -P tools/keyart/to_photoshop.py [render_2x.png] [out_dir]

Layers (bottom to top): Render, Bloom (Screen), Vignette (Multiply), Ground Shade (Multiply), Brightness/Contrast and Vibrance
adjustment layers, Logo Shadow, Logo. Stacked, they match composite_v3.py's output.
"""
import sys, os
from PIL import Image, ImageFilter, ImageChops, ImageEnhance, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "steam-store", "capsule-v3", "hero_render_2x.png")
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "docs", "steam-store", "capsule-v3", "photoshop")
LOGO_CX, LOGO_CY, LOGO_W, VIG, GROUND = 0.25, 0.30, 0.42, 0.6, 0.35   # as composite_v3.py is run for the hero
SIZE = tuple(int(v) for v in sys.argv[3].split("x")) if len(sys.argv) > 3 else (1232, 706)
if len(sys.argv) > 4 and sys.argv[4] == "nologo":
    LOGO_W = 0
os.makedirs(out, exist_ok=True)

im = Image.open(src).convert("RGB").resize(SIZE, Image.LANCZOS)
w, h = im.size
k = w / 1232.0                         # blur radii were tuned at 1232 wide
im.save(os.path.join(out, "01_render.png"))

bright = im.point(lambda v: max(0, (v - 170)) * 255 // 85 if v > 170 else 0)
glow = Image.new("RGB", im.size)
for radius, gain in ((5 * k, 0.5), (18 * k, 0.45), (45 * k, 0.3)):
    glow = ImageChops.add(glow, ImageEnhance.Brightness(bright.filter(ImageFilter.GaussianBlur(radius))).enhance(gain))
glow.save(os.path.join(out, "02_bloom_screen.png"))

mask = Image.new("L", (w, h), 0)
ImageDraw.Draw(mask).ellipse((-w * 0.2, -h * 0.3, w * 1.2, h * 1.3), fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) * 0.22))
vig = mask.point(lambda m: int(255 * (VIG + (1 - VIG) * m / 255)))       # multiply: 1 in the middle, VIG at the edges
Image.merge("RGB", (vig, vig, vig)).save(os.path.join(out, "03_vignette_multiply.png"))
g = Image.new("L", (w, h), 255)
px = g.load()
for y in range(h):
    t = min(max((y / h - 0.55) / 0.45, 0.0), 1.0) ** 1.3
    for x in range(w):
        px[x, y] = int(255 * (1 - GROUND * t * (1.0 - 0.55 * x / w)))
Image.merge("RGB", (g, g, g)).save(os.path.join(out, "04_ground_shade_multiply.png"))

if LOGO_W <= 0:
    print("layers in", out, "(no logo)")
    sys.exit(0)
logo = Image.open(os.path.join(ROOT, "Assets", "Sprites", "DoA-Logo_02-08-24.png")).convert("RGBA")
logo = logo.crop(logo.getbbox())
lw = int(w * LOGO_W)
logo = logo.resize((lw, int(lw * logo.height / logo.width)), Image.LANCZOS)
x0, y0 = int(w * LOGO_CX - logo.width / 2), int(h * LOGO_CY - logo.height / 2)
pad = 40
sh = Image.new("RGBA", logo.size, (0, 0, 0, 0))
sh.putalpha(logo.getchannel("A").point(lambda a: int(a * 0.75)))
shadow = Image.new("RGBA", (logo.width + pad * 2, logo.height + pad * 2), (0, 0, 0, 0))
shadow.alpha_composite(sh, (pad, pad + 6))
shadow = shadow.filter(ImageFilter.GaussianBlur(14))
layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
layer.alpha_composite(shadow, (x0 - pad, y0 - pad))
layer.save(os.path.join(out, "06_logo_shadow.png"))
layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
layer.alpha_composite(logo, (x0, y0))
layer.save(os.path.join(out, "07_logo.png"))
print("layers in", out)
