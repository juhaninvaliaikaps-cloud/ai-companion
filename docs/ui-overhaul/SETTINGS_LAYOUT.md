# Settings layout (JUH-182)

Settings looks like the Profile panel (Jussi, 2026-11-22, JUH-182):

- **Tabs.** There is one row of eight top tabs, with a row of sub-tabs under it. Each tab remembers its sub-tab while the window is open.
- **Boxes.** Every topic sits in its own rounded box with a bold title, so a page is a stack of short boxes instead of one long list. 18+ boxes have a pink title.
- **Help text.** A help note over 170 characters shows its first sentence and a **more** link that opens the rest (`SettingsNotes.Short`). Privacy notes always show in full.
- **Only what applies.** For example, Voice → Engine shows only the chosen local engine's files.

The table of tabs and pages, and when each page shows, is `NewUI/SettingsMenu.cs`. The view is `NewUI/HappiSettingsView.cs` (`BuildPage`).

No settings key changed, so existing settings files load as before. The 18+ items show under the same conditions as before.

### Ribbon Settings preview (JUH-205)

Launch with `--ribbon-ui`. Settings uses the clipped charcoal frame, mint edges, yellow title mark, and cyan selected navigation. At wider sizes, categories sit in a sidebar beside the current page. At narrower sizes they appear above the page and wrap when needed. The frame fits inside the overlay's visible work area. Existing pages, provider routes and visibility rules continue to work.

Screen has one action that changes from Look at screen to Stop look while capture is active. Tools exposes its actual permission choices. First-use consent is shown when an action requests it.

The Settings content pass keeps saved global defaults separate from the active persona's provider, model, voice, name, portrait and avatar overrides. Existing settings load as the saved defaults. The app resolves overrides into a runtime copy each time settings are applied. Previous builds may already have saved a persona's values as global values, and older files do not store the overwritten values separately.

Persona Studio edits identity and portrait folders. Settings keeps the persona picker, import and export. The card menu confirms deletion. Her life shows the three behavior switches together. Memories links to Profile for reading and groups its reset separately from routine controls.

Ollama has one shared editor linked from Public model and Private model. Mode leads with the effective chat route and explains the Persona reading exception. Voice shows the active engine, offers a voice sample, and groups engine files and Whisper setup under disclosures. Keyboard, mouse and controller push-to-talk are on Listening.

Appearance keeps the overlay and avatar controls. App → General owns startup, taskbar and developer visibility. Chat text has a font preview, while transcript retention and clearing stay on History. Screen puts the source and destination before the preview and capture action, with tuning below. Tools has one permission choice per tool and keeps web diagnostics on the Developer page. The 18+ subpages require the existing gate and link to Profile for discoveries.

Ctrl+Tab and Ctrl+Shift+Tab cycle pages. Ctrl+Left and Ctrl+Right switch categories. Page Up, Page Down, Home and End move the current page. The Ribbon view restores each page's scroll position and gives focused buttons a mint outline.

| Tab | Sub-tabs: what is on them |
| --- | --- |
| **Her** | **Persona:** the persona grid, New or import, Edit (Studio, display name, portrait folder), Export, Delete. **Personality:** what the reading found and Re-read, Correct the relationship, Temperament (Auto and presets), Custom temperament (nine sliders). **How she talks:** the reading, her voice, by mood, by closeness, her lines. **Her life:** Relationship changes, Life while you are away, Her own world. **Memories:** the summary, Start over, Smarter memory search. |
| **AI** | **Mode:** Private mode, What public mode may know, Active now. **Chat model:** the public provider (Grok/cloud API or Local Ollama), model, address, Can it see pictures?, the cloud API key and its test. **Private model:** Local Ollama or Custom server, the custom key and its test. **Pisara:** Use a cloud model for Pisara readings, its address and model, and the **Pisara API key** (moved from Keys). **This persona:** the model override. |
| **Voice** | **Speaking:** Speak replies, Voice narration, volume, Muted (F6). **Engine:** the local engine (Piper, Kokoro, Chatterbox, or None: cloud voice only), then only that engine's files (for Kokoro also its cores while a game is in front, JUH-168), the xAI cloud voice and the TTS language. **This persona:** her voice ids. **Listening:** Push-to-talk and its key, the Whisper model and language, the speech model file and its download. **In games:** the mouse button and controller buttons. |
| **Look** | **Avatar:** 3D model or 2D video. **2D video:** her clips, size and position, walking, clip sounds. **3D model:** the source, expression portrait, movement, the model for this persona. **Window:** on top (Always on top, Stay above games), size, click-through and idle frame rate, taskbar button, Start with Windows. |
| **Chat** | **Replies:** Step through replies, Spoken text glow, Show relationship changes. **Text and box:** font and size, chat box and portrait resets. **History:** Save and Clear chat history. |
| **Senses** | **Screen:** read a window, cloud screen analysis, whole screen, automatic looks, vision model, the window or display picker, the preview and Look. **Games:** the game companion, her lines, how often, one box per game, gacha games, status and port. **Tools:** one box per desktop tool, the web search test, the tool action log. |
| **18+** | **Status:** what 18+ still needs, and the Intimate expressions switch. While the gate is open: **Her** (Innocent to Perverted, libido and desire), **Secrets** (her 18+ secrets), **How she talks** (in intimate scenes). Every 18+ setting is on this tab (Jussi). |
| **App** | **Keys:** the hotkeys and the mouse, read only. **Data:** Export, Run setup again, Wipe all local data, Show developer settings. **Developer** (only with Show developer settings): readouts, reply timeout, 2D video tests. |

The two AI model pages can be filled in whichever mode is on, so the public and private models are set up together. A connection test shows only where it tests the right model: the cloud test only in public mode, and the Local Ollama test on the page of the mode that is on. The custom server test always tests the custom server.

## What moved in JUH-182

| Item | Was on | Now on |
| --- | --- | --- |
| Tools, Screen, Games | their own tabs | Senses |
| Window | its own tab | Look → Window |
| Keys, Data, Developer | their own tabs | App |
| Pisara API key | Keys | AI → Pisara |
| Innocence, libido and desire, her 18+ secrets | Persona → Who she is | 18+ → Her, 18+ → Secrets |
| How she talks in intimate scenes | Persona → Who she is | 18+ → How she talks |
| How she talks (everyday) | Persona → Who she is | Her → How she talks |
| The local engine switches (Piper, Kokoro, Chatterbox) | three switches | one choice of four on Voice → Engine |

## What moved in JUH-121

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
