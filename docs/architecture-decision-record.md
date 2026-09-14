# Architecture Decision Records

## ADR-001 — دو باینری مستقل در یک Repository

**وضعیت:** پذیرفته شده (Phase 0)

**تصمیم:** Windows Client و Android Agent در یک Git repo هستند، اما solution/Gradle جدا، تست جدا، و نسخهٔ انتشار جدا دارند. قرارداد فقط از `shared/protocol` می‌آید.

**چرا:** انتشار APK و نصب‌کنندهٔ ویندوز چرخهٔ متفاوت دارند. وابستگی Build یکی به دیگری، CI را شکننده می‌کند. پروتکل مشترک کافی است.

## ADR-002 — Windows: C# / .NET / WPF / MVVM

**وضعیت:** پذیرفته شده (Phase 0)

**گزینه‌ها:**

| گزینه | مزیت | هزینه |
| --- | --- | --- |
| WPF + .NET | رندر تصویر بالغ، MVVM جاافتاده، DI رسمی، نصب سادهٔ exe/folder | ظاهر قدیمی‌تر از WinUI |
| WinUI 3 | UI مدرن | MSIX، پیچیدگی بسته‌بندی، اکوسیستم تست سنگین‌تر برای MVP |
| Avalonia | چندسکویی | خارج از محدوده؛ هدف فقط ویندوز است |
| Electron | UI سریع | مصرف RAM، کنترل سطح‌پایین موس روی تصویر سخت‌تر |

**تصمیم:** WPF + MVVM + `Microsoft.Extensions.DependencyInjection` + CommunityToolkit.Mvvm + Serilog + xUnit.

**Target Framework:** `net8.0-windows` ترجیحی است (LTS). اگر SDK 8 روی ماشین توسعه نباشد، Phase 0 روی `net6.0-windows` Build می‌شود و با نصب SDK 8 به net8 مهاجرت می‌کند. APIهای استفاده‌شده با هر دو سازگارند.

WinUI 3 رد شد چون بسته‌بندی و پایداری برای یک ابزار شخصی USB در MVP هزینهٔ بی‌فایده دارد.

## ADR-003 — Android: Kotlin, minSdk 26, targetSdk 35

**وضعیت:** پذیرفته شده (Phase 0)

MediaProjection از API 21، Notification Listener از API 18، Accessibility قدیمی است. اما:

- Foreground Service types از API 29 جدی می‌شوند و در API 34 اجباری‌اند.
- محدودیت کلیپ‌بورد از API 29 سخت است.
- minSdk 26 (Android 8) Notification channel و سرویس پایدار را ساده می‌کند و دستگاه‌های قدیمی‌تر از دایرهٔ پشتیبانی شخصی خارج‌اند.

**تصمیم:** Kotlin, Coroutines/Flow, ماژول‌های feature/core، JUnit برای domain، Instrumentation جدا.

## ADR-004 — مسیر کنترل اصلی: TCP روی RNDIS، نه ADB

**وضعیت:** پذیرفته شده (Phase 0)

**مسئله:** USB tethering روی خیلی از گوشی‌ها function را به `rndis` عوض می‌کند و interface ADB ناپدید می‌شود. اگر mirroring روی ADB باشد، روشن کردن اینترنت = قطع کنترل.

**تصمیم:**

- کانال A: RNDIS دست‌نخورده می‌ماند.
- کانال B/D: Agent روی `0.0.0.0:17890` گوش می‌دهد. کلاینت به IP تترینگ گوشی (معمولاً `192.168.42.129`) وصل می‌شود.
- کانال C: پورت `17891` برای فریم تصویر.
- ADB: کشف و نصب، هرگز تغییر USB function در مسیر عادی.

**رد شده:**

- فقط `adb forward` — می‌میرد اگر ADB interface برود.
- Wireless debugging به‌عنوان مسیر اجباری — به Wi-Fi وابسته است و هدف «USB» را نقض می‌کند.
- USB Accessory protocol — پیچیده و با RNDIS هم‌زمانی ضعیف.

## ADR-005 — JSON نسخه‌دار برای MVP، Protobuf بعداً

**وضعیت:** پذیرفته شده (Phase 0)

Envelope مشترک (`version`, `type`, `requestId`, `timestamp`, `payload`, `error`) در JSON است. Schemaها در `shared/protocol/schemas`. مهاجرت به Protobuf بدون عوض کردن `type`ها انجام می‌شود (فیلد `encoding` در handshake).

دلیل JSON: دیباگ انسانی، تست سریالایز ساده، بدون وابستگی codegen در Phase 0.

## ADR-006 — Accessibility برای ژست، MediaProjection برای تصویر

**وضعیت:** پذیرفته شده (Phase 0)

`input tap` از ADB shell برای MVP ممکن است، اما شکننده است و به ADB زنده نیاز دارد. AccessibilityService ژست را روی خود دستگاه اجرا می‌کند و با کانال TCP سازگار است.

