# Her mood (JUH-103)

Leia has a lasting mood that changes slowly with the conversation. This is **phase 1**: the mood is kept, fades, is saved per persona, and can be watched. It does **not** pick expressions or reactions yet; that is phase 2. Persona temperaments are phase 3.

The approved design is the comment "Approved mood design (2026-11-12)" on JUH-103.

## What is verified

| Area | Status |
| --- | --- |
| Moods, decay, hysteresis, the Intimate gate, the input table, the mood file | Automated EditMode tests (`CompanionMoodTests`) |
| Encrypted per-persona file, round trip | Automated EditMode test with a test key |
| The keeper in the app, the readout, the log lines, saving on quit | **Not run in a player** |

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
| loving, affectionate / kiss | affectionate 0.3 / 0.35 |
| laughing / excited / playful | happy 0.35 / 0.3 / 0.25 |
| crying / hurt | sad 0.45 / 0.35 |
| scared / shocked / worried, nervous | anxious 0.35 / 0.3 / 0.25 |
| sleepy / tired | sleepy 0.25 / 0.2 |
| seductive | intimate 0.3 (gated), affectionate 0.1 |

Otherwise the emotion: happy → happy 0.25, sad → sad 0.3, surprised → anxious 0.1, embarrassed → flustered 0.3, shy → flustered 0.2 + affectionate 0.1, flustered → flustered 0.35, concerned → anxious 0.2 + sad 0.1. Neutral and thinking move nothing.

**The user's detected mood** (JUH-21, local rules, each message): happy → happy 0.2, sad → sad 0.15, angry → annoyed 0.15, anxious → anxious 0.15, affectionate → affectionate 0.25. Neutral, surprised and confused move nothing.

**Hooks only, no effect yet:** `CompanionMoodInputs.ReportTouch(Pat / Poke / Other)` for JUH-61 touches, and `ReportIgnored(seconds)` for being ignored.

## Saved per persona

`%APPDATA%\ProjectLeia\mood.json` for the default persona, `mood-<persona>.json` for the others (the same file-name rules as memory). Encrypted like memory (see [LOCAL_STORAGE.md](LOCAL_STORAGE.md)), and included in export and wipe.

- Saved every 60 seconds, when the persona changes, and when the app quits.
- On start, and when you switch persona, the saved mood is loaded and faded for the time the app was closed (at most a week). A clock that went backwards adds nothing. A missing or unreadable file starts at Content.
- Intimate is not saved.

## Watching it

Settings → **Overlay** → **Show mood readout (debug)**. Off by default. A small box at the top left shows the mood and the three strongest moods, for example:

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
