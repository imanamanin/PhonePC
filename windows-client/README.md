# Windows Client

WPF desktop app. Independent from the Android Gradle project.

```powershell
..\scripts\check-env.ps1
..\scripts\build-windows.ps1
..\scripts\test-windows.ps1
dotnet run --project src\PhoneControl.Desktop\PhoneControl.Desktop.csproj
```

Phase 3 requires a 6-digit PIN from the phone, then stores a session token with DPAPI. Battery and charge appear on the status bar.

Phase 2 streams H.264 on TCP **17891** and sends pointer/keyboard on the control channel. Latency is shown in the status bar (ms). Capture never starts through ADB.

Phase 1 discovers USB-tether adapters (RNDIS/NDIS/Ethernet on `192.168.42.x` / `192.168.137.x`) and falls back to `192.168.42.129:17890`. ADB is optional (`adb devices -l` only) and never changes USB mode.

Handshake, heartbeat, and Channel C framing can be verified without a phone via `HandshakeHeartbeatTests` and `VideoFramingTests`.

Hardware tests are `[Trait("Category","Hardware")]` and excluded from `scripts/test-windows.ps1`.
