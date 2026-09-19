# PC Phone Firefox extension

همان افزونهٔ PC Phone برای Firefox: تصویر و کنترل گوشی در نوار کناری مرورگر، روی USB tether یا Wi-Fi. PIN لازم نیست.

## نصب موقت (توسعه)

1. Firefox را باز کنید و به `about:debugging#/runtime/this-firefox` بروید.
2. **Load Temporary Add-on…** را بزنید.
3. فایل `firefox-extension/manifest.json` را انتخاب کنید.
4. روی آیکون PC Phone در نوار ابزار کلیک کنید تا sidebar باز شود (یا از View → Sidebar).

افزونهٔ موقت بعد از بستن Firefox برداشته می‌شود؛ دوباره Load Temporary کنید.

## استفاده

1. اپ گوشی را باز کنید، Accessibility و Capture را فعال کنید.
2. **USB:** تترینگ را روشن کنید و **اتصال USB** را بزنید.
3. **Wi-Fi:** گوشی و کامپیوتر را به همان روتر وصل کنید. IP وای‌فای روی صفحهٔ گوشی است. **اتصال Wi-Fi** را بزنید.
4. اگر پیدا نشد، همان IP را در کادر بنویسید.

پروتکل همان پورت **17893** است؛ اپ اندروید جداگانه‌ای برای فایرفاکس لازم نیست.
