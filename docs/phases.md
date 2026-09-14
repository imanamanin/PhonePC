# برنامهٔ فازبندی

بدون تأیید کاربر، از فازی به فاز بعد نمی‌رویم. هر فاز باید Build شود، تست مربوطه را رد کند، و محدودیت‌ها را ثبت کند.

| فاز | هدف | خروجی قابل مشاهده |
| --- | --- | --- |
| **0** Architecture | Repo، دو پروژه، پروتکل، interface، docs | انجام شد |
| **1** Device Connection | کشف ADB + کشف RNDIS، وضعیت اتصال، loopback TCP | انجام شد |
| **2** Screen and Input | اسکرین استریم H.264، Tap/Swipe/کلید/pinch، latency | انجام شد |
| **3** Pairing, auth, state sync | PIN/SAS، Session Token، DPAPI/Keystore، باتری | انجام شد |
| **4** Screen Capture | MediaProjection + FGS + نمایش ویندوز | استریم واقعی با رضایت |
| **5** Notifications and Clipboard | Listener + sync + UI | اعلان‌ها روی PC |
| **6** SMS and File Transfer | Adapter + progress/cancel | پنل پیام و فایل |
| **7** Reliability and Security | Heartbeat، reconnect، TLS، ذخیره امن، log | نشست پایدار |
| **8** UX and Packaging | Installer، APK release، راهنما | قابل نصب روی دستگاه واقعی |

قوانین خروج هر فاز در گزارش همان فاز (`docs/PHASE-N-REPORT.md`) ثبت می‌شود.
