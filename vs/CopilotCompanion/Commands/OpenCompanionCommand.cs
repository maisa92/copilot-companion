using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using CopilotCompanion.Services;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;
using TaskScheduler = System.Threading.Tasks.TaskScheduler;

namespace CopilotCompanion.Commands
{
    internal sealed class OpenCompanionCommand
    {
        private readonly CopilotCompanionPackage _package;

        private OpenCompanionCommand(CopilotCompanionPackage package)
        {
            _package = package;
        }

        public static void Initialize(CopilotCompanionPackage package, OleMenuCommandService commandService)
        {
            var instance = new OpenCompanionCommand(package);
            var commandId = new CommandID(PackageGuids.CmdSet, PackageIds.OpenCompanionCommandId);
            commandService.AddCommand(new OleMenuCommand(instance.Execute, commandId));
        }

        private void Execute(object sender, EventArgs e)
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ExecuteAsync();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _package.Logger?.LogError("Open Copilot Companion", ex);
                    _package.ShowError("Opening the Copilot companion failed. See the 'Copilot Companion' Output pane for details.");
                }
            }).FileAndForget("CopilotCompanion/open");
        }

        private async Task ExecuteAsync()
        {
            // Gather everything that needs the UI thread up front.
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

            string workspaceRoot = null;
            if (await _package.GetServiceAsync(typeof(SVsSolution)) is IVsSolution solution)
            {
                // Works for both .sln solutions and Open Folder mode.
                solution.GetSolutionInfo(out workspaceRoot, out _, out _);
            }

            if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            {
                _package.ShowError("No solution or folder is open. Open one first, then run Open Copilot Companion.");
                return;
            }

            IntPtr hostHwnd = _package.MainWindowHandle;
            var options = _package.Options;
            int splitRatio = options.SplitRatio;
            bool autoOpenChat = options.AutoOpenChat;
            string customCliPath = options.CodeExecutablePath;
            _package.FileSync.Enabled = options.FileSyncEnabled;

            // Everything else happens off the UI thread.
            await TaskScheduler.Default;

            try
            {
                SettingsJsonMerger.ApplyMinimalUiProfile(workspaceRoot);
            }
            catch (Exception ex)
            {
                // A malformed settings.json must not block the launch — and must not be overwritten.
                _package.Logger?.LogError("Merging .vscode/settings.json failed; leaving the file untouched", ex);
            }

            string cli = VsCodeCli.Resolve(customCliPath);
            if (cli == null)
            {
                _package.ShowError(
                    "Visual Studio Code was not found on PATH or in %LocalAppData%\\Programs\\Microsoft VS Code.\n\n" +
                    $"Download it from {VsCodeCli.DownloadUrl}, or set the executable path under " +
                    "Tools > Options > Copilot Companion.");
                return;
            }

            _package.Logger?.Log($"Launching VS Code ({cli}) for '{workspaceRoot}'.");

            var preExisting = WindowArranger.SnapshotVsCodeWindows();
            VsCodeCli.Start(cli, $"-n \"{workspaceRoot}\"");

            string folderName = Path.GetFileName(workspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            IntPtr companionHwnd = await WindowArranger.WaitForVsCodeWindowAsync(
                folderName, preExisting, TimeSpan.FromSeconds(15), _package.DisposalToken);

            if (companionHwnd == IntPtr.Zero)
            {
                _package.Logger?.Log("Timed out waiting for the VS Code window; skipping arrangement and chat.");
                _package.ShowError("VS Code was launched but its window did not appear within 15 seconds.");
                return;
            }

            NativeMethods.GetWindowThreadProcessId(companionHwnd, out uint companionPid);
            _package.Session.Track(companionHwnd, companionPid, workspaceRoot, cli);

            if (autoOpenChat && VsCodeCli.SupportsSubcommands(cli))
            {
                // "chat" only exists in newer VS Code CLIs; a failure here is expected and silent.
                await VsCodeCli.RunAsync(cli, "chat --reuse-window \"\"", TimeSpan.FromSeconds(10), _package.DisposalToken);
            }

            WindowArranger.ArrangeSideBySide(hostHwnd, companionHwnd, splitRatio);
            _package.Logger?.Log($"Companion ready (hwnd 0x{companionHwnd.ToInt64():X}, pid {companionPid}).");
        }
    }
}
