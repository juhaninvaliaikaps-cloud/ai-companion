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
| `gesture` | A catalog id (`wave_hello`) or that row's gesture name (`wave`). The director tries the id first. An unknown value is still parsed; the director then plays neutral. | none |

The parser is `AnimationCueParser` in `AICompanion.Core`. It returns the visible text with every cue tag removed and a `CueParseStatus` of `None`, `Parsed`, or `Invalid`. Quotes may be double or single. An invalid or unclosed tag is still stripped. Text that follows a closed tag is kept. A word such as `<animations>` is not a cue tag.

The reminder text sent to the model is `AnimationCueParser.Instruction`, and the catalog rows come from `AnimationClipCatalog.BuildPromptList()`. `AnimationDirector.SystemPromptSuffix` joins the two; `ChatPanel` appends it to `ChatSettings.systemPrompt` before each request.

## Nothing leaks while streaming

The tag arrives at the end of a reply, but a partial `<animation` can arrive mid-stream, or be split across chunks. `CueStreamFilter` holds back the earliest suffix that could still become a cue tag, and it removes a tag as soon as that tag has closed, even if the next chunk contains another `<`. Both the transcript and speech (`SpokenDelta`) receive only released text. On completion `Flush()` releases a suffix that was not a tag and parses the cue. The sealed assistant line is replaced with that parsed text, so a tag cannot remain in the next request. A provider that returns the whole reply without deltas is parsed the same way.

## Decisions

`AnimationDirectorModel.Decide(AnimationCue)` is pure logic with no Animator, so the tests do not need a scene:

| Cue | Decision |
| --- | --- |
| Gesture id or gesture name of a row that has an Animator state (`wave` / `wave_hello` once a clip exists) | `GestureState`: cross-fade to that state and set its emotion |
| Known id or gesture name with no gesture state (`wave`, `nod`, `shrug`, `laugh`, and emotion ids such as `happy`) | `Emotion`: the row's emotion and intensity. Emotion rows stay on the parameter path |
| Gesture the catalog does not know | `Neutral`: emotion `neutral` at 0.4, state `Idle`. The cue's own emotion is not used |
| Emotion cue with no gesture | `Emotion`: the cue's emotion and intensity |
| No cue at all | `Neutral` |

`AnimationDirector` (the MonoBehaviour) applies the decision through `IAvatarAnimator.SetEmotion` and, for `GestureState`, `IAnimationStatePlayer.TryPlayState`. The state player returns false when the current controller has no such state, so a stale catalog row degrades to an emotion instead of a broken cross-fade. `SetEmotion` is what moves the face: `EmotionFace` writes the smile, frown, and brow blendshapes from that emotion. A gesture row with no Animator state still calls `SetEmotion`, so the row's emotion changes the face. An emotion cue such as `happy` does the same. `wave` does not need a clip for that.

Each `Play` from a finished reply logs one line. The gesture token is the catalog id. The reply text is not logged.

```text
AnimationDirector: cue wave -> played
AnimationDirector: cue wave -> no animator state, skipped
AnimationDirector: emotion happy -> applied
AnimationDirector: no cue -> neutral
```

`AvatarAnimationController` implements `IAnimationStatePlayer` with `Animator.HasState` + `CrossFade` on layer 0. Emotion-only cues stay on the parameter path so the controller's own transitions keep working.

## Wiring

`AnimationDirector` sits on the `AppBootstrap` object in `Main.unity` next to `ChatPanel`, `AvatarAnimationController`, and `VoiceSpeaker`, with `catalog` pointing at `Assets/_Project/Settings/AnimationCatalog.asset`. `ChatPanel` finds it with `GetComponent` and plays `ChatExchange.LastCue` when a reply completes successfully.

The cue is applied when the reply finishes, which is when the tag has arrived. Speech is already playing by then, so the face and gesture change with the spoken reply rather than with the first streamed word. A talking avatar returns to its emotion state when speech ends, because the parameter path still drives the talk state.

## Tests

`AnimationCueParserTests`, `CueStreamFilterTests`, `AnimationDirectorTests`, and `CueTranscriptTests` cover the tag format, split chunks, a later `<`, text after a closed tag, catalog ids, the console line, and the sealed transcript. They run in EditMode and need no Animator. The system-prompt test checks that `SystemPromptSuffix` still contains `AnimationCueParser.Instruction` and the `<animation ... />` example.

## Limits

- No gesture row has an Animator state or clip yet, so today every gesture resolves to its emotion. The `GestureState` path is exercised by tests with a synthetic row. A cross-fade on the base layer fights the emotion AnyState transitions; see [ANIMATION_REVIEW.md](ANIMATION_REVIEW.md).
- The cue is one tag for the whole reply. Per-sentence cues would need a different streaming contract.
- `speech` and the cue are independent: a muted or failed utterance still changes the face, because the cue arrives with the text rather than with the audio.
