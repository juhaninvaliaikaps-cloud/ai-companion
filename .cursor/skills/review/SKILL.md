---
name: review
description: Review an AI Companion pull request for bugs, Unity pitfalls, missing logic tests, and secrets or adult content sent to a cloud model.
---

# Review this pull request

Read the diff and the surrounding code. Report only issues you can point at in the change. A clean review says so in a few lines.

## Look for

- Bugs: wrong conditions, dropped errors, races, and behavior that contradicts the call site.
- Unity main thread: `AssetDatabase`, `BuildPipeline`, `EditorApplication`, scene objects, and most `UnityEngine` calls belong on the main thread. A `[CliCommand]` that blocks the main thread for a long build or test run will hit the CLI timeout. Queue it and poll a status command.
- Allocations in `Update`, `LateUpdate`, or per-frame animation callbacks: new lists, string concatenation, and LINQ that run every frame.
- Serialized references: a public or `[SerializeField]` `UnityEngine.Object` used without a null check.
- Assembly definitions: a new script in an asmdef folder that calls another area needs that reference. Editor-only code stays in an Editor assembly. Do not reference `Unity.Pipeline` from a runtime assembly.
- Tests: logic changes (animation director, provider routing, commentary pacing, cue parsing) need a unit test. Avatar rendering and the overlay are manual checks on the Windows PC. Say when a logic change has no test.
- Secrets: API keys, license files, and `Config/appsettings.json` must stay gitignored. A diff that adds a key, a `.ulf`, or a filled config is a blocker.
- Adult or private mode: that path uses the local model only. A cloud request (xAI or any other provider) from adult or private mode is a blocker.

## Output

Use this shape:

```text
Verdict: approve | comment | block

Blockers
- file:line — what is wrong and why it matters

Notes
- file:line — non-blocking finding

Tests
- what was covered, and which logic change still has no test
```

Omit an empty section. Do not restate the whole diff.
