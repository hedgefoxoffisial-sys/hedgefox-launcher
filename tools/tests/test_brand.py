"""Brand checks for the HedgeFox launcher fork: colours from the site tokens, config, titles, no upstream brand."""
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
from brand import (contrast, find_brand_leaks, load_derived, load_tokens, parse_site_root,  # noqa: E402
                   read_axaml_colors, render_colors_axaml)

TOKENS = ROOT / "branding" / "tokens.json"
DERIVED = ROOT / "branding" / "site-derived.json"
SITE_TOKENS = Path.home() / "hedgefox-work" / "web" / "site" / "brand" / "tokens.json"
SITE_CSS = Path.home() / "hedgefox-work" / "web" / "site" / "src" / "styles" / "global.css"
APP = ROOT / "src" / "Gml.Launcher"
COLORS = APP / "Assets" / "Styles" / "Colors.axaml"
RKD = APP / "Assets" / "Resources" / "ResourceKeysDictionary.cs"
MAIN_WINDOW = APP / "Views" / "MainWindow.axaml"
SPLASH = APP / "Views" / "SplashScreen" / "SplashScreen.axaml"


def read(p):
    return Path(p).read_text(encoding="utf-8-sig")


class BrandTest(unittest.TestCase):
    def test_colors_axaml_is_generated_from_tokens(self):
        self.assertEqual(render_colors_axaml(load_tokens(TOKENS), load_derived(DERIVED)), read(COLORS))

    def test_primary_accent_background_come_from_tokens(self):
        t, c = load_tokens(TOKENS), read_axaml_colors(read(COLORS))
        self.assertEqual(c["PrimaryColorHover"], t["purple"])
        self.assertEqual(c["AccentColor"], t["orange"])
        self.assertEqual(c["BackgroundColor"], t["bg"])

    def test_text_contrast(self):
        c = read_axaml_colors(read(COLORS))
        self.assertGreaterEqual(contrast(c["HeadlineColor"], c["PrimaryColor"]), 4.5)
        self.assertGreaterEqual(contrast(c["ContentColor"], c["FrameBackgroundColor"]), 4.5)
        # the site's pairs: text on night and on raised fills, ink on orange and brass, light text on purple-deep
        self.assertGreaterEqual(contrast(c["HfTextBrush"], c["HfBgBrush"]), 7)
        self.assertGreaterEqual(contrast(c["HfMutedBrush"], c["HfRaisedBrush"]), 4.5)
        self.assertGreaterEqual(contrast(c["HfTextBrush"], c["HfRaisedHoverBrush"]), 4.5)
        self.assertGreaterEqual(contrast(c["HfInkBrush"], c["HfOrangeBrush"]), 4.5)
        self.assertGreaterEqual(contrast(c["HfInkBrush"], c["HfBrassBrush"]), 4.5)
        self.assertGreaterEqual(contrast(c["HfTextBrush"], c["HfPurpleDeepBrush"]), 4.5)
        self.assertGreaterEqual(contrast(c["HfErrorBrush"], c["HfNightBrush"]), 4.5)
        self.assertGreaterEqual(contrast(c["HfSuccessBrush"], c["HfNightBrush"]), 4.5)

    def test_derived_colours_come_from_the_site(self):
        d, c = load_derived(DERIVED), read_axaml_colors(read(COLORS))
        self.assertEqual(c["HfVoidBrush"], d["void"])
        self.assertEqual(c["HfNightBrush"], d["night"])
        self.assertEqual(c["HfLineBrush"], d["line"])
        self.assertEqual(c["HfPurpleDeepBrush"], d["purple-deep"])
        # stock GML keys now carry the site's values (they were hand-picked before site v2)
        self.assertEqual(c["PrimaryColor"], d["purple-deep"])
        self.assertEqual(c["FrameBackgroundBorderColor"], d["line"])
        self.assertEqual(c["SecondaryColor"], d["void"])
        self.assertEqual(c["FormBackgroundColor"], d["well"])
        self.assertEqual(c["FormBorderColor"], d["line"])
        self.assertEqual(c["FormBorderHoverColor"], load_tokens(TOKENS)["purple"])

    def test_right_column_has_its_own_scrim(self):
        # the status card and the news sit over the airship; the right scrim darkens from the right edge (visual-3)
        colors = read(COLORS)
        self.assertRegex(colors, r'<LinearGradientBrush x:Key="HfScrimRightBrush" StartPoint="100%, 0%" EndPoint="0%, 0%">')

    def test_parse_site_root(self):
        css = """body { --hf-x: #111111; }
:root {
  color-scheme: dark;
  --hf-void: #080511; /* shadow */
  --hf-night: #19102f;
  --hf-shadow: 4px 4px 0 var(--hf-void);
  --hf-bevel: inset 0 2px 0 rgb(255 255 255 / 0.28), inset 0 -3px 0 rgb(0 0 0 / 0.28);
}
:root { --hf-late: #222222; }"""
        self.assertEqual(parse_site_root(css), {"void": "#080511", "night": "#19102F",
                                                "bevel-light": "#47FFFFFF", "bevel-dark": "#47000000"})

    @unittest.skipUnless(SITE_CSS.exists(), "no monorepo here (CI)")
    def test_derived_copy_matches_site(self):
        self.assertEqual(load_derived(DERIVED), parse_site_root(SITE_CSS.read_text(encoding="utf-8")))

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

    def test_no_upstream_brand_in_the_app(self):
        self.assertEqual(find_brand_leaks(APP), [])


