package com.phonecontrol.agent.network

import com.phonecontrol.agent.domain.AgentCommandRouter
import com.phonecontrol.agent.domain.AgentCommandSink
import com.phonecontrol.agent.domain.ClientPresence
import com.phonecontrol.agent.domain.ControlPush
import com.phonecontrol.agent.domain.JsonLite
import com.phonecontrol.agent.domain.LengthPrefixed
import com.phonecontrol.agent.domain.LocalAddresses
import com.phonecontrol.agent.domain.ProtocolError
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

    fun describe(): String {
        val listen = if (isListening()) "listening" else "not-listening"
        val err = lastError?.let { " error=$it" } ?: ""
        val ips = LocalAddresses.ipv4().joinToString(",").ifEmpty { "no-ipv4" }
        return "control=$controlPort screen=$screenPort $listen $ips$err"
    }

    fun isListening(): Boolean {
        val socket = server
        return socket != null && socket.isBound && !socket.isClosed
    }

    @Volatile
    var lastError: String? = null
        private set

    @Synchronized
    fun start(sink: AgentCommandSink) {
        router = AgentCommandRouter(sink)
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
            try {
                socket.bind(InetSocketAddress("0.0.0.0", controlPort), 16)
            } catch (_: Exception) {
                socket.bind(InetSocketAddress(controlPort), 16)
            }
            server = socket
            acceptThread = thread(name = "control-17890", isDaemon = false) { acceptLoop(socket) }
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
            thread(name = "control-session", isDaemon = true) { session(client) }
        }
    }

    private fun session(socket: Socket) {
        ClientPresence.enter()
        try {
            socket.use { client ->
                val input = client.getInputStream()
                val output = client.getOutputStream()
                val outLock = Any()
                val push: (com.phonecontrol.agent.domain.Envelope) -> Unit = { envelope ->
                    synchronized(outLock) {
                        LengthPrefixed.write(
                            output,
                            JsonLite.encodeEnvelope(envelope).encodeToByteArray(),
                            ProtocolPorts.MAX_JSON_BYTES
                        )
                    }
                }
                ControlPush.addListener(push)
                try {
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
                        val reply = try {
                            router?.handle(envelope) ?: envelope
                        } catch (_: Exception) {
                            envelope.copy(
                                error = ProtocolError(
                                    "INTERNAL",
                                    "handler failed",
                                    retryable = true
                                )
                            )
                        }
                        try {
                            synchronized(outLock) {
                                LengthPrefixed.write(
                                    output,
                                    JsonLite.encodeEnvelope(reply).encodeToByteArray(),
                                    ProtocolPorts.MAX_JSON_BYTES
                                )
                            }
                        } catch (_: Exception) {
                            break
                        }
                    }
                } finally {
                    ControlPush.removeListener(push)
                }
            }
        } finally {
            ClientPresence.leave()
        }
    }

    companion object {
        val instance = ControlServer()
    }
}
