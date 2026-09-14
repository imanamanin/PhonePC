package com.phonecontrol.agent

import android.app.Application
import android.os.Build
import com.phonecontrol.agent.accessibility.InputInjector
import com.phonecontrol.agent.data.EncryptedPairingStore
import com.phonecontrol.agent.data.PairingStore
import com.phonecontrol.agent.devicestatus.DeviceStatusReader
import com.phonecontrol.agent.domain.AgentCommandSink
import com.phonecontrol.agent.domain.DeviceStatusFields
import com.phonecontrol.agent.domain.HelloInfo
import com.phonecontrol.agent.domain.PairingEngine
import com.phonecontrol.agent.domain.PinSubmitResult
import com.phonecontrol.agent.domain.TouchCommand
import com.phonecontrol.agent.network.ControlServer
import com.phonecontrol.agent.screencapture.CaptureSettings
import com.phonecontrol.agent.screencapture.ScreenCaptureController
import com.phonecontrol.agent.screencapture.ScreenCaptureService

class AgentApp : Application() {
    override fun onCreate() {
        super.onCreate()
        val store = EncryptedPairingStore(this)
        val engine = PairingEngine(nowMs = { System.currentTimeMillis() })
        store.load()?.let { engine.restore(it) }
        runtime = AgentRuntime(engine, store, DeviceStatusReader(this))
        ControlServer.instance.start(AppCommandSink(this, runtime!!))
    }

    companion object {
        var runtime: AgentRuntime? = null
    }
}

class AgentRuntime(
    val engine: PairingEngine,
    val store: PairingStore,
    val status: DeviceStatusReader
)

private class AppCommandSink(
    private val app: Application,
    private val runtime: AgentRuntime
) : AgentCommandSink {
    override fun hello(): HelloInfo = HelloInfo(
        model = Build.MODEL ?: "unknown",
        manufacturer = Build.MANUFACTURER ?: "unknown",
        androidVersion = Build.VERSION.RELEASE ?: "unknown",
        sdkInt = Build.VERSION.SDK_INT,
        serialHash = "none"
    )

    override fun evaluateHello(pairingId: String?, sessionToken: String?): Boolean =
        runtime.engine.validateToken(pairingId, sessionToken)

    override fun submitPin(pin: String): PinSubmitResult {
        val result = runtime.engine.submitPin(pin)
        if (result is PinSubmitResult.Accepted) {
            runtime.store.save(result.pairing)
        }
        return result
    }

    override fun clearPairing() {
        runtime.engine.clear()
        runtime.store.clear()
    }

    override fun isPaired(): Boolean = runtime.engine.isPaired()

    override fun displayedPin(): String? = runtime.engine.displayedPin()

    override fun displayedSas(): String? = runtime.engine.lastSas

    override fun deviceStatus(): DeviceStatusFields = runtime.status.read()

    override fun isCaptureGranted(): Boolean = ScreenCaptureController.instance.running

    override fun adapt(maxFps: Int, maxWidth: Int, bitrateKbps: Int) {
        CaptureSettings.update(maxFps, maxWidth, bitrateKbps)
    }

    override fun requestStopCapture() {
        app.startService(
            android.content.Intent(app, ScreenCaptureService::class.java)
                .setAction(ScreenCaptureService.ACTION_STOP)
        )
    }

    override fun injectTouch(command: TouchCommand): Boolean = InputInjector.touch(command)

    override fun injectKey(key: String): Boolean = InputInjector.key(key)
}
