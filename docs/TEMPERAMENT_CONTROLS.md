# Temperament controls: where the nine sliders go

JUH-217 evidence pass. Scope: the nine Profile → Personality controls, with the
intimate pack off. No production behaviour or provider calls were added.

## Short version

- The UI shows **1–10**, but storage and every runtime consumer use **0–100**.
  The display is only a rounded view.
- **Step 5 is 44, not 50.** A stored 50 is shown as step 6, and reselecting step
  6 does not rewrite it. The neutral middle cannot be selected with a step.
- The eight mood axes have a **deterministic** mood consumer. Curiosity does not.
- All nine axes reach the **voice reading prompt**. That is prompt-only; model
  behaviour is not measured here.
- Eight axes reach the **manner prompt** only at ≤30 or ≥70. Adjacent steps
  5→6 are silent; **7→8 (67→78) is the first adjacent pair that changes it**.
- `GameVoices.For` is a coarse threshold classifier. With one axis moved and a
  `friend` role, only Warmth=10 changes it (Sweet). Openness and Curiosity do
  not move it at all.
- Adjacent 5→6 changes mood multipliers by at most **0.108** (about 10%);
  it changes no manner, no game voice, and no threshold.

## Wiring

```text
Settings sliders
  HappiSettingsView.cs:838 (TemperamentSteps.ValueForStep)
    -> PersonaDefinition.SetAxes (PersonaDefinition.cs:158)
    -> PersonaJson "temperamentAxes" (PersonaDefinition.cs:533)
       -> load PersonaJson.Parse (PersonaDefinition.cs:561)
       -> old file: Axes = null, preset point used

Character sheet (Pisara reading)
  CharacterSheetPrompt.cs:23,35 asks for nine 0..100 axes
    -> CharacterSheet.temperament.axes (CharacterSheet.cs:269,274)

Runtime
  CompanionMoodKeeper.cs:288 PersonaRuntime.TemperamentChoiceFor
    -> PersonaCatalog.cs:603 PisaraReading.Choose
       -> TemperamentMood.From (TemperamentAxes.cs:446)       deterministic mood
       -> PersonaMannerNow.Set (CompanionMoodKeeper.cs:290)
          -> PersonaManner.Line (PersonaManner.cs:27)
          -> ChatPanel.Session.cs:62 appends to the prompt     prompt-only
    -> VoiceKeeper.cs:357-361 VoiceInput.Everyday
       -> VoiceReading.cs:430/483 includes every axis value    prompt-only
    -> GameCompanionController.cs:369 GameVoices.For
       -> GameReactions.cs:82                               deterministic game voice
```

`PisaraReadingKeeper` owns the sheet but does not read the axes itself.
`RelationshipKeeper` does not reference `TemperamentAxes` at all.

## Step mapping

`TemperamentSteps.cs:16`/`:25` uses `round(100 * (step - 1) / 9)`:

| Displayed | Stored |
|---:|---:|
| 1 | 0 |
| 4 | 33 |
| **5** | **44** |
| **6** | **56** |
| 7 | 67 |
| 8 | 78 |
| 10 | 100 |

`DisplayStep(50)` is **6**, and `IsNewStep(50, 6)` is false. This is a real
compatibility quirk, not a rounding detail.

## Consumer and measured values

Fixed inputs: all other axes at 50, `friend` relationship, no sheet. Values
shown as **step 1 / step 5 / step 10**; the adjacent pair is **step 5 → step 6**
and the threshold pair is **step 7 → step 8**.

| Control | Deterministic consumer | 1 (0) | 5 (44) | 10 (100) | 5→6 adjacent | Prompt-only |
|---|---|---|---|---|---|---|
| Warmth | `TemperamentMood.From` `TemperamentAxes.cs:446` | Affectionate gain **0.60** | **0.952** | **1.40** | 0.952→1.048; Manner none; GameVoice Cool→Cool | Manner low/none/high; voice includes `warmth` |
| Volatility | same | Annoyed gain + half-life **0.60 / 0.60** | **0.952 / 0.952** | **1.40 / 1.40** | 0.952→1.048; Manner none; GameVoice Cool→Cool | Manner low/none/high; voice includes `volatility` |
| Assertiveness | same | Flustered gain **1.30**, Annoyed gain **0.75** | **1.036 / 0.970** | **0.70 / 1.25** | Flustered 1.036→0.964; Manner none; GameVoice Cool→Cool | Manner low/none/high; voice includes `assertiveness` |
| Playfulness | same | Happy gain **0.80** | **0.976** | **1.20** | 0.976→1.024; Manner none; GameVoice Cool→Cool | Manner low/none/high; voice includes `playfulness` |
| Openness | same | Affectionate gain **0.88** | **0.9856** | **1.12** | 0.9856→1.0144; Manner none; GameVoice Cool→Cool | Manner low/none/high; **no GameVoice effect alone**; voice includes `openness` |
| Energy | same | Happy gain **0.80**, Happy half-life **0.70** | **0.976 / 0.964** | **1.20 / 1.30** | 0.976→1.024; Manner none; GameVoice Cool→Cool | Manner low/none/high; voice includes `energy` |
| Directness | same | Affectionate gain **0.55**, Flustered gain **1.25** | **0.946 / 1.030** | **1.45 / 0.75** | 0.946→1.054; Manner none; GameVoice Cool→Cool | Manner low/none/high; voice includes `directness` |
| Confidence | same | Anxious gain **1.35**, Flustered gain **1.25** | **1.042 / 1.030** | **0.65 / 0.75** | 1.042→0.958; Manner none; GameVoice Cool→Cool | Manner low/none/high + NotTimid at high; voice includes `confidence` |
| Curiosity | **none** | same tuning | same tuning | same tuning | no change | Only **VoiceInput** (`VoiceReading.cs:492`); Manner has no word; GameVoice ignores it |

