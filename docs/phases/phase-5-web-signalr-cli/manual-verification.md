# Phase 5 Manual Verification

> Related todo: `docs/phases/phase-5-web-signalr-cli/todo.md`

This file records manual checks that cannot be fully proven by automated tests, especially terminal UX, local OAuth browser flow, and overlay/browser behavior.

## Template

```markdown
## YYYY-MM-DD - <verification name>

- Verifier:
- Environment:
- Commands:
- Expected result:
- Actual result:
- Result: PASS / FAIL
- Evidence / commit:
```

## CLI Manual-Test Checklist

Run with a live local Web host and set `VULPERONEX_API_URL` to the active API port.

```powershell
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001
$env:VULPERONEX_API_URL = "http://127.0.0.1:<api_port>"
.\artifacts\cli-manual\Vulperonex.Cli.exe --interactive
```

If localized CLI help shows garbled Chinese in PowerShell, verify again after the UTF-8 setup above before treating the i18n JSON as corrupted.

Required checks:

- `help` shows categorized command groups and aliases.
- `simulate` with no subcommand shows local `chat|follow|sub` help, not `UNKNOWN_COMMAND`.
- `rule create <rule.json>` creates a workflow rule from a JSON file.
- `rule update <rule-id> <rule.json>` updates the same rule from a JSON file.
- `rule delete <rule-id>` removes the rule for cleanup.
- `member seed <platform-user-id> [display-name]` creates test member data through the simulation pipeline.
- `member list` shows the seeded member.
- `member delete <member-id>` removes the seeded member and its platform identities.
- `twitch auth start` starts Twitch OAuth when `Twitch:ClientId` is configured.
- `twitch auth reset` clears the stored refresh token so `twitch auth start` can be repeated.
- `config get oauth.twitch.refresh_token` still returns `OAUTH_CREDENTIAL_NAMESPACE`.

## 2026-05-16 - CLI simulate chat to overlay SignalR

- Verifier: Codex automated integration test
- Environment: Windows, local loopback Kestrel test host
- Commands: `POST /api/simulate/chat` through test HTTP client, SignalR client connected to `/hubs/overlay/chat`
- Expected result: overlay chat hub receives a chat payload within five seconds.
- Actual result: `Given_OverlayChatHub_When_ChatIsSimulated_Then_EventArrivesWithinFiveSeconds` passed.
- Result: PASS
- Evidence / commit: Phase 5 implementation commit

## 2026-05-19 - CLI manual-test UX automation coverage

- Verifier: Codex automated integration tests
- Environment: Windows, Release test build
- Commands: `simulate`, `rule create/update`, `member seed/delete`, `twitch auth reset`
- Expected result: local help and manual-test commands route correctly without calling unintended endpoints.
- Actual result: `CliCommandTests` passed 45 tests; `Phase5EndpointTests` excluding fixed-port exhaustion passed 40 tests.
- Result: PASS
- Evidence / commit: pending
