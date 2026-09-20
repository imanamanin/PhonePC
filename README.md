# PcPhone

کنترل گوشی اندروید از ویندوز داخل **Chrome** یا **Firefox**. اینترنت USB tethering قطع نمی‌شود.

Control your Android phone from Windows in **Chrome** or **Firefox**. USB tethering for PC internet stays on.

تحكّم بهاتف أندرويد من ويندوز عبر **Chrome** أو **Firefox**. تترينغ USB للإنترنت يبقى يعمل.

---

## نصب برای کاربر / For users / للمستخدم

**دانلود مستقیم (جایگزین نسخه‌های قبلی اپ و افزونهٔ کروم):**

**Direct download (replaces the previous Android app and Chrome extension):**

**تحميل مباشر (يستبدل تطبيق أندرويد وإضافة كروم السابقة):**

| # | فایل | دانلود |
| --- | --- | --- |
| ۱ | اپ اندروید | [1-android-PcPhone.apk](https://github.com/imanamanin/PhonePC/raw/master/install/1-android-PcPhone.apk) |
| ۲ | افزونه Chrome | [2-chrome-extension.zip](https://github.com/imanamanin/PhonePC/raw/master/install/2-chrome-extension.zip) |
| ۳ | افزونه Firefox (بدون تغییر) | [3-firefox-extension.zip](https://github.com/imanamanin/PhonePC/raw/master/install/3-firefox-extension.zip) |

همین فایل‌ها در پوشه [`install/`](install/) هم هستند.

راهنمای فارسی / English / العربية: **[`install/README.md`](install/README.md)**

خلاصه:

1. APK را روی گوشی نصب کنید (نسخهٔ قبلی را جایگزین کنید) → Accessibility و Capture را روشن کنید.
2. زیپ کروم را از حالت فشرده خارج کنید و با **Load unpacked** بارگذاری کنید (نسخه 0.1.7). افزونهٔ فایرفاکس در این انتشار عوض نشده.
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
