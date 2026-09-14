# مجوزهای اندروید

همهٔ مجوزها در UI Agent و در پنل وضعیت ویندوز دیده می‌شوند. هیچ مجوزی به‌صورت silent گرفته نمی‌شود.

## جدول مجوزها

| قابلیت | Permission / Component | نوع رضایت | اجباری برای MVP؟ | اگر نباشد |
| --- | --- | --- | --- | --- |
| Screen capture | `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PROJECTION`, MediaProjection token | Dialog سیستم هر بار / تا revoke | برای دیدن صفحه: بله | کنترل متنی و اعلان می‌ماند؛ تصویر نیست |
| ژست لمسی | Accessibility Service | Settings دستی کاربر | برای Tap/Swipe از Agent: بله | فقط fallback ADB اگر زنده باشد |
| اعلان‌ها | Notification Listener | Settings دستی کاربر | برای Notification Center: بله | پنل اعلان خالی با توضیح |
| SMS خواندن | `READ_SMS` runtime | Dialog سیستم | خیر، opt-in | Messages Panel غیرفعال |
| SMS دریافت | `RECEIVE_SMS` | Dialog سیستم | خیر | رویداد جدید نمی‌آید |
| SMS ارسال | `SEND_SMS` | Dialog سیستم | خیر | دکمهٔ ارسال disable |
| باتری / شارژ | بدون permission خاص | — | خیر | از sticky broadcast |
| شبکه | `ACCESS_NETWORK_STATE` | نصب | بله برای وضعیت | فیلد شبکه Unknown |
| اینترنت Agent | `INTERNET` | نصب | بله (TCP روی USB) | اتصال ممکن نیست |
| Foreground | `POST_NOTIFICATIONS` (API 33+) | Dialog | بله برای سرویس | سرویس ممکن است محدود شود |
| ذخیره pairing | بدون storage عمومی؛ EncryptedSharedPreferences | — | بله | Pairing پایدار نمی‌ماند |
| فایل (MediaStore) | `READ_MEDIA_*` / SAF | کاربر | Phase 6 | File transfer محدود به SAF |
| کلیپ‌بورد | بدون permission رسمی؛ محدودیت OS | فوکوس اپ | خیر | Sync فقط وقتی Agent در پیش‌زمینه است |

## سرویس‌هایی که کاربر باید دستی روشن کند

1. **Accessibility** — Settings → Accessibility → Phone Control Agent
2. **Notification Listener** — Settings → Notification access
3. **MediaProjection** — dialog هنگام Start capture
4. **SMS** — runtime prompt، فقط اگر کاربر Messages را در Settings Agent روشن کند

راهنمای داخل اپ برای هر کدام یک deep-link به صفحهٔ سیستم دارد.

## ویندوز

ویندوز permission اندروید را grant نمی‌کند. فقط وضعیت را از Agent می‌پرسد (`permissions.status`) و در UI نشان می‌دهد.

ADB USB debugging یک مجوز جدا روی گوشی است و فقط برای bootstrap لازم است، نه برای جلسهٔ کنترل روی RNDIS.
