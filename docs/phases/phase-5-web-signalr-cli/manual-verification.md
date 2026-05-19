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

When using `tools/cli.ps1`, set `$env:VULPERONEX_API_PORT = "<api_port>"` to force a single port probe, or pass `-ApiUrl http://127.0.0.1:<api_port>`.

If localized CLI help shows garbled Chinese in PowerShell, verify again after the UTF-8 setup above before treating the i18n JSON as corrupted.

Required checks:

- `help` shows categorized command groups and aliases.
- `simulate` with no subcommand shows local `chat|follow|sub` help, not `UNKNOWN_COMMAND`.
- `rule create <rule.json>` creates a workflow rule from a JSON file.
- `rule update <rule-id> <rule.json>` updates the same rule from a JSON file.
- `rule disable <rule-id>` prints `OK rule disabled: <rule-id>`.
- `rule enable <rule-id>` prints `OK rule enabled: <rule-id>`.
- `rule delete <rule-id>` prints `OK rule deleted: <rule-id>` and removes the rule for cleanup.
- `simulate chat|follow|sub` prints a JSON acknowledgement containing `accepted`, `eventTypeKey`, `eventId`, `platformUserId`, and `displayName`; use `eventId` to correlate Web/SignalR logs.
- `member seed <platform-user-id> [display-name]` creates test member data through the simulation pipeline and prints `OK member available: <member-id>` once the member can be listed.
- `member list` shows the seeded member.
- `member delete <member-id>` prints `OK member deleted: <member-id>` and removes the seeded member and its platform identities.
- `twitch auth start` starts Twitch OAuth when `Twitch:ClientId` is configured.
- `twitch auth reset` prints `OK Twitch authorization reset` and clears the stored refresh token so `twitch auth start` can be repeated.
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
- Commands: `simulate`, `rule create/update/enable/disable/delete`, `member seed/delete`, `twitch auth reset`
- Expected result: local help and manual-test commands route correctly without calling unintended endpoints.
- Actual result: `CliCommandTests` passed 45 tests; `Phase5EndpointTests` excluding fixed-port exhaustion passed 40 tests.
- Result: PASS
- Evidence / commit: pending

## 2026-05-19 - CLI empty-success feedback and member seed fix

- Verifier: Codex automated integration tests
- Environment: Windows, Release test build
- Commands: `simulate chat`, `simulate follow`, `simulate sub`, `member seed`, `rule enable`, `rule disable`, `rule delete`, `twitch auth reset`
- Expected result: simulate commands print a traceable JSON acknowledgement with `eventId`; other commands with empty HTTP success bodies print explicit `OK ...` output; member simulation events are consumed by the Web host and become visible in `member list`.
- Actual result: `CliCommandTests` passed 49 tests; `Phase5EndpointTests` excluding fixed-port exhaustion passed 40 tests.
- Result: PASS
- Evidence / commit: pending

## 2026-05-19 - Simulate event acknowledgement

- Verifier: Codex automated integration tests
- Environment: Windows, Release test build
- Commands: `simulate chat hello from cli`
- Expected result: Web API returns `202 Accepted` with JSON acknowledgement containing `accepted`, `eventTypeKey`, `eventId`, `platformUserId`, `displayName`, and `occurredAt`.
- Actual result: `CliCommandTests` and `Phase5EndpointTests` verify traceable simulate output.
- Result: PASS
- Evidence / commit: pending
