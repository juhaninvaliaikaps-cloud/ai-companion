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
