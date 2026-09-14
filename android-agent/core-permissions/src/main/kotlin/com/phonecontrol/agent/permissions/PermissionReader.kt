package com.phonecontrol.agent.permissions

import com.phonecontrol.agent.domain.PermissionState

data class AgentPermissionSnapshot(
    val accessibility: PermissionState = PermissionState.Unknown,
    val notificationListener: PermissionState = PermissionState.Unknown,
    val mediaProjection: PermissionState = PermissionState.Unknown,
    val smsRead: PermissionState = PermissionState.Unknown,
    val smsSend: PermissionState = PermissionState.Unknown,
    val postNotifications: PermissionState = PermissionState.Unknown
)

interface PermissionReader {
    fun snapshot(): AgentPermissionSnapshot
}

class StaticPermissionReader(
    private val value: AgentPermissionSnapshot = AgentPermissionSnapshot()
) : PermissionReader {
    override fun snapshot(): AgentPermissionSnapshot = value
}
