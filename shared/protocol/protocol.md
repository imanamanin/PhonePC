# Phone Control Protocol v1

قرارداد نسخه‌دار بین Windows Client و Android Agent.

- Encoding MVP: JSON UTF-8
- Framing: 4-byte big-endian length + body (حداکثر 1 MiB برای JSON؛ فریم تصویر جدا)
- پورت کنترل/داده: `17890`
- پورت تصویر: `17891`

جزئیات نسخه: [`versioning.md`](versioning.md).

## Envelope

```json
{
  "version": 1,
  "type": "string",
  "requestId": "uuid",
  "timestamp": 0,
  "payload": {},
  "error": null
}
```

| فیلد | معنی |
| --- | --- |
| `version` | نسخهٔ پروتکل پیام |
| `type` | شناسهٔ پایدار، نقطه‌ای |
| `requestId` | همبستگی request/response؛ برای event هم UUID جدید |
| `timestamp` | Unix ms UTC |
| `payload` | شیء وابسته به type؛ هرگز secret |
| `error` | فقط در پاسخ شکست: `{ "code", "message", "retryable" }` — `message` بدون دادهٔ خصوصی |

## Handshake

`session.hello` (Windows → Android)

```json
{
  "clientName": "PhoneControl.Desktop",
  "clientVersion": "0.1.0",
  "protocolMin": 1,
  "protocolMax": 1,
  "pairingId": null,
  "supportedEncodings": ["json"],
  "supportedScreenCodecs": ["h264", "jpeg"]
}
```

`session.hello_ack` (Android → Windows)

```json
{
  "agentName": "PhoneControl.Agent",
  "agentVersion": "0.1.0",
  "protocol": 1,
  "device": {
    "model": "Pixel",
    "manufacturer": "Google",
    "androidVersion": "14",
    "sdkInt": 34,
    "serialHash": "sha256-prefix"
  },
  "pairingRequired": true,
  "capabilities": ["input.touch", "screen.jpeg", "clipboard", "notifications"]
}
```

`serial` خام فرستاده نمی‌شود؛ `serialHash` کوتاه برای تشخیص دستگاه کافی است.

## Pairing

- `pairing.start` — Agent می‌سازد و PIN شش‌رقمی را **فقط روی UI گوشی** نشان می‌دهد (TTL پیش‌فرض ۱۲۰ ثانیه).
- `pairing.submit` `{ "pin": "123456" }` — ویندوز PIN را می‌فرستد. PIN هرگز لاگ نمی‌شود.
- `pairing.accepted` `{ "pairingId", "sessionToken", "expiresAt", "sas" }` — Session Token (۳۲ بایت، Base64) برای Trusted Device. `sas` شش رقم Numeric Comparison است که هر دو طرف از SHA-256 توکن محاسبه می‌کنند.
- `pairing.rejected` `{ "reason": "pin_mismatch" | "locked" }`
- `pairing.expired`
- `pairing.confirm` `{ "sas" }` — تأیید اختیاری تطابق SAS
- `pairing.cleared` — Unpair؛ توکن پاک می‌شود

`session.hello` می‌تواند `pairingId` و `sessionToken` ذخیره‌شده را بفرستد. اگر معتبر باشد `pairingRequired: false`.

Secret روی دیسک: ویندوز DPAPI، اندروید EncryptedSharedPreferences / Keystore. روی سیم در v1 روی USB plaintext است؛ Phase 7 داخل TLS می‌برد.

## Heartbeat و Ping

- `session.ping` / `session.pong` — payload `{ "t": unixMs }`
- `session.heartbeat` — هر ۵ ثانیه از هر دو طرف مجاز
- `session.close` — `{ "reason": "user" | "timeout" | "error" }`
- `session.reconnect` — همان `pairingId`، nonce جدید

## Capability و Permission

`capabilities.get` → `capabilities.status`

`permissions.get` → `permissions.status`

```json
{
  "accessibility": "granted",
  "notificationListener": "denied",
  "mediaProjection": "unknown",
  "smsRead": "denied",
  "smsSend": "denied",
  "postNotifications": "granted"
}
```

مقادیر: `granted` | `denied` | `unknown` | `unsupported`.

## Device status

`device.status` و `state.update` (event یا پاسخ):

```json
{
  "batteryPercent": 80,
  "charging": true,
  "network": "usb_tether",
  "deviceTimeUtc": 0,
  "screenWidth": 1080,
  "screenHeight": 2400,
  "rotation": 0,
  "storageFreeBytes": null
}
```

## Screen

کنترل (TCP 17890):

- `video.start` `{ "codec": "h264", "maxFps": 30, "maxWidth": 1280, "bitrateKbps": 4000, "port": 17891 }`
- `video.stop`
- `video.adapt` `{ "maxFps", "maxWidth", "bitrateKbps" }` — حداکثر هر ۲ ثانیه
- `video.frame` — فقط metadata؛ بایت پیکسل روی کنترل نیست
- `screen.start` / `screen.stop` / `screen.metadata` — JPEG fallback، هنوز معتبر

