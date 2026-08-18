using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace CopilotCompanion.Options
{
    public class CompanionOptionsPage : DialogPage
    {
        private int _splitRatio = 30;

        [Category("Layout")]
        [DisplayName("Split ratio (%)")]
        [Description("Percentage of the monitor working area given to the VS Code companion window (left side). Clamped to 10-90.")]
        [DefaultValue(30)]
        public int SplitRatio
        {
            get => _splitRatio;
            set => _splitRatio = value < 10 ? 10 : value > 90 ? 90 : value;
        }

        [Category("Behavior")]
        [DisplayName("Auto-open Copilot Chat")]
        [Description("Open the Copilot Chat view in VS Code right after the companion window launches.")]
        [DefaultValue(true)]
        public bool AutoOpenChat { get; set; } = true;

        [Category("Behavior")]
        [DisplayName("File sync enabled by default")]
        [Description("Follow the active Visual Studio document in the companion VS Code window. Can be toggled per session via Tools > Sync Active File with Companion.")]
        [DefaultValue(true)]
        public bool FileSyncEnabled { get; set; } = true;

        [Category("VS Code")]
        [DisplayName("Path to code executable")]
        [Description("Optional full path to the VS Code CLI (code.cmd / code.exe). Leave empty to resolve from PATH and the default install location.")]
        [DefaultValue("")]
        public string CodeExecutablePath { get; set; } = string.Empty;
    }
}
