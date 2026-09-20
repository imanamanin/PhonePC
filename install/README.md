# PcPhone — دانلود و نصب / Download & install / التحميل والتثبيت

دانلود مستقیم از گیت‌هاب (جایگزین نسخه‌های قبلی):

Direct GitHub download (replaces older builds):

تحميل مباشر من GitHub (يستبدل النسخ السابقة):

| # | فایل / File | لینک دانلود / Download |
| --- | --- | --- |
| ۱ | اپ اندروید | [**1-android-PcPhone.apk**](https://github.com/imanamanin/PhonePC/raw/master/install/1-android-PcPhone.apk) |
| ۲ | افزونه کروم | [**2-chrome-extension.zip**](https://github.com/imanamanin/PhonePC/raw/master/install/2-chrome-extension.zip) |
| ۳ | افزونه فایرفاکس (بدون تغییر) | [3-firefox-extension.zip](https://github.com/imanamanin/PhonePC/raw/master/install/3-firefox-extension.zip) |

همین سه فایل در همین پوشه هم هستند. سورس در پوشه‌های دیگر مخزن است.

These three files are also in this folder. Source code lives elsewhere in the repo.

هذه الملفات الثلاثة موجودة في هذا المجلد أيضاً. الشيفرة في مجلدات أخرى.

| ترتیب | فایل | نصب روی |
| --- | --- | --- |
| ۱ | `1-android-PcPhone.apk` | **اندروید** |
| ۲ | `2-chrome-extension.zip` | **Chrome** روی ویندوز |
| ۳ | `3-firefox-extension.zip` | Firefox روی ویندوز (نسخهٔ قبلی؛ در این انتشار عوض نشده) |

کلاینت ویندوز جدا لازم نیست. برای بیشتر کاربران: **اندروید + Chrome**.

You do not need the Windows desktop app. For most users: **Android + Chrome**.

لا تحتاج برنامج ويندوز منفصل. لمعظم المستخدمين: **أندرويد + Chrome**.

---

## فارسی — اندروید و Chrome

### ۱) گوشی — فقط این APK

1. از جدول بالا `1-android-PcPhone.apk` را دانلود کنید، یا همان فایل را از این پوشه به گوشی بفرستید.
2. روی گوشی بازش کنید و نصب کنید. اگر اندروید هشدار «منبع ناشناس» داد، نصب از این فایل را اجازه بدهید.
3. اپ **PcPhone** را باز کنید.
4. **دسترسی (Accessibility)** را روشن کنید.
5. **شروع تصویر (Capture)** را بزنید و پنجرهٔ سیستم را بپذیرید. اگر اجازهٔ نوتیفیکیشن خواست، بدهید.
6. گوشی را با **USB tethering** به کامپیوتر وصل کنید، **یا** وای‌فای گوشی و کامپیوتر را به **همان روتر** وصل کنید.

نسخهٔ قبلی اپ را حذف کنید و این APK را جایگزین کنید.

### ۲) کامپیوتر — افزونهٔ Google Chrome

1. `2-chrome-extension.zip` را دانلود و از حالت فشرده خارج کنید. یک پوشه می‌گیرید.
2. در کروم بروید به: `chrome://extensions`
3. **Developer mode** را روشن کنید.
4. اگر نسخهٔ قبلی افزونه را دارید، **Remove** کنید یا **Reload** بزنید.
5. **Load unpacked** را بزنید و **همان پوشهٔ از حالت فشرده خارج‌شده** را انتخاب کنید (فایلی که داخلش `manifest.json` است). نسخه باید **0.1.7** باشد.
6. اگر کروم اجازهٔ دسترسی به سایت‌ها را خواست، تأیید کنید.
7. آیکون **PC Phone** را بزنید. **اتصال USB** یا **اتصال Wi-Fi**. IP لازم نیست.

### فایل بکشید

بعد از وصل شدن، فایل را از دسکتاپ روی تصویر گوشی در افزونه رها کنید. از گوشی: در اپ **ارسال به ویندوز** یا Share به PcPhone.

### فایرفاکس

افزونهٔ فایرفاکس در این انتشار عوض نشده است. اگر قبلاً نصب کرده‌اید همان را نگه دارید، یا از `3-firefox-extension.zip` استفاده کنید.

---

## English — Android and Chrome

### 1) Phone — only this APK

1. Download `1-android-PcPhone.apk` from the table above, or copy it from this folder to the phone.
2. Open it and install. Allow install from this file if Android asks.
3. Open **PcPhone**.
4. Turn on **Accessibility**.
5. Tap **Start capture** and accept the system dialog. Allow notifications if asked.
6. Turn on **USB tethering**, **or** join the phone and PC to the **same Wi-Fi router**.

Uninstall the previous app, then install this APK.

### 2) PC — Google Chrome extension

1. Download and unzip `2-chrome-extension.zip`.
2. Open `chrome://extensions`.
3. Enable **Developer mode**.
4. Remove or **Reload** any older PC Phone extension.
5. **Load unpacked** and pick the unzipped folder (the one that contains `manifest.json`). Version should be **0.1.7**.
6. Allow site access if Chrome asks.
7. Click **PC Phone**, then **USB** or **Wi-Fi**. You do not type an IP.

### Drag and drop

After connect, drop a file from the desktop onto the phone image. From the phone: **Send to Windows** in the app, or Share to PcPhone.

### Firefox

The Firefox add-on is unchanged in this release. Keep what you already have, or use `3-firefox-extension.zip`.

---

## العربية — أندرويد وChrome

### ١) الهاتف — هذا الـ APK فقط

1. حمّل `1-android-PcPhone.apk` من الجدول أعلاه، أو انسخه من هذا المجلد إلى الهاتف.
2. افتحه وثبّته. اسمح بالتثبيت من هذا الملف إذا طلب أندرويد ذلك.
3. افتح تطبيق **PcPhone**.
4. فعّل **إمكانية الوصول (Accessibility)**.
5. اضغط **بدء الالتقاط** واقبل نافذة النظام. اسمح بالإشعارات إن طُلب ذلك.
6. فعّل **USB tethering**، **أو** صِل الهاتف والكمبيوتر بنفس راوتر الـ Wi-Fi.

احذف التطبيق السابق ثم ثبّت هذا الـ APK.

### ٢) الكمبيوتر — إضافة Google Chrome

1. حمّل `2-chrome-extension.zip` وفك ضغطه.
2. افتح `chrome://extensions`.
3. فعّل **Developer mode**.
4. احذف الإضافة القديمة أو اضغط **Reload**.
5. **Load unpacked** واختر المجلد بعد فك الضغط (الذي فيه `manifest.json`). الإصدار **0.1.7**.
6. اسمح بالوصول إلى المواقع إذا طلب كروم ذلك.
7. اضغط أيقونة **PC Phone** ثم **USB** أو **Wi-Fi**. لا حاجة لكتابة IP.

### سحب الملفات

بعد الاتصال اسحب ملفاً من سطح المكتب إلى صورة الهاتف. من الهاتف: **إرسال إلى ويندوز** أو المشاركة إلى PcPhone.

### فايرفوكس

إضافة فايرفوكس لم تتغيّر في هذا الإصدار. أبقِ نسختك الحالية أو استخدم `3-firefox-extension.zip`.
