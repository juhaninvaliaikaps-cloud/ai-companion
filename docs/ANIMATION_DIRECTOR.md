# Animation director

The director turns the cue the model returns into an Animator state and an emotion on the avatar. It is the layer between [the animation catalog](ANIMATION_CATALOG.md) and `AvatarAnimationController`.

## Cue format

The reply ends with one self-closing tag on its own line:

```text
That sounds fun. Let's go!

<animation emotion="happy" intensity="0.8" gesture="wave" />
```

| Attribute | Meaning | Default |
| --- | --- | --- |
| `emotion` | One of `neutral`, `happy`, `sad`, `surprised`, `thinking`. Unknown values make the cue invalid. | `neutral` |
| `intensity` | 0 to 1. Clamped by `AnimationCue`. | `0.6` |
| `gesture` | A catalog id such as `wave_hello`. Unknown ids are invalid, not errors. | none |

The parser is `AnimationCueParser` in `AICompanion.Core`. It returns the visible text with the tag removed and a `CueParseStatus` of `None`, `Parsed`, or `Invalid`. An invalid tag is still stripped, so a malformed cue can never reach the transcript.

The reminder text sent to the model is `AnimationCueParser.Instruction`, and the catalog rows come from `AnimationClipCatalog.BuildPromptList()`. `AnimationDirector.SystemPromptSuffix` joins the two; `ChatPanel` appends it to `ChatSettings.systemPrompt` before each request.

## Nothing leaks while streaming

The tag arrives at the end of a reply, but a partial `<animation` can arrive mid-stream. `CueStreamFilter` holds back text from the last `<` that could still become a tag and releases everything else immediately. Both the transcript and speech (`SpokenDelta`) receive only released text, so neither ever shows or speaks markup. On completion `Flush()` releases the rest and parses the cue.

## Decisions

`AnimationDirectorModel.Decide(AnimationCue)` is pure logic with no Animator, so the tests do not need a scene:

| Cue | Decision |
| --- | --- |
| Gesture the catalog knows, with an Animator state | `GestureState`: cross-fade to that state and set its emotion |
| Gesture the catalog knows, no state yet (`wave`, `nod`, `shrug`, `laugh` today) | `Emotion`: set the row's emotion and intensity |
| Gesture the catalog does not know | `Neutral`: emotion `neutral` at 0.4, state `Idle` |
| Emotion cue with no gesture | `Emotion`: the cue's emotion and intensity |
| No cue at all | `Neutral` |

`AnimationDirector` (the MonoBehaviour) applies the decision through `IAvatarAnimator.SetEmotion` and, for `GestureState`, `IAnimationStatePlayer.TryPlayState`. The state player returns false when the current controller has no such state, so a stale catalog row degrades to an emotion instead of a broken cross-fade.

`AvatarAnimationController` implements `IAnimationStatePlayer` with `Animator.HasState` + `CrossFade` on layer 0. Emotion-only cues stay on the parameter path so the controller's own transitions keep working.

## Wiring

`AnimationDirector` sits on the `AppBootstrap` object in `Main.unity` next to `ChatPanel`, `AvatarAnimationController`, and `VoiceSpeaker`, with `catalog` pointing at `Assets/_Project/Settings/AnimationCatalog.asset`. `ChatPanel` finds it with `GetComponent` and plays `ChatExchange.LastCue` when a reply completes successfully.

The cue is applied when the reply finishes, which is when the tag has arrived. Speech is already playing by then, so the face and gesture change with the spoken reply rather than with the first streamed word. A talking avatar returns to its emotion state when speech ends, because the parameter path still drives the talk state.

## Tests

`AnimationCueParserTests` (8), `CueStreamFilterTests` (5), and `AnimationDirectorTests` (6) cover the tag format, defaults, invalid tags, the streaming hold-back, and every decision row above. They run in EditMode and need no Animator.

## Limits

- No gesture row has an Animator state or clip yet, so today every gesture resolves to its emotion. The `GestureState` path is exercised by tests with a synthetic row.
- The cue is one tag for the whole reply. Per-sentence cues would need a different streaming contract.
- `speech` and the cue are independent: a muted or failed utterance still changes the face, because the cue arrives with the text rather than with the audio.
