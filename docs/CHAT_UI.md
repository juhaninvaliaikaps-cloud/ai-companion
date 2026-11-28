# Chat panel

The panel is the session transcript for the companion. It sits to the left of the avatar and below the top debug readout. Horizontal placement follows the avatar root (a stable point, not the rotating mesh bounds) and the panel is clamped fully inside the visible window, including the area above the taskbar. Ctrl+scroll scales the avatar, not this panel. If the transparent window grows so she is not clipped, the panel stays the same pixel size and clamps inside the new window. The gap is `sideGap` on the Chat Panel component (40 pixels by default). If the left side of the window cannot hold the panel plus that gap, the panel moves to her right with the same gap. A centered model in the 480-wide window keeps the panel on the left and narrows it so the gap stays. It calls `IChatProvider` only through `ChatProviderRegistry` and `ChatSettings`. It does not know whether the provider is Grok or a local server.

The full transcript stays on screen for the session. Each request sends the system prompt plus the earlier user and companion turns, dropping the oldest turns once there are more than 12 messages or about 4000 characters. System notes (errors, an empty reply, a saved key) stay on screen and are not sent back to the model.

**Long scenes (JUH-129).** Dropping old turns loses the setup of a long roleplay, and a small local model then invents a new place or outfit. Two things keep it:

- Every chat send carries one continuity line (`SceneDigest.ContinuityRule`): keep the place, the time of day, what each of them wears, holds and does, and what just happened, unless the story clearly moves on.
- Once the session no longer fits the window, the chat model writes a short digest of the scene (at most about 120 words: place and time, clothes and what each is doing, what happened so far, anything planned) every 10 user messages, after a reply. It is added to the system prompt under "The scene so far…" until the next digest replaces it. The request gets the previous digest and the end of the session transcript (about 9000 characters), with no memories, and a 120-second limit.
- Privacy is the same as the session-end summary: the digest is asked of the model the session must use (local when the session had private or 18+ turns), adult lines never reach a cloud request, and a digest of a session that stayed local is only added to a local or private server's prompt, never to a cloud one.
- It lives in memory for the running session only. A new session, a cleared transcript or a persona switch starts without one. With a local model the next reply may wait a few seconds while a digest is written.

