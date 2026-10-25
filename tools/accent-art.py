"""Draws the accent marks the game's fonts lack, in Jack's Candlestick's own style: each mark is made from the font's
own accent shapes, rendered as pictures (the font file is never changed): the circumflex flipped is a caron, one dot of
the dieresis a dot, two acutes a double acute, the ring from Å, the cedilla mirrored an ogonek, the comma a comma
below, the hyphen leaned for Ł's stroke; the breve is drawn to the same weight. Off-white (#F2F2F2) on a transparent
128x128 square, the mark centred (AccentOverlay places and sizes it). Writes Assets/Sprites/UI/Accents/<mark>.png.
Run: python tools/accent-art.py"""
import os

from PIL import Image, ImageDraw, ImageFont, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
FONT = os.path.join(HERE, '..', 'Assets', 'Fonts', 'jcandlestick.ttf')
OUT = os.path.join(HERE, '..', 'Assets', 'Sprites', 'UI', 'Accents')
SIZE = 128
WHITE = (242, 242, 242, 255)
font = ImageFont.truetype(FONT, 400)


def glyph(text):
    """A character rendered big in white, cropped to its ink"""
    image = Image.new('RGBA', (900, 700), (0, 0, 0, 0))
    ImageDraw.Draw(image).text((150, 50), text, font=font, fill=WHITE)
    return image.crop(image.getbbox())


def accent_of(letter, plain):
    """The accent of an accented capital: its ink above the plain letter's top"""
    accented, base = glyph(letter), glyph(plain)
    return accented.crop((0, 0, accented.width, max(1, accented.height - base.height - 6))).crop(
        accented.crop((0, 0, accented.width, max(1, accented.height - base.height - 6))).getbbox())


def below_of(letter, plain):
    """The mark under an accented capital (the cedilla): its ink below the plain letter"""
    accented, base = glyph(letter), glyph(plain)
    part = accented.crop((0, base.height - 4, accented.width, accented.height))
    return part.crop(part.getbbox())


def fit(mark, scale):
    """The mark at the shared scale, in the middle of the square"""
    mark = mark.resize((max(1, int(mark.width * scale)), max(1, int(mark.height * scale))), Image.LANCZOS)
    square = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    square.alpha_composite(mark, ((SIZE - mark.width) // 2, (SIZE - mark.height) // 2))
    return square


def side_by_side(a, b, gap=0.25):
    space = int(a.width * gap)
    both = Image.new('RGBA', (a.width + b.width + space, max(a.height, b.height)), (0, 0, 0, 0))
    both.alpha_composite(a, (0, 0))
    both.alpha_composite(b, (a.width + space, 0))
    return both


def left_dot(dieresis):
    """One dot of the dieresis: the ink left of its middle"""
    half = dieresis.crop((0, 0, dieresis.width // 2, dieresis.height))
    return half.crop(half.getbbox())


def breve(like):
    """A small cup, as wide and as heavy as the font's circumflex (it has no breve to borrow)"""
    w, h = like.width, like.height
    image = Image.new('RGBA', (w, int(h * 1.3)), (0, 0, 0, 0))
    ImageDraw.Draw(image).arc([0, -int(h * 1.3), w, int(h * 1.3)], 15, 165, fill=WHITE, width=int(h * 0.55))
    # The font's lean
    image = image.transform(image.size, Image.AFFINE, (1, 0.25, -h * 0.3, 0, 1, 0), resample=Image.BICUBIC)
    return image.crop(image.getbbox())


def main():
    os.makedirs(OUT, exist_ok=True)
    circumflex = accent_of('Ê', 'E')
    acute = accent_of('É', 'E')
    dieresis = accent_of('Ö', 'O')
    ring = accent_of('Å', 'A')
    cedilla = below_of('Ç', 'C')
    comma = glyph(',')
    hyphen = glyph('-')

    marks = {
        'caron': ImageOps.flip(circumflex),
        'breve': breve(circumflex),
        'dot': left_dot(dieresis),
        'doubleacute': side_by_side(acute, acute),
        'ring': ring,
        'ogonek': ImageOps.mirror(cedilla),
        'commabelow': comma,
        'acute': acute,
        'lstroke': hyphen.rotate(25, expand=True, resample=Image.BICUBIC),
    }
    # One scale for every mark, so they keep the font's proportions: the widest fills 90% of the square
    marks = {name: mark.crop(mark.getbbox()) for name, mark in marks.items()}
    scale = min(SIZE * 0.9 / max(m.width, m.height) for m in marks.values())
    for name, mark in marks.items():
        fit(mark, scale).save(os.path.join(OUT, name + '.png'))
        print('drew', name)
    # How tall a capital is in the squares' units: AccentOverlay sizes a square against the letter it marks
    print('CAP_UNITS', round(glyph('H').height * scale, 2))


if __name__ == '__main__':
    main()
