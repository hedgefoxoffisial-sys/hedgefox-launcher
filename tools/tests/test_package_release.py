"""Release packaging: asset names, SHA256SUMS, exec bits, version from the tag."""
import os
import sys
import tarfile
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from package_release import asset_name, package, tag_to_assembly_version  # noqa: E402

ALL_FIVE = ["HedgeFox-windows-x64.exe", "HedgeFox-linux-x64.tar.gz", "HedgeFox-macos-arm64.dmg",
            "HedgeFox-macos-x64.dmg", "hedgefox.png"]


def artifacts(skip=None):
    root = Path(tempfile.mkdtemp())
    files = {"win-x64/Gml.Launcher.exe": b"MZ", "linux-x64/Gml.Launcher": b"\x7fELF",
             "osx-arm64/HedgeFox-macos-arm64.dmg": b"dmg1", "osx-x64/HedgeFox-macos-x64.dmg": b"dmg2"}
    for rel, data in files.items():
        if skip and rel.startswith(skip):
            continue
        p = root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(data)
    return root


class PackageTest(unittest.TestCase):
    def test_asset_names(self):
        self.assertEqual(asset_name("win-x64"), "HedgeFox-windows-x64.exe")
        self.assertEqual(asset_name("osx-arm64"), "HedgeFox-macos-arm64.dmg")
        self.assertEqual(asset_name("osx-x64"), "HedgeFox-macos-x64.dmg")
        self.assertEqual(asset_name("linux-x64"), "HedgeFox-linux-x64.tar.gz")
        with self.assertRaises(ValueError):
            asset_name("win-arm64")

    def test_package_writes_sums_in_sha256sum_format(self):
        out = Path(tempfile.mkdtemp())
        names = package(artifacts(), out)
        self.assertEqual(sorted(names), sorted(ALL_FIVE + ["SHA256SUMS.txt"]))
        first = (out / "SHA256SUMS.txt").read_text().splitlines()[0]
        self.assertRegex(first, r"^[0-9a-f]{64}  HedgeFox-")

    def test_linux_tarball_keeps_exec_bit(self):
        out = Path(tempfile.mkdtemp())
        package(artifacts(), out)
        m = tarfile.open(out / "HedgeFox-linux-x64.tar.gz").getmember("HedgeFox/HedgeFox")
        self.assertTrue(m.mode & 0o111)

    def test_package_fails_when_rid_missing(self):
        with self.assertRaises(SystemExit) as e:
            package(artifacts(skip="osx-x64"), Path(tempfile.mkdtemp()))
        self.assertIn("osx-x64", str(e.exception))

    def test_tag_to_assembly_version(self):
        self.assertEqual(tag_to_assembly_version("v0.9.0"), "0.9.0.0")
        for bad in ("v0.9", "0.9.0", "v0.9.0-beta"):
            with self.assertRaises(ValueError):
                tag_to_assembly_version(bad)


if __name__ == "__main__":
    unittest.main()
