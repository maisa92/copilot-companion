package com.copilotcompanion.rider.toolwindow

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.project.DumbAware
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.Disposer
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowFactory
import com.intellij.ui.components.JBLabel
import com.intellij.ui.content.ContentFactory
import com.intellij.ui.jcef.JBCefApp
import com.intellij.ui.jcef.JBCefBrowser
import org.cef.browser.CefBrowser
import org.cef.browser.CefFrame
import org.cef.handler.CefLoadHandler
import org.cef.handler.CefLoadHandlerAdapter
import java.awt.BorderLayout
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import javax.swing.JPanel
import javax.swing.SwingConstants

/**
 * "Copilot Companion" tool window: embeds the full VS Code UI (served locally by
 * `code serve-web`) in a JCEF browser docked inside Rider — like the AI Assistant
 * panel, but it is the real VS Code with Copilot Chat.
 *
 * Self-healing: if the page fails to load (server not started yet, or it died),
 * the server is (re)started and the page reloaded automatically.
 */
class CompanionToolWindowFactory : ToolWindowFactory, DumbAware {

    override fun createToolWindowContent(project: Project, toolWindow: ToolWindow) {
        val panel = JPanel(BorderLayout())
        val status = JBLabel("Starting embedded VS Code…", SwingConstants.CENTER)
        panel.add(status, BorderLayout.CENTER)

        val content = ContentFactory.getInstance().createContent(panel, "", false)
        toolWindow.contentManager.addContent(content)

        if (!JBCefApp.isSupported()) {
            status.text = "This Rider runtime has no JCEF browser support; " +
                "use Tools | Open Copilot Companion instead."
            return
        }

        val browser = JBCefBrowser()
        Disposer.register(content, browser)

        val service = ServeWebService.getInstance(project)
        val consecutiveErrors = AtomicInteger(0)

        fun showBrowser() {
            panel.removeAll()
            panel.add(browser.component, BorderLayout.CENTER)
            panel.revalidate()
            panel.repaint()
        }

        fun showStatus(message: String) {
            panel.removeAll()
            status.text = message
            panel.add(status, BorderLayout.CENTER)
            panel.revalidate()
            panel.repaint()
        }

        fun startAndLoad() {
            service.ensureStarted(
                onReady = { url ->
                    ApplicationManager.getApplication().invokeLater {
                        showBrowser()
                        browser.loadURL(url)
                    }
                },
                onError = { message ->
                    ApplicationManager.getApplication().invokeLater { showStatus(message) }
                }
            )
        }

        browser.jbCefClient.addLoadHandler(object : CefLoadHandlerAdapter() {
            override fun onLoadError(
                cefBrowser: CefBrowser,
                frame: CefFrame,
                errorCode: CefLoadHandler.ErrorCode,
                errorText: String,
                failedUrl: String
            ) {
                if (!frame.isMain) return
                if (consecutiveErrors.incrementAndGet() > MAX_RETRIES) {
                    ApplicationManager.getApplication().invokeLater {
                        showStatus(
                            "Embedded VS Code is not reachable ($errorText). " +
                                "Close and reopen the tool window to retry."
                        )
                    }
                    return
                }
                // Server not up (yet) — restart it and reload once it answers.
                ApplicationManager.getApplication().executeOnPooledThread {
                    TimeUnit.SECONDS.sleep(2)
                    startAndLoad()
                }
            }

            override fun onLoadEnd(cefBrowser: CefBrowser, frame: CefFrame, httpStatusCode: Int) {
                if (!frame.isMain || httpStatusCode !in 200..399) return
                consecutiveErrors.set(0)
                // Chat-only layout: the workbench restores whatever layout the user last
                // had, so enforce it every time — maximize the chat (secondary side bar)
                // and hide the explorer.
                cefBrowser.executeJavaScript(CHAT_ONLY_JS, cefBrowser.url, 0)
            }
        }, browser.cefBrowser)

        startAndLoad()
    }

    private companion object {
        const val MAX_RETRIES = 10

        /**
         * Runs inside the embedded workbench once it has rendered. Polls until the
         * auxiliary bar (chat) exists, then clicks its "Maximize" title button; if that
         * button isn't found, falls back to hiding the primary side bar with Cmd/Ctrl+B.
         * Harmless no-op when the layout is already chat-only.
         */
        // language=JavaScript
        val CHAT_ONLY_JS = """
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
            })();
        """.trimIndent()
    }
}
