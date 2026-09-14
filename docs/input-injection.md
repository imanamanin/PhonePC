# تزریق ورودی (Phase 2)

ویندوز رویداد موس/کیبورد را از سطح تصویر می‌گیرد، به layout همان رکورد `INPUT` که Win32 `SendInput` استفاده می‌کند نگاشت می‌کند، و **روی TCP 17890** می‌فرستد. `user32!SendInput` روی دسکتاپ محلی صدا زده نمی‌شود؛ آن کار نشانگر را روی خود کلاینت WPF می‌انداخت، نه روی گوشی.

اندروید ژست را با `AccessibilityService.dispatchGesture` و کلیدهای سیستم را با `performGlobalAction` اجرا می‌کند.

## AccessibilityService در برابر SharedPreferences

| | AccessibilityService | SharedPreferences |
| --- | --- | --- |
| نقش واقعی | API رسمی ژست، با رضایت در Settings | ذخیرهٔ تنظیمات کلید/مقدار |
| Tap / swipe / pinch / scroll | بله، `dispatchGesture` | خیر — ورودی تزریق نمی‌کند |
| Back / Home / Recents | `GLOBAL_ACTION_*` | خیر |
| وابستگی به ADB | ندارد | ندارد |
| رضایت کاربر | روشن کردن سرویس در Accessibility | هیچ مجوزی برای input نمی‌دهد |
| محدودیت | بعضی OEMها ژست را روی قفل صفحه محدود می‌کنند؛ FLAG_SECURE مانع تصویر است نه لزوماً ژست | به‌عنوان مسیر input قابل استفاده نیست |

**تصمیم Phase 2:** فقط Accessibility. SharedPreferences برای pairing/settings می‌ماند (Phase 3+)، نه برای ماوس.

## نگاشت ویندوز

| رویداد سطح | Win32 flags / VK | پیام |
| --- | --- | --- |
| move | `MOUSEEVENTF_MOVE` | `input.touch` action=`move` |
| click / drag | `LEFTDOWN` / `MOVE` / `LEFTUP` | `down` / `move` / `up` |
| scroll | `MOUSEEVENTF_WHEEL` | `input.touch` action=`scroll` |
| pinch | manipulation scale | `input.pinch` |
| Esc / Backspace | VK_ESCAPE / VK_BACK | `input.key` `back` |
| Home | VK_HOME | `home` |

مختصات با letterbox از سطح WPF به پیکسل گوشی نگاشت می‌شوند.
