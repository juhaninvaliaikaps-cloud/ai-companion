# Agent rules for this repository

Several AI agents work on this project at the same time (Cursor, DeepSeek, Grok, Claude). Linear is the shared memory between them. Every agent follows the rules below, whichever tool it runs in.

## 1. Log your work to Linear

The Linear team is **Juhani Mustola** (key `JUH`), project **Project Leia**. Issue ids look like `JUH-21`.

Before you start:

- Read the issue, its comments, and the latest handover issue (search Linear for "Handover").
- Check the open pull requests so you do not edit the same files as another agent. If you must, say so in your Linear comment.
- Set the issue to **In Progress**.

When you stop, whether the work is finished or not, add a comment to the issue that another agent can act on without asking you anything. Include:

- **What changed**, in plain words, and why.
- **Files** added or changed, with the class names that matter.
- **Branch and pull request** number.
- **Tests**: which tests you added, and whether you ran them. If you could not run Unity, say that the code is unverified until CI or the Windows PC runs it.
- **Decisions and rejected options**, so nobody redoes them.
- **Open items**: what is left, what is blocked, and what Jussi has to check by hand.

Work that has no issue yet gets a new issue first. A handover that spans several issues gets its own issue titled "Handover: ...".

Do not mark an issue **Done** until its pull request is merged. Jussi decides what is merged.

## 2. Branches, commits and pull requests

- Branch name: `<agent>/juh-<number>-<short-name>`, for example `claude/juh-21-emotion-detection`.
- Start the commit and pull request title with the issue id: `JUH-21: ...`.
- One issue per pull request. Keep unrelated cleanup out.
- Never push to `main` directly.

## 3. Code rules

- Put logic in plain C# classes with no scene objects, and cover it with EditMode tests under `Assets/_Project/Tests/EditMode`. MonoBehaviours stay thin.
- Every new file under `Assets/` needs its `.meta` file with a new GUID. Do not change an existing GUID.
- Each folder under `Assets/_Project/Scripts` is its own assembly. Check the `.asmdef` references before using a type from another folder.
- No allocations in `Update` or `LateUpdate`.
- Visual behaviour (the avatar, the overlay window) is checked by hand on the Windows PC. A green test run does not prove it.
- Update the matching file in `docs/` when behaviour changes.

## 4. Privacy and secrets

- Private mode and adult mode use the local model only. A cloud call on that path is a blocker.
- Never commit API keys, Unity license files, or a filled `Config/appsettings.json`.

## 5. Where to read more

- `README.md`: project layout and the Unity version.
- `TESTING.md` and `docs/TESTING.md`: how tests and CI run.
- `.cursor/rules/unity-cli.mdc`: driving the open Unity Editor from a terminal.
- `.cursor/BUGBOT.md`: what a review looks for.
