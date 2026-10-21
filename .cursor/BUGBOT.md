# AI Companion review

Flag bugs in the diff. Stay specific: file, behavior, and why it is wrong.

Unity:

- Main-thread-only calls (`AssetDatabase`, scene objects, `BuildPipeline`, most `UnityEngine` APIs) must not run on a worker thread.
- A CLI command that blocks until a player build or a long test run finishes will time out. It should queue and tell the caller to poll status.
- Flag allocations in `Update` or `LateUpdate` (new collections, string building, LINQ).
- Flag a serialized `UnityEngine.Object` used with no null check.
- A script in an asmdef must reference the assemblies it uses. Editor-only code stays in an Editor assembly.

Tests:

- Logic changes need a unit test: animation director, provider routing, commentary pacing, cue parsing.
- Avatar look and the transparent overlay are checked on the Windows PC. Do not demand a PlayMode test for those.

Secrets and adult mode:

- Block API keys, Unity license files, or a filled `Config/appsettings.json` in the diff.
- Adult or private mode talks only to the local model. A cloud provider call on that path is a blocker.
