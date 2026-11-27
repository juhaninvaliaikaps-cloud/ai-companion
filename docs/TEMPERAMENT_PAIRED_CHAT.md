# Paired chat test: Assertiveness and Directness

JUH-222 builds a cheap, repeatable A/B for the next live temperament patch.
It is synthetic-only and never reads real chat history or calls a cloud
provider.

## Files

```text
tools/temperament-ab/fixtures.json       synthetic persona, history, cases, conditions
tools/temperament-ab/run_temperament_ab.py
tools/temperament-ab/results-dryrun.md   generated prompt-coverage receipt
Assets/_Project/Tests/EditMode/TemperamentPairedPromptTests.cs
```

## What is compared

The same synthetic persona, two-message history, sampling settings and seed
(4123) are used for every condition. One axis moves between step 2 and step 9;
all other axes stay at step 5 (stored 44).

```text
Assertiveness  11 -> 89, Directness 44
Directness     11 -> 89, Assertiveness 44
Negative       Curiosity 11 -> 89, both other axes 44
```

The pairs cover:

- **Initiative**: the user has a free evening and no plan. The reply should
  offer or start a concrete next step.
- **Directness**: the user asks whether to take a job offer. The reply should
  state a preference or answer explicitly.
- **Divergence**: the user is deciding whether to skip the gym. Initiative and
  directness can point in different directions: propose a plan, state an
  opinion, or both.
- **Negative control**: Curiosity moves while Assertiveness and Directness stay
  put. Its prompts must be identical on both paths.

## Prompt paths

Without a cached voice, `ChatPanel.Session.cs:62` appends
`PersonaMannerNow` when `VoiceNow.PromptBlock` is empty. With a cached voice,
that block is used instead and the manner line is not appended.

- No cached voice: `PersonaManner.Line` (`PersonaManner.cs:27`) names the low or
  high trait.
- Cached voice: `VoicePrompt.Block` (`VoiceReading.cs:574`). It reads Confidence
  and Assertiveness for `NoStammer` (`VoiceReading.cs:615`); it does **not**
  read Directness.

The runner fixes one synthetic cached `VoiceSheet` for every condition. The
voice is not re-read between conditions.

## Prompt coverage

Dry run, no model call:

```text
pairs=10 changed=6
initiative               no-voice  changed=True
initiative               cached    changed=True
directness               no-voice  changed=True
directness               cached    changed=False
divergence_assertiveness no-voice  changed=True
divergence_assertiveness cached    changed=True
divergence_directness    no-voice  changed=True
divergence_directness    cached    changed=False
negative_control         no-voice  changed=False
negative_control         cached    changed=False
```

| Pair | Voice path | Prompt changed | Observed difference |
|---|---|---|---|
| Initiative | no cached voice | yes | `Her manner: takes the lead ...` vs `lets others lead` |
| Initiative | cached voice | yes | only the `NoStammer` sentence is added at step 9 |
| Directness | no cached voice | yes | `says what she means` vs `speaks in hints` |
| Directness | cached voice | **no** | identical system prompt |
| Divergence / Assertiveness | no cached voice | yes | manner line |
| Divergence / Assertiveness | cached voice | yes | `NoStammer` only |
| Divergence / Directness | no cached voice | yes | manner line |
| Divergence / Directness | cached voice | **no** | identical system prompt |
| Negative control | either | **no** | identical system prompt |

The **Directness cached-voice gap is the important result**: changing
Directness from step 2 to 9 cannot affect a reply that already has cached voice,
because neither the cached block nor the no-cached override names it. High
Assertiveness still adds the no-stammer rule; low Assertiveness adds nothing.

## Observed model output

**No local text model was available on this machine at run time.** No cloud
provider was used. The runnable runner supports a local Ollama-style endpoint at
`127.0.0.1` or `localhost`; it refuses every other host. It samples one reply per
condition by default and writes `results.json`/`results.md`.

Run:

```powershell
python tools/temperament-ab/run_temperament_ab.py --dry-run
python tools/temperament-ab/run_temperament_ab.py `
  --endpoint http://127.0.0.1:11434 --model happi-qwen35-9b --samples 1
```

## Scoring

The runner records, per reply:

- `initiative_or_next_step`: a concrete action or offered next step;
- `explicit_preference_or_answer`: a stated preference/answer;
- `generic_or_forced_question`: a generic follow-up that does not earn its place;
- `ends_with_question`: kept separately, because a concrete question can be good;
- `score = initiative + explicit - generic`.

These are lexical flags for triage, not semantic judgments. A manual audit is
still needed; small samples are directional only.

## Tests

```text
TemperamentPairedPromptTests: 4 passed, 0 failed, 0 skipped
Full EditMode run:            2169 passed, 1 failed, 2 skipped (2172 total)
```

The one full-run failure is the existing
`SpeechToTextTests.NativeLoad_ListsEditorAndPlayerPluginFolders` on this Windows
batchmode run; it is unrelated to this change.

The class pins the low/high manner words, the cached-voice NoStammer gap for
Directness, the Curiosity negative control, and that every non-target axis stays
at step 5 (stored 44). The Python dry run proves the ten prompt pairs and the
six prompt changes listed above.

## Limits

- One fixed persona and two-message history; no real user data.
- One sample per condition by default; the task allows a small fixed number.
- Prompt coverage is proven; live behavioral separation is not.
- The negative control checks that a non-target axis does not change the prompt,
  not that a model is deterministic across seeds.
- No production files, temperament weights, Ribbon, or Her Room were changed.
