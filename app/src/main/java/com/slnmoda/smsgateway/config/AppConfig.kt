package com.slnmoda.smsgateway.config

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import java.security.SecureRandom

/**
 * Uygulama yapılandırması. Hassas değerler (API anahtarı) Android Keystore
 * destekli [EncryptedSharedPreferences] içinde şifreli olarak saklanır;
 * düz metin olarak diske hiçbir zaman yazılmaz.
 */
class AppConfig private constructor(context: Context) {

    private val prefs = EncryptedSharedPreferences.create(
        context,
        PREFS_NAME,
        MasterKey.Builder(context)
            .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
            .build(),
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )

    /**
     * X-API-KEY başlığında beklenen gizli servis anahtarı.
     * İlk açılışta yoksa kriptografik olarak güvenli bir anahtar üretilir.
     */
    var apiKey: String
        get() = prefs.getString(KEY_API_KEY, null) ?: generateAndStoreApiKey()
        set(value) = prefs.edit().putString(KEY_API_KEY, value.trim()).apply()

    /** Aynı numaraya iki OTP arasında beklenmesi gereken süre (ms). Spec: 60 sn. */
    var rateLimitWindowMs: Long
        get() = prefs.getLong(KEY_RATE_WINDOW, DEFAULT_RATE_WINDOW_MS)
        set(value) = prefs.edit().putLong(KEY_RATE_WINDOW, value).apply()

    /** Logların kaç günden eskisinin otomatik silineceği (KVKK saklama sınırı). */
    var logRetentionDays: Int
        get() = prefs.getInt(KEY_LOG_RETENTION, DEFAULT_LOG_RETENTION_DAYS)
        set(value) = prefs.edit().putInt(KEY_LOG_RETENTION, value).apply()

    private fun generateAndStoreApiKey(): String {
        val bytes = ByteArray(32).also { SecureRandom().nextBytes(it) }
        val key = bytes.joinToString("") { "%02x".format(it) }
        prefs.edit().putString(KEY_API_KEY, key).apply()
        return key
    }

    companion object {
        private const val PREFS_NAME = "gateway_secure_config"
        private const val KEY_API_KEY = "api_key"
        private const val KEY_RATE_WINDOW = "rate_limit_window_ms"
        private const val KEY_LOG_RETENTION = "log_retention_days"

        const val DEFAULT_RATE_WINDOW_MS = 60_000L
        const val DEFAULT_LOG_RETENTION_DAYS = 30

        @Volatile
        private var instance: AppConfig? = null

        fun get(context: Context): AppConfig =
            instance ?: synchronized(this) {
                instance ?: AppConfig(context.applicationContext).also { instance = it }
            }
    }
}
