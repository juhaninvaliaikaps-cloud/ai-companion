#requires -Version 5.1
<#
.SYNOPSIS
  Samples NVIDIA GPU load, VRAM, and per-process video memory while Happi runs.

.DESCRIPTION
  Writes three files into an output folder:
    samples.csv   one line per second: GPU util, VRAM, temperature, power, top GPU processes
    events.csv    new Player.log lines with the time they appeared
    summary.txt   peaks, top consumers, and what was happening around screen looks

  The per-process numbers come from the Windows GPU Process Memory counters and include
  graphics allocations (not only compute), so Happi, the game, Chatterbox, Ollama and
  browsers are all visible.

.EXAMPLE
  .\gpu-probe.ps1 -DurationSeconds 300
  .\gpu-probe.ps1 -DurationSeconds 600 -OutDir E:\LOOT\gpu-runs
#>
[CmdletBinding()]
param(
    [int]$DurationSeconds = 300,
    [int]$IntervalMs = 2500,
    [string]$OutDir = "",
    [string]$LogPath = ""
)

$ErrorActionPreference = "SilentlyContinue"
$inv = [System.Globalization.CultureInfo]::InvariantCulture
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path (Get-Location) ("artifacts\gpu-probe-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
}
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $env:USERPROFILE "AppData\LocalLow\AI Companion\Happi\Player.log"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$samplesPath = Join-Path $OutDir "samples.csv"
$eventsPath = Join-Path $OutDir "events.csv"
$summaryPath = Join-Path $OutDir "summary.txt"

function NowIso { (Get-Date).ToString("o", $inv) }
function Fmt([double]$v) { $v.ToString("0.##", $inv) }
function SafeName([string]$name) {
    if ([string]::IsNullOrWhiteSpace($name)) { return "?" }
    return ($name -replace "[,\r\n]", " ").Trim()
}

# Collect only new lines from the Unity log, using byte offsets so the file can grow.
$logOffset = 0L
if (Test-Path $LogPath) { $logOffset = (Get-Item $LogPath).Length }

"time,gpu_util_pct,vram_used_mb,vram_total_mb,vram_free_mb,temp_c,power_w,clock_mhz,gpu_engine_top_name,gpu_engine_top_pct,gpu1_name,gpu1_mb,gpu2_name,gpu2_mb,gpu3_name,gpu3_mb,gpu4_name,gpu4_mb,gpu5_name,gpu5_mb,happi_cpu,happi_ws_mb,ollama_cpu,ollama_ws_mb,python_cpu,python_ws_mb" |
    Set-Content -Path $samplesPath -Encoding UTF8
"time,event,line" | Set-Content -Path $eventsPath -Encoding UTF8
"GPU probe" | Set-Content -Path $summaryPath -Encoding UTF8
"Started: $(NowIso)" | Add-Content -Path $summaryPath -Encoding UTF8
"Log: $LogPath" | Add-Content -Path $summaryPath -Encoding UTF8
"Output: $OutDir" | Add-Content -Path $summaryPath -Encoding UTF8

$maxVram = 0.0
$maxVramTime = ""
$maxGpu = 0.0
$maxGpuTime = ""
$maxEnginePct = 0.0
$maxEngineName = ""
$maxEngineTime = ""
$maxByProcess = @{}
$samples = 0
$eventCount = 0
$lookCount = 0
$recentEvents = New-Object System.Collections.Generic.List[object]
$start = Get-Date
$nextPrint = 0

Write-Host "GPU probe running for $DurationSeconds s; Ctrl+C to stop." -ForegroundColor Cyan
Write-Host "Output: $OutDir"

