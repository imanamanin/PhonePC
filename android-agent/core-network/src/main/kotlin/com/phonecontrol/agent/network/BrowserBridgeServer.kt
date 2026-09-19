package com.phonecontrol.agent.network

import com.phonecontrol.agent.domain.AgentCommandRouter
import com.phonecontrol.agent.domain.AgentCommandSink
import com.phonecontrol.agent.domain.ClientPresence
import com.phonecontrol.agent.domain.ControlPush
import com.phonecontrol.agent.domain.JsonLite
import com.phonecontrol.agent.domain.LocalAddresses
import com.phonecontrol.agent.domain.ProtocolError
import com.phonecontrol.agent.domain.ProtocolPorts
import com.phonecontrol.agent.domain.StreamHub
import com.phonecontrol.agent.domain.WebSocketHandshake
import java.io.ByteArrayOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.concurrent.thread

/**
 * Chrome cannot open raw TCP 17890/17891. This HTTP/WebSocket bridge on 17893
 * reuses the same pairing and command router. USB tethering is unchanged.
 */
class BrowserBridgeServer(
    private val port: Int = ProtocolPorts.BROWSER
) {
    private val running = AtomicBoolean(false)
    private var server: ServerSocket? = null
    private var acceptThread: Thread? = null
    private var router: AgentCommandRouter? = null
    private var sink: AgentCommandSink? = null

    @Volatile
    var lastError: String? = null
        private set

    fun isListening(): Boolean {
        val socket = server
        return socket != null && socket.isBound && !socket.isClosed
    }

    @Synchronized
    fun start(commandSink: AgentCommandSink) {
        sink = commandSink
        router = AgentCommandRouter(commandSink)
        val existing = server
        if (existing != null && existing.isBound && !existing.isClosed) {
            return
        }
        try {
            existing?.close()
        } catch (_: Exception) {
            // Rebind.
        }
        running.set(true)
        lastError = null
        try {
            val socket = ServerSocket()
            socket.reuseAddress = true
            socket.soTimeout = 250
            try {
                socket.bind(InetSocketAddress("0.0.0.0", port), 16)
            } catch (_: Exception) {
                socket.bind(InetSocketAddress(port), 16)
            }
            server = socket
            acceptThread = thread(name = "browser-17893", isDaemon = false) { acceptLoop(socket) }
        } catch (ex: Exception) {
            running.set(false)
            server = null
            lastError = ex.javaClass.simpleName
        }
    }

    fun stop() {
        running.set(false)
        try {
            server?.close()
        } catch (_: Exception) {
            // Closing unblocks accept.
        }
        server = null
    }

    private fun acceptLoop(serverSocket: ServerSocket) {
        while (running.get()) {
            val client = try {
                serverSocket.accept()
            } catch (_: java.net.SocketTimeoutException) {
                continue
            } catch (_: Exception) {
                if (!running.get()) {
                    break
                }
                try {
                    Thread.sleep(250)
                } catch (_: InterruptedException) {
                    break
                }
                continue
            }
            thread(name = "browser-session", isDaemon = true) { session(client) }
        }
    }

    private fun session(socket: Socket) {
        socket.tcpNoDelay = true
        socket.use { client ->
            val input = client.getInputStream()
            val output = client.getOutputStream()
            val request = readHttp(input) ?: return
            if (request.method == "OPTIONS") {
                output.write(corsNoContent())
                return
            }
            if (request.isWebSocket) {
                val key = request.header("Sec-WebSocket-Key") ?: return
                output.write(switchingProtocols(WebSocketHandshake.accept(key)))
                output.flush()
                ClientPresence.enter()
                try {
                    websocket(client, input, output)
                } finally {
                    ClientPresence.leave()
                }
                return
            }
            if (request.method == "GET" && (request.path == "/" || request.path == "/health")) {
                output.write(jsonOk(healthJson()))
                return
            }
            output.write(jsonOk("""{"ok":false,"error":"not_found"}""", 404, "Not Found"))
        }
    }

    private fun websocket(socket: Socket, input: InputStream, output: OutputStream) {
        val lock = Any()
        val live = AtomicBoolean(true)
        val media = AtomicBoolean(false)
        fun send(opcode: Int, payload: ByteArray) {
            synchronized(lock) {
                if (!live.get()) return
                try {
                    WebSocketFrames.write(output, opcode, payload)
                } catch (_: Exception) {
                    live.set(false)
                }
            }
        }
        fun maybeMedia() {
            if (sink?.isPaired() != true || !media.compareAndSet(false, true)) {
                return
            }
            thread(name = "browser-media", isDaemon = true) {
                var sentVideo = 0L
                val audioListener: (ByteArray) -> Unit = { packet ->
                    send(WebSocketFrames.BINARY, packet)
                }
                StreamHub.addAudioListener(audioListener)
                try {
                    while (running.get() && live.get()) {
                        val snap = StreamHub.snapshot()
                        val video = snap.video
                        if (video != null && snap.videoSeq != sentVideo) {
                            send(WebSocketFrames.BINARY, video)
                            sentVideo = snap.videoSeq
                        } else {
                            try {
                                Thread.sleep(4)
                            } catch (_: InterruptedException) {
                                break
                            }
                        }
                    }
                } finally {
                    StreamHub.removeAudioListener(audioListener)
                }
            }
        }
        val push: (com.phonecontrol.agent.domain.Envelope) -> Unit = { envelope ->
            send(WebSocketFrames.TEXT, JsonLite.encodeEnvelope(envelope).encodeToByteArray())
        }
        ControlPush.addListener(push)
        try {
            while (running.get() && live.get()) {
                val frame = try {
                    WebSocketFrames.read(input, ProtocolPorts.MAX_JSON_BYTES)
                } catch (_: Exception) {
                    break
                } ?: break
                when (frame.opcode) {
                    WebSocketFrames.CLOSE -> {
                        send(WebSocketFrames.CLOSE, ByteArray(0))
                        break
                    }
                    WebSocketFrames.PING -> send(WebSocketFrames.PONG, frame.payload)
                    WebSocketFrames.PONG -> Unit
                    WebSocketFrames.TEXT -> {
                        val reply = handleText(frame.payload.decodeToString())
                        send(WebSocketFrames.TEXT, JsonLite.encodeEnvelope(reply).encodeToByteArray())
                        maybeMedia()
                    }
                    else -> Unit
                }
            }
        } finally {
            ControlPush.removeListener(push)
            live.set(false)
            try {
                socket.close()
            } catch (_: Exception) {
                // Already closed.
            }
        }
    }

    private fun handleText(body: String) = try {
        val envelope = JsonLite.parseEnvelope(body)
        router?.handle(envelope) ?: envelope
    } catch (_: Exception) {
        com.phonecontrol.agent.domain.Envelope(
            type = "session.close",
            requestId = "0",
            timestamp = System.currentTimeMillis(),
            error = ProtocolError("INTERNAL", "handler failed", retryable = true)
        )
    }

    private fun healthJson(): String {
        val ips = LocalAddresses.ipv4().joinToString(",") { "\"$it\"" }
        val wifi = LocalAddresses.wifi().firstOrNull()?.let { "\"$it\"" } ?: "null"
        val usb = LocalAddresses.usb().joinToString(",") { "\"$it\"" }
        return """{"ok":true,"name":"PC Phone","protocol":1,"easyConnect":true,"browserPort":$port,"controlPort":${ProtocolPorts.CONTROL},"screenPort":${ProtocolPorts.SCREEN},"wifi":$wifi,"usb":[$usb],"ipv4":[$ips]}"""
    }

    private fun readHttp(input: InputStream): HttpRequest? {
        val buf = ByteArrayOutputStream()
        while (buf.size() < 8192) {
            val b = input.read()
            if (b < 0) return null
            buf.write(b)
            val bytes = buf.toByteArray()
            if (bytes.size >= 4 &&
                bytes[bytes.size - 4] == 13.toByte() &&
                bytes[bytes.size - 3] == 10.toByte() &&
                bytes[bytes.size - 2] == 13.toByte() &&
                bytes[bytes.size - 1] == 10.toByte()
            ) {
                break
            }
        }
        return HttpRequest.parse(buf.toString(Charsets.US_ASCII))
    }

    private fun switchingProtocols(accept: String): ByteArray =
        ("HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Sec-WebSocket-Accept: $accept\r\n\r\n").encodeToByteArray()

    private fun corsNoContent(): ByteArray =
        ("HTTP/1.1 204 No Content\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Access-Control-Allow-Methods: GET, OPTIONS\r\n" +
            "Access-Control-Allow-Headers: *\r\n" +
            "Access-Control-Allow-Private-Network: true\r\n" +
            "Content-Length: 0\r\n\r\n").encodeToByteArray()

    private fun jsonOk(body: String, code: Int = 200, reason: String = "OK"): ByteArray {
        val bytes = body.encodeToByteArray()
        val head = "HTTP/1.1 $code $reason\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            "Content-Length: ${bytes.size}\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Access-Control-Allow-Private-Network: true\r\n" +
            "Connection: close\r\n\r\n"
        return head.encodeToByteArray() + bytes
    }

    private class HttpRequest(
        val method: String,
        val path: String,
        val headers: Map<String, String>
    ) {
        val isWebSocket: Boolean
            get() = header("Upgrade")?.equals("websocket", ignoreCase = true) == true &&
                header("Sec-WebSocket-Key") != null

        fun header(name: String): String? = headers[name.lowercase()]

        companion object {
            fun parse(raw: String): HttpRequest? {
                val lines = raw.split("\r\n")
                if (lines.isEmpty()) return null
                val parts = lines[0].split(" ")
                if (parts.size < 2) return null
                val headers = HashMap<String, String>()
                for (i in 1 until lines.size) {
                    val line = lines[i]
                    val colon = line.indexOf(':')
                    if (colon <= 0) continue
                    headers[line.substring(0, colon).trim().lowercase()] = line.substring(colon + 1).trim()
                }
                val path = parts[1].substringBefore('?')
                return HttpRequest(parts[0].uppercase(), path, headers)
            }
        }
    }

    companion object {
        val instance = BrowserBridgeServer()
    }
}
