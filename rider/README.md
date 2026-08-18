# Copilot Companion — Rider plugin

Launches VS Code as a side-by-side AI companion window (with GitHub Copilot Chat) for the
solution currently open in JetBrains Rider.

## Prerequisites

- **Rider 2025.1+** (plugin `sinceBuild` is 251).
- **VS Code** with the `code` CLI available. The plugin resolves it from (in order):
  a custom path in *Settings | Tools | Copilot Companion*, the `PATH`, and the default
  per-user Windows install (`%LocalAppData%\Programs\Microsoft VS Code`). Download:
  <https://code.visualstudio.com/download>.
- **GitHub Copilot Chat** extension installed in VS Code for the chat experience.
- **JDK 21** to build.

Window arranging (side-by-side tiling, restore layout) is Windows-only; on other
operating systems the plugin still launches VS Code, opens chat, and syncs files.

## Build

```
./gradlew buildPlugin
```

The installable zip lands in `build/distributions/`. First run downloads the Rider
2025.1 SDK (several GB) — be patient.

For development, launch a sandboxed Rider with the plugin pre-installed:

```
./gradlew runIde
```

## Install

Rider → *Settings | Plugins | ⚙ | Install Plugin from Disk…* → pick the zip from
`build/distributions/`, then restart.

## Usage

| Action | Shortcut | What it does |
|---|---|---|
| Open Copilot Companion | `Ctrl+Shift+Alt+C` | Writes a minimal-UI profile into the solution's `.vscode/settings.json` (merged, never overwriting your settings), launches `code -n <solution folder>`, opens Copilot Chat, and tiles VS Code left (30 % by default) / Rider right. |
| Restore Layout | `Ctrl+Shift+Alt+R` | Maximizes Rider and minimizes the companion window. |
| Toggle Copilot File Sync | — (Tools menu) | While the companion is active, the active file + caret line follow you into VS Code (`code --reuse-window --goto`), debounced 500 ms. |

Settings live under *Settings | Tools | Copilot Companion*: split ratio, auto-open chat,
file-sync default, custom `code` path.

## Docked mode (tool window)

Besides the side-by-side window mode, the plugin registers a **Copilot Companion tool
window** docked on the right edge of Rider (like the AI Assistant panel). It embeds the
full VS Code web UI — served locally by `code serve-web` on `127.0.0.1:8384` — in a JCEF
browser, opened on the current solution folder.

Notes:

- The first open downloads the VS Code server bundle (one-time, up to ~90 s).
- Extensions in the embedded VS Code are a separate profile from your desktop VS Code:
  install **GitHub Copilot Chat** and sign in once inside the tool window.
- Requires a Rider runtime with JCEF (the default JetBrains Runtime has it); otherwise
  the tool window tells you to use the windowed mode instead.
- The server is shared across projects and stopped when the project closes.

## File reloading

Rider auto-reloads externally changed files by default, so edits made by the Copilot
agent in VS Code appear in Rider with **no extra configuration** — unlike Visual Studio,
which needs its auto-reload option enabled (see the repo root README).

## Implementation notes

- Win32 window management uses JNA (`com.sun.jna.platform.win32.User32`) directly —
  Rider bundles jna/jna-platform, so no helper process or extra runtime dependency is
  needed. `compileOnly` JNA artifacts in `build.gradle.kts` exist only so compilation
  never depends on the resolved IDE distribution's classpath layout.
- All process launching and window polling runs on pooled threads; the EDT is never
  blocked. Errors go to `idea.log` and surface as balloon notifications.
