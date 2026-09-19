package com.phonecontrol.agent.domain

object FileNames {
    const val MAX_BYTES = 512L * 1024L * 1024L
    const val CHUNK_BYTES = 24 * 1024
    const val MAX_IN_FLIGHT = 4

    fun sanitize(name: String): String {
        val base = name.substringAfterLast('/')
            .substringAfterLast('\\')
            .trim()
            .trim('.')
        val cleaned = buildString(base.length.coerceAtMost(180)) {
            for (ch in base) {
                if (ch.isLetterOrDigit() || ch in "._- ()[]{}+,@") {
                    append(ch)
                } else {
                    append('_')
                }
            }
        }.trim('_', '.', ' ')
        return cleaned.take(180).ifBlank { "file" }
    }
}
