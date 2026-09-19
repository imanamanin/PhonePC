# PcPhone

کنترل گوشی اندروید از ویندوز داخل **Chrome** یا **Firefox**. اینترنت USB tethering قطع نمی‌شود.

Control your Android phone from Windows in **Chrome** or **Firefox**. USB tethering for PC internet stays on.

تحكّم بهاتف أندرويد من ويندوز عبر **Chrome** أو **Firefox**. تترينغ USB للإنترنت يبقى يعمل.

---

## نصب برای کاربر / For users / للمستخدم

**همهٔ فایل‌های نصبی اینجاست — فقط این سه تا را بردارید:**

**All install files are here — copy only these three:**

**كل ملفات التثبيت هنا — خذ هذه الثلاثة فقط:**

| # | فایل در پوشه [`install/`](install/) | نصب روی |
| --- | --- | --- |
| ۱ | [`1-android-PcPhone.apk`](install/1-android-PcPhone.apk) | گوشی |
| ۲ | [`2-chrome-extension.zip`](install/2-chrome-extension.zip) | کامپیوتر (Chrome) |
| ۳ | [`3-firefox-extension.zip`](install/3-firefox-extension.zip) | کامپیوتر (Firefox) |

راهنمای فارسی / English / العربية با عکس مراحل: **[`install/README.md`](install/README.md)**

خلاصه:

1. APK را روی گوشی نصب کنید → Accessibility و Capture را روشن کنید.
2. زیپ کروم **یا** فایرفاکس را از حالت فشرده خارج کنید و به‌صورت unpacked / temporary add-on بارگذاری کنید.
3. USB tethering یا همان وای‌فای روتر → در افزونه **اتصال USB** یا **اتصال Wi-Fi**. IP لازم نیست.
4. فایل را روی تصویر گوشی رها کنید تا به گوشی برود.

Chrome **or** Firefox — not both required. The old Windows desktop app is optional for developers.

---

## چه چیز را کپی نکنید / Do not copy / لا تنسخ

| پوشه | مال کیست |
| --- | --- |
| `android-agent/` | سورس اپ گوشی (اگر APK آماده است لازم نیست) |
| `chrome-extension/` | سورس افزونه کروم (همان محتوای زیپ نصب) |
| `firefox-extension/` | سورس افزونه فایرفاکس |
| `windows-client/` | برنامهٔ قدیمی ویندوز — برای استفادهٔ عادی لازم نیست |
| `docs/` `scripts/` `shared/` | مستندات و پروتکل توسعه |

---

## Developer

Windows 10+, .NET 8, Android Studio / JDK 17, compileSdk 35.

```powershell
.\scripts\check-env.ps1
.\scripts\build-windows.ps1
```

Android: open `android-agent/` in Android Studio, or `gradle :app:assembleDebug`.

USB tethering (internet) stays separate from control ports **17890** / **17891** / **17893**.

Architecture: [`docs/architecture.md`](docs/architecture.md). Protocol: [`shared/protocol/protocol.md`](shared/protocol/protocol.md).
