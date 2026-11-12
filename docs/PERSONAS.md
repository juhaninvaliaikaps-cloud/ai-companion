# Personas

A persona is the companion's name, personality, speaking style, voice, model, avatar, and portrait set. Pick one in **Settings → Persona**. The switch applies immediately. The app does not restart.

The emotion, gesture, and portrait cue instructions are added after the persona text on every reply. A persona that never mentions tags still gets them, so expressions, gestures, and portraits keep working.

## Where the files live

Built-in presets ship with the app:

`Assets/_Project/Resources/Personas/`

There are three: **Leia** (`friend`), **Avery** (`coach`), and **Rowan** (`calm`). They are neutral and appropriate for any audience. They use the Grok model, the cloud voice `eve`, and the Auto avatar. Piper and the portrait folder are blank, so those stay on the standard defaults.

Edits and new personas are saved on this PC, outside the repository:

`%APPDATA%\ProjectLeia\personas\<id>.json`

On a typical Windows account that is `C:\Users\<you>\AppData\Roaming\ProjectLeia\personas\`. A saved file with the same id replaces the built-in for you. The file in the repository is not changed. The copy under `%APPDATA%` is encrypted. The built-in files in the repository stay plain JSON. See [LOCAL_STORAGE.md](LOCAL_STORAGE.md).

`settings.json` stores `personaId` and, when a portrait folder was found, `portraitFolder`. It does not store the personality text or an API key.

## File format

Version 1. Unknown fields are ignored. A missing or broken file is skipped.

```json
{
  "version": 1,
  "id": "friend",
  "displayName": "Leia",
  "personality": "You are Leia, a warm, friendly desktop companion sitting beside the user. You are their friend. Reply in one or two short, conversational sentences. Stay kind, light, and appropriate for any audience.",
  "speakingStyle": "Plain words, a light tone, and no lecture.",
  "piperVoiceId": "",
  "cloudVoiceId": "eve",
  "providerId": "grok",
  "model": "grok-4.7",
  "avatarSource": "auto",
  "portraitFolder": ""
}
```

| Field | Meaning |
| --- | --- |
| `id` | File name. Letters, digits, `-`, and `_`. |
| `displayName` | Name in the picker and the setup name while this persona is active. |
| `personality` | System prompt. `systemPrompt` is accepted as the same field. Empty uses the setup name and relationship. |
| `speakingStyle` | Added under `Speaking style:` after the personality. |
| `piperVoiceId` | Piper voice id used when chat is local or private, for example `en_US-lessac-medium`. Resolved to a `.onnx` next to the current Piper voice or `piper.exe`. This is an id only. There is no voice plug-in. |
| `cloudVoiceId` | xAI voice id used when chat is Grok, for example `eve`. `voiceId` is accepted as the same field. |
| `providerId` | `grok` or `local`. `ollama` means `local`. `xai` means `grok`. |
| `model` | Model name for that provider. A Grok id such as `grok-4.7`, or a local Ollama tag. |
| `avatarSource` | `auto`, `local`, `vrm`, or `fbx`. |
| `portraitFolder` | Full path to a portrait set. Blank uses `%APPDATA%\ProjectLeia\portraits`. |

A local persona can point at an Ollama tag instead of Grok:

```json
{
  "version": 1,
  "id": "local-reader",
  "displayName": "Nia",
  "personality": "You are Nia, a calm desktop companion sitting beside the user. Reply in one or two short sentences. Stay kind, light, and appropriate for any audience.",
  "speakingStyle": "Quiet and brief.",
  "piperVoiceId": "en_US-lessac-medium",
  "cloudVoiceId": "eve",
  "providerId": "local",
  "model": "hf.co/mradermacher/Rocinante-X-12B-v1-Heretic-Uncensored-i1-GGUF:Q4_K_M",
  "avatarSource": "",
  "portraitFolder": ""
}
```

That model tag is an example of the field. The model file is not in this repository, and this persona is not one of the built-ins. Private mode still sends chat only to the local provider, even when the persona names Grok. The badge and **Settings → AI** show the model that is actually running, including the local name when private mode forces it.

Voice follows that same choice. Grok replies use `cloudVoiceId`. Local and private replies use `piperVoiceId` and are not sent to cloud TTS. A blank voice id keeps the current voice.

## What a blank field does

| Blank or unusable field | Result |
| --- | --- |
| Cloud voice id | The current cloud voice stays. |
| Piper voice id, or the `.onnx` is not next to Piper | The current Piper voice file stays. |
| Provider | The current provider stays. |
| Model | The current model stays. A long Ollama tag is kept up to 200 characters. |
| Avatar, or a value that is not `auto`, `local`, `vrm`, or `fbx` | The current avatar stays. |
| Portrait folder, or the folder is not on disk | The standard portraits folder is used. |

Text that looks like an API key (`xai-…`, `api_key`, `bearer …`) is removed before the file is written.

## Prompt order

1. Personality, then the speaking style.
2. `DirectorPrompt.Append` adds the animation catalog, `AnimationCueParser.Instruction`, and `EmotionNames.CatalogLine`. `ChatPanel` does this on every send.
3. Remembered facts are added after that, as in [MEMORY.md](MEMORY.md).

Choosing **Use setup name** clears `personaId` and the portrait override. The setup name and relationship become the prompt again. Voice and model stay as they are until you pick another persona. Setting the name in the setup wizard does the same.

The built-in presets set Grok, `eve`, and Auto. Picking one applies those. Change them in the persona editor and the copy under `personas\` is what the next launch uses.
