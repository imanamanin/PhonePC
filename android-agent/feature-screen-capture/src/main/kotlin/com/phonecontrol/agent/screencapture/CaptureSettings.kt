package com.phonecontrol.agent.screencapture

/**
 * Adaptive quality requested by Windows over the control channel.
 * Applied as bitrate immediately; width/fps on the next encoder start.
 * Never starts MediaProjection by itself.
 */
object CaptureSettings {
    @Volatile
    var maxFps: Int = 24

    @Volatile
    var maxWidth: Int = 720

    @Volatile
    var bitrateKbps: Int = 2000

    fun update(maxFps: Int, maxWidth: Int, bitrateKbps: Int) {
        this.maxFps = maxFps.coerceIn(5, 60)
        this.maxWidth = maxWidth.coerceIn(240, 1920)
        this.bitrateKbps = bitrateKbps.coerceIn(200, 8000)
    }
}
