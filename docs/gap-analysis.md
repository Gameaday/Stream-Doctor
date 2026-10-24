# Objective gap analysis

**Method:** line-by-line comparison of the design document's claims against the
implementation, plus adversarial review of the code that decides user-facing advice.
Every gap below names the file that exhibits it and the test that now proves the fix.
"Fixed" means code changed and a test runs in CI; it does **not** mean validated on
real hardware (that is what `docs/stress-testing.md` covers).

Verification at the end of this pass: **268 tests passing**, clean-clone build of
`StreamDoctor.Linux.slnf` green, Windows interop project compiles.

## Fixed in this pass

| ID | Gap (evidence) | Impact | Fix + proof |
|---|---|---|---|
| G-01 | `UsbBandwidthAnalyzer.FindHeadroom` compared a destination against the overloaded controller's **entire** load, while the advice said "move one of them". A controller with 3 units free was never suggested even though one 2.5-unit device fits. | Wrong/no rearranging advice — the core feature asked about. | `SuggestMove` walks devices largest-first and checks each individually; rule now names the device and the resulting load. Tests: `SuggestMove_NamesASpecificDeviceAndTheLeastLoadedDestination`, `SuggestMove_ReturnsNullWhenNoControllerCanHostEvenOneDevice`, `BandwidthRule_FlagsAControllerAtRiskAndSuggestsHeadroom`. |
| G-02 | `PreFlightRunner` awaited `CollectAsync(timeout.Token)`. A blocking WMI/COM collector never observes the token, so a 90 s "timeout" could hang the sweep forever. | App appears frozen; no report at all. | Race against `Task.Delay`; abandoned task's exception observed; failure recorded. Test: `RunAsync_TimesOutASourceThatIgnoresCancellation` (asserts <5 s). |
| G-03 | Re-enumeration used eject only. When the driver vetoed removal (`CR_REMOVE_SUPPRESSED`) the device was still ready, so the "wait until it disappears" loop never saw a transition and the method returned **true** without cycling anything. | Soak tests and the re-enum playbook lied. | `CM_Disable_DevNode`/`CM_Enable_DevNode` with verified disappear→reappear transitions; returns false if no transition. Windows-only, runtime-unverified here. |
| G-04 | `UsbDeviceProblemRule` marked every code except 43 as `Critical`, so a broken printer dropped the health score to red. | False alarms; users learn to ignore red. | Severity by class: stream-critical → Critical, storage/network → Warning, other/unknown → Advisory. Tests: `DeviceProblemRule_ScalesSeverityByDeviceClass` (4 cases) + existing capture-card test. |
| G-05 | `WindowsInterferenceRule` turned any crashed app in the session window into a stream `Warning`. | Noise; every background crash looked like a stream cause. | Group app crashes per executable; only stream-path software is a warning, others advisory with an explicit "probably unrelated" explanation. Tests: `WindowsInterferenceRule_KeepsObsCrashesAsWarnings`, `..._DowngradesUnrelatedCrashesToAdvisory`. |
| G-06 | winget parsing sliced rows at header column offsets. A name wider than its header column shifted every cell (the classic `Microsoft Visual C++ Redistributable` row), and a version could be read as the package id. | Updates silently mis-attributed or dropped. | Right-anchored parser: source/available/installed are the last tokens, id is the right-most id-shaped token, name is the remainder. Tests: 4 new cases (wide name, version-like name, dotted name, missing source). |
| G-07 | Power flags were read only from `HKLM\SYSTEM\CurrentControlSet\Enum\…\Device Parameters`; many drivers keep them under the driver key `Control\Class\{guid}\####`. | Audits reported "unknown" and missed real risks. | `UsbPowerSettings.Merge` over both locations; scan source reads both. Tests: `UsbPowerSettingsTests` (4 cases). |
| G-08 | `UpdateCandidate.RequiresRestart` was collected but never shown. | A driver update could be offered right before a stream with no warning. | Restart requirement appears in the finding explanation and playbook prerequisites. Tests: `UpdateRule_MentionsARestartWhenThePackageNeedsOne`, `Create_ForAnUpdateThatNeedsARestart_SaysSoInThePrerequisites`. |
| G-09 | `SetActivePowerPlan` passed an unvalidated string to `powercfg /setactive`. | Command-line injection surface (defense in depth). | `Guid.TryParse` before the call; always formats `guid:D`. |
| G-10 | OBS lines were matched with `$` anchors without trimming, so a stat line with trailing spaces was dropped. | Silent loss of frame stats → wrong debrief. | `TrimEnd()` on the message. Test: `Parse_ToleratesTrailingWhitespaceOnStatLines`. |
| G-11 | Findings referenced a "Fix Wizard" that did not exist, and nothing converted a finding into an executable playbook. | No path from diagnosis to fix outside tests. | New `FixCoordinator` resolves catalog and update playbooks (device lookup, update lookup, probe for missing targets), executes through guards and journal, supports rollback and recent changes. Tests: `FixCoordinatorTests`, `FixCoordinatorExecutionTests` (10 cases). **UI wiring is still open (O-01).** |

