# Morning report: JUH-104 UI overhaul + JUH-108 visual-novel chat (overnight 2026-11-12 → 13)

Good morning. Everything below was built and checked alone overnight; nothing was merged and
no Linear issue was moved. The new UI is **off by default**. Turn it on with Settings → Overlay →
**New UI (preview)**, or launch with `-newui`.

**Follow-up after your test (2026-11-13, on #73):** the new chat box can be dragged by its top bar
(position saved, kept on screen on either monitor, "Reset chat position" in Settings → Happi); the
old top-left debug text is gone from both UIs unless Settings → Overlay → "Show debug info" is on
(the mood readout keeps its own switch); spoken lines light up word by word ("Spoken text glow",
on by default, with an intensity slider). Decisions F1–F7, checklist items 6.1-1/2 and 6.2-12…14.

**Third follow-up (2026-11-13, on #73):** the blinking cursor really shows now (F12); the chat
box has a fifth spot under her, so she stands on it, and that is the default with the 2D avatar
(F13); the 2D portrait can be dragged and docks left or right of the chat box at the box's height
(F14). Checklist items 6.2-12, 15 and 16.

## 1. Pull requests (stacked, all against `main`, merge in this order)

| # | PR | Branch | CI | Notes |
| --- | --- | --- | --- | --- |
| 1 | [#69](https://github.com/juhaninvaliaikaps-cloud/ai-companion/pull/69) A: inventory, design docs, Happi theme, TMP resources | `claude/juh-104-ui-inventory` | **red (infrastructure)** | EditMode job killed by the runner (exit 137, out of memory) at Unity start-up, 3 attempts, before any project code compiled. The Windows player compile job passed. #70–#73 contain this exact commit and pass. Re-run once more before merging. |
| 2 | [#70](https://github.com/juhaninvaliaikaps-cloud/ai-companion/pull/70) B: presenters, no visual change | `claude/juh-104-presenters` | green | Depends on #69 |
| 3 | [#71](https://github.com/juhaninvaliaikaps-cloud/ai-companion/pull/71) C: new chat view + JUH-108 | `claude/juh-104-new-chat` | green | Depends on #70 |
| 4 | [#72](https://github.com/juhaninvaliaikaps-cloud/ai-companion/pull/72) D: radial wheel + body-part wheel | `claude/juh-104-radial-wheel` | green | Depends on #71 |
| 5 | [#73](https://github.com/juhaninvaliaikaps-cloud/ai-companion/pull/73) E: new settings view | `claude/juh-104-new-settings` | see the PR | Depends on #72. This report is committed here. |

Bugbot skipped all five ("too large": about 65,000 lines, almost all of it the TMP Essential
Resources in PR A, which every stacked PR repeats against `main`). Once #69 is merged, a push or a
"bugbot run" comment on #70–#73 should give real reviews.

#63 and #68 should merge first (as planned). Expected overlap: #63 edits `ChatPanel.cs` near
lines this stack also touches (one-word `partial`, Escape/labels/mute/send/look call sites,
`BindSpeech`, `ApplyCue`, `InteractiveRect`) and `SettingsPanel.cs` (`Open`, Overlay tab). These
are small, separate hunks; conflicts, if any, should be trivial. #68 only touches
`SettingsPanel.Persona.cs` among the files here, and that file is not changed by this stack.

## 2. What is done

- **PR A** — `INVENTORY.md` (every panel, control, setting, hotkey, wizard step, tray item, debug
  action and privacy gate on `main`, with file/method and an ID); `DESIGN.md` + `radial_mockup.png`;
  `DECISIONS.md`; `HappiTheme` (approved palette, fonts, sizes, radii, spacing, timings, DPI
  scaling) in a new `AICompanion.NewUI` assembly; TMP Essential Resources.
- **PR B** — presenters: `ChatPresenter` (Send/Stop, Mute, Look, Escape, labels), `SettingsPresenter`
  (read/write, provider switch, click-through), `PersonaPresenter`, `PrivacyConfirm` +
  `LocalDataPresenter`, `ScreenLookPresenter`, `VideoAvatarPresenter`, `MoodPresenter`,
  `MemoryPresenter`. The old ChatPanel and Settings tabs call them where it does not collide with
  #63/#68. New settings keys `newUiPreview`, `voiceNarration` (default off). `-newui`.
- **PR C / JUH-108** — new visual-novel chat box: tolerant narration/speech parser, current line
  highlighted, Auto/Step, Next/click/Space, Skip, Show all, Voice narration (off), TTS prefetch via a
  playback hold, cues at their own line, History with one text object per message (100 / 6000).
- **PR D** — main radial wheel above her head (8 fixed slices, approved look), follows her, stays in
  the window, closes on Esc / click outside / after an action, Shift+right-click still rotates;
  reusable body-part wheel component + JUH-101 hook (nothing registered, no new touch reactions).
- **PR E** — new settings view with every settings item, a Happi tab, "Classic settings" button.

Tests: 90 new EditMode tests. Local batch run on PR E: **1013 / 1014 passed**; the one failure is
the known `SpeechToTextTests.NativeLoad_ListsEditorAndPlayerPluginFolders` (TESTING.md).

## 3. What is not done

- **Setup wizard** in the new look (PR E "if time allows"): not built. The existing wizard is used.
- **A real chat round trip in the new chat was not tried overnight**: sending a message from the
  built player would have written to your real chat history and memories. The playback logic is
  covered by tests; please try it (checklist N-02).
- **Body-part wheels** only exist as a component and hook (as asked). No zone or slice set is registered.
- **#63's Private endpoint section** is not in the new settings view (it is not on `main` yet). Until
  it is added, use "Classic settings" for it.
- **"She glances at the cursor when the wheel opens"** (Linear comment): not done (not in the addendum).

## 4. Decisions

All with reasons in [`DECISIONS.md`](DECISIONS.md). Short list:

- A1 mockup copied from the image you attached (the `E:\LOOT` path does not exist). A2 uGUI + TMP
  (click-through relies on uGUI raycasts). A3 TMP Essential Resources committed. A4 theme is a C#
  class. A5 DPI scaling via the theme (the overlay forces CanvasScaler to 1). A6 new code under
  `Assets/_Project/Scripts/UI/NewUI/`. A7 "Happi" in new headers; wheel centre shows her name.
  A8 right-click opens the wheel, Shift+right-click rotates. A9 duplicate Piper fields shown once.
- B1 chat sending stays in ChatPanel (#63 rewrites it). B2 presenter placement by assembly. B3
  SettingsPanel is the single settings host. B4 which old call sites changed (Persona tab untouched).
  B5 new keys appended. B6 switch set in `SettingsApplicator`. B7 the wheel never grants screen
  consent. B8 "Do something" = existing actions only. B9 simple mood "why".
- C1 bottom dialogue box. C2 ChatPanel stays the engine. C3 every line spoken with ChatPanel's
  privacy origin; narration never sent to TTS while Voice narration is off. C4 prefetch through a
  playback hold. C5 cues per line, end-of-reply cue skipped. C6 prompt line only with New UI on.
  C7 parser tolerance. C8 silent screen remarks stay silent. C9 no live chat test overnight. C10
  Auto/Step stored in PlayerPrefs.
- D1 right-click only over her. D2 rotate mode reachable. D3 slices are sprites (custom mesh did not
  render in the transparent window). D4 placement/clamping. D5 slice actions. D6 closing. D7 greying
  hook. D8 body-part wheel hook.
- E1 same tabs + Happi tab, coverage test. E2 Classic settings button. E3 opening via `OpenOverride`.
  E4 same actions via host methods. E5 18+ unchanged. E6 wizard not rebuilt. E7 wheel acts on release.

## 5. Known issues

- PR A's CI EditMode job: runner OOM at start-up (see table). Not a code failure.
- The wheel is placed above her head but clamped inside the overlay window. With her head near the
  top of the window it overlaps her head (and, with "Show debug info" on, the debug text can cover the Settings slice).
- The chat box opens over her legs (visual-novel layout); since the follow-up you can drag it elsewhere. F10, ×, the tray and double-click work as before.
- The header name showed "Frie" on this PC; that looks like the stored companion or persona name,
  not a cut (no ellipsis), but check it.
- TMP logs once: "The character used for Ellipsis is not available in font asset … Switching Text
  Overflow mode to Truncate." Harmless; long labels are cut instead of getting "…".
- Bugbot could not review (size); see section 1.
- The local build modifies `UniversalRenderPipelineAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset`,
  `GraphicsSettings.asset` and creates `DefaultVolumeProfile.asset` in the worktree. They are not
  committed. Do not commit them.

## 6. Manual test checklist

IDs refer to [`INVENTORY.md`](INVENTORY.md). Build the combined branch first (section 7).

### 6.1 Old UI (New UI off: start **without** `-newui`, setting off)

1. [ ] P-01: window transparent, empty areas click through to the desktop. P-02: **no debug text top-left** by default; Settings → Overlay → "Show debug info" (S-OV-10) shows it again, off hides it. P-09: with "Show mood readout" on, only the mood readout shows, in the top-left corner.
2. [ ] K-10: right-click on her toggles rotate mode (drag to rotate; "Rotate: on" shows only with debug info on); R resets (K-11); Esc leaves.
3. [ ] K-01 F6 mute, K-02 F7 avatar, K-03 F8 topmost, K-04 F9 expression, K-05 F10 chat, K-06 F12 settings.
4. [ ] K-16/K-17 scroll zoom and Ctrl+scroll size over her; K-18 drag moves the window; K-19 double-click opens chat.
5. [ ] C-02…C-08: Mute/Muted, Send/Stop, What do you see?, Mic hold, Enter sends, API key Save: labels and behaviour as before.
6. [ ] S-OV-03: Click-through button cycles silhouette → panels only → capture the window, with the same help text.
7. [ ] S-AV-11: the eight "Test: …" reaction buttons (2D video) and their status messages.
8. [ ] S-PR-05/06: export folder and zip — same messages ("Choose a folder…", "Export did not run…", "Exported N file(s).").
9. [ ] S-OV-09: the new "New UI (preview)" toggle is on the Overlay tab, off. Everything else on all tabs unchanged.
10. [ ] T-01…T-07 tray menu; W-01…W-07 "Run setup again" wizard.
11. [ ] G-01…G-08: Private mode keeps chat and voice local; 18+ toggle unchanged.

### 6.2 New UI (setting on, or start with `-newui`)

1. [ ] N-01: turn **New UI (preview)** on in Overlay → the chat becomes the blue Happi box at the bottom. Turn it off in Settings → Happi → the classic chat returns at once.
2. [ ] C-01…C-08 in the new box: name + model badge (green local / amber cloud), History, Mute, What do you see?, ×, Mic hold, Message + Send/Stop, API key row (shown when no key is saved or in History).
3. [ ] N-02 (JUH-108): send a message that gets a reply with `*actions*` and speech.
   - [ ] lines appear one by one, narration in italics, the current line highlighted
   - [ ] narration is **not** spoken (Voice narration off); speech is
   - [ ] **Auto** plays through; switch to **Step**: Next, a click on the dialogue, or Space advance exactly one line
   - [ ] in Step mode the next spoken line starts immediately after Next (prefetch)
   - [ ] her face changes at the line where a cue tag appears, not at the start
   - [ ] **Skip** and **Show all** work; Esc / Stop stop the voice
   - [ ] Settings → Happi → Voice narration on → narration is spoken too
4. [ ] C-05: History shows the full transcript; long chats stay fast (no "65000" in Player.log).
5. [ ] N-04 wheel: right-click on her → wheel above her head, 8 slices in order Talk, Watch my screen, Mood, Persona, Do something, Leave me alone, Memories, Settings. Hover = pale blue, dark text, pops out.
   - [ ] Talk opens the chat; Mood shows her mood and the readout toggle; Persona switches (chat history follows the persona)
   - [ ] Do something: 2D shows the loaded reactions + walk/stop/face me; 3D shows the five emotions
   - [ ] Leave me alone → Focus mode pauses (slice then reads **Come back**), Mute, Hide (tray brings her back)
   - [ ] Memories: list, Forget asks first; Watch my screen: on/off, Look now, Pick a monitor (only with whole-screen consent), Screen settings
   - [ ] Esc, a click outside, the centre button and finishing an action close it; it follows her when she walks
   - [ ] Shift+right-click still toggles rotate mode; right-click on empty space still clicks through (K-10, P-01)
6. [ ] N-05: no body-part wheel appears (nothing registered yet).
7. [ ] S-* in the new settings (F12, tray Settings, wheel Settings): every tab, every item works as in 6.1; "Classic settings" opens the old panel; F12 toggles; Esc closes.
8. [ ] S-AV-17: "Intimate expressions (18+)" listed as before; intimate content still only with Private mode + adult folder (G-01…G-04).
9. [ ] G-06: with Play clip sounds on, clip sounds never play over her voice, also while a line is prefetched.
10. [ ] DPI: at 150% and 200% Windows scaling, text in the box, wheel and settings stays readable and fits.
11. [ ] K-01…K-09, K-13, K-16…K-20: hotkeys unchanged with the new UI on (and with the debug text hidden).
12. [ ] N-06 movable chat box: drag it by the top bar (name / badge area) anywhere around her, also fully beside her; four numbered ghost spots appear while dragging; let go near 1 (left of her head), 2 (right of her head: the portrait moves to her left), 3 (left of her body, navel height) or 4 (right) and it snaps there; let go elsewhere and it stays there; the header buttons still click; releasing does not send a message or advance a line; empty space still clicks through; the box follows her when she walks; at a screen edge a spot without room flips to her other side; restart → same place; other monitor → still visible; Settings → Happi → "Reset chat position" puts it back at the default (spot 5 under her in 2D, spot 4 in 3D). Spot 5 (2D only): she stands on top of the box and walks along on it; the 3D avatar never offers spot 5.
13. [ ] N-07 spoken-text glow: while she speaks a line, its words turn warm gold and grow slightly one by one, clearly visible, and fade back about 0.3 s later; narration does not glow unless Voice narration is on; Skip / Show all / Stop / Esc return the text to normal at once; Settings → Happi → "Spoken text glow" off stops it; the intensity slider goes from subtle to strong.
14. [ ] P-02 / S-OV-10 in the new settings: Overlay → "Show debug info" toggles the debug text.
15. [ ] N-08: click into the chat message field → a blinking text cursor and a light-blue ring show; same in the settings fields.
16. [ ] N-09 movable portrait (2D): drag the portrait; two ghost spots show left and right of the chat box; let go near one → it docks there with its top and bottom level with the box's; move the box → the portrait follows; let go elsewhere → it stays there and follows her when she walks; restart → same place; hide the chat → the portrait goes back beside her head; Settings → Happi → "Reset portrait position" → automatic again.

## 7. PowerShell 5.1 commands

Each command stands alone.

Make a combined local test branch (the top of the stack contains A–E):

```powershell
Set-Location 'E:\worktrees\juh-104'; git fetch origin; git switch -C test/juh-104-ui-overhaul origin/claude/juh-104-new-settings; git log --oneline -6
```

Build it (batchmode, waits until done, prints the result line):

```powershell
Set-Location 'E:\worktrees\juh-104'; Start-Process -Wait -FilePath 'E:\Unity Editor\6000.6.6f1\Editor\Unity.exe' -ArgumentList '-batchmode','-quit','-projectPath','E:\worktrees\juh-104','-buildTarget','Win64','-buildWindows64Player','E:\worktrees\juh-104\Builds\UIOverhaul\BackseatCatgirl.exe','-logFile','E:\worktrees\juh-104\Builds\UIOverhaul\build.log'; Select-String -Path 'E:\worktrees\juh-104\Builds\UIOverhaul\build.log' -Pattern 'Build Finished|error CS' | Select-Object -Last 5
```

Start the app with the new UI (drop `-newui` to test the old UI):

```powershell
Set-Location 'E:\worktrees\juh-104\Builds\UIOverhaul'; Start-Process -FilePath '.\BackseatCatgirl.exe' -ArgumentList '-newui'
```

Read the log (exceptions, the 65000-vertex error, new-UI and wheel lines, last 60 lines):

```powershell
Set-Location $env:USERPROFILE; Get-ChildItem -Path 'AppData\LocalLow\AI Companion\Backseat Catgirl\Player.log' -ErrorAction SilentlyContinue | ForEach-Object { $lines = Get-Content -Encoding UTF8 $_.FullName; $lines | Select-String -Pattern 'Exception|65000|NullReference|New UI|Wheel:|Chat line cue' | Select-Object -First 40 | ForEach-Object { $_.Line }; '--- last 60 lines ---'; $lines | Select-Object -Last 60 }
```

Notes: the worktree has the uncommitted Unity graphics files mentioned in section 5; `git switch`
keeps them. Close the app before building. Before a build, TESTING.md suggests copying
`Assets/_Project/Settings/AvatarSelection.asset` aside if you have a local avatar path in it.
