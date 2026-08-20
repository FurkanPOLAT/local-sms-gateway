package com.slnmoda.smsgateway.server.dto

import kotlinx.serialization.Serializable

/** POST /api/v1/sms/send istek gövdesi. */
@Serializable
data class SendSmsRequest(
    val phone: String? = null,
    val message: String? = null
)

/** Standart API yanıt zarfı. */
@Serializable
data class ApiResponse(
    val success: Boolean,
    val message: String,
    val timestamp: String
)
