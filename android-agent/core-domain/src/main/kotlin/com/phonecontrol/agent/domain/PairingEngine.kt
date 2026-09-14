package com.phonecontrol.agent.domain

import java.security.MessageDigest
import java.security.SecureRandom
import java.util.UUID

data class TrustedPairing(
    val pairingId: String,
    val sessionToken: ByteArray,
    val expiresAtEpochMs: Long
)

sealed class PinSubmitResult {
    data class Accepted(val pairing: TrustedPairing, val sas: String) : PinSubmitResult()
    object Rejected : PinSubmitResult()
    object Expired : PinSubmitResult()
    object Locked : PinSubmitResult()
}

class PairingEngine(
    private val nowMs: () -> Long,
    private val randomPin: () -> String = { defaultPin() },
    private val randomBytes: (Int) -> ByteArray = { size -> ByteArray(size).also { SecureRandom().nextBytes(it) } },
    private val pinTtlMs: Long = 120_000L,
    private val tokenTtlMs: Long = 30L * 24 * 60 * 60 * 1000L,
    private val maxAttempts: Int = 5
) {
    private var challenge: PairingCode? = null
    private var attempts: Int = 0
    var trusted: TrustedPairing? = null
        private set
    var lastSas: String? = null
        private set

    fun restore(record: TrustedPairing) {
        trusted = record
        lastSas = sas(record.sessionToken)
    }

    fun displayedPin(): String? {
        val code = ensureChallenge()
        return if (code.isExpired(nowMs())) null else code.value
    }

    fun ensureChallenge(): PairingCode {
        val existing = challenge
        if (existing != null && !existing.isExpired(nowMs()) && attempts < maxAttempts) {
            return existing
        }
        attempts = 0
        val created = PairingCode(randomPin(), nowMs() + pinTtlMs)
        challenge = created
        return created
    }

    fun submitPin(pin: String): PinSubmitResult {
        val code = challenge ?: return PinSubmitResult.Expired
        if (code.isExpired(nowMs())) {
            challenge = null
            return PinSubmitResult.Expired
        }
        if (attempts >= maxAttempts) {
            return PinSubmitResult.Locked
        }
        attempts++
        if (!constantTimeEquals(code.value, pin)) {
            return if (attempts >= maxAttempts) PinSubmitResult.Locked else PinSubmitResult.Rejected
        }
        val token = randomBytes(32)
        val pairing = TrustedPairing(
            pairingId = UUID.randomUUID().toString(),
            sessionToken = token,
            expiresAtEpochMs = nowMs() + tokenTtlMs
        )
        trusted = pairing
        lastSas = sas(token)
        challenge = null
        attempts = 0
        return PinSubmitResult.Accepted(pairing, lastSas!!)
    }

    fun validateToken(pairingId: String?, tokenB64: String?): Boolean {
        val current = trusted ?: return false
        if (current.expiresAtEpochMs <= nowMs()) {
            trusted = null
            return false
        }
        if (pairingId.isNullOrBlank() || tokenB64.isNullOrBlank()) return false
        if (!constantTimeEquals(current.pairingId, pairingId)) return false
        val incoming = try {
            java.util.Base64.getDecoder().decode(tokenB64)
        } catch (_: IllegalArgumentException) {
            return false
        }
        return constantTimeEquals(current.sessionToken, incoming)
    }

    fun isPaired(): Boolean {
        val current = trusted ?: return false
        if (current.expiresAtEpochMs <= nowMs()) {
            trusted = null
            return false
        }
        return true
    }

    fun clear() {
        trusted = null
        lastSas = null
        challenge = null
        attempts = 0
    }

    companion object {
        fun sas(token: ByteArray): String {
            val hash = MessageDigest.getInstance("SHA-256").digest(token)
            val n = ((hash[0].toInt() and 0xFF) shl 16) or
                ((hash[1].toInt() and 0xFF) shl 8) or
                (hash[2].toInt() and 0xFF)
            return String.format("%06d", n % 1_000_000)
        }

        fun constantTimeEquals(left: String, right: String): Boolean {
            val a = left.toByteArray()
            val b = right.toByteArray()
            return constantTimeEquals(a, b)
        }

        fun constantTimeEquals(left: ByteArray, right: ByteArray): Boolean {
            var diff = left.size xor right.size
            val n = maxOf(left.size, right.size)
            for (i in 0 until n) {
                val av = if (i < left.size) left[i].toInt() else 0
                val bv = if (i < right.size) right[i].toInt() else 0
                diff = diff or (av xor bv)
            }
            return diff == 0
        }

        private fun defaultPin(): String = (100000..999999).random().toString()
    }
}
