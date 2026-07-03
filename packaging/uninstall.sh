#!/usr/bin/env bash
# Removes the per-user WattSoup install created by install.sh.
set -euo pipefail

rm -f "$HOME/.local/bin/wattsoup"
rm -f "$HOME/.local/share/icons/hicolor/256x256/apps/wattsoup.png"
rm -f "$HOME/.local/share/applications/wattsoup.desktop"

command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$HOME/.local/share/applications" || true
command -v gtk-update-icon-cache   >/dev/null 2>&1 && gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" || true

echo "Uninstalled WattSoup."
