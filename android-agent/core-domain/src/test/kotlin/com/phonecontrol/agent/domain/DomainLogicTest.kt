package com.phonecontrol.agent.domain

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class CommandProcessorTest {
    private val processor = CommandProcessor()

    @Test
    fun knownTouchCommandIsAccepted() {
        val result = processor.accept(
            Envelope(type = "input.touch", requestId = "11111111-1111-1111-1111-111111111111", timestamp = 0)
        )
        assertEquals(null, result.error)
    }

    @Test
    fun unknownCommandIsRejected() {
        val result = processor.accept(
            Envelope(type = "shell.root", requestId = "11111111-1111-1111-1111-111111111111", timestamp = 0)
        )
        assertEquals("UNKNOWN_TYPE", result.error?.code)
    }

    @Test
    fun knownVideoCommandIsAccepted() {
        val result = processor.accept(
            Envelope(type = "video.start", requestId = "11111111-1111-1111-1111-111111111111", timestamp = 0)
        )
        assertEquals(null, result.error)
    }
}

class ClipboardLoopGuardTest {
    @Test
    fun dropsEchoOfSentHash() {
        val guard = ClipboardLoopGuard()
        guard.markSent("aaa")
        assertFalse(guard.shouldApplyIncoming("windows", "aaa", "android"))
    }

    @Test
    fun appliesNewRemoteHash() {
        val guard = ClipboardLoopGuard()
        assertTrue(guard.shouldApplyIncoming("windows", "bbb", "android"))
    }
}

class PairingCodeTest {
    @Test
    fun expires() {
        val code = PairingCode("123456", 100)
        assertTrue(code.isExpired(100))
        assertFalse(code.isExpired(99))
    }
}

class VideoPacketTest {
    @Test
    fun roundTripHeaderAndPayload() {
        val original = VideoPacket(
            codec = VideoPacket.H264,
            width = 720,
            height = 1280,
            captureTimestampMs = 42,
            flags = VideoPacket.KEYFRAME or VideoPacket.CONFIG,
            payload = byteArrayOf(0, 0, 0, 1, 103)
        )
        val decoded = VideoPacket.decode(VideoPacket.encode(original))
        assertEquals(720, decoded.width)
        assertEquals(1280, decoded.height)
        assertEquals(42L, decoded.captureTimestampMs)
        assertEquals(VideoPacket.H264, decoded.codec)
        assertEquals(VideoPacket.KEYFRAME or VideoPacket.CONFIG, decoded.flags)
        assertTrue(decoded.payload.contentEquals(original.payload))
    }
}

class LengthPrefixedTest {
    @Test
    fun writesAndReadsBody() {
        val stream = java.io.ByteArrayOutputStream()
        LengthPrefixed.write(stream, byteArrayOf(9, 8, 7), ProtocolPorts.MAX_JSON_BYTES)
        val input = java.io.ByteArrayInputStream(stream.toByteArray())
        val body = LengthPrefixed.read(input, ProtocolPorts.MAX_JSON_BYTES)
        assertTrue(body.contentEquals(byteArrayOf(9, 8, 7)))
    }
}

class JsonLiteWindowsEnvelopeTest {
    @Test
    fun parsesWindowsPairingSubmit() {
        val json =
            """{"version":1,"type":"pairing.submit","requestId":"d3c1a8e0-1b2a-4c3d-9e0f-123456789abc","timestamp":1726330000000,"payload":{"pin":"123456"}}"""
        val envelope = JsonLite.parseEnvelope(json)
        assertEquals("pairing.submit", envelope.type)
        assertEquals("d3c1a8e0-1b2a-4c3d-9e0f-123456789abc", envelope.requestId)
        assertEquals("123456", JsonLite.stringField(envelope.payloadJson!!, "pin"))
    }

