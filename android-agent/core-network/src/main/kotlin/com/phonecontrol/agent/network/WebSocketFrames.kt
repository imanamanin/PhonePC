package com.phonecontrol.agent.network

import java.io.InputStream
import java.io.OutputStream

internal object WebSocketFrames {
    const val TEXT = 1
    const val BINARY = 2
    const val CLOSE = 8
    const val PING = 9
    const val PONG = 10

    class Frame(val opcode: Int, val payload: ByteArray)

    fun write(out: OutputStream, opcode: Int, payload: ByteArray) {
        val len = payload.size
        val header = when {
            len < 126 -> byteArrayOf((0x80 or opcode).toByte(), len.toByte())
            len <= 0xFFFF -> byteArrayOf(
                (0x80 or opcode).toByte(),
                126,
                ((len ushr 8) and 0xFF).toByte(),
                (len and 0xFF).toByte()
            )
            else -> {
                val h = ByteArray(10)
                h[0] = (0x80 or opcode).toByte()
                h[1] = 127
                var v = len.toLong()
                for (i in 9 downTo 2) {
                    h[i] = (v and 0xFF).toByte()
                    v = v ushr 8
                }
                h
            }
        }
        out.write(header)
        out.write(payload)
        out.flush()
    }

    fun read(input: InputStream, max: Int): Frame? {
        val b0 = input.read()
        if (b0 < 0) return null
        val b1 = input.read()
        if (b1 < 0) return null
        val opcode = b0 and 0x0F
        val masked = (b1 and 0x80) != 0
        var length = (b1 and 0x7F).toLong()
        when (length) {
            126L -> {
                val extra = fully(input, 2) ?: return null
                length = ((extra[0].toInt() and 0xFF).toLong() shl 8) or (extra[1].toInt() and 0xFF).toLong()
            }
            127L -> {
                val extra = fully(input, 8) ?: return null
                length = 0L
                for (b in extra) {
                    length = (length shl 8) or (b.toLong() and 0xFF)
                }
            }
        }
        if (length > max) {
            throw IllegalArgumentException("frame")
        }
        val mask = if (masked) fully(input, 4) ?: return null else null
        val payload = fully(input, length.toInt()) ?: return null
        if (mask != null) {
            for (i in payload.indices) {
                payload[i] = (payload[i].toInt() xor (mask[i and 3].toInt() and 0xFF)).toByte()
            }
        }
        return Frame(opcode, payload)
    }

    private fun fully(input: InputStream, count: Int): ByteArray? {
        if (count == 0) return ByteArray(0)
        val out = ByteArray(count)
        var offset = 0
        while (offset < count) {
            val n = input.read(out, offset, count - offset)
            if (n <= 0) return null
            offset += n
        }
        return out
    }
}
