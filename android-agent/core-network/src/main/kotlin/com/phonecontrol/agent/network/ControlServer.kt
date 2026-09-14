package com.phonecontrol.agent.network

import com.phonecontrol.agent.domain.AgentCommandRouter
import com.phonecontrol.agent.domain.AgentCommandSink
import com.phonecontrol.agent.domain.JsonLite
import com.phonecontrol.agent.domain.LengthPrefixed
import com.phonecontrol.agent.domain.ProtocolPorts
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.concurrent.thread

/**
 * TCP control listener on 17890. Does not change USB tethering.
 * Does not start MediaProjection — that stays behind the system dialog.
 */
class ControlServer(
    private val controlPort: Int = ProtocolPorts.CONTROL,
    private val screenPort: Int = ProtocolPorts.SCREEN
) {
    private val running = AtomicBoolean(false)
    private var server: ServerSocket? = null
    private var acceptThread: Thread? = null
    private var router: AgentCommandRouter? = null

    fun describe(): String = "control=$controlPort screen=$screenPort"

    fun start(sink: AgentCommandSink) {
        if (!running.compareAndSet(false, true)) {
            router = AgentCommandRouter(sink)
            return
        }
        router = AgentCommandRouter(sink)
        val socket = ServerSocket()
        socket.reuseAddress = true
        socket.bind(InetSocketAddress(controlPort))
        server = socket
        acceptThread = thread(name = "control-17890", isDaemon = true) { acceptLoop(socket) }
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
            } catch (_: Exception) {
                break
            }
            thread(name = "control-session", isDaemon = true) { session(client) }
        }
    }

    private fun session(socket: Socket) {
        socket.use { client ->
            val input = client.getInputStream()
            val output = client.getOutputStream()
            while (running.get()) {
                val body = try {
                    LengthPrefixed.read(input, ProtocolPorts.MAX_JSON_BYTES)
                } catch (_: Exception) {
                    break
                }
                val envelope = try {
                    JsonLite.parseEnvelope(body.decodeToString())
                } catch (_: Exception) {
                    continue
                }
                val reply = router?.handle(envelope) ?: envelope
                LengthPrefixed.write(
                    output,
                    JsonLite.encodeEnvelope(reply).encodeToByteArray(),
                    ProtocolPorts.MAX_JSON_BYTES
                )
            }
        }
    }

    companion object {
        val instance = ControlServer()
    }
}