The logic is in `SceneDigest` (plain C#, `SceneDigestTests`); `ChatPanel.Scene.cs` wires it in.

The system prompt lives on `ChatSettings.systemPrompt`. A persona selected in Settings replaces it. With no persona, it is the setup name and relationship, and the default with no name is a short, friendly, PG desktop companion. `ChatPanel` then appends the director cue instructions. Adult or private mode is a later local-model feature and is not part of this panel.

## Ribbon prototype (JUH-198)

Happi uses the bundled Plus Jakarta Sans family by Tokotype across the interface. Chat starts at 18 px and retains its font and size pickers under Settings → Chat → Text and box. Reset chat font and size restores Plus Jakarta Sans at 18 px. An existing version 1 setting that matches both old defaults (Segoe UI at 14 px) adopts the new defaults. Other saved font and size combinations carry over. The Ribbon title and Send use ExtraBold Italic.

Launch the Windows player with `--ribbon-ui` to preview the head-side Ribbon menu and the charcoal chat style. Hover the menu icon, then hover Look now, Profile, AI, or Settings to see their illustrated scrolls. Look now previews Happi at a monitor and still performs a single look on selection. Close highlights in cyan on hover or keyboard focus and plays the ink stamp before dismissing the menu. The arrow shows which side the flyout will open on, and the speaker icon shows when her voice is muted. Profile opens the existing profile panel, and AI shortcuts open the current model and voice pages. Settings opens the full window on click. Its scroll offers All settings, Chat layout, and Window. The prototype shares voice mute with chat and uses the existing Look now and Private/Public behavior. A pink-and-yellow ink stamp covers the selected button. Menu destinations open after the 200 ms hold so flyout selections show the stamp before the menu disappears. Send has a cyan brushstroke that sweeps on hover or keyboard focus. Sending a message plays a stronger sweep with the ink stamp. The separate Her Room door shows a planned-feature hint when hovered. Ribbon chat uses compact voice and privacy controls, a darker composer, and cyan stripes next to Happi's replies. When the transcript is longer than the chatbox, a slim cyan indicator shows your scroll position. Scroll up to reveal the Latest button. Incoming replies leave your reading position in place when possible. Ribbon messages show a user glyph or rounded persona portrait beside each card when there is room. Happi uses a mint rim, and personas without a portrait use a silhouette. The live reply and transcript use the same layout, with the portrait near the beginning of longer replies. Wider windows give the cards more breathing room and use a taller title and artwork row when there is enough vertical space. The composer follows the same clipped outline as the chat frame and highlights that outline in mint while typing. Its prompt and typed text are centered vertically. The automatic Ribbon chatbox is 560 × 392 pixels at 100% scale and the default height setting. Smaller work areas reduce its size to keep the composer reachable. Manual resize dimensions are saved and fitted to the current monitor. The gear menu’s Playback & mic toggle shows optional playback controls and the microphone. A checkmark shows when they are enabled. At short chat heights it fits its actions into two columns below the header. Click the gear or press Alt+O in chat, use the arrow keys and Enter to choose, and press Escape to return to the previous chat field or the gear. Step mode also shows its playback controls. The chat header has a separate Hide button. F10 or a double-click on the avatar opens chat again. Click the persona name or press Alt+P while chat has focus to unroll the illustrated picker above it. The picker aims for 980 pixels wide at 100% scale and fits the entire Happi-Playful pose inside the painted edge. Choose from up to four personas, including the highlighted active persona and recent choices from this session, or open Create persona in Persona Studio or All personas in Settings. The picker opens below the header if there is too little room above. Arrow keys move between choices, Enter selects, and Escape returns to the previous chat field with its selection or to the persona button. The gear menu uses the same focus behavior. Long names shrink in their buttons, and a name that still overflows appears below the choices when hovered or selected. Quick switching waits for replies, playback, and microphone capture to finish. Unsent drafts stay with their persona until Happi exits, including when chat is hidden and reopened. Escape in the Ribbon composer ends editing and keeps the draft. The yellow Talk portrait meets the top and right edges of the chat frame. Narrow chatboxes place the header controls on a second row when there is room. The portrait gives way to the persona name and controls in short, narrow chatboxes. The dropdown fits above or below its icon and scales to fit short work areas. Ribbon adds transparent room on both sides of the Windows overlay so the illustrated flyout can reach farther across the desktop. The flyout aims for 1000 pixels wide at 100% scale and stays in the visible work area. A stationary illustrated curl overlaps the edge of the main tabs. The paper starts under the diagonal shaft and reveals outward from there. The curl casts a short shadow over the paper, and the top and bottom edges flare toward the painting where the work area allows. The tapered paper reveals the painting to its brush-cut end in about 210 ms, with the original artwork orientation on either side. A short pointer grace period keeps the tab open when the cursor crosses a neighboring row on the way to the flyout. The visible paper keeps the flyout open during hover. On Windows, flyout buttons highlight and accept a click when Happi is visible over another active window. Hover does not take focus. In the silhouette and panels-only modes, clicks on the art fall through to the desktop. Click the menu icon or press Alt+Down while Happi is focused to use the keyboard. Use Up and Down to move, Enter to choose, and Escape to back out. The arrow toward a flyout opens it. When Happi already has focus, Up or Down also takes over an open hover menu. An active text field keeps its arrow keys. Moving the pointer away from the icon returns to hover mode after a short distance. A click outside dismisses the menus.

Across from the head-side controls, a small icon shows the current mood using the Feelings glyphs. It replaces the numeric readout in Ribbon mode and lets clicks pass through.

Ribbon plays a quiet tick when pointer or keyboard focus reaches a menu row, a short confirmation on selection, and a distinct cue when a message is sent. Positive affection gets a warm cue. A secret reveal gets a brighter chime and takes priority when both rewards arrive together. Reward sounds follow the reveal timing. Chat settings has a UI sound volume control, which defaults to 35%. Set it to zero to silence those effects. Happi’s brief hum and surprised reaction use the installed reaction voice pack and follow voice mute and pause. They can wait briefly for another reaction voice to finish.

## Private and public (JUH-177)

The chat box header has a **Private / Public** switch in the place of "What do you see?" (looking at the screen stays on the wheel: Screen → Look now).
- **The switch.** It turns private mode on or off with the saved setting, so each side uses the model and voice set up for it. It does nothing while she is still answering; that reply finishes on its own model first. Private shows in pink, Public in amber. When it does switch, the chapter in progress is closed first, with the privacy that chapter had. A public chapter is still summarised by the public model, and it stays a public chapter even when the 18+ gate opens with the switch.
- **The note.** Switching writes a note in the chat ("Public mode on: what was said in private stays on this PC; …").

**Everything said in private mode stays local-only.** Every user line and reply added while private mode is on is marked local-only (`ChatConversation.MarkLocalNow`), not just 18+ interaction lines, so a public request leaves it out. Before this, the first public message after private mode sent the last private messages to the cloud. In public mode, private lines also do not pick lorebook entries. Back in private mode, the local model sees the whole conversation again.

**What public mode may know** (`PrivateChapter`, `PrivateChapterPrompt`; Settings → AI → Private mode). When a public request leaves private lines out, it gets one sentence written by code, never by a model, from a fixed list:
- **Nothing:** no sentence.
- **That it happened:** "you and the user talked in private; … don't guess at it, retell it or ask about it, and don't act as if nothing happened."
- **What kind of moment** (the default): the same, as a personal talk, a romantic moment or an intimate moment. The intimate one says "don't describe it or bring up details; it can leave you feeling closer".

The kind is the strongest seen during the private chapter. It comes from each private reply's cue only: an 18+ interaction, `heat` hot or release, an intimate expression or the intimate mood mean intimate; `heat` warm, a flirt or the affectionate mood mean romantic. No text is used. After a restart the kind is unknown, so "what kind" falls back to "that it happened". This is the one exception to "nothing from private mode goes to the cloud" (AGENTS.md).

**The session memory follows the same setting (JUH-233).** When a session held an 18+ moment and its public part is summarised, `SessionMemories.PublicMarker` adds one public memory, also written by code:
- **Nothing:** none. Public facts from that session carry no `moment` kind either.
- **That it happened:** "The user and <her name> had a private talk."
- **What kind of moment:** "The user and <her name> shared a private, intimate moment."

The private part keeps its own marker ("… were intimate."), which stays local.

A session handoff saved for a later retry keeps the setting it was saved with (`publicHint`). A handoff from before that key existed reads as Nothing, the strictest choice, and so does a caller that doesn't pass the setting.

## Hotkeys

| Key | When | What it does |
| --- | --- | --- |
| **F10** | Any time | Show or hide the panel. Opening it focuses the message field. The choice is saved in `PlayerPrefs` (`ai-companion.chat-open`). |
| **Enter** | Message field focused | Send the draft. Send is ignored while a reply is streaming. A new send also stops any speech still playing. |
| **Esc** | A reply is streaming, speech is playing, or the field is focused | Cancel the reply and stop speech. Also leaves the text field. |
| **Stop** | While a reply is streaming, or while speech is still playing | Same cancel as Esc. The button reads **Send** when idle. |
| **What do you see?** | The button under the title. It reads **Stop look** while a look is in progress | Asks about the window selected under Settings → Senses → Screen. See [SCREEN_LOOK.md](SCREEN_LOOK.md). |
| **Mute** / **F6** | **F6** is ignored while a text field is focused | Mute or unmute speech. Saved in `PlayerPrefs` (`ai-companion.tts-muted`). See [VOICE.md](VOICE.md). |
| **F9** | Panel field is not focused | Cycle the expression portrait preview. It does not open the panel, send the draft, or start a greeting. See [PORTRAITS.md](PORTRAITS.md). |
| **V** / **Mic** | **V** is ignored while a text field is focused. **Mic** is a hold | Record speech, then send the transcript as a **You** line. See [VOICE.md](VOICE.md). |
| Double-click | A visible avatar pixel, not on UI | Open the panel and focus the message field. |

While the message field or the API-key field is focused, letter keys **A–Z** (including **V**) and these debug keys do not fire: **1–5**, **T**, **R**, **F6**, **F7**, **F8**, **F9**. **F10** and **Esc** still work. **F7** still cycles the avatar, **F8** still toggles topmost, and **R** still resets yaw, once the field is not focused.

Spoken replies are a separate step from the text stream. The talk animation follows the audio, not the moment text arrives. See [VOICE.md](VOICE.md).

Failures from `ChatResult`, and voice failures, show up as a **Note** line. The Console still gets the scrubbed warning. The API key is never written to the log or the transcript.

The panel is drawn into the camera, so its pixels count as solid for the overlay hit test. Clicks on the panel do not pass through and do not drag the window. Empty pixels around the avatar still click through. The top-left debug label is still the window drag handle.

While the field is focused, or while a reply is streaming, the overlay stays at the active frame rate (60) instead of dropping to the idle 15.

On a Windows build, opening the panel or clicking it brings the window forward so the field can take keys. In the editor, click the Game view first.

The password row at the bottom writes the same per-user key file as **AI Companion > Set xAI API Key**. The field clears after a save. See [CHAT_PROVIDERS.md](CHAT_PROVIDERS.md) for where that file lives.

## Try it in the editor

1. Open the project in Unity 6000.6.6f1.
2. Set a key with **AI Companion > Set xAI API Key**, or with the password row on the panel. Restart the editor first if you use the `XAI_API_KEY` environment variable.
3. Open `Assets/_Project/Scenes/Main.unity` and enter Play mode. Click the Game view so it has keyboard focus.
4. The panel starts open to the left of the model, with a clear gap between its right edge and her silhouette. It should not cover her face or body, and it stays inside the Game view while you rotate her. **F10** hides it. **F10** again shows it and focuses the field. Quit and Play again: the last open or closed state comes back. To change the spacing, select **AppBootstrap** and edit **Chat Panel > Side Gap**. The Console logs `ChatPanel: pos ...` at most once a second when the rect changes.
5. Type a short message and press **Enter**. The line appears under **You**. The reply grows under **Companion** as chunks arrive. Speech and the talk state are covered in [VOICE.md](VOICE.md). The fps readout should stay near 60 while the reply is streaming or while audio is playing.
6. Press **Esc** or **Stop** during a reply. The partial text stays, and a **Note** says `Request cancelled`.
7. With the field focused, press **1**, **T**, **R**, **F6**, **F7**, **F8**, and **F9**. The avatar, mute, rotate reset, avatar source, and topmost flag should stay as they were. Click the empty area or press **Esc** to leave the field, then **1** and **T** work again. **F6** toggles mute once the field is not focused.
8. Click an empty pixel outside the avatar, the debug label, and the panel. The click reaches the desktop (in a Windows build) or is not consumed by the model. Drag still starts on the avatar or the top debug label, not on the panel.
9. Double-click a visible part of the avatar. The panel opens.
10. **F9**, with the field unfocused, cycles the portrait expression and does not send a message. An empty field stays empty.

A missing key, a rejected request, or a model the key cannot use shows as a **Note**, with the same wording as the provider layer (`Invalid API key`, the billing hint, `Model ... not available`, and the rest).

## Try it in the Windows build

1. Build and run `Builds/Windows/Happi.exe`.
2. If the key is not already in the environment or the per-user file, press **F10** if the panel is hidden, type the key into the password row, and press **Save**. The field clears and a note says the key was saved on this PC.
3. The panel is to her left, with empty space between the panel and her body. Click the panel or press **F10** so the window comes forward, then type and press **Enter**. The reply streams. Speech is in [VOICE.md](VOICE.md).
4. **Esc** or **Stop** cancels. **F10** hides the panel without ending the overlay. Empty pixels still click through to the desktop. Drag the window from her or from the top-left label, not from the panel.
5. Leave the field, wait a few seconds with the pointer outside the window, and the frame rate drops toward 15. Click the field or send another message and it returns to about 60 for the typing or the stream.
## Expressions within a spoken line

In Ribbon chat, the model can put up to two face-only cues within a reply. A cue such as `<animation portrait_beat="surprised" />` changes the drawn 2D expression at the following phrase. The words remain one line and the cues are omitted from speech and chat history. The portrait follows actual clip progress with an estimated word position. Muted replies use reading pace. Nearby cues are spaced so each face has time to settle. The main animation cue still handles body movement, mood, and relationship signals.
