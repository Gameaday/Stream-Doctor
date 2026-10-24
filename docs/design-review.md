# Design review — Stream Doctor v1

**Reviewed:** `Stream-Doctor-Product-Design-Document.md` (whole document)
**Review date:** 2026-10-22
**Verdict:** **No blocking faults.** The v1 direction (offline, on-demand, pre-flight + post-flight, no resident agent) is coherent, technically achievable, and the five-project layout maps cleanly onto the six feature modules. Development has started against the document as written.

Five correctable issues and one underspecified formula were found. Each is listed below with the mitigation now in the code, so the document and the implementation cannot drift apart silently.

---

## 1. What checks out

| Claim in the document | Verification |
|---|---|
| .NET 10 is the current LTS; WinUI 3 is the supported Windows UI path | .NET SDK 10.0.402 is current; `Microsoft.WindowsAppSDK` 1.8 servicing releases are published monthly. |
| C# + `CsWin32` is the right interop story | `Microsoft.Windows.CsWin32` 0.3.367 generates the `CM_*` declarations from `NativeMethods.txt`; the USB walk compiles with no hand-written P/Invoke. |
| The evidence is already on disk | OBS logs contain `==== Streaming Start/Stop ====`, `Switched to scene '…'`, `Device '…' invalidated.  Retrying (n)`, `Total frames output: N`, `Number of dropped frames…`, `Number of lagged frames…`, and `Video stopped, number of skipped frames…`. All are parsed and covered by fixtures. |
| No kernel drivers, no resident agent | Nothing in this repo loads a driver or starts a background service. Every collector is a one-shot read. |
| Five projects | `Core` (pure domain), `Windows` (all interop), `Fixes` (playbooks + journal executor), `App` (WinUI 3), `Tests` (xUnit). Implemented exactly as specified. |

## 2. Findings and mitigations

### F1 — Medium: "OBS writes stats every ~2 s" is not true of the log file

Per-2-second output statistics come from **obs-websocket** polling, not from the log. The log records session **totals** when an output stops, plus discrete events (scene switches, device retries, encoder warnings). A post-flight run — where the app was not running — therefore cannot reconstruct 2-second granularity.

**Mitigation:** `ObsLogParser` treats log statistics as session totals and never fabricates intervals; `ObsSessionStats` keeps reported counts *and* OBS-reported percentages. The post-flight timeline is built from discrete events. Per-interval analysis is deferred to the optional obs-websocket capture in M4.

A related parser detail found while testing: the skipped-frames line (`… skipped frames … 400/226000`) reports **attempted** frames, while `Total frames output` reports output frames. Conflating them inflates totals by thousands of frames, so the model now separates `TotalFrames` from `AttemptedFrames`.

### F2 — Medium: Test 2 (re-enumeration) contradicts three other rules

The USB re-enumeration test soft-disables devices via `SetupDiChangeState`. That (a) mutates the machine during a "scan", while §3 says every mutation is journaled, (b) requires administrator rights, while §5 says UAC is requested only for the specific fix action, and (c) can disable the keyboard, mouse, hub or host controller the user needs to recover.

**Mitigation:** re-enumeration is modelled as a **high-risk fix**, not a scan step:

- `DeviceReEnumerationGuard` refuses input devices, hubs, host controllers, `ROOT_HUB` nodes and unclassified devices.
- `FixGuard` requires explicit approval, elevation and an offline window; `PlaybookCatalog` marks the playbook `AutoFixSupported = false`.
- The Windows effector does not implement re-enumeration yet (`CanApply` returns false), so the executor fails safely until the M3 test bench ships the real implementation.

### F3 — Low: the confidence formula rewards noisy logs and can exceed 1

`confidence = base_weight × supporting_evidence_lines − contradicting_evidence` grows with repeated log lines (one flapping microphone can produce 100 identical lines) and is unbounded.

**Mitigation:** `EvidenceTally` counts **distinct signals** (device or category + timestamp), keeps every occurrence for the UI, caps support at 5 signals, weights contradictions at 0.25 and clamps to 0..1. Tests cover dedupe, saturation and contradiction. The UI shows the numeric confidence, so scoring stays auditable.
### F4 — Low: the approved dependency list is incomplete for .NET 10

- Reading event logs from .NET 10 requires the Microsoft-maintained `System.Diagnostics.EventLog` package (`EventLogReader` is not in the shared framework).
- WinUI 3 requires `Microsoft.WindowsAppSDK` (plus its `Microsoft.Windows.SDK.BuildTools` transitive dependency) — implied by "WinUI 3" but absent from the table.

