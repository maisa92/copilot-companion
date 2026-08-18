# Copilot Companion

Launch **VS Code as a side-by-side AI companion window** (with GitHub Copilot Chat) for the
solution/project you currently have open in your main IDE.

Two extensions, one behavior:

| Directory | IDE | Tech |
|-----------|-----|------|
| [`/vs`](vs/) | Visual Studio 2022 & 2026 (17.0–18.x) | C#, .NET Framework 4.8, VSIX |
| [`/rider`](rider/) | JetBrains Rider 2025.x | Kotlin, Gradle, IntelliJ Platform SDK |

## Install (download, no build needed)

### One-line install

**macOS / Linux (Rider):**

```bash
curl -fsSL https://raw.githubusercontent.com/maisa92/copilot-companion/main/install.sh | bash
```

**Windows (Rider + Visual Studio, in PowerShell):**

```powershell
irm https://raw.githubusercontent.com/maisa92/copilot-companion/main/install.ps1 | iex
```

Restart the IDE afterwards.

### Rider: plugin repository with auto-updates (recommended)

In Rider: *Settings | Plugins | ⚙ | Manage Plugin Repositories…* → add:

```
https://raw.githubusercontent.com/maisa92/copilot-companion/main/updatePlugins.xml
```

Then search for **Copilot Companion** in the *Marketplace* tab of the Plugins page and
install it like any other plugin — future releases show up as normal plugin updates.

### Manual install

Grab the latest artifacts from **[Releases](../../releases)**:

| IDE | File | How to install |
|---|---|---|
| JetBrains Rider (2025.1+, Windows/macOS/Linux) | `copilot-companion-<version>.zip` | Rider → *Settings \| Plugins \| ⚙ \| Install Plugin from Disk…* → pick the zip → restart. Do **not** unzip it. |
| Visual Studio 2022 / 2026 (Windows) | `CopilotCompanion.vsix` | Close VS, double-click the `.vsix`, follow the installer. |

Then install [VS Code](https://code.visualstudio.com/download) if you don't have it, open the tool window
(**Copilot Companion** on the right edge in Rider; *Tools → Open Companion Chat (Docked)* or
`Ctrl+Shift+Alt+D` in VS) and, inside the embedded VS Code, install **GitHub Copilot Chat**,
sign in, and trust the workspace — one time only.

## What it does

**Open Copilot Companion** (`Ctrl+Shift+Alt+C`):

1. Finds the root folder of the open solution/project (aborts with a notification if none is open).
2. Writes/merges `.vscode/settings.json` in that folder with a minimal-UI profile
   (activity bar hidden, status bar hidden, minimap off) — existing settings are preserved.
3. Launches VS Code in a new window on that folder (`code -n "<folder>"`).
4. Waits (up to 15 s, without blocking the IDE) for the VS Code window to appear.
5. Opens Copilot Chat via `code chat --reuse-window ""` (skipped silently on older VS Code).
6. Tiles the windows on the monitor your IDE is on: **VS Code left 30 %** of the working
   area, **your IDE right 70 %** (ratio configurable).

**Restore Layout** (`Ctrl+Shift+Alt+R`): maximizes your IDE again and minimizes the
companion VS Code window.

**File sync** (toggleable, on by default): whenever you switch files in the host IDE while
companion mode is active, VS Code follows along with
`code --reuse-window --goto "<file>:<line>"` (debounced 500 ms, includes your caret line).

Both extensions expose settings for the split ratio, auto-opening chat, the file-sync
default, and a custom path to the `code` executable.

## Docked chat mode (both IDEs)

Besides the side-by-side window mode, both extensions provide a **docked tool window**
that embeds the real VS Code UI — served locally by `code serve-web` on
`127.0.0.1:8384` — inside the host IDE (JCEF in Rider, WebView2 in Visual Studio).
After every load a script enforces a chat-only layout (Copilot Chat side bar
maximized, dark theme), so it looks like a native AI chat panel. First use inside the
embedded VS Code: install GitHub Copilot Chat, sign in, and trust the workspace.

## Prerequisite for both IDEs: the VS Code CLI

Both extensions drive VS Code through its `code` command-line interface.

1. Install [Visual Studio Code](https://code.visualstudio.com/download).
2. Make sure `code` is on your `PATH`. The default per-user Windows installer does this
   automatically; otherwise, in VS Code run
   **F1 → "Shell Command: Install 'code' command in PATH"**, or set the explicit path to
   `Code.exe` / `bin\code.cmd` in the extension's settings.
   (If `code` isn't on `PATH`, the extensions also probe the default install location
   `%LocalAppData%\Programs\Microsoft VS Code`.)
3. For the chat step, install the **GitHub Copilot Chat** extension in VS Code and sign in.

Window tiling uses the Win32 API and therefore takes effect on **Windows** only; on other
platforms everything else still works, and window arrangement is skipped gracefully.

## Install

### Visual Studio 2022 (`/vs`)

Build (needs VS 2022 with the *Visual Studio extension development* workload):

```
cd vs
msbuild /restore /p:Configuration=Release CopilotCompanion.sln
```

Then double-click the produced `CopilotCompanion.vsix` to install. Commands appear under
**Tools**, plus a toolbar; options under **Tools → Options → Copilot Companion**.

> **Important:** enable **Tools → Options → Environment → Documents →
> "Reload modified files unless there are unsaved changes"** (auto-reload of externally
> changed files). The Copilot agent edits files on disk, and without this Visual Studio
> will prompt for every change instead of picking it up automatically.

See [`vs/README.md`](vs/README.md) for details.

### JetBrains Rider (`/rider`)

```
cd rider
./gradlew buildPlugin        # gradlew.bat on Windows
```

Install the ZIP from `build/distributions/` via
**Settings → Plugins → ⚙ → Install Plugin from Disk…**. Settings live under
**Settings → Tools → Copilot Companion**.

> Rider auto-reloads externally changed files by default — no extra setting needed,
> unlike Visual Studio.

See [`rider/README.md`](rider/README.md) for details.

## License

[MIT](LICENSE) — free to use, modify, and distribute.
