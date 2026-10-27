# Animation catalog and director review

Review of the catalog (JUH-14) and director (JUH-15) as merged to `main`. Fixes landed with the regression tests in this branch. The items below were left as they are.

## Documented, not changed

### Base-layer cross-fade fights emotion AnyState transitions

`Companion.controller` enters Idle, Happy, Sad, Surprised, and Thinking from AnyState when the `Emotion` integer changes. `AnimationDirector.Apply` sets that parameter and, for a gesture row that names a state, also calls `TryPlayState` on layer 0. On the next animator tick the AnyState transition leaves the gesture state. `canTransitionToSelf` is false, so the gesture does not stay.

No starter gesture row has an animator state yet, so this does not play out today. Emotion ids (`happy`, `sad`, and the others) stay on the parameter path on purpose and are not cross-faded. A gesture clip on layer 0 would still be interrupted. The open gesture work puts those clips on their own override layer and passes that layer into `TryPlayState`. This branch does not change that method's signature.

`Talk` is a state on the additive Talk layer, not the base layer. `HasState(0, "Talk")` is false. The `talk` catalog row has no gesture, so a cue of `gesture="talk"` sets neutral at 0.3 and does not cross-fade. The row is still in the system prompt, so the model can spend its one cue on it and pull the body back to Idle. Hiding `talk` from `BuildPromptList` is a product choice and was not done here.

### One cross-fade per reply, always from the start

`CrossFade` uses normalized time 0. A second cue for the same state restarts it. That is one call per finished reply, not a per-frame loop. There is no priority between a gesture and a debug-key emotion change in the same frame.

### Cue before the avatar exists, or after a swap

`SetEmotion` stores the emotion on `AvatarAnimatorModel`, and `Attach` pushes parameters again, so a face emotion survives the avatar loading. `TryPlayState` returns false while the `Animator` is missing and the gesture is not replayed. `AnimationDirector` treats a destroyed avatar component as missing and will not call into it. It does not queue the gesture for the next `Attach`.

### User text is not a cue

Only the assistant stream is parsed. A `<animation ...>` tag typed or dictated by the user stays in the user turn and is sent to the model as dialogue. It does not play. If the model copies it into the reply, the copy is parsed like any other cue. Catalog descriptions are inserted into the system prompt as plain text; validation now rejects line breaks, and it does not try to make a description safe against prompt injection. The catalog is a local asset.

### Known-gesture intensity comes from the row

A cue that names a catalog row uses that row's emotion and intensity. `intensity` on the tag applies only when the tag has no gesture. An unknown gesture still discards a valid emotion and plays neutral. Both match the decision table; they are easy to misread from the tag format alone.

### Unclosed tag drops the rest of the reply

A closed tag is removed and the surrounding text stays. An unclosed `<animation` with no `>` is treated as running to the end of the reply, so prose after a forgotten `>` is removed with the tag. Attributes cannot contain `>` or a raw `<`.

### Threading

Streaming chunks arrive from `UnityWebRequest` on the player loop. `ChatPanel.Dispatch` applies the cue after the provider task, on Unity's synchronization context. The director does not call the `Animator` from a worker. A transport that invoked `onDelta` off the main thread would already have been unsafe for the chat UI and speech; this review did not add a marshal.

### Speech, camera, and push-to-talk

The cue is applied when the reply finishes, while speech may still be playing. Muting speech does not mute the cue. Nothing in this path reads the camera, the framing zoom, or the microphone. Push-to-talk submits ordinary user text through the same send path, so a dictated sentence is not parsed as a cue.

### CI on main

The Tests run for the JUH-15 merge (`40409114445`, commit `c51859b`) failed before EditMode or PlayMode executed. Both jobs logged `Machine bindings don't match` while activating `UNITY_LICENSE`, then fell back to the Unity account and exited without a test list. The previous main run, the catalog merge, was green. That license failure is infrastructure, not a test failure in the director.

The same activation failure hit EditMode on this branch (`40589295066`). The log shows `Machine bindings don't match`, then the editor exits 137 before a test list. PlayMode on that run did execute: 3 passed, 0 failed. The new EditMode tests were not run by CI.

## Tests that are still manual

Avatar cross-fades, a missing humanoid, and the transparent window are not covered by EditMode. The decision logic, the stream filter, and the sealed transcript are.
