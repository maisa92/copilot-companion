using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CopilotCompanion.Services;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Task = System.Threading.Tasks.Task;
using TaskScheduler = System.Threading.Tasks.TaskScheduler;

namespace CopilotCompanion.ToolWindows
{
    /// <summary>
    /// WPF content of the companion tool window: a status label until the embedded
    /// VS Code (code serve-web) is reachable, then a WebView2 showing it chat-only.
    /// Self-healing: failed navigations restart the server and reload, with a cap.
    /// </summary>
    internal sealed class CompanionToolWindowControl : Grid
    {
        private const int MaxRetries = 10;

        /// <summary>
        /// Runs inside the embedded workbench after every load. Polls until the
        /// auxiliary bar (chat) exists, then clicks its "Maximize" title button; if
        /// that button isn't found, falls back to hiding the primary side bar with
        /// Ctrl/Cmd+B. No-op when the layout is already chat-only.
        /// Keep in sync with the Rider plugin's CompanionToolWindowFactory.CHAT_ONLY_JS.
        /// </summary>
        private const string ChatOnlyJs = @"
            (() => {
                let tries = 0;
                const timer = setInterval(() => {
                    tries++;
                    if (tries > 120) { clearInterval(timer); return; }
                    const aux = document.querySelector('.monaco-workbench .part.auxiliarybar');
                    if (!aux || aux.clientWidth === 0) return;
                    const maxBtn = aux.querySelector(
                        '.codicon-auxiliarybar-maximize, .codicon-panel-maximize, .codicon-screen-full');
                    if (maxBtn) {
                        const item = maxBtn.closest('.action-item') || maxBtn;
                        const alreadyMaximized = item.classList.contains('checked')
                            || maxBtn.classList.contains('checked')
                            || maxBtn.getAttribute('aria-checked') === 'true';
                        if (!alreadyMaximized) maxBtn.click();
                        clearInterval(timer);
                        return;
                    }
                    const sidebar = document.querySelector('.monaco-workbench .part.sidebar');
                    if (sidebar && sidebar.clientWidth > 0) {
                        const mac = navigator.userAgent.includes('Mac');
                        document.body.dispatchEvent(new KeyboardEvent('keydown', {
                            key: 'b', code: 'KeyB', keyCode: 66,
                            metaKey: mac, ctrlKey: !mac, bubbles: true
                        }));
                    }
                    clearInterval(timer);
                }, 500);
            })();";

        private readonly TextBlock _status;
        private WebView2 _webView;
        private CopilotCompanionPackage _package;
        private string _url;
        private int _consecutiveErrors;

        public CompanionToolWindowControl()
        {
            _status = new TextBlock
            {
                Text = "Starting embedded VS Code…",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16),
            };
            Children.Add(_status);
        }

        public void Start(CopilotCompanionPackage package)
        {
            _package = package;
            package.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await RunAsync();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _package.Logger?.LogError("Companion tool window", ex);
                    await SetStatusAsync("The embedded VS Code failed to start: " + ex.Message);
                }
            }).FileAndForget("CopilotCompanion/toolwindow");
        }

        private async Task RunAsync()
        {
            string workspaceRoot = await WaitForWorkspaceRootAsync();
            if (workspaceRoot == null)
            {
                await SetStatusAsync("No solution or folder is open. Open one, then reopen this window.");
                return;
            }

            string customCliPath;
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            customCliPath = _package.Options.CodeExecutablePath;
            await TaskScheduler.Default;

            try
            {
                SettingsJsonMerger.ApplyMinimalUiProfile(workspaceRoot);
            }
            catch (Exception ex)
            {
                _package.Logger?.LogError("Merging .vscode/settings.json failed; leaving the file untouched", ex);
            }

            string error = await _package.ServeWeb.EnsureStartedAsync(customCliPath, _package.DisposalToken);
            if (error != null)
            {
                await SetStatusAsync(error);
                return;
            }

            _url = ServeWebServer.ProjectUrl(workspaceRoot);

            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CopilotCompanion", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

            _webView = new WebView2();
            await _webView.EnsureCoreWebView2Async(environment);
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            Children.Clear();
            Children.Add(_webView);
            _webView.Source = new Uri(_url);
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _consecutiveErrors = 0;
                // Enforce the chat-only layout regardless of what the workbench restored.
                _ = _webView.CoreWebView2.ExecuteScriptAsync(ChatOnlyJs);
                return;
            }

            if (++_consecutiveErrors > MaxRetries)
            {
                Children.Clear();
                _status.Text = "The embedded VS Code is not reachable. Close and reopen this window to retry.";
                Children.Add(_status);
                return;
            }

            // Server not up (yet) — restart it and reload once it answers.
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                await Task.Delay(2000, _package.DisposalToken);

                string customCliPath;
                await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
                customCliPath = _package.Options.CodeExecutablePath;
                await TaskScheduler.Default;

                string error = await _package.ServeWeb.EnsureStartedAsync(customCliPath, _package.DisposalToken);

                await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
                if (error == null)
                {
                    _webView.CoreWebView2.Navigate(_url);
                }
                else
                {
                    Children.Clear();
                    _status.Text = error;
                    Children.Add(_status);
                }
            }).FileAndForget("CopilotCompanion/toolwindow-retry");
        }

        /// <summary>
        /// The tool window can be restored by VS before the solution finishes loading,
        /// so poll (up to 5 minutes) for a workspace root instead of failing outright.
        /// </summary>
        private async Task<string> WaitForWorkspaceRootAsync()
        {
            for (int attempt = 0; attempt < 150; attempt++)
            {
                await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
                string root = null;
                if (await _package.GetServiceAsync(typeof(SVsSolution)) is IVsSolution solution)
                {
                    solution.GetSolutionInfo(out root, out _, out _);
                }

                if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                {
                    return root;
                }

                await TaskScheduler.Default;
                await Task.Delay(2000, _package.DisposalToken);
            }

            return null;
        }

        private async Task SetStatusAsync(string message)
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            Children.Clear();
            _status.Text = message;
            Children.Add(_status);
        }
    }
}
