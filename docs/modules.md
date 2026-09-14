# فهرست ماژول‌ها

## Windows Client (`windows-client/`)

| پروژه | مسئولیت |
| --- | --- |
| `PhoneControl.Domain` | Entity، enum وضعیت، interfaceهای پورت |
| `PhoneControl.Application` | Use-case: connect, pair, map input, clipboard policy |
| `PhoneControl.Protocol` | Envelope JSON، typeها، serialize |
| `PhoneControl.Adb` | اجرای platform-tools پشت `IAdbClient`؛ در Unit تست mock می‌شود |
| `PhoneControl.Screen` | نگاشت فریم به تصویر WPF، FPS/latency debug |
| `PhoneControl.Input` | تبدیل مختصات پنجره به پیکسل گوشی |
| `PhoneControl.Notifications` | مدل اعلان ویندوز، mapping از event پروتکل |
| `PhoneControl.Infrastructure` | TCP transport، DPAPI store، فایل، زمان |
| `PhoneControl.Desktop` | WPF، ViewModel، بدون منطق پروتکل |
| `PhoneControl.Tests` | Unit؛ Hardware با Trait جدا |

## Android Agent (`android-agent/`)

| ماژول | مسئولیت |
| --- | --- |
| `app` | UI، Application class، DI، Foreground orchestration |
| `core-domain` | مدل فرمان، نتیجه، capability — JVM |
| `core-data` | ذخیره pairing، repository |
| `core-network` | TCP server، framing |
| `core-permissions` | وضعیت مجوزها |
| `core-logging` | Logger با redaction |
| `feature-screen-capture` | MediaProjection |
| `feature-accessibility` | ژست |
| `feature-notifications` | Listener |
| `feature-sms` | Adapter SMS |
| `feature-clipboard` | Sync با anti-loop |
| `feature-device-status` | باتری و مدل |
| `feature-pairing` | کد و تأیید کاربر |
| `feature-settings` | صفحهٔ مجوز و راهنما |

## Shared

`shared/protocol` منبع حقیقت typeها است. کد C# و Kotlin معادل دستی دارند؛ در Phaseهای بعدی می‌توان codegen از JSON Schema اضافه کرد.
