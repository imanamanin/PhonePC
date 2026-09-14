package com.phonecontrol.agent.logging

/**
 * Never pass SMS bodies, notification text, clipboard text, or pairing codes.
 */
class RedactingLogger {
    fun info(event: String) {
        require(!containsSensitiveHint(event)) { "refusing to log sensitive event name" }
    }

    companion object {
        fun containsSensitiveHint(value: String): Boolean {
            val lower = value.lowercase()
            return listOf(
                "sms-body",
                "clipboard-text",
                "notification-text",
                "pairing-code",
                "pairingcode",
                "session-token",
                "sessiontoken",
                "password"
            )
                .any { lower.contains(it) }
        }
    }
}