**Mitigation:** both are pinned in `Directory.Packages.props`, which satisfies the policy (Microsoft-backed, security-serviced). No other dependency was needed: `Microsoft.Data.Sqlite`, `CommunityToolkit.Mvvm` and Serilog are used exactly as approved. `TraceEvent` is not referenced yet because present-event tracing is an M3 feature, not M1.

### F5 — Low: some Windows event IDs are stated more precisely than Windows guarantees

Event 400/411 exist for both `USBXHCI` and `USBHUB3` providers with slightly different meanings, and the UX example's "0x500" is a status code, not an event ID. Hard-coding one provider/ID pair would silently miss dropouts on other chipsets.

**Mitigation:** `WindowsEventLogInterpreter` matches the **provider family** (`USBXHCI`, `USBHUB3`, `USBHUB`, `Microsoft-Windows-USB`) for 400/411, uses message text only for the `Kernel-PnP` "surprise removal" case, and **drops everything it does not recognize** rather than guessing. Provider/ID pairs still need validation on real Intel, AMD and ASMedia machines; that is tracked as an open question, not a blocker.

### F6 — Low: the 0–100 health score had no formula

**Specified and implemented:** each finding deducts `severity_weight × (0.4 + 0.6 × confidence)` points, where critical = 45, warning = 18, advisory = 6 and info = 0; the total is floored at 0. `≥ 85` is green, `≥ 60` yellow, otherwise red. A module that was never scanned reports **Unknown**, never green — silence is not health.

## 3. Deliberate implementation deviations

| Deviation | Why |
|---|---|
| `StreamDoctor.Fixes` targets `net10.0`, not `net10.0-windows` | The journal, guards and executor are platform-neutral; Windows effectors are injected through `IChangeApplier`. This keeps the fix engine unit-testable on any OS and preserves "Core has no I/O". |
| `StreamDoctor.Windows` also compiles on Linux CI (`EnableWindowsTargeting`) | Interop and collector code is compile-checked on every push; only the WinUI app requires Windows to build. |
| `ObsLogParser` is a static class | It has no state; the analyzer rule CA1822 forced the honest design. Parsing is pure and golden-file tested. |
| Synthetic OBS fixtures instead of real anonymized logs | Real logs are not available in this environment. The document already requires golden files built from real anonymized OBS logs; replacing the fixtures is the top item on the M2 test backlog. |
| USB bandwidth is estimated from device classes | Measured throughput needs the M3 test bench. The analyzer's weights and per-controller budgets are documented as estimates and are deliberately conservative. |

## 4. Open questions for the product owner

1. **Auto-fix threshold:** default is 0.85 (configurable through `DiagnosisOptions`). Given "a wrong auto-fix damages trust permanently", consider 0.9 for USB power changes and guided-only for anything that interrupts a device.
2. **Re-enumeration in v1:** the design lists it as a test-bench test. Recommendation: ship it only as a guided, explicitly approved action, never as an automatic pre-flight step.
3. **Per-source timeout:** the runner uses 90 s per collector. A 7-day event-log lookback on a busy machine may need 120 s; measure on real hardware before freezing the value.
4. **Multi-day post-flight:** the loader reads the three newest OBS logs and picks the one overlapping the stream window. A stream that starts before log rotation needs merged sessions (M2).
5. **Localization:** every user-facing string is English in the codebase. The design's "explain like a streamer" voice will need a resource file before shipping in other languages.

## 5. M3 progress update (2026-10-24)

- **Re-enumeration is now real and guarded.** `UsbTestBench.ReEnumerateDeviceAsync`
  uses `CM_Request_Device_Eject` plus a parent-node rescan, and the journaled
  `WindowsChangeApplier` executes it for `FixActionKind.DeviceReEnumeration`.
  The previous refusal list still applies before execution (`FixGuard` +
  `DeviceReEnumerationGuard`), the playbook stays `AutoFixSupported = false`, and
  the operation is journaled so the change is auditable.
- **Soak math moved into Core** (`UsbSoakReport`) so the 95% pass threshold, totals
  and averages are unit-tested rather than living in Windows-only code.
- **Client-PC stress harness added** (`stress/StressTestRunner` + `docs/stress-testing.md`).
  It exercises pre-flight sweeps, post-flight debriefs, re-enumeration cycles and
  soak runs on real hardware. CI only compiles it: re-enumeration needs Windows,
  admin rights and a device you are willing to power-cycle.
- **Still open from the original plan:** WinUSB throughput measurement
  (`MeasureThroughputAsync` currently reports `IsEstimated`), hot-plug port
  labelling, and the remaining correlation rules for M3.
