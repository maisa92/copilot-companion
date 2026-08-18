using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CopilotCompanion.Services
{
    /// <summary>Merges the minimal-UI profile into &lt;workspace&gt;/.vscode/settings.json without touching other keys.</summary>
    internal static class SettingsJsonMerger
    {
        public static void ApplyMinimalUiProfile(string workspaceRoot)
        {
            string vscodeDir = Path.Combine(workspaceRoot, ".vscode");
            string settingsPath = Path.Combine(vscodeDir, "settings.json");

            JObject settings = new JObject();
            if (File.Exists(settingsPath))
            {
                string text = File.ReadAllText(settingsPath);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // VS Code settings files may contain // comments; Newtonsoft ignores them by default.
                    settings = JObject.Parse(text);
                }
            }

            settings["workbench.activityBar.location"] = "hidden";
            settings["workbench.statusBar.visible"] = false;
            settings["editor.minimap.enabled"] = false;
            // Chat-first profile (shared with the Rider plugin): open the Copilot Chat
            // side bar maximized so the companion reads as a chat panel, not a full IDE.
            settings["workbench.secondarySideBar.defaultVisibility"] = "maximized";
            settings["workbench.startupEditor"] = "none";
            // Built-in theme with the classic Visual Studio dark palette (#1E1E1E),
            // so the embedded panel blends into the VS 2022/2026 Dark theme.
            settings["workbench.colorTheme"] = "Visual Studio Dark";
            // Fresh VS Code installs auto-detect the OS color scheme, which overrides
            // workbench.colorTheme entirely — turn it off so dark mode always wins.
            settings["window.autoDetectColorScheme"] = false;

            Directory.CreateDirectory(vscodeDir);
            File.WriteAllText(settingsPath, settings.ToString(Formatting.Indented));
        }
    }
}
