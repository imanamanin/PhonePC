package com.phonecontrol.agent.domain

data class HelloInfo(
    val model: String,
    val manufacturer: String,
    val androidVersion: String,
    val sdkInt: Int,
    val serialHash: String
)

data class DeviceStatusFields(
    val batteryPercent: Int?,
    val charging: Boolean?,
    val network: String,
    val screenWidth: Int? = null,
    val screenHeight: Int? = null,
    val rotation: Int = 0,
    val accessibilityGranted: Boolean? = null,
    val captureGranted: Boolean? = null
)

interface AgentCommandSink {
    fun hello(): HelloInfo
    fun evaluateHello(pairingId: String?, sessionToken: String?): Boolean
    fun autoGrant(): TrustedPairing
    fun submitPin(pin: String): PinSubmitResult
    fun clearPairing()
    fun isPaired(): Boolean
    fun displayedPin(): String?
    fun displayedSas(): String?
    fun persistPairingAsync(pairing: TrustedPairing) {}
    fun deviceStatus(): DeviceStatusFields
    fun isCaptureGranted(): Boolean
    fun adapt(maxFps: Int, maxWidth: Int, bitrateKbps: Int)
    fun requestStopCapture()
    fun injectTouch(command: TouchCommand): Boolean
    fun injectKey(key: String): Boolean
    fun injectText(text: String): Boolean = false
}

