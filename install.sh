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

REPO_URL="https://raw.githubusercontent.com/$REPO/main/updatePlugins.xml"

# Registers the custom plugin repository in <config>/options/updates.xml so Rider
# offers updates automatically. Best effort — skipped if python3 is unavailable.
add_plugin_repo() {
    local options_dir="$1/options"
    mkdir -p "$options_dir"
    python3 - "$options_dir/updates.xml" "$REPO_URL" <<'PY' 2>/dev/null || echo "  (could not register the update repository automatically — add $REPO_URL under Settings | Plugins | Manage Plugin Repositories)"
import sys, os
import xml.etree.ElementTree as ET
path, url = sys.argv[1], sys.argv[2]
if os.path.isfile(path):
    tree = ET.parse(path)
    root = tree.getroot()
else:
    root = ET.Element('application')
    tree = ET.ElementTree(root)
comp = next((c for c in root.findall('component') if c.get('name') == 'UpdatesConfigurable'), None)
if comp is None:
    comp = ET.SubElement(root, 'component', {'name': 'UpdatesConfigurable'})
hosts = next((o for o in comp.findall('option') if o.get('name') == 'pluginHosts'), None)
if hosts is None:
    hosts = ET.SubElement(comp, 'option', {'name': 'pluginHosts'})
if not any(o.get('value') == url for o in hosts.findall('option')):
    ET.SubElement(hosts, 'option', {'value': url})
    tree.write(path, encoding='unicode', xml_declaration=False)
PY
}

FOUND=0
for rider_dir in "$JB_ROOT"/Rider*; do
    [ -d "$rider_dir" ] || continue
    FOUND=1
    plugins="$rider_dir/plugins"
    mkdir -p "$plugins"
    rm -rf "${plugins:?}/$PLUGIN_DIR_NAME"
    unzip -qo "$TMP/plugin.zip" -d "$plugins"
    echo "Installed into $plugins"
    add_plugin_repo "$rider_dir"
done

if [ "$FOUND" = 0 ]; then
    echo "No Rider installation found under $JB_ROOT." >&2
    echo "Install manually instead: Rider → Settings | Plugins | ⚙ | Install Plugin from Disk…" >&2
    echo "The downloaded zip is available from https://github.com/$REPO/releases" >&2
    exit 1
fi

echo
echo "Done. Restart Rider, then open the 'Copilot Companion' tool window on the right edge."
echo "Automatic updates: the update repository was registered; Rider will offer new versions"
echo "on the Plugins page. (If Rider was running during install, restart it to pick this up.)"
