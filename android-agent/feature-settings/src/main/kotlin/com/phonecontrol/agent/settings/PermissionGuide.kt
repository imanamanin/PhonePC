package com.phonecontrol.agent.settings

data class PermissionGuideItem(
    val id: String,
    val title: String,
    val settingsAction: String
)

class PermissionGuide {
    fun items(): List<PermissionGuideItem> = listOf(
        PermissionGuideItem("accessibility", "Accessibility", "android.settings.ACCESSIBILITY_SETTINGS"),
        PermissionGuideItem("notifications", "Notification access", "android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS"),
        PermissionGuideItem("capture", "Screen capture (system dialog, not ADB)", "runtime"),
        PermissionGuideItem("audio", "Microphone permission (needed to copy phone playback to the PC)", "runtime"),
        PermissionGuideItem("sms", "SMS", "runtime")
    )
}
