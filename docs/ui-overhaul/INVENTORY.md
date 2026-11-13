# UI inventory (JUH-104 acceptance checklist)

Every control, setting, button, hotkey, debug action, wizard step and panel that the
app has on `main` 64965e7, with the file and method that implements it. The new UI
("New UI (preview)") is accepted only when every row below still works, either in the
new UI or through the old path that stays reachable from it.

Paths are relative to `Assets/_Project/Scripts/`. Method names are the ones on `main`.
**ID** is used by `MORNING-REPORT.md` and by the manual test checklist.

Columns:

- **Old UI**: where it lives today.
- **New UI**: where it lives in the new UI (filled in as PRs C, D and E land). `—` means not
  moved yet; the old path stays reachable.

Status after PRs C–E: chat items (section 3) are in the new chat view (PR C); every settings
item (section 4) is on the same-named tab of the new settings view (PR E), and "Classic
settings" opens the old panel; the wizard (section 5), tray (6), debug readouts (7) and gates (8)
are shared and unchanged. Section 9 lists what is new.

## 1. Panels and windows

| ID | Panel | Implemented by | Opens with | New UI |
| --- | --- | --- | --- | --- |
| P-01 | Transparent overlay window (borderless, layered, topmost, click-through) | `Overlay/OverlayWindow.cs` `StartCompanion`, `Update`, `EnsureChrome`, `ApplySizedWindow` | always | unchanged (shared) |
| P-02 | Debug text top-left (topmost, fps, emotion, debug hint, rotate/size/zoom) | `Overlay/OverlayWindow.cs` `OnGUI`, `RefreshEmotionLabel`, `RefreshRotateLabel`, `AccumulateFps` | only with Settings → Overlay → "Show debug info" (S-OV-10, off by default since the follow-up) | same switch in both UIs |
| P-03 | Chat panel beside the avatar | `UI/ChatPanel.cs` `BuildUi`, `RefreshPlacement`, `SetVisible` | F10, double-click on her, tray "Open chat" | new chat view (PR C) |
| P-04 | Model badge (active provider/model, green local, amber cloud) | `UI/ChatPanel.cs` `RefreshModelBadge` | always while chat canvas exists | new chat view header (PR C) |
| P-05 | Settings panel with 8 tabs | `Settings/SettingsPanel.cs` `BuildChrome`, `RebuildBody`, `Open`, `Close`, `Place` | F12, tray "Settings" | new settings view (PR E) |
| P-06 | First-run setup wizard (5 steps) | `Settings/SetupWizard.cs` `Show`, `Hide`, `BuildChrome`, `RebuildBody`; flow in `Settings/SetupWizardLogic.cs` | first launch without settings/key, `--setup`, Settings → Privacy → "Run setup again" | old wizard (PR E if time) |
| P-07 | Expression portrait panel | `Settings/ExpressionPortraitPanel.cs` `EnsureUi`, `Refresh`, `Place`, `Apply` | Settings → Avatar → "Show expression portrait" | unchanged (shared) |
| P-08 | Screen-look exact-frame viewer | `Settings/SettingsPanel.Screen.cs` `BuildExactFrame`, `OpenExactFrame`, `CloseExactFrame` | Settings → Screen → "Inspect exact frame" | old panel (linked from new settings) |
| P-09 | Mood readout | `Settings/CompanionMoodKeeper.cs` `OnGUI` | Settings → Overlay → "Show mood readout (debug)"; top-left, below the debug text when that is shown | unchanged (shared) |
| P-10 | System tray icon and menu | `Overlay/CompanionTray.cs`, `Overlay/WindowsTrayHost.cs`, `Overlay/TrayMenuState.cs` `Build` | right-click tray icon | unchanged (shared) |
| P-11 | 2D video avatar | `Settings/VideoAvatar/VideoAvatar.cs` | Settings → Avatar → "Avatar: 2D video" | unchanged (shared) |

## 2. Hotkeys and mouse

