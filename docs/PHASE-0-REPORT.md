# گزارش Phase 0 — Architecture and Scaffolding

## هدف مرحله

ایجاد Repository، دو پروژهٔ مستقل قابل Build، مستندات معماری، قرارداد پروتکل، interfaceها و placeholderها. بدون ورود به کشف واقعی ADB یا Capture.

## فایل‌های ایجادشده

- ریشه: `README.md`, `.gitignore`, `docs/*`, `shared/protocol/*`, `scripts/*`, `tests/*`
- ویندوز: `windows-client/PhoneControl.sln` و ۱۰ پروژه
- اندروید: `android-agent/` با `app` + core + feature modules
- Canvas معماری در کنار چت (خارج از repo)

## فایل‌های تغییرکرده

Repository خالی بود؛ همهٔ فایل‌ها جدیدند.

## قابلیت‌های پیاده‌سازی‌شده

- تفکیک کانال A (USB Tethering) از B/C/D (TCP روی subnet تترینگ)
- ADB پشت interface؛ شکست ADB تست واحد را خراب نمی‌کند
- Envelope JSON نسخه‌دار + serialize/deserialize
- Coordinate mapper، clipboard loop guard، reconnect policy، SMS adapter خالی، notification mapper
- UI ویندوز با وضعیت اتصال mock (بدون گوشی)
- Agent اندروید: صفحهٔ Phase 0، stubهای Accessibility / Notification Listener / MediaProjection service
- UI مجوزها به‌صورت وضعیت قابل مشاهده (هنوز مقدار واقعی سیستم نیست)

## تست‌های اضافه‌شده

۲۲ تست واحد ویندوز (پروتکل، مختصات، clipboard loop، reconnect، SMS، notification، discovery بدون ADB).

تست‌های domain اندروید در `core-domain/src/test` نوشته شده‌اند؛ روی این ماشین JDK نیست و اجرا نشدند.

تست Hardware با `Category=Hardware` از `dotnet test` پیش‌فرض حذف می‌شود.

## دستور Build

```powershell
.\scripts\check-env.ps1
.\scripts\build-windows.ps1
```

نتیجهٔ این ماشین: **Build succeeded, 0 warning, 0 error** با .NET SDK 8.0.425.

```powershell
.\scripts\build-android.ps1
```

نتیجهٔ این ماشین: **قابل اجرا نیست** — JDK 17 و Android SDK نصب نیستند. ساختار Gradle کامل است؛ بعد از Android Studio باید `assembleDebug` بگیرد.

## دستور Test

```powershell
.\scripts\test-windows.ps1
```

نتیجه: **22 passed, 0 failed**.

## نحوه‌ی اجرای محلی

```powershell
$env:PATH = "$env:LOCALAPPDATA\dotnet;$env:PATH"
dotnet run --project windows-client\src\PhoneControl.Desktop\PhoneControl.Desktop.csproj
```

UI باید Device mock، وضعیت Disconnected و توضیح کانال TCP را نشان دهد. دکمهٔ Connect بدون گوشی واقعی به `PairingRequired` می‌رود اگر endpoint تترینگ mock موجود باشد.

Agent: پروژه را در Android Studio از پوشهٔ `android-agent/` باز کنید.

## محدودیت‌های فعلی

- استریم صفحه، ژست واقعی، pairing سیمی، SMS واقعی و انتقال فایل پیاده نشده‌اند
- TCP server اندروید هنوز bind نمی‌شود
- Secret pairing هنوز در DPAPI/EncryptedSharedPreferences ذخیره نمی‌شود (interface + InMemory)
- TLS برای Phase 7 رزرو است
- روی این PC، Java/Android SDK نیست؛ APK این مرحله Build نشد
- SDK 8 در `%LOCALAPPDATA%\dotnet` نصب شد (نه Program Files)؛ اسکریپت‌ها PATH را تنظیم می‌کنند

## مرحله‌ی بعد

**Phase 1 — Device Connection** فقط پس از تأیید شما:

- کشف واقعی ADB (اگر interface باشد) بدون تغییر USB function
- کشف endpoint تترینگ (`192.168.42.129` و gateway آداپتور RNDIS)
- نمایش مدل/وضعیت در WPF
- Reconnect/timeout با mock و در صورت امکان loopback TCP
- همچنان بدون Capture و بدون Input واقعی
