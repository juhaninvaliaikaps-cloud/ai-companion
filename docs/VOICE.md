# Voice

Replies are spoken with one swappable text-to-speech backend. The first backend is xAI's REST API. The chat panel does not talk to xAI directly. It calls `ISpeechOutput` in Core, and `VoiceSpeaker` implements that with `ITtsProvider`.

Push-to-talk is the other direction. `SpeechListener` records the microphone and `ISttProvider` transcribes it on this PC. The text is sent as a normal chat line. Wake word is out of scope.

## What plays

`VoiceSpeaker` on **AppBootstrap** watches the streaming reply. It splits the text into sentences and requests each one as soon as the sentence is finished, so a short line can start before the rest of the reply arrives. The request asks for raw PCM16 (`output_format.codec` `pcm`, 24000 Hz, mono). xAI answers that with `Content-Type: audio/pcm` and headerless little-endian PCM16. Unity also accepts `audio/L16`, `application/octet-stream`, a missing content type, a RIFF WAV (including a streaming header whose data size is 0 or `0xFFFFFFFF`), and a JSON envelope with base64 `audio` and `content_type`. It turns that into an `AudioClip` and queues it on an `AudioSource`. The next clip starts when the current one ends.

Project Settings keeps Unity audio enabled. With audio disabled, `AudioClip.Create` returns an empty clip and nothing can be heard.

A short, garbage, or unreadable body becomes a **Note**. The decoder does not throw. An earlier WAV parser threw `IndexOutOfRangeException` from the async synthesis path, and the Editor's Error Pause froze the Game view.

`IAvatarAnimator.SetTalking(true)` is on only while that audio is actually playing. It turns off when the queue finishes, when speech is cancelled, or when playback stops after an error. The text stream no longer holds the talk state open.

**F6** mutes and unmutes. The **Mute** button on the chat header does the same. The choice is stored in `PlayerPrefs` under `ai-companion.tts-muted` (`1` muted, `0` spoken). Mute stops the current audio and drops the queue. Unmute does not replay it. **F6** does nothing while the message field or the API-key field is focused.

**Esc** stops speech as well as a streaming reply, and leaves the text field. **Stop** does the same while a reply is streaming or while audio is still playing. Sending a new message cancels whatever was still being spoken.

Before a sentence is sent, markdown, emoji, and `*asterisk actions*` are removed. A sentence that is only punctuation is skipped. Periods inside numbers such as `3.14` are not treated as the end of a sentence.

## Settings

`Assets/_Project/Settings/VoiceSettings.asset` holds:

| Field | Default | Meaning |
| --- | --- | --- |
| enabled | on | Master switch for speech. |
| voiceId | `eve` | xAI voice id. |
| language | `en` | BCP-47 language. The API requires one. |
| speed | 1 | Clamped to 0.7–1.5 before the request. |
| volume | 1 | `AudioSource` volume. |
| allowCloudTtsForLocalReplies | off | See privacy below. |
| timeoutSeconds | 20 | Per-sentence request timeout. |
| sampleRate | 24000 | PCM rate requested from xAI. |

Create the asset from **AI Companion > Voice Settings** if it is missing. The scene references the one in `Settings`. This asset stores no API key.

## Key

Speech uses the same key as chat. `EnvironmentApiKeySource` checks `XAI_API_KEY`, then `%APPDATA%\ProjectLeia\secrets.json` (DPAPI on Windows), then `Application.persistentDataPath/ProjectLeia/secrets.json`. Set it with **AI Companion > Set xAI API Key** or the password row on the chat panel. The key is an `Authorization: Bearer` header. It is not written into the JSON body, the Console, or the transcript. See [CHAT_PROVIDERS.md](CHAT_PROVIDERS.md).

The endpoint is `POST https://api.x.ai/v1/tts` with `text`, `voice_id`, `language`, `speed`, and `output_format` (`codec` `pcm`, `sample_rate` 24000). Timestamps are not requested. xAI's REST schema describes a JSON body (`audio` base64, `content_type`, `duration`), and a request without timestamps can also return raw bytes. The crash on the WAV request was a raw `RIFF`/`WAVE` body whose chunk size ran past the buffer. PCM is what we ask for now. WAV, raw PCM16, and that JSON envelope are all played. MP3 is not.

## Privacy

When `ChatSettings.providerId` is `local` (the Ollama path, including adult or private replies), reply text is **not** sent to cloud TTS. `SpeechPolicy` skips speaking. Nothing is queued and no note is added on every message.

Turn on **Allow Cloud Tts For Local Replies** on the voice asset only if those replies should leave the machine. The default is off. Grok and any other non-local provider may be spoken when the voice asset is enabled and mute is off.

## Errors