Listed in-app by `Settings/HotkeyCatalog.cs` `Current` (Settings → Controls) and in the wizard tour.
Letter, digit and F keys are blocked while a text field has focus (`Core/DebugInputGuard.cs` `IsBlocked`).

| ID | Input | Action | Implemented by | New UI |
| --- | --- | --- | --- | --- |
| K-01 | F6 | Mute / unmute speech | `Voice/VoiceSpeaker.cs` `Update` (line ~239) | unchanged |
| K-02 | F7 | Cycle avatar source | `Avatar/AvatarMount.cs` `Update` (`cycleKey`) | unchanged |
| K-03 | F8 | Toggle always on top | `Overlay/OverlayWindow.cs` `Update` (`topmostKey`) → `SetTopmost` | unchanged |
| K-04 | F9 | Cycle expression preview (3D face, and portrait when shown) | `Avatar/AvatarAnimationController.cs` `Update`; `Settings/ExpressionPortraitPanel.cs` `LateUpdate` | unchanged |
| K-05 | F10 | Show / hide chat | `UI/ChatPanel.cs` `Update` → `SetVisible` | toggles the new chat view when New UI is on |
| K-06 | F12 | Show / hide settings | `Settings/SettingsPanel.cs` `Update` (`HotkeyCatalog.SettingsKey`) | toggles the new settings view when New UI is on (PR E) |
| K-07 | Push-to-talk key, hold (default V) | Record speech for Whisper | `Voice/SpeechListener.cs` (`sttSettings.PushToTalkKey`) | unchanged |
| K-08 | 1–5 (and keypad 1–5) | Emotion debug | `Avatar/AvatarAnimationController.cs` `Update` → `Core/AvatarAnimatorModel.cs` | unchanged |
| K-09 | T | Toggle talk (debug) | `Avatar/AvatarAnimationController.cs` `Update` | unchanged |
| K-10 | Right-click on a visible pixel of her | Toggle rotate mode | `Overlay/OverlayWindow.cs` `Update` (`AvatarRotateMode.ShouldToggle`) | New UI on: right-click opens the radial wheel, **Shift+right-click** toggles rotate mode (PR D) |
| K-11 | R (in rotate mode) | Reset rotation | `Overlay/OverlayWindow.cs` `Update` (`AvatarRotateMode.ShouldReset`) | unchanged |
| K-12 | Middle-click on her | Reset yaw (rotate mode) and camera zoom | `Overlay/OverlayWindow.cs` `Update` → `ResetCameraZoom` | unchanged |
| K-13 | Esc | Leave rotate mode; stop speech / cancel reply / stop look; blur chat fields | `Overlay/OverlayWindow.cs` `Update`; `UI/ChatPanel.cs` `Update` | unchanged; also closes the wheel (PR D) |
| K-14 | Esc (settings open) | Cancel wipe/clear confirm, else blur field, else close settings | `Settings/SettingsPanel.cs` `Update` | same in new settings (PR E) |
| K-15 | Esc (wizard open) | Close wizard | `Settings/SetupWizard.cs` `Update` | unchanged |
| K-16 | Scroll over her | Zoom toward the cursor | `Overlay/OverlayWindow.cs` `AdjustZoom` | unchanged |
| K-17 | Ctrl + scroll over her | Scale avatar / window | `Overlay/OverlayWindow.cs` `AdjustSize` | unchanged |
| K-18 | Left-drag on her | Move the window (rotate mode: rotate her) | `Overlay/OverlayWindow.cs` `Update` (`WindowDragGate`, `_yawDrag`) | unchanged |
| K-19 | Double-click on her | Open chat | `Overlay/OverlayWindow.cs` `NotifyAvatarDoubleClick` → `UI/ChatPanel.cs` `OnAvatarDoubleClick` | opens the new chat view when New UI is on |
| K-20 | Arrow keys (2D video, settings closed) | ←/→ walk, ↓ stop, ↑ front idle | `Settings/VideoAvatar/VideoAvatar.cs` (line ~839) | unchanged |
| K-21 | Enter in chat input | Send | `UI/ChatPanel.cs` `SubmitDraft` | same in new chat |
| K-22 | Enter in API-key field (chat) | Save key | `UI/ChatPanel.cs` `SaveKey` | same in new chat |
| K-23 | `--setup` command-line argument | Force the setup wizard | `Settings/SetupWizardLogic.cs` `SetupWizardGate.ForceArgument` | unchanged |
| K-24 | `-newui` command-line argument (new) | Turn New UI (preview) on for this launch | `UI/NewUI/NewUiSwitch.cs` | new |

