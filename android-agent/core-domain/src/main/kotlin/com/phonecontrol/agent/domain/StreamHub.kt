package com.phonecontrol.agent.domain

import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.atomic.AtomicLong
import java.util.concurrent.atomic.AtomicReference

data class StreamSnapshot(
    val videoSeq: Long,
    val video: ByteArray?,
    val audioSeq: Long,
    val audio: ByteArray?,
    val width: Int,
    val height: Int
)

/**
 * Latest JPEG plus a fan-out of PCM packets for every live client
 * (Windows TCP and the Chrome bridge). Capture still starts only after
 * the MediaProjection dialog.
 */
object StreamHub {
    private val video = AtomicReference<ByteArray?>(null)
    private val audio = AtomicReference<ByteArray?>(null)
    private val videoSeq = AtomicLong(0)
    private val audioSeq = AtomicLong(0)
    private val audioListeners = CopyOnWriteArrayList<(ByteArray) -> Unit>()

    @Volatile
    var width: Int = 0
        private set

    @Volatile
    var height: Int = 0
        private set

    fun publishVideo(packet: ByteArray, w: Int, h: Int) {
        width = w
        height = h
        video.set(packet)
        videoSeq.incrementAndGet()
    }

    fun publishAudio(packet: ByteArray) {
        audio.set(packet)
        audioSeq.incrementAndGet()
        for (listener in audioListeners) {
            try {
                listener(packet)
            } catch (_: Exception) {
                // Drop a dead client; the socket loop will detach.
            }
        }
    }

    fun addAudioListener(listener: (ByteArray) -> Unit) {
        audioListeners.add(listener)
    }

    fun removeAudioListener(listener: (ByteArray) -> Unit) {
        audioListeners.remove(listener)
    }

    fun snapshot(): StreamSnapshot = StreamSnapshot(
        videoSeq = videoSeq.get(),
        video = video.get(),
        audioSeq = audioSeq.get(),
        audio = audio.get(),
        width = width,
        height = height
    )

    fun clear() {
        video.set(null)
        audio.set(null)
        videoSeq.set(0)
        audioSeq.set(0)
        width = 0
        height = 0
    }
}
