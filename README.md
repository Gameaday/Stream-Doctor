# Stream Doctor 🩺

A Windows diagnostic and repair app for Twitch streamers — the pre-flight and post-flight mechanic for your streaming PC. Run it before you go live for a full health sweep, and after you end for a session debrief. It finds USB dropouts, frame-pacing problems, outdated plugins and a dozen other stream-killers, then fixes them (when safe) or walks you through the fix — with rollback for every change.

**v1 never runs during gameplay or while you are live.** No resident agent, no hooking, no kernel driver, no anti-cheat exposure.

Read the [product design document](Stream-Doctor-Product-Design-Document.md), the [design review](docs/design-review.md), and the [objective gap analysis](docs/gap-analysis.md).

---

## What works today (M1 + M2 groundwork)

| Capability | Where |
|---|---|
| USB tree mapping: controller → hub → port, device classes, driver versions, problem codes | `StreamDoctor.Windows/Usb` |
| USB bandwidth/port-sharing analysis with per-controller budgets and "move it here" advice | `Core/Usb/UsbBandwidthAnalyzer.cs` |
| USB power audit: selective suspend, idle IRP in D3, hub power saving, active power plan | `Core/Usb/UsbPowerAudit.cs` |
| Windows event correlation: USB resets/controller errors, TDRs, app crashes, service failures, updates, Kernel-Power | `Core/Windows/WindowsEventLogInterpreter.cs` |
| OBS log parsing: session windows, dropped/lagged/skipped frames, scene switches, device dropouts, plugin failures, 44.1 kHz detection — **stream keys are never captured** | `Core/Obs` |
| Post-flight debrief scoped to the actual stream window, with follow-up checks for the next pre-flight | `Core/PostFlight` |
| Rules engine with evidence-linked findings, transparent confidence scoring, health score and per-module board | `Core/Diagnosis` |
| Journaled fix executor + `FixCoordinator` (finding → playbook → guarded, journaled change) | `StreamDoctor.Fixes` |
| USB re-enumeration playbook (guarded, journaled, refusal list) + client-PC stress harness | `StreamDoctor.Windows/Usb` · `stress/` |
| WinUI 3 dashboard (pre-flight sweep + post-flight debrief) | `StreamDoctor.App` |
| Transactional USB/Windows collectors | `StreamDoctor.Windows` |

**Not implemented yet:** WinUSB throughput measurement (class-based estimates only), hot-plug port labelling, OBS WebSocket writes, snapshot/restore, cloud knowledge base. See the milestone list below and [stress testing](docs/stress-testing.md).

## Repository layout

```
src/StreamDoctor.Core        Domain model, USB analyses, OBS parser, rules engine, health score (no I/O)
src/StreamDoctor.Windows     All CsWin32/WMI/registry/event-log interop and the sweep composition root
src/StreamDoctor.Fixes       Playbook catalog, guards, journaled executor, SQLite journal, effector interface
src/StreamDoctor.App         WinUI 3 dashboard (MVVM via CommunityToolkit.Mvvm, Serilog file logging)
stress/StressTestRunner      Client-PC harness: sweeps, USB re-enumeration, bandwidth, soak
tests/StreamDoctor.Tests     xUnit: 268 tests, golden-file OBS fixtures, fakes for collectors/effectors
docs/design-review.md        Review of the product design document and the decisions taken
docs/stress-testing.md       Client-PC stress matrix, safety rules and result interpretation
docs/gap-analysis.md         Objective gap list: what is proven, what is still missing
```

## Build and test

Requirements: .NET SDK 10.0.4xx.

**Windows (full app):**

```powershell
dotnet restore StreamDoctor.sln
dotnet build StreamDoctor.sln -c Release
dotnet test tests/StreamDoctor.Tests/StreamDoctor.Tests.csproj -c Release
dotnet run --project src/StreamDoctor.App
```

The WinUI app needs Windows 10 2004+ (build 19041) and builds unpackaged (`WindowsPackageType=None`, self-contained Windows App SDK). Scanning never requires elevation; run elevated only when you intend to apply a fix.

