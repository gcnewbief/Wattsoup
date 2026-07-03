#!/usr/bin/env bash
#
# Installs WattSoup for the current user (no root required):
#   - binary -> ~/.local/bin/wattsoup
#   - icon   -> ~/.local/share/icons/hicolor/256x256/apps/wattsoup.png
#   - launcher -> ~/.local/share/applications/wattsoup.desktop
#
# Run ./packaging/build-and-install.sh style: from the repo root, run:
#   ./packaging/install.sh
#
# It expects a published binary at ./publish/WattSoup. If missing, it publishes one.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BIN_SRC="$REPO_ROOT/publish/WattSoup"
ICON_SRC="$REPO_ROOT/WattSoup/Assets/icon.png"

BIN_DIR="$HOME/.local/bin"
ICON_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"
APP_DIR="$HOME/.local/share/applications"

# Publish a self-contained single-file build if one isn't present.
if [[ ! -f "$BIN_SRC" ]]; then
    echo "No published binary found — publishing self-contained build..."
    dotnet publish "$REPO_ROOT/WattSoup" -c Release -r linux-x64 --self-contained true \
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
        -o "$REPO_ROOT/publish"
fi

mkdir -p "$BIN_DIR" "$ICON_DIR" "$APP_DIR"

install -m 0755 "$BIN_SRC" "$BIN_DIR/wattsoup"
install -m 0644 "$ICON_SRC" "$ICON_DIR/wattsoup.png"

# Generate the .desktop with the resolved absolute Exec path.
sed "s|^Exec=.*|Exec=$BIN_DIR/wattsoup|" "$REPO_ROOT/packaging/WattSoup.desktop" \
    > "$APP_DIR/wattsoup.desktop"
chmod 0644 "$APP_DIR/wattsoup.desktop"

# Refresh caches if the tools are available (harmless if they aren't).
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APP_DIR" || true
command -v gtk-update-icon-cache   >/dev/null 2>&1 && gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" || true

echo "Installed WattSoup."
echo "  binary:   $BIN_DIR/wattsoup"
echo "  icon:     $ICON_DIR/wattsoup.png"
echo "  launcher: $APP_DIR/wattsoup.desktop"
echo "Make sure $BIN_DIR is on your PATH. It should now appear in your app menu."
