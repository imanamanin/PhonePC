package com.phonecontrol.agent.domain

/** Small JSON helpers for protocol envelopes. No logging of payload text. */
object JsonLite {
    fun encodeEnvelope(envelope: Envelope): String {
        val payload = envelope.payloadJson ?: "null"
        val error = envelope.error?.let { err ->
            """{"code":${quote(err.code)},"message":${quote(err.message)},"retryable":${err.retryable}}"""
        } ?: "null"
        return """{"version":${envelope.version},"type":${quote(envelope.type)},"requestId":${quote(envelope.requestId)},"timestamp":${envelope.timestamp},"payload":$payload,"error":$error}"""
    }

    fun parseEnvelope(json: String): Envelope {
        return Envelope(
            version = intField(json, "version") ?: 1,
            type = stringField(json, "type") ?: throw IllegalArgumentException("type"),
            requestId = stringField(json, "requestId") ?: throw IllegalArgumentException("requestId"),
            timestamp = longField(json, "timestamp") ?: 0L,
            payloadJson = objectOrNullField(json, "payload"),
            error = null
        )
    }

    fun stringField(json: String, name: String): String? {
        val start = valueStart(json, name) ?: return null
        if (json[start] == 'n') return null
        if (json[start] != '"') return null
        return readString(json, start)
    }

    fun intField(json: String, name: String): Int? = doubleField(json, name)?.toInt()

    fun longField(json: String, name: String): Long? = doubleField(json, name)?.toLong()

    fun doubleField(json: String, name: String): Double? {
        val start = valueStart(json, name) ?: return null
        if (json[start] == 'n' || json[start] == '"') return null
        var end = start
        while (end < json.length && json[end] in "+-0123456789.eE") {
            end++
        }
        return json.substring(start, end).toDoubleOrNull()
    }

    fun objectOrNullField(json: String, name: String): String? {
        val start = valueStart(json, name) ?: return null
        if (json.startsWith("null", start)) return null
        return when (json[start]) {
            '{' -> balanced(json, start, '{', '}')
            '[' -> balanced(json, start, '[', ']')
            '"' -> "\"${readString(json, start)}\""
            else -> {
                var end = start
                while (end < json.length && json[end] !in ",}") end++
                json.substring(start, end).trim()
            }
        }
    }

    fun quote(value: String): String {
        val escaped = value.replace("\\", "\\\\").replace("\"", "\\\"")
        return "\"$escaped\""
    }

    private fun valueStart(json: String, name: String): Int? {
        val key = "\"$name\""
        var from = 0
        while (from < json.length) {
            val idx = json.indexOf(key, from)
            if (idx < 0) return null
            var i = idx + key.length
            while (i < json.length && json[i].isWhitespace()) i++
            if (i < json.length && json[i] == ':') {
                i++
                while (i < json.length && json[i].isWhitespace()) i++
                if (i < json.length) return i
            }
            from = idx + 1
        }
        return null
    }

    private fun readString(json: String, quoteIndex: Int): String {
        val builder = StringBuilder()
        var i = quoteIndex + 1
        while (i < json.length) {
            val ch = json[i]
            if (ch == '\\' && i + 1 < json.length) {
                builder.append(json[i + 1])
                i += 2
                continue
            }
            if (ch == '"') break
            builder.append(ch)
            i++
        }
        return builder.toString()
    }

    private fun balanced(json: String, start: Int, open: Char, close: Char): String {
        var depth = 0
        var inString = false
        var escape = false
        for (i in start until json.length) {
            val ch = json[i]
            if (inString) {
                if (escape) {
                    escape = false
                } else if (ch == '\\') {
                    escape = true
                } else if (ch == '"') {
                    inString = false
                }
                continue
            }
            when (ch) {
                '"' -> inString = true
                open -> depth++
                close -> {
                    depth--
                    if (depth == 0) return json.substring(start, i + 1)
                }
            }
        }
        return json.substring(start)
    }
}
