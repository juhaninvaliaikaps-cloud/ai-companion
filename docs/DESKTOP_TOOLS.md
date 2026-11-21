# Desktop tools and permissions (JUH-27)

Every desktop capability is **off until the user grants it**, individually.

## Tools

| Tool | Capability |
| --- | --- |
| Files | Find files by name in approved folders |
| Apps | Launch installed applications |
| Clipboard | Read or reformat clipboard text |
| Calendar | Read events, create reminders/events |
| Web | Search the web and summarize results |

## States

Each tool has one setting:

```text
0  Ask on first use
1  Allowed
2  Blocked
```

`Ask` means the capability is off, but the first attempt shows a consent card. The answer is remembered. The user can change any tool to Allow, Ask or Block in **Settings → Tools**, or clear the action log there.

## For a new tool

Call the permission gate **before** touching the desktop and only run the action when the callback receives `true`:

```csharp
DesktopToolPermissions.Request(
    settingsPanel,
    DesktopToolKind.Web,
    "search the web",
    allowed =>
    {
        if (!allowed)
            return;
        // perform the tool action here
    });
```

- `Request(SettingsPanel, ...)` saves the decision through the settings presenter.
- `Request(CompanionSettings, ...)` is the no-UI/test variant and does not persist.
- An allowed tool runs immediately.
- A blocked tool is refused without prompting.
- An `Ask` tool shows the consent card and records the answer.
- If no consent UI is present, the request is refused and logged.

## Action log

`DesktopToolAuditLog` writes encrypted `desktop-tool-actions.json` under `%APPDATA%\ProjectLeia`.

It records:

- timestamp
- tool
- a caller-supplied safe action label
- allowed/blocked
- reason

Never pass raw search queries, file paths, clipboard text or calendar contents as the action label. The log is included in local export/wipe as encrypted data.

## Important

- Permissions are global desktop-tool permissions, not persona permissions.
- The consent card only asks; it does not execute a tool.
- The actual file/app/clipboard/calendar/web implementations are tracked in JUH-29 and JUH-28.

## Web search (JUH-29 start)

The first web tool is a **read-only context search**, not a browser automation tool.

In chat:

```text
/search Counter-Strike 2 latest patch notes
```

It asks for Web search permission on first use, fetches current public results, then sends
the query to the companion with a dated source block. The reply can use that context but
should cite the sources.

The provider is the public Google News RSS feed: no API key, no background polling. Results
are cached for 10 minutes and requests are rate-limited to one every 8 seconds. Only 5
results are kept. The action log records “search the web”, never the query.

For the game companion, use:

```csharp
WebSearchTool.RequestForGame(panel, "Counter-Strike 2", response => { ... });
```

or the Core contract:

```csharp
IWebSearchContext search = host.GetComponent<IWebSearchContext>();
search.Search("Dota 2 latest patch", result => { ... });
```

Automatic game-context search is not wired into the commentary engine yet; the tool and
permissioned context contract are ready for it.

