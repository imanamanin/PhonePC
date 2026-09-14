package com.phonecontrol.agent.domain

import java.io.InputStream
import java.io.OutputStream

object LengthPrefixed {
    fun write(out: OutputStream, body: ByteArray, maxBytes: Int) {
        require(body.isNotEmpty() && body.size <= maxBytes)
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

    fun read(input: InputStream, maxBytes: Int): ByteArray {
        val header = readExact(input, 4)
        val size = ((header[0].toInt() and 0xFF) shl 24) or
            ((header[1].toInt() and 0xFF) shl 16) or
            ((header[2].toInt() and 0xFF) shl 8) or
            (header[3].toInt() and 0xFF)
        require(size in 1..maxBytes)
        return readExact(input, size)
    }

    private fun readExact(input: InputStream, count: Int): ByteArray {
        val buffer = ByteArray(count)
        var offset = 0
        while (offset < count) {
            val read = input.read(buffer, offset, count - offset)
            if (read < 0) throw java.io.EOFException()
            offset += read
        }
        return buffer
    }
}
