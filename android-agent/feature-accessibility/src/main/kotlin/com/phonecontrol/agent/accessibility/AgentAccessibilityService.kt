package com.phonecontrol.agent.accessibility

import android.accessibilityservice.AccessibilityService
import android.accessibilityservice.GestureDescription
import android.graphics.Path
import android.os.Bundle
import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityNodeInfo
import com.phonecontrol.agent.domain.EditorText
import com.phonecontrol.agent.domain.TouchCommand

class AgentAccessibilityService : AccessibilityService() {
    private var hasDown = false
    private var downX = 0f
    private var downY = 0f
    override fun onServiceConnected() {
        instance = this
        InputAccessibilityBridge.enabled = true
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent?) {
        // Never log node text.
    }

    override fun onInterrupt() = Unit

    override fun onDestroy() {
        if (instance === this) {
            instance = null
            InputAccessibilityBridge.enabled = false
        }
        super.onDestroy()
    }

    fun inject(command: TouchCommand): Boolean {
        return when (command.action) {
            "tap" -> tap(command.x, command.y, maxOf(50L, command.durationMs))
            "down" -> {
                downX = command.x
                downY = command.y
                hasDown = true
                true
            }
            "move" -> true
            "up" -> {
                if (!hasDown) {
                    return tap(command.x, command.y, 60L)
                }
                hasDown = false
                val x1 = downX
                val y1 = downY
                val x2 = command.x
                val y2 = command.y
                val dx = x2 - x1
                val dy = y2 - y1
                if (dx * dx + dy * dy < 24f * 24f) {
                    tap(x1, y1, 60L)
                } else {
                    stroke(x1, y1, x2, y2, maxOf(160L, command.durationMs))
                }
            }
            "swipe", "scroll" -> stroke(
                command.x,
                command.y,
                command.x2 ?: command.x,
                command.y2 ?: command.y,
                maxOf(180L, command.durationMs)
            )
            "pinch" -> pinch(command)
            else -> false
        }
    }

    fun global(key: String): Boolean {
        val action = when (key) {
            "back" -> GLOBAL_ACTION_BACK
            "home" -> GLOBAL_ACTION_HOME
            "recents" -> GLOBAL_ACTION_RECENTS
            "enter" -> {
                val editor = focusedEditor()
                if (editor != null && android.os.Build.VERSION.SDK_INT >= 30 &&
                    editor.performAction(ACTION_IME_ENTER)
                ) {
                    return true
                }
                return typeIntoFocused("\n")
            }
            "backspace", "delete" -> return deleteOne()
            else -> return false
        }
        return performGlobalAction(action)
    }

    fun typeIntoFocused(addition: String): Boolean {
        if (addition.isEmpty()) {
            return true
        }
        val focused = focusedEditor() ?: return false
        val current = visibleEditorText(focused)
        val args = Bundle()
        args.putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, current + addition)
        return focused.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, args)
    }

    fun deleteOne(): Boolean {
        val focused = focusedEditor() ?: return false
        val current = visibleEditorText(focused)
        if (current.isEmpty()) {
            return true
        }
        val args = Bundle()
        args.putCharSequence(
            AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE,
            current.dropLast(1)
        )
        return focused.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, args)
    }

    private fun visibleEditorText(node: AccessibilityNodeInfo): String {
        return EditorText.visibleContent(
            node.text,
            node.hintText,
            node.isShowingHintText
        )
    }

    private fun focusedEditor(): AccessibilityNodeInfo? {
        val root = rootInActiveWindow ?: return null
        val focused = root.findFocus(AccessibilityNodeInfo.FOCUS_INPUT)
        if (focused != null && (focused.isEditable || focused.isPassword)) {
            return focused
        }
        return findEditable(root)
    }

    private fun findEditable(node: AccessibilityNodeInfo): AccessibilityNodeInfo? {
        if (node.isFocused && (node.isEditable || node.isPassword)) {
            return node
        }
        for (i in 0 until node.childCount) {
            val child = node.getChild(i) ?: continue
            val found = findEditable(child)
            if (found != null) {
                return found
            }
        }
        return null
    }

    private fun tap(x: Float, y: Float, duration: Long): Boolean {
        val px = clampX(x)
        val py = clampY(y)
        return stroke(px, py, px + 1f, py, duration)
    }

    private fun stroke(x1: Float, y1: Float, x2: Float, y2: Float, duration: Long): Boolean {
        val path = Path().apply {
            moveTo(clampX(x1), clampY(y1))
            lineTo(clampX(x2), clampY(y2))
        }
        val stroke = GestureDescription.StrokeDescription(path, 0, duration.coerceIn(50L, 800L))
        val gesture = GestureDescription.Builder().addStroke(stroke).build()
        return dispatchGesture(gesture, null, null)
    }

    private fun clampX(value: Float): Float {
        val max = resources.displayMetrics.widthPixels - 1f
        return value.coerceIn(0f, max.coerceAtLeast(0f))
    }

    private fun clampY(value: Float): Float {
        val max = resources.displayMetrics.heightPixels - 1f
        return value.coerceIn(0f, max.coerceAtLeast(0f))
    }

    private fun pinch(command: TouchCommand): Boolean {
        val x2 = command.x2 ?: command.x
        val y2 = command.y2 ?: command.y
        val scale = command.scale ?: 1.2f
        val cx = (command.x + x2) / 2f
        val cy = (command.y + y2) / 2f
        val path1 = Path().apply {
            moveTo(command.x, command.y)
            lineTo(cx + (command.x - cx) * scale, cy + (command.y - cy) * scale)
        }
        val path2 = Path().apply {
            moveTo(x2, y2)
            lineTo(cx + (x2 - cx) * scale, cy + (y2 - cy) * scale)
        }
        val builder = GestureDescription.Builder()
            .addStroke(GestureDescription.StrokeDescription(path1, 0, 120))
            .addStroke(GestureDescription.StrokeDescription(path2, 0, 120))
        return dispatchGesture(builder.build(), null, null)
    }

    companion object {
        // AccessibilityNodeInfo.ACTION_IME_ENTER (API 30). Numeric to compile on incomplete platform stubs.
        private const val ACTION_IME_ENTER = 0x00200000

        @Volatile
        var instance: AgentAccessibilityService? = null
    }
}

object InputInjector {
    fun touchAvailable(): Boolean = AgentAccessibilityService.instance != null

    fun touch(command: TouchCommand): Boolean =
        AgentAccessibilityService.instance?.inject(command) ?: false

    fun key(key: String): Boolean =
        AgentAccessibilityService.instance?.global(key) ?: false

    fun type(text: String): Boolean =
        AgentAccessibilityService.instance?.typeIntoFocused(text) ?: false
}
