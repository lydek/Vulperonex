# Phase 4 Todo - Twitch Adapter + MemberModule

> Plan: `docs/phases/phase-4-twitch-member/plan.md`
> Parent checklist: `tasks/todo.md`

---

## Task 12 - Twitch Adapter

- [x] Task 12a: implement `TwitchAdapter` lifecycle and event type registration.
- [x] Task 12b: implement IRC chat parser and DisplayHints normalization.
- [x] Task 12c: implement seven MVP Twitch payload -> domain event mapping.
- [x] Task 12d: prove SimulationAdapter/TwitchAdapter WorkflowEngine equivalence for SC-6a.
- [x] Task 12e: implement connection state support primitives, reconnect backoff, and EventSub dedup cache.
- [x] Task 12f: implement OAuth PKCE callback/state/token refresh boundary.
- [x] Task 12g: implement adapter-owned display cache updates and idempotent absolute replacement.

## Task 13 - MemberModule + Overlay DTO

- [x] Task 13a: implement `MemberModule` event subscription and member resolution for SC-8.
- [x] Task 13b: implement subscription/follow stream state updates.
- [x] Task 13c: prove Simulation/Twitch MemberRecord DB state equivalence for SC-6b.
- [x] Task 13d: implement Overlay DTO exact whitelist contracts.
- [x] Task 13e: complete Phase 4 checkpoint review.

## Task 13 Follow-up Backlog

- [ ] Task 13f: strengthen SC-6a/SC-6b equivalence with follow/sub/donate payloads and assertions for cache state, member state, `TotalBitsGiven`, and subscriber tier.

## Checkpoint 4

- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings.
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [x] SC-1: mock Twitch payload -> seven MVP `IStreamEvent` concrete mappings pass.
- [x] SC-6a: SimulationAdapter and TwitchAdapter mock message payloads produce matching WorkflowEngine chat side effects.
- [x] SC-6b: SimulationAdapter and TwitchAdapter mock message payloads produce matching MemberRecord DB state.
- [x] SC-8: publishing `UserSentMessageEvent` resolves a `PlatformIdentity` and creates a ULID member id through the resolver.
- [x] Twitch IRC mock -> `UserSentMessageEvent` -> MemberRecord creation passes.
- [x] `platform.connection_changed` registry behavior is `IsKnown=true` and `IsKnownForWorkflow=false`.
- [x] `TwitchAdapter.StartAsync` double-start is idempotent.
- [x] reconnect exponential backoff is 1s -> 2s -> 4s, clamps jitter to +/-20%, and caps at 60s.
- [x] EventSub duplicate delivery uses `(platform, sourceEventId)` dedup cache with 1000-entry capacity and 10-minute TTL.
- [x] OAuth PKCE `state` is 32-byte random Base64Url, 10-minute TTL, single-use; `code_verifier` / `code_challenge` tests pass.
- [x] OAuth PKCE `state` mismatch prevents callback validation and therefore prevents code exchange.
- [x] OAuth callback validation accepts loopback remote IPs, Host header allowlist, fixed `/auth/callback` path, and single-use state.
- [x] OAuth callback port fallback uses 7979 -> 7980 -> 7981 and reports a user-facing error when all are unavailable.
- [x] access token stays in memory; refresh token is handed only to `IOAuthTokenStore`; startup decryption failure requires reauthorization without crashing.
- [x] DisplayHints segment type allowlist, six-digit color format, badge ID/value normalization, `user.avatar`, `user.is_subscriber`, and `user.bits_total` tests pass.
- [x] Adapter display cache update uses monotonic absolute replacement for cumulative bits and is replay safe for out-of-order values.
- [x] `MemberModule` does not reference `Vulperonex.Adapters.Abstractions` or `IPlatformUserInfoCache`.
- [x] Member state replay uses `(platform, sourceEventId)` dedup behavior and does not duplicate member identities.
- [x] Overlay DTO `System.Text.Json` key set exact whitelist tests pass.
- [x] Overlay alert/member payload acceptance passes:
  - alert `{schemaVersion,eventId,timestamp,displayName,eventType,tier}`
  - member `{schemaVersion,displayName,avatarUrl,checkInCount}`
- [x] SignalR serialization exact key-set validation remains assigned to Task 15; Phase 4 owns DTO contract tests only.
- [x] Domain/Application Twitch symbol leakage tests pass.
- [x] Domain coverage gate >90% passes.
- [x] Application coverage gate >80% passes.
- [x] Git staged set is task-scoped; unrelated `docs/design/` remains untracked and uncommitted.
- [x] Phase 4 is ready for Phase 5 planning/review handoff.
