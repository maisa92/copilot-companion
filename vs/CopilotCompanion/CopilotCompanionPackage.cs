using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CopilotCompanion.Commands;
using CopilotCompanion.Options;
using CopilotCompanion.Services;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace CopilotCompanion
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Copilot Companion", "Launches VS Code with GitHub Copilot Chat as a side-by-side or docked companion for the current solution.", "1.5")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionsPage(typeof(CompanionOptionsPage), "Copilot Companion", "General", 0, 0, true)]
    [ProvideToolWindow(typeof(ToolWindows.CompanionToolWindow),
        Style = VsDockStyle.Tabbed,
        Orientation = ToolWindowOrientation.Right,
        Window = "{3AE79031-E1BC-11D0-8F78-00A0C9110057}")] // dock tabbed with Solution Explorer (right)
    [Guid(PackageGuids.PackageGuidString)]
    public sealed class CopilotCompanionPackage : AsyncPackage
    {
        internal static CopilotCompanionPackage Instance { get; private set; }

        internal DTE2 Dte { get; private set; }
        internal OutputPaneLogger Logger { get; private set; }
        internal CompanionSession Session { get; } = new CompanionSession();
        internal FileSyncService FileSync { get; private set; }
        internal ServeWebServer ServeWeb { get; } = new ServeWebServer();

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Instance = this;
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Dte = (DTE2)await GetServiceAsync(typeof(DTE));
            Logger = await OutputPaneLogger.CreateAsync(this);

            FileSync = new FileSyncService(this) { Enabled = Options.FileSyncEnabled };
            FileSync.Start();

            if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                OpenCompanionCommand.Initialize(this, commandService);
                RestoreLayoutCommand.Initialize(this, commandService);
                ToggleFileSyncCommand.Initialize(this, commandService);
                OpenCompanionToolWindowCommand.Initialize(this, commandService);
            }

            Logger.Log("Copilot Companion initialized.");
        }

        /// <summary>UI thread only.</summary>
        internal CompanionOptionsPage Options
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return (CompanionOptionsPage)GetDialogPage(typeof(CompanionOptionsPage));
            }
        }

        /// <summary>UI thread only.</summary>
        internal IntPtr MainWindowHandle
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return new IntPtr(Dte.MainWindow.HWnd);
            }
        }

        internal void ShowError(string message)
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
                VsShellUtilities.ShowMessageBox(
                    this,
                    message,
                    "Copilot Companion",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }).FileAndForget("CopilotCompanion/showerror");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                FileSync?.Dispose();
                ServeWeb.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
