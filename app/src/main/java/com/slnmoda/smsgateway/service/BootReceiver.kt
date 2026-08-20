package com.slnmoda.smsgateway.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/**
 * Cihaz yeniden başladığında Gateway servisini otomatik ayağa kaldırır.
 * Spec Bölüm 5: servis kesintisiz çalışmalı.
 */
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        when (intent.action) {
            Intent.ACTION_BOOT_COMPLETED,
            "android.intent.action.QUICKBOOT_POWERON" -> GatewayService.start(context)
        }
    }
}
