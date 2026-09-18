package com.phonecontrol.agent.network

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import com.phonecontrol.agent.domain.LocalAddresses
import java.net.Inet4Address

object WifiStaAddress {
    fun ipv4(context: Context): String? {
        val cm = context.getSystemService(Context.CONNECTIVITY_SERVICE) as? ConnectivityManager ?: return null
        try {
            for (network in cm.allNetworks) {
                val caps = cm.getNetworkCapabilities(network) ?: continue
                if (!caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)) continue
                val props = cm.getLinkProperties(network) ?: continue
                for (link in props.linkAddresses) {
                    val addr = link.address
                    if (addr is Inet4Address && !addr.isLoopbackAddress) {
                        return addr.hostAddress
                    }
                }
            }
        } catch (_: Exception) {
            // Fall through.
        }
        return LocalAddresses.wifi().firstOrNull()
    }
}
