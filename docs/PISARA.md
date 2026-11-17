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
| Sheet format, parser, clamps, prompt hash | **Built**, EditMode tests | Claude |
| Sheet store (encrypted, per persona, in wipe and export) | **Built**, EditMode tests | Claude |
| Temperament blend from sheet weights (JUH-114 step 4) | **Built**, EditMode tests | Claude |
| Reading prompt | **Built**, tuned against local 12B models (JUH-118 section 1) | Claude, tuned by Claude Code |
| The reading call, cache, Re-read, persona settings display | **Built** (JUH-118 section 1), EditMode tests | Claude Code |
| Relationship state, storage, readout; active chat time; reset on wipe and Start over | **Built** (JUH-119), EditMode tests | DeepSeek |
| Temperament keyword scorer (offline fallback) | **Built** (JUH-119), EditMode tests; wired into the reading's fallback (`PisaraKeywords`, #91) | DeepSeek, Claude Code |
| Relationship and landing math, the `landed` cue attribute, the relationship prompt line | **Built**, EditMode tests; called from the app since #91 | Claude |
| Her world in memory: seeding, short-term fading and promotion, away threads | **Built**, EditMode tests | Claude |
| Start over with a persona (chat, memories, mood, relationship) | **Built**, EditMode tests; not run in a player | Claude, DeepSeek |
| Wiring her world: seeding, first scene, away line, session memories, new threads | **Built** (JUH-118 sections 2 and 3, #91), EditMode tests | Claude Code |
| Wiring the relationship: landing per reply, familiarity from time, start values, prompt line, Mood card reasons | **Built** (JUH-118 section 4, #91), EditMode tests | Claude Code |
| Innocent to Perverted (18+ only): local reading, slider, drift, prompt line, moods | **Built** (JUH-116), EditMode tests; not run in a player | Claude |

Every part of the sheet is used: the temperament since JUH-118 section 1, and the relationship, landing and world since #91.

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

**Which model.** By default the one this persona's chat would use, chosen by `PrivateChatRouting.ProviderId` with `IntimateExpressions.KeepsLocal(privateMode)`, exactly like a chat message. With Private mode on or the intimate gate open that is the local model or the user's own private server, and `PisaraReader.MayUse` refuses any other provider before anything is sent (test: `Private_NeverCallsACloudModel`). A persona whose provider is set to the local model is read by the local model. The request does not pass through the memory layer, so remembered facts about the user are not sent with it. The closed-gate rule is appended when the gate is closed; an open gate adds no consent rule.

**JUH-126: the optional Pisara cloud model.** Settings → AI has **Pisara model** with **Use a cloud model for Pisara readings** (off by default), its own base URL and model, and Settings → Keys has its own masked **Pisara API key**. With it on, the first reading, Re-read and the fallback retry use that service while chat keeps its own provider, model and key. An empty base URL or model falls back to `BaseUrl` and `Model` when the Pisara URL is empty or on the same host as the Grok chat URL; a different host requires its own base URL, model and key, so the Grok key is never sent there. An empty Pisara key falls back to the Grok key only when the Pisara URL is empty or on the Grok host. The request is always built gate-closed (`intimateActive = false`, `BlockCloud = false`) and carries the closed-gate rule, even when the 18+ gate is open; it never asks for anything 18+. **Private mode no longer keeps this one reading local:** because the 18+ gate only opens while private mode is on, the character-sheet reading may go to the cloud in private mode too. Everything else stays local in private mode: chat, voice, the voice agent, session memories and memory capture. Innocence, 18+ secrets and every other 18+ call stay on the local model. The sheet records `readInCloud` (the reading went to a cloud model) and `readBy`; seeded memories follow that flag, not the current gate or another persona's read. The default chat path outside private mode counts as cloud too, as it did on main; a local private-mode or gate-open reading stays private. Anthropic's native `/v1/messages` API is used when the base URL is an `anthropic.com` host; the reading is non-streamed there so the JSON can be parsed. Other hosts use the OpenAI-compatible streaming path. DeepSeek endpoints are sent `thinking: {"type":"disabled"}` for the reading: the V4 thinking models can spend the whole token budget on reasoning and return an empty `content`, which left the sheet unparsed.

**Fallback.** No model, a refused provider, a timeout (1200 s), an error, a reply without a usable object, or an empty object all give a neutral sheet (`source = default`). The keyword scorer (JUH-119) is not on main yet: `PisaraKeywords.Temperament` is the one place to plug it in, and then the fallback becomes `source = keywords`. The fallback is saved too, so the app does not ask again on every frame or every switch. A cached fallback is tried once more per app run when a provider is configured; **Re-read** also retries it. A fallback never replaces a sheet the model already wrote for the same prompt (`PisaraReading.Keep`): a Re-read while the model is unreachable keeps the earlier reading and says so.

**One correction in code.** Local models often give `close_friend` for a stepsister. When the label names a relative (`PisaraReading.IsKinLabel`: sister, stepbrother, mother-in-law, cousin and so on) the role is set to `family`, unless the model said `partner`.

**One consistency rule in code.** The same models sometimes rate familiarity at 60 or more and still answer `sharedHistory: false`. When the role is not `stranger` and familiarity is 50 or more, `sharedHistory` is set to true (`PisaraReading.ApplySharedHistory`). It is never set for a stranger and never cleared.

**Temperament: Auto, a preset, or nine sliders.** The persona file has an optional key `temperamentMode`: `auto`, `manual`, or missing. Missing means: a built-in persona keeps its hand-picked preset; a user persona that names a preset keeps it (it was picked by hand); a user persona without a preset is automatic. Picking a preset in settings writes `manual` and clears any custom sliders; **Auto** writes `auto` and keeps the preset in the file. A custom profile (JUH-136) writes `manual` and `temperamentAxes` (nine numbers, 0 to 100). Old files have no `temperamentAxes` and load the preset's point. Until the reading is back an automatic persona is balanced. The setup name (no persona) stays balanced.

The character sheet may also carry `temperament.axes` with `axesSet: true`. When any mood slider is off 50, that profile is what Auto runs, and the five preset weights stay for the profile radar. Curiosity alone does not replace them. All 50s, or a sheet from before the sliders, still uses the preset weights and the blend. A manual preset or manual sliders always win.

**Persona settings** (old and new settings window) show, for the active persona:

- `Temperament: Auto (Shy 60%, Cheerful 40%): <reason>`, or `Temperament: Tsundere (your pick)`, or `Temperament: Cheerful (built-in)`;
- `Relationship: stepsister (family), affection 25, familiarity 70, shared past: <reason>`;
- where the sheet came from (`Read by the model on <date>`, or that the model did not answer and why), and from JUH-126 the model that read her, stored in the sheet (`readBy`, for example `Read by claude-opus-5-5 (cloud)`), so it survives a restart;
- **Re-read**, **Auto**, the presets, and the nine temperament sliders (JUH-136).

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

**Developer tool.** `Happi.exe -allowmulti -pisara-read <folder>` reads every persona file (`*.json`, plain JSON as in `Assets/_Project/Resources/Personas`) in the folder with the configured Pisara model (the JUH-126 cloud service when its toggle is on, including in private mode, otherwise the local model), writes `<id>.sheet.json` and `<id>.reply.txt` next to it in plain text, logs the summary and quits. `-pisara-model <tag>` picks another local model; `-pisara-prompt <file>` tries another system prompt without a rebuild. The world and session-memory checks still use the local model. It never touches the user's own personas or sheets; the keeper does not run in this mode.

## Temperament from the sheet (JUH-114)

`PersonaTemperamentBlend.From`:

| Weights | Result |
| --- | --- |
| Nothing reaches 0.35 | Balanced |
| One preset leads, and the second is under 0.2 or under half of it | That preset, unchanged |
| The second is close | The two mixed by weight |

A mix averages gains and half-lives by share. It keeps the leader's baseline only when the leader has at least 60 %, and scales the rest bias by share. Intimate is never touched, and every number passes the same clamps as the presets. When `axesSet` is true and any mood slider is off 50, Auto uses those sliders instead of this blend (`TemperamentMood`). Curiosity alone does not. The sliders for a blend with no axes are the mix of the two presets' points, for the settings UI only.

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

`Your relationship with the user: stepsister (family); you like them; you know each other very well. Touchy about: (t1) being called childish. Warms to: (w1) being trusted with a secret.`

The ids in brackets are her secrets (JUH-123, see "Her secrets" below). All of them are listed, up to five per list.

| Affection | Phrase |
| --- | --- |
| -60 and below | you can't stand them right now |
| -60 to -20 | you are at odds with them |
| -20 to 20 | you have no strong feelings about them yet |
| 20 to 60 | you like them |
| 60 and above | you adore them |

At -20 and below the line adds that this is love-hate between two people who choose to be here, never fear. Negative affection never closes Intimate and never makes her unwilling: only the 18+ gate, Private mode and the adult folder control access.

### Wiring (JUH-118 section 4)

After each finished reply, once per reply (use `CueSerial`, as the mood does):

1. `var change = RelationshipMath.Land(cue.Landed, cue.Intensity, state.Affection, state.Familiarity);`
2. `if (relationshipKeeper.Apply(change)) RelationshipMath.PushMoods(moodState, change, cue.Intensity);`

`RelationshipKeeper.Apply` writes the numbers, records the reason for the Mood card, logs `change.LogLine()`, and does nothing while **Relationship changes** is off.

While the chat is in use, add time with `FamiliarityAfter`. Add `RelationshipPrompt.Line` after the mood line, and `LandingEvents.Instruction` to the cue instruction.

### The relationship as wired (#91)

Chat and the relationship keeper meet in `RelationshipNow` (Core), because the chat assembly cannot see the settings assembly.

1. **Landing, once per reply.** When a reply to a user message has finished, `ChatPanel` reports its cue once (guarded by `CueSerial`) with `RelationshipNow.ReportReply`. This also happens when the tag's emotion was unreadable or the tag was missing: the cue is then neutral, which moves no affection but still counts as an exchange. `RelationshipKeeper` lands it with `RelationshipMath.Land`, then `Apply` writes the numbers, records the reason (the wheel's Mood card shows the last four), marks it dirty and logs `change.LogLine()`. If it was applied, `CompanionMoodKeeper.ApplyRelationship` sends the change's moods through `RelationshipMath.PushMoods`. Screen-look remarks are not user messages and do not land.
2. **Familiarity from time.** The active chat time the keeper already counted (`ActivityWindowSeconds`) also goes through `RelationshipMath.FamiliarityAfter`. It is collected in a double (`FamiliarityClock`) and applied every 5 s, before a landed reply and before a save: per frame the gain rounds away in float above about 83, so a persona at 90 never grew. Many short steps give the same result as one long one (test: an hour at 60 fps against one 3600 s step).
3. **Start values.** When a persona is loaded and has no relationship file yet, the keeper waits for its model reading and then starts from `RelationshipMath.Start(sheet.relationship)`, once. An existing relationship is never overwritten by a reading or a Re-read. A user correction (`SetByUser`) or Start over ends the wait. The setup name never does this.
4. **The prompt line.** The keeper publishes `RelationshipPrompt.Line(sheet.relationship, sheet.landing, affection, familiarity)`, and chat puts it right after the mood line in every request. It is rebuilt only when the sheet, the switch or a rounded number changes. It is empty for the setup name (its own relationship text stays the only one), without a sheet, and while **Relationship changes** is off.
5. **The cue instruction** gets `LandingEvents.Instruction` (in `AnimationDirector.SystemPromptSuffix`) while **Relationship changes** is on. The streaming filter already hides the whole tag, so `landed=` never shows in the chat text (test `LandedAttribute_NeverShowsInTheChatText`).
6. **Keyword fallback.** `PisaraKeywords.Temperament` uses `TemperamentKeywordScorer`: the best preset at 0.6, and the second-best at 0.4 only when its score is above 0.2. The raw scores are not passed on: a single trait word scores 0.33, under the blend's 0.35.

## Memory and her world

**Built** in `WorldMemory` (`Assets/_Project/Scripts/Memory/WorldMemory.cs`), with EditMode tests, and **wired into the app** in JUH-118 sections 2 and 3 (see "Her world as wired" below).

`MemoryEntry` has new optional fields. They default to "an ordinary lived, long-term memory", so existing memory files load unchanged:

| Field | Meaning |
| --- | --- |
| `origin` | `""` lived with the user, `seeded` her life before the user, `away` something that happened while the user was gone |
| `horizon` | `0` long-term, `1` short-term |
| `happenedAtUtc` | When it happened in her world |
| `recalls` | How often a short-term memory went into a prompt |
| `isThread`, `goodOutcome`, `badOutcome` | Something she is in the middle of, and how it could turn out |

`memory.json` also gets `seededFromHash`: the prompt hash of the sheet her world came from.

### Seeding

`WorldMemory.Seed(store, sheet, metAtUtc, privateMode)`:

- `world.backstory` becomes long-term seeded facts.
- `world.lastWeek` becomes short-term seeded memories, dated `daysAgo` before the meeting.
- `world.openThreads` become thread entries.
- It runs once per sheet. When the persona prompt changes, the old seeded entries are replaced; lived and away memories are kept.
- The sheet must carry its `promptHash` (set it before calling). A sheet without one is never seeded, so a resolved thread cannot come back on the next start.
- Every entry carries the private flag passed in, so the existing rule holds: a private memory never goes into a cloud prompt.
- "Delete all memories" forgets everything, and also which sheet her world came from. On the next start her world is seeded fresh from the cached sheet: her backstory comes back, with her open threads reset. She keeps her own life and starts over with the user.

`WorldMemory.FirstScene(sheet)` gives the line that opens the very first conversation mid-scene, from `world.rightNow`.

### Short-term memories

- `WorldMemory.AddShortTerm(store, text, nowUtc, privateMode)` stores a session memory (the one to three lines the model writes at session end).
- `WorldMemory.MarkRecalled(store, entries)`: call it with the entries that went into a prompt. A short-term memory that came up twice becomes long-term.
- `WorldMemory.Fade(store, nowUtc)`: forgets short-term memories 14 days after they were stored. Core facts and threads are never forgotten this way. Call it once per start.

### While the user is away

`WorldMemory.ResolveThread(store, awaySeconds, roll, nowUtc, includePrivate)`:

- Needs at least 4 hours away and an open thread.
- `roll` is a random number from 0 to 1. Below 0.6 the thread goes well. A thread with only one written outcome takes that one.
- The thread is replaced by a short-term `away` memory, such as "Portfolio review on Friday. It went well."
- It returns a prompt line, so she can bring it up herself, and mood nudges (Happy when it went well; Sad and a little Anxious when it did not). Send those through the mood state with `CompanionMoodTable.Scaled`.
- With `includePrivate` false, private threads are left alone.

This needs no model call: the outcomes were written with the sheet. The model only phrases it in her next reply.

### Prompt

`MemoryPrompt.Build(entries, nowUtc)` puts "(3 days ago)" in front of memories that say when they happened.

### Her world as wired (JUH-118 sections 2 and 3)

Memory is one file per persona (`memory-<persona>.json`), so her world is seeded into her own store. The setup name (no persona) has no sheet and no world.

**Who does what.** `PisaraReadingKeeper` (settings side) owns the sheet, so it also seeds, fades, resolves the away thread and re-seeds after Start over. The chat panel cannot see the settings assembly, so the lines for the prompt travel through `WorldPromptNow` (Core), like the mood does through `CompanionMoodNow`. `ChatPanel.Session.cs` adds them to the prompt and runs the session memories.

1. **Seeding.** Whenever the active persona has a sheet with `source = model` and `WorldMemory.NeedsSeeding` says so (after a reading, on start, on a persona switch, after Start over), the keeper seeds it. A fallback sheet is never seeded. The entries are private unless that sheet's reading went to a cloud model (`CharacterSheet.readInCloud`: the Pisara cloud service or a chat cloud model). A locally read sheet stays private, and the current gate or another persona's read does not matter. Log: `Pisara world: <persona> seeded N memories`.
2. **The shared-history guard.** Before seeding, `WorldGuard.ForSeeding` makes a copy of the sheet and, when `relationship.sharedHistory` is false, leaves out every backstory fact, week entry and open thread that mentions the user ("the user", "you", "your": whole words). The cached sheet is not changed. The log says how many lines were left out.
3. **The first scene.** `WorldGuard.FirstScene(sheet)` is `WorldMemory.FirstScene`, except that without a shared past a "right now" that already has the user in it is not used. The chat adds the line to the persona prompt only for a typed or spoken message sent into a conversation with no user or companion lines, so it is in exactly one request: the first message of a new persona, and again after Start over. A persona that already has a chat history gets no first scene.
4. **Arriving** (app start and persona switch): `WorldMemory.Fade`, then `WorldMemory.ResolveThread`. The time away is counted from the persona's mood file (`CompanionMoodStore.SavedAtUtc`), which is saved once a minute while she is active and on quit. The roll is `UnityEngine.Random.value`. `includePrivate` is true only when this session's chat stays on the local model or the private server. When something happened: its `PromptLine` is added to every request until one reply has finished, its mood nudges go through `CompanionMoodTable.Scaled` (`CompanionMoodKeeper.TryApplyPushes`), and the log says `Away: <persona> = <text>`. A new persona (no mood file yet) has no away event, and the arrival runs before seeding, so a thread never resolves in the moment it is created.
5. **Memories in the prompt.** `MemoryChatProvider.Enrich` uses `MemoryPrompt.Build(entries, nowUtc)` and calls `WorldMemory.MarkRecalled` with the same entries. The fact-extraction request reads the same memories but does not count as a recall, and a request with `ChatRequest.NoMemory` gets no memory block at all.
6. **Session memories.** A session is the messages since the last pause. It ends on a persona switch (asked before the switch, so the old persona's model, privacy and store are used), after ten minutes without a message, or on quit. `SessionMemories` then asks the model once for one to three lines and stores each with `WorldMemory.AddShortTerm`. Sessions with fewer than four user messages are skipped. On quit the model gets four seconds; after that the app quits without the memories (a local 12B model usually needs longer, so quit memories mostly come from cloud models; the ten-minute pause covers the rest). Same model rule as the reading: with Private mode on or the intimate gate open only the local model or the private server is asked (`PisaraReader.MayUse`), and the memories are stored as private. The rule is sticky for the session (`SessionPrivacy`): every send notes its private flag, so a session that was private at any point stays local and private at its end, even when Private mode was turned off before the session ended. The closed-gate rule is in the prompt when the gate is closed. Log: `Session memories: N stored after <why>` (never the text).
7. **Start over.** The keeper implements `ICompanionStartOver`: right after the memories are deleted it seeds her world again from the cached sheet and drops a pending away line. The chat is empty again, so the first scene opens the next conversation.
8. **The memory viewer** (old and new settings) labels seeded entries `(her life)` and away entries `(while you were away)`. They can be forgotten like any other memory.
9. **New threads.** The session-end request also carries `WorldThreads.Instruction` (only for a persona, and only while **Her own world** and **Life while you are away** are on). A `newThread` object in the reply is stored with `WorldThreads.Add` (at most 3 open threads, with the session's private flag); its JSON is never stored as a memory line. Log: `Session memories: N stored after <why>, new thread: yes|no`.
10. **Switches** (Overlay tab): **Her own world** off skips seeding and the first scene; **Life while you are away** off skips `ResolveThread` and new threads. Nothing stored is deleted.

**Developer tool.** `Happi.exe -allowmulti -pisara-read <folder> -pisara-world` also writes `<id>.world.txt` per persona: the seeded memories, what the guard left out, her first reply, an away event with her reply to it, and the session memories of that short talk. It uses the local model and a throwaway memory file in that folder.

## Start over with a persona

Settings → Privacy → Memories → **Start over with [name]**, with a confirm step. For the active persona only, it clears:

- its chat history (the saved file and the chat window);
- everything she remembers (`MemoryStore.DeleteAll`), which also clears `seededFromHash`, so her own life is seeded fresh from the cached character sheet;
- her mood, back to how the temperament starts (`CompanionMoodState.StartOver`): a Cheerful persona starts on her happy baseline again;
- everything else that implements `ICompanionStartOver`. The relationship keeper (JUH-119) implements it and resets to `RelationshipMath.Start(sheet.relationship)`.

It keeps the persona, its description, the character sheet, the temperament pick, the settings, and every other persona. The log says `Start over: <persona>, <n> memories forgotten, chat history cleared.`

## Control and tuning

The reading is a guess, and the numbers are untuned, so the user can see and correct what Pisara does.

### Correcting the relationship

Settings → Persona → Who she is → **Correct the relationship**. The user picks the role, her word for it, affection and familiarity (in steps of 10), and whether they share a past. Every click applies at once:

- `PisaraCorrections.SetRelationship` writes it into the sheet and sets `relationshipLocked`. The same limits as a reading apply: a stranger stays near 0 and never shares a past; each role keeps its least familiarity.
- The current relationship is set to the corrected numbers (`RelationshipKeeper.SetByUser`), so it applies now, not only after a Start over.
- A new reading keeps the correction, even of an edited prompt (`PisaraCorrections.CarryOver` in `PisaraReadingKeeper`). The model cannot set the lock: `CharacterSheet.Parse` clears it.
- **Use the reading again** drops the lock and reads her description again.

The relationship line says "(your correction)" while the lock is on. Temperament already has its own manual pick.

### Switches

Settings → Persona → Her life has one switch per part, all on by default (`pisaraRelationship`, `pisaraAwayEvents`, `pisaraWorld` in the settings file). Off freezes that part; nothing is deleted. `PisaraSwitches` holds the current values for code that has no settings at hand.

| Switch | Off means |
| --- | --- |
| Relationship changes | `RelationshipKeeper.Apply` and time spent together change nothing. |
| Life while you are away | No thread resolves between sessions, and no new thread is added. |
| Her own world | No seeding, no first scene, no new threads. |

### Why it moved

The Mood card on the wheel shows where she stands ("she likes you; you know each other well") and the last four reasons, such as `An insult stung: affection -4.6` (`RelationshipHistory`, `RelationshipCard`). Neutral exchanges are not listed. The reasons last for the session.

### New threads

Without new threads her world runs dry: the two or three seeded ones turn out and nothing else ever happens to her. At session end the model may propose one new thread (`WorldThreads.Instruction`). It is stored with `origin = life`, as a long-term open thread, while fewer than three are open. It is about her, ordinary life, and nothing sexual; a private session's thread stays private.

### Floating changes (JUH-122)

While you chat, small pieces float up beside her head when a landing moves the relationship, like floating combat text: `+1.5 Compliment` with a pink heart, `-2.4 Brushed off` with a grey one, and `You know each other well now` when familiarity crosses into a new band. Each rises about 30 px and fades in about 1.5 s; older ones keep drifting up while new ones appear.

- **Critical hits** (`RelationshipMath`): a real landing (not neutral) is a crit when the cue intensity is `CritIntensity` (0.85) or more. A crit multiplies the affection change by `CritMultiplier` (2), inside the same clamps. `RelationshipChange.Critical` says so, the log line says `(critical)`, and the Mood card reason starts with `Critical: `. No random crits. `Land(..., secretHit)` takes the first find of a secret (JUH-123, below). Both numbers are proposed defaults for Jussi to confirm.
- **On screen**: a crit pops big and gold with a punch from 1.6 down to 1 and a CRITICAL caption; a crit drop is big and grey.
- **The rules** (`RelationshipFeedback`, plain C#): a normal piece needs an affection change of 0.5 or more; at most 2 normal pieces are on screen, and a new one pushes the oldest out early; crits always show and are not counted; familiarity shows only at a new band. The secret-found piece (a gold star) comes with the affection piece when a message finds a secret (JUH-123).
- **The event**: `RelationshipKeeper.Apply` raises `RelationshipNow.Changed` with a `RelationshipNotice` (the change, its short landing name, whether familiarity reached a new band), only when the change was applied. A band reached through chat time shows with the next landing.
- **The view** (`FloatingFeedbackView`, new UI only): one pool of six TMP pieces, beside her head (3D avatar or 2D video), on the side away from the expression portrait, or at the portrait when there is no head. No allocation per frame, no `Instantiate` per change.
- **Off**: nothing shows while **Show relationship changes while chatting** (`showRelationshipFeedback`, Settings → Chat → Replies) or **Relationship changes** is off.
- **The landing reminder**: the local model wrote the cue tag but never `landed=`, so nothing landed. While relationship changes are on, the reminder on the newest user message (`LandingEvents.TurnReminder`) has `landed` in its example and lists the words.
- **Checking it without typing**: `Happi.exe -allowmulti -newui -pisara-chat <file>` sends each line of the file through the real chat and saves two window screenshots after each reply, next to the file.

### Her secrets (JUH-123)

A game element: the user finds out what she likes and dislikes by talking with her. Her secrets are the landing phrases in her sheet, up to 5 "What stings her" (`landing.touchy`, ids t1..t5) and up to 5 "What wins her over" (`landing.winsHerOver`, ids w1..w5), in sheet order (`HerSecrets.From`).

- **The model judges, code keeps the state.** The prompt line lists each phrase with its id. While "Relationship changes" is on, the cue instruction (`HerSecrets.Instruction`) asks for `secret="t2"` when the user's message clearly hit one, and for her to react in that reply as if they found out something she had not told them. The cue parser reads it into `AnimationCue.Secret`; the tag is hidden from the chat like the other attributes.
- **First find, critical hit; later, ordinary** (Jussi, 2026-11-15). `RelationshipKeeper.OnReply` checks the id (`HerSecrets.Check`). The first find passes `firstSecretHit` to `RelationshipNow.Land`, so the change is a critical hit (×2, `RelationshipMath.CritMultiplier`; a neutral landing still moves nothing). A secret found before lands like any other message. An id that is not one of hers is ignored and logged.
- **Shown.** A gold "Secret found: …" piece floats up with the affection piece (`RelationshipNotice.SecretFound`, `FeedbackKind.Secret`). Profile → Personality shows "Her secrets · 3 of 8 found": found ones with their text, the rest as locked "Not found yet" chips, the newest find with a gold outline (`ProfileSecrets`).
- **Saved** per persona, encrypted, as `her-secrets.json` / `her-secrets-<persona>.json` (`SecretsStore`), by a stable hash of the phrase (`HerSecrets.Key`), so a re-read that keeps the phrase keeps it found. A find whose phrase left the sheet no longer counts. Wipe and export include the file (`LocalDataInventory.IsHerSecretsName`); Start over hides them again. The name is not `secrets.json`, which is the API keys.
- **Privacy.** Nothing new is sent: the phrases were already in the prompt.
- The 18+ secrets (kinks, sensitive areas, shy to do) are JUH-125.

### Scenario runner

`PisaraScenario` runs a fixed persona and a scripted conversation through the real `RelationshipMath` and mood state, without a model or a scene, and `Report()` prints the trajectory. `PisaraScenarioTests` checks the JUH-115 expectations and writes each trajectory to the test log:

- a stranger is not won over by ten compliments;
- teasing lands well with a stepsister and badly with a stranger;
- an insult hurts at least twice as much from a stranger as from a partner;
- an apology repairs part of an insult, not all;
- a rival insulted five times can't stand them, and affection stays in range;
- familiarity never drops, and earned affection does not drift back;
- a partner's compliments make her more Affectionate than a stranger's, and Intimate is never pushed.

Change a number in `RelationshipMath`, run the tests, and read the log to see what moved.

## Innocent to Perverted (JUH-116, 18+ only)

Where she sits between **Innocent (0)** and **Perverted (100)**. It reflects her sexual experience as well as her boldness. The design decisions are Jussi's (2026-11-14, on the JUH-116 issue).

Only while the 18+ gate is open. While it is closed nothing is read, nothing moves, the slider is hidden and the prompt line is empty.

**Start.** `InnocenceReader` reads her persona once on the local model or the user's private server, never a cloud model (`PisaraReader.MayUse` with keepsLocal; test `Read_NeverRunsWithTheGateClosed_OrOnACloudModel`). It judges what fits her most likely from her age, personality, relationships and anything the description says about her experience. A shy 18-year-old who has never dated sits near 10; a confident 26-year-old bully near 70. It is a separate request from the character sheet, because the sheet may be read by a cloud model. The reply is `{"innocence": 0-100, "reason": "..."}`. A changed prompt is read again; a failed reading is tried once more on the next launch.

**Slider.** Settings, Persona, under the relationship correction: "Innocent to Perverted (18+)". Moving it sets where she is and her baseline, and a new reading no longer changes it. **Use the reading** goes back to the reading; **Read again** asks the model again.

**Drift, both ways** (`InnocenceMath`):
- Each intimate moment moves her toward experienced. An intimate moment is a finished reply whose cue is an intimate expression, or an adult video reaction (`strip` or a naked clip). The step is 2 points at intensity 0.5 when she is fully innocent, scaled 0.5x to 1.5x by intensity and by how much room is left above her.
- Without intimate moments, what she gained above her baseline fades: nothing for 2 days, then half every 14 days. It never goes below the baseline, and is computed from her value at the last moment, so it does not compound.
- A new reading keeps what she gained on top of the new baseline (`Rebase`).

**What it changes:**
- **The prompt line** (`InnocencePrompt`), after the relationship line: her experience, how often she makes the first move, and how comfortable she is undressed. While she is naked (`IntimateExpressions.Undressed`) it says how she feels about being naked instead. Five bands: very innocent, innocent, in between, experienced, perverted. She has no way to message first, so "the first move" means how forward she is within her replies.
- **Her mood.** After an intimate moment the innocent side gets Flustered (and a little Anxious at the very innocent end), and the experienced side Happy and Affectionate. Being undressed pushes the same way, more strongly on the innocent side. The pushes are small (0.05 to 0.25), like the relationship ones. Intimate itself is never pushed from here.

**Storage.** `innocence-<persona>.json`, encrypted, next to the relationship file, in wipe and export (`LocalDataInventory.IsInnocenceName`). **Start over** puts her back at her baseline and keeps the reading and the slider setting. A full wipe deletes the file.

**Shown.** The wheel's Mood card adds `Innocence: 30 (innocent)` while the gate is open. Log lines start with `Innocence:`.

## Her 18+ secrets (JUH-125, 18+ only)

Three more groups of secrets, only while the 18+ gate is open (Jussi, 2026-11-15): **kinks** (a genre and the specific thing in it, ids k1..k3), **sensitive areas** (s1..s3) and what she is **shy to do** (y1..y3).

- **How many**: 1 to 3 per group, picked once from the persona id and her description hash (`IntimateSecretCounts`), so a re-read of the same description asks for the same counts. Kinks and sensitive areas: 1/2/3 at 20/40/40%. Shy to do: 50/35/15%. Proposed odds, to be tuned.
- **Read on the local model only** (`IntimateSecretsReader`): a separate request with `BlockCloud` and `NoMemory`, refused before anything is sent while the gate is closed or when the provider is not the local model or the private server. The Pisara model setting (JUH-126) never changes it. When the description says nothing, the model picks what fits her personality.
- **Kept** by `IntimateSecretsKeeper`, per persona, encrypted, as `intimate-secrets-<persona>.json` (`IntimateSecretsStore`): the reading and which were found. It reads once per description, and again from Settings → Persona → Her 18+ secrets → Read her secrets again. A find whose text is in the new reading stays found. Start over hides them again; wipe and export include the file. Player.log gets counts and ids only, never the secrets.
- **Prompt** (`IntimateSecretsPrompt`, through `IntimateSecretsNow`, after the innocence line): the three lists with ids, how she feels about the shy ones, and the instruction to set `secret="k1"` when an intimate moment clearly does one. `IntimateSecretsNow.Append` adds nothing while the gate is closed, whatever was published. While the gate is open the chat is local anyway.
- **Shy to do warms slowly with innocence** (JUH-116 bands, no fixed point): very innocent, she shies away; innocent, reluctant; in between, hesitant but can be coaxed; experienced, curious; perverted, open to them.
- **Rewards** (`RelationshipKeeper.OnReply`): the first find of any 18+ secret is a critical hit (×2). After that, one of her kinks or sensitive areas always multiplies the change by `RelationshipMath.FavouriteMultiplier` (1.25), on top of a crit from intensity: "doing her kink should always be more rewarding than not doing it". A shy-to-do found before lands as usual.
- **Shown**: the gold "Secret found" piece, and Profile → Personality → **Her 18+ secrets · 1 of 6 found** (Kinks, Sensitive areas, Shy to do, "How shy she is now"), only while the gate is open.

## Guards

- **Local only when it must be.** Every model call here follows `IntimateExpressions.KeepsLocal` and Private mode, except the JUH-126 character-sheet reading when the Pisara cloud toggle is on: that one reading may go to its own cloud service, always gate-closed. Chat, voice, session memories, innocence and 18+ secrets never use it.
- **Consent.** The app adds no consent rule when the 18+ gate is open. `IntimateBoundaries` only carries the closed-gate rule. The reading prompt still asks for a non-sexual world in which nobody is forced or distressed.
- **Intimate** is never a baseline, a bias or a blend target, and nothing here changes an 18+ gate.
- **Shared history.** Seeded content may mention the user only when `sharedHistory` is true, and that is only true when the persona prompt says so.
- **Compatibility.** New files and new keys with defaults only. Nothing existing is renamed or removed.
- **The model's numbers are never trusted raw.** Everything passes `Normalize`.
