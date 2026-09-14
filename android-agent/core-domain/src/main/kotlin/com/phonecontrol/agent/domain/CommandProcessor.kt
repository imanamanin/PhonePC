package com.phonecontrol.agent.domain

object MessageTypes {
    val ALL: Set<String> = setOf(
        "session.hello",
        "session.hello_ack",
        "session.ping",
        "session.pong",
        "session.heartbeat",
        "session.close",
        "session.reconnect",
        "pairing.start",
        "pairing.submit",
        "pairing.accepted",
        "pairing.rejected",
        "pairing.expired",
        "pairing.cleared",
        "pairing.confirm",
        "capabilities.get",
        "capabilities.status",
        "permissions.get",
        "permissions.status",
        "device.status",
        "state.update",
        "screen.start",
        "screen.stop",
        "screen.metadata",
        "video.start",
        "video.stop",
        "video.adapt",
        "video.frame",
        "input.touch",
        "input.key",
        "input.pinch",
        "input.screenshot",
        "notification.received",
        "notification.removed",
        "notification.updated",
        "notification.action",
        "clipboard.changed",
        "clipboard.set",
        "sms.list",
        "sms.received",
        "sms.send",
        "file.offer",
        "file.progress",
        "file.cancel",
        "file.complete",
        "file.error"
    )

    fun isKnown(type: String): Boolean = ALL.contains(type)
}

class UnknownCommandException(type: String) : IllegalArgumentException("UNKNOWN_TYPE:$type")

class CommandProcessor {
    fun accept(envelope: Envelope): Envelope {
        if (envelope.version < 1) {
            return error(envelope, "VALIDATION", "version must be >= 1", retryable = false)
        }
        if (!MessageTypes.isKnown(envelope.type)) {
            return error(envelope, "UNKNOWN_TYPE", "unsupported command", retryable = false)
        }
        return envelope.copy(error = null)
    }

    private fun error(source: Envelope, code: String, message: String, retryable: Boolean): Envelope {
        return source.copy(error = ProtocolError(code, message, retryable))
    }
}
