#!/usr/bin/env bash
# Build AppOogst AppImage from a single-file publish output.
# Usage: build-appimage.sh <publish-dir> <version> <output.AppImage>
set -euo pipefail
SRC="$1"; OUT="$3"
APPDIR="$(mktemp -d)/AppOogst.AppDir"
mkdir -p "$APPDIR/usr/bin"
cp "$SRC/AppOogst" "$APPDIR/usr/bin/AppOogst"
cp "$SRC/ipatool" "$APPDIR/usr/bin/ipatool"
chmod +x "$APPDIR/usr/bin/AppOogst" "$APPDIR/usr/bin/ipatool"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$APPDIR/usr/share/icons/hicolor/512x512/apps"
cp "$REPO_ROOT/Assets/icon.png" "$APPDIR/usr/share/icons/hicolor/512x512/apps/appoogst.png"
cp "$REPO_ROOT/Assets/icon.png" "$APPDIR/appoogst.png"
cat > "$APPDIR/AppOogst.desktop" <<DESK
[Desktop Entry]
Type=Application
Name=AppOogst
Exec=AppOogst
Icon=appoogst
Comment=Harvest your purchased App Store apps as .ipa
Categories=Utility;
DESK
cat > "$APPDIR/AppRun" <<'RUN'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/AppOogst" "$@"
RUN
chmod +x "$APPDIR/AppRun"
TOOLS=/tmp/appimagekit
mkdir -p "$TOOLS"
if [ ! -x "$TOOLS/appimagetool.AppImage" ]; then
  curl -sL "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage" -o "$TOOLS/appimagetool.AppImage"
  chmod +x "$TOOLS/appimagetool.AppImage"
fi
"$TOOLS/appimagetool.AppImage" --appimage-extract-and-run "$APPDIR" "$OUT"
echo "appimage: $OUT"