## 3. Chat panel (`UI/ChatPanel.cs`)

| ID | Control | Implemented by | New UI |
| --- | --- | --- | --- |
| C-01 | Title "Chat   F10" | `BuildUi` | header |
| C-02 | Mute / Muted button | `OnMuteClick`, `RefreshMuteLabel` | header button |
| C-03 | Send / Stop button (Stop while a reply streams or she speaks) | `OnActionClick`, `Update` (label) | header/footer button |
| C-04 | "What do you see?" / "Stop look" button (disabled when the screen-look policy says so) | `OnLookClick`, `Update` (`LookButtonInteractable`) | button |
| C-05 | Transcript (last 100 messages, 6000 chars each, one Text per message) | `RefreshTranscript`, `AddMessageText`, `UI/ChatTranscriptView.cs` | one message object per message, same limits |
| C-06 | Mic hold button (Mic / Rec / ...) | `CreateHoldButton`, `SetMicHeld`, `RefreshMic` | hold button |
| C-07 | Message input (Enter sends) | `CreateField`, `SubmitDraft` | input |
| C-08 | API key input + Save | `SaveKey` | input + Save |
| C-09 | Auto-placement beside her, left/right, clamped to the window | `RefreshPlacement`, `ResolveAnchorX`, `UI/ChatPanelPlacement.cs` | same placement math |
| C-10 | Chat open state remembered | `SetVisible` (`PlayerPrefs` `ai-companion.chat-open`) | same pref |
| C-11 | System notes in transcript (errors, speech notes, refusals) | `ShowSpeechNote`, `TrySend`, `SubmitObservation` | shown |
| C-12 | Streaming reply, cue applied at end, memory capture, auto-look request | `TrySend`, `Dispatch`, `ApplyCue`, `CaptureMemory` | same engine; cues per segment (PR C) |
| C-13 | Screen observations (cloud image, local text, fast, auto) | `SubmitObservation`, `SubmitTextObservation`, `SubmitFastObservation`, `SubmitAutoObservation` | same engine |
| C-14 | User-mood face reaction before the reply | `TrySend` (`UserMoodDetector`, `UserMoodReaction`) | same engine |

## 4. Settings panel tabs (`Settings/SettingsPanel*.cs`)

Shared chrome: title (`AppName.Display`), Close button, 8 tab buttons, scrolling body, status line
(`BuildChrome`, `RefreshTabs`, `SetStatus`). Toggles are buttons labelled "Name: on/off" (`AddToggle`).
Every change goes through `ApplyLive` → `SettingsApplicator.Apply` → `SaveSettings` (encrypted
`settings.json`, `CompanionSettingsStore.Save`).

### 4.1 AI tab (`SettingsPanel.cs` `BuildAi`)

