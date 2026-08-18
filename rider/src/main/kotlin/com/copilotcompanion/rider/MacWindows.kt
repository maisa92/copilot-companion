package com.copilotcompanion.rider

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.SystemInfo
import com.intellij.openapi.wm.WindowManager
import java.awt.Frame
import java.awt.Rectangle
import java.awt.Toolkit
import java.util.concurrent.TimeUnit

/**
 * macOS window arrangement.
 *
 * The Rider frame is our own Swing window, so it is moved directly. VS Code belongs to
 * another process, and macOS offers no public API to move foreign windows — the supported
 * route is AppleScript UI scripting (System Events), which requires the user to grant
 * Rider the Accessibility permission (System Settings | Privacy & Security | Accessibility).
 */
object MacWindows {

    private val log = Logger.getInstance(MacWindows::class.java)

    val isSupported: Boolean get() = SystemInfo.isMac

    /** VS Code's process name as seen by System Events. */
    private const val VSCODE_PROCESS = "Code"

    private fun osascript(script: String): String? = try {
        val proc = ProcessBuilder("osascript", "-e", script).redirectErrorStream(true).start()
        val out = proc.inputStream.bufferedReader().readText().trim()
        if (proc.waitFor(10, TimeUnit.SECONDS) && proc.exitValue() == 0) out else {
            log.warn("osascript failed: $out")
            null
        }
    } catch (e: Throwable) {
        log.warn("osascript failed", e)
        null
    }

    /** Waits until VS Code has at least one window System Events can see. */
    fun waitForVsCodeWindow(timeoutMs: Long): Boolean {
        val deadline = System.currentTimeMillis() + timeoutMs
        while (System.currentTimeMillis() < deadline) {
            val count = osascript(
                """tell application "System Events" to if exists process "$VSCODE_PROCESS" then count windows of process "$VSCODE_PROCESS""""
            )?.toIntOrNull() ?: 0
            if (count > 0) return true
            Thread.sleep(500)
        }
        return false
    }

    /**
     * Tiles VS Code onto the left [leftPercent]% of the work area of the monitor showing
     * the Rider frame, and the Rider frame onto the remainder. Returns false when VS Code
     * could not be moved (usually a missing Accessibility permission).
     */
    fun arrange(project: Project, leftPercent: Int): Boolean {
        val work = riderWorkArea(project) ?: return false
        val leftWidth = work.width * leftPercent.coerceIn(10, 90) / 100

        ApplicationManager.getApplication().invokeLater {
            WindowManager.getInstance().getFrame(project)?.apply {
                extendedState = Frame.NORMAL
                setBounds(work.x + leftWidth, work.y, work.width - leftWidth, work.height)
            }
        }

        // "window 1" is the frontmost VS Code window — the one `code -n` just opened.
        return osascript(
            """
            tell application "System Events" to tell process "$VSCODE_PROCESS"
                set position of window 1 to {${work.x}, ${work.y}}
                set size of window 1 to {$leftWidth, ${work.height}}
            end tell
            """.trimIndent()
        ) != null
    }

    /** Maximizes the Rider frame and minimizes the VS Code window. */
    fun restore(project: Project) {
        ApplicationManager.getApplication().invokeLater {
            WindowManager.getInstance().getFrame(project)?.extendedState = Frame.MAXIMIZED_BOTH
        }
        osascript(
            """tell application "System Events" to set value of attribute "AXMinimized" of window 1 of process "$VSCODE_PROCESS" to true"""
        )
    }

    /** Work area (screen minus menu bar/Dock) of the monitor showing the Rider frame, read on the EDT. */
    private fun riderWorkArea(project: Project): Rectangle? {
        var rect: Rectangle? = null
        ApplicationManager.getApplication().invokeAndWait {
            val gc = WindowManager.getInstance().getFrame(project)?.graphicsConfiguration ?: return@invokeAndWait
            val b = gc.bounds
            val i = Toolkit.getDefaultToolkit().getScreenInsets(gc)
            rect = Rectangle(b.x + i.left, b.y + i.top, b.width - i.left - i.right, b.height - i.top - i.bottom)
        }
        return rect
    }
}
