# Memory and personality

The companion keeps facts, conversation summaries, and its own persona on this machine and adds the relevant ones to the chat prompt. Nothing here is committed and nothing is stored next to the build.

## Files

Both live in the same per-user folder as the settings and the API key.

| File | Path | Content |
| --- | --- | --- |
| `memory.json` | `%APPDATA%\ProjectLeia\memory.json` | `{ version, entries[] }` |
| `personality.json` | `%APPDATA%\ProjectLeia\personality.json` | `{ version, personaName, mood, warmth, updatedUtc }` |

`MemoryPaths` resolves the folder: `%APPDATA%\ProjectLeia`, falling back to `Application.persistentDataPath` when the profile folder is unavailable. A missing, unreadable, or corrupt file behaves like an empty store; a failed write logs a warning instead of breaking chat.

## Entries

```json
{
  "version": 1,
  "entries": [
    {
      "id": "b3f1...",
      "text": "Likes coffee with oat milk",
      "kind": 0,
      "createdAtUtc": "2026-10-28T09:12:44.114Z",
      "isPrivate": false
    }
  ]
}
```

`kind` is `0` for a fact and `1` for a conversation summary. `isPrivate` records that the entry was created while private mode was on.

## Retrieval

`MemoryStore.Search(query, max, includePrivate)` is keyword search: the query is lowercased, split on non-alphanumeric characters, words shorter than three characters and a small stop-word list are dropped, and every remaining token that appears in an entry scores a point. A summary scores two, because it carries more context. Ties keep the newest entry. At most eight entries come back by default.

The chat path uses `MemoryStore.SearchFor(...)`, which is built on top of that:

1. **Core facts first** — entries flagged `isCore` (a name, how the user wants to be addressed). They always go into the prompt, so "what is my name?" works even though the stored sentence may never contain the word the user typed. At most three.
2. **Keyword hits** next, as above.
3. **Recent fallback** — when nothing matches, the newest entries are used instead of sending an empty memory block.

Embeddings are a later step; `Search`/`SearchFor` is the seam an embedding implementation would replace.

## Automatic capture

`ChatPanel.CaptureMemory` runs after every successful reply where the user wrote at least three words. It calls `MemoryCapture.CaptureAsync` with the same provider that answered:

1. `MemoryFactExtractor.BuildExchangeText` packs the user text and the reply into one message.
2. The request uses `MemoryFactExtractor.SystemPrompt`, which asks for a JSON array of short third-person facts and nothing else.
3. `MemoryFactExtractor.Parse` finds the array in the reply — prose or a code fence around it is fine — and rejects anything that is not a quoted list.
4. Each fact is stored with `MemoryStore.Add`, which skips text that already exists ignoring case, and flags identity facts (name, "call me") as core.
5. A log line reports only the count: `Memory: saved 2 fact(s)`.

The extraction call goes to the active provider. In private mode that is the local model, and if a cloud provider is somehow passed anyway, `CaptureAsync` returns before sending anything. Facts written in private mode carry `isPrivate` and are filtered out of cloud prompts later.

## Prompt wiring

`MemoryChatProvider` wraps every provider the factory registers (`ChatProviderFactory.Create(..., decorator)`) and only touches `ChatRequest.SystemPrompt`:

```text
<existing system prompt: the setup wizard persona plus the animation catalog>

What you remember from earlier sessions:
- Likes coffee with oat milk
- Has a dog named Pixel
```

Messages, model, temperature, and token limit are copied unchanged. When nothing matches, the original request instance is returned untouched.

The persona text itself comes from the first-run wizard (`SettingsApplicator.ApplyPersona` builds it from `CompanionSettings`). `MemoryRuntime` therefore passes no persona into the decorator: the two layers would otherwise restate the same name and mood. `MemoryChatProvider` still accepts a `PersonalityStore` for callers that have no wizard prompt, which is what the persona test exercises.

## Private mode

`isPrivate` entries are created with `AddFact(text, privateMode: true)`. Retrieval excludes them whenever the wrapped provider is not the local one: `MemoryChatProvider.IsLocal` is true only for `ChatDefaults.LocalId`. Private mode already routes chat to the local provider (`OutboundPrivacy.ChatProviderId`), so private memories can be used locally and can never reach Grok or another cloud endpoint, even if the provider id changes later.

## API

```csharp
var store = MemoryStore.CreateDefault();          // %APPDATA%\ProjectLeia\memory.json
store.AddFact("Likes coffee", privateMode: false);
store.AddSummary("Talked about the move to Tampere", privateMode: false);

IReadOnlyList<MemoryEntry> all = store.Entries;   // list
bool removed = store.Delete(entryId);             // delete one
int wiped = store.DeleteAll();                    // delete everything

IReadOnlyList<MemoryEntry> hits = store.Search("coffee", max: 5, includePrivate: false);

var personality = PersonalityStore.CreateDefault();
personality.SetMood("cheerful");
personality.SetPersona("Leia", 0.7f);
```

`MemoryRuntime` holds the process-wide store, personality, and decorator, loaded on first use. A settings or memory panel is a later ticket; this PR ships the data layer and the API only.

## Limits

- Keyword search plus core facts, not embeddings. Paraphrases still miss: "java" does not find "coffee", though a core fact such as the user's name is always present.
- Capture runs after each reply and costs one extra small request. Short messages (fewer than three words) are skipped. There is no per-session summary yet; only facts are extracted, and a later ticket can add summaries through `AddSummary` with the private flag.
- The extraction prompt sees the exchange only. If the model returns prose instead of a JSON array, that exchange contributes nothing.
- The files are plain JSON. Encryption is Phase 3 (`JUH-33`).
- A very large `memory.json` is read whole on start; pruning is not implemented.
