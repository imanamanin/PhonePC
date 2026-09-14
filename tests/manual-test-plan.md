# Manual / hardware test plan

Unit tests never require a phone. Use this list on a real device after Phase 1+.

## Preconditions

- Phone you own, USB cable, USB debugging (for install) optional after Agent is installed
- USB tethering enabled
- Windows Client running

## Checks

1. Enabling USB tethering still gives the PC internet.
2. Starting the Agent does not disable the RNDIS adapter.
3. Disconnect in the Windows UI does not disable tethering.
4. Killing ADB (`adb kill-server`) does not drop an already-open TCP control session (Phase 3+).
5. FLAG_SECURE apps show a protected-content message, not a crash.
6. Logs never contain SMS body, notification text, clipboard text, or pairing code.

Record OEM, Android version, and whether `rndis,adb` stays composite.
