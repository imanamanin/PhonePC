package com.phonecontrol.agent.clipboard

import com.phonecontrol.agent.domain.ClipboardLoopGuard

class ClipboardSync(private val guard: ClipboardLoopGuard = ClipboardLoopGuard()) {
    fun shouldApply(origin: String, hash: String): Boolean =
        guard.shouldApplyIncoming(origin, hash, localOrigin = "android")
}
