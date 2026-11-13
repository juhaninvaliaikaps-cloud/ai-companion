<#
.SYNOPSIS
Copies her sound from the original Comfy clips into the packed .sbs.mp4 clips.

.DESCRIPTION
Keying writes the packed clips without sound. Each keyed clip has a NAME.key.json next to
it whose "source" is the original Comfy clip. For each clip, this writes a new
NAME.sbs.mp4 into -OutFolder with the packed video copied unchanged (bit for bit) and the
source's audio track added (also copied, not re-encoded).

Nothing in -KeydFolder or the source folders is changed. -OutFolder must be a different
folder. An existing file in -OutFolder is kept unless -Force is given.

Needs ffmpeg and ffprobe on PATH (https://ffmpeg.org, or "winget install ffmpeg").

.EXAMPLE
.\add-clip-audio.ps1 -OutFolder "$env:USERPROFILE\Downloads\portraits\animations\keyd-audio"

.EXAMPLE
.\add-clip-audio.ps1 -OutFolder D:\clips-audio -Names pout, wink_dance
#>
param(
    [string]$KeydFolder = (Join-Path $env:USERPROFILE "Downloads\portraits\animations\keyd"),
    [Parameter(Mandatory = $true)][string]$OutFolder,
    # Clip names. Default: every clip whose source file name contains "_voice_".
    [string[]]$Names,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

foreach ($tool in "ffmpeg", "ffprobe") {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool was not found on PATH. Install ffmpeg (for example: winget install ffmpeg), open a new window, and run this again."
    }
}

$keyd = (Resolve-Path $KeydFolder).Path
New-Item -ItemType Directory -Force $OutFolder | Out-Null
$out = (Resolve-Path $OutFolder).Path
if ($out.TrimEnd('\') -ieq $keyd.TrimEnd('\')) {
    throw "-OutFolder must not be the keyd folder: the keyed clips there are never changed."
}

function Get-FrameCount([string]$path) {
    $n = & ffprobe -v error -select_streams v:0 -count_packets -show_entries stream=nb_read_packets -of csv=p=0 $path
    return [int]($n | Select-Object -First 1)
}

function Test-HasAudio([string]$path) {
    $a = & ffprobe -v error -select_streams a -show_entries stream=index -of csv=p=0 $path
    return -not [string]::IsNullOrWhiteSpace(($a | Out-String))
}

$keys = Get-ChildItem $keyd -Filter "*.key.json"
$rows = @()
foreach ($key in $keys) {
    $info = Get-Content $key.FullName -Raw | ConvertFrom-Json
    $name = $info.name
    if ($Names) {
        if ($Names -notcontains $name) { continue }
    } elseif ($info.source -notmatch "_voice_") {
        continue
    }

    $packed = Join-Path $keyd "$name.sbs.mp4"
    $target = Join-Path $out "$name.sbs.mp4"
    $row = [ordered]@{ Clip = $name; Result = ""; Source = (Split-Path $info.source -Leaf) }

    if (-not (Test-Path $packed)) { $row.Result = "skipped: no $name.sbs.mp4"; $rows += [pscustomobject]$row; continue }
    if (-not (Test-Path $info.source)) { $row.Result = "skipped: source not found"; $rows += [pscustomobject]$row; continue }
    if (-not (Test-HasAudio $info.source)) { $row.Result = "skipped: source has no audio"; $rows += [pscustomobject]$row; continue }
    if ((Test-Path $target) -and -not $Force) { $row.Result = "skipped: exists (use -Force)"; $rows += [pscustomobject]$row; continue }

    # Video copied from the packed clip, audio copied from the source. No -shortest: it
    # would cut the last video frame when the audio is a few milliseconds shorter.
    & ffmpeg -v error -y -i $packed -i $info.source -map 0:v:0 -map 1:a:0 -c copy -movflags +faststart $target
    if ($LASTEXITCODE -ne 0) { $row.Result = "failed: ffmpeg exit $LASTEXITCODE"; $rows += [pscustomobject]$row; continue }

    $before = Get-FrameCount $packed
    $after = Get-FrameCount $target
    if ($before -ne $after) {
        Remove-Item $target
        $row.Result = "failed: frames $before -> $after, removed"
    } else {
        $row.Result = "ok: $after frames + audio"
    }
    $rows += [pscustomobject]$row
}

$rows | Format-Table -AutoSize
"Written to $out. Copy these over the same names in your video folder, and set ""sound"": true for them in video-avatar.json."
