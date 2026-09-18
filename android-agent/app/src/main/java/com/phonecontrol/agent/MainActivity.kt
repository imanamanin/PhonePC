package com.phonecontrol.agent

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjectionManager
import android.os.Bundle
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.updatePadding
import com.phonecontrol.agent.databinding.ActivityMainBinding
import com.phonecontrol.agent.domain.LocalAddresses
import com.phonecontrol.agent.network.BrowserBridgeServer
import com.phonecontrol.agent.network.ControlServer
import com.phonecontrol.agent.network.WifiStaAddress
import com.phonecontrol.agent.screencapture.ScreenCaptureController
import com.phonecontrol.agent.screencapture.ScreenCaptureService

class MainActivity : AppCompatActivity() {
    private lateinit var binding: ActivityMainBinding

    private val captureLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        val data = result.data
        if (result.resultCode == Activity.RESULT_OK && data != null) {
            ScreenCaptureController.instance.onUserGrantedProjection()
            ScreenCaptureService.start(this, result.resultCode, data)
        }
    }

    private val audioPermission = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) {
        launchCaptureDialog()
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        val pad = (12 * resources.displayMetrics.density).toInt()
        ViewCompat.setOnApplyWindowInsetsListener(binding.root) { view, insets ->
            val bars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            view.updatePadding(
                left = bars.left + pad,
                top = bars.top + pad,
                right = bars.right + pad,
                bottom = bars.bottom + pad
            )
            insets
        }

        AgentApp.startControlListener()
        bindLanguageToggle()
        renderStatus()
        binding.refreshPinButton.setOnClickListener {
            AgentApp.runtime?.engine?.ensureChallenge()
            renderStatus()
        }
        binding.refreshPinButton.visibility = android.view.View.GONE
        binding.unpairButton.setOnClickListener {
            AgentApp.runtime?.engine?.clear()
            AgentApp.runtime?.store?.clear()
            renderStatus()
        }
        binding.accessibilityButton.setOnClickListener {
            startActivity(Intent(android.provider.Settings.ACTION_ACCESSIBILITY_SETTINGS))
        }
        binding.captureButton.setOnClickListener { requestCapture() }
        binding.stopCaptureButton.setOnClickListener {
            startService(Intent(this, ScreenCaptureService::class.java).setAction(ScreenCaptureService.ACTION_STOP))
        }
    }

    override fun onResume() {
        super.onResume()
        AgentApp.startControlListener()
        if (::binding.isInitialized) {
            renderStatus()
        }
    }

    private fun bindLanguageToggle() {
        when (currentLanguage()) {
            "fa" -> binding.langGroup.check(R.id.langFa)
            "ar" -> binding.langGroup.check(R.id.langAr)
            else -> binding.langGroup.check(R.id.langEn)
        }
        binding.langFa.setOnClickListener { setLanguage("fa") }
        binding.langEn.setOnClickListener { setLanguage("en") }
        binding.langAr.setOnClickListener { setLanguage("ar") }
    }

    private fun currentLanguage(): String {
        val locales = AppCompatDelegate.getApplicationLocales()
        val tag = locales[0]?.language
        if (!tag.isNullOrBlank()) {
            return tag
        }
        return java.util.Locale.getDefault().language
    }

    private fun setLanguage(tag: String) {
        if (currentLanguage() == tag) {
            return
        }
        AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags(tag))
    }

    private fun renderStatus() {
        val wifi = WifiStaAddress.ipv4(this)
        val usb = LocalAddresses.usb().firstOrNull()
        binding.pinText.text = wifi ?: getString(R.string.status_wifi_off)
        binding.sasText.text = buildString {
            append(getString(R.string.status_wifi_label))
            append(wifi ?: "—")
            append("\n")
            append(getString(R.string.status_usb_label))
            append(usb ?: "—")
        }
        binding.listenText.text = if (ControlServer.instance.isListening() || BrowserBridgeServer.instance.isListening()) {
            getString(R.string.status_listening)
        } else {
            getString(R.string.status_offline)
        }
    }

    private fun requestCapture() {
        if (checkSelfPermission(android.Manifest.permission.RECORD_AUDIO) !=
            android.content.pm.PackageManager.PERMISSION_GRANTED
        ) {
            audioPermission.launch(android.Manifest.permission.RECORD_AUDIO)
            return
        }
        launchCaptureDialog()
    }

    private fun launchCaptureDialog() {
        val mgr = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        captureLauncher.launch(mgr.createScreenCaptureIntent())
    }
}
