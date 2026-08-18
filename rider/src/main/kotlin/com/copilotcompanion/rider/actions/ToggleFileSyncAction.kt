package com.copilotcompanion.rider.actions

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.ToggleAction
import com.copilotcompanion.rider.CompanionSessionService

class ToggleFileSyncAction : ToggleAction() {

    override fun isSelected(e: AnActionEvent): Boolean {
        val project = e.project ?: return false
        return CompanionSessionService.getInstance(project).fileSyncEnabled
    }

    override fun setSelected(e: AnActionEvent, state: Boolean) {
        val project = e.project ?: return
        CompanionSessionService.getInstance(project).fileSyncEnabled = state
    }

    override fun update(e: AnActionEvent) {
        super.update(e)
        e.presentation.isEnabled = e.project != null
    }

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT
}
