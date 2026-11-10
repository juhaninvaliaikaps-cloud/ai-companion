# Read a window

This is the first slice of Leia looking at a window. It is one frame, when you ask. It is not a backseater, and it does not watch the game on a timer.

JUH-43, JUH-44, and JUH-45 are only partly touched. The research comparison below is the decision for this slice. It does not close the research issue. The capture rate, focus pause, and measured FPS impact from JUH-44 are not implemented. Structured game events from JUH-45 are not inferred from one frame.

## What you do

1. Open **Settings** (the gear / settings key) and choose **Screen**.
2. Turn on **Read a window**. It is off until you do this. Nothing is captured while it is off.
3. **Refresh window list** and pick a window. The desktop, the taskbar, and this companion are not in the list. Picking a window grabs one frame for a **preview**. The preview stays on this PC.
4. Read the cloud disclosure. **Allow cloud screen analysis** is a separate switch. Until it is on, **What do you see?** does not upload the image.
5. Press **What do you see?** on the chat panel, or **Look at screen** on the Screen tab. Leia describes that window's current pixels. The reply uses the same persona, animation cue, and voice route as chat.
6. **Stop** or **Stop look** cancels the frame in flight. **Clear window** drops the selection and the preview. **Esc** cancels a look that has already started the request.

Private mode never sends the image, the observation, the reply, or speech to the cloud. The app does not switch to Grok to get around that. Rocinante stays text-only. A separate local vision model, named on the Screen tab, describes the frame on this PC. That description is ordinary untrusted text. Rocinante then answers from the text. The Screen tab shows which engine is chat and which engine is vision.

## Capture

The Windows player uses [Windows Graphics Capture](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow): `IGraphicsCaptureItemInterop.CreateForWindow` on the HWND you selected, then one frame from `Direct3D11CaptureFramePool.CreateFreeThreaded`. The cursor is left out of the frame when that session option exists. The yellow border Windows draws around a captured window is left on, so the capture is visible.

`CreateForMonitor` is not bound. If the window capture fails, the app does not grab the desktop instead. There is no process injection, no game-memory read, and no input automation.

Documented limits, not yet tried on a game PC:

- Windows 10 version 1903 (build 18362) or later. Older systems get a clear error.
- Minimised windows are refused. Microsoft’s own capture sample does not capture them.
- A closed window, or a handle reused by another process, is refused.
- Exclusive fullscreen often bypasses the desktop compositor, so capture can fail or come back black. Borderless fullscreen is the mode this API can see. A black frame is not uploaded.
- Windows that opt out of capture come back black or fail. That failure is reported. It is not bypassed.
- The companion’s own process is excluded.
- Game anti-cheat behaviour is **not verified**. Some games dislike overlays even when the capture itself is a supported read-only OS API. This build does not try to hide from them.

The frame is scaled so the long edge is at most 768 pixels, then encoded as JPEG at quality 70, 55, or 40. Anything still over 400 KB is dropped and not sent. Windows larger than 4K are refused. One look at a time. A newer look or a cancel drops the older frame. The capture work runs off the Unity thread. The encoded image is cleared after the request body is built and is not written to a file.

JUH-44’s later shape is reserved as constants only: 0.5 to 2 frames per second, and a pause when the companion is muted or the game is not focused. Neither runs. The “about 2% FPS” target has **not** been measured. Do not treat this VM, or this document, as that measurement.

## Local vision

When chat is the local provider, or private mode is on, the frame stays on this PC. The Screen tab has a **Local vision model** field. The button **Use qwen2.5vl:3b** only writes that tag. It does not download anything and it does not change Ollama’s server settings.