| ID | Control | Setting / action | New UI |
| --- | --- | --- | --- |
| S-AI-01 | "Active now: …" label | `ActiveModelText` | |
| S-AI-02 | Grok button (active / remembered) | `ProviderId = grok` | |
| S-AI-03 | Local Ollama button | `ProviderId = local` | |
| S-AI-04 | Private-mode note | — | |
| S-AI-05 | Grok model field | `Model` | |
| S-AI-06 | Base URL field | `BaseUrl` | |
| S-AI-07 | Local model field | `LocalModel` | |
| S-AI-08 | Local address field | `LocalBaseUrl` | |
| S-AI-09 | Timeout seconds field | `TimeoutSeconds` | |
| S-AI-10 | API key status note (masked) | `ApiKeyMask.Label` | |
| S-AI-11 | Paste a new API key field + Save key | `SaveKey` → `ApiKeyFile.SavePreferred` | |
| S-AI-12 | Remove saved key | `RemoveKey` | |
| S-AI-13 | XAI_API_KEY override note | `EnvOverrides` | |
| S-AI-14 | Test connection / Testing... | `TestConnection`, `Probe` (`ConnectionProbe.Plan`) | |

### 4.2 Avatar tab (`BuildAvatar`, `SettingsPanel.VideoAvatar.cs` `BuildVideoAvatar`, `SettingsPanel.Portraits.cs` `BuildPortraitToggle`)

| ID | Control | Setting / action | New UI |
| --- | --- | --- | --- |
| S-AV-01 | Avatar: 3D model | `AvatarMode = 3d` (`SetAvatarMode`) | |
| S-AV-02 | Avatar: 2D video | `AvatarMode = video` | |
| S-AV-03 | Video status line (live) | `TickVideoAvatarStatus` | |
| S-AV-04 | Video folder field | `VideoAvatarFolder` | |
| S-AV-05 | Reload video | `VideoAvatar.Reload` | |
| S-AV-06 | Video size slider | `VideoAvatarSize` | |
| S-AV-07 | Video left/right slider | `VideoAvatarOffsetX` | |
| S-AV-08 | Video up/down slider | `VideoAvatarOffsetY` | |
| S-AV-09 | Reset video size and position | size/offsets to defaults | |
| S-AV-10 | Test: walk left / walk right / stop / front idle | `VideoTest` → `TestWalk`, `TestStop`, `TestFront` | |
| S-AV-11 | Test reactions: happy, concerned, turn360, embarrassed, flustered, pout, wink_dance, turn_around_back | `VideoReaction` → `RequestReaction` | also wheel → Do something (PR D) |
| S-AV-12 | Walk speed slider | `VideoAvatarWalkSpeed` (lowest step = clip speed) | |
| S-AV-13 | Play clip sounds | `PlayClipSounds` (never over her TTS) | |
| S-AV-14 | Clip sound volume slider | `ClipSoundVolume` | |
| S-AV-15 | Avatar source: Auto / Local prefab / VRM sample / FBX sample | `AvatarSource` (`AddSourceButton`) | |
| S-AV-16 | Show expression portrait | `ShowExpressionPortrait` | |
| S-AV-17 | **Intimate expressions (18+)** | `IntimateExpressions`; active only with Private mode and `%APPDATA%\ProjectLeia\portraits-adult` (`IntimateExpressions.Configure`) | same condition as today |
| S-AV-18 | Reset rotation | `AvatarMount.ResetRotation` | |
| S-AV-19 | Look at cursor | `CursorLookAt` | |

### 4.3 Voice tab (`BuildVoice`)

