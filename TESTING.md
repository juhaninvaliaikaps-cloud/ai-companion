# Testing

EditMode and PlayMode tests live under `Assets/_Project/Tests`. The example EditMode test is `AnimationCueTests.Constructor_ClampsIntensityAndKeepsFields`. Overlay decision, drag, and frame-rate tests are in `OverlayLogicTests`. `AppBootstrapStartupTests` covers the startup hook. `AvatarDescriptorTests` covers blendshape preset mapping (VRM names and ARKit names) and humanoid bone validation. `AvatarAnimatorModelTests` and `EmotionFaceTests` cover emotion parameters, debug keys, face weights, and the clip crossfade. `AvatarFacingTests` covers the camera-facing yaw, the drag delta, the per-source PlayerPrefs keys, and rotate-mode toggle rules. `ChatProviderTests` covers SSE parsing, error messages, provider selection, and API-key resolution with a fake transport and no network. `ChatConversationTests` covers the session transcript: appended turns, streamed deltas, trimming, error and cancel lines, and a fake provider with no network. The chat panel itself is checked by hand; see [docs/CHAT_UI.md](docs/CHAT_UI.md). `AvatarAnimationPlayModeTests` checks that those calls survive a frame with no model loaded, and that a +Z root still faces a camera on −Z after a few frames. The example PlayMode test is `AppBootstrapPlayModeTests.Awake_LogsReadyMessage`. The transparent window and the imported model are checked by hand; see [docs/OVERLAY.md](docs/OVERLAY.md) and [docs/AVATARS.md](docs/AVATARS.md).

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

`.github/workflows/tests.yml` runs on every push to `main` and on every pull request. It uses [GameCI `unity-test-runner` v4](https://game.ci/docs/github/test-runner) (`game-ci/unity-test-runner@v4`) on `ubuntu-latest`.

Docker Hub has an image for this editor: `unityci/editor:ubuntu-6000.6.6f1-base-3` (also published as `6000.6.6f1-base-3`). The workflow pins that image with `customImage` and sets `unityVersion` to `6000.6.6f1`. v4 is the current major version. v4.4.0, which `@v4` points at, includes the Unity 6.6 shared-memory fix (`--shm-size=1025m`).

Two jobs run, EditMode and PlayMode. Each restores a `Library` cache keyed by the editor version, the test mode, and the package manifest. Test results are uploaded as artifacts named `Test results (EditMode)` and `Test results (PlayMode)`. With `githubToken` set, GameCI also posts each run as a GitHub check. Code coverage is off via `GAME_CI_COVERAGE_ENABLED=false`. GameCI documents that coverage instrumentation has crashed PlayMode on Unity 6. Leave the action input `coverageEnabled` unset: `unity-test-runner` v4 forwards `false` as `--no-coverageEnabled`, and the GameCI CLI rejects that flag and exits before the editor starts.

If `UNITY_LICENSE`, `UNITY_EMAIL`, or `UNITY_PASSWORD` is missing, the job prints a notice and succeeds without running Unity. That keeps the check from failing before the secrets exist. After the secrets are set, the next run executes the tests.

The repository's Actions workflow permissions must allow creating checks. In the GitHub repo, open **Settings > Actions > General > Workflow permissions** and choose **Read and write permissions**. The workflow also requests `checks: write` for the token it uses.

## What to test

Unit tests cover logic: the animation director, provider routing, commentary pacing, and other pure behavior. `AnimationCue` is the current example.

Visuals are checked by hand on the Windows PC: the avatar, the transparent overlay, window focus, and anything that depends on a real display. Do not treat a green PlayMode run as proof that the overlay looks right.

Adult or private mode, when it exists, stays on the local model. A test or review that finds that path calling a cloud API is a failure.

## Pull request review

Every pull request gets a Cursor Bugbot review. Bugbot reads `.cursor/BUGBOT.md`.

Before merging, run a second review with the project skill `.cursor/skills/review/SKILL.md`. That pass is independent of Bugbot. It looks for bugs, Unity threading and allocation mistakes, missing tests on logic changes, and secrets or adult content sent to a cloud model.

Comment `bugbot run` on a pull request to ask Bugbot again. `bugbot run verbose=true` lists which rule files that run included.

## Add the Unity license secrets

These steps follow GameCI's Personal license guide: [Activation](https://game.ci/docs/github/activation). Use them for a free Personal license. A Pro or Plus license uses `UNITY_SERIAL` instead, and these Personal steps do not apply to it.

1. Install Unity Hub on the Windows PC and sign in with the Unity account that should own the CI license.
2. Open **Unity Hub > Preferences > Licenses** and click **Add**.
3. Choose **Get a free personal license** and finish activation. Click **Add** even if Hub already lists a license. GameCI says the `.ulf` file is created by that activation, and a license that only appears in the list may not have written the file.
4. If Hub does not offer a way to create a `.ulf`, GameCI points people to the [game-ci Discord](https://github.com/game-ci/documentation/blob/main/docs/03-github/02-activation.mdx) for the current workaround. Do not use the old manual `.alf` website flow. Unity no longer supports that for Personal licenses.
5. Open `C:\ProgramData\Unity\Unity_lic.ulf`. `ProgramData` is hidden. The same license file works for the Ubuntu CI image. It is not tied to one editor version.
6. On GitHub, open this repository, then **Settings > Secrets and variables > Actions > New repository secret**. Create three secrets. Paste values only into GitHub. Do not commit them.
   - `UNITY_LICENSE`: the full text of `Unity_lic.ulf`
   - `UNITY_EMAIL`: the Unity account email
   - `UNITY_PASSWORD`: the Unity account password
7. Re-run the **Tests** workflow. The skip notice should be gone, and EditMode and PlayMode should run on `unityci/editor:ubuntu-6000.6.6f1-base-3`.
