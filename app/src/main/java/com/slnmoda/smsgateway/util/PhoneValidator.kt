package com.slnmoda.smsgateway.util

/**
 * Telefon numarası doğrulama. Spec E.164 formatını hedefler (+ ve 8-15 hane).
 * Türkiye numaraları için +90 ön eki beklenir ama uluslararası da kabul edilir.
 */
object PhoneValidator {

    private val E164 = Regex("^\\+[1-9]\\d{7,14}$")

    /** Girdi geçerliyse normalize edilmiş numarayı, değilse null döner. */
    fun normalizeOrNull(raw: String?): String? {
        if (raw.isNullOrBlank()) return null
        // Boşluk, tire, parantez gibi ayırıcıları temizle.
        val cleaned = raw.filter { it.isDigit() || it == '+' }
        return if (E164.matches(cleaned)) cleaned else null
    }
}