| ID | Control | Setting / action | New UI |
| --- | --- | --- | --- |
| S-VO-01 | "Active now" + cloud/local voice note | `VoiceRoute.UsesCloudTts` | |
| S-VO-02 | Speak replies | `TtsEnabled` | |
| S-VO-03 | Cloud voice (Grok) field | `VoiceId` | |
| S-VO-04 | Piper exe field (appears twice on main) | `PiperExe` | once |
| S-VO-05 | Piper voice model field (appears twice on main) | `PiperVoice` | once |
| S-VO-06 | Use Piper locally / Local voice: Piper (selected) | `TtsProvider = piper` (off → `xai`) | |
| S-VO-07 | Use Kokoro locally / Local voice: Kokoro (selected) | `TtsProvider = kokoro` (off → `xai`) | |
| S-VO-08 | Kokoro python / script / model / voices / voice fields | `KokoroPython`, `KokoroScript`, `KokoroModel`, `KokoroVoices`, `KokoroVoice` | |
| S-VO-09 | TTS language field | `TtsLanguage` | |
| S-VO-10 | Volume slider | `Volume` | |
| S-VO-11 | Muted (same switch as F6) | `Muted` | |
| S-VO-12 | Push-to-talk | `SttEnabled` | |
| S-VO-13 | Push-to-talk key field (taken keys fall back to V) | `PushToTalkKey` | |
| S-VO-14 | Whisper model field | `WhisperModel` | |
| S-VO-15 | Speech language field | `SttLanguage` | |
| S-VO-16 | Placeholder notes: wake word, local-only voice, voice agent | — | |
| S-VO-17 | **Voice narration** (new, JUH-108, default off) | `VoiceNarration` | new chat view (PR C) |

### 4.4 Controls tab (`BuildControls`)

| ID | Control | Implemented by | New UI |
| --- | --- | --- | --- |
| S-CT-01 | Rebind note + hotkey list (K-01…K-17) | `HotkeyCatalog.Current` | |

### 4.5 Overlay tab (`BuildOverlay`)

| ID | Control | Setting / action | New UI |
| --- | --- | --- | --- |
| S-OV-01 | Always on top (F8 too) | `AlwaysOnTop` | |
| S-OV-02 | Size slider | `Scale` | |
| S-OV-03 | Click-through: silhouette → panels only → capture the window | `ClickThrough` | |
| S-OV-04 | Idle frame rate slider | `IdleFrameRate` | |
| S-OV-05 | Hide taskbar button | `HideTaskbarButton` | |
| S-OV-06 | Start with Windows | `StartWithWindows` | |
| S-OV-07 | Pause (matches tray checkmark) | `Paused` | also wheel → Leave me alone (PR D) |
| S-OV-08 | Show mood readout (debug) | `ShowMoodDebug` | |
| S-OV-09 | **New UI (preview)** (new, default off) | `NewUiPreview` | |
| S-OV-10 | **Show debug info** (new, default off): shows P-02 | `ShowDebugInfo` | Overlay tab |

### 4.6 Privacy tab (`BuildPrivacy`, `SettingsPanel.LocalData.cs`, `SettingsPanel.Memory.cs`)

| ID | Control | Setting / action | New UI |
| --- | --- | --- | --- |
| S-PR-01 | Save chat history | `ChatHistory` (writes per-persona encrypted history) | |
| S-PR-02 | **Private mode** | `PrivateMode` (chat, voice and screen stay local) | |
| S-PR-03 | Clear chat history → Confirm clear / Cancel | `ConfirmClear` | |
| S-PR-04 | Export folder field + Choose export folder | `ChooseExportFolder` (`ExportFolderDialog.TryPick`) | |
| S-PR-05 | Export decrypted folder | `ExportLocal(false)` → `LocalDataPortal.ExportFolder` | |
| S-PR-06 | Export decrypted zip | `ExportLocal(true)` → `LocalDataPortal.ExportZip` | |
| S-PR-07 | Wipe all local data → Confirm wipe / Cancel | `ConfirmWipe` → `LocalDataActions.Wipe` | |
| S-PR-08 | Run setup again | `SetupReplayRequested` → `SetupWizard` | |
| S-PR-09 | Memory summary notes | `BuildMemorySection` (`MemoryViewerModel.Summary`) | also wheel → Memories (PR D) |
| S-PR-10 | Memory row → Confirm delete / Cancel | `MemoryStore.Delete` | also wheel → Memories |
| S-PR-11 | Delete all memories → Confirm / Cancel | `MemoryStore.DeleteAll` | |

### 4.7 Persona tab (`SettingsPanel.Persona.cs` `BuildPersona`)

