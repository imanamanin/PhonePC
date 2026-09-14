package com.phonecontrol.agent.data

import com.phonecontrol.agent.domain.TrustedPairing

interface PairingStore {
    fun save(record: TrustedPairing)
    fun load(): TrustedPairing?
    fun clear()
}

class InMemoryPairingStore : PairingStore {
    private var record: TrustedPairing? = null

    override fun save(record: TrustedPairing) {
        this.record = TrustedPairing(
            record.pairingId,
            record.sessionToken.copyOf(),
            record.expiresAtEpochMs
        )
    }

    override fun load(): TrustedPairing? {
        val value = record ?: return null
        return TrustedPairing(value.pairingId, value.sessionToken.copyOf(), value.expiresAtEpochMs)
    }

    override fun clear() {
        record = null
    }
}
