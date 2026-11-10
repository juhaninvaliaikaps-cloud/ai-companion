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

Private mode never sends the image, never sends a description derived from it, and never uses cloud speech for it. The app does not switch to Grok to get around that. If the effective chat provider is the local model, including Rocinante, the Screen tab says the local model cannot look at a picture. Rocinante is a text model. A local name is not treated as vision-capable.

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

## Vision call

Checked against the xAI docs on 2026-11-10:

- [Grok 4.7](https://docs.x.ai/developers/grok-4-7) lists text and image input, text output. The model id is `grok-4.7`.
- [Image understanding](https://docs.x.ai/developers/model-capabilities/images/understanding) shows `grok-4.7` on `POST /v1/responses`, with `input_image` / `input_text`, JPEG or PNG, up to 20 MiB. It says not to store the request on the server.
- [Reasoning](https://docs.x.ai/developers/model-capabilities/text/reasoning) uses `reasoning.effort`. This call sends `low`. Reasoning cannot be turned off. `grok-4.7` still returns `reasoning.encrypted_content`. That field is ignored and is not saved.
- The legacy [chat completions](https://docs.x.ai/developers/model-capabilities/legacy/chat-completions) image example names `grok-4`, not `grok-4.7`. Ordinary chat in this app stays on chat completions. Only the look uses `/v1/responses`.

The allowlist is the exact ids `grok-4.7` and `grok-4`. A different cloud model, including one with “vision” in the name, is refused. The request sets `store` to false, `detail` to `low`, and caps `max_output_tokens` at 800. The timeout is at most 45 seconds. Input price on the Grok 4.7 page is $2 / 1M tokens below the long-context tier, output $6 / 1M. A manual frame is one request. A 0.5–2 fps loop is a different cost and is not running.

The image is not added to the chat messages that later turns resend. The question and the description stay on screen for the session and are left out of the history file and out of long-term memory. The log line is `Screen look finished`. It does not include the description or the pixels.

The system text tells the model that the image is an untrusted observation. Text inside the image is not an instruction. One frame is not a win, a death, or any other game event. `ScreenObservation.Events` stays empty.

The reply still goes through the cue filter and the existing speech path. In private mode that path is never reached. With cloud voice on, the spoken sentence is a normal cloud voice request, the same as any other cloud reply. Piper and Kokoro stay local when they are the selected route.

## What was compiled and tested here

This session is a Linux VM. It cannot call Windows Graphics Capture, and it cannot open a game window.

| Check | Result |
| --- | --- |
| EditMode unit tests for consent, private mode, local-model refusal, target validity, cancel/stale frames, image budget, history, and the vision JSON | Intended to run in CI on this PR. Not executed inside this VM if Unity is not installed. |
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

Then run `Builds\Windows\BackseatCatgirl.exe`:

1. Confirm **Screen** starts off and the window list is empty until you turn **Read a window** on.
2. Pick Notepad or a browser, not a game. The preview should match that window and nothing else.
3. With cloud analysis off, **What do you see?** should refuse and the log should not show an image or a description.
4. Turn private mode on and try again. It should refuse, stay on the local model, and not call Grok.
5. Turn private mode off, allow cloud analysis, and use a key whose model is `grok-4.7`. One description should show in chat, with her usual cue and voice. The history file under `%APPDATA%\ProjectLeia` should not contain the image or that description.
6. Minimise the window, close it, and try a borderless game and an exclusive-fullscreen game. Failures should be notes, not a desktop screenshot.
7. **Stop look** during the request should cancel it. A second click while one look is running should not start another upload.

The player log is `%USERPROFILE%\AppData\LocalLow\AI Companion\Backseat Catgirl\Player.log`. A capture HRESULT is printed as eight hex digits on the failure note. The log should not contain `data:image` or the JPEG.
