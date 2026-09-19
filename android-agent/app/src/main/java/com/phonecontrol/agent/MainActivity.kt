package com.phonecontrol.agent

import android.app.Activity
import android.content.ClipDescription
import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjectionManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.view.DragEvent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.content.IntentCompat
import androidx.core.os.LocaleListCompat
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.updatePadding
import com.phonecontrol.agent.databinding.ActivityMainBinding
import com.phonecontrol.agent.domain.ClientPresence
import com.phonecontrol.agent.domain.LocalAddresses
import com.phonecontrol.agent.files.PhoneFiles
import com.phonecontrol.agent.network.BrowserBridgeServer
import com.phonecontrol.agent.network.ControlServer
import com.phonecontrol.agent.network.WifiStaAddress
import com.phonecontrol.agent.screencapture.ScreenCaptureController
import com.phonecontrol.agent.screencapture.ScreenCaptureService
import com.phonecontrol.agent.ui.SoftChimes

class MainActivity : AppCompatActivity() {
    private lateinit var binding: ActivityMainBinding
    private val presenceListener: (Boolean) -> Unit = { connected ->
        if (connected) SoftChimes.connect() else SoftChimes.disconnect()
    }
    private val fileStatusListener: (String) -> Unit = { text ->
        if (::binding.isInitialized) {
            binding.fileStatusText.text = text
        }
    }

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

    private val pickFiles = registerForActivityResult(
        ActivityResultContracts.OpenMultipleDocuments()
    ) { uris ->
        PhoneFiles.sendUris(this, uris)
    }

    private val writePermission = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        if (granted || Build.VERSION.SDK_INT >= 29) {
            pickFiles.launch(arrayOf("*/*"))
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        ViewCompat.setOnApplyWindowInsetsListener(binding.contentScroll) { view, insets ->
            val bars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            val pad = resources.getDimensionPixelSize(R.dimen.pc_space_lg)
            view.updatePadding(
                left = bars.left,
                top = bars.top + pad,
                right = bars.right,
                bottom = bars.bottom + pad
            )
            insets
        }
        bindSplash(savedInstanceState)
        ClientPresence.addListener(presenceListener)
        PhoneFiles.addStatusListener(fileStatusListener)

        AgentApp.startControlListener()
        bindLanguageToggle()
        renderStatus()
        bindFileDrop()
        consumeShare(intent)
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
        binding.sendFileButton.setOnClickListener { pickAndSend() }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        consumeShare(intent)
    }

    override fun onDestroy() {
        PhoneFiles.removeStatusListener(fileStatusListener)
        ClientPresence.removeListener(presenceListener)
        super.onDestroy()
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
        val listening = ControlServer.instance.isListening() || BrowserBridgeServer.instance.isListening()
        binding.listenText.text = if (listening) {
            getString(R.string.status_listening)
        } else {
            getString(R.string.status_offline)
        }
        val accent = androidx.core.content.ContextCompat.getColor(
            this,
            if (listening) R.color.pcphone_turquoise else R.color.pcphone_orange
        )
        binding.listenText.setTextColor(accent)
        binding.connectionCard.strokeColor = accent
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

    private fun bindSplash(savedInstanceState: Bundle?) {
        if (savedInstanceState != null) {
            binding.splashOverlay.visibility = android.view.View.GONE
            return
        }
        binding.splashOverlay.onFinished = {
            binding.splashOverlay.animate()
                .alpha(0f)
                .setDuration(280)
                .withEndAction {
                    binding.splashOverlay.visibility = android.view.View.GONE
                }
                .start()
        }
    }

    private fun pickAndSend() {
        if (Build.VERSION.SDK_INT < 29 &&
            checkSelfPermission(android.Manifest.permission.WRITE_EXTERNAL_STORAGE) !=
            android.content.pm.PackageManager.PERMISSION_GRANTED
        ) {
            writePermission.launch(android.Manifest.permission.WRITE_EXTERNAL_STORAGE)
            return
        }
        pickFiles.launch(arrayOf("*/*"))
    }

    private fun bindFileDrop() {
        binding.root.setOnDragListener { _, event ->
            when (event.action) {
                DragEvent.ACTION_DRAG_STARTED ->
                    event.clipDescription?.hasMimeType(ClipDescription.MIMETYPE_TEXT_URILIST) == true ||
                        event.clipDescription?.hasMimeType("*/*") == true
                DragEvent.ACTION_DROP -> {
                    val uris = mutableListOf<Uri>()
                    val clip = event.clipData ?: return@setOnDragListener false
                    for (index in 0 until clip.itemCount) {
                        clip.getItemAt(index).uri?.let(uris::add)
                    }
                    if (uris.isEmpty()) return@setOnDragListener false
                    PhoneFiles.sendUris(this, uris)
                    true
                }
                else -> true
            }
        }
    }

    private fun consumeShare(intent: Intent?) {
        if (intent == null) return
        val uris = mutableListOf<Uri>()
        when (intent.action) {
            Intent.ACTION_SEND -> {
                IntentCompat.getParcelableExtra(intent, Intent.EXTRA_STREAM, Uri::class.java)?.let(uris::add)
            }
            Intent.ACTION_SEND_MULTIPLE -> {
                IntentCompat.getParcelableArrayListExtra(intent, Intent.EXTRA_STREAM, Uri::class.java)
                    ?.let(uris::addAll)
            }
        }
        if (uris.isNotEmpty()) {
            PhoneFiles.sendUris(this, uris)
        }
    }
}
