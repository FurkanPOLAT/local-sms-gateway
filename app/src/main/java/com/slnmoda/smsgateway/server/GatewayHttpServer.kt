package com.slnmoda.smsgateway.server

import android.content.Context
import android.util.Log
import com.slnmoda.smsgateway.config.AppConfig
import com.slnmoda.smsgateway.data.LogRepository
import com.slnmoda.smsgateway.server.dto.ApiResponse
import com.slnmoda.smsgateway.server.dto.SendSmsRequest
import com.slnmoda.smsgateway.sms.SmsSender
import com.slnmoda.smsgateway.util.PhoneValidator
import fi.iki.elonen.NanoHTTPD
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.json.Json
import java.time.Instant
import java.time.format.DateTimeFormatter

/**
 * Yerel ağda [port] üzerinde dinleyen hafif HTTP API.
 *
 * Uçlar:
 *   POST /api/v1/sms/send   -> SMS gönderir (X-API-KEY korumalı)
 *   GET  /api/v1/health     -> Basit sağlık kontrolü (kimlik doğrulaması yok)
 *
 * Ağ izolasyonu (Misafir VLAN engeli) firewall/VLAN katmanında yapılır; bu sunucu
 * yalnızca kendisine ulaşabilen isteklere yanıt verir (Spec Bölüm 4).
 */
class GatewayHttpServer(
    private val context: Context,
    port: Int
) : NanoHTTPD("0.0.0.0", port) {

    private val config = AppConfig.get(context)
    private val smsSender = SmsSender(context)
    private val logRepository = LogRepository(context)
    private val rateLimiter = RateLimiter(config.rateLimitWindowMs)
    private val json = Json { ignoreUnknownKeys = true }

    override fun serve(session: IHTTPSession): Response {
        return try {
            when {
                session.method == Method.GET && session.uri == "/api/v1/health" ->
                    ok(ApiResponse(true, "Gateway calisiyor.", nowUtc()))

                session.method == Method.POST && session.uri == "/api/v1/sms/send" ->
                    handleSendSms(session)

                else -> error(
                    Response.Status.NOT_FOUND,
                    "Endpoint bulunamadi: ${session.method} ${session.uri}"
                )
            }
        } catch (e: Exception) {
            Log.e(TAG, "Beklenmeyen sunucu hatasi", e)
            error(Response.Status.INTERNAL_ERROR, "Sunucu hatasi.")
        }
    }

    private fun handleSendSms(session: IHTTPSession): Response {
        // 1) Kimlik doğrulama — sabit zamanlı karşılaştırma (timing attack önleme).
        val providedKey = session.headers["x-api-key"]
        if (providedKey == null || !constantTimeEquals(providedKey, config.apiKey)) {
            return error(Response.Status.UNAUTHORIZED, "Gecersiz veya eksik API anahtari.")
        }

        // 2) Gövdeyi oku ve ayrıştır.
        val body = readBody(session)
        val request = try {
            json.decodeFromString<SendSmsRequest>(body)
        } catch (e: Exception) {
            return error(Response.Status.BAD_REQUEST, "Gecersiz JSON govdesi.")
        }

        // 3) Doğrulama.
        val phone = PhoneValidator.normalizeOrNull(request.phone)
            ?: return error(Response.Status.BAD_REQUEST, "Hatali telefon formati (E.164 bekleniyor).")
        val message = request.message?.takeIf { it.isNotBlank() }
            ?: return error(Response.Status.BAD_REQUEST, "Mesaj metni bos olamaz.")

        // 4) Rate limiting (numara bazlı cooldown).
        if (!rateLimiter.tryAcquire(phone)) {
            return error(
                Response.Status.TOO_MANY_REQUESTS,
                "Bu numaraya cok sik istek gonderildi, lutfen bekleyin."
            )
        }

        // 5) Gönder ve maskeli logla.
        val timestamp = nowUtc()
        return when (val result = smsSender.send(phone, message)) {
            is SmsSender.Result.Success -> {
                runBlocking { logRepository.logSuccess(phone, message, timestamp) }
                ok(ApiResponse(true, "SMS gonderim kuyruguna alindi.", timestamp))
            }
            is SmsSender.Result.Failure -> {
                runBlocking { logRepository.logFailure(phone, message, result.reason, timestamp) }
                Log.e(TAG, "SMS gonderilemedi: ${result.reason}")
                error(Response.Status.INTERNAL_ERROR, "SMS gonderilemedi (GSM/SIM hatasi).")
            }
        }
    }

    private fun readBody(session: IHTTPSession): String {
        val files = HashMap<String, String>()
        session.parseBody(files)
        // NanoHTTPD, application/json gövdesini "postData" anahtarına koyar.
        return files["postData"].orEmpty()
    }

    private fun ok(payload: ApiResponse): Response = newFixedLengthResponse(
        Response.Status.OK,
        "application/json",
        json.encodeToString(ApiResponse.serializer(), payload)
    )

    private fun error(status: Response.Status, message: String): Response = newFixedLengthResponse(
        status,
        "application/json",
        json.encodeToString(ApiResponse.serializer(), ApiResponse(false, message, nowUtc()))
    )

    private fun nowUtc(): String =
        DateTimeFormatter.ISO_INSTANT.format(Instant.now().truncatedTo(java.time.temporal.ChronoUnit.SECONDS))

    /** İki string'i uzunluk sızdırmadan, sabit zamanda karşılaştırır. */
    private fun constantTimeEquals(a: String, b: String): Boolean {
        val ba = a.toByteArray(Charsets.UTF_8)
        val bb = b.toByteArray(Charsets.UTF_8)
        var diff = ba.size xor bb.size
        for (i in ba.indices) {
            diff = diff or (ba[i].toInt() xor bb.getOrElse(i) { 0 }.toInt())
        }
        return diff == 0
    }

    companion object {
        private const val TAG = "GatewayHttpServer"
    }
}
