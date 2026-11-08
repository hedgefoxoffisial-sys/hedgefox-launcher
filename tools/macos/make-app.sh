#!/usr/bin/env bash
# Build dist/HedgeFox.app from a `dotnet publish` folder and pack it into HedgeFox-macos-<arch>.dmg.
#   tools/macos/make-app.sh <publish_dir> <osx-arm64|osx-x64> <version>
# The app is signed ad hoc only (no Apple Developer account): Gatekeeper asks to confirm the first start.
set -euo pipefail
pub="$1"; rid="$2"; ver="$3"
root="$(cd "$(dirname "$0")/../.." && pwd)"
arch="${rid#osx-}"
app="$root/dist/HedgeFox.app"
rm -rf "$app" "$root/dist/HedgeFox.iconset"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$root/dist/HedgeFox.iconset"
cp -R "$pub"/. "$app/Contents/MacOS/"
chmod +x "$app/Contents/MacOS/Gml.Launcher"
set_icon() { sips -z "$1" "$1" "$root/branding/hedgefox.png" --out "$root/dist/HedgeFox.iconset/$2" >/dev/null; }
for s in 16 32 128 256 512; do set_icon "$s" "icon_${s}x${s}.png"; done
for s in 16 32 128 256; do set_icon $((s * 2)) "icon_${s}x${s}@2x.png"; done
iconutil -c icns "$root/dist/HedgeFox.iconset" -o "$app/Contents/Resources/HedgeFox.icns"
sed "s/{{VERSION}}/$ver/g" "$root/tools/macos/Info.plist.tmpl" > "$app/Contents/Info.plist"
codesign --force --deep -s - "$app"
dmg="$root/HedgeFox-macos-$arch.dmg"
rm -f "$dmg"
hdiutil create -volname HedgeFox -srcfolder "$app" -ov -format UDZO "$dmg" >/dev/null
echo "$dmg"
