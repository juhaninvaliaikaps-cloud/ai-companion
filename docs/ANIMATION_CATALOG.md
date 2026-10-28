# Animation catalog

`AnimationClipCatalog` is the data-driven list of animations the companion can play. One entry is one row: an id the model can name, a short description, tags, an emotion, an intensity, and the gesture and Animator state the director resolves it to. Adding an animation is a new entry, not a code change and not retraining.

The asset is `Assets/_Project/Settings/AnimationCatalog.asset`. The type is in `AICompanion.AnimationDirector` (`Assets/_Project/Scripts/AnimationDirector/AnimationClipCatalog.cs`), the row type is `AnimationEntry` in the same folder, and the menu that rebuilds the asset is `AnimationCatalogMenu` in `Assets/_Project/Editor/AnimationDirector/`.

## Why a ScriptableObject

A JSON file would need its own parser and a path lookup at runtime. The catalog is an editor asset that the scene references directly, so a build carries the same rows without a file read, and Unity's own inspector edits it. The cost is that the catalog is not hot-reloadable at runtime; a companion that swaps animations from disk is a later feature, and the entry shape would survive that move.

## Fields

| Field | Meaning |
| --- | --- |
| `id` | Stable name the model uses, for example `wave_hello`. Lookups ignore case and surrounding space. |
| `description` | One short clause that tells the model when to use the row. It goes into the prompt list unchanged. |
| `tags` | Free-form labels for search and for the prompt list. Not used by the director yet. |
| `emotion` | The `Emotion` enum value the cue should use. |
| `intensity` | 0 to 1. Clamped by the cue, validated here. |
| `gesture` | The gesture name a cue may carry, for example `wave`. Empty for a pure emotion row. |
| `animatorState` | The Animator state name the director will cross-fade to. Empty while the clip does not exist. |
| `clip` | Optional direct reference, used for authoring and for the inspector. The director addresses states by name. |
| `durationSeconds` | Planned length. `0` means the clip is not ready yet. |

A row may have a gesture and no state, or a state and no gesture. Both are valid. The director matches a cue's gesture string to an id first and then to a gesture name. A known row without its own gesture state uses that row's emotion. An unknown gesture plays neutral, not the emotion written on the tag.

## Starter entries

Fourteen rows ship with the project: six for the Animator states the controller already has (`idle`, `happy`, `sad`, `surprised`, `thinking`, `talk`), four gestures that wait for their clips (`wave_hello`, `nod_yes`, `shrug`, `laugh`), and four face-only emotions (`embarrassed`, `shy`, `flustered`, `concerned`). The gesture rows and the face-only rows have an empty `animatorState` and a null `clip`. The face weights for those emotions live in [EXPRESSIONS.md](EXPRESSIONS.md), not in a new controller state.

**AI Companion > Animation Catalog > Rebuild Starter Catalog** rewrites the asset from `AnimationCatalogMenu.StarterEntries()`, which is where the starter rows are defined in code. Use it after an accidental delete. It also reloads clips from `Assets/_Project/Animations`, so a renamed clip shows up as a null reference on the row instead of a silent mismatch.

## Prompt export

`BuildPromptList()` returns one line per entry with an id, in list order, for the system prompt:

```text
- idle: calm resting pose; the director returns here when nothing else plays; emotion: neutral; tags: mood, idle
- wave_hello: friendly hello wave; gesture: wave; emotion: happy; tags: greeting, friendly
```

Rows without an id are skipped, so a half-filled draft cannot reach the model. The format is plain text on purpose: no JSON braces to escape, and the model sees the same wording a human reads in the inspector. **AI Companion > Animation Catalog > Log Prompt List** prints the current list and any validation problems to the Console.

## Tests

`Assets/_Project/Tests/EditMode/AnimationCatalogTests.cs` covers the prompt format, the id, gesture, and emotion lookups, and validation of duplicate ids, duplicate gestures, cue tokens, prompt line breaks, and a gesture that collides with another row's id. One test loads the committed asset itself and checks ten entries, a valid catalog, resolved clips on the `idle` row, the `wave` gesture lookup, and that every animator state name exists in `Companion.controller`.

## Consumers

`AnimationDirector` resolves cues through `TryGetByGesture`, and `ChatPanel` appends `BuildPromptList()` to the chat system prompt, so the catalog is live at runtime. See [ANIMATION_DIRECTOR.md](ANIMATION_DIRECTOR.md). The four gesture rows still have no Animator state or clip, so they currently resolve to their emotion.
