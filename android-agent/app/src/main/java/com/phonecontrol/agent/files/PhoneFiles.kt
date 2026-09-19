package com.phonecontrol.agent.files

import android.app.Application
import android.content.ContentValues
import android.content.Context
import android.media.MediaScannerConnection
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.os.Handler
import android.os.Looper
import android.provider.MediaStore
import android.provider.OpenableColumns
import com.phonecontrol.agent.R
import com.phonecontrol.agent.domain.ControlPush
import com.phonecontrol.agent.domain.Envelope
import com.phonecontrol.agent.domain.FileNames
import com.phonecontrol.agent.domain.JsonLite
import com.phonecontrol.agent.domain.ProtocolError
import java.io.File
import java.io.FileOutputStream
import java.io.OutputStream
import java.util.Base64
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.Executors

/**
 * Saves PC drops into Downloads/PcPhone and sends picked/shared files to the PC.
 * Does not log file bytes.
 */
object PhoneFiles {
    private val main = Handler(Looper.getMainLooper())
    private val worker = Executors.newSingleThreadExecutor { task -> Thread(task, "pcphone-files") }
    private val incoming = ConcurrentHashMap<String, Incoming>()
    private val statusListeners = CopyOnWriteArrayList<(String) -> Unit>()
    @Volatile
    private var app: Application? = null

    fun bind(application: Application) {
        app = application
    }

    fun addStatusListener(listener: (String) -> Unit) {
        statusListeners.add(listener)
    }

    fun removeStatusListener(listener: (String) -> Unit) {
        statusListeners.remove(listener)
    }

    fun handle(envelope: Envelope): Envelope {
        val context = app ?: return fail(envelope, "INTERNAL", "files not ready")
        return try {
            when (envelope.type) {
                "file.offer" -> begin(context, envelope)
                "file.chunk" -> chunk(envelope)
                "file.complete" -> finish(context, envelope)
                "file.cancel" -> {
                    cancel(JsonLite.stringField(envelope.payloadJson.orEmpty(), "transferId"))
                    ok(envelope, """{"ok":true}""")
                }
                else -> ok(envelope, """{"ok":true}""")
            }
        } catch (_: SecurityException) {
            fail(envelope, "PERMISSION_DENIED", "storage permission required")
        } catch (_: Exception) {
            fail(envelope, "INTERNAL", "file transfer failed")
        }
    }

    fun sendUris(context: Context, uris: List<Uri>) {
        if (uris.isEmpty()) return
        val appContext = context.applicationContext
        worker.execute {
            if (!ControlPush.hasListeners()) {
                notify(appContext.getString(R.string.file_need_pc))
                return@execute
            }
            for (uri in uris) {
                sendOne(appContext, uri)
            }
        }
    }

    private fun sendOne(context: Context, uri: Uri) {
        val meta = readMeta(context, uri)
        val name = FileNames.sanitize(meta.first)
        val mime = meta.second.ifBlank { mimeFromName(name) }
        val size = meta.third
        if (size > FileNames.MAX_BYTES) {
            notify(context.getString(R.string.file_too_large, name))
            return
        }
        val transferId = UUID.randomUUID().toString()
        notify(context.getString(R.string.file_sending, name))
        ControlPush.send(
            ControlPush.envelope(
                "file.offer",
                """{"transferId":${JsonLite.quote(transferId)},"name":${JsonLite.quote(name)},"size":$size,"mime":${JsonLite.quote(mime)},"direction":"to_windows"}"""
            )
        )
        var sent = 0L
        context.contentResolver.openInputStream(uri)?.use { input ->
            val buffer = ByteArray(FileNames.CHUNK_BYTES)
            while (true) {
                val read = input.read(buffer)
                if (read <= 0) break
                val data = Base64.getEncoder().encodeToString(buffer.copyOf(read))
                ControlPush.send(
                    ControlPush.envelope(
                        "file.chunk",
                        """{"transferId":${JsonLite.quote(transferId)},"offset":$sent,"data":${JsonLite.quote(data)}}"""
                    )
                )
                sent += read
                if (size > 0) {
                    val percent = ((sent * 100L) / size).toInt().coerceIn(0, 100)
                    notify(context.getString(R.string.file_sending_pct, name, percent))
                }
            }
        } ?: run {
            notify(context.getString(R.string.file_send_fail, name))
            return
        }
        ControlPush.send(
            ControlPush.envelope(
                "file.complete",
                """{"transferId":${JsonLite.quote(transferId)},"name":${JsonLite.quote(name)},"bytesSent":$sent}"""
            )
        )
        notify(context.getString(R.string.file_sent, name))
    }

