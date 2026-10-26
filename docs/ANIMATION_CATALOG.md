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

A row may have a gesture and no state, or a state and no gesture. Both are valid; the director falls back to the emotion when a gesture is unknown, and to idle when neither resolves.

## Starter entries

Ten rows ship with the project: six for the Animator states the controller already has (`idle`, `happy`, `sad`, `surprised`, `thinking`, `talk`) and four gestures that wait for their clips (`wave_hello`, `nod_yes`, `shrug`, `laugh`). The four gesture rows have an empty `animatorState` and a null `clip`; they exist so the model can already ask for them, and so the director has ids to resolve once the clips arrive.

**AI Companion > Animation Catalog > Rebuild Starter Catalog** rewrites the asset from `AnimationCatalogMenu.StarterEntries()`, which is where the ten rows are defined in code. Use it after an accidental delete. It also reloads clips from `Assets/_Project/Animations`, so a renamed clip shows up as a null reference on the row instead of a silent mismatch.

## Prompt export

`BuildPromptList()` returns one line per entry with an id, in list order, for the system prompt:

```text
- idle: calm resting pose; the director returns here when nothing else plays; emotion: neutral; tags: mood, idle
- wave_hello: friendly hello wave; gesture: wave; emotion: happy; tags: greeting, friendly
```

Rows without an id are skipped, so a half-filled draft cannot reach the model. The format is plain text on purpose: no JSON braces to escape, and the model sees the same wording a human reads in the inspector. **AI Companion > Animation Catalog > Log Prompt List** prints the current list and any validation problems to the Console.

## Tests

`Assets/_Project/Tests/EditMode/AnimationCatalogTests.cs` covers the prompt format, the id, gesture, and emotion lookups, and validation of duplicate ids and missing descriptions. One test loads the committed asset itself and checks ten entries, a valid catalog, resolved clips on the `idle` row, and the `wave` gesture lookup, so a broken asset fails the suite instead of failing at runtime.

## Not in this ticket

The director that consumes these rows is JUH-15. Chat providers do not emit cues yet, so nothing calls the catalog at runtime today; the prompt list is ready for the provider and the lookups are ready for the director.
