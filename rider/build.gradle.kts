plugins {
    id("java")
    kotlin("jvm") version "2.2.20"
    id("org.jetbrains.intellij.platform") version "2.18.1"
}

group = "com.copilotcompanion"
version = "1.5.0"

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        // Target Rider 2025.1. The Rider distribution bundles JNA (jna + jna-platform),
        // which we use for the Win32 window management — no extra runtime dependency.
        // useInstaller = false: the macOS Rider installer (.dmg) does not extract reliably
        // through Gradle's artifact transform; the plain zip distribution does.
        rider("2025.1.2") { useInstaller = false }
    }

    // Compile-only safety net so `com.sun.jna.platform.win32.*` always resolves at build
    // time regardless of which jars the resolved Rider distribution exposes on the compile
    // classpath. At runtime the IDE's bundled JNA is used; nothing is packaged into the plugin.
    compileOnly("net.java.dev.jna:jna:5.14.0")
    compileOnly("net.java.dev.jna:jna-platform:5.14.0")
}

kotlin {
    jvmToolchain(21)
}

intellijPlatform {
    pluginConfiguration {
        id = "com.copilotcompanion.rider"
        name = "Copilot Companion"
        version = project.version.toString()

        ideaVersion {
            sinceBuild = "251"
            // No untilBuild: stay compatible with future Rider builds until proven otherwise.
            untilBuild = provider { null }
        }
    }

    buildSearchableOptions = false
}
