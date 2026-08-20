package com.slnmoda.smsgateway.data

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.Query
import kotlinx.coroutines.flow.Flow

@Dao
interface SmsLogDao {

    @Insert
    suspend fun insert(entry: SmsLogEntity): Long

    @Query("SELECT * FROM sms_log ORDER BY id DESC LIMIT :limit")
    fun recent(limit: Int = 100): Flow<List<SmsLogEntity>>

    @Query("SELECT COUNT(*) FROM sms_log WHERE status = 'SUCCESS'")
    suspend fun successCount(): Int

    @Query("SELECT COUNT(*) FROM sms_log WHERE status = 'FAILED'")
    suspend fun failedCount(): Int

    /** Retention sınırından (epoch ms) eski kayıtları siler. KVKK temizliği. */
    @Query("DELETE FROM sms_log WHERE createdAtMillis < :thresholdMillis")
    suspend fun purgeOlderThan(thresholdMillis: Long): Int
}
