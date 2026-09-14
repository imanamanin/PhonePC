package com.phonecontrol.agent.data

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.phonecontrol.agent.domain.TrustedPairing
import java.util.Base64

class EncryptedPairingStore(context: Context) : PairingStore {
    private val prefs = EncryptedSharedPreferences.create(
        FILE,
        MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC),
        context,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )

    override fun save(record: TrustedPairing) {
        prefs.edit()
            .putString(KEY_ID, record.pairingId)
            .putString(KEY_TOKEN, Base64.getEncoder().encodeToString(record.sessionToken))
            .putLong(KEY_EXPIRES, record.expiresAtEpochMs)
            .apply()
    }

    override fun load(): TrustedPairing? {
        val id = prefs.getString(KEY_ID, null) ?: return null
        val token = prefs.getString(KEY_TOKEN, null) ?: return null
        val expires = prefs.getLong(KEY_EXPIRES, 0L)
        return try {
            TrustedPairing(id, Base64.getDecoder().decode(token), expires)
        } catch (_: IllegalArgumentException) {
            null
        }
    }

    override fun clear() {
        prefs.edit().clear().apply()
    }

    companion object {
        private const val FILE = "phone_control_pairing"
        private const val KEY_ID = "pairing_id"
        private const val KEY_TOKEN = "session_token"
        private const val KEY_EXPIRES = "expires_at"
    }
}
