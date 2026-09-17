package com.phonecontrol.agent.domain

class AudioPacket(
    val sampleRate: Int,
    val channels: Int,
    val bitsPerSample: Int,
    val captureTimestampMs: Long,
    val payload: ByteArray
) {
    companion object {
        const val HEADER = 16
        const val PCM16 = 1

        fun encode(packet: AudioPacket): ByteArray {
            val out = ByteArray(HEADER + packet.payload.size)
            out[0] = 0
            writeLong(out, 1, packet.captureTimestampMs)
            writeInt(out, 9, packet.sampleRate)
            out[13] = packet.channels.toByte()
            out[14] = packet.bitsPerSample.toByte()
            out[15] = PCM16.toByte()
            packet.payload.copyInto(out, HEADER)
            return out
        }

        fun decode(buffer: ByteArray): AudioPacket {
            require(buffer.size >= HEADER)
            return AudioPacket(
                sampleRate = readInt(buffer, 9),
                channels = buffer[13].toInt() and 0xFF,
                bitsPerSample = buffer[14].toInt() and 0xFF,
                captureTimestampMs = readLong(buffer, 1),
                payload = buffer.copyOfRange(HEADER, buffer.size)
            )
        }

        private fun writeLong(target: ByteArray, offset: Int, value: Long) {
            var v = value
            for (i in 7 downTo 0) {
                target[offset + i] = (v and 0xFF).toByte()
                v = v ushr 8
            }
        }

        private fun writeInt(target: ByteArray, offset: Int, value: Int) {
            target[offset] = ((value ushr 24) and 0xFF).toByte()
            target[offset + 1] = ((value ushr 16) and 0xFF).toByte()
            target[offset + 2] = ((value ushr 8) and 0xFF).toByte()
            target[offset + 3] = (value and 0xFF).toByte()
        }

        private fun readLong(source: ByteArray, offset: Int): Long {
            var v = 0L
            for (i in 0..7) {
                v = (v shl 8) or (source[offset + i].toLong() and 0xFF)
            }
            return v
        }

        private fun readInt(source: ByteArray, offset: Int): Int {
            return ((source[offset].toInt() and 0xFF) shl 24) or
                ((source[offset + 1].toInt() and 0xFF) shl 16) or
                ((source[offset + 2].toInt() and 0xFF) shl 8) or
                (source[offset + 3].toInt() and 0xFF)
        }
    }
}
