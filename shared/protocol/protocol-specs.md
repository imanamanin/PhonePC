# Protocol specs — Screen (Channel C) and Input (Channel B)

This addendum extends [`protocol.md`](protocol.md) for Phase 2. Channel split is unchanged: tethering stays on USB RNDIS, control JSON on TCP **17890**, elementary video on TCP **17891**. Capture is never started through ADB.

## video.start (Windows → Android, control)

```json
{
  "codec": "h264",
  "maxFps": 30,
  "maxWidth": 1280,
  "bitrateKbps": 4000,
  "port": 17891
}
```

Agent must not open MediaProjection unless the user already granted the system capture dialog. If the token is missing: `error.code = PERMISSION_DENIED`.

Preferred codec is **H.264** (`video/avc`). JPEG remains a fallback (`screen.*` still valid).

## video.stop

Stops the encoder and VirtualDisplay. Does not change USB tethering. Does not disable Accessibility.

## video.adapt (Windows → Android)

```json
{
  "maxFps": 15,
  "maxWidth": 720,
  "bitrateKbps": 1500
}
```

Sent when the client’s adaptive controller changes tier. Throttle: at most once every 2 seconds.

## video.frame (optional JSON on control)

Metadata only. Pixel/NAL bytes **never** ride on 17890.

```json
{
  "captureTimestamp": 0,
  "byteLength": 0,
  "keyframe": true,
  "width": 720,
  "height": 1600
}
```

## Channel C binary (17891)

TCP: 4-byte big-endian length, then body (max 8 MiB). Body:

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 1 | flags: bit0 keyframe, bit1 codec-config (SPS/PPS) |
| 1 | 8 | capture timestamp Unix ms, big-endian |
| 9 | 2 | width |
| 11 | 2 | height |
| 13 | 1 | codec: 1=H.264, 2=H.265, 3=JPEG, 4=raw BGRA (tests) |
| 14 | n | payload (Annex-B NALs for H.264) |

Windows connects **to the phone** on 17891 (same host as control). The agent listens; ADB reverse is not used.

## input.touch

`action`: `down` | `move` | `up` | `tap` | `swipe` | `scroll`

Scroll uses `delta` (wheel ticks) or `x2`/`y2`. Coordinates are phone pixels after letterbox mapping.

## input.pinch

```json
{
  "x": 200,
  "y": 400,
  "x2": 600,
  "y2": 800,
  "scale": 1.25
}
```

`scale` > 1 zoom in.

## input.key

Unchanged. `text` is not written to logs.

Windows captures pointer/keyboard from the video surface. Events are modeled with the Win32 `INPUT` layout (the same record `SendInput` uses) and **sent over the control channel**, not injected into the local desktop (that would click the WPF window). Android injects via Accessibility gestures.

## pairing.submit / pairing.accepted (Phase 3)

PIN is 6 digits, shown only on the phone. `sessionToken` is 32 random bytes, Base64 in JSON, stored with DPAPI / EncryptedSharedPreferences — never logged.

`sas` is six decimal digits: `SHA256(token)[0..2]` as unsigned 24-bit integer mod 1_000_000. Both peers compute the same value for numeric comparison.

`state.update` / `device.status` carry `batteryPercent`, `charging`, `network`. Windows shows battery on the status bar.
