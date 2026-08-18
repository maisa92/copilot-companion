package com.copilotcompanion.rider

import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.project.Project

/**
 * Balloon notifications for user-facing messages; safe to call from any thread.
 */
object CompanionNotifier {

    private const val GROUP_ID = "Copilot Companion"

    fun info(project: Project?, message: String) = notify(project, message, NotificationType.INFORMATION)

    fun warn(project: Project?, message: String) = notify(project, message, NotificationType.WARNING)

    fun error(project: Project?, message: String) = notify(project, message, NotificationType.ERROR)

    private fun notify(project: Project?, message: String, type: NotificationType) {
        ApplicationManager.getApplication().invokeLater {
            NotificationGroupManager.getInstance()
                .getNotificationGroup(GROUP_ID)
                .createNotification("Copilot Companion", message, type)
                .notify(project)
        }
    }
}
