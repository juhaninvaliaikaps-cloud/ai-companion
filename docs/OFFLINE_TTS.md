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