package com.phonecontrol.agent.sms

import com.phonecontrol.agent.domain.PermissionState

data class SmsPreview(val id: String, val address: String, val bodyLength: Int, val outgoing: Boolean)

interface SmsAdapter {
    suspend fun list(max: Int): List<SmsPreview>
    suspend fun send(to: String, body: String)
}

class PermissionGatedSmsAdapter(
    private val readState: PermissionState,
    private val sendState: PermissionState
) : SmsAdapter {
    override suspend fun list(max: Int): List<SmsPreview> {
        if (readState != PermissionState.Granted) {
            error("SMS_PERMISSION_DENIED")
        }
        return emptyList()
    }

    override suspend fun send(to: String, body: String) {
        if (sendState != PermissionState.Granted) {
            error("SMS_PERMISSION_DENIED")
        }
        require(to.isNotBlank() && body.isNotBlank())
    }
}
