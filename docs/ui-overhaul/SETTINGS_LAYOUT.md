# Settings layout (JUH-121)

The settings window is sorted by what you want to do. Jussi approved the layout on 2026-11-14 (JUH-121). No settings key changed, so existing settings files load as before. One key is new: `showDeveloperSettings`, off by default.

| Tab | What is on it |
| --- | --- |
| **Persona** | Pick a persona, New persona. Edit: display name, personality, speaking style, portrait folder. **Who she is:** what the model read (temperament, relationship), Re-read, Correct the relationship, Innocent to Perverted (18+ only), temperament Auto and presets, and nine temperament sliders for a custom profile. **Her life:** Relationship changes, Life while you are away, Her own world. **Memories:** the list, Start over, Delete all. Delete persona. |
| **AI** | **Private mode** first, because it decides the rest. Then **Pisara model** (off by default): use a cloud model for character-sheet readings, its base URL and model. Then Grok or Local Ollama, or with Private mode on, Local Ollama or Custom server. Model, address, keys, connection tests. **Model for this persona.** |
| **Voice** | Speak replies, Voice narration, volume, Muted (F6). **Voice engine:** cloud voice, Piper or Kokoro and their files, TTS language. **Voice for this persona.** **Speech input:** push-to-talk and its key, Whisper model, speech language. |
| **Look** | 3D model or 2D video. The 2D video folder, size, position, walk speed, clip sounds. **3D avatar:** source, expression portrait, reset rotation, look at cursor. **3D avatar for this persona.** |
| **Chat** | Step through replies, spoken text glow. **Chat box:** reset size (JUH-127: the box is resized by dragging its edges and corners; the old height slider is gone), reset position. **Portrait:** reset position. **Chat history:** save, clear. |
| **Screen** | Screen-look consents and the window or display picker (unchanged). |
| **Window** | Always on top (F8), size, click-through, idle frame rate, taskbar button, start with Windows. |
| **Data** | Export (folder or zip), Run setup again, Wipe all local data, Show developer settings. |
| **18+** | Whether 18+ is on, and what it still needs: Private mode, the adult portraits folder, the switch. The **Intimate expressions (18+)** switch. |
| **Keys** | The hotkeys and the mouse actions, read only. **Pisara API key** (masked; empty uses the Grok key). |
| **Developer** | Only with Data → Show developer settings. Debug info, mood readout, reply timeout, the 2D video test buttons (including the adult ones while 18+ is on). |

## What moved

| Item | Was on | Now on |
| --- | --- | --- |
| Private mode | Privacy | AI, at the top |
| Save and clear chat history | Privacy | Chat |
| Export, wipe, run setup again | Privacy | Data |
| Memories and Start over | Privacy | Persona |
| The three Pisara switches | Overlay | Persona, Her life |
| Show debug info, mood readout | Overlay | Developer |
| Pause | Overlay | removed (the wheel's Pause her and the tray) |
| Intimate expressions (18+) | Avatar | 18+ |
| 2D video test buttons | Avatar | Developer |
| Reply timeout | AI | Developer |
| Persona provider and model | Persona editor | AI, Model for this persona |
| Persona voice ids | Persona editor | Voice, Voice for this persona |
| Persona 3D avatar source | Persona editor | Look, 3D avatar for this persona |
| Right-click wheel help | Happi | Keys |

## Notes that were cut

Notes that only repeated "applies immediately", developer notes ("Asset Store, Mixamo, and Daz models stay out of this project", "Secrets live in the secret store, never settings.json", "Test connection does not write the key ... to the log"), and outdated ones ("New chat only.", "The 3D avatar stays loaded.").

## The wheel

Settings at the top centre, then Hide or Show chat, Profile, Watch my screen, Persona, Pause or Resume her (#108). See DECISIONS.md D5b.
