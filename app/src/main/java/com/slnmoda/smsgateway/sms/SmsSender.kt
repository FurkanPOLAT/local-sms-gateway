package com.slnmoda.smsgateway.sms

import android.content.Context
import android.os.Build
import android.telephony.SmsManager

/**
 * GSM SMS gönderiminden sorumlu sınıf. İşletim sisteminin [SmsManager]'ı üzerinden
 * fiziksel SIM kart ile tek yönlü SMS gönderir. 160 karakteri aşan mesajlar
 * otomatik olarak çok parçalı (multipart) gönderilir.
 */
class SmsSender(private val context: Context) {

    sealed interface Result {
        data object Success : Result
        data class Failure(val reason: String) : Result
    }

    fun send(phone: String, message: String): Result {
        return try {
            val manager = obtainSmsManager()
            val parts = manager.divideMessage(message)
            if (parts.size > 1) {
                manager.sendMultipartTextMessage(phone, null, parts, null, null)
            } else {
                manager.sendTextMessage(phone, null, message, null, null)
            }
            Result.Success
        } catch (e: IllegalArgumentException) {
            // Boş numara/mesaj gibi durumlar
            Result.Failure("Gecersiz SMS parametresi: ${e.message}")
        } catch (e: SecurityException) {
            Result.Failure("SEND_SMS izni yok veya reddedildi")
        } catch (e: Exception) {
            // SIM erişim hatası, servis yok vb. -> 500
            Result.Failure("GSM/SIM hatasi: ${e.message}")
        }
    }

    @Suppress("DEPRECATION")
    private fun obtainSmsManager(): SmsManager =
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            context.getSystemService(SmsManager::class.java)
        } else {
            SmsManager.getDefault()
        }
}
