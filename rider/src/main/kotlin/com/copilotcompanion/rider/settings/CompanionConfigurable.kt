package com.copilotcompanion.rider.settings

import com.intellij.openapi.options.BoundConfigurable
import com.intellij.openapi.ui.DialogPanel
import com.intellij.ui.dsl.builder.bindIntText
import com.intellij.ui.dsl.builder.bindSelected
import com.intellij.ui.dsl.builder.bindText
import com.intellij.ui.dsl.builder.columns
import com.intellij.ui.dsl.builder.panel

/**
 * Settings page under Settings | Tools | Copilot Companion.
 */
class CompanionConfigurable : BoundConfigurable("Copilot Companion") {

    override fun createPanel(): DialogPanel {
        val state = CompanionSettings.getInstance().state
        return panel {
            row("Split ratio (% of screen for VS Code):") {
                intTextField(range = 10..90)
                    .bindIntText(state::splitRatio)
                    .columns(4)
            }
            row {
                checkBox("Open Copilot Chat automatically on launch")
                    .bindSelected(state::autoOpenChat)
            }
            row {
                checkBox("Enable file sync by default")
                    .bindSelected(state::fileSyncEnabled)
                    .comment("Mirrors the active file and caret line into the companion window. Can be toggled per project via Tools | Toggle Copilot File Sync.")
            }
            row("VS Code executable (optional):") {
                textField()
                    .bindText(state::codeExecutablePath)
                    .columns(40)
                    .comment("Leave empty to resolve 'code' from PATH or the default install location.")
            }
        }
    }
}
