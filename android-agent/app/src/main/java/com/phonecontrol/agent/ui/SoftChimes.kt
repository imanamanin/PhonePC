package com.phonecontrol.agent.ui

import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack
import kotlin.concurrent.thread
import kotlin.math.PI
import kotlin.math.min
import kotlin.math.sin

/**
 * Soft synthesized chimes for splash and PC connect/disconnect.
 */
object SoftChimes {
    private const val SAMPLE_RATE = 22050

    fun splash() {
        play(
            listOf(
                Tone(523.25f, 0, 320, 0.16f),
                Tone(659.25f, 140, 340, 0.14f),
                Tone(783.99f, 280, 520, 0.18f)
            )
        )
    }

    fun connect() {
        play(
            listOf(
                Tone(587.33f, 0, 140, 0.17f),
                Tone(880.00f, 90, 180, 0.18f),
                Tone(1174.66f, 180, 280, 0.16f)
            )
        )
    }

    fun disconnect() {
        play(
            listOf(
                Tone(698.46f, 0, 180, 0.14f),
                Tone(523.25f, 120, 280, 0.13f)
            )
        )
    }

    private fun play(tones: List<Tone>) {
        thread(name = "pcphone-chime", isDaemon = true) {
            val end = tones.maxOf { it.startMs + it.durationMs }
            val total = ((end + 40) * SAMPLE_RATE) / 1000
            val pcm = ShortArray(total)
            tones.forEach { mix(pcm, it) }
            val track = try {
                AudioTrack.Builder()
                    .setAudioAttributes(
                        AudioAttributes.Builder()
                            .setUsage(AudioAttributes.USAGE_ASSISTANCE_SONIFICATION)
                            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                            .build()
                    )
                    .setAudioFormat(
                        AudioFormat.Builder()
                            .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                            .setSampleRate(SAMPLE_RATE)
                            .setChannelMask(AudioFormat.CHANNEL_OUT_MONO)
                            .build()
                    )
                    .setBufferSizeInBytes(pcm.size * 2)
                    .setTransferMode(AudioTrack.MODE_STATIC)
                    .build()
            } catch (_: Exception) {
                return@thread
            }
            try {
                track.write(pcm, 0, pcm.size)
                track.setVolume(0.42f)
                track.play()
                Thread.sleep((end + 80).toLong())
            } catch (_: Exception) {
                // Playback is optional.
            } finally {
                try {
                    track.release()
                } catch (_: Exception) {
                    // Already released.
                }
            }
        }
    }

    private fun mix(pcm: ShortArray, tone: Tone) {
        val start = (tone.startMs * SAMPLE_RATE) / 1000
        val length = (tone.durationMs * SAMPLE_RATE) / 1000
        val attack = min(length / 8, SAMPLE_RATE / 80)
        val release = min(length / 3, SAMPLE_RATE / 12)
        for (i in 0 until length) {
            val index = start + i
            if (index >= pcm.size) return
            val env = when {
                i < attack -> i.toFloat() / attack
                i > length - release -> (length - i).toFloat() / release
                else -> 1f
            }.coerceIn(0f, 1f)
            val sample = sin(2.0 * PI * tone.hz * i / SAMPLE_RATE) * tone.gain * env
            val mixed = pcm[index] + (sample * Short.MAX_VALUE).toInt()
            pcm[index] = mixed.coerceIn(Short.MIN_VALUE.toInt(), Short.MAX_VALUE.toInt()).toShort()
        }
    }

    private data class Tone(
        val hz: Float,
        val startMs: Int,
        val durationMs: Int,
        val gain: Float
    )
}
