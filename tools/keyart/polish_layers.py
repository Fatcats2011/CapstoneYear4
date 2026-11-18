"""Extra layers for the Photoshop polish pass (polish.jsx): a soft focus light on the riders and fine film grain.

  python -P tools/keyart/polish_layers.py [out_dir]
"""
import sys, os, random
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "steam-store", "capsule-v3", "photoshop")
W, H = 1232, 706
FOCUS = (0.67, 0.50, 0.30, 0.42)       # centre x, y and radii, as fractions: the purple / orange / package group

# focus light: white in the middle fading out (used as Soft Light, low opacity)
a = Image.new("L", (W, H), 0)
cx, cy, rx, ry = FOCUS[0] * W, FOCUS[1] * H, FOCUS[2] * W, FOCUS[3] * H
ImageDraw.Draw(a).ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=255)
a = a.filter(ImageFilter.GaussianBlur(min(W, H) * 0.16))
focus = Image.new("RGBA", (W, H), (255, 244, 230, 0))
focus.putalpha(a)
focus.save(os.path.join(out, "05_focus_light.png"))

# grain: mid grey with fine monochrome noise (used as Overlay, low opacity)
rnd = random.Random(7)
g = Image.new("L", (W, H))
g.putdata([max(0, min(255, int(rnd.gauss(128, 22)))) for _ in range(W * H)])
g = g.filter(ImageFilter.GaussianBlur(0.4))
Image.merge("RGB", (g, g, g)).save(os.path.join(out, "08_grain.png"))
print("polish layers in", out)
