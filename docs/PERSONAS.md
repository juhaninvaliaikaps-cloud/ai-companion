# Personas

A persona is the companion's name, personality, speaking style, voice, model, avatar, and portrait set. Pick one in **Settings → Her → Persona**. The switch applies immediately. The app does not restart.

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
| `speakingStyle` | Added under `Speaking style:` after the personality. Older files only: the Studio and the Persona tab have no Speaking style box any more (JUH-155). Opening the persona in the Studio moves it to the end of her description, under "How she talks:". |
| `scenario` | JUH-155: the user's own scenario, up to 4,000 characters. Added under "The world and what's going on (where your story with the user starts…)", after the personality and before the example lines. |
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

1. Personality, then the speaking style (older files), then her scenario, then her example lines.
2. `DirectorPrompt.Append` adds the animation catalog, `AnimationCueParser.Instruction`, and `EmotionNames.CatalogLine`. `ChatPanel` does this on every send.
3. Remembered facts are added after that, as in [MEMORY.md](MEMORY.md).

Choosing **Use setup name** clears `personaId` and the portrait override. The setup name and relationship become the prompt again. Voice and model stay as they are until you pick another persona. Setting the name in the setup wizard does the same.

The built-in presets set Grok, `eve`, and Auto. Picking one applies those. Change them in the persona editor and the copy under `personas\` is what the next launch uses.

## Persona Studio (JUH-150)

**New or import a persona…** and **Open in Persona Studio** on the Persona tab open the Studio. It is a window of its own inside the overlay: you can move it by its title bar, and it has its own close button. Unity has only one real window, so it can't be a separate Windows window.

It edits a draft and saves the whole persona on **Save**. Closing with unsaved changes asks first.

- **Start:** create a new persona, start from a template (one per temperament preset), duplicate one, edit the active one, import a character card, import a `.txt` or `.md` file, or paste a description.
- **1 · Identity:** her name, tagline and picture. Choose a picture or paste its path. The centre square is used, and she can be shown round or square.
- **2 · Description:** up to 32,000 characters, with a meter showing what the chat model sees. How she talks is part of the description; there is no separate Speaking style box (JUH-155). Also her example lines, and the 18+ notes, which are shown only when 18+ is turned on in settings.
  - **Scenario (JUH-155):** **Add custom scenario** opens a box (up to 4,000 characters) for her world, what's going on, where you are and how you met.
    - She reads it in every chat as where the story starts. The scene tracker (JUH-129) follows where the chat goes from there, so she doesn't snap back to the opening.
    - Pisara takes her relationship, shared history and "right now" from it instead of inventing them (`CharacterSheetPrompt.ScenarioHeading` and its rule).
    - Empty: Pisara makes those up as before.
    - The scenario goes to the chat model like the description, so 18+ details belong in the 18+ notes.

- **3 · Pisara (JUH-151):** Pisara reads the whole description, not the cut the chat model gets. It uses the same model and privacy rule as the background reading (`PisaraReadingKeeper.ReadDraftAsync`). The step shows who reads her, a progress bar and **Skip**, and the results:
  - **Temperament:** Auto with Pisara's line, or your own nine sliders.
  - **How she knows you:** a role drop-down, what she calls it, and a shared past. Your correction is locked, like a correction on the Persona tab.
  - **Her life:** backstory (one fact per line), right now, her last week, and what's going on.
  - **What lands with her:** touchy subjects and what wins her over.

  Each part has a **Redo** that reads again and replaces only that part.
  - **Condensed description:** shown when the description is over 4,000 characters. Pisara condenses it (`PersonaCondenser`) into text you can edit, and the persona is marked `condensed`. Editing the description clears the mark, and the cut is used again until you condense once more.
  - **18+:** innocence and the 18+ secrets are still read after you save, on the local model, while the gate is open.

  On **Save**, the sheet is stored for her id with the hash of her chat prompt, so the background reader uses it and doesn't read her again. Skipping the step leaves it to the background reader, as before.

- **4 · Voice (JUH-152, JUH-181):** two boxes, so her private and public voices are set together. **Private mode (local voice)** has a drop-down per local engine, with the Voice tab's engine first and marked "(in use)": Piper (the `.onnx` voices next to the Piper voice or `piper.exe`), Kokoro (the bundled voices) and Chatterbox (the voice folders with a `reference.wav`). **Public mode (cloud voice)** has the xAI voices: eve, ara, rex, sal and leo. Each list starts with "(the Voice tab's voice)", which leaves the id empty, and keeps a saved id that is not in the list (`VoiceChoices`). The local engine itself stays a Voice tab setting, because all personas share it. **Hear her** saves first if needed, then speaks her greeting or a short hello in the voice chat uses (`ChatPanel.SpeakSample`). For a persona who isn't the active one, the Studio offers **Save and use her** first, because otherwise you'd hear the active persona's voice.
- **5 · Look:** her 3D avatar source (same as the Look tab, Auto, local prefab, VRM, FBX) and her portrait folder. The 2D video avatar is set for all personas on the Look tab.
- **6 · Review:**
  - a summary: picture, name, tagline, the description meter, Pisara's temperament and relationship lines
  - **First greeting:** shown, not spoken, when the chat is empty, at launch with no history, after a persona switch, or after clearing the history (`SettingsPanel.GreetIfEmpty` → `ChatPanel.ShowGreeting`)
  - **Test chat:** talk to the draft before saving. It uses the chat model under the usual privacy rule, with `NoMemory`, and adds nothing to the real chat or history (`ChatPanel.RequestTestReply`). The last 20 messages are shown.

### Character cards (JUH-153)

**Import a character card (PNG or JSON)…** on the Studio's Start step reads SillyTavern and Chub cards: V2 (`chara_card_v2`, a PNG `tEXt` chunk named `chara` with base64 JSON), V3 (`chara_card_v3`, chunk `ccv3`; it wins when a PNG has both) and old flat V1 JSON. The card becomes a draft on the Identity step; a PNG card's picture becomes her picture (centre square, 512 px). Nothing is saved until **Save**.

| Card field | Persona |
| --- | --- |
| `name` | Display name |
| `description`, `personality` | Description, as labelled paragraphs ("Description:", "Personality:") |
| `scenario` | Scenario (JUH-155) |
| `character_book` | Her lorebook (JUH-156 import, JUH-159 export), see below |
| `first_mes` | Greeting |
| `mes_example` | Example lines: `{{char}}` and `<BOT>` become "Her", `{{user}}` and `<USER>` become "You", `<START>` separators go |
| `creator_notes`, `system_prompt`, `post_history_instructions` | Not imported (Happi writes its own instructions); the status line says so |

In the description and greeting, `{{char}}` becomes her name and `{{user}}` becomes "the user" (in the greeting, "you"). Only the first greeting is used; the status line says how many alternates the card had. Every field is cut to the persona's limits, and the status line names any field that was cut.

**18+ cards.** A card tagged `nsfw`, `nsfl`, `adult`, `18+`, `r18`, `explicit`, `lewd`, `smut`, `erotic`, `porn`, `hentai` or `sexual` keeps only its description in the description. Its personality and scenario go into her 18+ notes, which stay on this PC and reach the prompt only while the 18+ gate is open (chat is then always local). The status line tells the user.

The PNG is read chunk by chunk (signature, length, type, data, CRC). No length in the file is trusted: a chunk longer than what is left of the file, or longer than 8 MB, stops the import with a message. Files over 20 MB are refused.

**Export.** Settings → Her → Persona → **Export**, as:

- **a character card (PNG):** her picture, or a plain blue placeholder when she has none, with a V2 `chara` chunk (correct CRC). Her description goes to `description`, her speaking style (older files) to `personality`, her scenario to `scenario`, her greeting to `first_mes`, her example lines to `mes_example` (with `<START>`, `{{user}}` and `{{char}}`), and her tagline to `creator_notes`;
- **card JSON:** the same card as a `.json` file;
- **a zip:** her Happi persona file (`<id>.json`) and her picture (`<id>.png`).

Her 18+ notes are **never** exported unless **Include 18+ notes** is ticked (shown only when she has some). Ticked, a card carries them in `scenario`, after her scenario, and is tagged `nsfw`, so importing it again routes them back into the 18+ notes. The save dialog asks before overwriting a file.

### Lorebooks (JUH-156)

A lorebook is a set of short entries about her world (places, people, things). When an entry's key comes up in the chat, its text is added to what she knows for that message.

- **Import:** the Studio's Description step, **Lorebook → Import lorebook JSON…**. It reads:
  - SillyTavern World Info (`entries` keyed by number, with `key`, `keysecondary`, `comment`, `content`, `constant`, `selective`, `order`, `disable`)
  - a card's `character_book` (V2/V3, Chub, Character Tavern: `keys`, `secondary_keys`, `name`, `content`, `enabled`, `insertion_order`, `constant`, `selective`), on its own or inside a card
  - NovelAI (`text`, `displayName`) and Agnaistic (`entry`, `keywords`) entries
  - Parsing is `JsonTree`, then `LorebookImport`. `{{char}}` and `{{user}}` become her name and "the user". Entries that look like an API key are dropped, and each entry is capped at 4,000 characters, 500 entries a book.
- **Cards bring theirs:** importing a card, PNG or JSON, also imports its `character_book`, and the status line says so. Character Tavern's PNG download carries the whole card, lorebook included.
- **Matching** (`LoreMatcher`):
  - Happi scans the new message and the last 4 chat lines for keys: whole words, case-insensitive unless the entry says otherwise.
  - A selective entry also needs one of its secondary keys. Constant entries are always on.
  - Entries go in constant first, then by insertion order, up to about 3,000 characters a message, under "What you know about this world…". This happens in `ChatPanel.WithLore`, right after the scene tracker.
- **Storage:** `<id>.lorebook`, encrypted beside her persona file (`LorebookStore`), in V2 `character_book` shape. Duplicating her copies it, and deleting her deletes it. Saving the Studio with **Remove lorebook** removes it.
- **18+:** a lorebook from a card tagged 18+ is marked 18+. With 18+ turned on in settings, the Studio also has an **18+ lorebook** switch. An 18+ lorebook is used only while the 18+ gate is open, and the gate keeps chat local (`LoreNow`, like the 18+ notes). Any other lorebook goes to the chat model like her description.
- **Editing (JUH-159):** under the lorebook, **Edit an entry** opens a drop-down of the entries.
  - Each entry has: Name, "When any of these come up" (keys, comma-separated), "What she knows", "Only when one of these comes up too" (secondary keys, which make the entry selective), **Always on** and **On**.
  - **Add an entry** and **Delete this entry** do what they say. With no lorebook yet, **Start a lorebook** begins an empty one.
  - Nothing is kept until you save. A lorebook with no entries left is removed on save.
- **Export (JUH-159):**
  - **Export lorebook JSON…** writes her lorebook as a V2 `character_book`, which SillyTavern, Chub and Character Tavern import.
  - Exporting her as a card PNG or card JSON adds the lorebook as the card's `character_book`. The zip adds `<id>.lorebook.json`.
  - An 18+ lorebook goes into a card only with "include 18+" ticked, and then the card is tagged `nsfw`. Without the tick it's left out, and the status line says so.
  - Pisara reads the lorebook too (JUH-157).

### Persona grid, her picture, describing it, history (JUH-154)

**Persona tab.** The plain list is now a grid of cards: her picture (round or square, or her initial when she has none), her name and her tagline. The active one is highlighted. Click a card to use her. The **⋯** button on a card opens a small menu under its row:

- **Open in Studio**;
- **Duplicate**: a copy with her picture, named "… copy";
- **Export…**: her character card, card JSON or zip, in the format chosen under the active persona (her 18+ notes are not included from here);
- **Delete** for her own file, or **Delete saved copy** for an edited built-in (the built-in comes back). It asks first.

A built-in persona can't be deleted. "Use setup name" stays as a row above the grid.

**Her picture everywhere.** Her picture is shown left of her name in the new chat's header and in the wheel's Profile panel, round or square as chosen. It is loaded once per persona (`PersonaPictureCache`) and again only after a persona switch, an edit of her, or a picture save, never per frame.

**Hover to see it whole (JUH-166):** rest the mouse on her small picture in the chat header for a quarter of a second and the whole picture opens next to it, uncropped.
- It's shown at its own size, or smaller when it wouldn't fit in Happi's window (at most 92% of it).
- It opens above the small picture when there's room, otherwise below, and it always stays inside the window.
- It closes when the mouse leaves, and while the chat box is being dragged or resized.
- It never takes clicks or hover, so click-through is unchanged (`PictureHoverPreview`, `PicturePreviewMath`).

**Describe her from the picture.** On the Studio's Identity step, **Describe her from the picture** asks a vision model for a short appearance paragraph. The paragraph is shown with **Add to her description** and **Discard**; only Add changes the description (under "Appearance:").

- Outside Private mode, the cloud AI API is used when its model reads pictures (`VisionCapability.CloudModelAcceptsImages`), else the local vision model (Screen tab).
- In Private mode or with the 18+ gate open (`IntimateExpressions.KeepsLocal`), **only the local vision model** is used. Without one, nothing is sent, and the cloud is never a fallback.

**Version history.** Every save of a persona keeps the version it replaces, as `history/<id>.json.<timestamp>.bak` in the personas folder. These copies are encrypted like the file itself and written again at their own path, because the encryption is bound to the path. Only the last 10 stay, and saving the same text again keeps nothing. The Studio's Identity step has **History…** to load one as the draft; **Save** keeps it, and her picture isn't part of the history.

