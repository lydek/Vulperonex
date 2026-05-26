# Phase 4 Todo List - Twitch Adapter + MemberModule

> Plan: `docs/phases/phase-4-twitch-member/plan.md`
> Parent Checklist: `tasks/todo.md`

---

## Task 12 - Twitch Adapter

- [x] Task 12a: Implement `TwitchAdapter` lifecycle and event type registration.
- [x] Task 12b: Implement IRC chat parser and DisplayHints normalization.
- [x] Task 12c: Implement mapping from seven MVP Twitch payloads to domain events.
- [x] Task 12d: Prove SimulationAdapter/TwitchAdapter WorkflowEngine equivalence for SC-6a.
- [x] Task 12e: Implement connection state support primitives, reconnect backoff, and EventSub deduplication cache.
- [x] Task 12e: Implement OAuth PKCE callback/state/token refresh boundaries.
- [x] Task 12g: Implement adapter-owned display cache updates and idempotent absolute value replacement.

## Task 13 - MemberModule + Overlay DTO

- [x] Task 13a: Implement `MemberModule` event subscription and member resolution for SC-8.
- [x] Task 13b: Implement subscriber/follower stream status updates.
- [x] Task 13c: Prove simulation/Twitch MemberRecord database state equivalence for SC-6b.
- [x] Task 13d: Implement Overlay DTO exact whitelist contracts.
- [x] Task 13e: Complete Phase 4 checkpoint review.

## Task 13 Follow-up Backlog

- [x] Task 13f: Strengthen SC-6a/SC-6b equivalence, including follower/subscriber/donation payloads, and assertions on cache states, member states, `TotalBitsGiven`, and subscriber tiers.

## Checkpoint 4

- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings.
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [x] SC-1: Mock Twitch payload -> seven MVP `IStreamEvent` concrete mapping passes.
- [x] SC-6a: SimulationAdapter and TwitchAdapter mock message payloads produce matching WorkflowEngine chat side-effects.
- [x] SC-6b: SimulationAdapter and TwitchAdapter mock message payloads produce matching MemberRecord database states.
- [x] SC-8: Publishing `UserSentMessageEvent` resolves `PlatformIdentity` and establishes ULID member IDs via the resolver.
- [x] Twitch IRC mock -> `UserSentMessageEvent` -> MemberRecord creation passes.
- [x] `platform.connection_changed` registration behavior is `IsKnown=true` and `IsKnownForWorkflow=false`.
- [x] `TwitchAdapter.StartAsync` duplicate initialization is idempotent.
- [x] Reconnect exponential backoff is 1s -> 2s -> 4s, with jitter capped at +/-20% and max delay capped at 60s.
- [x] EventSub duplicate delivery uses a `(platform, sourceEventId)` deduplication cache with a 1000-entry capacity and a 10-minute TTL.
- [x] OAuth PKCE `state` is a 32-byte random Base64Url, with a 10-minute TTL, single-use; `code_verifier` / `code_challenge` tests pass.
- [x] OAuth PKCE `state` mismatch blocks callback validation, preventing token exchanges.
- [x] OAuth callback validation accepts loopback remote IPs, Host header allowlists, fixed `/auth/callback` paths, and single-use states.
- [x] OAuth callback port backoff uses 7979 -> 7980 -> 7981, reporting client-side errors when all are occupied.
- [x] Access tokens remain in memory; refresh tokens are stored strictly in `IOAuthTokenStore`; decryption failures on startup request re-authorization without crashing.
- [x] DisplayHints segment type allowlists, six-digit color formats, badge ID/value normalization, `user.avatar`, `user.is_subscriber`, and `user.bits_total` tests pass.
- [x] Adapter display cache updates for cumulative bits utilize monotonic absolute replacement, ensuring replay safety for out-of-order payloads.
- [x] `MemberModule` does not reference `Vulperonex.Adapters.Abstractions` or `IPlatformUserInfoCache`.
- [x] Member status replay uses `(platform, sourceEventId)` deduplication, avoiding duplicate member records.
- [x] Overlay DTO `System.Text.Json` key set exact whitelist tests pass.
- [x] Overlay alert/member payload validations pass:
  - Alert: `{schemaVersion,eventId,timestamp,displayName,eventType,tier}`
  - Member: `{schemaVersion,displayName,avatarUrl,checkInCount}`
- [x] SignalR serialization exact key set verification remains deferred to Task 15; Phase 4 only possesses DTO contract tests.
- [x] Domain/Application Twitch symbol leakage tests pass.
- [x] Domain coverage gate >90% passes.
- [x] Application coverage gate >80% passes.
- [x] Git staging set is limited to the task scope; unrelated `docs/design/` remains untracked and uncommitted.
- [x] Phase 4 is prepared for Phase 5 planning/review handoff.
