# Pisara: the leaf-drop persona system

Pisara ("drop" in Finnish) is the working name for how a companion gets a consistent personality, a relationship with the user, and a life of her own. Linear: JUH-117 (umbrella), JUH-118 (Claude Code), JUH-119 (DeepSeek). It absorbs JUH-114 (temperament detection) and JUH-115 (relationship gauge).

This document is the contract between the agents. If you need something it does not cover, comment on JUH-117 instead of working around it.

## The picture

One chat turn is a drop. It falls through leaves in a fixed order, and each leaf colours it:

1. **Sheet**: who she is (read once from the persona prompt).
2. **Relationship**: how the user's message lands, given who they are to each other.
3. **Temperament**: how fast the resulting mood builds and fades.
4. **Mood**: her lasting state (JUH-103).
5. **Memory**: what she remembers, including her life before the user.
6. **Looks**: what she shows (JUH-103 phase 2).

## Two rules

1. **One reading, many leaves.** The model reads the persona prompt once and writes a character sheet. Every leaf reads from that sheet. No leaf has its own detector.
2. **The model judges meaning; code keeps the numbers.** The model decides what a message meant. Plain, tested C# decides what that does to her state over time.

## Status

| Part | State | Owner |
| --- | --- | --- |
| Sheet format, parser, clamps, prompt hash | **Built** (this PR), EditMode tests | Claude |
| Sheet store (encrypted, per persona, in wipe and export) | **Built** (this PR), EditMode tests | Claude |
| Temperament blend from sheet weights (JUH-114 step 4) | **Built** (this PR), EditMode tests | Claude |
| Reading prompt | **Built**, tuned against local 12B models (JUH-118 section 1) | Claude, tuned by Claude Code |
| The reading call, cache, Re-read, persona settings display | **Built** (JUH-118 section 1), EditMode tests | Claude Code |
| Relationship state, storage, readout | Planned, JUH-119 | DeepSeek |
| Temperament keyword scorer (offline fallback) | Planned, JUH-119 | DeepSeek |
| Relationship and landing math, the `landed` cue attribute, the relationship prompt line | **Built**, EditMode tests; not called from the app yet | Claude |
| Memory tags, promotion and fading | Planned, PR after that | Claude |
| Seeded memories, first scene, away line, landing wiring | Planned, JUH-118 sections 2 to 4 | Claude Code |

The spec PR changed nothing in how she behaves. Since JUH-118 section 1 the sheet is read and its temperament is used (see "The reading as built"). The relationship, landing and world parts of the sheet are read, cached and shown, but nothing acts on them yet.

## The character sheet

`CharacterSheet` (`Assets/_Project/Scripts/Core/CharacterSheet.cs`). One per persona, in `sheet-<persona>.json` next to the mood file, encrypted like it.

```json
{
  "version": 1,
  "promptHash": "<sha-256 of the persona prompt>",
  "source": "model",
  "createdAtUtc": "2026-11-13T20:00:00Z",
  "temperament": { "calm": 0.1, "cheerful": 0.2, "shy": 0.0, "tsundere": 0.9, "moody": 0.3, "reason": "sharp-tongued but caring" },
  "relationship": { "role": "family", "label": "stepsister", "affection": 25, "familiarity": 70, "sharedHistory": true, "reason": "grew up together" },
  "landing": { "touchy": ["being called childish"], "winsHerOver": ["being trusted with a secret"] },
  "world": {
    "backstory": ["She studies design."],
    "lastWeek": [ { "daysAgo": 2, "text": "Failed a quiz." } ],
    "openThreads": [ { "text": "Portfolio review on Friday.", "goodOutcome": "It went well.", "badOutcome": "It went badly." } ],
    "rightNow": "Sketching at her desk, headphones on."
  }
}
```

| Field | Meaning | Limits (enforced by `Normalize`) |
| --- | --- | --- |
| `promptHash`, `source`, `createdAtUtc` | Set by code, never by the model. `source` is `model`, `keywords` (offline fallback) or `default`. | |
| `temperament.*` | How well each preset fits, 0 to 1. | Clamped to 0..1 |
| `relationship.role` | `stranger`, `acquaintance`, `friend`, `close_friend`, `family`, `partner`, `rival` | Unknown becomes `stranger` |
| `relationship.label` | The prompt's own word ("stepsister") | 40 characters |
| `relationship.affection` | Starting affection | -100..100 |
| `relationship.familiarity` | Starting familiarity | 0..100 |
| `relationship.sharedHistory` | True only when the prompt says they share a past | Always false for a stranger |
| `landing.touchy`, `landing.winsHerOver` | Short phrases | 5 each, 80 characters |
| `world.backstory` | Facts about her life | 20, 200 characters each |
| `world.lastWeek` | Small things from the past week | 8; `daysAgo` 0..14 |
| `world.openThreads` | Things she is in the middle of, with a good and a bad outcome | 3 |
| `world.rightNow` | What she is doing when the user arrives | 200 characters |

