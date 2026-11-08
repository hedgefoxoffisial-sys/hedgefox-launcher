#!/usr/bin/env python3
"""Static TTF instances of the site's brand fonts for the launcher.

Avalonia reads neither WOFF2 nor variable fonts (a variable TTF gives only its default instance plus synthetic
bold), so the launcher ships static instances cut from the site's variable WOFF2 subsets (Latin, Cyrillic, arrows):

  uvx --from fonttools --with brotli python tools/fonts.py [path/to/web/site/public/fonts]

Writes src/Gml.Launcher/Assets/HedgeFox/Fonts/<Family>-<Style>.ttf and copies the OFL texts next to them.
The output is byte-for-byte reproducible (tools/tests/test_fonts.py checks it when the monorepo is next door).
Glyphs the subsets lack (all three: ✓; Rubik: → ▶) are drawn as pixel icons in the launcher.
"""
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FONTS_DIR = ROOT / "src" / "Gml.Launcher" / "Assets" / "HedgeFox" / "Fonts"
SITE_FONTS = Path.home() / "hedgefox-work" / "web" / "site" / "public" / "fonts"

STYLE = {400: "Regular", 500: "Medium", 600: "SemiBold", 700: "Bold", 800: "ExtraBold"}

# family -> source subset, licence and the weights the launcher uses (the site's roles: Tiny5 display and labels,
# Unbounded buttons and headings, Rubik body text). Other axes are pinned to their defaults.
INSTANCES = {
    "Tiny5": {"src": "tiny5.woff2", "ofl": "OFL-tiny5.txt", "weights": [400], "pin": {"wdth": 100, "BLED": 0}},
    "Unbounded": {"src": "unbounded.woff2", "ofl": "OFL-unbounded.txt", "weights": [700, 800], "pin": {}},
    "Rubik": {"src": "rubik.woff2", "ofl": "OFL-rubik.txt", "weights": [400, 500, 600, 700], "pin": {}},
}


def ttf_name(family: str, weight: int) -> str:
    return f"{family}-{STYLE[weight]}.ttf"


def _set_names(font, family: str, weight: int) -> None:
    """One family name for every weight (Avalonia groups embedded fonts by family, then matches OS/2 weight)."""
    style = STYLE[weight]
    full = family if weight == 400 else f"{family} {style}"
    name = font["name"]
    for name_id in (1, 2, 3, 4, 6, 16, 17, 21, 22, 25):
        name.removeNames(nameID=name_id)
    version = font["head"].fontRevision
    values = {1: family, 2: style, 3: f"{version:.3f};HedgeFox;{family}-{style}", 4: full,
              6: f"{family}-{style}", 16: family, 17: style}
    for name_id, value in values.items():
        name.setName(value, name_id, 3, 1, 0x409)
        name.setName(value, name_id, 1, 0, 0)


def _set_style_bits(font, weight: int) -> None:
    os2, head = font["OS/2"], font["head"]
    os2.usWeightClass = weight
    os2.fsSelection &= ~((1 << 0) | (1 << 5) | (1 << 6))  # italic, bold, regular
    os2.fsSelection |= (1 << 5) if weight == 700 else (1 << 6) if weight == 400 else 0
    head.macStyle = (head.macStyle & ~0b11) | (1 if weight == 700 else 0)


def build(src_dir: Path, out_dir: Path) -> list:
    from fontTools.ttLib import TTFont
    from fontTools.varLib import instancer

    out_dir.mkdir(parents=True, exist_ok=True)
    written = []
    for family, spec in INSTANCES.items():
        for weight in spec["weights"]:
            var = TTFont(src_dir / spec["src"])
            modified = var["head"].modified
            static = instancer.instantiateVariableFont(var, {"wght": weight, **spec["pin"]})
            static.flavor = None  # WOFF2 -> plain TrueType
            for tag in ("STAT", "MVAR", "HVAR", "avar"):
                if tag in static:
                    del static[tag]
            _set_names(static, family, weight)
            _set_style_bits(static, weight)
            static["head"].modified = modified
            static.recalcTimestamp = False
            path = out_dir / ttf_name(family, weight)
            static.save(path)
            written.append(path)
        shutil.copyfile(src_dir / spec["ofl"], out_dir / spec["ofl"])
    return written


if __name__ == "__main__":
    src = Path(sys.argv[1]).expanduser() if len(sys.argv) > 1 else SITE_FONTS
    for p in build(src, FONTS_DIR):
        print(f"wrote {p.relative_to(ROOT)} ({p.stat().st_size} B)")
