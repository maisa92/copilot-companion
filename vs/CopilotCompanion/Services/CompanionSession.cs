using System;

namespace CopilotCompanion.Services
{
    /// <summary>Tracks the companion VS Code window across commands. Thread-safe.</summary>
    internal sealed class CompanionSession
    {
        private readonly object _gate = new object();
        private IntPtr _companionHwnd;
        private uint _companionProcessId;
        private string _workspaceRoot;
        private string _cliPath;

        public void Track(IntPtr hwnd, uint processId, string workspaceRoot, string cliPath)
        {
            lock (_gate)
            {
                _companionHwnd = hwnd;
                _companionProcessId = processId;
                _workspaceRoot = workspaceRoot;
                _cliPath = cliPath;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _companionHwnd = IntPtr.Zero;
                _companionProcessId = 0;
                _workspaceRoot = null;
                _cliPath = null;
            }
        }

        public IntPtr CompanionHwnd { get { lock (_gate) { return _companionHwnd; } } }

        public uint CompanionProcessId { get { lock (_gate) { return _companionProcessId; } } }

        public string WorkspaceRoot { get { lock (_gate) { return _workspaceRoot; } } }

        public string CliPath { get { lock (_gate) { return _cliPath; } } }

        public bool IsActive
        {
            get
            {
                IntPtr hwnd = CompanionHwnd;
                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }

                if (!NativeMethods.IsWindow(hwnd))
                {
                    Clear();
                    return false;
                }

                return true;
            }
        }
    }
}
