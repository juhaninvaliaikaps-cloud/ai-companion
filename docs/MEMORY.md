# Memory and personality

The companion keeps facts, conversation summaries, and its own persona on this machine and adds the relevant ones to the chat prompt. Nothing here is committed and nothing is stored next to the build.

## Files

Both live in the same per-user folder as the settings and the API key.

| File | Path | Content |
| --- | --- | --- |
| `memory.json` | `%APPDATA%\ProjectLeia\memory.json` | `{ version, entries[] }` |
| `personality.json` | `%APPDATA%\ProjectLeia\personality.json` | `{ version, personaName, mood, warmth, updatedUtc }` |

`MemoryPaths` resolves the folder: `%APPDATA%\ProjectLeia`, falling back to `Application.persistentDataPath` when the profile folder is unavailable. On disk the bytes are AES-GCM, not the JSON below. A missing, unreadable, or corrupt file behaves like an empty store; a failed write logs a warning instead of breaking chat. The JSON in this document is what you see after export. See [LOCAL_STORAGE.md](LOCAL_STORAGE.md).

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

Entries can also carry her world (JUH-117): `origin`, `horizon`, `happenedAtUtc`, `recalls`, `isThread`, `goodOutcome` and `badOutcome`. They default to an ordinary lived, long-term memory, so older files load unchanged. See [PISARA.md](PISARA.md#memory-and-her-world).

## Retrieval

### Memory links in Ribbon chat

Ribbon includes temporary references beside the facts and timeline entries selected for a conversational reply. When Happi uses a detail, she can identify the entry and quote the short phrase in her final animation cue. The app shows at most two links after the reply. A link requires a reference from that turn and a unique, exact phrase with a matching detail. Cue metadata stays out of the displayed or spoken reply and future chat prompts.

Hover a highlighted phrase or focus its message to preview the recalled detail. Activate the phrase to open the exact saved item in Profile → Memories. Saved chat keeps the local reference and phrase, with the current memory looked up again when used. A missing, closed, or inaccessible item has no active link. Private memory links are available only in Private mode.

### Relationship timeline retrieval (JUH-141)

The dated timeline (JUH-134) is searched separately from the facts, by `TimelineSearch.Rank`, at most 3 entries per turn under "Shared history".

- **Words.** Whole words only ("work" does not find "homework"), stop words dropped ("that", "with", "about"…), so small talk finds nothing. A light stemmer folds endings: promise/promised, sister/sisters, family/families, live/lives. The app is English only (Jussi, 2026-11-21), so there is no other language's stemming.
- **Concepts.** Groups such as family, work, fear, anger, joy, promise, boundary, home, travel, health, loss and pets add a concept term, so "my family" finds "her sister" at 0.6 of a direct word.
- **Ranking.** BM25 over this persona's open entries: a word that appears in few entries counts for more. The latest user message counts fully, the one before it at half weight, so a follow-up like "do you remember what I told you?" keeps its topic.
- **Trust.** The relevance is shaded by trust: repetition (`evidenceCount`, +10% per extra mention, up to +50%), the explicit "remember this" command (+20%) and the kind (boundaries and promises +15%) count for more than the model's own confidence, which moves it only ±10%. Importance and recency add a little.
- **Already raised.** An entry she brought up in the last 10 minutes stays only while the talk names it directly (2+ words), tagged "you already brought this up today; keep it in mind, but do not repeat it unprompted". A passing word does not bring it back. This replaced the 10-minute skip, which made her forget the topic mid-conversation.
- **Privacy.** Private entries never reach a cloud provider; resolved entries are not recalled.
- **Measured.** `TimelineSearchTests` is the acceptance set: real questions and the entry each must find first, and small talk that must find nothing. Tuning has to keep it green.

### Smarter search: meaning as well as words (JUH-141)

Off by default: **Settings → Her → Memories → Smarter memory search** (`CompanionSettings.MemoryEmbeddings`, `"memoryEmbeddings"` in the settings file). When it is on, the timeline search also matches by meaning, so "how is my old man holding up?" finds "his father is in hospital" with no shared word.

- **The model.** `nomic-embed-text` (about 274 MB) in the local Ollama, through `/api/embed` (`OllamaEmbedder`). Install it once with `ollama pull nomic-embed-text`. It is only ever called on Ollama on this PC (a loopback address on port 11434); any other base URL is refused before anything is sent. So private mode and 18+ memories are covered too.
- **Vectors.** Each open timeline entry gets a vector of `search_document: <summary>`; the query is `search_query: <latest message>` (with the previous message when the latest is short). The prefixes are what nomic-embed-text was trained with; Ollama does not add them. Vectors live in memory only (`EmbeddingIndex`, per persona). They are filled in the background, 64 entries per chat turn, and rebuilt after a restart. A corrected entry is embedded again. Nothing new is written to disk.
- **Ranking.** `TimelineSearch.Rank` takes the cosine of each entry. Above `SemanticFloor` (0.55) it adds up to `SemanticWeight` (3) to the word relevance. From `SemanticOnly` (0.68) an entry is found with no shared word. A just-raised entry stays while the meaning is at least `SemanticRaised` (0.78). These are first guesses for nomic-embed-text and are meant to be tuned on the Windows PC.
- **Falling back.** With the switch off, Ollama not running, the model not pulled, or a query vector slower than 3 seconds, the search is the word search, unchanged. A failure is logged once (`Memory: smarter search is using words only: …`, with the `ollama pull` hint), and the model is not asked again for 5 minutes.
- **Not yet.** Stored facts (`MemoryStore`) still use the word search; only the timeline uses meaning.

### Corrections, kept promises and real dates (JUH-141)

The session summary used to write blind: its request is `NoMemory`, so a correction ("her sister moved from Tampere to Helsinki") became a second, contradicting entry, and a promise stayed open forever. Now `SessionMemories.CaptureAsync` shows the summary what she already remembers (`SessionKnowledge`):

- **Shown:** up to 6 entries the conversation is about (ranked over the transcript, ignoring "already raised"), then open promises and threads, newest first, at most 8, as `[m1] (12.11.2026) [promise] …`. A summary going to a cloud model is never shown private entries. Plus "Today is Friday 20.11.2026", so a date is written as a date ("on Sunday 22.11.2026"), not "on Sunday". A resumed handoff uses the day the conversation happened.
- **Answered:** `update m1: <full new sentence>` corrects an entry; `resolved m2` closes a promise or thread that was kept, done or is no longer true. These lines are never stored as new memories.
- **Applied** by `RelationshipTimeline.ApplySession`: a correction must itself be durable and concrete (a vague line never replaces a concrete one); it keeps the entry's id and date, saves the old sentence in `previousSummary` with `updatedAtUtc`, counts as one more mention, and a correction from a private session makes the entry private. A resolved entry gets `resolvedAtUtc` and is no longer recalled. An id the summary was not shown, or an unknown one, changes nothing.
- Not yet: the fact store (`MemoryStore`) keeps its own older facts; only the timeline is corrected.

### Whose memory it is (JUH-141)

When the retrieved memories mix her own life (Pisara's `seeded`, `away` and `life` entries) with what she knows about the user, `MemoryPrompt.Build` puts them under two sub-headings: "About the user and your time together" and "Your own life (things that happened to you, not to the user)". A list of one kind stays flat. Timeline lines already start with "The user" or her name.

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

**Corrections (JUH-141).** The request also lists up to six stored facts the exchange is about, as `[f1] The user's sister lives in Tampere`: the identity facts first, then the user's facts ranked like the timeline (`TimelineSearch`, with stemming). Her own life and session summaries are not listed, and private facts are listed only to the local model. When the exchange changes one, the model answers `"update f1: The user's sister moved to Oulu"` in the same array, and `MemoryStore.Correct` replaces the sentence instead of keeping both. The old sentence stays in `previousText`, with `updatedAtUtc`. A correction made in private mode makes the fact private. A correction is refused when it shares no topic word with the old fact (so "the user likes green tea" cannot overwrite the sister), and an id that was not listed is ignored. When the corrected sentence is already stored, the old row is removed. Since the request carries its own list, it is sent with `NoMemory` and skips the memory layer.

The extraction call goes to the active provider. In private mode that is the local model, and if a cloud provider is somehow passed anyway, `CaptureAsync` returns before sending anything. Facts written in private mode carry `isPrivate` and are filtered out of cloud prompts later. The private flag is the one from the send, so turning 18+ off while a reply is still in flight does not store that turn as a public fact. An adult interaction line is also marked local-only in the transcript, and that flag is saved with the line. A later cloud chat request leaves that turn out. A local or private request still includes it. After the transcript is loaded again, a session that contains one of those lines stays local, so the session-end summary is not asked of a cloud model.

### Review fixes on the timeline (JUH-134)

- **The fact-extraction request is not her talking.** Its "user" message is the whole exchange (`User: … Assistant: …`), so `MemoryChatProvider.Enrich` neither searches nor injects nor marks timeline entries for it. Her reply can no longer restart an entry's recall cooldown, and old history is not extracted again as new facts.
- **"Remember that …" is kept as the user's words.** `MemoryCommands.TryExtract` takes the clause, and `MemoryCommands.Stored` saves it as `The user asked you to remember: "I hate pickles"`, both as a core fact and as a timeline entry. Saved as `I hate pickles`, it would read as her own dislike under "What you remember".
- **A recollection is not a command.** There is no bare `remember ` prefix any more; the forms are "remember that / this: / to", "don't forget" and "keep in mind". A clause that starts like a shared moment or a plea ("when", "how", "that time", "the night", "me", "us") is not stored: "Remember when we went to the beach" stays a sentence.
- **Scene words no longer drop real memories.** `MemoryDurability.IsDurable` treats body beats (trembled, blushed, heart raced, "for a moment") as scene state unless the line names something lasting (a family member, likes, hates, lives, works, died, a promise, a boundary). "Said" or "told" is not enough, so "She blushed when the user said she was pretty" still goes. Everyday words (smiled, looked at, silence, suddenly, felt nervous) only drop a short line under 7 words: "The user's father suddenly died last year", "She hates long silences" and "The user looked at apartments in Tampere" are kept; "The user smiled at her warmly" is not.
- **One timeline per persona in memory.** `RelationshipTimeline.ForStore` returns the same instance while its file is unchanged (write time and size), and reads it again after anything else wrote it (a wipe, an import, a test's own instance). Chat no longer decrypts up to 1000 entries twice per exchange, and a request that loaded before a session ended can no longer save over that session's new entries. `RelationshipTimeline.Load` still gives a fresh instance.
- **One handoff file per session.** The session-end request is written to `pending-session-<session>.json`. The rolling checkpoint and the final save of the same session share that file, so the next session's checkpoint can no longer overwrite a session whose summary is still running. On start (and after settings are applied) `ChatPanel.ResumePendingSession` finishes every waiting file, oldest first, including the old single `pending-session.json`. It skips the running session's own checkpoint and a session whose summary is already being written. A resumed session uses the live memory store when it belongs to the same persona.

## Prompt wiring

`MemoryChatProvider` wraps every provider the factory registers (`ChatProviderFactory.Create(..., decorator)`) and only touches `ChatRequest.SystemPrompt`:

```text
<existing system prompt: the selected persona, or the setup name, plus the animation catalog and cue instructions>

What you remember from earlier sessions:
- Likes coffee with oat milk
- Has a dog named Pixel
```

Messages, model, temperature, and token limit are copied unchanged. When nothing matches, the original request instance is returned untouched.

Since JUH-118 a memory that says when it happened is written as `- (3 days ago) ...`, the entries that went into a prompt are counted as recalled (a short-term memory that came up twice becomes long-term), and a request with `ChatRequest.NoMemory` is passed through untouched. Her own life (seeded and away memories) and the session memories are described in [PISARA.md](PISARA.md), "Her world as wired".

The persona text comes from the selected persona file when `personaId` is set, and otherwise from the first-run wizard (`SettingsApplicator.ApplyPersona`). The director cue instructions are already on that prompt before memories are added. `MemoryRuntime` passes no extra persona into the decorator: the two layers would otherwise restate the same name and mood. `MemoryChatProvider` still accepts a `PersonalityStore` for callers that have no wizard prompt, which is what the persona test exercises. See [PERSONAS.md](PERSONAS.md).

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
- The files are encrypted at rest (AES-256-GCM, key in `install-key.leia`). See [LOCAL_STORAGE.md](LOCAL_STORAGE.md).
- A very large `memory.json` is read whole on start; pruning is not implemented.
