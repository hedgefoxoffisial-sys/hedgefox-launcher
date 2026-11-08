#!/usr/bin/env python3
"""Collect CI build outputs into release assets with SHA256SUMS.txt. Stdlib only.

  python3 tools/package_release.py package <artifacts_dir> <out_dir>
  python3 tools/package_release.py version <tag>       # v0.9.0 -> 0.9.0.0 (AssemblyVersion/FileVersion)

Input layout (contract with .github/workflows/hedgefox-release.yml):
  artifacts/win-x64/Gml.Launcher.exe, artifacts/linux-x64/Gml.Launcher,
  artifacts/osx-arm64/HedgeFox-macos-arm64.dmg, artifacts/osx-x64/HedgeFox-macos-x64.dmg
"""
import hashlib
import io
import re
import shutil
import sys
import tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NAMES = {"win-x64": "HedgeFox-windows-x64.exe", "linux-x64": "HedgeFox-linux-x64.tar.gz",
         "osx-arm64": "HedgeFox-macos-arm64.dmg", "osx-x64": "HedgeFox-macos-x64.dmg"}
SOURCES = {"win-x64": "Gml.Launcher.exe", "linux-x64": "Gml.Launcher",
           "osx-arm64": "HedgeFox-macos-arm64.dmg", "osx-x64": "HedgeFox-macos-x64.dmg"}


def asset_name(rid: str) -> str:
    if rid not in NAMES:
        raise ValueError(f"unknown runtime id {rid}")
    return NAMES[rid]


def tag_to_assembly_version(tag: str) -> str:
    m = re.fullmatch(r"v(\d+)\.(\d+)\.(\d+)", tag)
    if not m:
        raise ValueError(f"tag must look like v1.2.3, got {tag!r}")
    return ".".join(m.groups()) + ".0"


def _linux_tarball(binary: Path, dest: Path) -> None:
    with tarfile.open(dest, "w:gz") as tar:
        info = tarfile.TarInfo("HedgeFox/HedgeFox")
        data = binary.read_bytes()
        info.size, info.mode = len(data), 0o755
        tar.addfile(info, io.BytesIO(data))


def package(artifacts_dir, out_dir) -> list:
    artifacts_dir, out_dir = Path(artifacts_dir), Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    for rid, src in SOURCES.items():
        if not (artifacts_dir / rid / src).is_file():
            raise SystemExit(f"missing build for {rid}: {artifacts_dir / rid / src}")
    for rid, src in SOURCES.items():
        path, dest = artifacts_dir / rid / src, out_dir / asset_name(rid)
        if rid == "linux-x64":
            _linux_tarball(path, dest)
        else:
            shutil.copyfile(path, dest)
    icon = ROOT / "branding" / "hedgefox.png"
    (out_dir / "hedgefox.png").write_bytes(icon.read_bytes() if icon.exists() else b"")
    names = sorted(p.name for p in out_dir.iterdir() if p.name != "SHA256SUMS.txt")
    sums = [f"{hashlib.sha256((out_dir / n).read_bytes()).hexdigest()}  {n}" for n in names]
    (out_dir / "SHA256SUMS.txt").write_text("\n".join(sums) + "\n")
    return names + ["SHA256SUMS.txt"]


if __name__ == "__main__":
    if len(sys.argv) == 4 and sys.argv[1] == "package":
        print("\n".join(package(sys.argv[2], sys.argv[3])))
    elif len(sys.argv) == 3 and sys.argv[1] == "version":
        print(tag_to_assembly_version(sys.argv[2]))
    else:
        raise SystemExit(__doc__)