try {
    while (((Get-Date) - $start).TotalSeconds -lt $DurationSeconds) {
        $sampleStart = Get-Date
        $time = NowIso

        # Overall GPU numbers from the driver.
        $gpuUtil = 0.0; $vramUsed = 0.0; $vramTotal = 0.0; $temp = 0.0; $power = 0.0; $clock = 0.0
        $smi = & nvidia-smi --query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu,power.draw,clocks.sm --format=csv,noheader,nounits 2>$null
        if ($smi) {
            $p = ($smi | Select-Object -First 1) -split ','
            if ($p.Count -ge 6) {
                [double]::TryParse($p[0].Trim(), [ref]$gpuUtil) | Out-Null
                [double]::TryParse($p[1].Trim(), [ref]$vramUsed) | Out-Null
                [double]::TryParse($p[2].Trim(), [ref]$vramTotal) | Out-Null
                [double]::TryParse($p[3].Trim(), [ref]$temp) | Out-Null
                [double]::TryParse($p[4].Trim(), [ref]$power) | Out-Null
                [double]::TryParse($p[5].Trim(), [ref]$clock) | Out-Null
            }
        }

        # Per-process dedicated video memory, including graphics allocations.
        $byPid = @{}
        $memCounter = Get-Counter '\GPU Process Memory(*)\Dedicated Usage' -MaxSamples 1
        foreach ($cs in $memCounter.CounterSamples) {
            if ($cs.CookedValue -le 0) { continue }
            if ($cs.InstanceName -match 'pid_(\d+)_') {
                $pidValue = [int]$Matches[1]
                if (-not $byPid.ContainsKey($pidValue)) { $byPid[$pidValue] = 0.0 }
                $byPid[$pidValue] += [double]$cs.CookedValue
            }
        }

        $top = @()
        foreach ($kv in $byPid.GetEnumerator()) {
            $proc = Get-Process -Id $kv.Key -ErrorAction SilentlyContinue
            $name = if ($proc) { $proc.ProcessName } else { "pid-" + $kv.Key }
            $top += [pscustomobject]@{ Name = $name; Pid = $kv.Key; Bytes = $kv.Value }
        }
        $top = @($top | Sort-Object Bytes -Descending | Select-Object -First 5)

        # Per-process GPU engine utilization. This is where screen capture, video decode,
        # 3D rendering and local vision show up separately.
        $engineByPid = @{}
        $engineCounter = Get-Counter '\GPU Engine(*)\Utilization Percentage' -MaxSamples 1
        foreach ($cs in $engineCounter.CounterSamples) {
            if ($cs.CookedValue -le 0) { continue }
            if ($cs.InstanceName -match 'pid_(\d+)_') {
                $procId = [int]$Matches[1]
                if (-not $engineByPid.ContainsKey($procId)) { $engineByPid[$procId] = 0.0 }
                $engineByPid[$procId] += [double]$cs.CookedValue
            }
        }
        $engineTopName = ""
        $engineTopPct = 0.0
        foreach ($kv in $engineByPid.GetEnumerator()) {
            if ($kv.Value -le $engineTopPct) { continue }
            $proc = Get-Process -Id $kv.Key -ErrorAction SilentlyContinue
            $engineTopName = if ($proc) { $proc.ProcessName } else { "pid-" + $kv.Key }
            $engineTopPct = $kv.Value
        }

        $happi = Get-Process Happi -ErrorAction SilentlyContinue | Sort-Object CPU -Descending | Select-Object -First 1
        $ollama = Get-Process ollama -ErrorAction SilentlyContinue | Select-Object -First 1
        $python = Get-Process python -ErrorAction SilentlyContinue | Sort-Object CPU -Descending | Select-Object -First 1
        $happiCpu = if ($happi) { [double]$happi.CPU } else { 0 }
        $happiWs = if ($happi) { [double]$happi.WorkingSet64 / 1MB } else { 0 }
        $ollamaCpu = if ($ollama) { [double]$ollama.CPU } else { 0 }
        $ollamaWs = if ($ollama) { [double]$ollama.WorkingSet64 / 1MB } else { 0 }
        $pythonCpu = if ($python) { [double]$python.CPU } else { 0 }
        $pythonWs = if ($python) { [double]$python.WorkingSet64 / 1MB } else { 0 }

        $fields = @($time, (Fmt $gpuUtil), (Fmt $vramUsed), (Fmt $vramTotal), (Fmt ($vramTotal - $vramUsed)),
            (Fmt $temp), (Fmt $power), (Fmt $clock), (SafeName $engineTopName), (Fmt $engineTopPct))
        for ($i = 0; $i -lt 5; $i++) {
            if ($i -lt $top.Count) {
                $fields += (SafeName $top[$i].Name)
                $fields += (Fmt ($top[$i].Bytes / 1MB))
            } else {
                $fields += ""
                $fields += ""
            }
        }
        $fields += (Fmt $happiCpu); $fields += (Fmt $happiWs)
        $fields += (Fmt $ollamaCpu); $fields += (Fmt $ollamaWs)
        $fields += (Fmt $pythonCpu); $fields += (Fmt $pythonWs)
        ($fields -join ",") | Add-Content -Path $samplesPath -Encoding UTF8
        $samples++

        foreach ($entry in $top) {
            $mb = $entry.Bytes / 1MB
            if (-not $maxByProcess.ContainsKey($entry.Name) -or $mb -gt $maxByProcess[$entry.Name]) {
                $maxByProcess[$entry.Name] = $mb
            }
        }
        if ($vramUsed -gt $maxVram) { $maxVram = $vramUsed; $maxVramTime = $time }
        if ($gpuUtil -gt $maxGpu) { $maxGpu = $gpuUtil; $maxGpuTime = $time }
        if ($engineTopPct -gt $maxEnginePct) {
            $maxEnginePct = $engineTopPct
            $maxEngineName = $engineTopName
            $maxEngineTime = $time
        }

        # New Player.log lines since the last sample.
        if (Test-Path $LogPath) {
            $len = (Get-Item $LogPath).Length
            if ($len -lt $logOffset) { $logOffset = 0L }
            if ($len -gt $logOffset) {
                $fs = [System.IO.File]::Open($LogPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
                try {
                    [void]$fs.Seek($logOffset, [System.IO.SeekOrigin]::Begin)
                    $reader = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8)
                    $newText = $reader.ReadToEnd()
                    $logOffset = $fs.Position
                } finally { $fs.Dispose() }

                foreach ($line in ($newText -split "`r?`n")) {
                    if ([string]::IsNullOrWhiteSpace($line)) { continue }
                    if ($line -notmatch 'Screen look|ScreenLook|Vision|Voice:|Chat reply|Session memories|Session handoff|Relationship timeline|Memory:|Game companion|Kokoro|Describe|Exception|error|OUT OF MEMORY|out of memory|CUDA') { continue }
                    $eventCount++
                    $eventLine = $line.Substring(0, [Math]::Min(500, $line.Length))
                    $eventRow = [pscustomobject]@{ Time = $time; Line = $eventLine; Vram = $vramUsed; Gpu = $gpuUtil }
                    $recentEvents.Add($eventRow)
                    if ($line -match 'Screen look|ScreenLook|Game companion: She said') { $lookCount++ }
                    if ($recentEvents.Count -gt 100) { $recentEvents.RemoveAt(0) }
                    "`"$time`",event,`"$($eventLine -replace '"','""')`"" | Add-Content -Path $eventsPath -Encoding UTF8
                    Write-Host ("  {0}  {1}" -f $time, $eventLine) -ForegroundColor DarkGray
                }
            }
        }

        if ($samples -ge $nextPrint) {
            Write-Host ("{0}  GPU {1,3}%  VRAM {2,5}/{3,5} MB  {4,5} C  {5,6} W" -f $time, [int]$gpuUtil, [int]$vramUsed, [int]$vramTotal, [int]$temp, [int]$power)
            if ($top.Count -gt 0) {
                $line = ($top | ForEach-Object { "$($_.Name) $([int]($_.Bytes / 1MB)) MB" }) -join "  |  "
                Write-Host "  $line" -ForegroundColor DarkGray
                if ($engineTopName) { Write-Host "  most GPU engine work: $engineTopName $([int]$engineTopPct)%" -ForegroundColor DarkGray }
            }
            $nextPrint += 10
        }

        $elapsed = ((Get-Date) - $sampleStart).TotalMilliseconds
        $remain = $IntervalMs - $elapsed
        if ($remain -gt 0) { Start-Sleep -Milliseconds ([int]$remain) }
    }
} finally {
    $finished = NowIso
    "Finished: $finished" | Add-Content -Path $summaryPath -Encoding UTF8
    "Samples: $samples" | Add-Content -Path $summaryPath -Encoding UTF8
    "Peak GPU: $(Fmt $maxGpu)% at $maxGpuTime" | Add-Content -Path $summaryPath -Encoding UTF8
    "Peak VRAM: $(Fmt $maxVram) MB at $maxVramTime" | Add-Content -Path $summaryPath -Encoding UTF8
    "Peak GPU engine: $maxEngineName $(Fmt $maxEnginePct)% at $maxEngineTime" | Add-Content -Path $summaryPath -Encoding UTF8
    "" | Add-Content -Path $summaryPath -Encoding UTF8
    "Top processes by peak dedicated GPU memory:" | Add-Content -Path $summaryPath -Encoding UTF8
    foreach ($kv in ($maxByProcess.GetEnumerator() | Sort-Object Value -Descending)) {
        "  {0}: {1} MB" -f $kv.Key, (Fmt $kv.Value) | Add-Content -Path $summaryPath -Encoding UTF8
    }
    "" | Add-Content -Path $summaryPath -Encoding UTF8
    "Events: $eventCount" | Add-Content -Path $summaryPath -Encoding UTF8
    "Screen-look events: $lookCount" | Add-Content -Path $summaryPath -Encoding UTF8
    $looks = @($recentEvents | Where-Object { $_.Line -match 'Screen look|ScreenLook|Game companion: She said' })
    foreach ($look in $looks | Select-Object -Last 10) {
        "  {0}  VRAM {1} MB  GPU {2}%  {3}" -f $look.Time, (Fmt $look.Vram), (Fmt $look.Gpu), $look.Line |
            Add-Content -Path $summaryPath -Encoding UTF8
    }
    "" | Add-Content -Path $summaryPath -Encoding UTF8
    "Open samples.csv for the full timeline. Correlate a spike with events.csv." | Add-Content -Path $summaryPath -Encoding UTF8
    Write-Host "Done. Summary: $summaryPath" -ForegroundColor Cyan
}