MediaProjection تنها API رسمی capture است و رضایت کاربر را اجباری می‌کند — مطابق سیاست پروژه.

scrcpy/minicap سطح پایین‌ترند و اغلب به encoding خصوصی یا تغییر سیاست نیاز دارند؛ برای این پروژهٔ با رضایت صریح رد می‌شوند.

## ADR-007 — رمزنگاری اتصال از Phase 7، معماری از Phase 0

**وضعیت:** پذیرفته شده (Phase 0)

روی USB tethering ترافیک از کابل فیزیکی می‌گذرد، نه اینترنت عمومی. تهدید اصلی شبکهٔ عمومی نیست، اما:

- Secret pairing از Phase 0 در secure storage است.
- Handshake فیلدهای `sessionNonce` و `auth` را دارد.
- Transport پشت `IControlTransport` است تا TLS بدون عوض کردن Application layer اضافه شود.

در Phase 0/1 plaintext TCP روی لینک USB قابل قبول است چون کابل در مالکیت کاربر است. از Phase 7: TLS 1.3 با PSK حاصل از pairing.

## ADR-008 — بدون Docker برای Runtime

**وضعیت:** پذیرفته شده (Phase 0)

ویندوز دسکتاپ و Agent اندروید در Docker اجرا نمی‌شوند. `docker-compose` برای این Suite لازم نیست. فایل compose در repo گذاشته نمی‌شود تا مسیر اشتباه القا نشود.

## ADR-009 — لاگ امن به‌صورت پیش‌فرض

**وضعیت:** پذیرفته شده (Phase 0)

Serilog / Android Log هرگز `text`, `smsBody`, `clipboard`, `pairingCode`, `secret` را نمی‌نویسند. Redaction در لایهٔ logging، نه در UI.

## ADR-010 — تست سخت‌افزار از Unit جدا است

**وضعیت:** پذیرفته شده (Phase 0)

تست‌هایی که ADB یا گوشی واقعی می‌خواهند در `tests/hardware` و دستهٔ `[Trait("Category","Hardware")]` هستند. `dotnet test` پیش‌فرض آن‌ها را اجرا نمی‌کند.

## ADR-011 — دیکودر Media Foundation، رندر WriteableBitmap، تزریق Accessibility

**وضعیت:** پذیرفته شده (Phase 2)

### دیکودر: Media Foundation، نه FFmpeg

| گزینه | مزیت | هزینه |
| --- | --- | --- |
| Media Foundation (mfplat H.264/H.265 MFT) | داخل ویندوز است، DXVA وقتی OS بدهد، بدون DLL اضافه، بدون LGPL | COM/IMFTransform پیچیده است؛ باید پشت `IVideoDecoder` بماند |
| FFmpeg / Libavcodec | پیاده‌سازی بالغ، Annex-B ساده | باید باینری native پخش شود، مجوز LGPL/GPL، اندازهٔ نصب |

**تصمیم:** Media Foundation. تست‌ها فقط `IVideoDecoder` و codec خام BGRA/JPEG را می‌زنند و mfplat را بار نمی‌کنند.

### رندر: WriteableBitmap، نه D3DImage

خروجی MFT/WIC روی CPU به‌صورت NV12→BGRA یا BGRA است. `WriteableBitmap.WritePixels` یک کپی است. `D3DImage` به بافت D3D9 و interop جدا نیاز دارد و وقتی فریم از CPU می‌آید سود ندارد.

### تزریق ورودی اندروید: AccessibilityService، نه SharedPreferences

SharedPreferences ذخیرهٔ کلید/مقدار است، API تزریق لمس نیست. AccessibilityService با رضایت کاربر در Settings، `dispatchGesture` و global Back/Home/Recents را می‌دهد و روی TCP 17890 کار می‌کند بدون ADB.

ویندوز رویداد را با layout همان `INPUT`ی که `SendInput` می‌سازد مدل می‌کند، اما آن را روی دسکتاپ محلی تزریق نمی‌کند (روی همان پنجرهٔ WPF کلیک می‌شد). پیام روی کانال کنترل می‌رود.

## ADR-012 — Pairing با PIN + SAS و ذخیرهٔ DPAPI / Keystore

**وضعیت:** پذیرفته شده (Phase 3)

PIN شش‌رقمی فقط روی گوشی نمایش داده می‌شود (TTL ۱۲۰ ثانیه، قفل بعد از ۵ تلاش). پس از تطابق، Session Token ۳۲ بایتی صادر می‌شود. SAS شش‌رقمی از SHA-256 همان توکن برای Numeric Comparison روی هر دو UI است.

ذخیره: ویندوز `ProtectedData` (DPAPI CurrentUser) روی فایل محلی؛ تست‌ها `PassthroughDataProtector` + `InMemorySecureStore`. اندروید EncryptedSharedPreferences (AES-256 GCM، کلید در Android Keystore)؛ تست دامنه `InMemoryPairingStore`.

توکن، PIN و SAS در Serilog/Logcat نوشته نمی‌شوند (`LogRedaction` / `RedactingLogger`).


