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
| Reading prompt, first version | **Built** (this PR); not yet run against any model | Claude, tuned by Claude Code |
| Relationship state, storage, readout | Planned, JUH-119 | DeepSeek |
| Temperament keyword scorer (offline fallback) | Planned, JUH-119 | DeepSeek |
| Relationship and landing math | Planned, next PR | Claude |
| Memory tags, promotion and fading | Planned, PR after that | Claude |
| The reading call, settings display, seeded memories, away line, wiring | Planned, JUH-118 | Claude Code |

Nothing in this PR changes how she behaves. The classes are not called from the app yet.

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

## Temperament from the sheet (JUH-114)

`PersonaTemperamentBlend.From`:

| Weights | Result |
| --- | --- |
| Nothing reaches 0.35 | Balanced |
| One preset leads, and the second is under 0.2 or under half of it | That preset, unchanged |
| The second is close | The two mixed by weight |

A mix averages gains and half-lives by share. It keeps the leader's baseline only when the leader has at least 60 %, and scales the rest bias by share. Intimate is never touched, and every number passes the same clamps as the presets.

## Relationship (planned)

State per persona (`RelationshipState`, JUH-119): `Affection` -100..100, `Familiarity` 0..100, `SecondsTogether`. A new persona starts from the sheet's values.

**Landing.** Her reply's cue tag gets one optional attribute saying how the user's message landed:

`landed="complimented | thanked | comforted | teased | insulted | dismissed | apologized | flirted | neutral"`

A missing or unknown value is `neutral`. The relationship leaf then scales the event:

- Affection change = base amount for the event × a familiarity factor × a standing factor.
- The same event can change sign. A tease from a stranger costs a little; from someone close it adds a little. An insult costs most from a stranger and least from family. A compliment from a stranger adds little and pushes Anxious (suspicion); from a partner it adds most.
- Familiarity only grows: with real time together and a little with each exchange.
- The leaf passes mood pushes on to the existing mood state, where the temperament scales them.
- Affection does not drift back by itself in the first version. What was earned stays.

**Prompt.** One line after the mood line, for example: `Your relationship with the user: stepsister; you like them; you know each other very well.`

**Negative affection** means bickering and love-hate. It never closes Intimate, and it never makes her unwilling: `IntimateBoundaries` stays in every prompt.

The exact numbers come with the next PR and its tests.

## Memory and her world

**Built** in `WorldMemory` (`Assets/_Project/Scripts/Memory/WorldMemory.cs`), with EditMode tests. Not called from the app yet; JUH-118 wires it.

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

### Wiring (JUH-118 sections 2 and 3)

1. After the reading: `if (WorldMemory.NeedsSeeding(store, sheet)) WorldMemory.Seed(...)`. Pass the persona's private flag.
2. On the first message of a new persona, add `WorldMemory.FirstScene(sheet)` to the prompt.
3. On start: `WorldMemory.Fade`, then `WorldMemory.ResolveThread` with the time since the last session. Add its `PromptLine` to the next prompt once, and push its moods.
4. Where memories go into the prompt: use `MemoryPrompt.Build(entries, nowUtc)` and call `WorldMemory.MarkRecalled`.
5. At session end: ask the model for one to three memories and store each with `AddShortTerm`.

## Guards

- **Local only when it must be.** Every model call here follows `IntimateExpressions.KeepsLocal` and Private mode. A private persona never calls a cloud model.
- **Consent.** `IntimateBoundaries` stays in every prompt. The reading prompt also asks for a non-sexual world in which nobody is forced or distressed.
- **Intimate** is never a baseline, a bias or a blend target, and nothing here changes an 18+ gate.
- **Shared history.** Seeded content may mention the user only when `sharedHistory` is true, and that is only true when the persona prompt says so.
- **Compatibility.** New files and new keys with defaults only. Nothing existing is renamed or removed.
- **The model's numbers are never trusted raw.** Everything passes `Normalize`.
