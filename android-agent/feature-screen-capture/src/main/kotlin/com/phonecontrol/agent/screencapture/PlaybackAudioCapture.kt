package com.phonecontrol.agent.screencapture

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioPlaybackCaptureConfiguration
import android.media.AudioRecord
import android.media.projection.MediaProjection
import android.os.Build
import com.phonecontrol.agent.domain.AudioPacket
import java.util.concurrent.ArrayBlockingQueue
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import kotlin.concurrent.thread

/**
 * Digital playback mix from MediaProjection, same grant as the screen.
 * Does not use the microphone and does not change USB tethering.
 */
internal class PlaybackAudioCapture {
    private val running = AtomicBoolean(false)
    private val chunks = ArrayBlockingQueue<ByteArray>(24)
    private val sampleRate = AtomicInteger(48000)
    private var record: AudioRecord? = null
    private var captureThread: Thread? = null
    private var appContext: Context? = null

    fun start(context: Context, projection: MediaProjection) {
        if (Build.VERSION.SDK_INT < 29) {
            return
        }
        stop()
        appContext = context.applicationContext
        running.set(true)
        captureThread = thread(name = "pcm-playback", isDaemon = false) { runCapture(projection) }
    }

    fun pollPcm(): ByteArray? = chunks.poll()

    fun encodePcm(pcm: ByteArray): ByteArray = AudioPacket.encode(
        AudioPacket(
            sampleRate = sampleRate.get(),
            channels = 2,
            bitsPerSample = 16,
            captureTimestampMs = System.currentTimeMillis(),
            payload = pcm
        )
    )

    fun stop() {
        running.set(false)
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

    private fun runCapture(projection: MediaProjection) {
        var rec = build(projection)
        if (rec == null) {
            Thread.sleep(400)
            if (!running.get()) {
                return
            }
            rec = build(projection)
        }
        if (rec == null) {
            return
        }
        record = rec
        sampleRate.set(rec.sampleRate.coerceAtLeast(8000))
        try {
            rec.startRecording()
        } catch (_: Exception) {
            return
        }
        val ch = rec.channelCount.coerceAtLeast(1)
        val rate = rec.sampleRate.coerceAtLeast(8000)
        val frameBytes = (rate / 50) * ch * 2
        val buffer = ByteArray(frameBytes.coerceIn(1920, 16384))
        while (running.get()) {
            val n = try {
                rec.read(buffer, 0, buffer.size)
            } catch (_: Exception) {
                break
            }
            if (n <= 0) {
                Thread.sleep(8)
                continue
            }
            val pcm = toStereo(buffer, n, ch)
            if (pcm.isEmpty()) {
                continue
            }
            sampleRate.set(rate)
            if (!chunks.offer(pcm)) {
                chunks.poll()
                chunks.offer(pcm)
            }
        }
    }

    private fun build(projection: MediaProjection): AudioRecord? {
        val rates = intArrayOf(48000, 44100)
        val masks = intArrayOf(AudioFormat.CHANNEL_IN_STEREO, AudioFormat.CHANNEL_IN_MONO)
        for (rate in rates) {
            for (mask in masks) {
                val built = tryBuild(projection, rate, mask)
                if (built != null) {
                    return built
                }
            }
        }
        return null
    }

    private fun tryBuild(projection: MediaProjection, rate: Int, channelMask: Int): AudioRecord? {
        return try {
            val config = AudioPlaybackCaptureConfiguration.Builder(projection)
                .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
                .addMatchingUsage(AudioAttributes.USAGE_GAME)
                .addMatchingUsage(AudioAttributes.USAGE_UNKNOWN)
                .addMatchingUsage(AudioAttributes.USAGE_ASSISTANT)
                .addMatchingUsage(AudioAttributes.USAGE_ASSISTANCE_SONIFICATION)
                .addMatchingUsage(AudioAttributes.USAGE_ASSISTANCE_NAVIGATION_GUIDANCE)
                .addMatchingUsage(AudioAttributes.USAGE_ASSISTANCE_ACCESSIBILITY)
                .addMatchingUsage(AudioAttributes.USAGE_NOTIFICATION)
                .addMatchingUsage(AudioAttributes.USAGE_ALARM)
                .addMatchingUsage(AudioAttributes.USAGE_VOICE_COMMUNICATION)
                .addMatchingUsage(AudioAttributes.USAGE_VOICE_COMMUNICATION_SIGNALLING)
                .build()
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
                .setBufferSizeInBytes((min * 4).coerceAtLeast(rate / 5 * 4))
                .setAudioPlaybackCaptureConfig(config)
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

    companion object {
        private fun toStereo(buffer: ByteArray, bytes: Int, channelCount: Int): ByteArray {
            val n = bytes - (bytes % (2 * channelCount.coerceAtLeast(1)))
            if (n <= 0) {
                return ByteArray(0)
            }
            if (channelCount >= 2) {
                return buffer.copyOf(n)
            }
            val stereo = ByteArray(n * 2)
            var o = 0
            var i = 0
            while (i + 1 < n) {
                stereo[o] = buffer[i]
                stereo[o + 1] = buffer[i + 1]
                stereo[o + 2] = buffer[i]
                stereo[o + 3] = buffer[i + 1]
                o += 4
                i += 2
            }
            return stereo
        }
    }
}
