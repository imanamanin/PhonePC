package com.phonecontrol.agent.data

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.phonecontrol.agent.domain.TrustedPairing
import java.util.Base64

class EncryptedPairingStore(context: Context) : PairingStore {
    private val prefs: SharedPreferences = openPrefs(context)

    override fun save(record: TrustedPairing) {
        prefs.edit()
            .putString(KEY_ID, record.pairingId)
            .putString(KEY_TOKEN, Base64.getEncoder().encodeToString(record.sessionToken))
            .putLong(KEY_EXPIRES, record.expiresAtEpochMs)
            .commit()
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
        prefs.edit().clear().commit()
    }

    companion object {
        private const val FILE = "phone_control_pairing"
        private const val FILE_FALLBACK = "phone_control_pairing_plain"
        private const val KEY_ID = "pairing_id"
        private const val KEY_TOKEN = "session_token"
        private const val KEY_EXPIRES = "expires_at"

        private fun openPrefs(context: Context): SharedPreferences {
            return try {
                EncryptedSharedPreferences.create(
                    FILE,
                    MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC),
                    context,
                    EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                    EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
                )
            } catch (_: Exception) {
                context.getSharedPreferences(FILE_FALLBACK, Context.MODE_PRIVATE)
            }
        }
    }
}
