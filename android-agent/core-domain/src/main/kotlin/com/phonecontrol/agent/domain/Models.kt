package com.phonecontrol.agent.domain

object ProtocolPorts {
    const val CONTROL = 17890
    const val SCREEN = 17891
    const val AUDIO = 17892
    const val VERSION = 1
    const val HEARTBEAT_MS = 5000
    const val TIMEOUT_MS = 15000
    const val MAX_JSON_BYTES = 1_048_576
    const val MAX_VIDEO_BYTES = 8_388_608
}

enum class ConnectionState {
    Disconnected,
    Connecting,
    PairingRequired,
    Connected,
    Reconnecting,
    Error
}

enum class PermissionState {
    Unknown,
    Granted,
    Denied,
    Unsupported
}

data class ProtocolError(
    val code: String,
    val message: String,
    val retryable: Boolean
)

data class Envelope(
    val version: Int = ProtocolPorts.VERSION,
    val type: String,
    val requestId: String,
    val timestamp: Long,
    val payloadJson: String? = null,
    val error: ProtocolError? = null
)
