# گزارش Phase 2 — Screen and Input

## هدف مرحله

استریم تصویر روی **فقط** TCP 17891 (کانال C)، ورودی روی کانال کنترل 17890، دیکودر/انکودر پشت interface، متریک تأخیر در StatusBar، و Capture فقط بعد از dialog سیستم MediaProjection. بدون ADB برای شروع capture.

## انتخاب‌ها

### دیکودر ویندوز: Media Foundation (نه FFmpeg)

Inbox H.264/H.265 MFT (`CLSID_CMSH264DecoderMFT` / HEVC)، NV12 → BGRA، پشت `IVideoDecoder`. دلیل: بدون redistributable، DXVA وقتی OS بدهد، بدون مجوز LGPL. FFmpeg برای MVP رد شد چون باید DLL native پخش شود.

JPEG (WIC) و raw BGRA برای fallback و تست می‌مانند. تست‌ها mfplat را بار نمی‌کنند.

### رندر: WriteableBitmap (نه D3DImage)

خروجی دیکودر CPU BGRA است. یک `WritePixels` کافی است. D3DImage وقتی بافت از قبل روی GPU D3D9 نیست هزینهٔ interop اضافه می‌دهد.

### انکودر اندروید: MediaCodec H.264، hardware اول

`COLOR_FormatSurface` + VirtualDisplay. نام‌های `OMX.google` / `c2.android` رد می‌شوند مگر اینکه سخت‌افزار نباشد. Bitrate با `PARAMETER_KEY_VIDEO_BITRATE` وقتی `video.adapt` برسد.

### تزریق: AccessibilityService، نه SharedPreferences

SharedPreferences API ورودی نیست. جزئیات: [`input-injection.md`](input-injection.md) و ADR-011.

Win32 `SendInput` فقط به‌عنوان مدل رکورد است؛ تزریق محلی انجام نمی‌شود.

## فایل‌های اصلی

- پروتکل: `shared/protocol/protocol-specs.md`, `protocol.md` (`video.*`, `input.pinch`, هدر کانال C)
- ویندوز: `PhoneControl.Screen`, `PhoneControl.Input`, `VideoSession`, `TcpVideoTransport`, `MediaFoundationH264Decoder`, `PhoneSurface`, StatusBar
- اندروید: `MediaCodecH264Encoder`, `ScreenCaptureService`, `AgentAccessibilityService`, `ControlServer` روی 17890, `AgentCommandRouter`
- تست: `VideoFramingTests` (loopback TCP + raw BGRA), `VideoAndInputTests`, تست‌های Kotlin برای `VideoPacket` / framing / `video.start` بدون permission

## محدودیت‌های حفظ‌شده

- MediaProjection فقط اگر `RESULT_OK` و Intent سیستم برگردد. `video.start` بدون توکن → `PERMISSION_DENIED`.
- هیچ `adb shell` / `adb exec-out` برای capture نیست.
- USB function عوض نمی‌شود.
- متن SMS/اعلان/کلیپ‌بورد لاگ نمی‌شود.
- Pairing کامل Phase 3 است؛ hello_ack فعلاً `pairingRequired: false` می‌دهد تا استریم Phase 2 وصل شود.

## Adaptive

Tierها: High 1280/30/4000 → Medium 720/24/2000 → Low 540/15/1000 → Minimal 360/10/600. بر اساس bitrate مشاهده‌شده و latency. Throttle حداقل ۲ ثانیه. پیام `video.adapt` روی 17890.

## Latency

`captureTimestampMs` در هدر فریم در برابر زمان دریافت؛ EMA در StatusBar به‌صورت ms.

## تست و Build

```powershell
$env:PATH = "$env:LOCALAPPDATA\dotnet;$env:PATH"
.\scripts\build-windows.ps1
.\scripts\test-windows.ps1
```

نتیجهٔ این ماشین: **Build succeeded, 0 warning, 0 error**. **67 passed, 0 failed**.

Android Agent روی این ماشین بدون JDK/SDK فقط source است. Capture روی دستگاه واقعی: کاربر دکمهٔ Start capture را می‌زند، dialog سیستم را می‌پذیرد، سپس ویندوز به 17891 وصل می‌شود.

## خروج از فاز

Phase 2 کامل است. **Phase 3 شروع نمی‌شود تا تأیید کنید.**