| ID | Control | Setting / action | New UI |
| --- | --- | --- | --- |
| S-PE-01 | Notes + "Active now" | — | |
| S-PE-02 | Use setup name / Setup name (active) | `ClearActivePersona` | |
| S-PE-03 | One button per persona (active / saved) | `UsePersona` (switches per-persona history) | also wheel → Persona (PR D) |
| S-PE-04 | New persona | `CreatePersona` | |
| S-PE-05 | Display name, Personality, Speaking style | `SaveActive` | |
| S-PE-06 | Piper / Kokoro / Cloud voice ids | `SaveActive` | |
| S-PE-07 | Provider: Grok / Local Ollama / Leave unchanged | `SaveActive` | |
| S-PE-08 | Model name | `SaveActive` | |
| S-PE-09 | Avatar auto / local / VRM / FBX / Leave unchanged | `SaveActive` | |
| S-PE-10 | Portrait folder | `SaveActive` | |
| S-PE-11 | Delete saved copy / Delete persona | `DeleteActivePersona` | |

### 4.8 Screen tab (`SettingsPanel.Screen.cs` `BuildScreen`)

| ID | Control | Setting / action | New UI |
| --- | --- | --- | --- |
| S-SC-01 | Read a window | `ScreenLookEnabled` (`RefreshWindows` / `DisableFeature`) | also wheel → Watch my screen (PR D) |
| S-SC-02 | Allow cloud screen analysis | `ScreenCloudConsent` | |
| S-SC-03 | Whole screen instead of a window | `ScreenCaptureWholeMonitor` | |
| S-SC-04 | Allow whole-screen capture | `ScreenWholeMonitorConsent` | |
| S-SC-05 | Monitor buttons (Selected: …) | `ScreenLookController.SelectMonitor` (`ScreenMonitorDevice`) | also wheel → Watch my screen |
| S-SC-06 | Let her look during chat | `ScreenAutoLook` | |
| S-SC-07 | Pause automatic looks | `ScreenAutoLookPaused` | |
| S-SC-08 | Fast screen replies | `ScreenFastReply` | |
| S-SC-09 | Chat engine / vision engine note | `CurrentDecision`, `PlansLocalVision` | |
| S-SC-10 | Local vision model field + Use qwen2.5vl:3b | `LocalVisionModel` | |
| S-SC-11 | Route notes (local / cloud / consent required) | `ScreenLookPolicy` | |
| S-SC-12 | Status line | `ScreenLookController.StatusText` | |
| S-SC-13 | Refresh window list + window buttons | `RefreshWindows`, `Select` | |
| S-SC-14 | Preview image + delivery note | `AddPreview` | |
| S-SC-15 | Inspect exact frame (Fit / 1:1 / Close) | `OpenExactFrame`, `BuildExactFrame` | |
| S-SC-16 | Look at screen | `RequestLook` / `Stop` | |
| S-SC-17 | Stop / Stop look | `Stop` | |
| S-SC-18 | Clear window | `ClearSelection` | |

## 5. Setup wizard (`Settings/SetupWizard.cs`, `Settings/SetupWizardLogic.cs`)

Footer: Back, Skip, Next/Done, step label "Step n of 5 · Title" (`RefreshFooter`). Esc closes.

| ID | Step | Controls | Implemented by |
| --- | --- | --- | --- |
| W-01 | Welcome | three notes | `BuildWelcome`, `SetupWizardCopy` |
| W-02 | AI | key status, XAI_API_KEY note, private note, key field, Test connection | `BuildAi`, `CommitTypedKey`, `TestConnection`, `Probe` |
| W-03 | Avatar | Auto / Local prefab / VRM sample / FBX sample (live preview) | `BuildAvatar`, `AddSourceButton` → `SettingsPanel.PreviewSetup` |
| W-04 | Personality | Companion name, Friend / Girlfriend | `BuildPersonality`, `AddStyleButton` |
| W-05 | Tour | tour notes + hotkey list | `BuildTour`, `SetupWizardFlow.TourHotkeys` |
| W-06 | Finish | saves and applies | `Finish` → `SettingsPanel.CommitSetup` |
| W-07 | Skip | step default, then next | `Skip` |

