package com.copilotcompanion.rider.toolwindow

import com.copilotcompanion.rider.VsCodeCli
import com.copilotcompanion.rider.VsCodeSettingsMerger
import com.intellij.openapi.Disposable
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.components.Service
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.project.Project
import java.net.HttpURLConnection
import java.net.URI
import java.net.URLEncoder
import java.nio.charset.StandardCharsets
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Runs `code serve-web` — VS Code's built-in web server — so the full VS Code UI
 * (including Copilot Chat) can be embedded in a JCEF browser inside Rider.
 *
 * The server is shared: if something already answers on the port (e.g. a serve-web
 * instance from another project), it is reused instead of spawning a second one.
 */
@Service(Service.Level.PROJECT)
class ServeWebService(private val project: Project) : Disposable {

    private val log = Logger.getInstance(ServeWebService::class.java)

    @Volatile
    private var process: Process? = null
    private val starting = AtomicBoolean(false)

    /** URL of the embedded VS Code opened on this project's root folder. */
    fun projectUrl(): String {
        val folder = URLEncoder.encode(project.basePath ?: "", StandardCharsets.UTF_8)
        return "http://$HOST:$PORT/?folder=$folder"
    }

    /**
     * Ensures the server is running, then calls [onReady] (with [projectUrl]) or
     * [onError] — both on a pooled thread; callers do their own `invokeLater`.
     */
    fun ensureStarted(onReady: (String) -> Unit, onError: (String) -> Unit) {
        if (!starting.compareAndSet(false, true)) return
        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                // Chat-first minimal-UI profile for the embedded workbench (merged,
                // never overwriting existing user settings).
                project.basePath?.let { VsCodeSettingsMerger.merge(java.io.File(it)) }
                if (isUp()) {
                    onReady(projectUrl())
                    return@executeOnPooledThread
                }
                val code = VsCodeCli.resolve()
                if (code == null) {
                    onError("VS Code CLI not found. Install VS Code from ${VsCodeCli.DOWNLOAD_URL}.")
                    return@executeOnPooledThread
                }
                val serveWeb = VsCodeCli.command(
                    code, "serve-web",
                    "--host", HOST,
                    "--port", PORT.toString(),
                    // Localhost-only + embedded browser: a connection token would just
                    // break the iframe-style embedding.
                    "--without-connection-token",
                    "--accept-server-license-terms"
                )
                // On macOS/Linux, an IDE launched from the Dock/desktop has a minimal
                // environment; run the server through a login shell so terminals, Copilot
                // tools, and hooks inside it inherit the user's full PATH.
                val command = if (com.intellij.openapi.util.SystemInfo.isWindows) {
                    serveWeb
                } else {
                    val shell = System.getenv("SHELL")?.takeIf { it.isNotBlank() } ?: "/bin/zsh"
                    val quoted = serveWeb.joinToString(" ") { "'" + it.replace("'", "'\\''") + "'" }
                    listOf(shell, "-lc", "exec $quoted")
                }
                process = ProcessBuilder(command).redirectErrorStream(true).start()

                // First launch downloads the VS Code server bundle — allow up to 90 s.
                val deadline = System.currentTimeMillis() + 90_000
                while (System.currentTimeMillis() < deadline) {
                    if (isUp()) {
                        onReady(projectUrl())
                        return@executeOnPooledThread
                    }
                    if (process?.isAlive != true) break
                    Thread.sleep(500)
                }
                onError(
                    "VS Code web server did not start. Your VS Code version may not support " +
                        "`code serve-web` — update VS Code and try again."
                )
            } catch (e: Throwable) {
                log.warn("serve-web failed", e)
                onError("Failed to start the VS Code web server: ${e.message}")
            } finally {
                starting.set(false)
            }
        }
    }

    private fun isUp(): Boolean = try {
        val conn = URI("http://$HOST:$PORT/").toURL().openConnection() as HttpURLConnection
        conn.connectTimeout = 1000
        conn.readTimeout = 1000
        conn.requestMethod = "GET"
        conn.responseCode in 200..399
    } catch (_: Throwable) {
        false
    }

    override fun dispose() {
        process?.destroy()
        process = null
    }

    companion object {
        private const val HOST = "127.0.0.1"
        private const val PORT = 8384

        fun getInstance(project: Project): ServeWebService =
            project.getService(ServeWebService::class.java)
    }
}
