"""Bloom + logo over the Blender render -> Steam main capsule (1232x706).
python -I composite.py <render.png> <logo.png> <out.png>"""
import sys
from PIL import Image, ImageFilter, ImageChops, ImageEnhance
render, logo, out = sys.argv[1:4]
im = Image.open(render).convert("RGB")
# bloom: keep bright parts, blur wide, screen back on
lum = im.convert("L").point(lambda v: min(255, max(0, v - 165) * 3))
bright = Image.composite(im, Image.new("RGB", im.size), lum)
for radius, gain in ((6, 0.5), (20, 0.6), (60, 0.55)):
    im = ImageChops.screen(im, ImageEnhance.Brightness(bright.filter(ImageFilter.GaussianBlur(radius))).enhance(gain))
im = ImageEnhance.Color(im).enhance(1.12)
# darken bottom-right a touch so the logo reads
from PIL import ImageDraw
shade = Image.new("L", im.size, 0)
ImageDraw.Draw(shade).ellipse((300, 470, 932, 820), fill=110)
shade = shade.filter(ImageFilter.GaussianBlur(70))
im = Image.composite(Image.new("RGB", im.size, (6, 4, 16)), im, shade)
# logo
lg = Image.open(logo).convert("RGBA")
lg = lg.crop(lg.getbbox())
W = 450
lg = lg.resize((W, round(lg.height * W / lg.width)), Image.LANCZOS)
x = (im.width - W) // 2
y = im.height - lg.height - 14
im = im.convert("RGBA")
im.alpha_composite(lg, (x, y))
im.convert("RGB").save(out)
print(out, im.size, "logo at", x, y, lg.size)