## Open gaps (ranked, not fixed in this pass)

| ID | Gap | Why it matters | Next action |
|---|---|---|---|
| O-01 | **No fix UI.** `FindingViewModel.ActionText` still says "open the Fix Wizard"; the window has no fix/rollback controls. The execution layer now exists (`FixCoordinator`), the surface does not. | The product promise is "fix it", not "read about it". | Wire `FixCoordinator` into `ScanService`, add per-finding Fix buttons, a confirmation dialog carrying preconditions, and a "recent changes / undo" list. |
| O-02 | **Bandwidth numbers are uncalibrated.** "Units" (capture 3.0, webcam 2.5) and budgets (USB2 4, USB3 12) are invented; `MeasureThroughputAsync` reports `IsEstimated = true`. | Advice can be wrong in both directions until Test 1 measures real throughput. | Implement WinUSB pipe measurement (bulk/isoc) or land the benchmark and re-fit weights from field data. |
| O-03 | **No scan-history persistence.** The design says scan events/findings live in SQLite with retention and feed the next pre-flight; only the change journal persists. | No trend, no "last time this happened", no auto-generated pre-flight checks. | Add `scan_events`/`findings` tables behind a store interface, retention job, and a rule that turns last debrief findings into checklist items. |
| O-04 | **`UsbConnectionSpeed` is effectively Unknown.** It reads a `DeviceSpeed` registry value that most stacks do not publish; the authoritative source is `IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX`. | The model promises a value it cannot source; "webcam running at USB 2.0 speed" is the #1 real cause of bad video. | Implement the IOCTL against the hub interface, or remove the field from UI output until then. |
| O-05 | **WUA and winget integration is compile-verified only.** The WUA path is late-bound COM (`dynamic`); no contract test runs it against a real update session, and `InstallWindowsUpdate` shells out to a nonexistent cmdlet (`Install-WindowsUpdate` is from a third-party module). | The "guaranteed updates" promise is not yet true at runtime. | Replace the PowerShell cmdlet with `IUpdateInstaller` COM automation; add a dry-run mode and a manual test checklist on the client PC. |
| O-06 | **Event coverage is narrow.** Only USBXHCI/USBHUB3 400/411, Kernel-PnP surprise removal, TDR 4101, WER 1000, SCM 7031/7034, Kernel-Power 41 and WU 19/20 are mapped. | AMD/ASMedia controllers and WHEA events can carry the same signal under different IDs. | Collect logs from real machines (stress runs), extend `WindowsEventLogInterpreter`, keep unknown events dropped rather than guessed. |
| O-07 | **No multi-day OBS log merge.** The loader reads the three newest logs and picks one overlapping the window; a stream crossing rotation can lose its start or stop. | Wrong session window → wrong debrief. | Merge sessions from consecutive logs, de-duplicating the overlapping tail. |
| O-08 | **Full device-tree walk.** `UsbTopologyReader` recurses from the root devnode and reads ~7 registry properties per node. | Seconds of latency on large machines, and it enumerates non-USB nodes needlessly. | Enumerate host controllers by device interface GUID and walk only their subtrees. |
| O-09 | **Health score is linear with a hard floor.** Three critical findings pin the score at 0 and hide which one matters. | Users cannot prioritise; score stops being informative when it matters most. | Add diminishing returns per module (e.g. `sqrt` of summed penalty) and show per-module contribution. |
| O-10 | **Plugin inventory is name-only.** Parsed plugin names are not versioned and have no update path. | The design's plugin update manager is still absent. | Read `FileVersionInfo` for `obs-plugins\64bit\*.dll`, map to the marketplace/known-bad list, guided updates only. |
| O-11 | **English-only strings**, no resource files. | Blocks the "explain like a streamer" voice outside English. | Move user-facing strings to `.resw`/resx before beta. |
| O-12 | **Process gaps:** CI workflow lives in `docs/ci` (token limitation), no code signing, no installer, no SBOM in the build. | Not shippable to users yet. | Re-add the workflow with a token that has `workflows` permission; MSIX/WiX + signing in M5. |

## Claims that still overstate reality

These are documentation/UI statements that the code does not yet back; they should be
toned down or fixed before any external demo:

1. `FindingViewModel.ActionText` — "open the Fix Wizard" (no wizard exists).
2. README "WinUSB throughput measurement" — currently estimates; the table now says so, but the app UI does not.
3. Design doc "driver/firmware updates via Windows Update" — the code path is late-bound and untested; the UI cannot trigger it yet.
4. Design doc "findings feed the next pre-flight" — only `FollowUpChecks` in the debrief exists; nothing persists.

## What is solid (for balance)

- Evidence-first model: no finding without `ScanEvent`s; every fix names a playbook and journals before/after values.
- Guardrails: offline-only, approval/elevation, dry run, reverse-order rollback, device denylist.
- OBS parsing: sessions, three frame-failure modes, midnight rollover, stream-key redaction, golden-file tests.
- Update policy: only winget/Windows Update eligible, known-bad targets blocked, patch-level-only auto-apply, restart surfaced.
- The USB rearrangement path is now per-device and covered by adversarial tests.
