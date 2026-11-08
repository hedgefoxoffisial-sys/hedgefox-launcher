"""Brand fonts in the launcher: static TTF instances of the site's subsets, with their OFL texts.

Avalonia reads neither WOFF2 nor variable fonts, so tools/fonts.py cuts static instances. The stdlib checks run
everywhere (CI too); the fontTools checks run when fontTools is importable, e.g.
  uvx --from fonttools --with brotli python -m unittest discover -s tools/tests
"""
import struct
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
from fonts import FONTS_DIR, INSTANCES, SITE_FONTS, build, ttf_name  # noqa: E402

try:
    from fontTools.ttLib import TTFont
except ImportError:  # CI: plain python3
    TTFont = None

try:
    import brotli  # noqa: F401  (WOFF2 decoding for the reproducibility check)
except ImportError:
    brotli = None

EXPECTED = {
    "Tiny5-Regular.ttf", "Unbounded-Bold.ttf", "Unbounded-ExtraBold.ttf",
    "Rubik-Regular.ttf", "Rubik-Medium.ttf", "Rubik-SemiBold.ttf", "Rubik-Bold.ttf",
}
# Russian UI text: the alphabet plus the punctuation the site's voice uses
RU_TEXT = "".join(chr(c) for c in range(0x410, 0x450)) + "ёЁ№«»—–…"


def sfnt_tables(path: Path) -> dict:
    data = path.read_bytes()
    version, num = struct.unpack(">IH", data[:6])
    tables = {}
    for i in range(num):
        tag, _sum, off, length = struct.unpack(">4sIII", data[12 + 16 * i:28 + 16 * i])
        tables[tag.decode("latin-1")] = data[off:off + length]
    return {"version": version, "tables": tables}


class FontFilesTest(unittest.TestCase):
    def test_expected_static_ttfs_exist(self):
        self.assertEqual({ttf_name(f, w) for f, spec in INSTANCES.items() for w in spec["weights"]}, EXPECTED)
        for name in EXPECTED:
            self.assertTrue((FONTS_DIR / name).is_file(), name)

    def test_ttfs_are_static_truetype_with_right_weight(self):
        for family, spec in INSTANCES.items():
            for weight in spec["weights"]:
                info = sfnt_tables(FONTS_DIR / ttf_name(family, weight))
                self.assertEqual(info["version"], 0x00010000, f"{family} {weight}: not a TrueType sfnt")
                self.assertNotIn("fvar", info["tables"], f"{family} {weight}: still variable")
                self.assertNotIn("gvar", info["tables"])
                us_weight = struct.unpack(">H", info["tables"]["OS/2"][4:6])[0]
                self.assertEqual(us_weight, weight, f"{family}: OS/2 usWeightClass")

    def test_ofl_texts_ship_with_fonts(self):
        for family, spec in INSTANCES.items():
            ofl = FONTS_DIR / spec["ofl"]
            self.assertTrue(ofl.is_file(), ofl.name)
            self.assertIn("SIL OPEN FONT LICENSE", ofl.read_text(encoding="utf-8").upper())

    def test_no_stray_files(self):
        allowed = EXPECTED | {spec["ofl"] for spec in INSTANCES.values()}
        self.assertEqual({p.name for p in FONTS_DIR.iterdir()} - allowed, set())


@unittest.skipUnless(TTFont, "fontTools is not installed")
class FontLoadTest(unittest.TestCase):
    def test_family_name_and_weight(self):
        for family, spec in INSTANCES.items():
            for weight in spec["weights"]:
                f = TTFont(FONTS_DIR / ttf_name(family, weight))
                self.assertEqual(f["name"].getDebugName(1), family)
                self.assertEqual(f["name"].getDebugName(16), family)
                self.assertEqual(f["OS/2"].usWeightClass, weight)

    def test_russian_coverage(self):
        for family, spec in INSTANCES.items():
            for weight in spec["weights"]:
                cmap = TTFont(FONTS_DIR / ttf_name(family, weight)).getBestCmap()
                missing = [c for c in RU_TEXT if ord(c) not in cmap]
                self.assertEqual(missing, [], f"{family} {weight}")

    @unittest.skipUnless(brotli and SITE_FONTS.is_dir(), "needs brotli and the monorepo's site fonts")
    def test_reproducible_from_site_subsets(self):
        with tempfile.TemporaryDirectory() as tmp:
            build(SITE_FONTS, Path(tmp))
            for name in EXPECTED:
                self.assertEqual((Path(tmp) / name).read_bytes(), (FONTS_DIR / name).read_bytes(), name)


if __name__ == "__main__":
    unittest.main()
