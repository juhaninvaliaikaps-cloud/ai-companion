# Decisions made during the overnight JUH-104 / JUH-108 run

Each entry: what was ambiguous, what was chosen, and why. Nobody was available to ask, so
the safest reasonable option was taken.

## PR A

**A1. Mockup file location.** The addendum names `E:\LOOT\radial_mockup.png`, which does not
exist on this PC. The identical approved image was attached to the task in chat; that file is
committed as `docs/ui-overhaul/radial_mockup.png`.

**A2. uGUI + TextMeshPro, not UI Toolkit.** `OverlayWindow.IsOverUi` decides click-through
with `EventSystem.RaycastAll`, which only sees uGUI graphics. UI Toolkit panels would need a
second hit test. uGUI keeps that rule unchanged.

**A3. TMP Essential Resources committed.** `com.unity.ugui` 2.6.0 includes TextMeshPro, but its
default font asset, settings and SDF shaders come from the "TMP Essential Resources" package.
Without them a player build has no default TMP font. They were imported once in batchmode
(`-importPackage`) into `Assets/TextMesh Pro/` (about 4 MB, the `.ttf` through LFS). The old
UI does not use them.

**A4. Theme is a C# class, not a ScriptableObject asset.** `HappiTheme` holds every colour,
font, size, radius, spacing and timing, and `HappiTheme.Current` is the single instance the
new views read. A plain class has no asset GUID or import step, is easy to diff, and works in
EditMode tests. Making it a ScriptableObject later only means wrapping this class.

**A5. 100–200% scaling through the theme.** `OverlayWindow.LockUiPixels` forces every
`CanvasScaler` to `ConstantPixelSize` with factor 1, including new ones. Changing that would
change the old UI. New views multiply their theme sizes by `HappiTheme.UiScale(Screen.dpi)`
(clamped 1.0–2.5).

**A6. New code location.** The brief says `Assets/Scripts/UI/NewUI/`; the project keeps scripts
under `Assets/_Project/Scripts/`, so new views go in `Assets/_Project/Scripts/UI/NewUI/` with a
new assembly `AICompanion.NewUI`. It references the existing assemblies; none of them
references it, so the old UI cannot depend on the new one.

**A7. Product name.** Following the JUH-104 comment, new views use "Happi" as the app name
(settings header, wizard, chat title). The old UI keeps `AppName.Display` unchanged. The wheel
centre shows her name: the active persona's display name, else the setup name, else "Leia".

**A8. Right-click conflict.** On `main`, right-click on her toggles rotate mode (K-10). The
wheel needs right-click. With New UI **on**, right-click on her opens the wheel and
**Shift+right-click** toggles rotate mode as before; rotate mode stays reachable. With New UI
**off** nothing changes. (Implemented in PR D.)

**A9. Inventory duplicates.** The Voice tab on `main` shows the Piper exe and Piper voice
fields twice. The inventory lists them once (S-VO-04, S-VO-05); the new settings view shows
them once. The old panel is not changed.

## PR B

**B1. Chat sending stays in ChatPanel.** Open PR #63 rewrites ChatPanel's send path (provider
routing for the custom private endpoint, `SpeechOriginFor`). Moving `TrySend` / `Dispatch` /
`BeginSpeech` into a presenter now would make #63 conflict on almost every line. `ChatPresenter`
therefore owns the button behaviour and labels (Send/Stop order, Mute, Look, Escape, mic label)
and calls ChatPanel's existing entry points (`TrySend`, `CancelReply`) through
`ChatPanel.Presenters.cs`. ChatPanel.cs itself changes by one word (`partial`) plus the call
sites. Privacy routing is untouched.

**B2. Presenters live next to what they wrap.** `ChatPresenter` is in `AICompanion.UI` (it
only needs Core interfaces) so the old ChatPanel can call it. The settings-side presenters
are in `AICompanion.Settings` (`Settings/Presenters/`) because they need `CompanionSettings`,
and Settings already references UI. The new views (`AICompanion.NewUI`) reference both.

