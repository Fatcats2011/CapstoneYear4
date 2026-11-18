"""App icon, step 2: build the enhancement layers from the square and logo masks icon_masks.jsx exported.
Every layer is cut to the rounded square, so the corners stay clear.

  python -P tools/keyart/icon_layers.py [work_dir]

  sky.png        moon and a few stars in the empty top band (Screen)
  light.png      soft light from the top left (Soft Light)
  depth.png      darker toward the edges and bottom (Multiply)
  logo_shadow.png  soft drop shadow so the logo lifts off the background (Normal)
  rim.png        thin bevel on the square's edge: light on top, dark below (Normal)
"""
import sys, os, random, math
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
work = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "steam-store", "app-icon", "work")
rect = Image.open(os.path.join(work, "rect.png")).convert("RGBA").getchannel("A")
logo = Image.open(os.path.join(work, "logo.png")).convert("RGBA").getchannel("A")
W, H = rect.size


def save(rgb, alpha, name):
    im = Image.new("RGBA", (W, H), rgb if isinstance(rgb, tuple) else (0, 0, 0, 0))
    if not isinstance(rgb, tuple):
        im = rgb.convert("RGBA")
    im.putalpha(ImageChops.multiply(alpha, rect))
    im.save(os.path.join(work, name))


# moon + stars, top band (the logo starts about a quarter of the way down)
sky = Image.new("RGB", (W, H), (0, 0, 0))
d = ImageDraw.Draw(sky)
mx, my, mr = W * 0.80, H * 0.135, W * 0.068
glow = Image.new("L", (W, H), 0)
ImageDraw.Draw(glow).ellipse((mx - mr * 2.4, my - mr * 2.4, mx + mr * 2.4, my + mr * 2.4), fill=110)
glow = glow.filter(ImageFilter.GaussianBlur(mr * 0.9))
sky = Image.composite(Image.new("RGB", (W, H), (190, 200, 255)), sky, glow)
d = ImageDraw.Draw(sky)
d.ellipse((mx - mr, my - mr, mx + mr, my + mr), fill=(236, 236, 250))
d.ellipse((mx - mr * 0.25, my - mr * 0.45, mx + mr * 0.15, my - mr * 0.05), fill=(205, 205, 225))   # craters
d.ellipse((mx + mr * 0.2, my + mr * 0.15, mx + mr * 0.55, my + mr * 0.5), fill=(212, 212, 230))
rnd = random.Random(4)
for _ in range(9):
    x, y = rnd.uniform(W * 0.08, W * 0.92), rnd.uniform(H * 0.04, H * 0.22)
    if math.hypot(x - mx, y - my) < mr * 2:
        continue
    s = rnd.choice((1.6, 2.2, 3.0))                         # drawn soft, so they twinkle rather than read as pixels
    st = Image.new("L", (W, H), 0)
    ImageDraw.Draw(st).ellipse((x - s, y - s, x + s, y + s), fill=255)
    st = st.filter(ImageFilter.GaussianBlur(s * 0.45))
    sky = Image.composite(Image.new("RGB", (W, H), (235, 235, 255)), sky, st.point(lambda v: int(v * 0.8)))
save(sky, Image.new("L", (W, H), 255), "sky.png")

# light from the top left
light = Image.new("L", (W, H), 0)
ImageDraw.Draw(light).ellipse((-W * 0.35, -H * 0.45, W * 0.75, H * 0.55), fill=255)
light = light.filter(ImageFilter.GaussianBlur(W * 0.18))
save((255, 245, 255, 255), light, "light.png")

# depth: darker toward the edges, more at the bottom
dep = Image.new("L", (W, H), 0)
ImageDraw.Draw(dep).ellipse((W * 0.02, -H * 0.05, W * 0.98, H * 0.92), fill=255)
dep = dep.filter(ImageFilter.GaussianBlur(W * 0.16))
dep = dep.point(lambda v: 255 - v)                         # strong where the ellipse is not
save((40, 10, 70, 255), dep, "depth.png")

# logo drop shadow
sh = logo.filter(ImageFilter.GaussianBlur(W * 0.022)).point(lambda v: int(v * 0.75))
sh = ImageChops.offset(sh, 0, int(H * 0.018))
save((15, 0, 30, 255), sh, "logo_shadow.png")

# rim: a thin band just inside the edge, light on the upper half, dark on the lower
inner = rect.filter(ImageFilter.MinFilter(5))
band = ImageChops.subtract(rect, inner).filter(ImageFilter.GaussianBlur(0.8))
top = Image.linear_gradient("L").resize((W, H))            # 0 at top -> 255 at bottom
rim = Image.new("RGBA", (W, H))
lightband = ImageChops.multiply(band, top.point(lambda v: int(max(0, 255 - v * 2.2))))
darkband = ImageChops.multiply(band, top.point(lambda v: int(max(0, (v - 140) * 2.2))))
rim = Image.composite(Image.new("RGB", (W, H), (255, 235, 255)), Image.new("RGB", (W, H), (20, 0, 40)), lightband.point(lambda v: 255 if v > 0 else 0))
save(rim, ImageChops.add(lightband.point(lambda v: int(v * 0.55)), darkband.point(lambda v: int(v * 0.5))), "rim.png")
print("icon layers in", work)
