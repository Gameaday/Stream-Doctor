<#
.SYNOPSIS
    Runs the Stream Doctor stress harness on a Windows client PC.

.DESCRIPTION
    Refuses to run without elevation, filters out unsafe USB targets
    (keyboards, mice, hubs, host controllers), then runs one of the harness modes.
    Results are written to a timestamped folder under -OutputRoot.

.PARAMETER Mode
    preflight | postflight | usbreenum | usbbandwidth | soak | full

.PARAMETER DeviceInstanceId
    Device instance ID (USB\VID_....\...). If omitted, a filtered list is shown.

.PARAMETER Iterations
    Number of cycles (default 10).

.PARAMETER DelaySeconds
    Delay between cycles (default 5).

.PARAMETER OutputRoot
    Where JSON/logs are written (default .\stress-results).

.EXAMPLE
    .\run-stress.ps1 -Mode preflight -Iterations 5

.EXAMPLE
    .\run-stress.ps1 -Mode soak -DeviceInstanceId 'USB\VID_0FD9&PID_0066\5&2F3A1B4C&0&1' -Iterations 30
#>
[CmdletBinding()]
param(
    [ValidateSet('preflight', 'postflight', 'usbreenum', 'usbbandwidth', 'soak', 'full')]
    [string]$Mode = 'preflight',

    [string]$DeviceInstanceId,

    [int]$Iterations = 10,

    [int]$DelaySeconds = 5,

    [string]$OutputRoot = (Join-Path (Get-Location) 'stress-results')
)

$ErrorActionPreference = 'Stop'

function Test-IsElevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsElevated)) {
    Write-Error 'Run this script from an elevated PowerShell window.'
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'stress\StressTestRunner\StressTestRunner.csproj'
$exe = Join-Path $repoRoot 'stress\StressTestRunner\bin\Release\net10.0-windows10.0.19041.0\StreamDoctor.StressTest.exe'

if (-not (Test-Path $exe)) {
    Write-Host 'Building the stress harness...' -ForegroundColor Cyan
    dotnet build $project -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not $DeviceInstanceId -and $Mode -in @('usbreenum', 'usbbandwidth', 'soak', 'full')) {
    Write-Host ''
    Write-Host 'Selecting a safe USB target (cameras, capture cards, audio, stream decks):' -ForegroundColor Cyan

    $excludedClasses = @('Keyboard', 'Mouse', 'HIDClass', 'USB', 'Bluetooth', 'System', 'Firmware', 'Processor', 'Net', 'Display')
    $candidates = Get-PnpDevice -PresentOnly -Status OK |
        Where-Object {
            $_.InstanceId -like 'USB\*' -and
            $_.Class -notin $excludedClasses -and
            $_.FriendlyName -notmatch 'hub|keyboard|mouse|root|composite|billboard'
        } |
        Sort-Object FriendlyName |
        Select-Object -First 20 FriendlyName, Class, InstanceId

    if (-not $candidates) {
        Write-Error 'No safe USB candidate found. Pass -DeviceInstanceId explicitly.'
        exit 1
    }

    $index = 0
    $candidates | ForEach-Object {
        Write-Host ("[{0}] {1}  ({2})" -f $index, $_.FriendlyName, $_.Class)
        Write-Host ("     {0}" -f $_.InstanceId) -ForegroundColor DarkGray
        $index++
    }

    $selection = Read-Host 'Choose a device index (or press Ctrl+C to abort)'
    $chosen = $candidates[[int]$selection]
    if (-not $chosen) { Write-Error 'Invalid selection.'; exit 1 }

    if ($chosen.FriendlyName -match 'keyboard|mouse|hub') {
        Write-Error "Refusing to power-cycle '$($chosen.FriendlyName)'."
        exit 1
    }

    $DeviceInstanceId = $chosen.InstanceId
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$jsonPath = Join-Path $OutputRoot "$Mode-$stamp.json"
$logPath = Join-Path $OutputRoot "$Mode-$stamp.log"

$arguments = @('--mode', $Mode, '--iterations', $Iterations, '--delay', $DelaySeconds, '--output', $jsonPath)
if ($DeviceInstanceId) { $arguments += @('--device', $DeviceInstanceId) }

Write-Host ''
Write-Host ("Running: {0} {1}" -f $exe, ($arguments -join ' ')) -ForegroundColor Cyan
Write-Host ("Log:     {0}" -f $logPath) -ForegroundColor DarkGray
Write-Host ("Results: {0}" -f $jsonPath) -ForegroundColor DarkGray
Write-Host ''

& $exe @arguments 2>&1 | Tee-Object -FilePath $logPath
$exitCode = $LASTEXITCODE

Write-Host ''
if ($exitCode -eq 0) {
    Write-Host 'Stress run PASSED.' -ForegroundColor Green
} else {
    Write-Host "Stress run FAILED (exit $exitCode). Inspect $jsonPath." -ForegroundColor Red
}

exit $exitCode
