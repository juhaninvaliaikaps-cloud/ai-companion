# Testing

EditMode and PlayMode tests live under `Assets/_Project/Tests`. The example EditMode test is `AnimationCueTests.Constructor_ClampsIntensityAndKeepsFields`. Overlay decision, drag, and frame-rate tests are in `OverlayLogicTests`. `AppBootstrapStartupTests` covers the startup hook. `AvatarDescriptorTests` covers blendshape preset mapping (VRM names and ARKit names) and humanoid bone validation. `AvatarAnimatorModelTests` and `EmotionFaceTests` cover emotion parameters, debug keys, face weights, and the clip crossfade. `AvatarFacingTests` covers the camera-facing yaw, the drag delta, the per-source PlayerPrefs keys, and rotate-mode toggle rules. `ChatProviderTests` covers SSE parsing, error messages, provider selection, and API-key resolution with a fake transport and no network. `ChatConversationTests` covers the session transcript: appended turns, streamed deltas, trimming, error and cancel lines, and a fake provider with no network. `VoiceSpeechTests` covers sentence splitting, spoken-text cleanup, the speech queue and cancel, voice settings, and a fake text-to-speech provider with no network. See [docs/VOICE.md](docs/VOICE.md). `LipSyncTests` covers amplitude, the loudness knee, jaw and vowel clamps, viseme hold, attack/release, the return to rest, and which mouth target is chosen. `ProceduralLifeTests` covers blink timing, look-at clamps, idle crossfades, and the cursor-look settings flag. `ExpressionPresetTests` covers expression preset lookup, blendshape name resolution, blush strength, and lip-sync priority over mouth weights. The cheek overlay is checked by hand. See [docs/EXPRESSIONS.md](docs/EXPRESSIONS.md). `TrayMenuTests` covers the tray menu labels and check marks, pause (speech, push-to-talk, and reaction suppression), the per-user Run command, the taskbar-button default, click decoding, and icon add/remove. The Windows notification area itself is checked by hand. See [docs/OVERLAY.md](docs/OVERLAY.md). The moving mouth is checked by hand. See [docs/AVATARS.md](docs/AVATARS.md). The chat panel itself is checked by hand; see [docs/CHAT_UI.md](docs/CHAT_UI.md). `AvatarAnimationPlayModeTests` checks that those calls survive a frame with no model loaded, and that a +Z root still faces a camera on −Z after a few frames. The example PlayMode test is `AppBootstrapPlayModeTests.Awake_LogsReadyMessage`. The transparent window and the imported model are checked by hand; see [docs/OVERLAY.md](docs/OVERLAY.md) and [docs/AVATARS.md](docs/AVATARS.md).

## Run tests locally

The Editor must be open on this project (Unity 6000.6.6f1) for the CLI commands.

In the Editor: **Window > General > Test Runner**. Choose **EditMode** or **PlayMode**, then **Run All**.

From the repository root:

```text
unity command run_editmode_tests
unity command run_tests --mode playmode
```

