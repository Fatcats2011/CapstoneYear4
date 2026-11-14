"""Post pass for the capsule render: bloom on the bright parts, a light vignette, a touch more contrast,
then the logo on top.

  python tools/keyart/composite_capsule.py [scene_render.png] [out.png]

This stands in for the poster's Photoshop after-effects until those are done by hand.
"""
import sys, os
from PIL import Image, ImageFilter, ImageChops, ImageEnhance, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "steam-store", "capsule_scene_1232x706.png")
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "docs", "steam-store", "capsule_glow_preview_1232x706.png")

im = Image.open(src).convert("RGB")
w, h = im.size

# bloom: keep only the bright parts, blur them at two sizes, screen them back on
bright = im.point(lambda v: max(0, (v - 165)) * 255 // 90 if v > 165 else 0)
glow = Image.new("RGB", im.size)
for radius, gain in ((6, 0.55), (22, 0.45)):
    layer = bright.filter(ImageFilter.GaussianBlur(radius))
    layer = ImageEnhance.Brightness(layer).enhance(gain)
    glow = ImageChops.add(glow, layer)
res = ImageChops.screen(im, glow)

# vignette: darken the corners a little
mask = Image.new("L", (w, h), 0)
ImageDraw.Draw(mask).ellipse((-w * 0.25, -h * 0.35, w * 1.25, h * 1.35), fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) * 0.25))
res = Image.composite(res, ImageEnhance.Brightness(res).enhance(0.55), mask)

res = ImageEnhance.Contrast(res).enhance(1.08)
res = ImageEnhance.Color(res).enhance(1.06)

# logo, on top of everything (same place as the poster: bottom centre)
LOGO_WIDTH, LOGO_CENTRE = 0.345, (0.503, 0.855)        # fractions of the frame
logo = Image.open(os.path.join(ROOT, "Assets", "Sprites", "DoA-Logo_02-08-24.png")).convert("RGBA")
lw = int(w * LOGO_WIDTH)
logo = logo.resize((lw, int(lw * logo.height / logo.width)), Image.LANCZOS)
res = res.convert("RGBA")
res.alpha_composite(logo, (int(w * LOGO_CENTRE[0] - logo.width / 2), int(h * LOGO_CENTRE[1] - logo.height / 2)))
res = res.convert("RGB")
res.save(out)
print("wrote", out)
