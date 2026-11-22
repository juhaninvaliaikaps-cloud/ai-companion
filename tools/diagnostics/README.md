# GPU probe

This samples the GPU while you play and records what was using it.

## Run

From the repo:

```powershell
.\tools\diagnostics\gpu-probe.cmd -DurationSeconds 300
```

Longer run, or a chosen output folder:

```powershell
.\tools\diagnostics\gpu-probe.cmd -DurationSeconds 900 -OutDir E:\LOOT\gpu-runs
```

Leave the window open while you play. Ctrl+C stops early and still writes the summary.

## Output

A folder like `artifacts\gpu-probe-YYYYmmdd-HHmmss`:

| File | What it holds |
|---|---|
| `samples.csv` | One row every 2–3 seconds: GPU %, VRAM used/free, temperature, power, top GPU-memory processes, top GPU-engine process, Happi/Ollama/Python CPU and RAM |
| `events.csv` | New Player.log lines with the local time they appeared — screen looks, voice, chat, memory, errors |
| `summary.txt` | Peaks, top processes by peak dedicated GPU memory, screen-look events near the end |

## How to read it

1. Open `samples.csv` in Excel or a spreadsheet.
2. Sort or chart `vram_used_mb` and `gpu_util_pct`.
3. Look at the `gpu1_name`/`gpu1_mb` columns at the peaks.
4. Open `events.csv` and find the `Screen look` / `Vision` / `Voice` lines at those times.

Typical readings:

- **Chatterbox or a local model loading** — `python` or `llama-server` climbs by hundreds of MB or several GB.
- **Screen reading (capture/vision)** — `Happi` engine time or `llama-server` memory rises around a `Screen look` event.
- **Avatar video decode** — `Happi` has persistent video-decode engine work (`engtype_videodecode`).
- **VRAM near the total** — Windows may be sharing or paging; GPU-visible memory is the limit, not system RAM.

The per-process numbers come from Windows `GPU Process Memory` and `GPU Engine` counters, so they include graphics allocations, not only CUDA/compute.

## Notes

- NVIDIA driver required for overall GPU numbers; the per-process counters still work without it.
- Start the probe **before** the thing you want to measure. Use a hotkey or start it just before a screen look.
- The probe itself is lightweight: no screenshots, only performance counters. The counters add about a second per sample, which is why the default cadence is roughly every 2–3 seconds.
