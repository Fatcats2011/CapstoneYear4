"""Draws the language flags for Options → LANGUAGE and the title screen: simple, flat shapes with rounded corners and a
dark outline, 96x64. Spain's for Spanish, Mexico's for Latin American Spanish, Portugal's and Brazil's for the two
Portugueses, the UK's for English. Writes Assets/Sprites/UI/Flags/<locale>.png. Run: python tools/flag-art.py"""
import math
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'Assets', 'Sprites', 'UI', 'Flags')
W, H, S = 96, 64, 4  # drawn at 4x, then shrunk for smooth edges
OUTLINE = (30, 20, 40, 255)


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) + (255,)


def vertical(d, colours, widths=None):
    widths = widths or [1] * len(colours)
    total, x = sum(widths), 0
    for c, w in zip(colours, widths):
        d.rectangle([x * W * S / total, 0, (x + w) * W * S / total, H * S], fill=rgb(c))
        x += w


def horizontal(d, colours, heights=None):
    heights = heights or [1] * len(colours)
    total, y = sum(heights), 0
    for c, h in zip(colours, heights):
        d.rectangle([0, y * H * S / total, W * S, (y + h) * H * S / total], fill=rgb(c))
        y += h


def nordic(d, field, cross, inner=None):
    d.rectangle([0, 0, W * S, H * S], fill=rgb(field))
    x, t = 30 * S, 10 * S
    d.rectangle([x, 0, x + t, H * S], fill=rgb(cross))
    d.rectangle([0, H * S / 2 - t / 2, W * S, H * S / 2 + t / 2], fill=rgb(cross))


def circle(d, cx, cy, r, colour):
    d.ellipse([(cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S], fill=rgb(colour))


def star(d, cx, cy, r, colour, rotation=-90):
    points = []
    for i in range(10):
        radius = r if i % 2 == 0 else r * 0.42
        a = math.radians(rotation + i * 36)
        points.append(((cx + math.cos(a) * radius) * S, (cy + math.sin(a) * radius) * S))
    d.polygon(points, fill=rgb(colour))


def uk(d):
    d.rectangle([0, 0, W * S, H * S], fill=rgb('#012169'))
    for a, b in (((0, 0), (W, H)), ((W, 0), (0, H))):
        d.line([(a[0] * S, a[1] * S), (b[0] * S, b[1] * S)], fill=rgb('#FFFFFF'), width=14 * S)
        d.line([(a[0] * S, a[1] * S), (b[0] * S, b[1] * S)], fill=rgb('#C8102E'), width=5 * S)
    d.rectangle([W * S / 2 - 10 * S, 0, W * S / 2 + 10 * S, H * S], fill=rgb('#FFFFFF'))
    d.rectangle([0, H * S / 2 - 10 * S, W * S, H * S / 2 + 10 * S], fill=rgb('#FFFFFF'))
    d.rectangle([W * S / 2 - 6 * S, 0, W * S / 2 + 6 * S, H * S], fill=rgb('#C8102E'))
    d.rectangle([0, H * S / 2 - 6 * S, W * S, H * S / 2 + 6 * S], fill=rgb('#C8102E'))


def turkey(d):
    d.rectangle([0, 0, W * S, H * S], fill=rgb('#E30A17'))
    circle(d, 38, 32, 16, '#FFFFFF')
    circle(d, 42, 32, 13, '#E30A17')
    star(d, 58, 32, 8, '#FFFFFF', rotation=180)


def czech(d):
    horizontal(d, ['#FFFFFF', '#D7141A'])
    d.polygon([(0, 0), (W * S / 2, H * S / 2), (0, H * S)], fill=rgb('#11457E'))


def mexico(d):
    vertical(d, ['#006847', '#FFFFFF', '#CE1126'])
    circle(d, 48, 32, 8, '#8C5A2B')


def portugal(d):
    vertical(d, ['#006600', '#FF0000'], [2, 3])
    circle(d, 38, 32, 11, '#FFD700')
    circle(d, 38, 32, 6, '#FF0000')


def brazil(d):
    d.rectangle([0, 0, W * S, H * S], fill=rgb('#009C3B'))
    d.polygon([(48 * S, 8 * S), (88 * S, 32 * S), (48 * S, 56 * S), (8 * S, 32 * S)], fill=rgb('#FFDF00'))
    circle(d, 48, 32, 13, '#002776')


FLAGS = {
    'en': uk,
    'fr': lambda d: vertical(d, ['#0055A4', '#FFFFFF', '#EF4135']),
    'de': lambda d: horizontal(d, ['#000000', '#DD0000', '#FFCE00']),
    'it': lambda d: vertical(d, ['#009246', '#FFFFFF', '#CE2B37']),
    'nl': lambda d: horizontal(d, ['#AE1C28', '#FFFFFF', '#21468B']),
    'es': lambda d: horizontal(d, ['#AA151B', '#F1BF00', '#AA151B'], [1, 2, 1]),
    'es-419': mexico,
    'pt': portugal,
    'pt-BR': brazil,
    'pl': lambda d: horizontal(d, ['#FFFFFF', '#DC143C']),
    'tr': turkey,
    'cs': czech,
    'ro': lambda d: vertical(d, ['#002B7F', '#FCD116', '#CE1126']),
    'hu': lambda d: horizontal(d, ['#CE2939', '#FFFFFF', '#477050']),
    'sv': lambda d: nordic(d, '#006AA7', '#FECC00'),
    'da': lambda d: nordic(d, '#C8102E', '#FFFFFF'),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for code, paint in FLAGS.items():
        flag = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
        paint(ImageDraw.Draw(flag))
        # Rounded corners and a dark outline, like the game's other UI pieces
        mask = Image.new('L', flag.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle([0, 0, W * S - 1, H * S - 1], radius=8 * S, fill=255)
        out = Image.new('RGBA', flag.size, (0, 0, 0, 0))
        out.paste(flag, (0, 0), mask)
        ImageDraw.Draw(out).rounded_rectangle([0, 0, W * S - 1, H * S - 1], radius=8 * S, outline=OUTLINE, width=3 * S)
        out.resize((W, H), Image.LANCZOS).save(os.path.join(OUT, code + '.png'))
        print('drew', code)


if __name__ == '__main__':
    main()
