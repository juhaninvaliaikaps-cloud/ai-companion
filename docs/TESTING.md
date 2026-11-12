# Tests

## What CI runs

- **EditMode tests** on every push to `main` and every pull request (`EditMode and PlayMode tests` check).
- The job authenticates Unity Personal and returns the seat when it ends. It is serialized through the `unity-personal-license` concurrency group because Personal has one seat.
- The Unity editor image is pinned: `unityci/editor:ubuntu-6000.6.6f1-base-3`.

### Why EditMode only

PlayMode runs inside the same container and produced **no results file**:

```
cat: /github/workspace/artifacts/playmode-results.xml: No such file or directory
##[error]Test run failed with exit code 1
```

That made the check red while all 457 EditMode tests passed. PlayMode needs a display for the overlay and avatar scene, so it belongs on a machine with a desktop.

To try all modes in CI anyway, run the workflow manually:

1. Actions → **Tests** → **Run workflow**
2. `testMode`: `playmode` or `all`
3. If PlayMode still produces no `playmode-results.xml`, the container has no display; use the local command below instead.

## Running tests locally

Window capture is covered in [SCREEN_LOOK.md](SCREEN_LOOK.md). Encrypted local files are covered by `LocalStorageTests` and the Windows checklist in [LOCAL_STORAGE.md](LOCAL_STORAGE.md). EditMode tests do not grab a window. Before a batch test or a player build, copy `Assets/_Project/Settings/AvatarSelection.asset` and its `.meta` aside and copy them back afterward so a local avatar path is not rewritten.

EditMode:

```powershell
& "E:\Unity Editor\6000.6.6f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "C:\path\to\ai-companion" `
  -runTests -testPlatform EditMode `
  -testResults "$env:TEMP\editmode.xml" `
  -logFile "$env:TEMP\editmode.log"
```

PlayMode:

```powershell
& "E:\Unity Editor\6000.6.6f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "C:\path\to\ai-companion" `
  -runTests -testPlatform PlayMode `
  -testResults "$env:TEMP\playmode.xml" `
  -logFile "$env:TEMP\playmode.log"
```

The editor exits with code **2** when tests ran (that is not a failure). Read the XML:

```powershell
[xml]$x = Get-Content "$env:TEMP\editmode.xml"
$x.'test-run' | Select-Object total, passed, failed
```

### Known failure

`AICompanion.Tests.EditMode.SpeechToTextTests.NativeLoad_ListsEditorAndPlayerPluginFolders` fails on machines where the Whisper plugin folder layout differs from the test's Windows assumption. It is unrelated to feature work; ignore that one result.

## Unity batch tips

- Never switch branches while a batch run is active; it corrupts `Temp` and can fail mid-build.
- A build that spends many minutes on `prepared` values is compiling shader variants. If the project has unresolved merge conflicts in `UniversalRenderPipelineAsset` or `GraphicsSettings`, variants explode (millions per pass). Resolve `git status` conflicts first.
- Delete `Library/ScriptAssemblies` when a build seems to run stale code, then rebuild.
