package com.phonecontrol.agent.screencapture

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioPlaybackCaptureConfiguration
import android.media.AudioRecord
import android.media.projection.MediaProjection
import android.os.Build
import com.phonecontrol.agent.domain.AudioPacket
import com.phonecontrol.agent.domain.ProtocolPorts
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.util.concurrent.ArrayBlockingQueue
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.concurrent.thread

/**
 * Phone playback on TCP 17892 via MediaProjection. Does not change USB tethering.
 */
internal class PlaybackAudioCapture {
    private val running = AtomicBoolean(false)
    private val chunks = ArrayBlockingQueue<ByteArray>(16)
    private var record: AudioRecord? = null
    private var captureThread: Thread? = null
    private var serverThread: Thread? = null
    private var serverSocket: ServerSocket? = null
    private var sampleRate = 48000
    private var channels = 2
    private var appContext: Context? = null

    fun start(context: Context, projection: MediaProjection) {
        if (Build.VERSION.SDK_INT < 29) {
            return
        }
        stop()
        appContext = context.applicationContext
        running.set(true)
        serverThread = thread(name = "audio-17892", isDaemon = true) { serve() }
        captureThread = thread(name = "pcm-capture", isDaemon = true) {
            var rec = buildRecord(projection)
            if (rec == null) {
                Thread.sleep(400)
                if (!running.get()) {
                    return@thread
                }
                rec = buildRecord(projection)
            }
            if (rec == null) {
                return@thread
            }
            record = rec
            sampleRate = rec.sampleRate
            channels = rec.channelCount.coerceAtLeast(1)
            try {
                rec.startRecording()
            } catch (_: Exception) {
                return@thread
            }
            captureLoop(rec)
        }
    }

    fun stop() {
        running.set(false)
        try {
            serverSocket?.close()
        } catch (_: Exception) {
            // Unblock accept.
        }
        serverSocket = null
        try {
            record?.stop()
        } catch (_: Exception) {
            // Already stopped.
        }
        try {
            record?.release()
        } catch (_: Exception) {
            // Already released.
        }
        record = null
        chunks.clear()
        appContext = null
    }

    private fun buildRecord(projection: MediaProjection): AudioRecord? {
        val rates = intArrayOf(48000, 44100)
        val channelMasks = intArrayOf(AudioFormat.CHANNEL_IN_STEREO, AudioFormat.CHANNEL_IN_MONO)
        for (rate in rates) {
            for (mask in channelMasks) {
                val built = tryBuild(projection, rate, mask)
                if (built != null) {
                    return built
                }
            }
        }
        return null
    }

    private fun tryBuild(projection: MediaProjection, rate: Int, channelMask: Int): AudioRecord? {
        if (Build.VERSION.SDK_INT < 29) {
            return null
        }
        return try {
            val configBuilder = AudioPlaybackCaptureConfiguration.Builder(projection)
                .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
                .addMatchingUsage(AudioAttributes.USAGE_GAME)
                .addMatchingUsage(AudioAttributes.USAGE_UNKNOWN)
                .addMatchingUsage(AudioAttributes.USAGE_ASSISTANCE_SONIFICATION)
                .addMatchingUsage(AudioAttributes.USAGE_ASSISTANCE_NAVIGATION_GUIDANCE)
                .addMatchingUsage(AudioAttributes.USAGE_ASSISTANCE_ACCESSIBILITY)
                .addMatchingUsage(AudioAttributes.USAGE_NOTIFICATION)
                .addMatchingUsage(AudioAttributes.USAGE_ALARM)
                .addMatchingUsage(AudioAttributes.USAGE_VOICE_COMMUNICATION)
                .addMatchingUsage(AudioAttributes.USAGE_VOICE_COMMUNICATION_SIGNALLING)
            val format = AudioFormat.Builder()
                .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                .setSampleRate(rate)
                .setChannelMask(channelMask)
                .build()
            val min = AudioRecord.getMinBufferSize(rate, channelMask, AudioFormat.ENCODING_PCM_16BIT)
            if (min <= 0) {
                return null
            }
            val builder = AudioRecord.Builder()
                .setAudioFormat(format)
                .setBufferSizeInBytes(min.coerceAtLeast(rate / 5 * 4))
                .setAudioPlaybackCaptureConfig(configBuilder.build())
            if (Build.VERSION.SDK_INT >= 30) {
                builder.setPrivacySensitive(false)
            }
            if (Build.VERSION.SDK_INT >= 31) {
                appContext?.let { builder.setContext(it) }
            }
            val rec = builder.build()
            if (rec.state != AudioRecord.STATE_INITIALIZED) {
                rec.release()
                null
            } else {
                rec
            }
        } catch (_: Exception) {
            null
        }
    }

    private fun captureLoop(audioRecord: AudioRecord) {
        val frameBytes = (sampleRate / 50) * channels * 2
        val buffer = ByteArray(frameBytes.coerceIn(1920, 16384))
        while (running.get()) {
            val n = try {
                audioRecord.read(buffer, 0, buffer.size)
            } catch (_: Exception) {
                break
            }
            if (n > 0) {
                val copy = buffer.copyOf(n)
                if (!chunks.offer(copy)) {
                    chunks.poll()
                    chunks.offer(copy)
                }
            } else if (n == 0) {
                Thread.sleep(8)
            } else {
                Thread.sleep(20)
            }
        }
    }

    private fun serve() {
        val server = ServerSocket()
        server.reuseAddress = true
        server.soTimeout = 250
        server.bind(InetSocketAddress("0.0.0.0", ProtocolPorts.AUDIO), 16)
        serverSocket = server
        server.use {
            while (running.get()) {
                val socket = try {
                    server.accept()
                } catch (_: java.net.SocketTimeoutException) {
                    continue
                } catch (_: Exception) {
                    if (!running.get()) {
                        break
                    }
                    continue
                }
                socket.use { client ->
                    val out = client.getOutputStream()
                    while (running.get()) {
                        val pcm = chunks.poll(40, TimeUnit.MILLISECONDS) ?: continue
                        writeFrame(out, packet(pcm))
                    }
                }
            }
        }
    }

    private fun packet(pcm: ByteArray): ByteArray {
        return AudioPacket.encode(
            AudioPacket(
                sampleRate = sampleRate,
                channels = channels,
                bitsPerSample = 16,
                captureTimestampMs = System.currentTimeMillis(),
                payload = pcm
            )
        )
    }

    private fun writeFrame(out: java.io.OutputStream, body: ByteArray) {
        if (body.size > ProtocolPorts.MAX_VIDEO_BYTES) {
            return
        }
        val header = ByteArray(4)
        val size = body.size
        header[0] = ((size ushr 24) and 0xFF).toByte()
        header[1] = ((size ushr 16) and 0xFF).toByte()
        header[2] = ((size ushr 8) and 0xFF).toByte()
        header[3] = (size and 0xFF).toByte()
        out.write(header)
        out.write(body)
        out.flush()
    }
}
