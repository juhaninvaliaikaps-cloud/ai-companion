# AI Companion

Desktop companion for Windows. A 3D animated avatar sits in a transparent, always-on-top overlay, chats through a swappable AI provider (cloud APIs such as xAI Grok first, local models later), speaks and listens, and plays animations from cues the model returns.

This repository is the Unity project skeleton. It compiles as an empty project. Chat, voice, the overlay, and avatar playback are later phases.

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

There is no scene yet. Before the first player build, create one with **File > New Scene**, add an empty GameObject with the `AppBootstrap` component, and save it under `Assets/_Project/Scenes`. Then add that scene in **File > Build Profiles** (Unity 6) or **File > Build Settings**.

### Build a Windows player

1. Install Windows Build Support (Mono) for 6000.6.6f1.
2. Open **File > Build Profiles**.
3. Choose **Windows** and architecture **x86_64**.
4. Confirm the scene from `Assets/_Project/Scenes` is enabled.
5. Choose **Build** and pick an output folder outside the repository (for example `Builds/Windows`). `Build/` and `Builds/` are gitignored.

The player is an empty window until the overlay and avatar scenes exist. This environment does not have the Unity Editor, so the project was not opened and the example test was not executed here. Run it locally with **Window > General > Test Runner > EditMode > Run All**. `AnimationCueTests.Constructor_ClampsIntensityAndKeepsFields` should pass. The PlayMode assembly is in place and has no tests yet.

## Render pipeline

The skeleton uses the **built-in render pipeline**. `GraphicsSettings` has no custom render pipeline asset, so the project opens without pipeline files that would have to match a specific Universal Render Pipeline (URP) package revision.

URP is a later step, once the avatar scene exists:

1. In Package Manager, install **Universal RP** for this editor.
2. Use **Create > Rendering > URP Asset (with Universal Renderer)**.
3. Assign that asset under **Project Settings > Graphics** and on each quality level under **Project Settings > Quality**.

`preserveFramebufferAlpha` is already enabled in Player Settings so a later transparent overlay can keep the window alpha.

## Packages

`Packages/manifest.json` includes:

| Package | Role |
| --- | --- |
| `com.unity.test-framework` 1.8.0 | EditMode and PlayMode tests. Core package. Unity 6000.6.0 moved this from 1.7.0 to 1.8.0, and the 6000.6.6f1 release notes do not list a later bump. |
| `com.unity.inputsystem` 1.20.1 | New Input System. The Unity 6000.6 manual lists 1.20.1 as the released version for this editor. Player Settings use **Both** (new Input System and the legacy Input Manager). |
| `com.unity.ugui` 2.6.0 | Unity UI, including TextMesh Pro. Core package shipped with Unity 6000.6. The old `com.unity.textmeshpro` package is not used. |
| `com.unity.timeline` 6.6.0 | Core package on Unity 6000.6, renumbered from the 1.8 line. UniVRM depends on Timeline. |
| `com.unity.mathematics` 1.3.3 | Required by UniVRM. Latest registry release; its package metadata allows Unity 2021.3 and newer. It is not a 6000.6 core package. |
| `com.vrmc.gltf` and `com.vrmc.vrm` | UniVRM **VRM 1.0**, tag `v0.131.4`. That release includes the Unity 6.6 deprecation fix. |

UniVRM is installed the way the project documents it: Git URLs in the manifest, not a copied `.unitypackage`.

```json
"com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.4",
"com.vrmc.vrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.4"
```

`com.vrmc.vrm` is VRM 1.0. It depends on `com.vrmc.gltf` (UniGLTF). Both entries are required because Unity does not accept Git dependencies inside a package manifest; the project manifest has to list them.

VRM 0.x models need a third package, added the same way when those files show up:

```text
https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM#v0.131.4
```

That package name is `com.vrmc.univrm`. Source and release notes: [github.com/vrm-c/UniVRM](https://github.com/vrm-c/UniVRM/releases/tag/v0.131.4).

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
    Avatar/                VRM and humanoid avatar runtime               AICompanion.Avatar
    AnimationDirector/     cue to Animator mapping                       AICompanion.AnimationDirector
    Overlay/               transparent always-on-top window              AICompanion.Overlay
    UI/                    chat and settings UI                          AICompanion.UI
  Models/                  FBX and other source meshes (Git LFS)
  Avatars/                 VRM files (Git LFS)
  Animations/              clips and controllers
  Scenes/                  Unity scenes
  Settings/                  runtime settings assets (not API keys)
  Plugins/                   native or third-party plug-ins that are not UPM packages
  Tests/
    EditMode/              editor tests, including AnimationCueTests
    PlayMode/              play-mode test assembly (no tests yet)
Config/
  appsettings.example.json template for local provider keys
  appsettings.json         gitignored local secrets
```

`AICompanion.AI` references `Core`. `UI` references `Core` and `AI`. Voice, Avatar, AnimationDirector, and Overlay each reference `Core` only, so chat and animation cannot form a cycle. `AnimationCue` lives in `Core` because both the provider layer and the director will use it.

Git LFS tracks binary assets (`.fbx`, `.vrm`, `.png`, `.wav`, audio, video, and similar). Scenes, prefabs, and `.asset` files stay as text. Run `git lfs install` before adding those binaries.

## What the skeleton contains

- `IChatProvider` in `AICompanion.AI` is the boundary for a cloud or local LLM. It has no network calls.
- `AnimationCue` carries an `Emotion`, an intensity clamped to 0–1, and a gesture name.
- `AppBootstrap` is the `MonoBehaviour` scene entry point. `Awake` logs once.

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
- `ProjectSettings/QualitySettings.asset` (not included here; Unity writes the default quality levels on first open)
- The first scene, which you save under `Assets/_Project/Scenes`

Confirm in the Console that import finished with no compile errors, then run the EditMode test named above.