    private fun begin(context: Context, envelope: Envelope): Envelope {
        val payload = envelope.payloadJson.orEmpty()
        val direction = JsonLite.stringField(payload, "direction") ?: "to_phone"
        if (direction == "to_windows") {
            return ok(envelope, """{"ok":true}""")
        }
        val rawName = JsonLite.stringField(payload, "name") ?: "file"
        val name = FileNames.sanitize(rawName)
        val size = JsonLite.longField(payload, "size") ?: 0L
        val mime = JsonLite.stringField(payload, "mime")?.ifBlank { null } ?: mimeFromName(name)
        val transferId = JsonLite.stringField(payload, "transferId")
            ?: return fail(envelope, "VALIDATION", "transferId required")
        if (size < 0 || size > FileNames.MAX_BYTES) {
            return fail(envelope, "VALIDATION", "file too large")
        }
        if (incoming.size >= FileNames.MAX_IN_FLIGHT) {
            return fail(envelope, "TIMEOUT", "too many transfers")
        }
        cancel(transferId)
        val created = openOutgoing(context, name, mime)
        incoming[transferId] = Incoming(
            name = created.first,
            size = size,
            mime = mime,
            uri = created.second,
            file = created.third,
            stream = created.fourth
        )
        notify(context.getString(R.string.file_receiving, created.first))
        return progress(envelope, transferId, 0, size)
    }

    private fun chunk(envelope: Envelope): Envelope {
        val payload = envelope.payloadJson.orEmpty()
        val transferId = JsonLite.stringField(payload, "transferId")
            ?: return fail(envelope, "VALIDATION", "transferId required")
        val session = incoming[transferId]
            ?: return fail(envelope, "VALIDATION", "unknown transfer")
        val data = JsonLite.stringField(payload, "data") ?: ""
        if (data.isNotEmpty()) {
            val bytes = Base64.getDecoder().decode(data)
            if (session.written + bytes.size > FileNames.MAX_BYTES) {
                cancel(transferId)
                return fail(envelope, "VALIDATION", "file too large")
            }
            session.stream.write(bytes)
            session.written += bytes.size
        }
        return progress(envelope, transferId, session.written, session.size)
    }

    private fun finish(context: Context, envelope: Envelope): Envelope {
        val transferId = JsonLite.stringField(envelope.payloadJson.orEmpty(), "transferId")
            ?: return fail(envelope, "VALIDATION", "transferId required")
        val session = incoming.remove(transferId)
            ?: return fail(envelope, "VALIDATION", "unknown transfer")
        try {
            session.stream.flush()
            session.stream.close()
            publish(context, session)
            notify(context.getString(R.string.file_received, session.name))
            return envelope.copy(
                type = "file.complete",
                payloadJson = """{"transferId":${JsonLite.quote(transferId)},"name":${JsonLite.quote(session.name)},"bytesSent":${session.written},"ok":true}""",
                error = null
            )
        } catch (_: Exception) {
            session.stream.closeQuietly()
            return fail(envelope, "INTERNAL", "could not save file")
        }
    }

    private fun cancel(transferId: String?) {
        if (transferId.isNullOrBlank()) return
        val session = incoming.remove(transferId) ?: return
        session.stream.closeQuietly()
        try {
            val context = app ?: return
            if (session.uri != null) {
                context.contentResolver.delete(session.uri, null, null)
            } else {
                session.file?.delete()
            }
        } catch (_: Exception) {
            // Best-effort cleanup.
        }
    }

