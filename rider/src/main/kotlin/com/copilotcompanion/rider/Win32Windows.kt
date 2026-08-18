package com.copilotcompanion.rider

import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.util.SystemInfo
import com.sun.jna.Native
import com.sun.jna.Pointer
import com.sun.jna.platform.win32.User32
import com.sun.jna.platform.win32.WinDef.HWND
import com.sun.jna.platform.win32.WinUser
import java.awt.Component

/**
 * Win32 window management via JNA.
 *
 * Rider bundles jna and jna-platform in its distribution, so calling
 * [com.sun.jna.platform.win32.User32] directly is simpler and more reliable than
 * shipping a PowerShell/C# helper process — no temp files, no AV false positives,
 * no PowerShell execution-policy issues. Every entry point is a no-op off Windows.
 */
object Win32Windows {

    private val log = Logger.getInstance(Win32Windows::class.java)

    // Not all of these constants are exposed by WinUser across JNA versions; define locally.
    private const val SW_RESTORE = 9
    private const val SW_MAXIMIZE = 3
    private const val SW_MINIMIZE = 6
    private const val SWP_NOZORDER = 0x0004
    private const val SWP_SHOWWINDOW = 0x0040
    private const val MONITOR_DEFAULTTONEAREST = 2

    val isSupported: Boolean get() = SystemInfo.isWindows

    /** HWND of a heavyweight AWT component (the Rider main frame). */
    fun hwndOf(component: Component): HWND? {
        if (!isSupported) return null
        return try {
            val id = Native.getComponentID(component)
            if (id == 0L) null else HWND(Pointer.createConstant(id))
        } catch (e: Throwable) {
            log.warn("Could not resolve HWND of IDE frame", e)
            null
        }
    }

    /**
     * Finds the first visible top-level window whose title contains all of [substrings]
     * (case-insensitive). Used to locate the freshly launched VS Code window by
     * folder name + "Visual Studio Code" so we never match Rider's own frame.
     */
    fun findWindow(vararg substrings: String): HWND? {
        if (!isSupported) return null
        var result: HWND? = null
        try {
            User32.INSTANCE.EnumWindows({ hwnd, _ ->
                if (User32.INSTANCE.IsWindowVisible(hwnd)) {
                    val buffer = CharArray(1024)
                    val length = User32.INSTANCE.GetWindowText(hwnd, buffer, buffer.size)
                    if (length > 0) {
                        val title = String(buffer, 0, length)
                        if (substrings.all { title.contains(it, ignoreCase = true) }) {
                            result = hwnd
                            return@EnumWindows false // stop enumeration
                        }
                    }
                }
                true
            }, Pointer.NULL)
        } catch (e: Throwable) {
            log.warn("EnumWindows failed", e)
        }
        return result
    }

    fun isWindow(hwnd: HWND): Boolean =
        isSupported && runCatching { User32.INSTANCE.IsWindow(hwnd) }.getOrDefault(false)

    /**
     * Tiles [left] onto the left [leftPercent]% of the work area of the monitor
     * containing [right], and [right] onto the remainder. Both windows are restored
     * from the maximized/minimized state first — SetWindowPos has no effect on a
     * maximized window.
     */
    fun arrangeSideBySide(left: HWND, right: HWND, leftPercent: Int) {
        if (!isSupported) return
        try {
            val u32 = User32.INSTANCE
            val monitor = u32.MonitorFromWindow(right, MONITOR_DEFAULTTONEAREST)
            val info = WinUser.MONITORINFO()
            if (!u32.GetMonitorInfo(monitor, info).booleanValue()) {
                log.warn("GetMonitorInfo failed; skipping window arrangement")
                return
            }
            val work = info.rcWork
            val workWidth = work.right - work.left
            val workHeight = work.bottom - work.top
            val leftWidth = workWidth * leftPercent.coerceIn(10, 90) / 100

            u32.ShowWindow(left, SW_RESTORE)
            u32.ShowWindow(right, SW_RESTORE)

            u32.SetWindowPos(
                left, null,
                work.left, work.top, leftWidth, workHeight,
                SWP_NOZORDER or SWP_SHOWWINDOW
            )
            u32.SetWindowPos(
                right, null,
                work.left + leftWidth, work.top, workWidth - leftWidth, workHeight,
                SWP_NOZORDER or SWP_SHOWWINDOW
            )
        } catch (e: Throwable) {
            log.warn("Failed to arrange windows", e)
        }
    }

    fun maximize(hwnd: HWND) = show(hwnd, SW_MAXIMIZE)

    fun minimize(hwnd: HWND) = show(hwnd, SW_MINIMIZE)

    private fun show(hwnd: HWND, command: Int) {
        if (!isSupported) return
        try {
            User32.INSTANCE.ShowWindow(hwnd, command)
        } catch (e: Throwable) {
            log.warn("ShowWindow($command) failed", e)
        }
    }
}
