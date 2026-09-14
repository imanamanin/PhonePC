package com.phonecontrol.agent.devicestatus

import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.BatteryManager
import android.util.DisplayMetrics
import android.view.WindowManager
import com.phonecontrol.agent.domain.DeviceStatusFields

class DeviceStatusReader(private val context: Context? = null) {
    fun placeholder(): DeviceStatusSnapshot = DeviceStatusSnapshot(
        batteryPercent = null,
        charging = null,
        model = "unknown",
        androidVersion = "unknown",
        sdkInt = 0,
        screenWidth = 0,
        screenHeight = 0
    )

    fun read(): DeviceStatusFields {
        val ctx = context ?: return DeviceStatusFields(null, null, "unknown")
        val intent = ctx.registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
        val level = intent?.getIntExtra(BatteryManager.EXTRA_LEVEL, -1) ?: -1
        val scale = intent?.getIntExtra(BatteryManager.EXTRA_SCALE, 100) ?: 100
        val percent = if (level >= 0 && scale > 0) (level * 100) / scale else null
        val status = intent?.getIntExtra(BatteryManager.EXTRA_STATUS, -1) ?: -1
        val charging = status == BatteryManager.BATTERY_STATUS_CHARGING ||
            status == BatteryManager.BATTERY_STATUS_FULL
        val metrics = DisplayMetrics()
        val wm = ctx.getSystemService(Context.WINDOW_SERVICE) as WindowManager
        @Suppress("DEPRECATION")
        wm.defaultDisplay.getMetrics(metrics)
        return DeviceStatusFields(
            batteryPercent = percent,
            charging = charging,
            network = "usb_tether",
            screenWidth = metrics.widthPixels,
            screenHeight = metrics.heightPixels,
            rotation = 0
        )
    }
}

data class DeviceStatusSnapshot(
    val batteryPercent: Int?,
    val charging: Boolean?,
    val model: String,
    val androidVersion: String,
    val sdkInt: Int,
    val screenWidth: Int,
    val screenHeight: Int
)
