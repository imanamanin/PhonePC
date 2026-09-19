package com.phonecontrol.agent.domain

import java.util.concurrent.CopyOnWriteArrayList

/**
 * Fan-out of unsolicited control envelopes (file transfer) to live PC sessions.
 */
object ControlPush {
    private val listeners = CopyOnWriteArrayList<(Envelope) -> Unit>()

    fun addListener(listener: (Envelope) -> Unit) {
        listeners.add(listener)
    }

    fun removeListener(listener: (Envelope) -> Unit) {
        listeners.remove(listener)
    }

    fun hasListeners(): Boolean = listeners.isNotEmpty()

    fun send(envelope: Envelope) {
        for (listener in listeners) {
            try {
                listener(envelope)
            } catch (_: Exception) {
                // Dead socket; session loop detaches itself.
            }
        }
    }

    fun envelope(type: String, payloadJson: String): Envelope = Envelope(
        type = type,
        requestId = java.util.UUID.randomUUID().toString(),
        timestamp = System.currentTimeMillis(),
        payloadJson = payloadJson
    )
}