`run_editmode_tests` is the project command. It runs EditMode tests synchronously and returns pass, fail, and skip counts. `run_tests` is the Pipeline package command. Its `mode` values are `all`, `editor`, and `playmode` ([build, compilation, and test commands](https://docs.unity3d.com/Packages/com.unity.pipeline@0.6/manual/commands/build-and-compilation.html)). `editor` is EditMode. `run_tests` waits for the run on the request, so a long PlayMode run can hit the CLI timeout the same way a blocking player build did. For EditMode, prefer `run_editmode_tests`.

## What CI does

`.github/workflows/tests.yml` runs on every push to `main` and on every pull request. It uses [GameCI `unity-test-runner` v4.4.0](https://game.ci/docs/github/test-runner) on `ubuntu-latest`, with the game-ci CLI pinned to `v0.1.72`. The job timeout is 60 minutes.

Docker Hub has an image for this editor: `unityci/editor:ubuntu-6000.6.6f1-base-3` (also published as `6000.6.6f1-base-3`). The workflow pins that image with `customImage` and sets `unityVersion` to `6000.6.6f1`. v4.4.0 includes the Unity 6.6 shared-memory fix (`--shm-size=1025m`).

One job runs PlayMode and EditMode (`testMode: all`). That is one activation, then both modes, then the action returns the Personal seat. A second workflow waits on the `unity-personal-license` concurrency group so two runs do not take the seat at once. The job restores a `Library` cache keyed by the editor version and the package manifest. Test results are uploaded as an artifact named `Test results` (`editmode-results.xml` and `playmode-results.xml`). With `githubToken` set, GameCI also posts the run as a GitHub check. Code coverage is off via `GAME_CI_COVERAGE_ENABLED=false`. GameCI documents that coverage instrumentation has crashed PlayMode on Unity 6. Leave the action input `coverageEnabled` unset: `unity-test-runner` v4 forwards `false` as `--no-coverageEnabled`, and the GameCI CLI rejects that flag and exits before the editor starts.

A second job, **Windows player script compile**, starts after that test job finishes so it can take the same seat. It uses [GameCI `unity-builder` v6.0.0](https://game.ci/docs/github/builder) with CLI `v0.1.72`, `targetPlatform: StandaloneWindows64`, and `unityci/editor:ubuntu-6000.6.6f1-windows-mono-3`. The build method `AICompanion.Editor.WindowsPlayerScriptCheck.VerifyStandaloneWindows64` only checks that player scripts compile for that target. It does not write a player. Linux EditMode defines `UNITY_EDITOR`, so `UNITY_STANDALONE_WIN && !UNITY_EDITOR` code is invisible to the test job. The same missing-secret skip applies. This job does not restore `Library`. Restoring the Windows editor cache killed the process with exit code 137 during the first domain reload. A cold `Library` is what completed the job.

If `UNITY_EMAIL` or `UNITY_PASSWORD` is missing, the job prints a notice and succeeds without running Unity. That keeps the check from failing before the secrets exist. After the secrets are set, the next run executes the tests.

The repository's Actions workflow permissions must allow creating checks. In the GitHub repo, open **Settings > Actions > General > Workflow permissions** and choose **Read and write permissions**. The workflow also requests `checks: write` for the token it uses.

## Windows player

Build `Builds\Windows\BackseatCatgirl.exe`. The standalone log is `%USERPROFILE%\AppData\LocalLow\AI Companion\Backseat Catgirl\Player.log`. The Editor log is still the Unity Editor log. It does not move with `productName`.

## What to test

Unit tests cover logic: the animation director, provider routing, commentary pacing, and other pure behavior. `AnimationCue` is the current example.

Visuals are checked by hand on the Windows PC: the avatar, the transparent overlay, window focus, and anything that depends on a real display. Do not treat a green PlayMode run as proof that the overlay looks right.

Adult or private mode, when it exists, stays on the local model. A test or review that finds that path calling a cloud API is a failure.

## Pull request review

Every pull request gets a Cursor Bugbot review. Bugbot reads `.cursor/BUGBOT.md`.

Before merging, run a second review with the project skill `.cursor/skills/review/SKILL.md`. That pass is independent of Bugbot. It looks for bugs, Unity threading and allocation mistakes, missing tests on logic changes, and secrets or adult content sent to a cloud model.

Comment `bugbot run` on a pull request to ask Bugbot again. `bugbot run verbose=true` lists which rule files that run included.

## Add the Unity license secrets

CI activates a free Personal seat with the Unity account. [game-ci/cli v0.1.63](https://github.com/game-ci/cli/releases/tag/v0.1.63) measured that path on Unity 6000.6: `UNITY_EMAIL` and `UNITY_PASSWORD` are enough, and no serial is required. A `.ulf` (`UNITY_LICENSE`) is bound to the machine that created it. Loading one inside the Ubuntu image fails with `Machine bindings don't match` (code 400) and the editor exits before the tests. Leave `UNITY_LICENSE` unset. A Pro or Plus seat is a different setup (`UNITY_SERIAL`) and these Personal steps do not apply to it.

1. On GitHub, open this repository, then **Settings > Secrets and variables > Actions**.
2. Create or update two repository secrets. Paste values only into GitHub. Do not commit them, and do not put them in chat.
   - `UNITY_EMAIL`: the Unity account email
   - `UNITY_PASSWORD`: the Unity account password
3. Re-run the **Tests** workflow from the **Actions** tab. The skip notice should be gone, and EditMode and PlayMode should run on `unityci/editor:ubuntu-6000.6.6f1-base-3`.

The action returns the seat when the job ends. If a later run reports no available Personal seats, open [id.unity.com](https://id.unity.com), release the stuck activation, and re-run **Tests**. An existing `UNITY_LICENSE` secret can stay; the workflow does not read it.
