pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "phone-control-agent"

include(
    ":app",
    ":core-domain",
    ":core-data",
    ":core-network",
    ":core-permissions",
    ":core-logging",
    ":feature-screen-capture",
    ":feature-accessibility",
    ":feature-notifications",
    ":feature-sms",
    ":feature-clipboard",
    ":feature-device-status",
    ":feature-pairing",
    ":feature-settings"
)
