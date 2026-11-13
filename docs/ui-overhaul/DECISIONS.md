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
