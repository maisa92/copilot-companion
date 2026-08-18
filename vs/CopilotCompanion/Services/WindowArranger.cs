using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CopilotCompanion.Services
{
    /// <summary>Finds and positions top-level windows via user32. All methods are UI-thread agnostic.</summary>
    internal static class WindowArranger
    {
        private const string VsCodeTitleMarker = "Visual Studio Code";

        /// <summary>Snapshot of VS Code top-level windows, used to tell a newly launched window from pre-existing ones.</summary>
        public static HashSet<IntPtr> SnapshotVsCodeWindows()
        {
            var result = new HashSet<IntPtr>();
            NativeMethods.EnumWindows((hwnd, lparam) =>
            {
                if (NativeMethods.IsWindowVisible(hwnd) &&
                    NativeMethods.GetWindowTitle(hwnd).IndexOf(VsCodeTitleMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(hwnd);
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        /// <summary>
        /// Polls for a visible VS Code window whose title contains <paramref name="folderName"/>,
        /// preferring windows not present in <paramref name="preExisting"/>. Never call on the UI thread.
        /// </summary>
        public static async Task<IntPtr> WaitForVsCodeWindowAsync(
            string folderName,
            HashSet<IntPtr> preExisting,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            IntPtr fallback = IntPtr.Zero;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IntPtr found = IntPtr.Zero;
                IntPtr existingMatch = IntPtr.Zero;
                NativeMethods.EnumWindows((hwnd, lparam) =>
                {
                    if (!NativeMethods.IsWindowVisible(hwnd))
                    {
                        return true;
                    }

                    string title = NativeMethods.GetWindowTitle(hwnd);
                    if (title.IndexOf(VsCodeTitleMarker, StringComparison.OrdinalIgnoreCase) < 0 ||
                        title.IndexOf(folderName, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return true;
                    }

                    if (preExisting != null && preExisting.Contains(hwnd))
                    {
                        existingMatch = hwnd;
                        return true;
                    }

                    found = hwnd;
                    return false;
                }, IntPtr.Zero);

                if (found != IntPtr.Zero)
                {
                    return found;
                }

                fallback = existingMatch != IntPtr.Zero ? existingMatch : fallback;
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            // A pre-existing window on the same folder is still a usable companion.
            return fallback;
        }

        /// <summary>Splits the work area of the monitor hosting <paramref name="hostHwnd"/>: companion left, host right.</summary>
        public static void ArrangeSideBySide(IntPtr hostHwnd, IntPtr companionHwnd, int companionPercent)
        {
            IntPtr monitor = NativeMethods.MonitorFromWindow(hostHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = NativeMethods.MONITORINFO.Create();
            if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                return;
            }

            NativeMethods.RECT work = info.rcWork;
            int width = work.Right - work.Left;
            int height = work.Bottom - work.Top;
            int companionWidth = width * companionPercent / 100;

            // SetWindowPos is ignored for maximized windows, so restore first.
            if (NativeMethods.IsZoomed(hostHwnd))
            {
                NativeMethods.ShowWindow(hostHwnd, NativeMethods.SW_RESTORE);
            }
            if (NativeMethods.IsZoomed(companionHwnd))
            {
                NativeMethods.ShowWindow(companionHwnd, NativeMethods.SW_RESTORE);
            }

            NativeMethods.SetWindowPos(
                companionHwnd, IntPtr.Zero,
                work.Left, work.Top, companionWidth, height,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW);

            NativeMethods.SetWindowPos(
                hostHwnd, IntPtr.Zero,
                work.Left + companionWidth, work.Top, width - companionWidth, height,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW);
        }

        public static void RestoreLayout(IntPtr hostHwnd, IntPtr companionHwnd)
        {
            if (companionHwnd != IntPtr.Zero && NativeMethods.IsWindow(companionHwnd))
            {
                NativeMethods.ShowWindow(companionHwnd, NativeMethods.SW_MINIMIZE);
            }

            NativeMethods.ShowWindow(hostHwnd, NativeMethods.SW_MAXIMIZE);
            NativeMethods.SetForegroundWindow(hostHwnd);
        }
    }
}
