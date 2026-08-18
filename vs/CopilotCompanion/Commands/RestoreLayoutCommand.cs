using System;
using System.ComponentModel.Design;
using CopilotCompanion.Services;
using Microsoft.VisualStudio.Shell;

namespace CopilotCompanion.Commands
{
    internal sealed class RestoreLayoutCommand
    {
        private readonly CopilotCompanionPackage _package;

        private RestoreLayoutCommand(CopilotCompanionPackage package)
        {
            _package = package;
        }

        public static void Initialize(CopilotCompanionPackage package, OleMenuCommandService commandService)
        {
            var instance = new RestoreLayoutCommand(package);
            var commandId = new CommandID(PackageGuids.CmdSet, PackageIds.RestoreLayoutCommandId);
            commandService.AddCommand(new OleMenuCommand(instance.Execute, commandId));
        }

        private void Execute(object sender, EventArgs e)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                IntPtr hostHwnd = _package.MainWindowHandle;
                IntPtr companionHwnd = _package.Session.IsActive ? _package.Session.CompanionHwnd : IntPtr.Zero;
                WindowArranger.RestoreLayout(hostHwnd, companionHwnd);
            }
            catch (Exception ex)
            {
                _package.Logger?.LogError("Restore Layout", ex);
            }
        }
    }
}