class LeakScannerTest(unittest.TestCase):
    """find_brand_leaks must see every place upstream left its brand: links, code, all resources, packaging."""

    def scan(self, files: dict) -> list:
        with tempfile.TemporaryDirectory() as tmp:
            for rel, text in files.items():
                p = Path(tmp) / rel
                p.parent.mkdir(parents=True, exist_ok=True)
                p.write_text(text, encoding="utf-8")
            return find_brand_leaks(Path(tmp))

    def test_command_parameter_links(self):
        self.assertEqual(len(self.scan({"Views/A.axaml": '<Button CommandParameter="https://vk.com/recloud.studio"/>'})), 1)
        self.assertEqual(len(self.scan({"Views/A.axaml": '<Button CommandParameter="https://recloud.tech"/>'})), 1)

    def test_csharp_string_literals(self):
        self.assertEqual(len(self.scan({"A.cs": 'var u = "https://www.clipartkey.com/steve.png";'})), 1)
        self.assertEqual(len(self.scan({"A.cs": 'var n = "GmlLauncher";'})), 1)
        self.assertEqual(len(self.scan({"A.cs": 'var c = @"Recloud LLC";'})), 1)

    def test_csharp_assembly_name_and_comments_are_fine(self):
        self.assertEqual(self.scan({"A.cs": '// fork of recloud\nvar a = "avares://Gml.Launcher/Assets/x.png";'}), [])

    def test_every_resx(self):
        resx = '<root><data name="A"><value>Join vk.com/recloud.studio</value></data></root>'
        self.assertEqual(len(self.scan({"Assets/Resources/Resources.en.resx": resx})), 1)
        self.assertEqual(len(self.scan({"Assets/Resources/Resources.zh.resx": resx})), 1)

    def test_parcel(self):
        found = self.scan({"Gml.Launcher.parcel": '{"Maintainer": "Recloud LLC", "ApplicationName": "GmlLauncher"}'})
        self.assertEqual(len(found), 2)

    def test_build_output_is_skipped(self):
        self.assertEqual(self.scan({"obj/Release/A.cs": 'var u = "https://recloud.tech";',
                                    "bin/Release/A.axaml": '<B CommandParameter="https://recloud.tech"/>'}), [])


if __name__ == "__main__":
    unittest.main()