class AgentCommandRouter(
    private val sink: AgentCommandSink,
    private val processor: CommandProcessor = CommandProcessor()
) {
    fun handle(envelope: Envelope): Envelope {
        val accepted = processor.accept(envelope)
        if (accepted.error != null) {
            return accepted
        }

        if (!isPublic(envelope.type) && !sink.isPaired()) {
            return envelope.copy(
                error = ProtocolError("UNPAIRED", "pairing required", retryable = false)
            )
        }

        return when (envelope.type) {
            "session.hello" -> helloAck(envelope)
            "session.ping" -> pong(envelope)
            "session.heartbeat" -> envelope.copy(error = null)
            "pairing.submit" -> submitPin(envelope)
            "pairing.cleared" -> {
                sink.clearPairing()
                ok(envelope)
            }
            "pairing.start" -> {
                sink.displayedPin()
                ok(envelope)
            }
            "device.status", "state.update" -> status(envelope)
            "video.start" -> videoStart(envelope)
            "video.stop" -> {
                sink.requestStopCapture()
                ok(envelope)
            }
            "video.adapt" -> {
                val payload = envelope.payloadJson.orEmpty()
                sink.adapt(
                    JsonLite.intField(payload, "maxFps") ?: 24,
                    JsonLite.intField(payload, "maxWidth") ?: 720,
                    JsonLite.intField(payload, "bitrateKbps") ?: 2000
                )
                ok(envelope)
            }
            "input.touch" -> injectTouch(envelope)
            "input.pinch" -> injectPinch(envelope)
            "input.key" -> injectKey(envelope)
            else -> ok(envelope)
        }
    }

    private fun isPublic(type: String): Boolean =
        type.startsWith("session.") || type.startsWith("pairing.")

    private fun helloAck(envelope: Envelope): Envelope {
        val payload = envelope.payloadJson.orEmpty()
        sink.evaluateHello(
            JsonLite.stringField(payload, "pairingId"),
            JsonLite.stringField(payload, "sessionToken")
        )
        val pairing = sink.autoGrant()
        sink.persistPairingAsync(pairing)
        val token = java.util.Base64.getEncoder().encodeToString(pairing.sessionToken)
        val info = sink.hello()
        val json = """{"agentName":"PhoneControl.Agent","agentVersion":"0.1.0","protocol":1,"device":{"model":${JsonLite.quote(info.model)},"manufacturer":${JsonLite.quote(info.manufacturer)},"androidVersion":${JsonLite.quote(info.androidVersion)},"sdkInt":${info.sdkInt},"serialHash":${JsonLite.quote(info.serialHash)}},"pairingRequired":false,"easyConnect":true,"pairingId":${JsonLite.quote(pairing.pairingId)},"sessionToken":${JsonLite.quote(token)},"capabilities":["input.touch","input.key","input.pinch","video.h264","device.status"]}"""
        return envelope.copy(type = "session.hello_ack", payloadJson = json, error = null)
    }

    private fun submitPin(envelope: Envelope): Envelope {
        val pin = JsonLite.stringField(envelope.payloadJson.orEmpty(), "pin") ?: ""
        return when (val result = sink.submitPin(pin)) {
            is PinSubmitResult.Accepted -> {
                sink.persistPairingAsync(result.pairing)
                val token = java.util.Base64.getEncoder().encodeToString(result.pairing.sessionToken)
                val json = """{"pairingId":${JsonLite.quote(result.pairing.pairingId)},"sessionToken":${JsonLite.quote(token)},"expiresAt":${result.pairing.expiresAtEpochMs},"sas":${JsonLite.quote(result.sas)}}"""
                envelope.copy(type = "pairing.accepted", payloadJson = json, error = null)
            }
            PinSubmitResult.Expired -> envelope.copy(
                type = "pairing.expired",
                error = ProtocolError("TIMEOUT", "pairing pin expired", retryable = true)
            )
            PinSubmitResult.Locked -> envelope.copy(
                type = "pairing.rejected",
                payloadJson = """{"reason":"locked"}""",
                error = ProtocolError("UNPAIRED", "too many pin attempts", retryable = false)
            )
            PinSubmitResult.Rejected -> envelope.copy(
                type = "pairing.rejected",
                payloadJson = """{"reason":"pin_mismatch"}""",
                error = ProtocolError("UNPAIRED", "pin mismatch", retryable = true)
            )
        }
    }

    private fun status(envelope: Envelope): Envelope {
        val fields = sink.deviceStatus()
        val json = """{"batteryPercent":${fields.batteryPercent ?: "null"},"charging":${fields.charging ?: "null"},"network":${JsonLite.quote(fields.network)},"deviceTimeUtc":${System.currentTimeMillis()},"screenWidth":${fields.screenWidth ?: "null"},"screenHeight":${fields.screenHeight ?: "null"},"rotation":${fields.rotation},"accessibility":${fields.accessibilityGranted ?: "null"},"mediaProjection":${fields.captureGranted ?: "null"}}"""
        return envelope.copy(type = envelope.type, payloadJson = json, error = null)
    }

    private fun videoStart(envelope: Envelope): Envelope {
        if (!sink.isCaptureGranted()) {
            return envelope.copy(
                error = ProtocolError(
                    "PERMISSION_DENIED",
                    "MediaProjection is not granted. The user must accept the system capture dialog.",
                    retryable = true
                )
            )
        }
        val payload = envelope.payloadJson.orEmpty()
        sink.adapt(
            JsonLite.intField(payload, "maxFps") ?: 30,
            JsonLite.intField(payload, "maxWidth") ?: 1280,
            JsonLite.intField(payload, "bitrateKbps") ?: 4000
        )
        return ok(envelope)
    }

    private fun injectTouch(envelope: Envelope): Envelope {
        val payload = envelope.payloadJson.orEmpty()
        val command = TouchCommand(
            action = JsonLite.stringField(payload, "action") ?: "tap",
            x = JsonLite.doubleField(payload, "x")?.toFloat() ?: 0f,
            y = JsonLite.doubleField(payload, "y")?.toFloat() ?: 0f,
            x2 = JsonLite.doubleField(payload, "x2")?.toFloat(),
            y2 = JsonLite.doubleField(payload, "y2")?.toFloat(),
            durationMs = JsonLite.longField(payload, "durationMs") ?: 1L
        )
        return if (sink.injectTouch(command)) ok(envelope) else permission(envelope, "Accessibility is not enabled.")
    }

    private fun injectPinch(envelope: Envelope): Envelope {
        val payload = envelope.payloadJson.orEmpty()
        val command = TouchCommand(
            action = "pinch",
            x = JsonLite.doubleField(payload, "x")?.toFloat() ?: 0f,
            y = JsonLite.doubleField(payload, "y")?.toFloat() ?: 0f,
            x2 = JsonLite.doubleField(payload, "x2")?.toFloat(),
            y2 = JsonLite.doubleField(payload, "y2")?.toFloat(),
            scale = JsonLite.doubleField(payload, "scale")?.toFloat()
        )
        return if (sink.injectTouch(command)) ok(envelope) else permission(envelope, "Accessibility is not enabled.")
    }

    private fun injectKey(envelope: Envelope): Envelope {
        val payload = envelope.payloadJson.orEmpty()
        val key = JsonLite.stringField(payload, "key") ?: ""
        val text = JsonLite.stringField(payload, "text")
        val ok = when {
            !text.isNullOrEmpty() || key == "type" -> sink.injectText(text ?: "")
            else -> sink.injectKey(key)
        }
        return if (ok) ok(envelope) else permission(envelope, "Accessibility is not enabled.")
    }

    private fun pong(envelope: Envelope): Envelope {
        val t = JsonLite.longField(envelope.payloadJson.orEmpty(), "t") ?: envelope.timestamp
        return envelope.copy(type = "session.pong", payloadJson = """{"t":$t}""", error = null)
    }

    private fun ok(envelope: Envelope) = envelope.copy(error = null)

    private fun permission(envelope: Envelope, message: String) = envelope.copy(
        error = ProtocolError("PERMISSION_DENIED", message, retryable = true)
    )
}
