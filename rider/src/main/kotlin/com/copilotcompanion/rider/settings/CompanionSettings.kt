package com.copilotcompanion.rider.settings

import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage
import com.intellij.openapi.components.service

/**
 * Application-level settings, persisted to copilotCompanion.xml in the IDE config directory.
 */
@Service(Service.Level.APP)
@State(name = "CopilotCompanionSettings", storages = [Storage("copilotCompanion.xml")])
class CompanionSettings : PersistentStateComponent<CompanionSettings.State> {

    class State {
        /** Width of the VS Code companion window, as a percentage of the monitor work area. */
        var splitRatio: Int = 30

        /** Open the Copilot Chat view right after launching the companion window. */
        var autoOpenChat: Boolean = true

        /** Default for the per-project file-sync toggle. */
        var fileSyncEnabled: Boolean = true

        /** Explicit path to the VS Code CLI; empty means "resolve automatically". */
        var codeExecutablePath: String = ""
    }

    private var state = State()

    override fun getState(): State = state

    override fun loadState(state: State) {
        this.state = state
    }

    companion object {
        fun getInstance(): CompanionSettings = service()
    }
}
