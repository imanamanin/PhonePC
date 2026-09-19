package com.phonecontrol.agent.domain

import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.atomic.AtomicInteger

/**
 * Presentation helper: counts live PC sessions without changing protocol.
 */
object ClientPresence {
    private val count = AtomicInteger(0)
    private val listeners = CopyOnWriteArrayList<(Boolean) -> Unit>()

    fun addListener(listener: (Boolean) -> Unit) {
        listeners.add(listener)
    }

    fun removeListener(listener: (Boolean) -> Unit) {
        listeners.remove(listener)
    }

    fun enter() {
        if (count.incrementAndGet() == 1) {
            listeners.forEach { it(true) }
        }
    }

    fun leave() {
        val remaining = count.updateAndGet { value -> if (value <= 0) 0 else value - 1 }
        if (remaining == 0) {
            listeners.forEach { it(false) }
        }
    }
}
