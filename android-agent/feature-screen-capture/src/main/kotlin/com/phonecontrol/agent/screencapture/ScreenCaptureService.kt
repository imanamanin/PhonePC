package com.phonecontrol.agent.screencapture

import android.app.Activity
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.graphics.Bitmap
import android.graphics.PixelFormat
import android.hardware.display.DisplayManager
import android.hardware.display.VirtualDisplay
import android.media.Image
import android.media.ImageReader
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.os.Build
import android.os.Handler
import android.os.HandlerThread
import android.os.IBinder
import androidx.core.app.NotificationCompat
import com.phonecontrol.agent.domain.ProtocolPorts
import com.phonecontrol.agent.domain.StreamHub
import com.phonecontrol.agent.domain.VideoPacket
import java.io.ByteArrayOutputStream
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong
import java.util.concurrent.atomic.AtomicReference
import kotlin.concurrent.thread

/**
 * Channel C on TCP 17891. JPEG is the live codec because Windows already decodes it with WIC.
 * Capture still starts only after the system MediaProjection dialog.
 */
class ScreenCaptureService : Service() {
    private var projection: MediaProjection? = null
    private var display: VirtualDisplay? = null
    private var imageReader: ImageReader? = null
    private var imageThread: HandlerThread? = null
    private var serverThread: Thread? = null
    private var serverSocket: ServerSocket? = null
    private val audio = PlaybackAudioCapture()
    private val running = AtomicBoolean(false)
    private val latestJpeg = AtomicReference<ByteArray?>()
    private val jpegSeq = AtomicLong(0)
    private var lastJpegMs = 0L
    private var frameWidth = 0
    private var frameHeight = 0

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
        val metrics = resources.displayMetrics
        val srcW = metrics.widthPixels.coerceAtLeast(2)
        val srcH = metrics.heightPixels.coerceAtLeast(2)
        val width = (CaptureSettings.maxWidth and 1.inv()).coerceAtLeast(2)
        val height = ((width.toLong() * srcH / srcW).toInt() and 1.inv()).coerceAtLeast(2)
        startReader(width, height)
        return START_STICKY
    }

    private fun startReader(width: Int, height: Int) {
        frameWidth = width
        frameHeight = height
        val thread = HandlerThread("jpeg-17891")
        thread.start()
        imageThread = thread
        val reader = ImageReader.newInstance(width, height, PixelFormat.RGBA_8888, 3)
        imageReader = reader
        reader.setOnImageAvailableListener({ pending ->
            val image = try {
                pending.acquireLatestImage()
            } catch (_: Exception) {
                null
            } ?: return@setOnImageAvailableListener
            image.use { encodeLatest(it) }
        }, Handler(thread.looper))
        display = projection?.createVirtualDisplay(
            "phone-control",
            width,
            height,
            resources.displayMetrics.densityDpi,
            DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
            reader.surface,
            null,
            null
        )
        running.set(true)
        thread(name = "media-hub", isDaemon = true) { pumpHub() }
        serverThread = thread(name = "video-17891", isDaemon = true) { serve() }
        projection?.let { audio.start(this, it) }
    }

    private fun encodeLatest(image: Image) {
        val minGap = (1000 / CaptureSettings.maxFps.coerceIn(5, 30)).toLong()
        val now = System.currentTimeMillis()
        if (now - lastJpegMs < minGap) {
            return
        }
        lastJpegMs = now
        val jpeg = toJpeg(image) ?: return
        latestJpeg.set(jpeg)
        jpegSeq.incrementAndGet()
    }

    private fun toJpeg(image: Image): ByteArray? {
        val plane = image.planes.firstOrNull() ?: return null
        val buffer = plane.buffer
        val pixelStride = plane.pixelStride.coerceAtLeast(1)
        val rowStride = plane.rowStride
        val rowPadding = (rowStride - pixelStride * image.width).coerceAtLeast(0)
        val bitmapWidth = image.width + rowPadding / pixelStride
        val bitmap = Bitmap.createBitmap(bitmapWidth, image.height, Bitmap.Config.ARGB_8888)
        buffer.rewind()
        bitmap.copyPixelsFromBuffer(buffer)
        val framed = if (bitmap.width == image.width) {
            bitmap
        } else {
            Bitmap.createBitmap(bitmap, 0, 0, image.width, image.height)
        }
        val out = ByteArrayOutputStream()
        val ok = framed.compress(Bitmap.CompressFormat.JPEG, 70, out)
        if (framed !== bitmap) {
            bitmap.recycle()
        }
        framed.recycle()
        val bytes = out.toByteArray()
        return if (ok && bytes.isNotEmpty() && bytes.size <= ProtocolPorts.MAX_VIDEO_BYTES) bytes else null
    }

    private fun pumpHub() {
        var sent = 0L
        while (running.get()) {
            var wrote = false
            val seq = jpegSeq.get()
            val jpeg = latestJpeg.get()
            if (jpeg != null && seq != sent) {
                StreamHub.publishVideo(jpegPacket(jpeg, frameWidth, frameHeight), frameWidth, frameHeight)
                sent = seq
                wrote = true
            }
            val pcm = audio.pollPcm()
            if (pcm != null && pcm.isNotEmpty()) {
                StreamHub.publishAudio(audio.encodePcm(pcm))
                wrote = true
            }
            if (!wrote) {
                try {
                    Thread.sleep(4)
                } catch (_: InterruptedException) {
                    break
                }
            }
        }
    }

    private fun serve() {
        val server = ServerSocket()
        server.reuseAddress = true
        server.soTimeout = 250
        server.bind(InetSocketAddress("0.0.0.0", ProtocolPorts.SCREEN), 16)
        serverSocket = server
        server.use {
            while (running.get()) {
                val socket = try {
                    server.accept()
                } catch (_: java.net.SocketTimeoutException) {
                    continue
                } catch (_: Exception) {
                    if (!running.get()) {
                        break
                    }
                    continue
                }
                socket.tcpNoDelay = true
                socket.use { client ->
                    val out = client.getOutputStream()
                    val writeLock = Any()
                    fun emit(body: ByteArray) {
                        synchronized(writeLock) {
                            writeFrame(out, body)
                        }
                    }
                    val audioListener: (ByteArray) -> Unit = { packet ->
                        try {
                            emit(packet)
                        } catch (_: Exception) {
                            // Socket closed; the loop exits next.
                        }
                    }
                    StreamHub.addAudioListener(audioListener)
                    try {
                        var sentVideo = 0L
                        while (running.get()) {
                            val snap = StreamHub.snapshot()
                            val video = snap.video
                            if (video != null && snap.videoSeq != sentVideo) {
                                emit(video)
                                sentVideo = snap.videoSeq
                            } else {
                                Thread.sleep(4)
                            }
                        }
                    } finally {
                        StreamHub.removeAudioListener(audioListener)
                    }
                }
            }
        }
    }

    private fun jpegPacket(jpeg: ByteArray, width: Int, height: Int): ByteArray {
        return VideoPacket.encode(
            VideoPacket(
                codec = VideoPacket.JPEG,
                width = width,
                height = height,
                captureTimestampMs = System.currentTimeMillis(),
                flags = VideoPacket.KEYFRAME,
                payload = jpeg
            )
        )
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
        audio.stop()
        try {
            serverSocket?.close()
        } catch (_: Exception) {
            // Unblock accept.
        }
        serverSocket = null
        display?.release()
        display = null
        imageReader?.close()
        imageReader = null
        imageThread?.quitSafely()
        imageThread = null
        latestJpeg.set(null)
        StreamHub.clear()
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
