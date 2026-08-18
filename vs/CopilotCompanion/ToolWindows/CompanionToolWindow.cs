using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace CopilotCompanion.ToolWindows
{
    /// <summary>
    /// "Copilot Companion" tool window: embeds the full VS Code UI (served locally by
    /// `code serve-web`) in a WebView2 docked inside Visual Studio — the chat-only
    /// companion panel, like the Rider tool window.
    /// </summary>
    [Guid(ToolWindowGuidString)]
    public sealed class CompanionToolWindow : ToolWindowPane
    {
        public const string ToolWindowGuidString = "a3c9f7d2-5e14-4b86-9a07-6c2d8f1b3e45";

        public CompanionToolWindow() : base(null)
        {
            Caption = "Copilot Companion";
            Content = new CompanionToolWindowControl();
        }

        protected override void Initialize()
        {
            base.Initialize();
            ((CompanionToolWindowControl)Content).Start(CopilotCompanionPackage.Instance);
        }
    }
}
