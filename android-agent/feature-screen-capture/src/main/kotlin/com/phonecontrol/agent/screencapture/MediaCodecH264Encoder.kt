package com.phonecontrol.agent.screencapture

import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaCodecList
import android.media.MediaFormat
import android.os.Bundle
import android.view.Surface

data class EncodedNal(val bytes: ByteArray, val keyframe: Boolean, val config: Boolean)

interface VideoEncoder {
    fun configure(width: Int, height: Int, fps: Int, bitrateKbps: Int): Surface?
    fun drain(): List<EncodedNal>
    fun applyBitrate(bitrateKbps: Int)
    fun requestKeyFrame()
    fun release()
}

/**
 * Hardware-first H.264 encoder. COLOR_FormatSurface lets MediaProjection's VirtualDisplay
 * feed the encoder without a CPU copy. Software OMX.google / c2.android is the fallback.
 */
class MediaCodecH264Encoder : VideoEncoder {
    private var codec: MediaCodec? = null

    override fun configure(width: Int, height: Int, fps: Int, bitrateKbps: Int): Surface? {
        release()
        val evenWidth = width and 1.inv()
        val evenHeight = height and 1.inv()
        val format = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, evenWidth, evenHeight).apply {
            setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface)
            setInteger(MediaFormat.KEY_BIT_RATE, bitrateKbps * 1000)
            setInteger(MediaFormat.KEY_FRAME_RATE, fps)
            setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1)
        }
        if (android.os.Build.VERSION.SDK_INT >= 29) {
            format.setInteger(MediaFormat.KEY_MAX_B_FRAMES, 0)
        }
        val encoder = try {
            createHardwareFirst()
        } catch (_: Exception) {
            return null
        }
        return try {
            encoder.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE)
            val surface = encoder.createInputSurface()
            encoder.start()
            codec = encoder
            surface
        } catch (_: Exception) {
            encoder.release()
            null
        }
    }

    override fun drain(): List<EncodedNal> {
        val encoder = codec ?: return emptyList()
        val info = MediaCodec.BufferInfo()
        val nals = mutableListOf<EncodedNal>()
        while (true) {
            val index = encoder.dequeueOutputBuffer(info, 0)
            when {
                index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED -> {
                    csdFromFormat(encoder.outputFormat)?.let { nals += it }
                }
                index < 0 -> break
                else -> {
                    val buffer = encoder.getOutputBuffer(index)
                    if (buffer == null) {
                        encoder.releaseOutputBuffer(index, false)
                        continue
                    }
                    val bytes = ByteArray(info.size)
                    buffer.position(info.offset)
                    buffer.get(bytes)
                    nals += EncodedNal(
                        bytes = bytes,
                        keyframe = info.flags and MediaCodec.BUFFER_FLAG_KEY_FRAME != 0,
                        config = info.flags and MediaCodec.BUFFER_FLAG_CODEC_CONFIG != 0
                    )
                    encoder.releaseOutputBuffer(index, false)
                }
            }
        }
        return nals
    }

    override fun requestKeyFrame() {
        val encoder = codec ?: return
        val bundle = Bundle()
        bundle.putInt(MediaCodec.PARAMETER_KEY_REQUEST_SYNC_FRAME, 0)
        try {
            encoder.setParameters(bundle)
        } catch (_: Exception) {
            // Some OEM encoders reject sync-frame requests.
        }
    }

    override fun applyBitrate(bitrateKbps: Int) {
        val encoder = codec ?: return
        val bundle = Bundle()
        bundle.putInt(MediaCodec.PARAMETER_KEY_VIDEO_BITRATE, bitrateKbps * 1000)
        try {
            encoder.setParameters(bundle)
        } catch (_: Exception) {
            // Some OEM encoders reject runtime bitrate changes.
        }
    }

    override fun release() {
        try {
            codec?.stop()
        } catch (_: Exception) {
            // Already stopped.
        }
        codec?.release()
        codec = null
    }

    private fun createHardwareFirst(): MediaCodec {
        val list = MediaCodecList(MediaCodecList.REGULAR_CODECS)
        for (info in list.codecInfos) {
            if (!info.isEncoder) continue
            if (!info.supportedTypes.any { it.equals(MediaFormat.MIMETYPE_VIDEO_AVC, ignoreCase = true) }) continue
            val name = info.name.lowercase()
            if (name.contains("google") || name.contains("c2.android")) continue
            return MediaCodec.createByCodecName(info.name)
        }
        return MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC)
    }

    private fun csdFromFormat(format: MediaFormat): EncodedNal? {
        val sps = byteArrayFrom(format, "csd-0") ?: return null
        val pps = byteArrayFrom(format, "csd-1")
        val bytes = if (pps == null) sps else sps + pps
        return EncodedNal(bytes = bytes, keyframe = true, config = true)
    }

    private fun byteArrayFrom(format: MediaFormat, key: String): ByteArray? {
        val buffer = format.getByteBuffer(key) ?: return null
        val copy = buffer.duplicate()
        val bytes = ByteArray(copy.remaining())
        copy.get(bytes)
        return bytes
    }
}
