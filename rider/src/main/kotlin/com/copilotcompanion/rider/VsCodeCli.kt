package com.copilotcompanion.rider

import com.intellij.execution.configurations.PathEnvironmentVariableUtil
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.util.SystemInfo
import com.copilotcompanion.rider.settings.CompanionSettings
import java.io.File
import java.util.concurrent.TimeUnit

/**
 * Locates and invokes the VS Code command-line interface.
 * All methods block and must be called from a pooled thread, never the EDT.
 */
object VsCodeCli {

    private val log = Logger.getInstance(VsCodeCli::class.java)

    const val DOWNLOAD_URL = "https://code.visualstudio.com/download"

    /**
     * Resolution order: explicit setting, PATH, then the default per-user
     * Windows install location. Returns null if nothing usable is found.
     */
    fun resolve(): String? {
        val configured = CompanionSettings.getInstance().state.codeExecutablePath.trim()
        if (configured.isNotEmpty()) {
            return if (File(configured).isFile) configured else null.also {
                log.warn("Configured VS Code path does not exist: $configured")
            }
        }

        // PathEnvironmentVariableUtil honors PATHEXT on Windows, so this finds code.cmd too.
        PathEnvironmentVariableUtil.findInPath("code")?.let { return it.absolutePath }

        if (SystemInfo.isWindows) {
            val localAppData = System.getenv("LOCALAPPDATA") ?: return null
            val root = File(localAppData, "Programs\\Microsoft VS Code")
            for (candidate in listOf(File(root, "bin\\code.cmd"), File(root, "Code.exe"))) {
                if (candidate.isFile) return candidate.absolutePath
            }
        }

        if (SystemInfo.isMac) {
            // Rider launched from the Dock/Finder does not inherit the shell PATH, so the
            // "Install 'code' command in PATH" symlink in /usr/local/bin may be invisible.
            for (candidate in listOf(
                File("/usr/local/bin/code"),
                File("/opt/homebrew/bin/code"),
                File("/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code"),
                File(System.getProperty("user.home"), "Applications/Visual Studio Code.app/Contents/Resources/app/bin/code")
            )) {
                if (candidate.isFile) return candidate.absolutePath
            }
        }
        return null
    }

    /** Full command line for [executable]; .cmd/.bat files are not directly executable by ProcessBuilder. */
    fun command(executable: String, vararg args: String): List<String> =
        if (SystemInfo.isWindows && (executable.endsWith(".cmd", true) || executable.endsWith(".bat", true))) {
            listOf("cmd.exe", "/c", executable) + args
        } else {
            listOf(executable) + args
        }

    /**
     * Runs the CLI with [args] and waits up to [timeoutSeconds] for it to exit
     * (the `code` launcher forks the real process and returns quickly).
     * Returns true if the process exited with code 0.
     */
    fun run(executable: String, vararg args: String, timeoutSeconds: Long = 30): Boolean {
        return try {
            val command = command(executable, *args)
            log.info("Running: ${command.joinToString(" ")}")
            val process = ProcessBuilder(command)
                .redirectErrorStream(true)
                .start()
            if (!process.waitFor(timeoutSeconds, TimeUnit.SECONDS)) {
                process.destroy()
                log.warn("VS Code CLI did not exit within ${timeoutSeconds}s: ${command.joinToString(" ")}")
                return false
            }
            process.exitValue() == 0
        } catch (e: Exception) {
            log.warn("Failed to run VS Code CLI", e)
            false
        }
    }
}
