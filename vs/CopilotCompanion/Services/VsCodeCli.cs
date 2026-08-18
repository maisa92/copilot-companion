using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CopilotCompanion.Services
{
    /// <summary>Resolves and invokes the VS Code command-line interface. Background-thread only.</summary>
    internal static class VsCodeCli
    {
        public const string DownloadUrl = "https://code.visualstudio.com/download";

        /// <summary>
        /// Resolution order: user-configured path, PATH, default per-user install.
        /// Prefers bin\code.cmd over Code.exe because CLI subcommands ("chat") only
        /// exist in the cmd shim, which routes through VS Code's cli.js.
        /// </summary>
        public static string Resolve(string customPath)
        {
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            {
                return customPath;
            }

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string rawDir in pathEnv.Split(Path.PathSeparator))
            {
                string dir = rawDir.Trim().Trim('"');
                if (dir.Length == 0)
                {
                    continue;
                }

                foreach (string candidate in new[] { "code.cmd", "code.exe", "code" })
                {
                    try
                    {
                        string full = Path.Combine(dir, candidate);
                        if (File.Exists(full))
                        {
                            return full;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Malformed PATH entry; skip.
                    }
                }
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string installRoot = Path.Combine(localAppData, "Programs", "Microsoft VS Code");
            string codeCmd = Path.Combine(installRoot, "bin", "code.cmd");
            if (File.Exists(codeCmd))
            {
                return codeCmd;
            }

            string codeExe = Path.Combine(installRoot, "Code.exe");
            return File.Exists(codeExe) ? codeExe : null;
        }

        /// <summary>CLI subcommands like "chat" need the code.cmd/cli.js shim; plain Code.exe only accepts electron-style args.</summary>
        public static bool SupportsSubcommands(string cliPath)
        {
            string name = Path.GetFileName(cliPath) ?? string.Empty;
            return !name.Equals("Code.exe", StringComparison.OrdinalIgnoreCase);
        }

        public static void Start(string cliPath, string arguments)
        {
            using (Process.Start(BuildStartInfo(cliPath, arguments)))
            {
            }
        }

        /// <summary>Starts a long-running CLI process (e.g. serve-web) and returns it for lifetime management.</summary>
        public static Process StartLongRunning(string cliPath, string arguments)
        {
            return Process.Start(BuildStartInfo(cliPath, arguments));
        }

        /// <summary>Runs the CLI and waits for exit (bounded). Returns the exit code, or null on timeout/start failure.</summary>
        public static async Task<int?> RunAsync(string cliPath, string arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using (var process = new Process { StartInfo = BuildStartInfo(cliPath, arguments) })
            {
                var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) => exited.TrySetResult(process.ExitCode);

                if (!process.Start())
                {
                    return null;
                }

                Task completed = await Task.WhenAny(exited.Task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
                return completed == exited.Task ? (int?)await exited.Task.ConfigureAwait(false) : null;
            }
        }

        private static ProcessStartInfo BuildStartInfo(string cliPath, string arguments)
        {
            // .cmd files cannot be started directly with UseShellExecute=false, so route
            // through cmd.exe; that also keeps CreateNoWindow effective (no console flash).
            if (cliPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessStartInfo
                {
                    FileName = Environment.ExpandEnvironmentVariables("%ComSpec%"),
                    Arguments = $"/d /s /c \"\"{cliPath}\" {arguments}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            return new ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }
    }
}
