# Troubleshooting

Local development environment issues that aren't about the SDK's own code - things that have
tripped someone up once and are worth writing down before the next person (or the same person,
months later) has to rediscover the cause from scratch.

## `dotnet test` fails with "Application Control policy has blocked this file" (`0x800711C7`)

**Symptom:** `dotnet build` succeeds, but `dotnet test` fails at runtime with an error like
*"An Application Control policy has blocked this file. (0x800711C7)"* when the test host tries to
load a freshly built assembly.

**Cause:** Windows 11's **Smart App Control** blocking an unsigned, locally-built DLL. It only
blocks at load time (when a process tries to run the file), not at compile time, so a clean build
followed by a blocked test run is expected, not a sign anything is wrong with the code. Confirm
via Event Viewer → *Applications and Services Logs → Microsoft → Windows → CodeIntegrity →
Operational* - a block citing an "Enterprise signing level" requirement is Smart App Control
surfaced through its underlying WDAC policy. Confirm which policy fired with an elevated
PowerShell:

```powershell
citool.exe -lp -json
```

Look for `VerifiedAndReputableDesktop` with `"IsEnforced": true`.

**Fix:** Smart App Control has no local allow-list for unsigned dev builds - the only way to
unblock local test runs is **Windows Security → App & browser control → Smart App Control → Off**.
This is a one-way change: Microsoft doesn't support turning it back on without a clean Windows
reinstall, so treat it as a permanent tradeoff, not a toggle.
