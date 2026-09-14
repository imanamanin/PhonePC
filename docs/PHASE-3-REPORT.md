# گزارش Phase 3 — Pairing, Authentication & State Sync

## هدف

جفت‌سازی واقعی به‌جای `pairingRequired: false`، Session Token برای Trusted Device، ذخیرهٔ بدون plaintext، و همگام‌سازی وضعیت باتری/شارژ/شبکه. Phase 4 شروع نشده است.

## Pairing flow

1. `session.hello` بدون توکن معتبر → `pairingRequired: true`. PIN شش‌رقمی فقط روی UI گوشی (TTL ۱۲۰ ثانیه).
2. ویندوز `pairing.submit` با PIN می‌فرستد. عدم تطابق → `pairing.rejected` / `PIN_MISMATCH`. انقضا → `pairing.expired`. پنج تلاش → `locked`.
3. تطابق → `pairing.accepted` با `pairingId`، `sessionToken` (۳۲ بایت Base64)، `expiresAt` (۳۰ روز)، `sas` شش‌رقمی.
4. SAS = `SHA256(token)[0..2]` به‌صورت عدد ۲۴ بیتی unsigned mod 1_000_000. هر دو طرف همان رقم را نشان می‌دهند (Numeric Comparison).
5. hello بعدی با `pairingId` + `sessionToken` ذخیره‌شده → Trusted Device، بدون PIN.

فرمان‌های `video.*` / `input.*` بدون نشست pairشده `UNPAIRED` می‌گیرند. استریم تصویر فقط در حالت `Connected` شروع می‌شود.

## ذخیرهٔ امن

| طرف | Production | تست |
| --- | --- | --- |
| Windows | `ProtectedSecureStore` + `FileSecureStore` + `DpapiDataProtector` (`ProtectedData`, CurrentUser) | `InMemorySecureStore` + `PassthroughDataProtector` |
| Android | `EncryptedPairingStore` (EncryptedSharedPreferences / Android Keystore) | `InMemoryPairingStore` + `PairingEngine` خالص |

لاگ: `RedactingAppLogger` / `RedactingLogger` از نوشتن PIN، token، pairing-code، secret خودداری می‌کنند.

## State sync

`device.status` و `state.update`: `batteryPercent`, `charging`, `network`. بعد از Connected، کلاینت وضعیت را می‌پرسد. StatusBar ویندوز: `Battery N% charging|on battery` کنار latency.

## فایل‌های اصلی

- پروتکل: `shared/protocol/protocol.md`, `protocol-specs.md`, `examples/pairing-submit.json`, `examples/state-update.json`
- ویندوز: `PairingCrypto.cs`, `TrustedDeviceStore`, `SecureStorage.cs` (DPAPI), `ConnectionManager` (hello token, SubmitPin, Unpair), UI PIN/SAS/باتری
- اندروید: `PairingEngine.kt`, `AgentCommandRouter` (UNPAIRED + pairing), `EncryptedPairingStore`, `DeviceStatusReader`, PIN روی MainActivity
- تست: `PairingAndStatusTests`, `LoopbackTcp_SubmitPin_BecomesTrusted`, تست‌های Kotlin PIN/expiry/token

## Build / تست این ماشین

```powershell
.\scripts\build-windows.ps1
.\scripts\test-windows.ps1
```

**Build succeeded, 0 warning, 0 error. 77 passed, 0 failed** (۶۷ از Phase 2 حفظ شدند).

Android بدون JDK/SDK روی این PC فقط source است.

## خروج از فاز

Phase 3 کامل است. **Phase 4 شروع نمی‌شود تا تأیید کنید.**
