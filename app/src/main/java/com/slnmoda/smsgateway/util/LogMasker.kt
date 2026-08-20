package com.slnmoda.smsgateway.util

/**
 * KVKK / ISO 27001 uyumu: loglarda hassas veriyi maskeler.
 * Mesaj içindeki OTP/sayısal kodlar ve telefon numarasının orta haneleri gizlenir.
 * Log tablosuna asla ham mesaj metni yazılmamalıdır.
 */
object LogMasker {

    // 3+ haneli sayı dizilerini (OTP kodları) maskele.
    private val DIGIT_RUN = Regex("\\d{3,}")

    /** "+905321112233" -> "+9053****2233" */
    fun maskPhone(phone: String): String {
        if (phone.length <= 6) return "****"
        val head = phone.take(5)
        val tail = phone.takeLast(4)
        return "$head****$tail"
    }

    /**
     * Mesaj metnini loga yazmak yerine yalnızca uzunluğunu ve maskelenmiş
     * bir önizlemesini tutar. OTP kodları "***" ile değiştirilir.
     */
    fun maskMessagePreview(message: String): String {
        val masked = DIGIT_RUN.replace(message) { "*".repeat(it.value.length) }
        val preview = masked.take(24)
        return if (masked.length > 24) "$preview…" else preview
    }
}
