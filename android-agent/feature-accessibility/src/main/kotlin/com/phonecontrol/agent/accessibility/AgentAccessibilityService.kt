package com.phonecontrol.agent.accessibility

import android.accessibilityservice.AccessibilityService
import android.accessibilityservice.GestureDescription
import android.graphics.Path
import android.view.accessibility.AccessibilityEvent
import com.phonecontrol.agent.domain.TouchCommand

class AgentAccessibilityService : AccessibilityService() {
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
            "tap" -> stroke(command.x, command.y, command.x, command.y, maxOf(1L, command.durationMs))
            "down", "move", "up" -> stroke(command.x, command.y, command.x, command.y, 1)
            "swipe", "scroll" -> stroke(
                command.x,
                command.y,
                command.x2 ?: command.x,
                command.y2 ?: command.y,
                maxOf(30L, command.durationMs)
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
            else -> return false
        }
        return performGlobalAction(action)
    }

    private fun stroke(x1: Float, y1: Float, x2: Float, y2: Float, duration: Long): Boolean {
        val path = Path().apply {
            moveTo(x1, y1)
            lineTo(x2, y2)
        }
        val stroke = GestureDescription.StrokeDescription(path, 0, duration)
        val gesture = GestureDescription.Builder().addStroke(stroke).build()
        return dispatchGesture(gesture, null, null)
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
        @Volatile
        var instance: AgentAccessibilityService? = null
    }
}

object InputInjector {
    fun touch(command: TouchCommand): Boolean =
        AgentAccessibilityService.instance?.inject(command) ?: false

    fun key(key: String): Boolean =
        AgentAccessibilityService.instance?.global(key) ?: false
}
