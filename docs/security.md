# امنیت و حریم خصوصی

## اصول

1. دستگاه متعلق به کاربر است؛ رضایت در UI اندروید و ویندوز دیده می‌شود.
2. هیچ credentialای plaintext روی دیسک نیست.
3. Log متن SMS، اعلان، کلیپ‌بورد، pairing code، یا password ندارد.
4. فرمان ناشناخته اجرا نمی‌شود.
5. قطع اتصال یک کلیک / یک Notification action است.

## Pairing

- کد کوتاه فقط روی صفحهٔ گوشی؛ TTL محدود (پیش‌فرض ۱۲۰ ثانیه).
- پس از تأیید، `pairingId` + `secret` تصادفی ۳۲ بایت.
- ویندوز: `ProtectedData.Protect` (DPAPI، CurrentUser).
- اندروید: EncryptedSharedPreferences (AES-256 GCM).
- Unpair هر دو طرف را پاک می‌کند.

## اعتبارسنجی فرمان

`CommandProcessor` فقط `type`های موجود در پروتکل نسخهٔ مذاکره‌شده را می‌پذیرد. Payload با schema چک می‌شود. مختصات خارج از صفحه رد می‌شود.

## شبکه

Phase 0–6: TCP روی کابل USB. Phase 7: TLS-PSK.

Bind اندروید روی `0.0.0.0` است چون RNDIS یک interface جدا است؛ فایروال گوشی ترافیک اینترنت موبایل را به این پورت forward نمی‌کند. با این حال فقط session pairشده accept می‌شود.

## قطع سریع

- ویندوز: دکمهٔ Disconnect فوری socket را می‌بندد.
- اندروید: Notification Foreground → Stop؛ Accessibility می‌تواند غیرفعال شود.
- Heartbeat از دست‌رفته → حالت Error، بدون اجرای فرمان باقی‌مانده.