### API

- `CharacterSheet.Parse(modelReply)`: tolerant of prose, code fences and trailing commas. Returns null when there is no usable object. The model cannot set `promptHash`, `source` or `createdAtUtc`.
- `CharacterSheet.Neutral(prompt)`: balanced, a stranger at 0 and 0, an empty world. Use it when the reading fails and there is no keyword result.
- `CharacterSheet.HashPrompt(prompt)` and `sheet.IsFor(prompt)`: line endings and outer whitespace do not change the hash.
- `CharacterSheetStore.FileFor / Load / LoadFor / Save`: `LoadFor(path, prompt)` returns null when the prompt changed, which means a new reading is due.
- `CharacterSheetPrompt.System` and `UserMessage(name, prompt)`: the request. The JSON shape in it must keep matching the class; a test checks this.
- `PersonaTemperamentBlend.From(sheet.temperament)`: returns the temperament and a summary such as `Shy 60%, Cheerful 40%`.

### How the reading runs (JUH-118)

1. On persona load, call `CharacterSheetStore.LoadFor`. If it returns a sheet, use it.
2. Otherwise send `CharacterSheetPrompt` to the model that this persona may use, and `Parse` the reply.
3. On no model, a timeout or a null parse: build a sheet from the keyword scorer (`source = keywords`), or `Neutral`.
4. Set `promptHash`, `source` and `createdAtUtc`, then `Save`.
5. Never block the main thread. Until the reading is back, she runs on `Neutral`.

A manual temperament pick in persona settings always wins over the sheet. Built-in personas keep their hand-picked temperament.

## The reading as built (JUH-118 section 1)

