package com.phonecontrol.agent.domain

class ClipboardLoopGuard {
    private var lastSentHash: String? = null
    private var lastAppliedHash: String? = null

    fun shouldApplyIncoming(origin: String, hash: String, localOrigin: String): Boolean {
        if (hash.isBlank()) return false
        if (origin == localOrigin) return false
        if (hash == lastSentHash) return false
        if (hash == lastAppliedHash) return false
        return true
    }

    fun markSent(hash: String) {
        lastSentHash = hash
    }

    fun markApplied(hash: String) {
        lastAppliedHash = hash
    }
}

class PairingCode(val value: String, val expiresAtEpochMs: Long) {
    init {
        require(value.length == 6 && value.all { it.isDigit() }) { "pairing code must be 6 digits" }
    }

    fun isExpired(nowEpochMs: Long): Boolean = nowEpochMs >= expiresAtEpochMs
}
