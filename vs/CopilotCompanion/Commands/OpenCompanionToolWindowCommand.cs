using System;
using System.ComponentModel.Design;
using CopilotCompanion.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CopilotCompanion.Commands
{
    /// <summary>Shows the docked "Copilot Companion" tool window (embedded VS Code chat).</summary>
    internal sealed class OpenCompanionToolWindowCommand
    {
        private readonly CopilotCompanionPackage _package;

        private OpenCompanionToolWindowCommand(CopilotCompanionPackage package)
        {
            _package = package;
        }

        public static void Initialize(CopilotCompanionPackage package, OleMenuCommandService commandService)
        {
            var instance = new OpenCompanionToolWindowCommand(package);
            var commandId = new CommandID(PackageGuids.CmdSet, PackageIds.OpenCompanionToolWindowCommandId);
            commandService.AddCommand(new OleMenuCommand(instance.Execute, commandId));
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                ToolWindowPane window = _package.FindToolWindow(typeof(CompanionToolWindow), 0, true);
                if (window?.Frame == null)
                {
                    throw new NotSupportedException("Cannot create the Copilot Companion tool window.");
                }

                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(((IVsWindowFrame)window.Frame).Show());
            }
            catch (Exception ex)
            {
                _package.Logger?.LogError("Open Companion Chat (Docked)", ex);
                _package.ShowError("Opening the docked companion window failed. See the 'Copilot Companion' Output pane for details.");
            }
        }
    }
}
