package com.phonecontrol.agent.pairing

import com.phonecontrol.agent.domain.PairingEngine

class PairingController(
    private val engine: PairingEngine
) {
    fun start(ttlMs: Long = 120_000L) = engine.ensureChallenge()

    fun pinForDisplay(): String? = engine.displayedPin()

    fun sasForDisplay(): String? = engine.lastSas
}
