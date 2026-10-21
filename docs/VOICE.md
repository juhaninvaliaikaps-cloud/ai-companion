# Voice

Replies are spoken with one swappable text-to-speech backend. The first backend is xAI's REST API. The chat panel does not talk to xAI directly. It calls `ISpeechOutput` in Core, and `VoiceSpeaker` implements that with `ITtsProvider`.

## What plays

`VoiceSpeaker` on **AppBootstrap** watches the streaming reply. It splits the text into sentences and requests each one as soon as the sentence is finished, so a short line can start before the rest of the reply arrives. The request asks for WAV (`output_format.codec` `wav`, 24000 Hz). Unity turns that PCM into an `AudioClip` and queues it on an `AudioSource`. The next clip starts when the current one ends.

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
| sampleRate | 24000 | WAV rate requested from xAI. |

Create the asset from **AI Companion > Voice Settings** if it is missing. The scene references the one in `Settings`. This asset stores no API key.

## Key

Speech uses the same key as chat. `EnvironmentApiKeySource` checks `XAI_API_KEY`, then `%APPDATA%\ProjectLeia\secrets.json` (DPAPI on Windows), then `Application.persistentDataPath/ProjectLeia/secrets.json`. Set it with **AI Companion > Set xAI API Key** or the password row on the chat panel. The key is an `Authorization: Bearer` header. It is not written into the JSON body, the Console, or the transcript. See [CHAT_PROVIDERS.md](CHAT_PROVIDERS.md).

The endpoint is `POST https://api.x.ai/v1/tts` with `text`, `voice_id`, `language`, `speed`, and `output_format`. Timestamps are not requested, because that would wrap the audio in JSON.

## Privacy

When `ChatSettings.providerId` is `local` (the Ollama path, including adult or private replies), reply text is **not** sent to cloud TTS. `SpeechPolicy` skips speaking. Nothing is queued and no note is added on every message.

Turn on **Allow Cloud Tts For Local Replies** on the voice asset only if those replies should leave the machine. The default is off. Grok and any other non-local provider may be spoken when the voice asset is enabled and mute is off.

## Errors

A voice failure becomes a **Note** in the chat panel. The wording matches the chat errors (`Invalid API key`, the billing hint, `Rate limited, try again shortly`, `Couldn't reach the voice`, and the timeout line). The Console line is prefixed `Voice error:` and is scrubbed of bearer tokens and `xai-` keys. Text chat keeps working. A missing key, an invalid key, HTTP 401, HTTP 403, or HTTP 429 stops the rest of that reply so the app does not keep calling the API. Other failures skip that sentence and try the next one. The same note is shown once per reply.

## Swap the voice later

Implement `ITtsProvider.Synthesize` and construct it from `VoiceSpeaker` instead of `XaiTtsProvider`. The queue, mute, cleanup, and talk state stay. Tests use a fake transport and do not open a socket.

## Try it in the editor

1. Open the project in Unity 6000.6.6f1.
2. Set a key with **AI Companion > Set xAI API Key**, or the password row on the panel. Restart the editor first if you use `XAI_API_KEY`.
3. Open `Assets/_Project/Scenes/Main.unity` and enter Play mode. Click the Game view so it has keyboard focus.
4. Confirm **AppBootstrap** has **Voice Speaker**, and that **Voice Settings** is the asset above (`eve`, `en`, cloud TTS for local replies off).
5. Send a short sentence, for example `Say hello in one short sentence.` You should hear `eve` start within about a second of that sentence arriving. The debug label shows `Talk on` while the audio plays and `Talk off` when it ends, including after the text has already finished.
6. Press **F6**, or the **Mute** button. Speech stops and the button reads **Muted**. Press it again to allow the next reply. Quit Play mode and enter again: the mute choice remains.
7. Send a longer reply, then press **Esc** or **Stop** while it is speaking. The audio stops and the queue is cleared. Send another message while the first is still speaking: the first voice stops and the new reply speaks.
8. Set `ChatSettings.providerId` to `local` and send a message. No request should go to `api.x.ai` for speech unless **Allow Cloud Tts For Local Replies** is on.
9. With the field focused, **F6** does not toggle mute. Click off the field and **F6** works. A bad key shows a **Note** and the typed reply still appears.

## Try it in the Windows build

1. Build and run `Builds/Windows/AICompanion.exe`.
2. If the key is not already saved, open the panel, enter it in the password row, and press **Save**.
3. Send a short sentence. Hear `eve`, and watch `Talk on` only while the audio plays.
4. **Mute** or **F6** silences the current reply and is still muted after you restart the build. **Esc**, **Stop**, and a new message cut off speech.
5. Leave the local provider on the machine. Cloud speech stays off for that provider until the voice asset allows it.
