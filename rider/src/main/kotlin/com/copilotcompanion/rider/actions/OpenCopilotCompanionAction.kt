package com.copilotcompanion.rider.actions

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.copilotcompanion.rider.CompanionNotifier
import com.copilotcompanion.rider.CompanionSessionService

class OpenCopilotCompanionAction : AnAction() {

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project
        if (project == null) {
            CompanionNotifier.warn(null, "Open a solution first, then launch the companion window.")
            return
        }
        CompanionSessionService.getInstance(project).openCompanion()
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabled = e.project != null
    }

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT
}
