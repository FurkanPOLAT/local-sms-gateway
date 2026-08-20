package com.slnmoda.smsgateway

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.wifi.WifiManager
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import com.slnmoda.smsgateway.config.AppConfig
import com.slnmoda.smsgateway.databinding.ActivityMainBinding
import com.slnmoda.smsgateway.service.GatewayService
import java.net.Inet4Address
import java.net.NetworkInterface

/**
 * Yönetim ekranı: izinleri ister, servisi başlatıp durdurur, cihazın yerel
 * erişim adresini (IP:port) ve API anahtarını operatöre gösterir.
 */
class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding
    private lateinit var config: AppConfig

    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { result ->
        if (result[Manifest.permission.SEND_SMS] == true) {
            GatewayService.start(this)
            refreshStatus()
        } else {
            Toast.makeText(this, R.string.err_sms_permission, Toast.LENGTH_LONG).show()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)
        config = AppConfig.get(this)

        binding.btnStart.setOnClickListener { requestPermissionsAndStart() }
        binding.btnStop.setOnClickListener {
            GatewayService.stop(this)
            refreshStatus()
        }
        binding.btnBattery.setOnClickListener { openBatterySettings() }
        binding.btnRegenerateKey.setOnClickListener { regenerateApiKey() }

        refreshStatus()
    }

    private fun requestPermissionsAndStart() {
        val needed = buildList {
            add(Manifest.permission.SEND_SMS)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                add(Manifest.permission.POST_NOTIFICATIONS)
            }
        }.filter {
            ContextCompat.checkSelfPermission(this, it) != PackageManager.PERMISSION_GRANTED
        }

        if (needed.isEmpty()) {
            GatewayService.start(this)
            refreshStatus()
        } else {
            permissionLauncher.launch(needed.toTypedArray())
        }
    }

    @SuppressLint("BatteryLife")
    private fun openBatterySettings() {
        // Pil optimizasyonundan muaf tutma ekranını açar (Spec Bölüm 5).
        val intent = Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS).apply {
            data = android.net.Uri.parse("package:$packageName")
        }
        runCatching { startActivity(intent) }
    }

    private fun regenerateApiKey() {
        val bytes = ByteArray(32).also { java.security.SecureRandom().nextBytes(it) }
        config.apiKey = bytes.joinToString("") { "%02x".format(it) }
        Toast.makeText(this, R.string.msg_key_regenerated, Toast.LENGTH_SHORT).show()
        refreshStatus()
    }

    private fun refreshStatus() {
        val ip = localIpAddress() ?: "IP alinamadi"
        binding.txtEndpoint.text = getString(
            R.string.status_endpoint, ip, BuildConfig.GATEWAY_PORT
        )
        binding.txtApiKey.text = getString(R.string.status_api_key, config.apiKey)
    }

    /** Cihazın Wi-Fi/yerel ağ IPv4 adresini bulur. */
    private fun localIpAddress(): String? {
        // Önce Wi-Fi arayüzünü dene.
        NetworkInterface.getNetworkInterfaces()?.toList()?.forEach { nif ->
            if (!nif.isUp || nif.isLoopback) return@forEach
            nif.inetAddresses.toList().forEach { addr ->
                if (addr is Inet4Address && !addr.isLoopbackAddress) {
                    return addr.hostAddress
                }
            }
        }
        // Yedek: WifiManager
        @Suppress("DEPRECATION")
        val wifi = applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
        val ipInt = wifi?.connectionInfo?.ipAddress ?: return null
        if (ipInt == 0) return null
        return "%d.%d.%d.%d".format(
            ipInt and 0xff, ipInt shr 8 and 0xff, ipInt shr 16 and 0xff, ipInt shr 24 and 0xff
        )
    }
}