## 6. Tray menu (`Overlay/TrayMenuState.cs` `Build`, `Overlay/CompanionTray.cs`)

| ID | Item | Action |
| --- | --- | --- |
| T-01 | Hide companion / Show companion | `ICompanionVisibility.SetCompanionVisible` |
| T-02 | Open chat | `IChatWindow.OpenChat` |
| T-03 | Settings | `SettingsPanel.Open` |
| T-04 | Pause (checkmark) | `Paused` |
| T-05 | Start with Windows (checkmark) | `StartWithWindows` |
| T-06 | Quit | `Application.Quit` |
| T-07 | Left-click / double-click on tray icon | `TrayClick` (show and open chat) |

## 7. Debug actions and readouts

| ID | Action | Implemented by |
| --- | --- | --- |
| D-01 | Emotion keys 1–5, T talk, F9 preview | K-08, K-09, K-04 |
| D-02 | Debug text: topmost, fps, emotion label, debug hint, rotate/size/zoom (hidden unless S-OV-10) | P-02 |
| D-03 | Mood readout (her mood, top 3) | P-09, S-OV-08 |
| D-04 | Video avatar test buttons and arrow keys | S-AV-10, S-AV-11, K-20 |
| D-05 | Placement log line in Player.log | `UI/ChatPanel.cs` `LogPlacement` |
| D-06 | Cue/emotion log lines per reply | `UI/ChatPanel.cs` `ApplyCue` |

## 8. Privacy and 18+ gates that must not change

| ID | Gate | Implemented by |
| --- | --- | --- |
| G-01 | Intimate content only when `IntimateExpressions.Active` (enabled + Private mode + adult folder present) | `Core/IntimateExpressions.cs` `Configure`, `Allows` |
| G-02 | Intimate/private content never goes to cloud chat or cloud TTS | `IntimateExpressions.KeepsLocal`, `Voice/OutboundPrivacy.cs`, `Voice/VoiceRoute.cs` |
| G-03 | Consent rule in every prompt; closed gate forbids intimate scenes | `Core/IntimateBoundaries.cs` (#65) |
| G-04 | Adult portraits load only from `%APPDATA%\ProjectLeia\portraits-adult` | `Settings/Portraits/PortraitLibrary.cs`, `SettingsApplicator` (`PortraitFolders.Adult`) |
| G-05 | Private mode: screen image never sent to cloud | `Settings/ScreenLookController.cs`, `ChatPanel.SubmitObservation` |
| G-06 | Clip sounds never play over her TTS | `Settings/VideoAvatar/VideoClipPlayer.cs` |
| G-07 | API key never stored in settings.json | `CompanionSettingsSecrets.ContainsForbiddenSecretField` |
| G-08 | Settings, memories, history encrypted at rest (#58) | `Core/Storage/ProtectedLocalFile.cs` |

## 9. New in the new UI (no old counterpart)

| ID | Item | PR |
| --- | --- | --- |
| N-01 | New UI (preview) setting + `-newui` | A/B |
| N-02 | Visual-novel chat playback: narration/speech split, highlight, Auto/Step, Next/click/Space, Show all/Skip, TTS prefetch, cue per segment | C |
| N-03 | Voice narration setting | C |
| N-04 | Main radial wheel above her head (8 fixed slices) | D |
| N-05 | Body-part wheel component + JUH-101 hit-zone hook | D |
| N-06 | Movable chat box: drag by the header bar, saved position (`ChatBoxX`/`ChatBoxY`), clamped on screen on either monitor, Settings → Happi → "Reset chat position" | follow-up |
| N-07 | Spoken-text glow: words light up as she says them; Settings → Happi → "Spoken text glow" (`SpokenGlow`, on) + intensity (`SpokenGlowIntensity`) | follow-up |
