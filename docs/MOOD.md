# Her mood (JUH-103)

Leia has a lasting mood that changes slowly with the conversation. **Phase 1** keeps the mood: it fades, is saved per persona, and can be watched. **Phase 2** lets the mood colour what she shows (see "What she shows"), without overriding a reply's own cue. **Phase 3** gives each persona a temperament that shapes how her moods build and fade (see "Temperaments").

The approved design is the comment "Approved mood design (2026-11-12)" on JUH-103.

## What is verified

| Area | Status |
| --- | --- |
| Moods, decay, hysteresis, the Intimate gate, the input table, the mood file | Automated EditMode tests (`CompanionMoodTests`) |
| Encrypted per-persona file, round trip | Automated EditMode test with a test key |
| The keeper in the app, the readout, the log lines, saving on quit | **Not run in a player** |
| Phase 2: the look table, cue softening, the prompt line, the 2D idle clip, the Intimate gate | Automated EditMode tests (`CompanionMoodLookTests`) |
| Phase 2: the rest face on the 3D avatar and portrait, the 2D idle clip on screen | **Not run in a player** |
| Phase 3: presets, default = phase 1/2, gains, half-lives, floors, Intimate gate, persona file and switch | Automated EditMode tests (`PersonaTemperamentTests`) |
| Phase 3: the picker in persona settings, the readout name, the log line | **Not run in a player** |

## Moods

Content is the baseline. The other eight each have a strength from 0 to 1. Only the strongest one shows.

`happy`, `affectionate`, `flustered`, `annoyed`, `sad`, `anxious`, `sleepy`, `intimate`.

- **Showing one mood.** A mood shows when its strength reaches 0.3 (`EnterStrength`). A different mood replaces it only when it is stronger by 0.1 (`SwitchMargin`). The shown mood stays until it falls below 0.15 (`LeaveStrength`), then the strongest mood above 0.3 shows, or Content. So two close moods do not flicker.
- **Pushes add up.** Each push covers its share of the distance left to 1: `strength += amount * (1 - strength)`. Repeated small pushes build up; nothing passes 1.
- **Fading.** Every mood halves on its own clock while nothing feeds it: Happy 3 min, Flustered 4 min, Affectionate and Anxious 7 min, Intimate 10 min, Annoyed 15 min, Sad 20 min, Sleepy 30 min.
- **Sleepy at night.** By the local clock, Sleepy is pulled toward a night level: 0 before 22:00, rising to 0.6 at 01:00, held until 05:00, then fading again. It takes about 20 minutes to cover two thirds of the way, so it grows slowly.
- **Intimate.** It can only gain strength while `IntimateExpressions.Active` (the Intimate expressions 18+ toggle, Private mode, and the portraits-adult folder). Otherwise it stays 0, is never shown, is not in the readout or the log, and is never written to disk. Turning the gate off clears it at once.

All the numbers are in `CompanionMoodTuning` (`Assets/_Project/Scripts/Core/CompanionMoodState.cs`).

## What moves it

One table, `CompanionMoodTable` (`Assets/_Project/Scripts/Core/CompanionMoodTable.cs`). A push is `gain * (0.5 + intensity)`, so a cue at intensity 1 moves 1.5 times the gain and one at 0 half of it.

**Her reply's cue.** The emotion word as the model wrote it comes first, so richer words count:

| Word | Mood (gain) |
| --- | --- |
| pout, pouty, annoyed, grumpy | annoyed 0.3 / 0.25 |
| angry | annoyed 0.35 |
| tsundere | annoyed 0.25, flustered 0.1 |
| smug | happy 0.2 |
| loving, affectionate, love / kiss | affectionate 0.3 / 0.35 |
| laughing / excited / playful / smug_wink | happy 0.35 / 0.3 / 0.25 / 0.2 |
| crying / hurt | sad 0.45 / 0.35 |
| scared, afraid, fear / shocked, shock / worried, worry, nervous | anxious 0.35 / 0.3 / 0.25 |
| embarrassed_worried | flustered 0.2, anxious 0.1 |
| sleepy / tired / bored | sleepy 0.25 / 0.2 / 0.15 |
| seductive, flirty | intimate 0.3 (gated), affectionate 0.1 |
| distressed | anxious 0.3, sad 0.15 |
| anxious | anxious 0.3 |
| upset | sad 0.25, anxious 0.1 |
| teary_happy | happy 0.25, affectionate 0.1 |

JUH-113: `distressed`, `anxious`, `nervous` and `upset` now also read as the concerned face (before, the whole tag fell back to neutral). `hurt` and `grumpy` read as sad; `playful` and `affectionate` read as happy. As a gesture or reaction word, `teary_happy` plays the `happy` clip, and `distressed`, `anxious` and `upset` play `concerned`.

Otherwise the emotion: happy → happy 0.25, sad → sad 0.3, surprised → anxious 0.1, embarrassed → flustered 0.3, shy → flustered 0.2 + affectionate 0.1, flustered → flustered 0.35, concerned → anxious 0.2 + sad 0.1. Neutral and thinking move nothing.