    @Test
    fun unicodeEscapesBecomeLetters() {
        assertEquals("سلام", JsonLite.stringField("{\"text\":\"\\u0633\\u0644\\u0627\\u0645\"}", "text"))
        assertEquals("سلام", JsonLite.stringField("{\"text\":\"سلام\"}", "text"))
    }
}

class AgentCommandRouterTest {
    @Test
    fun videoStartWithoutCaptureIsDenied() {
        val router = AgentCommandRouter(FakeSink(granted = false, inject = true))
        val result = router.handle(
            Envelope(
                type = "video.start",
                requestId = "11111111-1111-1111-1111-111111111111",
                timestamp = 0,
                payloadJson = """{"codec":"h264","maxFps":30,"maxWidth":1280,"bitrateKbps":4000,"port":17891}"""
            )
        )
        assertEquals("PERMISSION_DENIED", result.error?.code)
    }

    @Test
    fun videoStartWithCaptureIsOk() {
        val router = AgentCommandRouter(FakeSink(granted = true, inject = true))
        val result = router.handle(
            Envelope(type = "video.start", requestId = "11111111-1111-1111-1111-111111111111", timestamp = 0)
        )
        assertEquals(null, result.error)
    }

    @Test
    fun helloAckKeepsRequestId() {
        val router = AgentCommandRouter(FakeSink(granted = false, inject = true, paired = false))
        val result = router.handle(
            Envelope(type = "session.hello", requestId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", timestamp = 1)
        )
        assertEquals("session.hello_ack", result.type)
        assertEquals("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", result.requestId)
        assertTrue(result.payloadJson!!.contains("\"pairingRequired\":true"))
    }

    @Test
    fun inputWithoutAccessibilityIsDenied() {
        val router = AgentCommandRouter(FakeSink(granted = true, inject = false))
        val result = router.handle(
            Envelope(
                type = "input.touch",
                requestId = "11111111-1111-1111-1111-111111111111",
                timestamp = 0,
                payloadJson = """{"action":"tap","x":10,"y":20}"""
            )
        )
        assertEquals("PERMISSION_DENIED", result.error?.code)
    }

    @Test
    fun videoStartWhenUnpairedIsRejected() {
        val router = AgentCommandRouter(FakeSink(granted = true, inject = true, paired = false))
        val result = router.handle(
            Envelope(type = "video.start", requestId = "11111111-1111-1111-1111-111111111111", timestamp = 0)
        )
        assertEquals("UNPAIRED", result.error?.code)
    }

    @Test
    fun typedTextIsInjected() {
        val router = AgentCommandRouter(FakeSink(granted = true, inject = true))
        val result = router.handle(
            Envelope(
                type = "input.key",
                requestId = "11111111-1111-1111-1111-111111111111",
                timestamp = 0,
                payloadJson = """{"action":"type","key":"type","text":"hi"}"""
            )
        )
        assertEquals(null, result.error)
    }
}

class EditorTextTest {
    @Test
    fun hintIsNotKeptAsTypedText() {
        assertEquals("", EditorText.visibleContent("Text message", "Text message", showingHint = true))
        assertEquals("", EditorText.visibleContent("Text message", "Text message", showingHint = false))
        assertEquals("hello", EditorText.visibleContent("Text messagehello", "Text message", showingHint = false))
        assertEquals("hi", EditorText.visibleContent("hi", "Text message", showingHint = false))
    }
}

class WebSocketHandshakeTest {
    @Test
    fun rfc6455SampleKey() {
        assertEquals(
            "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=",
            WebSocketHandshake.accept("dGhlIHNhbXBsZSBub25jZQ==")
        )
    }
}

class StreamHubTest {
    @Test
    fun publishesLatestVideoAndAudio() {
        StreamHub.clear()
        StreamHub.publishVideo(byteArrayOf(1, 2, 3), 720, 1600)
        StreamHub.publishAudio(byteArrayOf(0xA1.toByte(), 4))
        val snap = StreamHub.snapshot()
        assertEquals(720, snap.width)
        assertEquals(1600, snap.height)
        assertTrue(snap.video!!.contentEquals(byteArrayOf(1, 2, 3)))
        assertTrue(snap.audio!!.contentEquals(byteArrayOf(0xA1.toByte(), 4)))
        val heard = ArrayList<ByteArray>()
        val listener: (ByteArray) -> Unit = { heard.add(it) }
        StreamHub.addAudioListener(listener)
        StreamHub.publishAudio(byteArrayOf(7, 8))
        StreamHub.removeAudioListener(listener)
        assertEquals(1, heard.size)
        StreamHub.clear()
        assertEquals(null, StreamHub.snapshot().video)
    }
}

class AudioPacketTest {
    @Test
    fun roundTripPcm() {
        val original = AudioPacket(48000, 2, 16, 99, byteArrayOf(1, 2, 3, 4))
        val decoded = AudioPacket.decode(AudioPacket.encode(original))
        assertEquals(48000, decoded.sampleRate)
        assertEquals(2, decoded.channels)
        assertEquals(16, decoded.bitsPerSample)
        assertEquals(99L, decoded.captureTimestampMs)
        assertTrue(decoded.payload.contentEquals(original.payload))
    }
}

class PairingEngineTest {
    @Test
    fun pinMismatchIsRejected() {
        val engine = PairingEngine(nowMs = { 1_000L }, randomPin = { "123456" }, randomBytes = { ByteArray(it) { 7 } })
        engine.ensureChallenge()
        assertTrue(engine.submitPin("000000") is PinSubmitResult.Rejected)
    }