A voice failure becomes a **Note** in the chat panel. The wording matches the chat errors (`Invalid API key`, the billing hint, `Rate limited, try again shortly`, `Couldn't reach the voice`, and the timeout line). The Console line is prefixed `Voice error:` and is scrubbed of bearer tokens and `xai-` keys. Text chat keeps working. A missing key, an invalid key, HTTP 401, HTTP 403, or HTTP 429 stops the rest of that reply so the app does not keep calling the API. Other failures skip that sentence and try the next one. The same note is shown once per reply. A hung request is aborted after the voice timeout. Nothing in this path blocks the main thread, and an exception in synthesis or playback is caught so Play mode keeps running.

While a sentence is being requested or a clip is queued or playing, the overlay stays at the active frame rate. It returns to the idle rate only after speech is finished.

The Console also logs lines that contain no key and no reply text: `Voice: TTS request starting (N chars, pcm, 24000 Hz)` when a sentence is sent, `Voice: TTS response HTTP <status> <content-type> <byte count> bytes head <four bytes as hex and ASCII>` when the body arrives, and `Voice: playing clip <frames> frames <rate> Hz <channels> ch` when a clip starts. A body that is not played logs `Voice: rejected audio (<content-type>, <byte count> bytes): <reason>`.

## Swap the voice later

Implement `ITtsProvider.Synthesize` and construct it from `VoiceSpeaker` instead of `XaiTtsProvider`. The queue, mute, cleanup, and talk state stay. Tests use a fake transport and do not open a socket.

## Try it in the editor

1. Open the project in Unity 6000.6.6f1.
2. Set a key with **AI Companion > Set xAI API Key**, or the password row on the panel. Restart the editor first if you use `XAI_API_KEY`.
3. Open `Assets/_Project/Scenes/Main.unity` and enter Play mode. Click the Game view so it has keyboard focus.
4. Confirm **AppBootstrap** has **Voice Speaker**, and that **Voice Settings** is the asset above (`eve`, `en`, cloud TTS for local replies off).
5. Send a short sentence, for example `Say hello in one short sentence.` You should hear `eve` start within about a second of that sentence arriving. The Console shows the request line, then the response line (status, content type, byte count, and the first four bytes), then the playing-clip line. The debug label shows `Talk on` while the audio plays and `Talk off` when it ends, including after the text has already finished. The Game view keeps running. If the body cannot be played, a **Note** appears and the next sentence can still be tried.
6. Press **F6**, or the **Mute** button. Speech stops and the button reads **Muted**. Press it again to allow the next reply. Quit Play mode and enter again: the mute choice remains.
7. Send a longer reply, then press **Esc** or **Stop** while it is speaking. The audio stops and the queue is cleared. Send another message while the first is still speaking: the first voice stops and the new reply speaks.
8. Set `ChatSettings.providerId` to `local` and send a message. No request should go to `api.x.ai` for speech unless **Allow Cloud Tts For Local Replies** is on.
9. With the field focused, **F6** does not toggle mute. Click off the field and **F6** works. A bad key shows a **Note** and the typed reply still appears.

## Try it in the Windows build

1. Build and run `Builds/Windows/AICompanion.exe`.
2. If the key is not already saved, open the panel, enter it in the password row, and press **Save**.
3. Send a short sentence. Hear `eve`, and watch `Talk on` only while the audio plays. The Game view stays live if a reply cannot be played; a **Note** explains it.
4. **Mute** or **F6** silences the current reply and is still muted after you restart the build. **Esc**, **Stop**, and a new message cut off speech.
5. Leave the local provider on the machine. Cloud speech stays off for that provider until the voice asset allows it.

## Speech to text

Hold **V**, or hold the **Mic** button at the left of the message field. Recording lasts only while that hold lasts. The button turns red and reads **Rec**. Releasing it transcribes on this machine and sends the text as a **You** line. A short sentence on the dev PC should come back in under about two seconds with the base model.

**V** does not record while the message field or the API-key field is focused, so typing the letter still works. **F6** (mute), **F7** (avatar), **F8** (topmost), **F9**, **F10**, **Esc**, **1–5**, **T**, and **R** are left alone. The asset can pick another key. Those taken keys fall back to **V**.

Starting a hold stops any TTS that is playing. The overlay stays at the active frame rate while the mic is open or a transcription is running.

The mic button is a hold, not a toggle. Letting go, or sliding off the button, ends the recording. A new hold during transcription is ignored until the current one finishes and the key or button has been released.

### Local Whisper

