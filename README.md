# Phone Control Suite

سیستم شخصی برای کنترل گوشی اندروید از روی ویندوز، در حالی که **USB Tethering** برای اینترنت کامپیوتر فعال می‌ماند.

دو نرم‌افزار مستقل در یک Repository:

| پروژه | مسیر | نقش |
| --- | --- | --- |
| Windows Client | `windows-client/` | کشف دستگاه، نمایش صفحه، ورودی، اعلان، پیامک، فایل |
| Android Agent | `android-agent/` | Capture، Accessibility، Listenerها، اجرای فرمان مجاز |

این پروژه برای استفادهٔ شخصی روی دستگاهی است که مالک آن هستید. همهٔ مجوزها باید توسط خود کاربر فعال شوند. هیچ روش مخفی، دور زدن امنیت، یا کنترل بدون رضایت وجود ندارد.

## وضعیت فعلی

**Phase 3 — Pairing, Authentication & State Sync** تمام شده است.

بدون تأیید شما به Phase 4 نمی‌رویم.

جزئیات: [`docs/phases.md`](docs/phases.md)، [`docs/PHASE-3-REPORT.md`](docs/PHASE-3-REPORT.md).

## اصل ارتباطی

سه مسیر از هم جدا هستند:

1. **USB Tethering** — فقط اینترنت. کنترل نباید آن را قطع کند.
2. **Control / Data Channel** — فرمان‌ها، اعلان‌ها، کلیپ‌بورد، وضعیت. TCP روی همان لینک RNDIS (نه وابسته به عوض کردن USB mode).
3. **Screen Stream** — تصویر صفحه روی کانال C، TCP **17891** (H.264؛ JPEG/raw برای fallback و تست).

ADB فقط برای کشف، نصب و bootstrap است، نه تنها مسیر کنترل.

نمودار و تصمیم‌ها: [`docs/architecture.md`](docs/architecture.md).

## پیش‌نیاز توسعه

### Windows Client

- Windows 10 1809 یا جدیدتر
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (ترجیحی) یا SDK 6 در صورت نبود SDK 8
- ADB (`platform-tools`) فقط برای Phase 1 به بعد لازم است

```powershell
.\scripts\check-env.ps1
.\scripts\build-windows.ps1
.\scripts\test-windows.ps1
```

اجرای UI:

```powershell
dotnet run --project windows-client\src\PhoneControl.Desktop\PhoneControl.Desktop.csproj
```

### Android Agent

- Android Studio Hedgehog+ / JDK 17
- Android SDK با compileSdk 35
- گوشی با USB debugging (اختیاری برای bootstrap) و USB tethering

```powershell
.\scripts\build-android.ps1
```

حداقل SDK هدف: **26 (Android 8.0)**. Target SDK: **35**.

## ساختار Repository

```
phone-control-suite/
├── windows-client/     # .NET / WPF — مستقل Build می‌شود
├── android-agent/      # Kotlin / Gradle — مستقل Build می‌شود
├── shared/protocol/    # قرارداد نسخه‌دار JSON
├── docs/               # معماری، ADR، مجوزها، محدودیت‌ها
├── scripts/            # Build / Test / محیط
└── tests/              # پروتکل، یکپارچه، سخت‌افزار، دستی
```

## امنیت

- Pairing صریح روی گوشی
- ذخیرهٔ کلید با DPAPI (ویندوز) و EncryptedSharedPreferences (اندروید)
- Log بدون متن SMS، اعلان، کلیپ‌بورد یا رمز
- رمزنگاری اتصال در Phase 7؛ معماری از Phase 0 برای آن آماده است

جزئیات: [`docs/security.md`](docs/security.md).

## مجوزهای اندروید

جدول کامل: [`docs/permissions.md`](docs/permissions.md).

محدودیت‌های واقعی سیستم‌عامل: [`docs/android-limitations.md`](docs/android-limitations.md).

## پروتکل

[`shared/protocol/protocol.md`](shared/protocol/protocol.md)
