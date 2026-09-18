package com.phonecontrol.agent.network

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.net.wifi.WifiManager
import com.phonecontrol.agent.domain.ProtocolPorts
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Advertises the browser bridge on the LAN so a PC on Wi-Fi can find the phone.
 * Does not change USB tethering.
 */
class LanAdvertiser {
    private val started = AtomicBoolean(false)
    private var nsd: NsdManager? = null
    private var multicast: WifiManager.MulticastLock? = null
    private var wifiLock: WifiManager.WifiLock? = null
    private var listener: NsdManager.RegistrationListener? = null

    fun start(context: Context) {
        if (!started.compareAndSet(false, true)) {
            return
        }
        val app = context.applicationContext
        try {
            val wifi = app.getSystemService(Context.WIFI_SERVICE) as WifiManager
            val lock = wifi.createMulticastLock("pc-phone-nsd")
            lock.setReferenceCounted(false)
            lock.acquire()
            multicast = lock
            val stay = if (android.os.Build.VERSION.SDK_INT >= 29) {
                wifi.createWifiLock(WifiManager.WIFI_MODE_FULL_LOW_LATENCY, "pc-phone-wifi")
            } else {
                @Suppress("DEPRECATION")
                wifi.createWifiLock(WifiManager.WIFI_MODE_FULL_HIGH_PERF, "pc-phone-wifi")
            }
            stay.setReferenceCounted(false)
            stay.acquire()
            wifiLock = stay
        } catch (_: Exception) {
            // Discovery still works via subnet scan.
        }
        try {
            val manager = app.getSystemService(Context.NSD_SERVICE) as NsdManager
            val info = NsdServiceInfo().apply {
                serviceName = "PC Phone"
                serviceType = "_pcphone._tcp."
                port = ProtocolPorts.BROWSER
            }
            val registration = object : NsdManager.RegistrationListener {
                override fun onServiceRegistered(serviceInfo: NsdServiceInfo) = Unit
                override fun onRegistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) = Unit
                override fun onServiceUnregistered(serviceInfo: NsdServiceInfo) = Unit
                override fun onUnregistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) = Unit
            }
            listener = registration
            nsd = manager
            manager.registerService(info, NsdManager.PROTOCOL_DNS_SD, registration)
        } catch (_: Exception) {
            started.set(false)
        }
    }

    companion object {
        val instance = LanAdvertiser()
    }
}
