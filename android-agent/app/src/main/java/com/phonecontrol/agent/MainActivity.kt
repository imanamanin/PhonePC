package com.phonecontrol.agent

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjectionManager
import android.os.Bundle
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import com.phonecontrol.agent.databinding.ActivityMainBinding
import com.phonecontrol.agent.domain.ProtocolPorts
import com.phonecontrol.agent.permissions.StaticPermissionReader
import com.phonecontrol.agent.screencapture.ScreenCaptureController
import com.phonecontrol.agent.screencapture.ScreenCaptureService
import com.phonecontrol.agent.settings.PermissionGuide

class MainActivity : AppCompatActivity() {
    private val captureLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        val data = result.data
        if (result.resultCode == Activity.RESULT_OK && data != null) {
            ScreenCaptureController.instance.onUserGrantedProjection()
            ScreenCaptureService.start(this, result.resultCode, data)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val permissions = StaticPermissionReader().snapshot()
        val guide = PermissionGuide().items().joinToString("\n") { "- ${it.title}" }

        binding.titleText.text = getString(R.string.app_name)
        binding.phaseText.text = getString(R.string.phase_three)
        binding.statusText.text = getString(
            R.string.status_body,
            ProtocolPorts.CONTROL,
            ProtocolPorts.SCREEN,
            permissions.accessibility.name,
            permissions.notificationListener.name,
            guide
        )
        renderPairing(binding)
        binding.refreshPinButton.setOnClickListener {
            AgentApp.runtime?.engine?.ensureChallenge()
            renderPairing(binding)
        }
        binding.unpairButton.setOnClickListener {
            AgentApp.runtime?.engine?.clear()
            AgentApp.runtime?.store?.clear()
            renderPairing(binding)
        }
        binding.captureButton.setOnClickListener { requestCapture() }
        binding.stopCaptureButton.setOnClickListener {
            startService(Intent(this, ScreenCaptureService::class.java).setAction(ScreenCaptureService.ACTION_STOP))
        }
    }

    private fun renderPairing(binding: ActivityMainBinding) {
        val engine = AgentApp.runtime?.engine
        val pin = engine?.displayedPin()
        val sas = engine?.lastSas
        binding.pinText.text = pin ?: "------"
        binding.sasText.text = when {
            sas != null -> "Numeric comparison: $sas (must match Windows)"
            engine?.isPaired() == true -> "Trusted PC stored in EncryptedSharedPreferences"
            else -> "Show this PIN on Windows. The PIN is not written to logcat."
        }
    }

    private fun requestCapture() {
        val mgr = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        captureLauncher.launch(mgr.createScreenCaptureIntent())
    }
}
