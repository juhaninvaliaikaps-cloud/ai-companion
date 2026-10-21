# AI Companion

Desktop companion for Windows. A 3D animated avatar sits in a transparent, always-on-top overlay, chats through a swappable AI provider (cloud APIs such as xAI Grok first, local models later), speaks and listens, and plays animations from cues the model returns.

This repository is the Unity project. Chat, voice, and avatar playback are later phases. The Windows overlay window is in [docs/OVERLAY.md](docs/OVERLAY.md).

## Unity version

**Unity 6000.6.6f1** (changeset `d20c9ac777da`). This is the build Unity Hub currently recommends. It is a supported Unity 6 update, not the 6.3 LTS line.

Install that exact editor with Unity Hub. Add the **Windows Build Support (Mono)** module. IL2CPP can wait until a release build needs it.

`ProjectSettings/ProjectVersion.txt` records the same version and changeset. Open the project with 6000.6.6f1. Another Unity 6 line will try to upgrade or downgrade the project.

## Open the project on Windows

1. Install [Git for Windows](https://git-scm.com/download/win) and [Git LFS](https://git-lfs.com/). In a terminal, run `git lfs install` once per machine.
2. Install Unity Hub and editor **6000.6.6f1** with Windows Build Support (Mono). Hub may already offer this as the recommended install.
3. Clone the repository. Git LFS must be installed before the first pull that contains models or audio, otherwise those files stay as pointer text.
4. In Unity Hub, choose **Add** (or **Open**) **project from disk** and select the cloned folder. That folder contains `Assets`, `Packages`, and `ProjectSettings`.
5. Let the first import finish. Unity downloads packages, including UniVRM from GitHub, so `git` must be on `PATH` and the machine needs network access. The first import can take several minutes.
6. Unity generates `Library/`, `Temp/`, `Logs/`, `UserSettings/`, the IDE solution (`*.sln`, `*.csproj`), and `Packages/packages-lock.json`. Do not commit `Library/`, `Temp/`, `Logs/`, or `UserSettings/`. After a clean import, commit `packages-lock.json` so later clones resolve the same package versions.

`.meta` files are already in the repository so script and folder GUIDs stay stable. If Unity rewrites a `.meta` on import, read the diff before committing it.

`Assets/_Project/Scenes/Main.unity` is the scene in the build. It has `AppBootstrap`, the overlay window, a camera, a light, and a placeholder capsule. How to build and check the transparent window is in [docs/OVERLAY.md](docs/OVERLAY.md).

### Build a Windows player

1. Install Windows Build Support (Mono) for 6000.6.6f1.
2. Open **File > Build Profiles**.
3. Choose **Windows** and architecture **x86_64**.
4. Confirm the scene from `Assets/_Project/Scenes` is enabled.
5. Choose **Build** and pick an output folder outside the repository (for example `Builds/Windows`). `Build/` and `Builds/` are gitignored.

Run tests from **Window > General > Test Runner**, or follow [TESTING.md](TESTING.md). `AnimationCueTests.Constructor_ClampsIntensityAndKeepsFields` is the EditMode example. `AppBootstrapPlayModeTests.Awake_LogsReadyMessage` is the PlayMode example. The Windows player is the overlay window described in [docs/OVERLAY.md](docs/OVERLAY.md).

The same player can be queued from a terminal once the Unity CLI is connected. See [Unity CLI](#unity-cli). `build_windows_player` returns immediately and the Editor writes `Builds/Windows/AICompanion.exe` while you poll `build_status`. `Build/` and `Builds/` are gitignored.

## Unity CLI

A local Cursor agent on Windows can drive the open Editor through the [Unity CLI](https://unity.com/blog/meet-the-unity-cli) and the experimental [Unity Pipeline](https://docs.unity3d.com/Packages/com.unity.pipeline@0.6/manual/index.html) package (`com.unity.pipeline` 0.6.0-exp.1). The server listens on localhost only. Leave the runtime server disabled; it is for development and QA players, and a release build should not include it.

`Packages/manifest.json` already requests `0.6.0-exp.1`, the version in the 0.6 package manual. The documented installer is still `unity pipeline install`, which adds that package to the open project.

1. Install the Unity CLI. In PowerShell:

   ```powershell
   $env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
   ```

   Close that terminal and open a new one so `unity` is on `PATH`. The current install channel and the macOS/Linux script are in the [Unity CLI blog post](https://unity.com/blog/meet-the-unity-cli).
2. Sign in with `unity auth login`.
3. Open this project in Unity **6000.6.6f1** and wait until the Pipeline package has imported and scripts have compiled. The Editor has to be open before the next commands can talk to it.
4. From the repository root, run `unity pipeline install` if Package Manager has not already resolved `com.unity.pipeline`. Then run `unity pipeline list` and confirm this project shows **Pipeline: Installed**.
5. Run `unity command`. The list should include the project commands in `Assets/_Project/Editor/CliCommands`:
   - `run_editmode_tests` — EditMode pass, fail, and skip counts, plus leaf failures.
   - `get_compile_and_console_errors` — whether scripts failed to compile, and compiler or console errors seen since this domain loaded.
   - `list_project_avatars_and_clips` — `.vrm` and `.fbx` files and animation clips under `Assets/_Project`.
   - `build_windows_player` — queues a Windows x64 development player at `Builds/Windows/AICompanion.exe` and returns immediately. Poll `build_status` until `status` is `completed`. It returns `error` when no scene is enabled.
6. Run one with `unity command <name>`, for example `unity command run_editmode_tests`. For a one-off check, `unity command eval "return Application.version;"` runs C# in the open Editor. Eval is token-gated and stays on this machine.
7. For the Windows player, run `unity command build_windows_player`, then poll `unity command build_status` until `status` is `completed`. The same build is `unity command build --target StandaloneWindows64 --outputPath Builds/Windows/AICompanion.exe --options Development --confirm true`. `--confirm true` is required or the pipeline refuses the build. `--options Development` is what makes it a development player; the 0.6 manual uses DetailedBuildReport when `options` is omitted. `build_windows_player` used to call `BuildPipeline.BuildPlayer` on the request thread, and the CLI timed out after 30 seconds even though the player was produced. The command now queues the pipeline `build` command and returns `queued`.

How an agent in this repo should call those commands is in `.cursor/rules/unity-cli.mdc`. If more than one Editor is open, pass `--project-path` with this repository's path.

This environment has no Unity Editor, so the package was not downloaded and these commands were not executed. The asmdef references assembly `Unity.Pipeline`, which is the assembly name in Unity's CLI authoring notes for `[CliCommand]`. The 0.6 manual describes shipped commands as coming from an assembly such as `Unity.Pipeline.Editor`. If the Editor reports a missing assembly, point the asmdef at the assembly name the imported package shows. The Windows architecture value is stored with the setting name `Architecture`, which is the key `EditorUserBuildSettings` writes for `StandaloneWindows64`; that key is not a documented public constant. `BuildTarget.StandaloneWindows64`, `BuildOptions.Development`, and `OSArchitecture.x64` are the documented build APIs. Compiler and console entries from before the current domain load are not available from the public events this command subscribes to.

## Render pipeline

The project uses the **Universal Render Pipeline 17.6.0**, the URP version bundled with Unity 6000.6.6f1. `Assets/_Project/Settings/UniversalRenderPipelineAsset.asset` is assigned in Graphics and on every quality level. HDR and MSAA are off so the transparent overlay keeps framebuffer alpha. How to load a VRM, an FBX, or the local Asuna prefab is in [docs/AVATARS.md](docs/AVATARS.md). The window itself is in [docs/OVERLAY.md](docs/OVERLAY.md).

## Packages

`Packages/manifest.json` includes:

| Package | Role |
| --- | --- |
| `com.unity.test-framework` 1.8.0 | EditMode and PlayMode tests. Core package. Unity 6000.6.0 moved this from 1.7.0 to 1.8.0, and the 6000.6.6f1 release notes do not list a later bump. |
| `com.unity.inputsystem` 1.20.1 | New Input System. The Unity 6000.6 manual lists 1.20.1 as the released version for this editor. Player Settings use **Both** (new Input System and the legacy Input Manager). |
| `com.unity.ugui` 2.6.0 | Unity UI, including TextMesh Pro. Core package shipped with Unity 6000.6. The old `com.unity.textmeshpro` package is not used. |
| `com.unity.timeline` 6.6.0 | Core package on Unity 6000.6, renumbered from the 1.8 line. UniVRM depends on Timeline. |
| `com.unity.mathematics` 1.3.3 | Required by UniVRM. Latest registry release; its package metadata allows Unity 2021.3 and newer. It is not a 6000.6 core package. |
| `com.unity.pipeline` 0.6.0-exp.1 | Experimental Pipeline package. Lets the Unity CLI run Editor commands, including the project commands under `Assets/_Project/Editor/CliCommands`. |
| `com.unity.render-pipelines.universal` 17.6.0 | URP bundled with Unity 6000.6.6f1. Required for Asuna's materials and for UniVRM MToon on this editor. |
| `com.vrmc.gltf`, `com.vrmc.vrm`, and `com.vrmc.univrm` | UniVRM **v0.131.4**. VRM 1.0 plus the VRM 0.x importer. That release includes the Unity 6.6 deprecation fix. |

UniVRM is installed the way the project documents it: Git URLs in the manifest, not a copied `.unitypackage`.

```json
"com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.4",
"com.vrmc.univrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM#v0.131.4",
"com.vrmc.vrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.4"
```

`com.vrmc.vrm` is VRM 1.0. It depends on `com.vrmc.gltf` (UniGLTF). `com.vrmc.univrm` is the VRM 0.x importer. Unity does not accept Git dependencies inside a package manifest, so the project manifest lists all three. Runtime loads also migrate a VRM 0.x file through `Vrm10.LoadPathAsync`. Source and release notes: [github.com/vrm-c/UniVRM](https://github.com/vrm-c/UniVRM/releases/tag/v0.131.4).

Test Framework, uGUI, and Timeline are core packages on Unity 6000.6: the editor ships one version of each, and Package Manager will not substitute a different one. The pins above come from the Unity 6000.6.0 package update (`test-framework` 1.8.0, `ugui` 2.6.0, `timeline` 6.6.0) and the 6000.6 manual (`inputsystem` 1.20.1). The 6000.6.6f1 patch notes do not change those four. This repository was not opened in the editor, so the bundled versions were not read from that install's package manifest.

If Package Manager says one of those versions cannot be found, set the manifest entry to the version shown for that package in **Window > Package Manager** and reopen the project. In particular, do not put Timeline back on `1.8.13`; Unity 6000.6 expects `6.6.0`.

The other `com.unity.modules.*` entries stay at `1.0.0`, which is how Unity lists built-in modules. No 6000.6 note turned up that removes one of the modules in this manifest. `com.unity.mathematics` 1.3.3 was not re-verified against a 6000.6 editor install. UniVRM stays on `v0.131.4`, the current official tag, which calls out a Unity 6.6 fix.

The Player Settings file was first written for a Unity 6.3 editor. Unity 6000.6.6f1 upgrades that YAML on the first open. Review the `ProjectSettings` diff before committing it.

## Secrets

API keys stay out of the repository and out of `Assets/`, so Unity cannot import them into a build.

1. Copy `Config/appsettings.example.json` to `Config/appsettings.json`.
2. Fill in the xAI API key locally.
3. Leave `Config/appsettings.json` untracked. `.gitignore` already excludes it.

The example file has empty strings only. Do not commit a filled-in copy.

## Repository layout

```text
Assets/_Project/
  Scripts/
    Core/                  AppBootstrap, AnimationCue, Emotion          AICompanion.Core
    AI/                    IChatProvider                                 AICompanion.AI
    Voice/                 speech-to-text and text-to-speech             AICompanion.Voice
    Avatar/                humanoid load, blendshapes, descriptor        AICompanion.Avatar
    AnimationDirector/     cue to Animator mapping                       AICompanion.AnimationDirector
    Overlay/               transparent always-on-top window              AICompanion.Overlay
    UI/                    chat and settings UI                          AICompanion.UI
  Models/                  other source meshes (Git LFS). Character FBX here is imported as Humanoid.
  Avatars/                 FBX humanoids, including the CC0 sample (Git LFS)
  StreamingAssets/Avatars/ CC0 VRM loaded at runtime (Git LFS)
LocalAvatars/              gitignored. Asset Store models such as Asuna go here.
  Animations/              Companion.controller and the CC0 humanoid clips
  Scenes/                  Unity scenes
  Settings/                  runtime settings assets (not API keys)
  Plugins/                   native or third-party plug-ins that are not UPM packages
  Editor/CliCommands/        Unity CLI [CliCommand] methods               AICompanion.Editor.CliCommands
  Tests/
    EditMode/              editor tests, including AnimationCueTests
    PlayMode/              play-mode tests, including AppBootstrapPlayModeTests
Config/
  appsettings.example.json template for local provider keys
  appsettings.json         gitignored local secrets
```

`AICompanion.AI` references `Core`. `UI` references `Core` and `AI`. `Avatar` references `Core` and UniVRM's `VRM10` assembly. Voice, AnimationDirector, and Overlay each reference `Core` only, so chat and animation cannot form a cycle. `AnimationCue` lives in `Core` because both the provider layer and the director will use it.

Git LFS tracks binary assets (`.fbx`, `.vrm`, `.png`, `.wav`, audio, video, and similar). Scenes, prefabs, and `.asset` files stay as text. Run `git lfs install` before adding those binaries.

## What the skeleton contains

- `IChatProvider` in `AICompanion.AI` is the boundary for a cloud or local LLM. It has no network calls.
- `AnimationCue` carries an `Emotion`, an intensity clamped to 0–1, and a gesture name.
- `IAvatarAnimator` is the runtime hook for that cue. `SetTalking(bool)` and `SetEmotion(Emotion, intensity)` drive the shared humanoid controller and the face. The debug keys are 1–5 and T. On spawn the humanoid root yaws to face the overlay camera. Right-click a visible pixel, then drag, to add a yaw that is saved per avatar source.
- `AppBootstrap` is the `MonoBehaviour` scene entry point. `Awake` logs once and starts `ICompanionStartup` components on the same object, including `OverlayWindow`.

## Roadmap

Work is tracked in the AI Companion Linear project. The phases below match those milestones.

### Phase 1 — MVP

The desktop window and a first conversation.

- This Unity project and repository.
- Transparent, always-on-top overlay on Windows.
- Import a humanoid avatar through UniVRM (VRM 1.0) or FBX.
- Idle, talk, and a few emotion states.
- Chat provider interface with xAI Grok as the first implementation. The API key stays in the local config.
- Text chat panel.
- One text-to-speech voice, Whisper speech-to-text with push-to-talk, and basic lip sync.
- A small animation catalog and a director that maps AI cues onto Animator states.

### Phase 2 — Core systems

The companion becomes useful on the machine and in a game.

- Local model provider (Ollama or llama.cpp) and routing rules: normal chat may use a cloud API; adult mode, when it exists, stays on the local model.
- Long-term memory, persona presets, and swappable voice modules.
- Richer animation (layered blending, procedural idle, reactive listening).
- Desktop tools (reminders, calendar, files, apps, clipboard) behind an opt-in permission system.
- Game companion prototype: read-only capture or official game APIs, cloud vision, and paced commentary.

### Phase 3 — Adult mode, plug-ins, and release

Optional, separate from the base app, then a Windows release.

- Adult mode is off by default, behind an age gate and an explicit opt-in. Requests in that mode use the local model only. Related content stays on the device.
- Adult content ships as a separate optional pack, not inside the base player.
- A plug-in and tool API for voices, personas, animations, and avatars, still subject to the permission system.
- Encrypted local storage, distribution research, installer, updates, and onboarding.

## First open, done by you

Unity will create anything this skeleton does not check in:

- `Library/`, `Temp/`, `Logs/`, `UserSettings/`
- `Packages/packages-lock.json` (commit this after the first successful import)
- IDE solution files

Confirm in the Console that import finished with no compile errors, then run the EditMode test named above.
