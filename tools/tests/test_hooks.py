"""The fork's one-line hooks into upstream GML files stay in place (docs: HEDGEFOX.md «Что меняем»)."""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
APP = ROOT / "src" / "Gml.Launcher"


def read(p):
    return Path(p).read_text(encoding="utf-8-sig")


class HookTest(unittest.TestCase):
    def test_theme_is_included_last_and_dark(self):
        app = read(APP / "App.axaml")
        self.assertIn('RequestedThemeVariant="Dark"', app)
        includes = re.findall(r'<StyleInclude Source="([^"]+)"', app)
        self.assertEqual(includes[-1], "Assets/Styles/HedgeFoxTheme.axaml")

    def test_services_are_registered_after_the_language_check(self):
        src = read(APP / "Core" / "Extensions" / "ServiceLocator.cs")
        body = src[src.index("RegisterServices("):]
        self.assertIn("HedgeFoxServices.Register(storageService);", body)
        self.assertLess(body.index("CheckAndChangeLanguage("), body.index("HedgeFoxServices.Register("))

    def test_hedgefox_pages_are_resolved_first(self):
        src = read(APP / "Core" / "Helpers" / "AppViewLocator.cs")
        body = src[src.index("viewModel switch"):]
        first_arm = body.split("{", 1)[1].strip().splitlines()[0]
        self.assertIn("HfViews.Resolve(viewModel)", first_arm)

    def test_theme_has_no_corner_radius(self):
        theme = read(APP / "Assets" / "Styles" / "HedgeFoxTheme.axaml")
        for value in re.findall(r'(?:CornerRadius="([^"]*)")|(?:Property="CornerRadius" Value="([^"]*)")', theme):
            self.assertEqual("".join(value).strip(), "0")
        for value in re.findall(r'<CornerRadius x:Key="\w+">([^<]*)</CornerRadius>', theme):
            self.assertEqual(value.strip(), "0")

    def test_macos_asks_for_the_microphone(self):
        plist = read(ROOT / "tools" / "macos" / "Info.plist.tmpl")
        self.assertRegex(plist, r"<key>NSMicrophoneUsageDescription</key>\s*<string>Голосовой чат в игре</string>")


if __name__ == "__main__":
    unittest.main()
