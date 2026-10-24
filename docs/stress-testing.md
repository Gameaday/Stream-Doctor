# Client-PC stress testing

This runbook is for the **Windows streaming PC**, not CI. CI only compiles the
harness; every runtime check below needs Windows, administrator rights and real
USB hardware.

The harness lives in [`stress/StressTestRunner`](../stress/StressTestRunner) and
drives the same code the app uses: the pre-flight runner, the post-flight
analyzer and the USB test bench.

## Safety first

- **Never target a keyboard, mouse, gamepad, hub or host controller.** Re-enumeration
  power-cycles the device; losing your only input device means a hard reboot.
  `DeviceReEnumerationGuard` refuses these classes, and the harness pre-filters them,
  but the final choice is yours.
- **Close OBS, games and vendor tools** before USB modes.
- **Run offline.** Fixes and updates are not part of these tests, but a Windows
  Update landing mid-soak invalidates the numbers.
- **Elevated shell required.** The harness exits with code 1 otherwise.
- A failed cycle is data, not a crash: the report keeps the error text per iteration.

## Build and publish

```powershell
# compile (fast, used by CI)
dotnet build stress/StressTestRunner/StressTestRunner.csproj -c Release

# single-file exe for a client PC
dotnet publish stress/StressTestRunner/StressTestRunner.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -o artifacts/stress
```

The published exe is self-contained: the client PC does not need the .NET SDK,
only Windows 10 2004+ and admin rights.

## Find a safe device instance ID

Pick a camera, capture card, audio interface or stream deck — not an input device.

```powershell
Get-PnpDevice -PresentOnly -Status OK |
  Where-Object { $_.InstanceId -like 'USB\*' } |
  Select-Object FriendlyName, Class, InstanceId |
  Sort-Object Class, FriendlyName |
  Format-Table -AutoSize
```

Example IDs:

```
USB\VID_0FD9&PID_0066\5&2F3A1B4C&0&1     # Elgato Stream Deck
USB\VID_0BDA&PID_58B0\...                # capture card
USB\VID_046D&PID_0825\...                # Logitech Brio (camera class)
```

`stress/run-stress.ps1` lists and filters candidates for you; it refuses anything
whose class looks like an input device or a hub.

## Modes

| Mode | What it exercises | Exit code |
|---|---|---|
| `preflight` | Full read-only sweep repeated N times: topology, power, event logs, OBS logs, updates, rules, health score | 0 when every sweep completed |
| `postflight` | Session windowing + debrief repeated N times | 0 |
| `usbreenum` | `CM_Request_Device_Eject` + parent rescan, N cycles | 0 when every cycle re-enumerated |
| `usbbandwidth` | Throughput measurement loop (currently reports "estimated" until WinUSB lands) | 0 |
| `soak` | Re-enumeration + bandwidth per cycle; pass mark is a 95% success rate | 0 when `UsbSoakReport.MeetsThreshold()` |
| `full` | `preflight`, then the three USB suites | 0 only if all pass |

Examples:

```powershell
# stability of the scan pipeline itself
StreamDoctor.StressTest.exe --mode preflight --iterations 20 --delay 2 --output .\preflight.json

# 10 power cycles of a capture card, 15 s apart
StreamDoctor.StressTest.exe --mode usbreenum `
  --device "USB\VID_0BDA&PID_58B0\5&1A2B3C4D&0&2" --iterations 10 --delay 15 --output .\reenum.json

# 30-cycle soak (re-enum + bandwidth), ~10 minutes
StreamDoctor.StressTest.exe --mode soak `
  --device "USB\VID_0FD9&PID_0066\5&2F3A1B4C&0&1" --iterations 30 --delay 10 --output .\soak.json

# everything, including sweeps
StreamDoctor.StressTest.exe --mode full `
  --device "USB\VID_0BDA&PID_58B0\5&1A2B3C4D&0&2" --iterations 10 --output .\full.json
```

Or let the helper script pick a safe device for you:

```powershell
.\stress\run-stress.ps1 -Mode soak -Iterations 30
```

## Recommended matrix

| Tier | Command | Duration | Pass criteria |
|---|---|---|---|
| Smoke | `preflight --iterations 3` | ~1 min | 3/3 complete, health score stable |
| Standard | `full --iterations 10 --delay 5` | ~8 min | preflight 10/10, re-enum 10/10, soak ≥95% |
| Soak | `soak --iterations 50 --delay 10` | ~20 min | ≥95% cycles, no growing re-enum times |
| Long soak | `soak --iterations 200 --delay 5` | ~1 h | ≥98%, average re-enum < 3 s, no memory growth |

Run the long soak with OBS open but **not streaming** if you want to see whether
OBS's device handle survives re-enumeration (it normally does not; expect the
source to be recreated — that is a real finding, not a harness bug).

## Reading the results

`preflight.json` is an array of iterations. Watch for:

- **health score drift** between iterations on an idle machine (should be 0);
- **duration growth** (the first sweep is slow: cold caches; later sweeps should be faster);
- **`failures`** entries: a collector that times out is a real finding (WMI broken,
  event log unreadable, OBS logs missing).

`soak.json` contains `SuccessRate`, `TotalTime` and per-iteration timings.

- `SuccessRate < 0.95` → the device or its port is unreliable under reset; try
  another cable/port, then another controller.
- Re-enumeration times **growing** across iterations → hub/controller degradation
  or a driver that leaks handles.
- Cycles failing with an `Error` text → usually a veto from the driver
  (`CM_REMOVE_SUPPRESSED`); the device may be in use by OBS.

## What these tests do not measure

- **Actual USB throughput.** `MeasureThroughputAsync` reports `IsEstimated = true`
  until the WinUSB pipe implementation lands; the analyzer keeps using class-based
  weights for now.
- **Frame pacing / GPU / encoder behaviour.** Those need OBS running under load and
  are covered by the (future) test-bench broadcast.
- **Hardware failures.** A soak test proves the OS/driver stack survives resets;
  it cannot certify the silicon.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `ERROR: Stress tests require administrator privileges` | not elevated | re-run from an elevated prompt |
| `CM_Request_Device_Eject` returns `CR_REMOVE_SUPPRESSED` | a driver vetoed removal (OBS holds it) | close OBS/vendor tool |
| Device never reappears | parent rescan failed | unplug/replug manually; check the port |
| `no measurable endpoints` | device has no WinUSB/raw I/O interface | expected until WinUSB support lands |
| Pre-flight always incomplete | collector timeout / WMI disabled | read `failures` in the JSON |

## CI vs client PC

`StreamDoctor.Linux.slnf` includes the harness so every push **compiles** it, but
CI never runs it: re-enumeration needs Windows, admin rights and a device you are
willing to power-cycle. Treat the matrix above as the release gate for the USB
test bench.
