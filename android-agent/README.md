# Android Agent

Kotlin agent that runs on the phone you own. Independent Gradle build from the Windows client.

## Phase 3

- 6-digit PIN on the phone UI only; Windows submits `pairing.submit`.
- Session token in EncryptedSharedPreferences (Android Keystore).
- `device.status` / `state.update` for battery and charging.
- Control commands other than session/pairing require a paired session (`UNPAIRED` otherwise).

## Phase 2

- Control TCP **17890** (handshake, `video.*`, `input.*`). Does not change USB tethering.
- Screen TCP **17891** after the user accepts the **system MediaProjection dialog**. ADB is never used to start capture.
- H.264 `MediaCodec` encoder, hardware encoder first.
- Touch/scroll/pinch/Back/Home/Recents through the Accessibility service the user enables in Settings.

## Build

Requires JDK 17 and Android SDK (compileSdk 35). This machine may not have them; Android Studio installs both.

1. Copy `local.properties.example` to `local.properties` and set `sdk.dir`.
2. Open `android-agent/` in Android Studio, or:

```powershell
..\scripts\build-android.ps1
```

## Permissions

The user must enable Accessibility, Notification access, MediaProjection (system dialog), and SMS (opt-in) from system settings. See `../docs/permissions.md`. `video.start` without that dialog returns `PERMISSION_DENIED`.

## USB tethering

The agent listens on TCP 17890/17891 so Windows can reach it through the RNDIS subnet. It does not change USB functions.
