using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CopilotCompanion.Services
{
    /// <summary>Writes to the "Copilot Companion" Output window pane. Safe to call from any thread.</summary>
    internal sealed class OutputPaneLogger
    {
        private static readonly Guid PaneGuid = new Guid("a1e6b7c8-2d43-4f5a-9b0e-6c7d8e9f0a13");

        private readonly AsyncPackage _package;
        private IVsOutputWindowPane _pane;

        private OutputPaneLogger(AsyncPackage package)
        {
            _package = package;
        }

        public static async Task<OutputPaneLogger> CreateAsync(AsyncPackage package)
        {
            var logger = new OutputPaneLogger(package);
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            if (await package.GetServiceAsync(typeof(SVsOutputWindow)) is IVsOutputWindow outputWindow)
            {
                Guid paneGuid = PaneGuid;
                outputWindow.CreatePane(ref paneGuid, "Copilot Companion", fInitVisible: 1, fClearWithSolution: 0);
                outputWindow.GetPane(ref paneGuid, out logger._pane);
            }

            return logger;
        }

        public void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
            IVsOutputWindowPane pane = _pane;
            if (pane == null)
            {
                return;
            }

            // OutputStringThreadSafe still requires the UI thread on some pane
            // implementations, so marshal there without blocking the caller.
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
                pane.OutputStringThreadSafe(line);
            }).FileAndForget("CopilotCompanion/log");
        }

        public void LogError(string context, Exception ex) => Log($"{context}: {ex}");
    }
}
