package com.slnmoda.smsgateway.data

import androidx.room.Entity
import androidx.room.PrimaryKey

/**
 * Gönderilen bir SMS'in denetim (audit) kaydı.
 * Not: [maskedPhone] ve [messagePreview] maskelenmiş değerlerdir; ham içerik
 * KVKK gereği burada tutulmaz.
 */
@Entity(tableName = "sms_log")
data class SmsLogEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val maskedPhone: String,
    val messagePreview: String,
    val status: String,          // SUCCESS | FAILED
    val detail: String?,         // Hata durumunda kısa açıklama (maskeli)
    val timestampUtc: String,    // ISO-8601, UTC
    val createdAtMillis: Long    // Retention temizliği için epoch ms
)
