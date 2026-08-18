package com.copilotcompanion.rider.sync

import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.fileEditor.FileEditorManagerEvent
import com.intellij.openapi.fileEditor.FileEditorManagerListener
import com.copilotcompanion.rider.CompanionSessionService

/**
 * Mirrors editor selection changes into the companion VS Code window.
 * Registered as a project listener in plugin.xml; [selectionChanged] runs on the EDT.
 */
class FileSyncListener : FileEditorManagerListener {

    private val log = Logger.getInstance(FileSyncListener::class.java)

    override fun selectionChanged(event: FileEditorManagerEvent) {
        try {
            val project = event.manager.project
            val session = CompanionSessionService.getInstance(project)
            if (!session.isActive || !session.fileSyncEnabled) return

            val file = event.newFile ?: return
            if (!file.isInLocalFileSystem) return

            // Caret line of the newly selected text editor; 1-based for `code --goto`.
            val editor = event.manager.selectedTextEditor
            val line = if (editor != null) editor.caretModel.logicalPosition.line + 1 else 1

            session.scheduleSync(file.path, line)
        } catch (e: Throwable) {
            // File sync is best-effort; never disturb the IDE.
            log.warn("File sync listener failed", e)
        }
    }
}