**Who does what.** `PisaraReader` (AI assembly, plain C#) builds the request, applies the privacy rule, waits with a timeout, parses and falls back. `PisaraReadingKeeper` (a component next to `CompanionMoodKeeper`) decides when to read and keeps the active persona's sheet. `PisaraReading` (Core, plain C#) holds the lines for settings and the log, and the rule for which temperament applies.

**When a reading happens.** Only for a real persona (not the setup name), and only on an event:

- the persona is selected, or its prompt changed (the personality or speaking style was edited), and there is no cached sheet for that prompt;
- the cached sheet is a fallback (`keywords` or `default`): the model is tried once more per app run;
- the user presses **Re-read** in persona settings.

The prompt that is hashed and sent is the persona text the chat uses (personality plus speaking style, without the cue instructions). A new edit while a reading is running cancels it and starts again. Nothing runs per frame: the keeper compares two strings that are normally the same objects.

**Which model.** The one this persona's chat would use, chosen by `PrivateChatRouting.ProviderId` with `IntimateExpressions.KeepsLocal(privateMode)`, exactly like a chat message. With Private mode on or the intimate gate open that is the local model or the user's own private server, and `PisaraReader.MayUse` refuses any other provider before anything is sent (test: `Private_NeverCallsACloudModel`). A persona whose provider is set to the local model is read by the local model. The request does not pass through the memory layer, so remembered facts about the user are not sent with it. `IntimateBoundaries` is appended to the system prompt.

**Fallback.** No model, a refused provider, a timeout (180 s), an error, a reply without a usable object, or an empty object all give a neutral sheet (`source = default`). The keyword scorer (JUH-119) is not on main yet: `PisaraKeywords.Temperament` is the one place to plug it in, and then the fallback becomes `source = keywords`. The fallback is saved too, so the app does not ask again on every frame or every switch. A fallback never replaces a sheet the model already wrote for the same prompt (`PisaraReading.Keep`): a Re-read while the model is unreachable keeps the earlier reading and says so.

**One correction in code.** Local models often give `close_friend` for a stepsister. When the label names a relative (`PisaraReading.IsKinLabel`: sister, stepbrother, mother-in-law, cousin and so on) the role is set to `family`, unless the model said `partner`.

**One consistency rule in code.** The same models sometimes rate familiarity at 60 or more and still answer `sharedHistory: false`. When the role is not `stranger` and familiarity is 50 or more, `sharedHistory` is set to true (`PisaraReading.ApplySharedHistory`). It is never set for a stranger and never cleared.

**Temperament: Auto or a pick.** The persona file has a new optional key `temperamentMode`: `auto`, `manual`, or missing. Missing means: a built-in persona keeps its hand-picked preset; a user persona that names a preset keeps it (it was picked by hand); a user persona without a preset is automatic. Picking a preset in settings writes `manual`; **Auto** writes `auto` and keeps the preset in the file. Until the reading is back an automatic persona is balanced. The setup name (no persona) stays balanced.

**Persona settings** (old and new settings window) show, for the active persona:

- `Temperament: Auto (Shy 60%, Cheerful 40%): <reason>`, or `Temperament: Tsundere (your pick)`, or `Temperament: Cheerful (built-in)`;
- `Relationship: stepsister (family), affection 25, familiarity 70, shared past: <reason>`;
- where the sheet came from (`Read by the model on <date>`, or that the model did not answer and why);
- **Re-read**, **Auto** and the presets.

**Log.** `Pisara reading: <persona> = <temperament>; <role>, affection N, familiarity N; world N facts, N week entries, N threads; source <model|keywords|default>`, with the reason in brackets when the model was not used. The log is plain text, so it never contains the backstory.

**Tuning the reading prompt.** `CharacterSheetPrompt.System` was changed after reading real output from local 12B models (mistral-nemo 12b and a Rocinante 12B fine-tune):

| Problem in the output | Change |
| --- | --- |
| 3 backstory facts, 2 week entries, 1 thread when asked for "10 to 20", "5 to 8", "2 or 3" | Exact counts ("exactly 12", "exactly 6", "exactly 2", "Count them") and an example with that many empty slots. The parser still accepts up to 20, 8 and 3. |
| A stepsister read as `acquaintance` or `close_friend` | Each role is explained, "decide first whether they are related", plus the kinship correction in code. |
| Several temperament words at 0.5 or more, which blends for no reason | Each word is explained; one word gets the high value, a second gets more than 0.3 only when the description clearly shows it. |
| Affection and familiarity all over the place; a stranger starting at affection 20 | Anchor values for both scales; a stranger is between -10 and 10 unless the description says otherwise. |
| A nervous, shy character rated 0.8 calm | "calm" is explained as steady and composed, "a nervous or timid character is not calm". |
| A stepsister of six years with `sharedHistory: false` | The rule lists who shares a past (relatives, old friends, partners, people who live or work together), plus the consistency rule in code. |
| "Right now" was a scene with the user | "By herself. The user is not in it." |
| With a shared past, most of her week was about the user, with invented user details | At most 2 backstory facts and 2 week entries may involve the user; open threads are her own; "invent nothing about the user". |
| Backstory only repeated the description | At most 5 facts repeat it; the rest are new small details. |

**Developer tool.** `Happi.exe -allowmulti -pisara-read <folder>` reads every persona file (`*.json`, plain JSON as in `Assets/_Project/Resources/Personas`) in the folder with the **local** model, writes `<id>.sheet.json` and `<id>.reply.txt` next to it in plain text, logs the summary and quits. `-pisara-model <tag>` picks another local model; `-pisara-prompt <file>` tries another system prompt without a rebuild. It never calls a cloud model and never touches the user's own personas or sheets; the keeper does not run in this mode.

## Temperament from the sheet (JUH-114)

`PersonaTemperamentBlend.From`:

| Weights | Result |
| --- | --- |
| Nothing reaches 0.35 | Balanced |
| One preset leads, and the second is under 0.2 or under half of it | That preset, unchanged |
| The second is close | The two mixed by weight |

A mix averages gains and half-lives by share. It keeps the leader's baseline only when the leader has at least 60 %, and scales the rest bias by share. Intimate is never touched, and every number passes the same clamps as the presets.

## Relationship

State per persona (`RelationshipState`, JUH-119): `Affection` -100..100, `Familiarity` 0..100, `SecondsTogether`. The math below works on plain numbers, so it does not depend on that class.

### Where a new persona starts

`RelationshipMath.Start(sheet.relationship, out affection, out familiarity)` keeps the sheet's numbers inside what the role allows:

| Role | Least familiarity |
| --- | --- |
| stranger | 0, and at most 15 |
| acquaintance | 10 |
| rival | 25 |
| friend | 30 |
| close_friend | 55 |
| family | 60 |
| partner | 70 |

### Landing

Her reply's cue tag gets one optional attribute: how the user's last message landed with her.

`landed="complimented | thanked | comforted | teased | insulted | dismissed | apologized | flirted | neutral"`

- `AnimationCue.Landed` carries it. A missing or unknown word is `neutral` and never makes the tag unreadable.
- `LandingEvents.Instruction` is the sentence to add to the cue instruction (JUH-118 section 4).

### What a landed message does

`RelationshipMath.Land(landing, cueIntensity, affection, familiarity)` returns the new numbers, the deltas, and mood nudges.

Affection change = table value × (0.5 + cue intensity) × standing factor. The table value slides from the stranger column to the close column with familiarity:

| Event | Stranger (familiarity 0) | Close (familiarity 100) |
| --- | --- | --- |
| complimented | +1 | +4 |
| thanked | +1 | +2 |
| comforted | +2 | +5 |
| teased | -2 | +2 |
| insulted | -10 | -3 |
| dismissed | -3 | -6 |
| apologized | +2 | +4 |
| flirted | -1 | +4 |
| neutral | 0 | 0 |

Standing factor:

- **Kindness** (a positive change): ×1 at affection 0, down to ×0.5 at +100 (diminishing returns) and at -100 (it lands weaker while she dislikes them).
- **Slights** (a negative change): ×0.6 at +100 (forgiven), ×1.3 at -100 (sharper).

Mood nudges are small, because her own cue emotion already moves her mood. They also slide with familiarity: a stranger's compliment pushes Flustered and Anxious (suspicion), a close one's pushes Happy and Affectionate; being dismissed by someone close pushes Sad. `RelationshipMath.PushMoods` sends them through the mood state, where the temperament scales them. Intimate is never pushed from here.

### Familiarity

- Every exchange adds 0.1 at familiarity 0, shrinking to nothing at 100. Kind or not, it is time spent knowing each other.
- `RelationshipMath.FamiliarityAfter(familiarity, seconds)` adds real chat time: 20 hours cover about 63 % of what is left. One hour from zero is under 5 points.
- Familiarity never drops.

Affection does not drift back by itself. What was earned stays.

### Prompt

`RelationshipPrompt.Line(sheet.relationship, sheet.landing, affection, familiarity)`, one line after the mood line:

`Your relationship with the user: stepsister (family); you like them; you know each other very well. Touchy about: being called childish. Warms to: being trusted with a secret.`

| Affection | Phrase |
| --- | --- |
| -60 and below | you can't stand them right now |
| -60 to -20 | you are at odds with them |
| -20 to 20 | you have no strong feelings about them yet |
| 20 to 60 | you like them |
| 60 and above | you adore them |

At -20 and below the line adds that this is love-hate between two people who choose to be here, never fear. Negative affection never closes Intimate and never makes her unwilling: `IntimateBoundaries` stays in every prompt.

### Wiring (JUH-118 section 4)

After each finished reply, once per reply (use `CueSerial`, as the mood does):

1. `var change = RelationshipMath.Land(cue.Landed, cue.Intensity, state.Affection, state.Familiarity);`
2. Write `change.Affection` and `change.Familiarity` to the relationship state.
3. `RelationshipMath.PushMoods(moodState, change, cue.Intensity);`
4. Log `change.LogLine()`.

While the chat is in use, add time with `FamiliarityAfter`. Add `RelationshipPrompt.Line` after the mood line, and `LandingEvents.Instruction` to the cue instruction.

## Memory and her world (planned)

`MemoryEntry` gets new optional fields with defaults, so existing files keep loading:

- `origin`: empty for lived memories, `seeded` for her life before the user, `away` for things that happened while the user was gone.
- `horizon`: long or short. Short entries fade after about two weeks unless they are promoted.
- `happenedAtUtc`: when it happened in her world.

How the sheet becomes memory:

- `world.backstory` becomes long, seeded entries.
- `world.lastWeek` becomes short, seeded entries dated `daysAgo` before the first meeting.
- `world.openThreads` are kept with a status (open, went well, went badly).
- `world.rightNow` opens the first scene.

When the user returns after a long gap, one open thread is resolved with its good or bad outcome, stored as an `away` memory, and its mood nudge goes to the mood state. At session end the model writes one to three memories of the session; promotion and fading are plain C#.

The user can view and forget seeded memories like any other.

## Start over with a persona

Settings → Privacy → Memories → **Start over with [name]**, with a confirm step. For the active persona only, it clears:

- its chat history (the saved file and the chat window);
- everything she remembers (`MemoryStore.DeleteAll`), which also clears `seededFromHash`, so her own life is seeded fresh from the cached character sheet;
- her mood, back to how the temperament starts (`CompanionMoodState.StartOver`): a Cheerful persona starts on her happy baseline again;
- everything else that implements `ICompanionStartOver`. The relationship keeper (JUH-119) implements it and resets to `RelationshipMath.Start(sheet.relationship)`.

It keeps the persona, its description, the character sheet, the temperament pick, the settings, and every other persona. The log says `Start over: <persona>, <n> memories forgotten, chat history cleared.`

## Guards

- **Local only when it must be.** Every model call here follows `IntimateExpressions.KeepsLocal` and Private mode. A private persona never calls a cloud model.
- **Consent.** `IntimateBoundaries` stays in every prompt. The reading prompt also asks for a non-sexual world in which nobody is forced or distressed.
- **Intimate** is never a baseline, a bias or a blend target, and nothing here changes an 18+ gate.
- **Shared history.** Seeded content may mention the user only when `sharedHistory` is true, and that is only true when the persona prompt says so.
- **Compatibility.** New files and new keys with defaults only. Nothing existing is renamed or removed.
- **The model's numbers are never trusted raw.** Everything passes `Normalize`.
