# Phase 5 Manual Verification

> Parent todo: `docs/phases/phase-5-web-signalr-cli/todo.md`

Manual checks supplement automated tests for browser, OBS, and local SignalR behavior. They do not replace automated acceptance tests.

## Entry Template

```markdown
## YYYY-MM-DD - Scenario Name

- Verifier:
- Environment:
- Command/browser/OBS setup:
- Expected behavior:
- Observed behavior:
- Result: Pass | Fail
- Follow-up issue/commit:
```

## Example Entry

```markdown
## 2026-05-16 - CLI simulate chat reaches overlay SignalR

- Verifier: <name>
- Environment: Windows, local loopback, API port <port>, overlay port <port>
- Command/browser/OBS setup: `vulperonex simulate chat --user test --message "hello"` with overlay chat client connected to `http://localhost:<overlayPort>/overlay/chat`
- Expected behavior: overlay client receives one chat payload with the expected display name and message.
- Observed behavior: <observed result>
- Result: Pass | Fail
- Follow-up issue/commit: <link or commit>
```