All eight deterministic mood axes also change the **manner prompt** at 1 and 10
(low and high word respectively); 5 and 6 produce an empty line. At **7→8**
(67→78) the manner line changes for every one of the eight, because it crosses
`PersonaManner.High = 70`.

`GameVoices.For` with these fixed inputs: Warmth is the only single axis that
changes the voice (1=Cool, 5=Cool, 10=Sweet). Volatility, Assertiveness,
Playfulness, Energy, Directness and Confidence remain Cool when moved alone;
Openness and Curiosity have no GameVoice consumer. GameVoice is therefore a
multi-axis threshold surface, not a per-axis readout.

## Prompt-only evidence

`PersonaManner.Line` (the actual chat line):

```text
Warmth 1:  Her manner: cool and distant. Keep to this in every scene, ... 
Warmth 5:  (empty)
Warmth 10: Her manner: warm. Keep to this in every scene, ...
```

Confidence at 10 also appends `PersonaManner.NotTimid`. Curiosity produces no
manner line at any value.

`VoiceInput.Everyday` includes every exact value, for example:

```text
- Temperament sliders, 0 to 100 (50 is the middle): warmth 44, volatility 50, ...
```

Changing one axis from step 5 to step 6 changes that line (44→56) even when the
manner line and GameVoice stay the same. **The resulting model behaviour is
unmeasured.**

## Flags

1. **No deterministic consumer for Curiosity.** It is stored, displayed and
   sent to the voice prompt; it has no mood, manner or game effect.
2. **Openness has no GameVoice consumer.**
3. **Step 5 is 44, not 50.** The true neutral 50 is displayed as step 6 and
   cannot be selected by moving to step 6. A profile can also remain at a
   stored 50 that no step shows.
4. **Saturation hides adjacent steps.** The manner threshold is 30/70; 1→2 and
   2→3 are one side, 4→7 is silent, and only 7→8 and 3→4 (depending on axis)
   cross. The mood caps at `TemperamentMood.MaxMoodGain`/`MaxNegativeHalfLife`
   are approached at the extremes, not at 5→6.
5. **Default and legacy paths.** A persona without `temperamentAxes` loads its
   preset point, not 50s. A character sheet without axes falls back to the
   five-word blend; a sheet with only Curiosity off 50 also keeps the blend.
6. **100-scale compatibility is preserved.** No migration rewrites stored
   0–100 values; the 1–10 UI rounds display (`TemperamentSteps.cs:16`) and
   writes (`:25`). Older files keep their values and clamp to 0..100.
7. **Prompt-only does not mean effective.** The voice reading receives exact
   values, but no provider call or model response was measured here.

## Priority

1. Fix the **step 5 = 44** mismatch (or document that 5/10 is not neutral) and
   the unselectable 50. This affects every default/midpoint check.
2. Decide whether Curiosity needs a deterministic consumer or should be
   labelled prompt-only in the UI.
3. Add a multi-axis test for GameVoice thresholds; single-axis characterization
   is not enough to predict it.
4. Treat 5→6 as a cosmetic mood nudge, not a meaningful tuning step. Use 7→8
   or the extremes when evaluating manner changes.

## Tests

`Assets/_Project/Tests/EditMode/TemperamentControlTraceTests.cs` is the
dedicated characterization class. It pins the 1–10→0–100 mapping, the 5/6
middle pair, the mood fingerprints at 1/5/10, the manner 7→8 threshold, the
voice prompt values, GameVoice's coarse thresholds, and the persona round trip.

Run:

```text
unity command run_editmode_tests
```
