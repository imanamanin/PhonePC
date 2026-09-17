package com.phonecontrol.agent.domain

/** Visible editor text only. Never logs the value. */
object EditorText {
    private val placeholders = listOf(
        "text message",
        "send a message",
        "type a message",
        "write a message",
        "start a conversation",
        "enter message"
    )

    fun visibleContent(displayed: CharSequence?, hint: CharSequence?, showingHint: Boolean): String {
        if (showingHint) {
            return ""
        }

        var text = displayed?.toString().orEmpty()
        val hintText = hint?.toString().orEmpty()
        if (hintText.isNotEmpty()) {
            if (text == hintText) {
                return ""
            }
            if (text.startsWith(hintText)) {
                text = text.substring(hintText.length)
            }
        }

        if (text.isEmpty()) {
            return ""
        }

        val lower = text.lowercase()
        for (placeholder in placeholders) {
            if (lower == placeholder) {
                return ""
            }
            if (lower.startsWith(placeholder) && text.length > placeholder.length) {
                val rest = text.substring(placeholder.length)
                if (rest.first().isLetterOrDigit()) {
                    return rest
                }
            }
        }

        return text
    }
}
