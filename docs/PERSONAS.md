# Personas

A persona is the companion's name, personality, speaking style, voice, model, avatar, and portrait set. Pick one in **Settings → Persona**. The switch applies immediately. The app does not restart.

When a persona is selected or its personality text changes, the app asks the model once for a character sheet (temperament, relationship, her world) and caches it encrypted next to her mood. Persona settings show what was read and have a **Re-read** button; a temperament picked by hand overrides the reading (`temperamentMode` in the persona file: `auto`, `manual`, or missing for older files). See [PISARA.md](PISARA.md).

The emotion, gesture, and portrait cue instructions are added after the persona text on every reply. A persona that never mentions tags still gets them, so expressions, gestures, and portraits keep working.

## Where the files live

Built-in presets ship with the app:

`Assets/_Project/Resources/Personas/`

There are four. **Happi** (`happi`) is the default companion: a cheerful, playful catgirl friend with the Cheerful temperament. Her prompt is the user-written text in `happi.json` as is. Her provider, model, voices and avatar are blank, so picking her keeps whatever is set now.

The other three are **Leia** (`friend`), **Avery** (`coach`), and **Rowan** (`calm`). They are neutral and appropriate for any audience. They use the Grok model, the cloud voice `eve`, and the Auto avatar. Piper and the portrait folder are blank, so those stay on the standard defaults.

### Which persona is active at start

- **New install** (no `settings.json` yet): Happi. The setup wizard's Personality step shows "Happi"; keeping that name as a friend, or Skip, keeps Happi. Another name, or Girlfriend, makes your own setup-name companion as before.
- **Existing settings**: the persona you had selected stays selected, including the setup-name companion (no persona). Nobody is switched to Happi without choosing her.
- **Wipe all local data**: starts again as Happi, like a new install.

Leia stays available on the Persona tab. The chat header and the wheel centre in the new UI show the active persona's display name.

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
| `tagline` | JUH-150. One line under her name, at most 120 characters. |
| `description` | JUH-150. The full description, up to 32,000 characters, as written or imported in the Persona Studio. `personality` is filled from it: the whole text when it fits in 4,000 characters, otherwise cut at a paragraph end until Pisara condenses it. Empty on older files, which keep using `personality`. |
| `greeting` | JUH-150. Her first message in a new chat. Optional. |
| `exampleLines` | JUH-150. A few sample exchanges. They are added to her prompt under "Examples of how you talk". |
| `adultNotes` | JUH-150. 18+ notes. They are added to the prompt only while the 18+ gate is open (`AdultNotesNow`), when chat is always local, so they never reach a cloud model. |
| `condensed` | JUH-151. True when `personality` is Pisara's condensed version of a long `description`. |
| `hasPicture`, `pictureShape` | JUH-150. Her picture is a 512 × 512 PNG kept encrypted beside her file as `<id>.picture`. The shape (`round` or `square`) is only how it is shown. |

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

## Persona Studio (JUH-150)

**New or import a persona…** and **Open in Persona Studio** on the Persona tab open the Studio. It is a window of its own inside the overlay: you can move it by its title bar, and it has its own close button. Unity has only one real window, so it can't be a separate Windows window.

It edits a draft and saves the whole persona on **Save**. Closing with unsaved changes asks first.

- **Start:** create a new persona, start from a template (one per temperament preset), duplicate one, edit the active one, import a `.txt` or `.md` file, or paste a description.
- **1 · Identity:** her name, tagline and picture. Choose a picture or paste its path. The centre square is used, and she can be shown round or square.
- **2 · Description:** up to 32,000 characters, with a meter showing what the chat model sees. Also her speaking style, her example lines, and the 18+ notes, which are shown only when 18+ is turned on in settings.

- **3 · Pisara (JUH-151):** Pisara reads the whole description, not the cut the chat model gets. It uses the same model and privacy rule as the background reading (`PisaraReadingKeeper.ReadDraftAsync`). The step shows who reads her, a progress bar and **Skip**, and the results:
  - **Temperament:** Auto with Pisara's line, or your own nine sliders.
  - **How she knows you:** a role drop-down, what she calls it, and a shared past. Your correction is locked, like a correction on the Persona tab.
  - **Her life:** backstory (one fact per line), right now, her last week, and what's going on.
  - **What lands with her:** touchy subjects and what wins her over.

  Each part has a **Redo** that reads again and replaces only that part.
  - **Condensed description:** shown when the description is over 4,000 characters. Pisara condenses it (`PersonaCondenser`) into text you can edit, and the persona is marked `condensed`. Editing the description clears the mark, and the cut is used again until you condense once more.
  - **18+:** innocence and the 18+ secrets are still read after you save, on the local model, while the gate is open.

  On **Save**, the sheet is stored for her id with the hash of her chat prompt, so the background reader uses it and doesn't read her again. Skipping the step leaves it to the background reader, as before.

- **4 · Voice (JUH-152):** her Piper, Kokoro (drop-down), Chatterbox and xAI voice ids. **Hear her** saves first if needed, then speaks her greeting or a short hello in the voice chat uses (`ChatPanel.SpeakSample`). For a persona who isn't the active one, the Studio offers **Save and use her** first, because otherwise you'd hear the active persona's voice.
- **5 · Look:** her 3D avatar source (same as the Look tab, Auto, local prefab, VRM, FBX) and her portrait folder. The 2D video avatar is set for all personas on the Look tab.
- **6 · Review:**
  - a summary: picture, name, tagline, the description meter, Pisara's temperament and relationship lines
  - **First greeting:** shown, not spoken, when the chat is empty, at launch with no history, after a persona switch, or after clearing the history (`SettingsPanel.GreetIfEmpty` → `ChatPanel.ShowGreeting`)
  - **Test chat:** talk to the draft before saving. It uses the chat model under the usual privacy rule, with `NoMemory`, and adds nothing to the real chat or history (`ChatPanel.RequestTestReply`). The last 20 messages are shown.

Still to come: character cards and export (JUH-153), and the picture grid, describe-from-picture and version history (JUH-154).

