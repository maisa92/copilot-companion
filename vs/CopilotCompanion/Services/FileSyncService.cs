using System;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using TaskScheduler = System.Threading.Tasks.TaskScheduler;

namespace CopilotCompanion.Services
{
    /// <summary>
    /// Mirrors the active document (and caret line) into the companion VS Code window
    /// via "code --reuse-window --goto file:line", debounced by 500ms.
    /// </summary>
    internal sealed class FileSyncService : IDisposable
    {
        private const int DebounceMilliseconds = 500;

        private readonly CopilotCompanionPackage _package;
        private readonly object _gate = new object();
        private readonly Timer _debounce;

        // Rooted COM event references — without these fields the event sinks get garbage collected.
        private Events _events;
        private WindowEvents _windowEvents;

        private string _pendingFile;
        private int _pendingLine;

        public FileSyncService(CopilotCompanionPackage package)
        {
            _package = package;
            _debounce = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>Session toggle; seeded from options when the companion opens.</summary>
        public bool Enabled { get; set; }

        public void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _events = _package.Dte.Events;
            _windowEvents = _events.WindowEvents;
            _windowEvents.WindowActivated += OnWindowActivated;
        }

        private void OnWindowActivated(Window gotFocus, Window lostFocus)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (!Enabled || !_package.Session.IsActive)
                {
                    return;
                }

                if (gotFocus == null || gotFocus.Kind != "Document" || gotFocus.Document == null)
                {
                    return;
                }

                string path = gotFocus.Document.FullName;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                int line = 1;
                if (gotFocus.Document.Selection is TextSelection selection)
                {
                    line = selection.ActivePoint.Line;
                }

                lock (_gate)
                {
                    _pendingFile = path;
                    _pendingLine = line;
                }
                _debounce.Change(DebounceMilliseconds, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                _package.Logger?.LogError("File sync (WindowActivated)", ex);
            }
        }

        private void OnDebounceElapsed(object state)
        {
            // Thread-pool thread; never touch DTE here.
            try
            {
                string file;
                int line;
                lock (_gate)
                {
                    file = _pendingFile;
                    line = _pendingLine;
                    _pendingFile = null;
                }

                if (file == null || !_package.Session.IsActive)
                {
                    return;
                }

                string cli = _package.Session.CliPath;
                if (cli == null)
                {
                    return;
                }

                _package.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    await VsCodeCli.RunAsync(cli, $"--reuse-window --goto \"{file}:{line}\"", TimeSpan.FromSeconds(10), _package.DisposalToken);
                }).FileAndForget("CopilotCompanion/filesync");
            }
            catch (Exception ex)
            {
                _package.Logger?.LogError("File sync (debounce)", ex);
            }
        }

        public void Dispose()
        {
            _debounce.Dispose();
            if (_windowEvents != null)
            {
                try
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    _windowEvents.WindowActivated -= OnWindowActivated;
                }
                catch (Exception)
                {
                    // VS is shutting down; the event source may already be gone.
                }
                _windowEvents = null;
                _events = null;
            }
        }
    }
}
