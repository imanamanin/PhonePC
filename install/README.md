# PcPhone — فایل‌های نصب / Installation files / ملفات التثبيت

این پوشه فقط برای **نصب** است. سورس برنامه در پوشه‌های دیگر مخزن است.

This folder is only for **install**. Source code lives in the other repo folders.

هذا المجلد للـ**تثبيت** فقط. الشيفرة في مجلدات أخرى.

| ترتیب / Order / الترتيب | فایل / File | چه کار می‌کند / What | چه کسی نصب می‌کند |
| --- | --- | --- | --- |
| ۱ | `1-android-PcPhone.apk` | اپ گوشی | روی **اندروید** |
| ۲ | `2-chrome-extension.zip` | افزونه کروم | روی **ویندوز** اگر Chrome دارید |
| ۳ | `3-firefox-extension.zip` | افزونه فایرفاکس | روی **ویندوز** اگر Firefox دارید |

کلاینت ویندوز جدا لازم نیست. یکی از دو مرورگر کافی است.

You do not need the Windows desktop app. Chrome **or** Firefox is enough.

لا تحتاج برنامج ويندوز منفصل. كروم **أو** فايرفوكس يكفي.

---

## فارسی — نصب گام‌به‌گام

### ۱) گوشی — فقط این APK

1. فایل `1-android-PcPhone.apk` را به گوشی کپی کنید (USB، تلگرام به خودتان، یا دانلود از همین مخزن).
2. روی گوشی بازش کنید و نصب کنید. اگر اندروید هشدار «منبع ناشناس» داد، نصب از این فایل را اجازه بدهید.
3. اپ **PcPhone** را باز کنید.
4. **دسترسی (Accessibility)** را روشن کنید.
5. **شروع تصویر (Capture)** را بزنید و پنجرهٔ سیستم را بپذیرید.
6. گوشی را با **USB tethering** به کامپیوتر وصل کنید، **یا** وای‌فای گوشی و کامپیوتر را به **همان روتر** وصل کنید.

### ۲) کامپیوتر — کروم **یا** فایرفاکس

**کروم**

1. `2-chrome-extension.zip` را از حالت فشرده خارج کنید. یک پوشه می‌گیرید.
2. در کروم بروید به: `chrome://extensions`
3. **Developer mode** را روشن کنید.
4. **Load unpacked** را بزنید و **همان پوشهٔ از حالت فشرده خارج‌شده** را انتخاب کنید (فایلی که داخلش `manifest.json` است).
5. اگر کروم اجازهٔ دسترسی به سایت‌ها را خواست، تأیید کنید.
6. آیکون **PC Phone** را بزنید. **اتصال USB** یا **اتصال Wi-Fi**. IP لازم نیست.

**فایرفاکس**

1. `3-firefox-extension.zip` را از حالت فشرده خارج کنید.
2. داخل پوشه بروید به `native` و یک‌بار `install.ps1` را در PowerShell اجرا کنید (برای پیدا کردن IP شبکه).
3. در فایرفاکس بروید به: `about:debugging#/runtime/this-firefox`
4. **Load Temporary Add-on…** را بزنید و فایل `manifest.json` داخل پوشهٔ خارج‌شده را انتخاب کنید.
5. آیکون **PC Phone** را بزنید. **اتصال USB** یا **اتصال Wi-Fi**.

> افزونهٔ موقت فایرفاکس بعد از بستن مرورگر پاک می‌شود؛ دوباره Load Temporary کنید.

### فایل بکشید

بعد از وصل شدن، فایل را از دسکتاپ روی تصویر گوشی در افزونه رها کنید. از گوشی: در اپ **ارسال به ویندوز** یا Share به PcPhone.

---

## English — step by step

### 1) Phone — only this APK

1. Copy `1-android-PcPhone.apk` to the phone.
2. Open it and install. Allow install from this file if Android asks.
3. Open **PcPhone**.
4. Turn on **Accessibility**.
5. Tap **Start capture** and accept the system dialog.
6. Turn on **USB tethering**, **or** join the phone and PC to the **same Wi-Fi router**.

### 2) PC — Chrome **or** Firefox

**Chrome**

1. Unzip `2-chrome-extension.zip`.
2. Open `chrome://extensions`.
3. Enable **Developer mode**.
4. **Load unpacked** and pick the unzipped folder (the one that contains `manifest.json`).
5. Allow site access if Chrome asks.
6. Click **PC Phone**, then **USB** or **Wi-Fi**. You do not type an IP.

**Firefox**

1. Unzip `3-firefox-extension.zip`.
2. Run `native/install.ps1` once from that folder (helps the PC see local IPs).
3. Open `about:debugging#/runtime/this-firefox`.
4. **Load Temporary Add-on…** and choose `manifest.json` in the unzipped folder.
5. Click **PC Phone**, then **USB** or **Wi-Fi**.

> A temporary Firefox add-on is removed when Firefox quits. Load it again after restart.

### Drag and drop

After connect, drop a file from the desktop onto the phone image. From the phone: **Send to Windows** in the app, or Share to PcPhone.

---

## العربية — خطوة بخطوة

### ١) الهاتف — هذا الـ APK فقط

1. انسخ `1-android-PcPhone.apk` إلى الهاتف.
2. افتحه وثبّته. اسمح بالتثبيت من هذا الملف إذا طلب أندرويد ذلك.
3. افتح تطبيق **PcPhone**.
4. فعّل **إمكانية الوصول (Accessibility)**.
5. اضغط **بدء الالتقاط** واقبل نافذة النظام.
6. فعّل **USB tethering**، **أو** صِل الهاتف والكمبيوتر بنفس راوتر الـ Wi-Fi.

### ٢) الكمبيوتر — كروم **أو** فايرفوكس

**كروم**

1. فك ضغط `2-chrome-extension.zip`.
2. افتح `chrome://extensions`.
3. فعّل **Developer mode**.
4. **Load unpacked** واختر المجلد بعد فك الضغط (الذي فيه `manifest.json`).
5. اسمح بالوصول إلى المواقع إذا طلب كروم ذلك.
6. اضغط أيقونة **PC Phone** ثم **USB** أو **Wi-Fi**. لا حاجة لكتابة IP.

**فايرفوكس**

1. فك ضغط `3-firefox-extension.zip`.
2. نفّذ مرة واحدة `native/install.ps1` من ذلك المجلد.
3. افتح `about:debugging#/runtime/this-firefox`.
4. **Load Temporary Add-on…** واختر `manifest.json`.
5. اضغط **PC Phone** ثم **USB** أو **Wi-Fi**.

> الإضافة المؤقتة في فايرفوكس تُحذف عند إغلاق المتصفح. حمّلها مرة أخرى بعد إعادة الفتح.

### سحب الملفات

بعد الاتصال اسحب ملفاً من سطح المكتب إلى صورة الهاتف. من الهاتف: **إرسال إلى ويندوز** أو المشاركة إلى PcPhone.
