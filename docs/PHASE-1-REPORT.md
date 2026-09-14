# گزارش Phase 1 — Device Connection

## هدف مرحله

کشف خودکار تترینگ/RNDIS، کشف اختیاری ADB بدون تغییر USB function، ماشین وضعیت اتصال، handshake و heartbeat روی TCP 17890، و تست loopback بدون گوشی فیزیکی.

## فایل‌های ایجادشده

- `windows-client/src/PhoneControl.Domain/NetworkPorts.cs`
- `windows-client/src/PhoneControl.Protocol/SessionPayloads.cs`
- `windows-client/src/PhoneControl.Application/ClipboardLoopGuard.cs` (جدا شد)
- `windows-client/src/PhoneControl.Application/ReconnectPolicy.cs` (جدا شد)
- `windows-client/src/PhoneControl.Application/ConnectionStateMachine.cs`
- `windows-client/src/PhoneControl.Application/TetherEndpointSelector.cs`
- `windows-client/src/PhoneControl.Application/ConnectionOptions.cs`
- `windows-client/src/PhoneControl.Application/FakeNetworkDiscovery.cs`
- `windows-client/src/PhoneControl.Infrastructure/LengthPrefixedFrame.cs`
- `windows-client/src/PhoneControl.Tests/ConnectionHarness.cs`
- `windows-client/src/PhoneControl.Tests/Fakes/AutoReplyingTransport.cs`
- `windows-client/src/PhoneControl.Tests/Unit/ConnectionStateMachineTests.cs`
- `windows-client/src/PhoneControl.Tests/Unit/NetworkDiscoveryTests.cs`
- `windows-client/src/PhoneControl.Tests/Unit/AdbClientTests.cs`
- `windows-client/src/PhoneControl.Tests/Integration/LoopbackAgentServer.cs`
- `windows-client/src/PhoneControl.Tests/Integration/HandshakeHeartbeatTests.cs`
- `docs/PHASE-1-REPORT.md`

## فایل‌های تغییرکرده

- `ConnectionManager` (جلسهٔ پس‌زمینه، handshake، heartbeat، reconnect)
- `CompositeDeviceDiscovery` (شبکه + ADB non-blocking)
- `ProcessAdbClient` / parser / `adb devices -l` فقط
- `TcpControlTransport` (framing مشترک)
- `App.xaml.cs` و `MainViewModel` / `MainWindow.xaml`
- تست‌های قبلی Connection/Composite برای constructor جدید
- `README.md`, `docs/phases.md`, `windows-client/README.md`, `tests/integration/README.md`

## قابلیت‌های پیاده‌سازی‌شده

- `INetworkDiscoveryService` + `INetworkInterfaceProbe`: آداپتور Ethernet/NDIS/RNDIS، gateway، subnet `192.168.42.x` / `192.168.137.x`، fallback `192.168.42.129:17890`
- ADB فقط `devices -l`؛ نبود باینری → لیست خالی، بدون شکست برنامه
- State machine: Disconnected → Connecting → PairingRequired | Connected → Reconnecting → Error (و بازگشت به Disconnected)
- Handshake `session.hello` / `session.hello_ack` و Ping/Pong روی TCP length-prefixed
- Exponential backoff از طریق `ReconnectPolicy` + `IDelay`
- UI ویندوز: وضعیت، endpoint فعال، لیست کشف‌شده، ADB/tethering

## تست‌های اضافه‌شده

۵۴ تست پاس (قبلاً ۲۲). پوشش:

- جدول کامل ترنزیشن state machine
- Timeout handshake → Reconnecting → Error `TIMEOUT`
- Timeout heartbeat → Reconnecting
- Cancel هنگام Connect → Disconnected
- کشف شبکه با probe جعلی
- ADB parser، missing binary، runner فقط devices-list
- Integration loopback TCP: handshake + heartbeat بدون گوشی

## دستور Build

```powershell
.\scripts\build-windows.ps1
```

نتیجه: **Build succeeded, 0 warning, 0 error**

## دستور Test

```powershell
.\scripts\test-windows.ps1
```

نتیجه: **54 passed, 0 failed** (Hardware با فیلتر `Category!=Hardware` اجرا نمی‌شود)

## نحوه‌ی اجرای محلی

```powershell
$env:PATH = "$env:LOCALAPPDATA\dotnet;$env:PATH"
dotnet run --project windows-client\src\PhoneControl.Desktop\PhoneControl.Desktop.csproj
```

Refresh: آداپتورهای تترینگ و fallback را نشان می‌دهد. Connect بدون Agent واقعی پس از timeout به Reconnecting و در نهایت Error می‌رود؛ USB tethering را تغییر نمی‌دهد.

Loopback بدون UI: تست‌های `PhoneControl.Tests.Integration.HandshakeHeartbeatTests` یک `TcpListener` روی `127.0.0.1` بالا می‌آورند و hello/pong را جواب می‌دهند.

## محدودیت‌های فعلی

- Pairing واقعی (کد/QR و secret) هنوز Phase 3 است؛ `PairingRequired` یعنی handshake موفق و Agent pairing می‌خواهد
- TCP server روی گوشی هنوز bind نمی‌شود (Agent Phase 3)
- Screen/Input شروع نشده (Phase 2)
- TLS نیست (Phase 7)
- اگر گوشی روی IP غیر از fallback/subnetهای شناخته‌شده باشد، تا وقتی gateway آداپتور دیده شود کشف می‌شود؛ در غیر این صورت فقط fallback امتحان می‌شود

## مرحله‌ی بعد

**Phase 2 — Basic Screen and Input** فقط پس از تأیید شما: اسکرین‌شات/فریم اولیه و Tap/Swipe/کلید. وارد آن نمی‌شوم تا تأیید شود.
