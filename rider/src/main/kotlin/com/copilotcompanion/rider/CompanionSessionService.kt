package com.copilotcompanion.rider

import com.intellij.openapi.Disposable
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.components.Service
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.WindowManager
import com.intellij.util.Alarm
import com.copilotcompanion.rider.settings.CompanionSettings
import com.sun.jna.platform.win32.WinDef.HWND
import java.io.File
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Per-project companion session: launches VS Code, tracks its window,
 * arranges the layout and mirrors the active file.
 *
 * All heavy work runs on pooled threads; nothing here may block the EDT.
 */
@Service(Service.Level.PROJECT)
class CompanionSessionService(private val project: Project) : Disposable {

    private val log = Logger.getInstance(CompanionSessionService::class.java)

    @Volatile
    var companionHwnd: HWND? = null
        private set

    @Volatile
    var isActive: Boolean = false
        private set

    /** Runtime toggle, seeded from the application-level default on first launch. */
    @Volatile
    var fileSyncEnabled: Boolean = CompanionSettings.getInstance().state.fileSyncEnabled

    private val launching = AtomicBoolean(false)
    private val syncAlarm = Alarm(Alarm.ThreadToUse.POOLED_THREAD, this)

    // ------------------------------------------------------------------ open

    fun openCompanion() {
        val root = project.basePath?.let(::File)
        if (root == null || !root.isDirectory) {
            CompanionNotifier.warn(project, "No solution/project folder is open — cannot launch the companion window.")
            return
        }
        if (!launching.compareAndSet(false, true)) {
            log.info("Companion launch already in progress; ignoring")
            return
        }
        // Grab the Rider frame HWND on the EDT before going async; Swing must not be
        // touched from a pooled thread.
        val riderFrame = WindowManager.getInstance().getFrame(project)
        val riderHwnd = riderFrame?.let(Win32Windows::hwndOf)

        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                launchAndArrange(root, riderHwnd)
            } catch (e: Throwable) {
                // Never let anything escape into the IDE.
                log.error("Copilot Companion launch failed", e)
                CompanionNotifier.error(project, "Failed to launch the companion window: ${e.message}")
            } finally {
                launching.set(false)
            }
        }
    }

    private fun launchAndArrange(root: File, riderHwnd: HWND?) {
        val settings = CompanionSettings.getInstance().state

        if (!VsCodeSettingsMerger.merge(root)) {
            CompanionNotifier.warn(
                project,
                "Could not update ${root.name}/.vscode/settings.json (kept as-is); launching anyway."
            )
        }

        val code = VsCodeCli.resolve()
        if (code == null) {
            CompanionNotifier.error(
                project,
                "VS Code CLI not found. Install VS Code from ${VsCodeCli.DOWNLOAD_URL} " +
                    "or set the executable path in Settings | Tools | Copilot Companion."
            )
            return
        }

        if (!VsCodeCli.run(code, "-n", root.absolutePath)) {
            CompanionNotifier.error(project, "Failed to launch VS Code for ${root.absolutePath}.")
            return
        }

        // Wait up to 15 s for the new window. We match on folder name AND product name
        // so Rider's own title (which also contains the project name) never matches.
        var codeHwnd: HWND? = null
        if (Win32Windows.isSupported) {
            val deadline = System.currentTimeMillis() + 15_000
            while (System.currentTimeMillis() < deadline) {
                codeHwnd = Win32Windows.findWindow(root.name, "Visual Studio Code")
                if (codeHwnd != null) break
                Thread.sleep(500)
            }
            if (codeHwnd == null) {
                log.warn("VS Code window did not appear within 15 s; skipping arrangement")
            }
        } else if (MacWindows.isSupported) {
            if (!MacWindows.waitForVsCodeWindow(15_000)) {
                log.warn("VS Code window did not appear within 15 s; arrangement may target the wrong window")
            }
        } else {
            log.info("Window arrangement is not supported on this OS; skipping")
        }

        companionHwnd = codeHwnd
        isActive = true
        fileSyncEnabled = settings.fileSyncEnabled

        if (settings.autoOpenChat) {
            // Older VS Code versions have no `chat` subcommand — a non-zero exit is fine to ignore.
            VsCodeCli.run(code, "chat", "--reuse-window", "")
        }

        if (codeHwnd != null && riderHwnd != null) {
            Win32Windows.arrangeSideBySide(codeHwnd, riderHwnd, settings.splitRatio)
        } else if (MacWindows.isSupported) {
            if (!MacWindows.arrange(project, settings.splitRatio)) {
                CompanionNotifier.warn(
                    project,
                    "Could not arrange the windows. Grant Rider the Accessibility permission in " +
                        "System Settings | Privacy & Security | Accessibility, then run the command again."
                )
            }
        }

        CompanionNotifier.info(project, "Copilot companion window is ready.")
    }

    // --------------------------------------------------------------- restore

    fun restoreLayout() {
        val riderFrame = WindowManager.getInstance().getFrame(project)
        val riderHwnd = riderFrame?.let(Win32Windows::hwndOf)
        val codeHwnd = companionHwnd

        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                if (MacWindows.isSupported) {
                    MacWindows.restore(project)
                    return@executeOnPooledThread
                }
                if (riderHwnd != null) Win32Windows.maximize(riderHwnd)
                if (codeHwnd != null && Win32Windows.isWindow(codeHwnd)) {
                    Win32Windows.minimize(codeHwnd)
                }
            } catch (e: Throwable) {
                log.warn("Restore layout failed", e)
            }
        }
    }

    // ------------------------------------------------------------- file sync

    /**
     * Debounced (500 ms) push of the active file + caret line into the companion window.
     * Called from the EDT by [com.copilotcompanion.rider.sync.FileSyncListener].
     */
    fun scheduleSync(filePath: String, line: Int) {
        if (!isActive || !fileSyncEnabled) return
        syncAlarm.cancelAllRequests()
        syncAlarm.addRequest({
            try {
                val code = VsCodeCli.resolve() ?: return@addRequest
                VsCodeCli.run(code, "--reuse-window", "--goto", "$filePath:$line")
            } catch (e: Throwable) {
                log.warn("File sync failed", e)
            }
        }, 500)
    }

    override fun dispose() {
        isActive = false
        companionHwnd = null
    }

    companion object {
        fun getInstance(project: Project): CompanionSessionService =
            project.getService(CompanionSessionService::class.java)
    }
}
