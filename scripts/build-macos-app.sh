#!/usr/bin/env bash
# Build a real AppOogst.app bundle from a single-file publish output.
# Usage: build-macos-app.sh <publish-dir> <version> <output-dmg>
set -euo pipefail
SRC="$1"; VER="$2"; DMG="$3"
APP="AppOogst.app"
rm -rf "$APP" "$APP.dmg"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
# PkgInfo is required for LaunchServices to identify the bundle (menu bar name, Dock)
printf 'APPL????' > "$APP/Contents/PkgInfo"
# icns from Assets/icon.png
cd "$(dirname "$0")/.."
mkdir -p /tmp/appoogst.iconset
for s in 16 32 64 128 256 512; do sips -z "$s" "$s" Assets/icon.png --out "/tmp/appoogst.iconset/icon_${s}x${s}.png" >/dev/null; done
sips -z 32 32 Assets/icon.png --out /tmp/appoogst.iconset/icon_16x16@2x.png >/dev/null
sips -z 64 64 Assets/icon.png --out /tmp/appoogst.iconset/icon_32x32@2x.png >/dev/null
sips -z 256 256 Assets/icon.png --out /tmp/appoogst.iconset/icon_128x128@2x.png >/dev/null
sips -z 512 512 Assets/icon.png --out /tmp/appoogst.iconset/icon_256x256@2x.png >/dev/null
iconutil -c icns /tmp/appoogst.iconset -o "$APP/Contents/Resources/AppOogst.icns"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>AppOogst</string>
  <key>CFBundleDisplayName</key><string>AppOogst</string>
  <key>CFBundleIdentifier</key><string>be.jphermans.appoogst</string>
  <key>CFBundleExecutable</key><string>AppOogst</string>
  <key>CFBundleIconFile</key><string>AppOogst</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$VER</string>
  <key>CFBundleVersion</key><string>$VER</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
PLIST
cp "$SRC/AppOogst" "$APP/Contents/MacOS/AppOogst"
chmod +x "$APP/Contents/MacOS/AppOogst"
if [ -f "$SRC/ipatool" ]; then
  cp "$SRC/ipatool" "$APP/Contents/MacOS/ipatool"
  chmod +x "$APP/Contents/MacOS/ipatool"
fi
codesign --force --deep --sign - "$APP"
echo "bundle: $APP"
if [ "$DMG" != "none" ]; then
  hdiutil create -volname AppOogst -srcfolder "$APP" -ov -format UDZO "$DMG"
  echo "dmg: $DMG"
fi