The engine is [whisper.unity](https://github.com/Macoron/whisper.unity) 1.4.0 (whisper.cpp v1.7.5, commit `529a628`), embedded at `Packages/com.whisper.unity`. The binding, whisper.cpp, and the OpenAI Whisper weights are MIT, which is fine for this public repository. The copy in this repo adds a `UnityEngine.UI` reference and drops the JetBrains annotations so it compiles in Unity 6000.6. It includes the Windows and Linux CPU plugins and leaves out the Vulkan backend. The Windows plugins are enabled for the Windows editor and the Windows player, and they load on the first transcription, not at startup. The Linux plugins are for a Linux player only, so the Linux test editor does not load them. There is no Python service and no cloud speech-to-text call. Audio is not uploaded and is not written to a file.

`ggml.dll` still imports `ggml_backend_vk_reg` from `ggml-vulkan.dll`. This repo ships a stub of that library that returns null, so the import resolves and Vulkan stays off. On Windows, Unity loads `libwhisper.dll` by a full path and does not search the plugin folder for the `ggml*.dll` files beside it. `WhisperNativeLoad` loads those siblings from the plugin folder before the first transcription. After you pull this change, quit the Unity Editor completely and open the project again so the new DLL is imported into a fresh process. Entering Play mode again in a process that already failed to load `libwhisper` keeps that failure. No Vulkan driver is required. If a later `STT:` line says `ggml-cpu.dll win32=126`, install the Microsoft Visual C++ 2015-2022 Redistributable (x64). The Editor install normally already includes it.

`ISttProvider` is the seam. `WhisperSttProvider` is the first implementation. The chat panel only sees `IChatSubmitter` and `IPushToTalk`, so the UI assembly does not reference Whisper.

The weight is **not** in git. The default is multilingual `ggml-base.bin` (about 148 MB), which covers Finnish and English. Do not download `ggml-base.en.bin`. That file is English only. `ggml-small.bin` (about 488 MB) is the higher-quality option and can miss the two-second target on CPU.

Language defaults to `auto`. Set `language` on the asset to `fi` or `en` to force one. `translateToEnglish` stays off, so Finnish stays Finnish.

### Get the model

In the Editor, with the project open:

1. **AI Companion > Download Whisper Model (base)**
2. Wait until the dialog says it saved `Assets/StreamingAssets/Whisper/ggml-base.bin`.

The same menu has **Download Whisper Model (small)**. If you use that, set `modelFile` on `SttSettings` to `ggml-small.bin`.

From a shell, the same base file is:

```text
curl -L -o Assets/StreamingAssets/Whisper/ggml-base.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
```

`Assets/StreamingAssets/Whisper/*.bin` is gitignored. CI does not download a model. Edit Mode tests cover resampling, the hold state machine, and the hotkey rules without native code and without the weight.

A missing model, no microphone, or a microphone the OS refused becomes a **Note**. Play mode keeps running. The Console lines are short and start with `STT:`. They include the device, rate, duration, model file name, language code, and seconds. A failed transcription also includes the exception type and `path=` the plugin folder. They do not include the transcript or the audio.

### Settings

`Assets/_Project/Settings/SttSettings.asset` holds:

| Field | Default | Meaning |
| --- | --- | --- |
| enabled | on | Master switch for push-to-talk. |
| pushToTalkKey | V | Hold key. Taken keys fall back to V. |
| language | `auto` | Whisper language. `fi` or `en` force one. |
| modelFile | `ggml-base.bin` | File name inside `StreamingAssets/Whisper`. |
| microphoneDevice | empty | Empty uses the system default. Otherwise the device name from Unity's microphone list. |
| maxRecordSeconds | 30 | The hold stops at this length. |

**AppBootstrap** has **Speech Listener** pointing at that asset. Create a replacement from **AI Companion > Speech To Text Settings** if it is missing.

### Try it in the editor

1. Check out `cursor/juh-12-whisper-stt-1850`. Pull with Git LFS so `Packages/com.whisper.unity/Plugins/Windows/ggml-vulkan.dll` is a real DLL of a few KB. Quit the Unity Editor completely, then open the project in Unity 6000.6.6f1 and let the Whisper package import.
2. Run **AI Companion > Download Whisper Model (base)** and confirm `Assets/StreamingAssets/Whisper/ggml-base.bin` exists.
3. Open `Assets/_Project/Scenes/Main.unity` and enter Play mode. Click the Game view so it has keyboard focus.
4. Confirm **Speech Listener** is on **AppBootstrap** and **Stt Settings** is the asset above.
5. If a reply is speaking, hold **V** or **Mic**. The voice stops, the button reads **Rec** and turns red, and the overlay stays near 60 fps.
6. Say a short English or Finnish sentence and release. Within about two seconds the line shows under **You**, then the companion replies. The Console shows `STT:` lines for the recording and the transcription, with no transcript text.
7. Hold **V** while the message field is focused. Nothing records. Click off the field and **V** works again. **F6** still mutes. **F7** still cycles the avatar.
8. Unplug the microphone, or deny microphone access, and hold **V**. A **Note** explains it. Delete the `.bin` and hold **V** again. A **Note** names the download menu. Play mode does not freeze.

### Try it in the Windows build

1. Download the model in the Editor first, so `StreamingAssets/Whisper/ggml-base.bin` is present, then rebuild and run `Builds/Windows/AICompanion.exe`. The Whisper plugins, including `ggml-vulkan.dll`, are copied into `AICompanion_Data/Plugins/x86_64`. The model is copied from StreamingAssets.
2. If Windows asks, allow the microphone for the build.
3. Click the window. Hold **V** or **Mic**, speak a short sentence, and release. The **You** line appears, and any current TTS stops when the hold starts.
4. A missing model or a blocked microphone shows a **Note**. No audio file is written next to the exe. The player log's `STT:` lines stay free of the transcript.
