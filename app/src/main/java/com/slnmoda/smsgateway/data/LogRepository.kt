package com.slnmoda.smsgateway.data

import android.content.Context
import com.slnmoda.smsgateway.config.AppConfig
import com.slnmoda.smsgateway.util.LogMasker
import java.time.Instant
import java.time.temporal.ChronoUnit

/**
 * SMS log kayıtlarını maskeleyerek yazan ve retention temizliğini yöneten katman.
 */
class LogRepository(context: Context) {

    private val dao = AppDatabase.get(context).smsLogDao()
    private val config = AppConfig.get(context)

    fun recent(limit: Int = 100) = dao.recent(limit)

    suspend fun logSuccess(phone: String, message: String, timestampUtc: String) {
        insert(phone, message, "SUCCESS", null, timestampUtc)
    }

    suspend fun logFailure(phone: String, message: String, reason: String, timestampUtc: String) {
        // Hata açıklaması da maskelenir (içinde numara/kod geçebilir).
        insert(phone, message, "FAILED", LogMasker.maskMessagePreview(reason), timestampUtc)
    }

    private suspend fun insert(
        phone: String,
        message: String,
        status: String,
        detail: String?,
        timestampUtc: String
    ) {
        dao.insert(
            SmsLogEntity(
                maskedPhone = LogMasker.maskPhone(phone),
                messagePreview = LogMasker.maskMessagePreview(message),
                status = status,
                detail = detail,
                timestampUtc = timestampUtc,
                createdAtMillis = System.currentTimeMillis()
            )
        )
    }

    suspend fun successCount() = dao.successCount()
    suspend fun failedCount() = dao.failedCount()

    /** Retention penceresinden eski kayıtları siler. Silinen kayıt sayısını döner. */
    suspend fun purgeExpired(): Int {
        val threshold = Instant.now()
            .minus(config.logRetentionDays.toLong(), ChronoUnit.DAYS)
            .toEpochMilli()
        return dao.purgeOlderThan(threshold)
    }
}