**B3. SettingsPanel is the settings host.** `SettingsPanel.Presenters.cs` implements
`ISettingsHost` / `IPersonaHost` on top of the panel's existing `ApplyLive`, `SetStatus`,
`UsePersona`, `ClearActivePersona`, `ConfirmClear` and `ConfirmWipe`. So every view (old tabs,
new settings, wheel) applies and saves settings the same way and there is still only one
live `CompanionSettings` object. No second settings file.

**B4. What the old panels now call.** Chat buttons and labels (ChatPresenter), click-through
cycle and its labels (SettingsPresenter), export (LocalDataPresenter), the eight video
reaction test buttons and their status lines (VideoAvatarPresenter). The Persona tab is
**not** changed, because #63 and #68 both edit SettingsPanel.Persona.cs; `PersonaPresenter`
repeats its label wording instead, and a test pins that wording. The setup wizard already
keeps its logic in `SetupWizardLogic.cs` (`SetupWizardFlow`, `SetupWizardGate`,
`SetupWizardCopy`), so it is not split further.

**B5. New settings keys.** `newUiPreview` and `voiceNarration` are appended to the end of the
settings JSON reader and writer (both default false). Old files without them load with both
off; no key was renamed or removed; the file stays encrypted by the existing
`ProtectedLocalFile` path.

**B6. Where the switch is set.** `SettingsApplicator.Apply` (startup, every change, wipe) ends
with `NewUiSwitch.Set(Resolve(setting, args))`. `-newui` (or `--newui`) turns the new UI on
for that launch without saving the setting.

**B7. "Watch my screen" never grants consent.** The monitor list is offered only when "Whole
screen instead of a window" and "Allow whole-screen capture" are already on in Settings →
Screen; otherwise the presenter returns a note pointing there.

**B8. "Do something" offers only existing actions.** In 2D: the reaction clips the video
folder actually has (`AvatarReactionCatalog.Names`), plus walk left/right, stop and face me
(the existing test actions). In 3D: the five debug emotions (keys 1–5).

**B9. Mood "why".** The JUH-103 mood state keeps strengths, not a history of causes, so the
explanation is simple: a sentence per mood (late night for sleepy, etc.) plus the three
strongest moods. The intimate mood is never named unless the 18+ gate is open.

## PR C

**C1. Visual-novel box at the bottom.** The overlay window is small (about 430×650 at 100%)
and she stands in the middle, so the old side panel ends up about 100 px wide. The new chat
is a dialogue box along the bottom of the window, the usual visual-novel layout. It covers
her legs while open; F10, × and the tray close it as before. **History** expands the box to
show the whole transcript. The API-key row is shown while no key is saved, and always in
History, so C-08 stays reachable.

