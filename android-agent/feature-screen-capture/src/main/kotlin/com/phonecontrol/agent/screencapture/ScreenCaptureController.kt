package com.phonecontrol.agent.screencapture

/** MediaProjection capture starts only after the system consent dialog returns RESULT_OK. */
class ScreenCaptureController {
    var running: Boolean = false
        private set

    fun onUserGrantedProjection() {
        running = true
    }

    fun stop() {
        running = false
    }

    companion object {
        val instance = ScreenCaptureController()
    }
}
