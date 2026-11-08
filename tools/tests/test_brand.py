"""Brand checks for the HedgeFox launcher fork: colours from the site tokens, config, titles, no upstream brand."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
from brand import contrast, find_brand_leaks, load_tokens, read_axaml_colors, render_colors_axaml  # noqa: E402

TOKENS = ROOT / "branding" / "tokens.json"
SITE_TOKENS = Path.home() / "hedgefox-work" / "web" / "site" / "brand" / "tokens.json"
APP = ROOT / "src" / "Gml.Launcher"
COLORS = APP / "Assets" / "Styles" / "Colors.axaml"
RKD = APP / "Assets" / "Resources" / "ResourceKeysDictionary.cs"
MAIN_WINDOW = APP / "Views" / "MainWindow.axaml"
SPLASH = APP / "Views" / "SplashScreen" / "SplashScreen.axaml"


def read(p):
    return Path(p).read_text(encoding="utf-8-sig")


class BrandTest(unittest.TestCase):
    def test_colors_axaml_is_generated_from_tokens(self):
        self.assertEqual(render_colors_axaml(load_tokens(TOKENS)), read(COLORS))

    def test_primary_accent_background_come_from_tokens(self):
        t, c = load_tokens(TOKENS), read_axaml_colors(read(COLORS))
        self.assertEqual(c["PrimaryColorHover"], t["purple"])
        self.assertEqual(c["AccentColor"], t["orange"])
        self.assertEqual(c["BackgroundColor"], t["bg"])

    def test_text_contrast(self):
        c = read_axaml_colors(read(COLORS))
        self.assertGreaterEqual(contrast(c["HeadlineColor"], c["PrimaryColor"]), 4.5)
        self.assertGreaterEqual(contrast(c["ContentColor"], c["FrameBackgroundColor"]), 4.5)

    def test_contrast_known_value(self):
        self.assertAlmostEqual(contrast("#FFFFFF", "#000000"), 21.0, places=1)

    @unittest.skipUnless(SITE_TOKENS.exists(), "no monorepo here (CI)")
    def test_tokens_copy_matches_site(self):
        self.assertEqual(load_tokens(TOKENS), load_tokens(SITE_TOKENS))

    def test_config(self):
        src = read(RKD)
        self.assertIn('Host = "https://api.hedgefox.ru"', src)
        self.assertIn('SecondaryHost = "https://api.hedgefox.ru"', src)
        self.assertIn('FolderName = ".hedgefox"', src)
        self.assertNotIn("recloud", src.lower())
        self.assertNotIn("localhost", src)

    def test_window_titles(self):
        for f in (MAIN_WINDOW, SPLASH):
            self.assertIn('Title="HedgeFox"', read(f))

    def test_no_upstream_brand_in_user_text(self):
        self.assertEqual(find_brand_leaks(APP), [])


if __name__ == "__main__":
    unittest.main()
