"""Checks the Steam achievement icons. Run: python -m unittest tools/test_achievement_icons.py"""
import importlib.util
import os
import tempfile
import unittest

from PIL import Image, ImageStat

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("achievement_icons", os.path.join(HERE, "achievement-icons.py"))
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

NAMES = {"FIRST_DELIVERY", "GOLDEN_WIN", "TUTORIAL_DONE", "STEAL", "GOLDEN_STEAL",
         "HARD_DELIVERY", "WIN", "LAST_TO_FIRST", "FELL_IN_WATER"}


# The icons are drawn into a temporary folder: the tests never touch the committed ones
TEMP = tempfile.TemporaryDirectory()
OUT = os.path.join(TEMP.name, "achievements")


def files():
    for name in sorted(NAMES):
        yield name, os.path.join(OUT, name + ".jpg"), os.path.join(OUT, name + "_locked.jpg")


def spread(img):
    return max(max(p) - min(p) for p in img.getdata())


def luma(p):
    return 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]


class AchievementIconTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        mod.main(OUT, os.path.join(TEMP.name, "preview.png"))

    @classmethod
    def tearDownClass(cls):
        TEMP.cleanup()

    def test_all_nine_names(self):
        self.assertEqual(set(mod.ICONS), NAMES)

    def test_files_written(self):
        for _, *paths in files():
            for path in paths:
                with Image.open(path) as img:
                    self.assertEqual(img.format, "JPEG", path)
                    self.assertEqual(img.mode, "RGB", path)
                    self.assertEqual(img.size, (256, 256), path)

    def test_locked_is_grey(self):
        for name, _, path in files():
            with Image.open(path) as img:
                self.assertLessEqual(spread(img), 8, name)
                self.assertGreater(ImageStat.Stat(img.convert("L")).stddev[0], 20, name)

    def test_unlocked_has_colour(self):
        for name, path, _ in files():
            with Image.open(path) as img:
                self.assertGreater(spread(img), 60, name)

    def test_corners_dark(self):
        for name, *paths in files():
            for path in paths:
                with Image.open(path) as img:
                    for xy in [(0, 0), (255, 0), (0, 255), (255, 255)]:
                        self.assertLess(luma(img.getpixel(xy)), 60, path)


if __name__ == "__main__":
    unittest.main()