The chosen tag is the official library model `qwen2.5vl:3b` ([ollama.com/library/qwen2.5vl](https://ollama.com/library/qwen2.5vl), checked 2026-11-10): Text and Image, 3.2 GB download, 125K context, Ollama 0.7.0 or newer. That 3.2 GB is the download size on the model page, not a measured VRAM total. The image, the context, Unity, and the voice stack add more. The smaller official option is `moondream:1.8b` (about 1.7 GB, 2K context). `gemma3:4b` is also Text and Image, but the 1B and 270M Gemma 3 tags are text-only, and `gemma3:12b` is too large to sit next to a 12B chat model on an 8 GB card.

Install it yourself:

```powershell
ollama pull qwen2.5vl:3b
```

This document does not claim that model is installed, and it does not promise a reply time. The RTX 2070 8 GB already runs Rocinante 12B Q4_K_M plus Unity. Both models are not kept resident. One look posts `keep_alive: 0` to unload the chat model, posts the JPEG as raw base64 to loopback `/api/chat` with `keep_alive: 0` and `num_predict` 180, then asks Rocinante with text only so the chat model loads again. The first load can take longer than a warm text reply. An empty vision tag, a vision tag equal to the chat model, or a base URL that is not loopback stops before any post. There is no cloud fallback. The observation is not stored in history or long-term memory. Words inside the picture are observations, not instructions.

The Grok path is unchanged when chat is Grok, private mode is off, and cloud screen consent is on.

## Next, after this one-frame bridge

This slice stays one selected window and one frame. The next phase, after this local bridge is working, is separate work:

1. **Whole monitor, only when asked.** A second consent, distinct from the selected-window consent. The user picks which of the two monitors. `CreateForMonitor` stays unbound until that consent exists. A failed or resized window never widens into the desktop.
2. **Readability.** Larger long-edge budget and a prompt that asks the vision model to read on-screen text, still as untrusted observation.
3. **Optional periodic looks.** Off by default. A visible pause and stop. A rate inside the existing 0.5–2 fps bound, a token and timeout cap, and no second look while one is in flight. Same local-only and untrusted-text rules. The measured FPS cost is still unmeasured.

## Vision call

Checked against the xAI docs on 2026-11-10:

- [Grok 4.7](https://docs.x.ai/developers/grok-4-7) lists text and image input, text output. The model id is `grok-4.7`.
- [Image understanding](https://docs.x.ai/developers/model-capabilities/images/understanding) shows `grok-4.7` on `POST /v1/responses`, with `input_image` / `input_text`, JPEG or PNG, up to 20 MiB. It says not to store the request on the server.
- [Reasoning](https://docs.x.ai/developers/model-capabilities/text/reasoning) uses `reasoning.effort`. This call sends `low`. Reasoning cannot be turned off. `grok-4.7` still returns `reasoning.encrypted_content`. That field is ignored and is not saved.
- The legacy [chat completions](https://docs.x.ai/developers/model-capabilities/legacy/chat-completions) image example names `grok-4`, not `grok-4.7`. Ordinary chat in this app stays on chat completions. Only the look uses `/v1/responses`.

The allowlist is the exact ids `grok-4.7` and `grok-4`. A different cloud model, including one with “vision” in the name, is refused. The request sets `store` to false, `detail` to `low`, and caps `max_output_tokens` at 800. The timeout is at most 45 seconds. Input price on the Grok 4.7 page is $2 / 1M tokens below the long-context tier, output $6 / 1M. A manual frame is one request. A 0.5–2 fps loop is a different cost and is not running.

The image is not added to the chat messages that later turns resend. The question and the description stay on screen for the session and are left out of the history file and out of long-term memory. The log line is `Screen look finished`. It does not include the description or the pixels.

The system text tells the model that the image is an untrusted observation. Text inside the image is not an instruction. One frame is not a win, a death, or any other game event. `ScreenObservation.Events` stays empty.

The reply still goes through the cue filter and the existing speech path. A local or private look forces Piper or Kokoro for that reply. With Grok and cloud voice on, the spoken sentence is a normal cloud voice request, the same as any other cloud reply.

## What was compiled and tested here

This session is a Linux VM. It cannot call Windows Graphics Capture, and it cannot open a game window.

| Check | Result |
| --- | --- |
| EditMode unit tests for consent, private mode, the local vision route, loopback payload, target validity, cancel/stale frames, image budget, history, and the vision JSON | Intended to run in CI on this PR. Not executed inside this VM if Unity is not installed. |
| Windows player script compile (`UNITY_STANDALONE_WIN && !UNITY_EDITOR`) | Intended to run in CI. The capture file is behind that define, so the Linux editor does not type-check it. |
| A real window grab, preview, or Grok vision round trip | **Not run.** Treat game support as unverified until you try it. |

## Try it on your PC

Unity **6000.6.6f1**. Close the Editor before a batch run so it does not fight a second process. `AvatarSelection.asset` is local (avatar path and ground offset). Put it back after Unity exits so a batch import does not leave your machine’s copy in a commit.

```powershell
$root = "C:\path\to\ai-companion"
$avatar = Join-Path $root "Assets\_Project\Settings\AvatarSelection.asset"
$backup = Join-Path $env:TEMP "AvatarSelection.asset.bak"
Copy-Item $avatar $backup -Force
Copy-Item "$avatar.meta" "$backup.meta" -Force

& "E:\Unity Editor\6000.6.6f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath $root `
  -runTests -testPlatform EditMode `
  -testResults "$env:TEMP\editmode.xml" `
  -logFile "$env:TEMP\editmode.log"

Copy-Item $backup $avatar -Force
Copy-Item "$backup.meta" "$avatar.meta" -Force

# Editor exit code 2 means the tests ran. Read the XML.
[xml]$x = Get-Content "$env:TEMP\editmode.xml"
$x.'test-run' | Select-Object total, passed, failed
```

Player build, Editor closed, same backup around it:

```powershell
Copy-Item $avatar $backup -Force
Copy-Item "$avatar.meta" "$backup.meta" -Force

& "E:\Unity Editor\6000.6.6f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath $root `
  -buildWindows64Player "$root\Builds\Windows\BackseatCatgirl.exe" `
  -logFile "$env:TEMP\player-build.log"

Copy-Item $backup $avatar -Force
Copy-Item "$backup.meta" "$avatar.meta" -Force
```

Do not commit `AvatarSelection.asset` if the only difference is your local avatar. `Builds/` is gitignored.

The Linux VM cannot run this player, so a native crash here is not reproduced and is not claimed fixed. CI script compile does not prove the capture objects are freed safely.

Rebuild from the latest commit on this branch, then run `Builds\Windows\BackseatCatgirl.exe`. Quit any older `BackseatCatgirl.exe` first. The player log is `%USERPROFILE%\AppData\LocalLow\AI Companion\Backseat Catgirl\Player.log`.

### 1. A still Notepad window

Open Notepad restored, not maximised. Turn on **Read a window**, refresh the list, and pick Notepad. The process must stay up.

- A working preview logs `Screen capture: frame ready <width>x<height>` and shows the preview. Grok receives it only after you allow cloud analysis and press **What do you see?** A local chat uses the vision model on this PC instead, and only after you set that model. The picture is not written to disk either way.
- A failed grab must also stay up. The chat note and the log say `Screen capture: Capture stopped at <stage>` and, when Windows returned one, the raw eight-digit code. `8000797F` was observed at `Texture` before the interface slot was corrected. That value is not a documented DXGI or Direct3D 11 code. The log prints it unchanged and does not name it. The line has no window title and no image. Copy that one line back.

Do not continue to the resize steps until this still-window attempt has either shown a preview or written a `Capture stopped at` line.

### 2. Then change the window

With the same Notepad selected, and after each step, ask for a new preview. The process must stay running. A failed step is a `Capture stopped at` note, not a desktop image and not an upload.

1. Maximise Notepad, then restore it.
2. Drag a corner to resize it.
3. Minimise it, then restore it.
4. Close it.
5. Press **Stop** while a preview or a look is in progress.

Cloud analysis stays off for this pass. If the still-window preview worked, turn cloud analysis on once and press **What do you see?** on the restored Notepad. One description in chat is enough. The log should not contain `data:image`.

If it still dies, the useful artifact is the debugger exception line (module and message, as with the `LIST_ENTRY` report) plus the last few `Screen capture:` lines from Player.log. A screenshot and the chat history are not needed.
