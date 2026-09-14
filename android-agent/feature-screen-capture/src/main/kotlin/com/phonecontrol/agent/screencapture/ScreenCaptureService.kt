package com.phonecontrol.agent.screencapture

import android.app.Activity
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.hardware.display.DisplayManager
import android.hardware.display.VirtualDisplay
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.os.Build
import android.os.IBinder
import android.view.Surface
import androidx.core.app.NotificationCompat
import com.phonecontrol.agent.domain.ProtocolPorts
import com.phonecontrol.agent.domain.VideoPacket
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.concurrent.thread

class ScreenCaptureService : Service() {
    private var projection: MediaProjection? = null
    private var display: VirtualDisplay? = null
    private var encoder: VideoEncoder? = null
    private var inputSurface: Surface? = null
    private var serverThread: Thread? = null
    private var serverSocket: ServerSocket? = null
    private val running = AtomicBoolean(false)

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) {
            stopCapture()
            stopSelf()
            return START_NOT_STICKY
        }

        val resultCode = intent?.getIntExtra(EXTRA_RESULT_CODE, 0) ?: 0
        val data = if (Build.VERSION.SDK_INT >= 33) {
            intent?.getParcelableExtra(EXTRA_RESULT_DATA, Intent::class.java)
        } else {
            @Suppress("DEPRECATION")
            intent?.getParcelableExtra(EXTRA_RESULT_DATA)
        }
        if (resultCode != Activity.RESULT_OK || data == null) {
            stopSelf()
            return START_NOT_STICKY
        }

        startForegroundNotification()
        val mgr = getSystemService(MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        projection = mgr.getMediaProjection(resultCode, data)
        if (projection == null) {
            stopSelf()
            return START_NOT_STICKY
        }
        projection?.registerCallback(object : MediaProjection.Callback() {
            override fun onStop() {
                stopCapture()
                stopSelf()
            }
        }, null)
        ScreenCaptureController.instance.onUserGrantedProjection()
        val height = (CaptureSettings.maxWidth * 16 / 9).coerceAtMost(1920)
        startEncoder(CaptureSettings.maxWidth, height, CaptureSettings.maxFps, CaptureSettings.bitrateKbps)
        return START_STICKY
    }

    private fun startEncoder(width: Int, height: Int, fps: Int, bitrateKbps: Int) {
        val h264 = MediaCodecH264Encoder()
        val surface = h264.configure(width, height, fps, bitrateKbps) ?: return
        encoder = h264
        inputSurface = surface
        display = projection?.createVirtualDisplay(
            "phone-control",
            width,
            height,
            resources.displayMetrics.densityDpi,
            DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
            surface,
            null,
            null
        )
        running.set(true)
        serverThread = thread(name = "video-17891", isDaemon = true) { serve(h264, width, height) }
    }

    private fun serve(encoder: VideoEncoder, width: Int, height: Int) {
        val server = ServerSocket()
        server.reuseAddress = true
        server.bind(InetSocketAddress(ProtocolPorts.SCREEN))
        serverSocket = server
        server.use {
            while (running.get()) {
                encoder.applyBitrate(CaptureSettings.bitrateKbps)
                val socket = try {
                    server.accept()
                } catch (_: Exception) {
                    break
                }
                socket.use { client ->
                    val out = client.getOutputStream()
                    while (running.get()) {
                        encoder.applyBitrate(CaptureSettings.bitrateKbps)
                        val nals = encoder.drain()
                        for (nal in nals) {
                            var flags = 0
                            if (nal.keyframe) flags = flags or VideoPacket.KEYFRAME
                            if (nal.config) flags = flags or VideoPacket.CONFIG
                            val packet = VideoPacket.encode(
                                VideoPacket(
                                    codec = VideoPacket.H264,
                                    width = width,
                                    height = height,
                                    captureTimestampMs = System.currentTimeMillis(),
                                    flags = flags,
                                    payload = nal.bytes
                                )
                            )
                            writeFrame(out, packet)
                        }
                        Thread.sleep(5)
                    }
                }
            }
        }
    }

    private fun writeFrame(out: java.io.OutputStream, body: ByteArray) {
        if (body.size > ProtocolPorts.MAX_VIDEO_BYTES) return
        val header = ByteArray(4)
        val size = body.size
        header[0] = ((size ushr 24) and 0xFF).toByte()
        header[1] = ((size ushr 16) and 0xFF).toByte()
        header[2] = ((size ushr 8) and 0xFF).toByte()
        header[3] = (size and 0xFF).toByte()
        out.write(header)
        out.write(body)
        out.flush()
    }

    private fun startForegroundNotification() {
        val channelId = "capture"
        if (Build.VERSION.SDK_INT >= 26) {
            val mgr = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
            mgr.createNotificationChannel(
                NotificationChannel(channelId, "Screen capture", NotificationManager.IMPORTANCE_LOW)
            )
        }
        val notification = NotificationCompat.Builder(this, channelId)
            .setContentTitle("Phone Control Agent")
            .setContentText("Screen capture is active. Stop from the app or this notification.")
            .setSmallIcon(android.R.drawable.ic_menu_camera)
            .build()
        if (Build.VERSION.SDK_INT >= 29) {
            startForeground(42, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION)
        } else {
            startForeground(42, notification)
        }
    }

    private fun stopCapture() {
        running.set(false)
        try {
            serverSocket?.close()
        } catch (_: Exception) {
            // Unblock accept.
        }
        serverSocket = null
        display?.release()
        display = null
        inputSurface?.release()
        inputSurface = null
        encoder?.release()
        encoder = null
        try {
            projection?.stop()
        } catch (_: Exception) {
            // Already stopped.
        }
        projection = null
        ScreenCaptureController.instance.stop()
    }

    override fun onDestroy() {
        stopCapture()
        super.onDestroy()
    }

    companion object {
        const val EXTRA_RESULT_CODE = "resultCode"
        const val EXTRA_RESULT_DATA = "resultData"
        const val ACTION_STOP = "com.phonecontrol.agent.STOP_CAPTURE"

        fun start(context: Context, resultCode: Int, data: Intent) {
            val intent = Intent(context, ScreenCaptureService::class.java)
                .putExtra(EXTRA_RESULT_CODE, resultCode)
                .putExtra(EXTRA_RESULT_DATA, data)
            if (Build.VERSION.SDK_INT >= 26) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }
    }
}