**Linux/macOS (everything except the WinUI app):**

```bash
dotnet build StreamDoctor.Linux.slnf -c Release
dotnet test StreamDoctor.Linux.slnf -c Release
```

`StreamDoctor.Windows` cross-compiles with `EnableWindowsTargeting`, so the interop layer is compile-checked on Linux CI. `StreamDoctor.App` requires Windows and is excluded from the filter.

### Stress testing on the streaming PC

The harness in `stress/StressTestRunner` runs the real sweep pipeline plus the USB
test bench on Windows with admin rights:

```powershell
# interactive: pick a safe device, run a 30-cycle soak
.\stress
un-stress.ps1 -Mode soak -Iterations 30

# or drive the exe directly
dotnet build stress/StressTestRunner -c Release
.\stress\StressTestRunnerin\Release
et10.0-windows10.0.19041.0\StreamDoctor.StressTest.exe `
  --mode full --iterations 10 --output .ull.json
```

Read [docs/stress-testing.md](docs/stress-testing.md) first: it lists the safety
rules (never target keyboards, mice or hubs), the recommended matrix, and how to
interpret health drift and soak success rates. CI compiles the harness but never
runs it — power-cycling hardware needs a real machine.


**CI:** the workflow is versioned at [`docs/ci/github-actions-ci.yml`](docs/ci/github-actions-ci.yml) (the sandbox's GitHub token cannot create workflow files). Copy it to `.github/workflows/ci.yml` in a checkout with workflow permissions; it builds/tests on Ubuntu and Windows, verifies the committed lock files, scans for vulnerable packages and generates an SBOM.
## How a sweep works

```
collectors (read-only)            evidence                 diagnosis                    action
─────────────────────            ────────                 ─────────                    ──────
USB topology (CfgMgr32)  ─┐
USB power flags (registry)├─►  ScanEvent[]  ──►  rules ──►  Finding[]  ──►  Fix Wizard / journal
event logs (EvtQuery)     │      (normalized)              (confidence,              (guarded, reversible)
OBS logs (parser)         │                                  evidence, health)
system inventory (WMI)    ┘
```

Everything is evidence-first: a finding without `ScanEvent`s cannot be produced, every fix names a playbook, and every playbook change is written to the journal **before** it is applied.

## Safety guarantees

- **Offline only.** `FixGuard` refuses to run while a stream or game is active, and refuses automatic fixes below the confidence threshold.
- **No silent changes.** Registry and playbook changes need approval, plus elevation where required. Dry runs change nothing and write nothing.
- **Exact rollback.** Before/after values are journaled; a failed multi-step fix rolls back the steps already applied, in reverse order.
- **Device denylist.** Re-enumeration is refused for keyboards, mice, gamepads, hubs, host controllers and unclassified devices.
- **Stream keys stay on disk.** The OBS parser records only the ingest scheme and host, never the stream key or URL path.
- **No kernel drivers, ever.** Interop is generated from Microsoft metadata via CsWin32 and limited to the documented functions in `NativeMethods.txt`.

## Milestones

- **M1 — Know the machine** ✅ topology map, device inventory, power audit, event-log scan, health board (read-only).
- **M2 — Know OBS** ✅ log parser, session windowing, post-flight debrief with rules; ⏳ obs-websocket config lint, real-log golden files.
- **M3 — Prove it** 🟡 re-enumeration playbook + soak harness implemented (see [stress testing](docs/stress-testing.md)); WinUSB throughput measurement, hot-plug port labelling and additional rules still open.
- **M4 — Make it reversible** ⏳ guided OBS fixes via WebSocket, restore points, test-bench effector for re-enumeration.
- **M5 — Ship value** ⏳ snapshot/restore with AES-256-GCM, update manager via winget, signed installer.

## Dependency policy

BCL first, then Microsoft-maintained packages, then large community projects with a security process. Versions are pinned in `Directory.Packages.props`, restore is locked (`packages.lock.json` committed) and CI runs `dotnet list package --vulnerable --include-transitive` and generates an SBOM.