بدون توکن MediaProjection، Agent باید `error.code = PERMISSION_DENIED` بدهد. Capture با ADB شروع نمی‌شود.

باینری روی کانال C، پورت **17891** (حداکثر ۸ MiB):

```
uint32 be length | uint8 flags | uint64 be timestampMs | uint16 be width | uint16 be height | uint8 codec | payload
```

`flags`: bit0 keyframe, bit1 codec-config (SPS/PPS).  
`codec`: 1=H.264, 2=H.265, 3=JPEG, 4=raw BGRA (تست).

جزئیات: [`protocol-specs.md`](protocol-specs.md).

## Input

`input.touch`

```json
{
  "action": "tap",
  "x": 500,
  "y": 800,
  "pointerId": 0,
  "durationMs": 0
}
```

`action`: `down` | `move` | `up` | `tap` | `swipe` | `scroll`

`swipe` / `scroll`: `x2`, `y2`, `durationMs`. Scroll می‌تواند `delta` داشته باشد.

`input.pinch`

```json
{
  "x": 200,
  "y": 400,
  "x2": 600,
  "y2": 800,
  "scale": 1.25
}
```

`input.key`

```json
{
  "action": "click",
  "key": "back",
  "text": null
}
```

`key`: `back` | `home` | `recents` | `volume_up` | `volume_down` | `power` | `enter` | `text`

`text` فقط وقتی `key=text`. خود متن در Log نمی‌رود.

`input.screenshot` — درخواست ذخیره سمت ویندوز؛ Agent یک فریم می‌فرستد.

## Notifications

Event: `notification.received` | `notification.removed` | `notification.updated`

```json
{
  "key": "pkg|id|tag",
  "packageName": "example.package",
  "title": "New message",
  "text": "Message preview",
  "postedAt": 0,
  "actions": [{ "id": "reply", "label": "Reply", "hasRemoteInput": true }]
}
```

`notification.action` از ویندوز: `{ "key", "actionId", "replyText"? }`

## Clipboard

`clipboard.changed` `{ "origin": "android"|"windows", "hash": "sha256", "textLength": 12 }`

`clipboard.set` `{ "origin", "hash", "text" }`

Loop: اگر `hash` برابر آخرین مقدار ارسالی خود طرف باشد، اعمال و event نمی‌شود. متن در Log نمی‌آید.

## SMS

`sms.list` `{ "max": 50 }` — ممکن است `error.code = SMS_PERMISSION_DENIED`

`sms.received` event

`sms.send` `{ "to", "body" }` — فقط با permission

بدنه در Log نمی‌آید.

## File transfer

`file.offer` `{ "transferId", "name", "size", "direction": "to_phone"|"to_windows" }`

`file.progress` `{ "transferId", "bytesSent", "bytesTotal" }`

`file.cancel` `{ "transferId" }`

داده روی کنترل به صورت chunk base64 در MVP؛ بعداً استریم باینری.

## Error codes

| code | معنی |
| --- | --- |
| `UNKNOWN_TYPE` | type پشتیبانی نشده |
| `VALIDATION` | payload نامعتبر |
| `UNPAIRED` | نیاز به pairing |
| `PERMISSION_DENIED` | مجوز اندروید نیست |
| `TIMEOUT` | اجرا نرسید |
| `CANCELLED` | لغو شد |
| `SECURE_CONTENT` | FLAG_SECURE / غیرقابل capture |
| `SMS_PERMISSION_DENIED` | SMS |
| `UNSUPPORTED` | OEM / API |
| `INTERNAL` | خطای داخلی بدون جزئیات خصوصی |

## فهرست typeها

Handshake/session: `session.hello`, `session.hello_ack`, `session.ping`, `session.pong`, `session.heartbeat`, `session.close`, `session.reconnect`

Pairing: `pairing.start`, `pairing.submit`, `pairing.accepted`, `pairing.rejected`, `pairing.expired`, `pairing.cleared`, `pairing.confirm`

Meta: `capabilities.get`, `capabilities.status`, `permissions.get`, `permissions.status`, `device.status`, `state.update`

Screen: `screen.start`, `screen.stop`, `screen.metadata`, `video.start`, `video.stop`, `video.adapt`, `video.frame`

Input: `input.touch`, `input.key`, `input.pinch`, `input.screenshot`

Notify: `notification.received`, `notification.removed`, `notification.updated`, `notification.action`

Clipboard: `clipboard.changed`, `clipboard.set`

SMS: `sms.list`, `sms.received`, `sms.send`

File: `file.offer`, `file.progress`, `file.cancel`, `file.complete`, `file.error`

پاسخ موفق همان `type` با پسوند `.ok` نیست؛ همان type با payload نتیجه یا `error` پر می‌شود. برای commandها پاسخ `result` با `{ "ok": true }` کافی است مگر خلافش در schema آمده باشد.
