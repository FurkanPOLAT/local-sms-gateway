package com.slnmoda.smsgateway.server

import java.util.concurrent.ConcurrentHashMap

/**
 * Numara bazlı basit cooldown rate limiter (SMS spam / OTP flood önleme).
 * Aynı numaraya [windowMs] içinde ikinci istek reddedilir.
 * Bellekte tutulur; servis yeniden başlarsa sayaç sıfırlanır (kabul edilebilir).
 */
class RateLimiter(private val windowMs: Long) {

    private val lastSentAt = ConcurrentHashMap<String, Long>()

    /**
     * @return true ise gönderime izin verilir (ve zaman damgası güncellenir),
     *         false ise cooldown penceresi içindedir.
     */
    fun tryAcquire(phone: String, nowMs: Long = System.currentTimeMillis()): Boolean {
        val previous = lastSentAt[phone]
        if (previous != null && nowMs - previous < windowMs) {
            return false
        }
        lastSentAt[phone] = nowMs
        // Bellek şişmesini önlemek için ara sıra eski girdileri temizle.
        if (lastSentAt.size > MAX_ENTRIES) {
            lastSentAt.entries.removeIf { nowMs - it.value > windowMs }
        }
        return true
    }

    private companion object {
        const val MAX_ENTRIES = 1000
    }
}
