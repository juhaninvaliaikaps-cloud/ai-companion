# Chat panel

The panel is the session transcript for the companion. It sits to the left of the avatar, clear of her on-screen bounds, and below the top debug readout. The gap is `sideGap` on the Chat Panel component (40 pixels by default). If the left side of the window cannot hold the panel plus that gap, the panel moves to her right with the same gap. A centered model in the 480-wide window keeps the panel on the left and narrows it so the gap stays. It calls `IChatProvider` only through `ChatProviderRegistry` and `ChatSettings`. It does not know whether the provider is Grok or a local server.

The full transcript stays on screen for the session. Each request sends the system prompt plus the earlier user and companion turns, dropping the oldest turns once there are more than 12 messages or about 4000 characters. System notes (errors, an empty reply, a saved key) stay on screen and are not sent back to the model.

The system prompt lives on `ChatSettings.systemPrompt`. The default is a short, friendly, PG desktop companion. Adult or private mode is a later local-model feature and is not part of this panel.

## Hotkeys

| Key | When | What it does |
| --- | --- | --- |
| **F10** | Any time | Show or hide the panel. Opening it focuses the message field. The choice is saved in `PlayerPrefs` (`ai-companion.chat-open`). |
| **Enter** | Message field focused | Send the draft. Send is ignored while a reply is streaming. |
| **Esc** | A reply is streaming, or the field is focused | Cancel the reply. Also leaves the text field. |
| **Stop** | While a reply is streaming | Same cancel as Esc. The button reads **Send** when idle. |
| **F9** | Panel field is not focused | Open the panel if it is hidden and send the draft. If the draft is empty, send `Say hello in one short sentence.` |
| Double-click | A visible avatar pixel, not on UI | Open the panel and focus the message field. |

While the message field or the API-key field is focused, these debug keys do not fire: **1–5**, **T**, **R**, **F7**, **F8**, **F9**. **F10** and **Esc** still work. **F7** still cycles the avatar, **F8** still toggles topmost, and **R** still resets yaw, once the field is not focused.

A streaming reply calls `IAvatarAnimator.SetTalking(true)` and sets it back to false when the reply finishes, fails, or is cancelled. Failures from `ChatResult` show up as a **Note** line. The Console still gets the scrubbed `Chat error:` warning from the provider layer. The API key is never written to the log or the transcript.

The panel is drawn into the camera, so its pixels count as solid for the overlay hit test. Clicks on the panel do not pass through and do not drag the window. Empty pixels around the avatar still click through. The top-left debug label is still the window drag handle.

While the field is focused, or while a reply is streaming, the overlay stays at the active frame rate (60) instead of dropping to the idle 15.

On a Windows build, opening the panel or clicking it brings the window forward so the field can take keys. In the editor, click the Game view first.

The password row at the bottom writes the same per-user key file as **AI Companion > Set xAI API Key**. The field clears after a save. See [CHAT_PROVIDERS.md](CHAT_PROVIDERS.md) for where that file lives.

## Try it in the editor

1. Open the project in Unity 6000.6.6f1.
2. Set a key with **AI Companion > Set xAI API Key**, or with the password row on the panel. Restart the editor first if you use the `XAI_API_KEY` environment variable.
3. Open `Assets/_Project/Scenes/Main.unity` and enter Play mode. Click the Game view so it has keyboard focus.
4. The panel starts open to the left of the model, with a clear gap between its right edge and her silhouette. It should not cover her face or body. **F10** hides it. **F10** again shows it and focuses the field. Quit and Play again: the last open or closed state comes back. To change the spacing, select **AppBootstrap** and edit **Chat Panel > Side Gap**.
5. Type a short message and press **Enter**. The line appears under **You**. The reply grows under **Companion** as chunks arrive. The debug label shows `Talk on` during the reply and `Talk off` when it finishes. The fps readout should stay near 60 while the reply is streaming.
6. Press **Esc** or **Stop** during a reply. The partial text stays, and a **Note** says `Request cancelled`.
7. With the field focused, press **1**, **T**, **R**, **F7**, **F8**, and **F9**. The avatar, rotate reset, avatar source, and topmost flag should stay as they were. Click the empty area or press **Esc** to leave the field, then **1** and **T** work again.
8. Click an empty pixel outside the avatar, the debug label, and the panel. The click reaches the desktop (in a Windows build) or is not consumed by the model. Drag still starts on the avatar or the top debug label, not on the panel.
9. Double-click a visible part of the avatar. The panel opens.
10. **F9**, with the field unfocused and empty, sends the one-line hello prompt.

A missing key, a rejected request, or a model the key cannot use shows as a **Note**, with the same wording as the provider layer (`Invalid API key`, the billing hint, `Model ... not available`, and the rest).

## Try it in the Windows build

1. Build and run `Builds/Windows/AICompanion.exe`.
2. If the key is not already in the environment or the per-user file, press **F10** if the panel is hidden, type the key into the password row, and press **Save**. The field clears and a note says the key was saved on this PC.
3. The panel is to her left, with empty space between the panel and her body. Click the panel or press **F10** so the window comes forward, then type and press **Enter**. The reply streams, and she talks for the duration of the reply.
4. **Esc** or **Stop** cancels. **F10** hides the panel without ending the overlay. Empty pixels still click through to the desktop. Drag the window from her or from the top-left label, not from the panel.
5. Leave the field, wait a few seconds with the pointer outside the window, and the frame rate drops toward 15. Click the field or send another message and it returns to about 60 for the typing or the stream.