**C2. ChatPanel stays the chat engine.** With the new UI on, ChatPanel still sends, routes
(private mode, #63's custom endpoint once merged), captures memory and handles screen looks.
The new view only draws and calls `ChatPresenter`. Hooks added to ChatPanel are a speech
override (`UseSpeechOverride`), a raw-reply observer, and hiding its own canvas
(`SetLegacyHidden`, which also zeroes its click-blocking rect). The open state
(`ai-companion.chat-open`, F10, tray, double-click, push-to-talk) is shared.

**C3. Speech still goes through the chat's privacy origin.** The visual-novel player never
picks a voice route itself. ChatPanel calls `BeginUtterance(origin)` on the player's sink as
before; the player then speaks each line with `VoiceSpeaker.BeginUtterance(origin)`, so
`VoiceUtterance`/`VoiceRoute`/`IntimateExpressions.KeepsLocal` decide exactly as on main, and
cloud TTS never gets private or intimate lines. With "Voice narration" off (default) narration
lines are never sent to any TTS.

**C4. Prefetch through a playback hold.** `VoiceSpeaker` gets `ISpeechPlaybackHold`: a held
line is synthesized and queued but not played until Next. Cancel, mute, pause and any new
utterance clear the hold, so nothing can stay stuck. Prefetch starts only while the voice is
idle (the reader is on narration or waiting in Step mode). While a line is held,
`IsSpeaking` stays true, so clip sounds stay silent (the "never over her TTS" rule holds).

**C5. Cues per line.** Cue tags are read from the raw reply (`ChatExchange.RawDelta`) and
attached to the line that follows them; a tag after some text starts a new line. Each line's
cues go to the animation director and to her mood (`CompanionMoodInputs.ReportCue`) when the
line starts. ChatPanel's end-of-reply cue is skipped while the new chat is on, so nothing
fires twice. A reply with no tag returns her to neutral at the end, as on main. "Show all"
jumps to the latest cue instead of replaying every one.

**C6. Prompt line only with the new UI.** `VisualNovelPrompt` asks for `*narration*` and
allows a cue tag before a later part. `DirectorPrompt` adds it only while the new UI is on;
with it off the prompt is byte-for-byte as on main (tested).

**C7. Parser tolerance.** A run of asterisks (`*` or `**`) toggles narration; a line break
ends a line and also ends narration whose closing asterisk is missing; unmarked text is
speech; a half-written tag is held back while streaming; at most 64 lines per reply (the rest
join the last line, nothing is dropped).

**C8. Replies that were silent on main stay silent.** Screen remarks added without speech
(fast and automatic looks) are shown as lines but not voiced, as on main.

**C9. Not tested end to end in the player.** Sending a message from the built player would
write to the real chat history and memories on this PC, so the overnight check only opened
the new chat with `-newui` (screenshot: renders, no exceptions). The playback logic is
covered by EditMode tests; the morning checklist has the manual chat test.

**C10. Mode remembered per PC.** Auto/Step is a UI preference in PlayerPrefs
(`happi.vn-mode`, like the chat-open flag), not a settings-file key.

## PR D

**D1. Right-click only over her.** The overlay already decides "on a visible pixel of her"
(framebuffer alpha) before it toggles rotate mode. It now asks `AvatarContextMenu.TryOpen`
first; the new UI's handler opens the wheel. Empty areas stay click-through because the
overlay's rule is unchanged; the wheel adds a transparent raycast disk only under itself, so
only the wheel blocks clicks while it is open. With New UI off there is no handler and
right-click toggles rotate mode as on main.

**D2. Rotate mode stays reachable.** Shift+right-click on her still toggles rotate mode (Shift
is read with `GetAsyncKeyState`, because right-clicking her does not focus the window). While
rotate mode is on, a plain right-click leaves it as before. Esc and R work as before.

**D3. Slices are sprites, not a custom mesh.** A first version drew slices with a custom
`MaskableGraphic` mesh; its geometry tested correct but it did not render properly in the
transparent player window, and that could not be debugged overnight. Slices are now ordinary
uGUI Images from code-generated sprites (one fill/border/glow set per wheel shape, rotated per
slice, cached by size). The coverage maths is unit-tested and the look was checked by
screenshot: gaps, outline, hover pop, glow and dark hover text as in the mockup.

**D4. Placement.** Above her head (2D video head point or the humanoid head bone, the same
sources as the expression portrait), clamped inside the overlay window, which keeps it on
screen at monitor edges because the window itself is kept inside the work area. When her head
is near the top of the window the wheel overlaps her head instead of leaving the window. It
re-places every frame, so it follows her. The wheel scale is the DPI scale, reduced when the
window is too small for it.

**D5. What each slice does.** Talk opens the (new) chat. Watch my screen: on/off (same as Read
a window), Look now, Pick a monitor (only with whole-screen consent, B7), Screen settings.
Mood: her mood and why (B9) plus the mood-readout toggle. Persona: quick switch (up to 7 + More
→ settings). Do something: existing reactions/actions only (B8), paged. Leave me alone: Focus
mode (Pause), Mute/Unmute, Hide (tray brings her back; new `CompanionTray.HideCompanion`, same
as the tray item); while paused the slice reads **Come back** and unpauses. Memories: list with
two-step Forget. Settings: opens settings (old panel until PR E).

**D6. Closing.** Escape (the wheel focuses the window when it opens, so Esc reaches it), any
click outside the wheel (global mouse state, like the overlay's own drag code), the centre
button, or after an action. Esc steps back from a submenu or card first.

**D7. Greying out.** `WheelSliceGates.Rule` is the hook. Default rule: only "Do something" can
be refused, when she is annoyed or sleepy at 0.6 or more; clicking it shows her refusal. A rule
that throws fails open.

**D8. Body-part wheels.** `RadialWheelView` takes any slice count, a centre label and hint,
per-slice accent (pink for intimate), and draws a connector line to an anchor. JUH-101 plugs
in through `BodyPartWheels.HitZones` and `BodyPartWheels.Register`; nothing is registered, so
right-click always opens the main wheel now and no touch reactions were added. Slices marked
`RequiresIntimateGate` are left out unless `IntimateExpressions.Active`.

## PR E

**E1. Same tabs, one more.** The new settings view keeps the old tabs (AI, Avatar, Voice,
Controls, Overlay, Privacy, Persona, Screen) with the same items and wording, plus a "Happi"
tab for the new UI's own options (New UI, Voice narration, Step mode, wheel help). A coverage
test fails if any `CompanionSettings` field has no control in the view (exceptions are listed
in the test with where they are set instead).

**E2. Classic settings stay one click away.** The header has "Classic settings", which opens
the old panel even while the new UI is on. This is the safety net for anything the new view
misses, and for items that land on main after this PR (for example #63's Private endpoint
section on the AI tab, which the new view does not have yet).

**E3. Opening.** F12, the tray "Settings" item and the wheel all call `SettingsPanel.Open()`;
while the new UI is on, a new `OpenOverride` hook (one added line in `Open()`) shows the new
view instead. F12 toggles; the tray and the wheel only open.

**E4. Same actions as the old tabs.** API key save/remove/test, chat history, clear, wipe,
export, persona edit/create/delete, reset rotation and run setup go through small host
methods in `SettingsPanel.Presenters.cs` that call the old panel's existing private methods,
so messages, confirmations and file handling are identical. Clear and Wipe keep the two-step
confirm (`PrivacyConfirm`).

**E5. 18+ condition unchanged.** "Intimate expressions (18+)" is listed on the Avatar tab
exactly as on the old tab (always listed, with the same note; it only takes effect with
Private mode and the adult folder, enforced by `IntimateExpressions.Configure` as before).

**E6. Setup wizard not rebuilt.** There was not enough time for a themed wizard. "Run setup
again" (and first launch) open the existing wizard, which works with the new UI on.

**E7. Wheel acts on release.** Slices act when the mouse button is released over the same
slice it was pressed on. Acting on press let the release land on the panel a slice had just
opened (it selected a settings tab by accident in testing).

## Follow-up after the user's test (2026-11-13)

**F1. Debug text hidden in both UIs.** The top-left text (topmost, fps, emotion, key hints,
rotate/size/zoom) is drawn only with the new setting **Show debug info** (Overlay tab in both
settings views, key `showDebugInfo`, default off). While hidden its area clicks through like any
empty pixel. The hotkeys are unchanged. The **mood readout** stays on its own existing switch and
moves to the top-left corner when the debug text is hidden (below it when shown).

**F2. Movable chat box, saved as a fraction of the window.** The box centre is stored as 0..1 of
the overlay window (`chatBoxX`, `chatBoxY`, default -1 = bottom centre), so it survives window
resizes. It is always clamped to the part of the window that lies on its monitor's work area. The
overlay's own work-area lookup (`SPI_GETWORKAREA`) only knows the primary monitor, so the chat
box asks Windows for the monitor under the window (`MonitorFromPoint` + `GetMonitorInfo`): it
works on both monitors, and a spot saved on another monitor or screen size snaps back on screen.

**F3. Dragging.** Only the header bar starts a drag; the header buttons sit on top of it and still
click. The drag follows the global cursor and button state (like the wheel), so a fast drag that
leaves the box keeps going, and nothing else receives the press: no message is sent, no line is
advanced, the overlay's click-through elsewhere is unchanged. The position is saved on release
with a save-only path (`SettingsPanel.SaveLayoutSetting`), because a full apply rebuilds the voice
provider (Kokoro would reload its model).

**F4. Spoken-text glow timing: clip duration, not engine timestamps.** In this app none of the
voices return word or phoneme timestamps: the xAI TTS request returns audio only, and the Piper and
Kokoro processes return WAV audio. So the sweep uses each clip's real playback position
(`ISpeechProgress` on `VoiceSpeaker`: the sentence a clip says, its length, and how far it has
played). Within a clip the time is spread over that sentence's words by letter count, with short
pauses after commas (2 letters' worth) and sentence ends (4). A line said in several clips continues
clip by clip. Before the first clip plays, a fixed pace of 0.07 s per letter is used.

**F5. Glow rendering.** The current line's characters get their TMP vertex colours blended toward a
warm light (`#FFE3A8`, `HappiTheme.SpokenGlowColor`) while the word is said, then fade back over
0.3 s. TMP's material glow is not used because it would light every character in the box. No
per-letter objects, no particles, no per-frame allocations (arrays are made once per line).
Settings > Happi: **Spoken text glow** (on by default) and an intensity slider (subtle → strong),
both saved without a full apply. Colours, fade time, pauses and pace are in `HappiTheme`.

**F6. When the glow stops.** Narration glows only when Voice narration is on (only voiced lines
glow). Skip, Show all, Stop and a new line re-render the text, so it is back to normal at once.
When speech stops (pause, mute, end of line) the words already lit fade out over 0.3 s.

**F7. Daytime checks without input.** The overnight checks drove the mouse; with the user at the
PC the follow-up did not move the mouse or capture the screen. Dragging is covered by
`ChatBoxPlacementTests` and is on the manual checklist.

## Second follow-up (2026-11-13): glow, cursor, free chat box, snap spots

**F8. Stronger glow.** The lit words now turn a saturated warm gold (`#FFD166`, was a pale
cream) and grow 14% while they are said (glyph quads scaled around their centre, same vertex pass,
still no allocations). The intensity slider now maps to 0.55–1 (was 0.25–1), so the default 0.6
gives 0.82. All values are in `HappiTheme` (SpokenGlowColor, SpokenGlowMinStrength, SpokenGlowPop).

**F9. Text cursor.** TMP's caret is set explicitly: white, 2 px (more at high DPI), blinking at
0.85 s. The field also gets a light-blue ring while it has the keyboard, so it is obvious which
field is active even between blinks. Every new-UI text field (chat and settings) has both.

**F10. Free chat box: the window gets room beside her.** Unity has one window, so the chat box
cannot be its own OS window. While the new UI is on, the overlay window gets transparent,
click-through room on both sides of her (one box width + gaps, about 364 px each at 100%):
`OverlayWindow.SetSideRoom`. Her own placement, anchor, dragging and walking use the window
without that room (`DesktopPresence` subtracts it), so she does not move and still walks to the
screen edges; the room may hang off the screen. The box is a fixed 340 px wide instead of the whole
window. It is placed relative to her head, so it travels with her when she walks or is dragged
(like a speech panel), and it is always clamped to the part of the window that is on screen.
The debug text, the mood readout and the wheel shift or clamp with the room. With the new UI off,
or while the classic settings or the setup wizard is open (they sit at the window's left edge),
the room is 0 and the window is exactly as on main. A fully independent box (staying put while
she walks) would need the overlay to cover the whole screen; that is a larger change, not done.

**F11. Snap spots.** Dragging shows four ghost boxes numbered 1–4: 1 left of her head, 2 right
of her head, 3 beside her on the left, 4 beside her on the right. Letting go within about 90 px of
one snaps there; otherwise the box stays where it was dropped (saved as an offset from her head).
For 3 and 4 the box is centred at navel height (0.62 of the way from her feet to her head centre),
but its bottom never goes below her feet, so a tall box sits with its bottom level with her feet.
Spot 2 sends the 2D expression portrait to her left (the portrait also keeps clear of the box at
any spot). When a spot's side has no room on screen (she stands at an edge), the box uses the
mirrored spot on her other side. Default: spot 4. Her head and feet come from the 2D video's idle
rectangle or the 3D humanoid bones. New keys: `chatBoxSnap`, `chatBoxOffsetX`, `chatBoxOffsetY`;
`chatBoxX`/`chatBoxY` (window fractions, from the first follow-up) are still read and written but
no longer used.
