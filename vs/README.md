# Copilot Companion — Visual Studio 2022 / 2026 extension

Adds four commands (Tools menu + "Copilot Companion" toolbar):

| Command | Shortcut | What it does |
| --- | --- | --- |
| Open Copilot Companion | `Ctrl+Shift+Alt+C` | Writes a minimal-UI `.vscode/settings.json` into the solution root (merged, non-destructive), launches VS Code in a new window on that folder, opens Copilot Chat, and arranges VS Code left / Visual Studio right on the current monitor. |
| Open Companion Chat (Docked) | `Ctrl+Shift+Alt+D` | Opens the **Copilot Companion tool window** — the full VS Code UI (served locally by `code serve-web` on `127.0.0.1:8384`) embedded in a WebView2, forced into a chat-only layout, dockable anywhere in Visual Studio like any tool window. |
| Restore Layout | `Ctrl+Shift+Alt+R` | Maximizes Visual Studio and minimizes the companion VS Code window. |
| Sync Active File with Companion | — | Toggle. While on (and the companion is open), the active document + caret line follow into VS Code (`code --reuse-window --goto`), debounced 500 ms. |

Settings: **Tools > Options > Copilot Companion** — split ratio, auto-open chat, file-sync default, custom `code` executable path.

## Prerequisites

- Visual Studio 2022 (17.x) or Visual Studio 2026 (18.x), Windows (amd64 or arm64).
- The docked tool window needs the WebView2 Runtime (preinstalled on Windows 11 and on any machine with Edge or VS 2022+).
- [Visual Studio Code](https://code.visualstudio.com/download) with the GitHub Copilot Chat extension. The `code` CLI is resolved from the custom path setting, then `PATH`, then `%LocalAppData%\Programs\Microsoft VS Code`.

## Build

Requires VS 2022 with the **Visual Studio extension development** workload.

```
cd vs
msbuild /restore CopilotCompanion.sln /p:Configuration=Release
```

The VSIX lands in `CopilotCompanion\bin\Release\CopilotCompanion.vsix` — double-click to install, or press F5 in VS to debug in the experimental instance.

## Important: enable auto-reload of externally changed files

The Copilot agent edits files on disk from the VS Code side. So Visual Studio picks those edits up without prompting, enable:

**Tools > Options > Environment > Documents > "Detect when file is changed outside the environment"** and **"Reload modified files unless there are unsaved changes"**.

## Docked mode notes

- The first open downloads the VS Code server bundle (one-time, up to ~90 s).
- The embedded VS Code has its own extension profile: install **GitHub Copilot Chat** and
  sign in once inside the tool window, and Trust the workspace when prompted.
- After every load the extension enforces a chat-only layout (maximized secondary side
  bar) via VS Code's own "Toggle Maximized Secondary Side Bar" action.
- The local server is shared with the Rider plugin (same port 8384) and killed when VS closes.
