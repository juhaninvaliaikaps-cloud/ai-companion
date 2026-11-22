# Offline voice (Piper)

The companion can speak without an API key through [Piper](https://github.com/rhasspy/piper), a small neural text-to-speech binary. The cloud voice (`xai`) stays the default; the local one is opt-in.

## Install

1. Download the Piper Windows release and unpack it somewhere permanent, for example:
   `E:\Tools\piper\piper.exe`
2. Download one voice model (`.onnx` plus its `.onnx.json`), for example a Finnish or English voice from the Piper voices page, into the same folder:
   `E:\Tools\piper\fi_FI-harri-medium.onnx`
3. In the app: **Settings → Voice → Use local voice (Piper)**, then set
   - **Piper exe**: `E:\Tools\piper\piper.exe`
   - **Piper voice model**: `E:\Tools\piper\fi_FI-harri-medium.onnx`

The values persist in `settings.json` as `ttsProvider`, `piperExe` and `piperVoice`.

## How it runs

Each sentence is sent to `piper.exe -m "<voice>" -f "<temp wav>"` on stdin; the WAV comes back and goes through the same playback path as the cloud voice, so lip sync and the portrait talk frames follow it. The temporary file is deleted afterwards.

- Missing exe or model: one warning, and speech is skipped for that reply. Nothing crashes.
- Timeout and cancellation: the process is killed, so cancelling a reply stops the audio.
- No network and no key are used.

## Tests

`PiperTtsProviderTests` covers the missing exe, the missing voice, a successful WAV, a failing run and cancellation, using a fake runner so no binary is needed in CI.

## Later

- Bundling a default voice with the installer.
- A per-voice speed control (Piper's `--length_scale`).
- Streaming partial audio instead of one WAV per sentence.

## Kokoro (better quality, same shape)

Kokoro-82M sounds much closer to a commercial voice than the Piper mediums. It needs a small
Python environment, because it runs as an ONNX model:

1. Create a venv and install the package once:
   ```powershell
   & "C:\Users\Jussi\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe" -m venv E:\Tools\tts\venv
   E:\Tools\tts\venv\Scripts\python.exe -m pip install kokoro-onnx soundfile
   ```
2. Download the model files into `E:\Tools\tts`:
   - `kokoro-v1.0.onnx` and `voices-v1.0.bin` from the kokoro-onnx releases.
3. Copy `tools/kokoro/kokoro_server.py` from this repo into `E:\Tools\tts`.
4. In the app: **Settings ? Voice ? Use Kokoro voice**, then set
   - **Kokoro python**: `E:\Tools\tts\venv\Scripts\python.exe`
   - **Kokoro script**: `E:\Tools\tts\kokoro_server.py`
   - **Kokoro model**: `E:\Tools\tts\kokoro-v1.0.onnx`
   - **Kokoro voices**: `E:\Tools\tts\voices-v1.0.bin`
   - **Kokoro voice**: `af_heart` (other options: `af_bella`, `af_nicole`, `af_sky`, `bf_emma`, `bf_isabella`)

The app starts the Python server once and keeps it alive, because loading the model takes about
15 seconds; after that a sentence takes roughly 1 second. If the process dies it is restarted on
the next sentence.

### Fewer cores while a game is in front (JUH-168)

Kokoro runs on the processor, not the graphics card. `kokoro-onnx` uses onnxruntime's CPU build: `onnxruntime` 1.31.1 in `E:\Tools\tts\venv` offers only the CPU and Azure engines. Left alone, onnxruntime keeps about five of the Ryzen 5600X's six cores busy for each sentence.

On 2026-11-21 the PC lost power twice (Kernel-Power 41, no crash code, no dump) while Arknights: Endfield had the graphics card at full load and Happi answered in the Kokoro voice. That jump in power draw on top of the game is what this limits. It softens the spike; it doesn't replace a power supply that can't take the load.

**Settings → Voice → Kokoro: fewer cores while a game is in front** is on by default, with **Kokoro cores in games** at 2 (1 to 16). "A game in front" is the overlay's own detection (JUH-164): a borderless or windowed-fullscreen game, or a game she is watching. It is checked once a second.

- **While a game is in front,** each sentence asks the script for that many threads (`"threads": 2`). The script runs it on a second onnxruntime session:
  - `intra_op_num_threads` set to that number, and one inter-op thread;
  - no busy-waiting;
  - below-normal process priority.
- **When the game leaves the front,** the next sentence goes back to the default session (every core, normal priority).
- **Warming.** As soon as a game comes to the front, the app sends `{"warm": true, "threads": 2}`. The script builds the limited session and speaks one word into nothing, so the first sentence in the game isn't the slow one. Without this, it once took 21.6 s.
- **Memory.** Only the latest limited session is kept, so at most two copies of the model are loaded.
- **Measured on this PC,** for one sentence of 11.9 s of audio:

  | Threads | Time | Cores busy | CPU time |
  | --- | --- | --- | --- |
  | every core (default) | 3.6 s | 5.4 | 19.3 s |
  | 4 | 3.9 s | 3.7 | 14.4 s |
  | 3 | 4.3 s | 2.8 | 12.2 s |
  | 2, no busy-waiting | 5.3 s | 1.8 | 9.4 s |
  | 1, no busy-waiting | 8.8 s | 1.0 | 8.6 s |

  With 2 threads she still speaks more than twice as fast as real time, so she doesn't stutter; each sentence starts a second or two later.
- **Why the script does it, not the app.**
  - Pinning the process to two cores (affinity) left onnxruntime's six threads fighting over them: 18 s for the same sentence, slower than real time.
  - The venv's `python.exe` is only a launcher that starts the real Python as a child process, so a limit on the process the app starts misses the worker.
- **Update your copy of the script.** Copy `tools/kokoro/kokoro_server.py` over `E:\Tools\tts\kokoro_server.py` again. An older copy ignores `"threads"` and keeps using every core; Player.log then says "this kokoro_server.py doesn't limit its threads" once.
- **Player.log** says `Voice: Kokoro uses 2 threads at below-normal priority while a game is in front` and `Voice: Kokoro uses every core again`.