    private fun openOutgoing(
        context: Context,
        name: String,
        mime: String
    ): Quadruple {
        var unique = name
        var index = 2
        if (Build.VERSION.SDK_INT >= 29) {
            while (true) {
                val values = ContentValues().apply {
                    put(MediaStore.Downloads.DISPLAY_NAME, unique)
                    put(MediaStore.Downloads.MIME_TYPE, mime)
                    put(MediaStore.Downloads.IS_PENDING, 1)
                    put(
                        MediaStore.MediaColumns.RELATIVE_PATH,
                        Environment.DIRECTORY_DOWNLOADS + "/PcPhone"
                    )
                }
                val uri = context.contentResolver.insert(
                    MediaStore.Downloads.EXTERNAL_CONTENT_URI,
                    values
                )
                if (uri != null) {
                    val stream = context.contentResolver.openOutputStream(uri)
                        ?: throw IllegalStateException("stream")
                    return Quadruple(unique, uri, null, stream)
                }
                unique = numbered(name, index)
                index += 1
                if (index > 40) throw IllegalStateException("insert")
            }
        }
        @Suppress("DEPRECATION")
        val dir = File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), "PcPhone")
        if (!dir.exists()) dir.mkdirs()
        var file = File(dir, unique)
        while (file.exists()) {
            unique = numbered(name, index)
            file = File(dir, unique)
            index += 1
            if (index > 40) break
        }
        return Quadruple(unique, null, file, FileOutputStream(file))
    }

    private fun publish(context: Context, session: Incoming) {
        if (Build.VERSION.SDK_INT >= 29 && session.uri != null) {
            val values = ContentValues().apply {
                put(MediaStore.Downloads.IS_PENDING, 0)
            }
            context.contentResolver.update(session.uri, values, null, null)
            return
        }
        val path = session.file?.absolutePath ?: return
        MediaScannerConnection.scanFile(context, arrayOf(path), arrayOf(session.mime), null)
    }

    private fun readMeta(context: Context, uri: Uri): Triple<String, String, Long> {
        var name = "file"
        var mime = context.contentResolver.getType(uri) ?: ""
        var size = 0L
        context.contentResolver.query(uri, null, null, null, null)?.use { cursor ->
            val nameIdx = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
            val sizeIdx = cursor.getColumnIndex(OpenableColumns.SIZE)
            if (cursor.moveToFirst()) {
                if (nameIdx >= 0) name = cursor.getString(nameIdx) ?: name
                if (sizeIdx >= 0) size = cursor.getLong(sizeIdx)
            }
        }
        if (size <= 0L) {
            size = context.contentResolver.openAssetFileDescriptor(uri, "r")?.use { it.length } ?: 0L
        }
        if (mime.isBlank()) mime = mimeFromName(name)
        return Triple(name, mime, size.coerceAtLeast(0L))
    }

    private fun mimeFromName(name: String): String {
        val ext = name.substringAfterLast('.', "").lowercase()
        return when (ext) {
            "jpg", "jpeg" -> "image/jpeg"
            "png" -> "image/png"
            "gif" -> "image/gif"
            "webp" -> "image/webp"
            "pdf" -> "application/pdf"
            "txt" -> "text/plain"
            "zip" -> "application/zip"
            "apk" -> "application/vnd.android.package-archive"
            "mp4" -> "video/mp4"
            "mp3" -> "audio/mpeg"
            else -> "application/octet-stream"
        }
    }

    private fun numbered(name: String, index: Int): String {
        val dot = name.lastIndexOf('.')
        return if (dot > 0) {
            "${name.substring(0, dot)}_$index${name.substring(dot)}"
        } else {
            "${name}_$index"
        }
    }

    private fun notify(text: String) {
        main.post {
            statusListeners.forEach { it(text) }
        }
    }

    private fun progress(envelope: Envelope, transferId: String, sent: Long, total: Long): Envelope {
        return envelope.copy(
            type = "file.progress",
            payloadJson = """{"transferId":${JsonLite.quote(transferId)},"bytesSent":$sent,"bytesTotal":$total}""",
            error = null
        )
    }

    private fun ok(envelope: Envelope, payload: String) =
        envelope.copy(payloadJson = payload, error = null)

    private fun fail(envelope: Envelope, code: String, message: String) =
        envelope.copy(error = ProtocolError(code, message, retryable = code != "VALIDATION"))

    private fun OutputStream.closeQuietly() {
        try {
            close()
        } catch (_: Exception) {
            // Already closed.
        }
    }

    private class Incoming(
        val name: String,
        val size: Long,
        val mime: String,
        val uri: Uri?,
        val file: File?,
        val stream: OutputStream,
        var written: Long = 0
    )

    private data class Quadruple(
        val first: String,
        val second: Uri?,
        val third: File?,
        val fourth: OutputStream
    )
}