Her cue counts once per chat turn. The screen-look remarks, which reuse the last reply's cue, do not push it again.

**The user's detected mood** (JUH-21, local rules, each message): happy → happy 0.2, sad → sad 0.15, angry → annoyed 0.15, anxious → anxious 0.15, affectionate → affectionate 0.25. Neutral, surprised and confused move nothing.

**Hooks only, no effect yet:** `CompanionMoodInputs.ReportTouch(Pat / Poke / Other)` for JUH-61 touches, and `ReportIgnored(seconds)` for being ignored.

## What she shows (phase 2)

One table, `CompanionMoodLooks` (`Assets/_Project/Scripts/Core/CompanionMoodLooks.cs`). The keeper publishes the shown mood to `CompanionMoodNow`; the face, portrait, 2D clips, director and prompt read it from there.

**Rest look.** While no reply expression is held (before the first reply, after the hold releases, and for neutral or unreadable cues), she shows her mood:

| Mood | Portrait | 3D face | 2D idle clip |
| --- | --- | --- | --- |
| content | neutral | neutral | none |
| happy | smile | happy 0.35 | happy |
| affectionate | smile | happy 0.25 | none |
| flustered | shy | shy 0.3 | none |
| annoyed | pouty | sad 0.25 | none (pout has sound) |
| sad | concerned | concerned 0.35 | concerned |
| anxious | embarrassed_worried | concerned 0.3 | concerned |
| sleepy | sleepy | neutral (no sleepy 3D pose) | none (no sleepy clip) |
| intimate (gated) | shy | shy 0.3 | none |

The 3D face intensity is 60 % of the table value at strength 0 and 100 % at strength 1. The body emotion stays neutral, so the 2D avatar does not play an emotion reaction because of the rest look.

**2D idle clip.** When the mood names a clip that the folder has as a silent reaction (not a held pose), it stands in for half of the idle variations (`VideoAvatarDirector.MoodVariationShare`), never twice in a row while there is a plain variation. With no plain variation it is used every time the variation timer fires.

**Reply cues win.** A reply's cue always sets the face. The mood only picks a softer face in the same family, at 80 % intensity:

| Mood | Cue face | Shown |
| --- | --- | --- |
| sad | happy, excited, laughing, smug | smile |
| anxious, sleepy | happy, excited, laughing | smile |
| annoyed | happy / loving | smug / tsundere |
| happy | sad | concerned |
| affectionate | angry | pouty |
| flustered | happy | shy |

Gestures, reactions and every other face are played as written.

**Prompt.** One line goes right after the persona text: `Your current mood: annoyed, mild. Let it colour your tone a little; the conversation still decides what you say.` The degree is mild below 0.5, moderate below 0.75, then strong. Content says `Your current mood: content.`

**Intimate.** While `IntimateExpressions.Active` is false, Intimate counts as content everywhere: no look, no softening, and it is never named in the prompt. The rest look never uses a `finger_*` portrait.

**Log.** `Mood expression: <mood> → <expression or clip> (<where>)`. Where is one of:
- `rest face`: logged once each time the rest look changes.
- `softened <cue> cue`
- `neutral cue`
- `2D idle clip`

## Temperaments (phase 3)

A temperament adds no new moods. It changes four things, all in `PersonaTemperament` (`Assets/_Project/Scripts/Core/PersonaTemperament.cs`):

- **Gain**: multiplies every push into a mood (in `CompanionMoodTable`).
- **Half-life**: multiplies how long a mood takes to halve (in `CompanionMoodState.Tick` and when the app was closed).
- **Baseline**: a mood she settles back to, held at 0.35. That is enough to show.
- **Rest bias**: a low floor under one mood, so that mood shows sooner. It is at most 0.12: below showing (0.3) and below leaving (0.15), so a biased mood can still stop showing.

A mood starts on its floor as soon as the temperament is set (load, switch, or picker), never fades below it, and is back on it after the app was closed. A Cheerful persona therefore rests on happy straight away. Hysteresis, the Intimate gate, the mood file and the phase 2 looks are unchanged.

| Preset | What it does |
| --- | --- |
| Balanced (default) | Exactly phase 1/2. |
| Calm | Annoyed and anxious build ×0.7, flustered ×0.8. Annoyed and anxious fade ×0.6 the time, sad ×0.8. |
| Cheerful | Baseline happy. Happy builds ×1.3 and lasts ×1.5. Affectionate builds ×1.1. Sad and annoyed build ×0.8 and fade ×0.7 the time. |
| Shy | Flustered bias 0.1. Flustered builds ×1.4 and lasts ×1.5. Anxious builds ×1.2. Affectionate builds ×0.9, annoyed ×0.8. |
| Tsundere | Annoyed bias 0.1. Annoyed builds ×1.4 and lasts ×1.3. Affectionate builds ×0.7 and fades ×0.7 the time. Flustered builds ×1.2. |
| Moody | Sad bias 0.1. Sad builds ×1.3 and lasts ×1.4. Annoyed builds ×1.2 and lasts ×1.2. Anxious builds ×1.2. Happy builds ×0.8 and fades ×0.7 the time. |

