package com.phonecontrol.agent.permissions

class PermissionManager(private val reader: PermissionReader) {
    fun snapshot() = reader.snapshot()
}
