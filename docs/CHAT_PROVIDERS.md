# Chat providers

`IChatProvider` is the only type the rest of the app needs. `StreamChat` takes a system prompt, the messages, the model, temperature, and a token limit, then calls back with each piece of the reply. `ChatProviderRegistry` maps the id on `ChatSettings` to a provider. Callers do not switch on the id. Adding a provider is a new class plus one `Register` call in `ChatProviderFactory`.

Two ids ship today:

| Id | What it calls |
| --- | --- |
| `grok` | `https://api.x.ai/v1/chat/completions` (OpenAI-compatible, SSE). Default model `grok-4.7`, checked against the xAI streaming docs on 2026-10-21. |
| `local` | The same protocol at `localBaseUrl`. The default is Ollama's OpenAI endpoint, `http://127.0.0.1:11434/v1`. LM Studio is usually `http://127.0.0.1:1234/v1`. This id does not send an API key. |

Change the id, model, base URL, timeout, temperature, max tokens, or system prompt on `Assets/_Project/Settings/ChatSettings.asset`. That asset holds no secret.

The endpoint is the legacy Chat Completions API, which is what xAI documents as OpenAI-compatible. Visible reply text is `delta.content`. Reasoning text (`reasoning_content`) is not shown.

## Set the xAI key

The key is never stored in the repository. Resolution order:

1. Environment variable `XAI_API_KEY`.
2. `%APPDATA%/ProjectLeia/secrets.json` (on Windows, `C:\Users\<you>\AppData\Roaming\ProjectLeia\secrets.json`).
3. `Application.persistentDataPath/ProjectLeia/secrets.json`, if the file above is missing.

On Windows the file is encrypted with DPAPI for the current user (`CryptProtectData`). On a machine where DPAPI is not available, including the Linux editor, the same file is plain JSON with `"protected": false`. A key saved with DPAPI on Windows does not decrypt on Linux. An environment variable wins over either file.

The value is not written to the Console, to exception text, or to the chat reply.

### In the Unity editor

1. Open this project in Unity 6000.6.6f1.
2. **AI Companion > Set xAI API Key**. Paste the key and choose **Save**. The window shows the file path. The field clears after a save.
3. Or set `XAI_API_KEY` in the environment before you start the editor. Restart the editor after changing it.

**Remove saved key** deletes the per-user file. It does not unset the environment variable.

### In the Windows player

1. Build and run `Builds/Windows/AICompanion.exe`.
2. Either set `XAI_API_KEY` for that process before launch, or use the password field at the bottom of the window and press **Save key**. That writes the same per-user file.
3. Focus the window and press **F9**.

## Try a reply before the chat UI exists

The debug box sits at the bottom of the Game view and the player window. It is the same kind of always-on debug label as the top-left overlay readout.

1. Open `Assets/_Project/Scenes/Main.unity` and enter Play mode, or run the Windows player.
2. The prompt defaults to `Say hello in one short sentence.` Edit it if you want.
3. Press **F9**. The reply streams into the box and into the Console (`Chat delta:` lines, then `Chat reply:`).
4. **Esc** cancels the request that is in flight.
5. A failure stays on the box. The Console gets a warning with the HTTP status and the provider's message (`Chat error: HTTP 403 from Grok: ...`). The box shows a shorter line, or a hint when the cause is clear: `Invalid API key`, `xAI account has no API credits or billing is not set up (console.x.ai > Billing)`, `Model grok-4.7 not available for this key; change ChatSettings.model`, `Rate limited, try again shortly`, `Couldn't reach Grok`, or `Grok took too long to answer`. The log never includes the API key.

F7, F8, 1–5, T, and R are unchanged. Empty pixels around the avatar still click through. The chat box itself stays clickable.

## Add a provider

1. Implement `IChatProvider`, or configure another `OpenAiCompatibleChatProvider` if the server speaks Chat Completions with SSE.
2. `Register` it in `ChatProviderFactory.Create`.
3. Set `providerId` on `ChatSettings` to that id.

`ChatDebugHarness` and later UI should keep calling `registry.TryGet(settings.providerId)`. They should not grow a switch.

## Tests

`ChatProviderTests` feeds a fake transport. It does not open a socket and does not need `XAI_API_KEY`. CI stays green without a key.
