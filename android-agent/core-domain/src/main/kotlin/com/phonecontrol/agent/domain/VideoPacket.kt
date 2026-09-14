package com.phonecontrol.agent.domain

class VideoPacket(
    val codec: Int,
    val width: Int,
    val height: Int,
    val captureTimestampMs: Long,
    val flags: Int,
    val payload: ByteArray
) {
    companion object {
        const val HEADER = 14
        const val H264 = 1
        const val JPEG = 3
        const val RAW_BGRA = 4
        const val KEYFRAME = 1
        const val CONFIG = 2

        fun encode(packet: VideoPacket): ByteArray {
            val out = ByteArray(HEADER + packet.payload.size)
            out[0] = packet.flags.toByte()
            writeLong(out, 1, packet.captureTimestampMs)
            writeShort(out, 9, packet.width)
            writeShort(out, 11, packet.height)
            out[13] = packet.codec.toByte()
            packet.payload.copyInto(out, HEADER)
            return out
        }

        fun decode(buffer: ByteArray): VideoPacket {
            require(buffer.size >= HEADER)
            val payload = buffer.copyOfRange(HEADER, buffer.size)
            return VideoPacket(
                codec = buffer[13].toInt() and 0xFF,
                width = readShort(buffer, 9),
                height = readShort(buffer, 11),
                captureTimestampMs = readLong(buffer, 1),
                flags = buffer[0].toInt() and 0xFF,
                payload = payload
            )
        }

        private fun writeLong(target: ByteArray, offset: Int, value: Long) {
            var v = value
            for (i in 7 downTo 0) {
                target[offset + i] = (v and 0xFF).toByte()
                v = v ushr 8
            }
        }

        private fun writeShort(target: ByteArray, offset: Int, value: Int) {
            target[offset] = ((value ushr 8) and 0xFF).toByte()
            target[offset + 1] = (value and 0xFF).toByte()
        }

        private fun readLong(source: ByteArray, offset: Int): Long {
            var v = 0L
            for (i in 0..7) {
                v = (v shl 8) or (source[offset + i].toLong() and 0xFF)
            }
            return v
        }

        private fun readShort(source: ByteArray, offset: Int): Int {
            return ((source[offset].toInt() and 0xFF) shl 8) or (source[offset + 1].toInt() and 0xFF)
        }
    }
}

data class TouchCommand(
    val action: String,
    val x: Float,
    val y: Float,
    val x2: Float? = null,
    val y2: Float? = null,
    val durationMs: Long = 1,
    val scale: Float? = null
)
