package com.copilotcompanion.rider

import com.google.gson.GsonBuilder
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.google.gson.JsonPrimitive
import com.intellij.openapi.diagnostic.Logger
import java.io.File

/**
 * Writes the minimal-UI profile into `.vscode/settings.json` of the target folder,
 * merging with any existing user settings instead of replacing them.
 *
 * Gson is used because it ships with the IntelliJ Platform — no extra dependency.
 */
object VsCodeSettingsMerger {

    private val log = Logger.getInstance(VsCodeSettingsMerger::class.java)

    private val PROFILE: Map<String, Any> = mapOf(
        "workbench.activityBar.location" to "hidden",
        "workbench.statusBar.visible" to false,
        "editor.minimap.enabled" to false,
        // Chat-first: open the Copilot Chat side bar maximized (covering explorer and
        // editor) so the companion looks like a chat panel, not a full IDE.
        "workbench.secondarySideBar.defaultVisibility" to "maximized",
        "workbench.startupEditor" to "none",
        "workbench.colorTheme" to "Default Dark Modern",
        // Fresh VS Code installs auto-detect the OS color scheme, which overrides
        // workbench.colorTheme entirely — turn it off so dark mode always wins.
        "window.autoDetectColorScheme" to false,
    )

    /**
     * Returns true on success. On a parse failure of an existing file the file is
     * left untouched (never destroy user settings) and false is returned.
     */
    fun merge(projectRoot: File): Boolean {
        return try {
            val vscodeDir = File(projectRoot, ".vscode")
            val settingsFile = File(vscodeDir, "settings.json")

            val root: JsonObject = if (settingsFile.isFile) {
                val parsed = try {
                    // Lenient mode tolerates trailing commas; JSONC comments will still
                    // fail to parse, in which case we bail out rather than clobber the file.
                    JsonParser.parseString(settingsFile.readText())
                } catch (e: Exception) {
                    log.warn("Could not parse existing ${settingsFile.path}; leaving it untouched", e)
                    return false
                }
                if (parsed.isJsonObject) parsed.asJsonObject else {
                    log.warn("${settingsFile.path} is not a JSON object; leaving it untouched")
                    return false
                }
            } else {
                JsonObject()
            }

            for ((key, value) in PROFILE) {
                when (value) {
                    is Boolean -> root.add(key, JsonPrimitive(value))
                    is String -> root.add(key, JsonPrimitive(value))
                    else -> error("Unsupported profile value type for $key")
                }
            }

            vscodeDir.mkdirs()
            val gson = GsonBuilder().setPrettyPrinting().disableHtmlEscaping().create()
            settingsFile.writeText(gson.toJson(root) + "\n")
            true
        } catch (e: Exception) {
            log.warn("Failed to write .vscode/settings.json", e)
            false
        }
    }
}