    @Test
    fun pinMatchIssuesTokenAndSas() {
        val engine = PairingEngine(nowMs = { 1_000L }, randomPin = { "123456" }, randomBytes = { ByteArray(it) { 7 } })
        engine.ensureChallenge()
        val result = engine.submitPin("123456") as PinSubmitResult.Accepted
        assertEquals(PairingEngine.sas(ByteArray(32) { 7 }), result.sas)
        val token = java.util.Base64.getEncoder().encodeToString(result.pairing.sessionToken)
        assertTrue(engine.validateToken(result.pairing.pairingId, token))
    }

    @Test
    fun expiredPinIsRejected() {
        var now = 0L
        val engine = PairingEngine(nowMs = { now }, randomPin = { "123456" }, pinTtlMs = 10)
        engine.ensureChallenge()
        now = 50
        assertTrue(engine.submitPin("123456") is PinSubmitResult.Expired)
    }

    @Test
    fun expiredTokenIsNotTrusted() {
        var now = 0L
        val engine = PairingEngine(nowMs = { now }, randomPin = { "123456" }, randomBytes = { ByteArray(it) { 1 } }, tokenTtlMs = 10)
        engine.ensureChallenge()
        val accepted = engine.submitPin("123456") as PinSubmitResult.Accepted
        now = 50
        val token = java.util.Base64.getEncoder().encodeToString(accepted.pairing.sessionToken)
        assertFalse(engine.validateToken(accepted.pairing.pairingId, token))
    }
}

private class FakeSink(
    private val granted: Boolean,
    private val inject: Boolean,
    private val paired: Boolean = true
) : AgentCommandSink {
    override fun hello() = HelloInfo("Pixel", "Google", "14", 34, "none")
    override fun evaluateHello(pairingId: String?, sessionToken: String?) = paired
    override fun submitPin(pin: String) = PinSubmitResult.Rejected
    override fun clearPairing() = Unit
    override fun isPaired() = paired
    override fun displayedPin() = "123456"
    override fun displayedSas() = null
    override fun deviceStatus() = DeviceStatusFields(80, true, "usb_tether")
    override fun isCaptureGranted() = granted
    override fun adapt(maxFps: Int, maxWidth: Int, bitrateKbps: Int) = Unit
    override fun requestStopCapture() = Unit
    override fun injectTouch(command: TouchCommand) = inject
    override fun injectKey(key: String) = inject
    override fun injectText(text: String) = inject
}
