package com.slnmoda.smsgateway.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.lifecycle.LifecycleService
import androidx.lifecycle.lifecycleScope
import com.slnmoda.smsgateway.BuildConfig
import com.slnmoda.smsgateway.MainActivity
import com.slnmoda.smsgateway.R
import com.slnmoda.smsgateway.data.LogRepository
import com.slnmoda.smsgateway.server.GatewayHttpServer
import fi.iki.elonen.NanoHTTPD
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

/**
 * Kalıcı ön plan servisi. HTTP sunucusunu ayakta tutar, WakeLock ile CPU'nun
 * uyumasını engeller ve periyodik olarak eski logları temizler (KVKK).
 */
class GatewayService : LifecycleService() {

    private var server: GatewayHttpServer? = null
    private var wakeLock: PowerManager.WakeLock? = null

    override fun onCreate() {
        super.onCreate()
        startAsForeground()
        acquireWakeLock()
        startServer()
        purgeOldLogs()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        super.onStartCommand(intent, flags, startId)
        // Sistem servisi öldürürse yeniden başlatılsın.
        return START_STICKY
    }

    private fun startServer() {
        if (server != null) return
        try {
            server = GatewayHttpServer(applicationContext, BuildConfig.GATEWAY_PORT).apply {
                start(NanoHTTPD.SOCKET_READ_TIMEOUT, false)
            }
            Log.i(TAG, "Gateway sunucusu ${BuildConfig.GATEWAY_PORT} portunda baslatildi.")
        } catch (e: Exception) {
            Log.e(TAG, "HTTP sunucusu baslatilamadi", e)
            stopSelf()
        }
    }

    private fun startAsForeground() {
        val channelId = ensureChannel()
        val openApp = PendingIntent.getActivity(
            this, 0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE
        )
        val notification: Notification = NotificationCompat.Builder(this, channelId)
            .setContentTitle(getString(R.string.notif_title))
            .setContentText(getString(R.string.notif_text, BuildConfig.GATEWAY_PORT))
            .setSmallIcon(R.drawable.ic_gateway)
            .setOngoing(true)
            .setContentIntent(openApp)
            .setForegroundServiceBehavior(NotificationCompat.FOREGROUND_SERVICE_IMMEDIATE)
            .build()

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            startForeground(
                NOTIF_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE
            )
        } else {
            startForeground(NOTIF_ID, notification)
        }
    }

    private fun ensureChannel(): String {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val manager = getSystemService(NotificationManager::class.java)
            val channel = NotificationChannel(
                CHANNEL_ID,
                getString(R.string.notif_channel_name),
                NotificationManager.IMPORTANCE_LOW
            ).apply { description = getString(R.string.notif_channel_desc) }
            manager.createNotificationChannel(channel)
        }
        return CHANNEL_ID
    }

    private fun acquireWakeLock() {
        val pm = getSystemService(Context.POWER_SERVICE) as PowerManager
        wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, WAKELOCK_TAG).apply {
            setReferenceCounted(false)
            acquire()
        }
    }

    private fun purgeOldLogs() {
        lifecycleScope.launch(Dispatchers.IO) {
            runCatching {
                val removed = LogRepository(applicationContext).purgeExpired()
                if (removed > 0) Log.i(TAG, "$removed eski log kaydi temizlendi (KVKK).")
            }
        }
    }

    override fun onDestroy() {
        server?.stop()
        server = null
        wakeLock?.let { if (it.isHeld) it.release() }
        wakeLock = null
        Log.i(TAG, "Gateway servisi durduruldu.")
        super.onDestroy()
    }

    override fun onBind(intent: Intent): IBinder? {
        super.onBind(intent)
        return null
    }

    companion object {
        private const val TAG = "GatewayService"
        private const val CHANNEL_ID = "sms_gateway_channel"
        private const val NOTIF_ID = 1001
        private const val WAKELOCK_TAG = "SmsGateway::ServerWakeLock"

        fun start(context: Context) {
            val intent = Intent(context, GatewayService::class.java)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }

        fun stop(context: Context) {
            context.stopService(Intent(context, GatewayService::class.java))
        }
    }
}
