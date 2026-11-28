# Chat providers

`IChatProvider` is the only type the rest of the app needs. `StreamChat` takes a system prompt, the messages, the model, temperature, and a token limit, then calls back with each piece of the reply. `ChatProviderRegistry` maps the id on `ChatSettings` to a provider. Callers do not switch on the id. Adding a provider is a new class plus one `Register` call in `ChatProviderFactory`.

Two ids ship today:

| Id | What it calls |
| --- | --- |
| `grok` | The **Cloud AI API** slot (JUH-143): any cloud API, chosen by the base URL. xAI by default: `https://api.x.ai/v1/chat/completions` (OpenAI-compatible, SSE), model `grok-4.7`. The id stays `grok` so saved settings keep working. |
| `local` | The same protocol at `localBaseUrl`. The default is Ollama's OpenAI endpoint, `http://127.0.0.1:11434/v1`. LM Studio is usually `http://127.0.0.1:1234/v1`. This id does not send an API key. |

Change the id, model, base URL, timeout, temperature, max tokens, or system prompt on `Assets/_Project/Settings/ChatSettings.asset`. That asset holds no secret.

**Thinking models on the local route (JUH-238).** Local chat goes to Ollama's native `/api/chat` (`OllamaChatProvider`), which keeps `num_ctx`. Every request sends `"think": false`. Without it, Ollama 0.40 lets a model with the thinking capability think first, and the hidden thinking used the whole reply budget: on this PC, `happi-qwen35-9b`, `happi-defiant-9b` and `happi-gemma12b-heretic` all returned no visible text (17–19 s for 512 tokens with `happi-qwen35-9b`). A model that can't think, such as Rocinante 12B, accepts the field and answers the same.

The endpoint is the legacy Chat Completions API, which is what xAI documents as OpenAI-compatible. Visible reply text is `delta.content`. Reasoning text (`reasoning_content`) is not shown.

A one-frame window look is the exception. When the cloud AI API's model reads pictures and cloud screen consent is on, the image goes to it (JUH-149; see Screen look below). For xAI Grok that request uses the Responses API (`/v1/responses`) because that is the image path the current xAI image-understanding guide shows for `grok-4.7`. It sends one JPEG, sets `store` to false, and does not keep the image in the transcript. When chat is local, or private mode is on, the JPEG goes only to a separate Ollama vision model on loopback. Rocinante then receives the description as text. Details and the opt-in are in [SCREEN_LOOK.md](SCREEN_LOOK.md).

## Set the xAI key

The key is never stored in the repository. Resolution order:

1. Environment variable `XAI_API_KEY`.
2. `%APPDATA%/ProjectLeia/secrets.json` (on Windows, `C:\Users\<you>\AppData\Roaming\ProjectLeia\secrets.json`).
3. `Application.persistentDataPath/ProjectLeia/secrets.json`, if the file above is missing.

On Windows the file is encrypted with DPAPI for the current user (`CryptProtectData`). On a machine where DPAPI is not available, including the Linux editor, the same file is plain JSON with `"protected": false`. A Windows player does not write that plaintext file if protection fails. A key saved with DPAPI on Windows does not decrypt on Linux. An environment variable wins over either file. Other private files in the same folder use a separate AES key; see [LOCAL_STORAGE.md](LOCAL_STORAGE.md).

The value is not written to the Console, to exception text, or to the chat reply.

### In the Unity editor

1. Open this project in Unity 6000.6.6f1.
2. **AI Companion > Set xAI API Key**. Paste the key and choose **Save**. The window shows the file path. The field clears after a save.
3. Or set `XAI_API_KEY` in the environment before you start the editor. Restart the editor after changing it.

**Remove saved key** deletes the per-user file. It does not unset the environment variable.

### In the Windows player

1. Build and run `Builds/Windows/Happi.exe`.
2. Either set `XAI_API_KEY` for that process before launch, or press **F10** and use the password field on the chat panel. **Save** writes the same per-user file and clears the field.
3. Click the panel so the window is in front, then follow [CHAT_UI.md](CHAT_UI.md).

## Try a reply

The chat panel is documented in [CHAT_UI.md](CHAT_UI.md). Short version:

