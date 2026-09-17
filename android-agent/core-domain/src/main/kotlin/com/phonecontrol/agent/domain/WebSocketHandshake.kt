package com.phonecontrol.agent.domain

import java.security.MessageDigest
import java.util.Base64

object WebSocketHandshake {
    const val GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"

    fun accept(secWebSocketKey: String): String {
        val md = MessageDigest.getInstance("SHA-1")
        val digest = md.digest((secWebSocketKey.trim() + GUID).toByteArray(Charsets.US_ASCII))
        return Base64.getEncoder().encodeToString(digest)
    }
}
