using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;

namespace CopilotCompanion.Commands
{
    internal sealed class ToggleFileSyncCommand
    {
        private readonly CopilotCompanionPackage _package;
        private OleMenuCommand _command;

        private ToggleFileSyncCommand(CopilotCompanionPackage package)
        {
            _package = package;
        }

        public static void Initialize(CopilotCompanionPackage package, OleMenuCommandService commandService)
        {
            var instance = new ToggleFileSyncCommand(package);
            var commandId = new CommandID(PackageGuids.CmdSet, PackageIds.ToggleFileSyncCommandId);
            instance._command = new OleMenuCommand(instance.Execute, commandId);
            instance._command.BeforeQueryStatus += instance.OnBeforeQueryStatus;
            commandService.AddCommand(instance._command);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            _command.Checked = _package.FileSync?.Enabled == true;
        }

        private void Execute(object sender, EventArgs e)
        {
            try
            {
                bool enabled = !_package.FileSync.Enabled;
                _package.FileSync.Enabled = enabled;
                _package.Logger?.Log($"File sync {(enabled ? "enabled" : "disabled")}.");
            }
            catch (Exception ex)
            {
                _package.Logger?.LogError("Toggle File Sync", ex);
            }
        }
    }
}