**Intimate** is never a baseline or a bias. A temperament's Intimate gain is used only while `IntimateExpressions.Active`. No preset touches Intimate.

**Where it is stored.** The persona file stores the preset id (`"temperament": "tsundere"`), never numbers. Custom persona files are encrypted as before.
- The built-ins: Leia (`friend`) is Cheerful; Rowan (`calm`) and Avery (`coach`) are Calm.
- The setup name, a custom persona and an unknown id get Balanced.
- In Settings → Persona, the **Temperament** buttons pick a preset. Picking one for a built-in saves a copy, like any other edit.

**Switching.** A persona switch loads that persona's mood and temperament. The keeper logs `Mood temperament: <persona> = <preset>` on load, on a switch, and when the picker changes it. The readout shows `Mood: <mood> (<Temperament>)`.

## Saved per persona

`%APPDATA%\ProjectLeia\mood.json` for the default persona, `mood-<persona>.json` for the others (the same file-name rules as memory). Encrypted like memory (see [LOCAL_STORAGE.md](LOCAL_STORAGE.md)), and included in export and wipe.

- Saved every 60 seconds, when the persona changes, and when the app quits.
- On start, and when you switch persona, the saved mood is loaded and faded for the time the app was closed (at most a week). A clock that went backwards adds nothing. A missing or unreadable file starts at Content.
- Intimate is not saved.

## Watching it

Settings → **Overlay** → **Show mood readout (debug)**. Off by default. A small box at the top left, just below the overlay's status box, shows the mood and the three strongest moods, for example:

```
Mood: annoyed
annoyed 0.52, sad 0.18, happy 0.05
```

`Player.log`:

- `Mood: content for persona lola (none)` when a persona's mood is loaded.
- `Mood: now annoyed after her cue (annoyed 0.52, sad 0.18)` whenever the shown mood changes. The cause is `her cue`, `your mood` or `time`.

## Code

- `Core/CompanionMood.cs`: the moods and their names.
- `Core/CompanionMoodState.cs`: strengths, showing one mood, fading, the night, the mood file; `CompanionMoodTuning`.
- `Core/CompanionMoodTable.cs`: the input table, `CompanionMoodInputs` (reports from the rest of the app), `CompanionTouch`.
- `Core/CompanionMoodStore.cs`: file names and the encrypted load and save.
- `Settings/CompanionMoodKeeper.cs`: the component in the app (added by `SettingsPanel`): loads per persona, ticks once a second, listens to the inputs, saves, draws the readout.
- `UI/ChatPanel.cs`: reports her reply's cue and the user's detected mood.

## Windows checklist (phase 1)

1. Settings → Overlay → turn on **Show mood readout (debug)**. The box shows `Mood: content` and `none`.
2. Chat in a way that makes her happy, then tease her, then say something sad. The readout and `Mood: now …` lines follow. A single mild reply should not switch the mood back and forth.
3. Leave her alone for 10 minutes: Happy fades first, Sad and Annoyed last.
4. Quit and start again: the mood is still there, a little weaker.
5. Switch persona: the other persona has its own mood. Switch back: the first one returns.
6. With Intimate expressions off (or Private mode off) Intimate never appears.

## Windows checklist (phase 2)

1. Turn on the mood readout. Say something sad until the readout shows `Mood: sad`. Once she stops talking, the portrait and the 3D face rest on `concerned`, and the log shows `Mood expression: sad → concerned (rest face)`.
2. Still sad, get her to reply with a happy cue. She shows a smile instead of the full happy face, and the log shows `Mood expression: sad → smile (softened happy cue)`.
3. A reply with a strong cue that has no softer variant, such as `surprised`, plays exactly as written.
4. In 2D video mode while Happy or Sad, some idle variations become the silent `happy` or `concerned` clip (log `… (2D idle clip)`). Clips with sound are never used for this.
5. After 22:00 Sleepy rises. The portrait rests on `sleepy`.
6. With Intimate expressions off, nothing ever logs `intimate`, and the prompt never names it.

## Windows checklist (phase 3)

1. Turn on the mood readout. With Leia (`friend`), the log shows `Mood temperament: friend = cheerful`, and the readout shows `(Cheerful)`. She rests on happy (smile) straight away, and again after a restart.
2. Switch to Rowan. The log shows `Mood temperament: calm = calm`.
3. Make a new persona. The readout shows `(Balanced)`. Under Settings → Persona, pick **Temperament: Tsundere**. The log shows `… = tsundere` at once. Teasing now makes her annoyed faster, and affection builds more slowly.
4. Quit and start again: the persona still has Tsundere.
5. With Intimate expressions off, `intimate` never appears in the readout or the log.