1. Open `Assets/_Project/Scenes/Main.unity` and enter Play mode, or run the Windows player.
2. Press **F10** if the panel is hidden. Type a message and press **Enter**. **F9** cycles the expression portrait and does not send the draft.
3. The reply streams into the panel. The Console logs `Chat reply:` when it finishes. It does not log each chunk.
4. **Esc** or **Stop** cancels the request that is in flight.
5. A failure is a **Note** in the panel. The Console gets a warning with the HTTP status and the provider's message (`Chat error: HTTP 403 from Cloud AI: ...`). The note shows a shorter line, or a hint when the cause is clear: `Invalid API key`, `xAI account has no API credits or billing is not set up (console.x.ai > Billing)`, `Model grok-4.7 not available for this key; change ChatSettings.model`, `Rate limited, try again shortly`, `Couldn't reach Cloud AI`, or `Cloud AI took too long to answer`. The log never includes the API key.

## The Cloud AI API slot (JUH-143)

Jussi (2026-11-21): the user can use whatever cloud API they want, and the interface calls the slot **"Cloud AI API (Grok, Claude, DeepSeek etc.)"** (`ChatDefaults.CloudName`). `CloudChatProvider` reads the base URL on every request and picks the API:

| Base URL host | API | Notes |
| --- | --- | --- |
| `api.anthropic.com` | Claude's Messages API, streamed (`AnthropicChatProvider` with `Stream`) | `x-api-key` and `anthropic-version` headers; `"stream": true`; text from each `content_block_delta` / `text_delta`. Set a Claude model name, such as `claude-sonnet-5-5`. |
| `*.deepseek.com` | OpenAI-compatible | Thinking is switched off (`"thinking":{"type":"disabled"}`), as for the Pisara reading. |
| anything else (xAI by default, OpenAI, OpenRouter, Groq…) | OpenAI-compatible Chat Completions | `Authorization: Bearer`. |

- **One key.** The saved cloud key (and `XAI_API_KEY`) is sent only to the host in the base URL. The key file did not move.
- **Voice.** The xAI cloud voice is only used when the base URL is xAI (`VoiceRoute.HasXaiVoice`), so a Claude or DeepSeek key is never sent to xAI. With any other cloud API she speaks with the local voice.
- **Screen look (JUH-149).** Any cloud API whose model reads pictures gets the window image. `VisionCapability.CloudModelAcceptsImages` decides it, using the AI tab's **Cloud model can see pictures** setting:
  - **Auto** uses the known list: `grok-4.7` and `grok-4`; `deepseek-flash` and the older DeepSeek vision preview; Claude 3 and newer; `gpt-4o`, `gpt-4.1`, `gpt-5`, `o3`, `o4-mini`; Gemini; Pixtral; Llama 4; Qwen-VL. A provider prefix like `openai/` is ignored.
  - **Yes** trusts any model.
  - **No** never sends a picture.

  The request shape depends on the host:
  - xAI: the Responses API, as before.
  - Other OpenAI-compatible APIs (DeepSeek, OpenAI, OpenRouter and others): chat completions with an `image_url` data URL (`VisionChatJson`), not streamed, with DeepSeek's thinking off.
  - Anthropic: a base64 `image` block on the last user message. The copy of that JPEG is cleared after the body is built, and also when the request stops first because the API key or the transport is missing.

  With a model that can't read pictures, the screen look uses the local vision model when one is set (`ScreenLookPolicy.UseLocalVision`); without one it is refused and nothing is sent. `CloudChatProvider` wipes and refuses an image for such a model before anything is sent.
- **Backstop.** A request marked `BlockCloud` (private, 18+ and observation turns) is refused by the cloud slot before any provider sees it. Routing already keeps those requests local; this only catches a mistake.
- **Connection test.** `ConnectionProbe.AuthHeaders` sends Claude's headers to `api.anthropic.com/v1/models` and a Bearer token everywhere else.
- **Usage policies.** Each provider has its own. Anthropic's usage policy forbids erotic chat. That is already the app's rule: 18+ content only runs on the local model, and private mode never reaches any cloud provider.

Error messages name the slot "Cloud AI" ("Cloud AI took too long to answer").

**How long a reply may take (JUH-183).** The timeout (120 s by default) counts the time since the last data arrived, not since the request started. A slow reply that keeps arriving finishes, for example a 12B local model sharing the GPU with Chatterbox. A server that stops answering still times out after the same time, and the wait for the first word is the same as before. A whole request is capped at 15 minutes (`StreamTimeout`). The last-data time is a stopwatch stamp (`StreamTimeout.Clock`). The download callback may run off the main thread, so it records that stamp with no Unity API, and the waiting loop reads it with an interlocked read. Player.log says `Chat transport: nothing arrived for 120 s in the middle of the reply; stopped` when it happens.

## Add a provider

1. Implement `IChatProvider`, or configure another `OpenAiCompatibleChatProvider` if the server speaks Chat Completions with SSE.
2. `Register` it in `ChatProviderFactory.Create`.
3. Set `providerId` on `ChatSettings` to that id.

`ChatPanel` calls `registry.TryGet(settings.providerId)`. Later UI should keep doing that. It should not grow a switch.

## Tests

`ChatProviderTests` feeds a fake transport. It does not open a socket and does not need `XAI_API_KEY`. `ChatConversationTests` does the same for the transcript. CI stays green without a key. Speech uses the same key and is described in [VOICE.md](VOICE.md).
