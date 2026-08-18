using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CopilotCompanion.Services
{
    /// <summary>
    /// Runs `code serve-web` — VS Code's built-in local web server — so the full VS Code
    /// UI (including Copilot Chat) can be embedded in a WebView2 tool window.
    /// Background-thread only. If something already answers on the port (e.g. an
    /// instance from another VS or Rider window), it is reused instead of spawning a
    /// second server.
    /// </summary>
    internal sealed class ServeWebServer : IDisposable
    {
        private const string Host = "127.0.0.1";
        private const int Port = 8384;

        private readonly object _gate = new object();
        private Process _process;

        public static string ProjectUrl(string workspaceRoot) =>
            $"http://{Host}:{Port}/?folder={Uri.EscapeDataString(workspaceRoot)}";

        /// <summary>
        /// Ensures the server is running. Returns null on success, or a user-facing
        /// error message on failure.
        /// </summary>
        public async Task<string> EnsureStartedAsync(string customCliPath, CancellationToken cancellationToken)
        {
            if (await IsUpAsync().ConfigureAwait(false))
            {
                return null;
            }

            string cli = VsCodeCli.Resolve(customCliPath);
            if (cli == null)
            {
                return "Visual Studio Code was not found on PATH or in %LocalAppData%\\Programs\\Microsoft VS Code.\n" +
                       $"Download it from {VsCodeCli.DownloadUrl}, or set the executable path under Tools > Options > Copilot Companion.";
            }

            if (!VsCodeCli.SupportsSubcommands(cli))
            {
                return "The configured VS Code path points at Code.exe, which cannot run the embedded web server.\n" +
                       "Point the setting at bin\\code.cmd instead (or clear it to auto-detect).";
            }

            lock (_gate)
            {
                if (_process == null || _process.HasExited)
                {
                    _process = VsCodeCli.StartLongRunning(
                        cli,
                        $"serve-web --host {Host} --port {Port} --without-connection-token --accept-server-license-terms");
                }
            }

            // The first launch may download the VS Code server bundle — allow up to 90 s.
            DateTime deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsUpAsync().ConfigureAwait(false))
                {
                    return null;
                }

                lock (_gate)
                {
                    if (_process == null || _process.HasExited)
                    {
                        break;
                    }
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            return "The VS Code web server did not start. Your VS Code version may not support " +
                   "`code serve-web` — update VS Code and try again.";
        }

        private static async Task<bool> IsUpAsync()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create($"http://{Host}:{Port}/");
                request.Method = "GET";
                request.Timeout = 1000;
                request.ReadWriteTimeout = 1000;
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                {
                    return (int)response.StatusCode >= 200 && (int)response.StatusCode < 400;
                }
            }
            catch (WebException e)
            {
                // Any HTTP status still proves something is listening.
                return e.Response != null;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            Process process;
            lock (_gate)
            {
                process = _process;
                _process = null;
            }

            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    // The server runs as a child of the cmd.exe shim, so kill the whole tree.
                    using (Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {process.Id} /T /F",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }))
                    {
                    }
                }
            }
            catch
            {
                // Best effort; an orphaned serve-web is reused by the next session anyway.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
