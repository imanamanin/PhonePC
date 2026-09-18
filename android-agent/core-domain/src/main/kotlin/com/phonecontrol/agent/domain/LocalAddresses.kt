package com.phonecontrol.agent.domain

import java.net.Inet4Address
import java.net.NetworkInterface

data class LanHost(
    val host: String,
    val iface: String
) {
    val kind: String
        get() {
            val n = iface.lowercase()
            if (n.contains("rndis") || n.contains("usb") || n.contains("ncm") || n.contains("tether")) {
                return "usb"
            }
            if (n.contains("wlan") || n.contains("wifi") || n.contains("ap0") || n.contains("swlan")) {
                return "wifi"
            }
            if (host.startsWith("192.168.42.") || host.startsWith("192.168.137.")) {
                return "usb"
            }
            return "lan"
        }
}

object LocalAddresses {
    fun hosts(): List<LanHost> {
        val found = ArrayList<LanHost>()
        try {
            val ifaces = NetworkInterface.getNetworkInterfaces() ?: return found
            for (nic in ifaces) {
                if (!nic.isUp || nic.isLoopback) continue
                val name = nic.name ?: "net"
                for (addr in nic.inetAddresses) {
                    if (addr !is Inet4Address || addr.isLoopbackAddress) continue
                    val host = addr.hostAddress ?: continue
                    found.add(LanHost(host, name))
                }
            }
        } catch (_: Exception) {
            // Diagnostic only.
        }
        return found
    }

    fun ipv4(): List<String> = hosts().map { it.host }.distinct()

    fun wifi(): List<String> = hosts().filter { it.kind == "wifi" }.map { it.host }

    fun usb(): List<String> = hosts().filter { it.kind == "usb" }.map { it.host }
}
