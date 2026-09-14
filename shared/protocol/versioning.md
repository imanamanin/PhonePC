# نسخه‌بندی پروتکل

- نسخهٔ جاری سند: **1**
- افزایش `version` وقتی فیلد اجباری اضافه یا معنی type عوض شود.
- فیلد اختیاری جدید در همان version مجاز است؛ گیرنده باید ناشناخته را نادیده بگیرد.
- `session.hello` بازهٔ `protocolMin`/`protocolMax` را می‌فرستد. Agent بزرگ‌ترین نسخهٔ مشترک را انتخاب می‌کند.
- اگر اشتراک نبود: `error.code = UNSUPPORTED` و اتصال بسته می‌شود.
- Encoding بعدی: `supportedEncodings: ["json","protobuf"]`. انتخاب در hello_ack.
- Type names پایدار می‌مانند تا Protobuf map شود.

شاخهٔ schema: `schemas/v1/`.
