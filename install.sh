#!/usr/bin/env bash
# Copilot Companion installer for macOS/Linux (JetBrains Rider plugin).
#   curl -fsSL https://raw.githubusercontent.com/maisa92/copilot-companion/main/install.sh | bash
set -euo pipefail

REPO="maisa92/copilot-companion"
PLUGIN_DIR_NAME="copilot-companion"

echo "Copilot Companion installer"
echo

# Stable redirect to the newest release; no GitHub API call (avoids rate limits).
ZIP_URL="https://github.com/$REPO/releases/latest/download/copilot-companion.zip"
echo "Downloading $ZIP_URL"

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
curl -fsSL -o "$TMP/plugin.zip" "$ZIP_URL"

# --- find Rider plugin directories -------------------------------------------
case "$(uname -s)" in
    Darwin) JB_ROOT="$HOME/Library/Application Support/JetBrains" ;;
    *)      JB_ROOT="${XDG_DATA_HOME:-$HOME/.local/share}/JetBrains" ;;
esac

FOUND=0
for rider_dir in "$JB_ROOT"/Rider*; do
    [ -d "$rider_dir" ] || continue
    FOUND=1
    plugins="$rider_dir/plugins"
    mkdir -p "$plugins"
    rm -rf "${plugins:?}/$PLUGIN_DIR_NAME"
    unzip -qo "$TMP/plugin.zip" -d "$plugins"
    echo "Installed into $plugins"
done

if [ "$FOUND" = 0 ]; then
    echo "No Rider installation found under $JB_ROOT." >&2
    echo "Install manually instead: Rider → Settings | Plugins | ⚙ | Install Plugin from Disk…" >&2
    echo "The downloaded zip is available from https://github.com/$REPO/releases" >&2
    exit 1
fi

echo
echo "Done. Restart Rider, then open the 'Copilot Companion' tool window on the right edge."
