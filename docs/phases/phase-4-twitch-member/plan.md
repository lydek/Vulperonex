# Phase 4 Detailed Plan: Twitch Adapter + MemberModule

> Parent Plan: `tasks/plan.md` Phase 4
> Scope: Tasks 12-13 only
> Goal: Connect a real Twitch adapter to the Phase 3 event and workflow pipeline, and complete the secure projection of MemberRecord / Overlay DTOs. Phase 4 does not implement Web API CRUD, SignalR hub, or frontend UIs; these are deferred to Phases 5 and 6.

---

## Execution Rules

- Develop each slice on a small branch. Commit immediately after verification. Use `git merge --ff-only` when merging back to `main`.
- For each behavioral requirement, write BDD-style Given / When / Then scenarios first, then implement using TDD RED / GREEN / REFACTOR.
- Task 12 does not add new NuGet packages. If OAuth/WebSocket requires new packages, obtain approval per SPEC ask-first rules.
- The Twitch adapter is an adapter implementation layer. The Domain and Application layers must not reference Twitch types or Twitch payloads.
- The Twitch adapter must publish domain events strictly via `IStreamEventBus.PublishAsync`, not calling the WorkflowEngine directly.
- The `PlatformUserDisplayCache` is updated by the adapter; `MemberModule` does not reference `Vulperonex.Adapters.Abstractions` or `IPlatformUserInfoCache`.
- OAuth refresh tokens must be stored strictly via Task 8's `IOAuthTokenStore`; the Twitch adapter does not call `ISystemSettingsService` directly and does not encrypt tokens itself.
- DisplayHints must not output raw HTML. The security boundary consists of a segment type allowlist and frontend text rendering; do not mutate text values.
- The `--no-build` flag is strictly reserved for commands that immediately follow a successful compilation within the same task.
- Keep `.claude/`, DB files, test outputs, and other local files out of commits.

---

## Dependency Order

```text
Task 12a Twitch Event Type Registration
    -> Task 12b IRC Chat Parser and DisplayHints
    -> Task 12c Twitch to Domain Event Mapping for All MVP Events
    -> Task 12d Twitch and Simulation Workflow Equivalence
    -> Task 12e Connection State and Reconnect Backoff
    -> Task 12f OAuth PKCE Callback and Token Refresh
    -> Task 12g Adapter-Owned Display Cache Updates

Task 13a MemberModule Event Subscription and Member Resolution
    -> Task 13b Member State Updates from Stream Events
    -> Task 13c Simulation and Twitch Member-State Equivalence
    -> Task 13d Overlay DTO Whitelist Contracts
    -> Task 13e Phase 4 Checkpoint Review
    -> Task 13f SC-6a/SC-6b Equivalence Strengthening Follow-Up
```

Task 12 depends on the Phase 3 event/workflow contracts and Phase 2 token/cache infrastructures. Task 13 depends on Task 12 because SC-6b requires comparing outputs and member states between SimulationAdapter and TwitchAdapter.

---

## Task 12a: Twitch Adapter Lifecycle and Event Type Registration

**Description:** Establish the minimal lifecycle and event type registration for `TwitchAdapter` without starting OAuth, IRC, or EventSub socket listeners. This slice only allows the real adapter to register Twitch-supported MVP keys and system connection events during `StartAsync`.

**Acceptance Criteria:**
- [ ] `TwitchAdapter` implements `IStreamEventSource`, located in `Vulperonex.Adapters.Twitch`.
- [ ] `StartAsync` registers seven MVP workflow-visible event keys: message, followed, donated, subscribed, gifted subscription, raided, and reward redeemed.
- [ ] `StartAsync` also registers `platform.connection_changed`, marked as `IsSystemEvent=true`.
- [ ] `StartAsync` is idempotent: duplicate calls do not re-register keys or start duplicate sockets, returning success directly.
- [ ] `StopAsync` is idempotent and does not throw exceptions.
- [ ] The adapter constructor does not require real Twitch credentials; tests can start using fakes.

**Verification:**
- [ ] Unit/integration test: After calling `TwitchAdapter.StartAsync`, `IStreamEventTypeRegistry.IsKnown("user.message") = true`.
- [ ] Unit/integration test: `IsKnownForWorkflow("platform.connection_changed") = false`.
- [ ] Unit test: Start duplication is idempotent; event key registration occurs only once.
- [ ] Architectural test: `Vulperonex.Adapters.Twitch` can reference Domain/Application/Adapters.Abstractions, but Domain/Application does not reference Twitch.

**Dependencies:** Task 9a, Task 9b

**Files Likely Involved:**
- `src/Adapters/Vulperonex.Adapters.Twitch/TwitchAdapter.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/TwitchAdapterEventTypeTests.cs`
- `tests/Vulperonex.Tests.Architecture/Adapters/TwitchAdapterIsolationTests.cs`

**Estimated Size:** S

---

## Task 12b: IRC Chat Parser and DisplayHints

**Description:** Implement the Twitch IRC message payload parser, transforming mock IRC tags into `UserSentMessageEvent` and generating secure display hints (DisplayHints). This slice does not connect to real sockets.

**Acceptance Criteria:**
- [ ] The IRC message parser produces `UserSentMessageEvent { Platform = "twitch" }`.
- [ ] `StreamUser` contains normalized platform user ID, display name, and roles/badges.
- [ ] `display.segments` segment types only permit `text | emote | badge | mention`.
- [ ] HTML-style text preserves raw strings, not outputting raw HTML segment types.
- [ ] `display.color` only accepts `^#[0-9A-Fa-f]{6}$`; does not accept three-digit abbreviations, eight-digit alpha, CSS named colors, or empty strings.
- [ ] Badges are deduplicated, preserving first-occurrence order; badge IDs only permit `[A-Za-z0-9_/\-]`, values max 64 characters, and max 20 badges.
- [ ] `user.avatar` is filled only by first-party Twitch payloads; MVP does not implement URL allowlists, and the overlay side still accesses it strictly via secure DTO projections.
- [ ] `user.is_subscriber` outputs `"true"` / `"false"` strings, sourced from normalized IRC badge/role states.
- [ ] `user.bits_total` outputs integer value strings; unknown or invalid values are omitted.

**Verification:**
- [ ] Unit test: IRC message parsing -> `UserSentMessageEvent`.
- [ ] Unit test: HTML-style text appears only in text segment values.
- [ ] Unit test: Valid hex colors are preserved; invalid colors are omitted.
- [ ] Unit test: Badge normalization deduplicates, filters, and truncates badge IDs/values.
- [ ] Unit test: avatar, is_subscriber, and bits_total display hints format correctly; invalid bits totals are omitted.

**Dependencies:** Task 12a

**Files Likely Involved:**
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcMessage.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcMessageParser.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Display/`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Irc/`

**Estimated Size:** M

---

## Task 12c: Twitch Payload Mapping for All MVP Events

**Description:** Implement mapping from mock Twitch IRC/EventSub payloads to the seven MVP domain events. This slice only handles pure mapping and the publishing path, without socket reconnection or OAuth logic.

**Acceptance Criteria:**
- [ ] Mock Twitch payloads produce the seven MVP `IStreamEvent` types: message, followed, donated, subscribed, gifted subscription, raided, and reward redeemed.
- [ ] All events feature `Platform = "twitch"`.
- [ ] All Twitch-specific payload types do not leak outside the adapter assembly.
- [ ] The publishing path routes strictly through `IStreamEventBus.PublishAsync`.
- [ ] SC-1 passes: mock Twitch payloads -> all seven MVP events are produced.

**Verification:**
- [ ] Unit test: Each payload maps to the correct concrete event type.
- [ ] Unit test: Subscription tiers, gift counts, bits totals, raid viewer counts, and reward IDs/titles are correctly preserved.
- [ ] Unit/integration test: After adapter publishing, the event bus subscribers receive the corresponding events.

**Dependencies:** Task 12b

**Files Likely Involved:**
- `src/Adapters/Vulperonex.Adapters.Twitch/Mapping/`
- `src/Adapters/Vulperonex.Adapters.Twitch/EventSub/`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Mapping/`

**Estimated Size:** M

---

## Task 12d: Twitch and Simulation Workflow Equivalence

**Description:** Verify that the TwitchAdapter mock IRC and SimulationAdapter trigger identical WorkflowEngine side-effects for the same chat payload, fulfilling the WorkflowEngine portion of SC-6a.

**Acceptance Criteria:**
- [ ] SimulationAdapter and TwitchAdapter mock IRC route through the same `IStreamEventBus -> WorkflowEngine -> IPlatformChatSender` path.
- [ ] The same message payload triggers identical matching rules.
- [ ] `IPlatformChatSender.SendAsync` receives identical message payloads and platform routing results.
- [ ] Do not utilize testing shortcuts that call the WorkflowEngine directly.

**Verification:**
- [ ] Integration test: The mock executor calls snapshot for simulation and Twitch mock runs are identical.
- [ ] SC-6a passes in `dotnet test`.

**Dependencies:** Task 12c, Task 10

**Files Likely Involved:**
- `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/`

**Estimated Size:** S

---

## Task 12e: Connection State and Reconnect Backoff

**Description:** Implement the socket lifecycle abstraction and reconnect policy for the Twitch adapter. This slice utilizes a fake clock and fake socket, not connecting to external Twitch servers.

**Acceptance Criteria:**
- [ ] Disconnecting the IRC WebSocket immediately publishes `PlatformConnectionChangedEvent { Platform = "twitch", IsConnected = false, Reason = "reconnecting" }`.
- [ ] Reconnecting successfully publishes `PlatformConnectionChangedEvent { IsConnected = true }`.
- [ ] Reconnect delays utilize exponential backoff (1s -> 2s -> 4s, max 60s) with a ±20% jitter to prevent client reconnect synchronization storming.
- [ ] Replayed events within the EventSub 10-minute replay window are not filtered out by the adapter due to replay flags; they are only skipped if the same `(platform, sourceEventId)` is delivered duplicate times within the deduplication cache. The deduplication cache is capped at 1000 entries or a 10-minute TTL, whichever is reached first.
- [ ] If the EventSub exceeds the replay window, it continues running and logs a warning, without crashing or deadlocking.

**Verification:**
- [ ] Unit test (fake clock + fake socket): First three delays are approx 1s, 2s, 4s, falling within the ±20% range with jitter and not exceeding 60s.
- [ ] Unit test: Disconnect and reconnect publish connection change events in the correct sequence.
- [ ] Unit test: Both missed events within the EventSub replay window are published.
- [ ] Unit test: The same `(platform, sourceEventId)` delivered duplicate times in the deduplication cache is published only once, and cache entries are released after the 10-minute TTL expires.
- [ ] Unit test: EventSub replay timeouts log warnings and continue running.

**Dependencies:** Task 12c

**Files Likely Involved:**
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/EventSub/TwitchEventSubClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Reconnect/`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Reconnect/`

**Estimated Size:** M

---

## Task 12f: OAuth PKCE Callback and Token Refresh

**Description:** Implement the local callback listener, state validation, token exchange, and refresh token storage boundaries for the OAuth PKCE flow. This slice utilizes a mock token endpoint, not connecting to Twitch.

**Acceptance Criteria:**
- [ ] The PKCE flow produces a `code_verifier` and its corresponding `code_challenge`; token exchanges must utilize the same `code_verifier`.
- [ ] `state` is generated as 32 cryptographically random bytes and represented as Base64Url.
- [ ] The callback listener only accepts IPv4 `127.0.0.1` and IPv6 `::1` loopback requests; private IP ranges are rejected.
- [ ] The callback listener validates the Host header, accepting only `localhost:{port}`, `127.0.0.1:{port}`, and `[::1]:{port}` to prevent DNS rebinding attacks.
- [ ] Both the remote IP allowlist and Host header allowlist must pass; satisfying only one is treated as a rejection.
- [ ] The callback listener accepts only the fixed path `/auth/callback`, closing after a single-use successful callback.
- [ ] `state` has a 10-minute TTL and is single-use; rejects mismatching, expired, or already-used states without calling the token exchange endpoint. This TTL aligns with the EventSub replay window to give users buffer time for login and 2FA.
- [ ] The callback port defaults to 7979; conflicts attempt 7980 and 7981; failing explicitly if all are occupied without hanging.
- [ ] Access tokens are stored strictly in memory, not written to databases or log files.
- [ ] Refresh tokens are stored strictly via `IOAuthTokenStore.StoreRefreshTokenAsync("twitch", rawRefreshToken)`.
- [ ] During `StartAsync`, if `IOAuthTokenStore.GetRefreshTokenAsync("twitch")` contains a value, call the mock refresh endpoint to update the in-memory access token.
- [ ] If `GetRefreshTokenAsync` throws a `CredentialDecryptionException`, prompt the user to re-authorize instead of crashing.
- [ ] Add or update `appsettings.json` samples to include `Auth:CallbackPort: 7979` and Twitch redirect URI documentation: Twitch Developer Console requires `http://localhost:7979/auth/callback` as registered redirect URIs, with `http://localhost:7980/auth/callback` and `http://localhost:7981/auth/callback` listed as fallbacks.
- [ ] When all three callback ports are occupied, user-facing error messages must clearly indicate port collisions, prompting the user to close conflicting applications or register available redirect URIs in the Twitch Developer Console.

**Verification:**
- [ ] Unit test: state is a 32-byte random Base64Url, differing between two generation attempts.
- [ ] Unit test: state enforces a 10-minute TTL, rejects expired states, and cannot be reused after a successful callback.
- [ ] Unit test: code challenge is generated from the code verifier, and token exchange utilizes the original verifier.
- [ ] Unit test: state mismatch fails to exchange token codes.
- [ ] Unit test: `127.0.0.1` / `::1` requests are accepted; `192.168.x.x` / non-loopback requests are rejected.
- [ ] Unit test: Rejects Host headers other than `localhost:{port}`, `127.0.0.1:{port}`, or `[::1]:{port}`.
- [ ] Unit test: Paths other than `/auth/callback` are ignored.
- [ ] Unit test: The listener shuts down immediately after a single successful callback.
- [ ] Unit test: Callback port increments to 7980 upon collision.
- [ ] Unit test: The flow fails explicitly without hanging when 7979, 7980, and 7981 are all occupied.
- [ ] Unit test: Access tokens remain in memory after mock exchange; loggers do not contain access tokens, authorization codes, or code verifiers.
- [ ] Unit test: Refresh token storage receives the raw refresh token; loggers do not contain raw refresh tokens or `refresh_token` plain text values.
- [ ] Unit test: Refresh token retrieval and decryption failure flows on startup are verified.

**Dependencies:** Task 8, Task 12a

**Files Likely Involved:**
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/OAuthCallbackListener.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/TwitchOAuthClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/TwitchAccessTokenProvider.cs`
- `src/Hosts/Vulperonex.Web/appsettings.json` (add sample configuration if not already present)
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Auth/`

**Estimated Size:** M

---

## Task 12g: Adapter-Owned Display Cache Updates

**Description:** Direct the Twitch adapter to update `IPlatformUserInfoCache` when handling Twitch events. This responsibility is isolated to the adapter infrastructure layer; the Application and Domain layers remain oblivious to the display cache.

**Acceptance Criteria:**
- [ ] The adapter callback for `UserSubscribedEvent` calls `IPlatformUserInfoCache.UpdateAsync`, setting `IsSubscriber = true`.
- [ ] `UserDonatedEvent.TotalBitsGiven` is Twitch's cumulative absolute value; cache updates utilize monotonic absolute replacement: `TotalBitsGiven = max(existing, incoming)`, rather than delta additions to prevent out-of-order old payloads from regressing values.
- [ ] Replaying the same donation event does not double-accumulate `TotalBitsGiven`.
- [ ] Updates for `UserFollowedEvent` ensure the follower badge appears in the cached badges.
- [ ] Adapter cache updates are not implemented in the `MemberModule`.
- [ ] Open Question: If a user's cumulative bits are manually adjusted downward in the Twitch dashboard, Phase 4 does not automatically regress the local cache value; this will be handled via an explicit administrative reset workflow in subsequent phases.

**Verification:**
- [ ] Unit test: Subscription events update the cache.
- [ ] Unit test: Donation totals use monotonic absolute replacement; replays are idempotent, and out-of-order smaller payloads do not overwrite larger existing values.
- [ ] Unit test: Follow events update follower badges.
- [ ] Architectural test/dependency check: Application/Domain does not reference `IPlatformUserInfoCache`.

**Dependencies:** Task 7, Task 12c

**Files Likely Involved:**
- `src/Adapters/Vulperonex.Adapters.Twitch/Mapping/`
- `src/Adapters/Vulperonex.Adapters.Abstractions/IPlatformUserInfoCache.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/DisplayCache/`

**Estimated Size:** S

---

## Task 13a: MemberModule Event Subscription and Member Resolution

**Description:** Implement `MemberModule`, subscribing to domain events and resolving/creating MemberRecords via `IMemberResolver`. This slice completes the basic member creation for SC-8.

**Acceptance Criteria:**
- [ ] `MemberModule` is located in Application or within boundaries conforming to existing hosted service patterns.
- [ ] Subscribes to `IStreamEvent` or explicit MVP user events, calling `IMemberResolver.ResolveAsync` upon receiving a `UserSentMessageEvent`.
- [ ] Publishing `UserSentMessageEvent` establishes `PlatformIdentity`.
- [ ] `MemberId` is represented as a ULID format.
- [ ] `MemberModule` does not reference `Vulperonex.Adapters.Abstractions` or `IPlatformUserInfoCache`.

**Verification:**
- [ ] Integration test: Publishing `UserSentMessageEvent` -> creates MemberRecord.
- [ ] Integration test: MemberId conforms to the ULID format.
- [ ] Architectural test: MemberModule does not reference adapter abstractions or the display cache.

**Dependencies:** Task 7, Task 10

**Files Likely Involved:**
- `src/Vulperonex.Application/Members/MemberModule.cs`
- `tests/Vulperonex.Tests.Integration/Members/MemberModuleTests.cs`
- `tests/Vulperonex.Tests.Architecture/Members/MemberModuleDependencyTests.cs`

**Estimated Size:** M

---

## Task 13b: Member State Updates from Stream Events

**Description:** Expand `MemberModule` to handle member state updates for subscription/follow events. The display cache is still updated by the adapter; `MemberModule` only handles MemberRecord states.

**Acceptance Criteria:**
- [ ] `UserSubscribedEvent` updates the subscriber status in the MemberRecord.
- [ ] Follow/subscribe events resolve the member before updating states.
- [ ] Update logic is idempotent; TDQ replays do not trigger duplicate accumulation or duplicate rows. Deduplication keys use `(platform, sourceEventId)`, and mock events must provide stable `sourceEventIds` (do not generate a new ULID per-run as replay deduplication keys).
- [ ] MemberModule does not read display hints or use Twitch payloads.

**Verification:**
- [ ] Integration test: `UserSubscribedEvent` -> subscriber status in MemberRecord is updated.
- [ ] Integration test: Replaying the same `(platform, sourceEventId)` subscription event preserves identical member identity and status.
- [ ] Unit/integration test: Paths lacking members resolve them via the resolver before updating.

**Dependencies:** Task 13a

**Files Likely Involved:**
- `src/Vulperonex.Application/Members/MemberModule.cs`
- `src/Vulperonex.Application/Members/`
- `tests/Vulperonex.Tests.Integration/Members/`

**Estimated Size:** S

---

## Task 13c: Simulation/Twitch Member-State Equivalence

**Description:** Complete the MemberRecord portion of SC-6b: SimulationAdapter and TwitchAdapter mock IRC trigger identical MemberRecord database states for the same payload.

**Acceptance Criteria:**
- [ ] Simulation runs utilize a clean SQLite test fixture.
- [ ] Twitch mock runs utilize a separate clean SQLite test fixture.
- [ ] Both environments publish identical user/message payloads, taking a snapshot of the MemberRecord state after the event bus goes idle.
- [ ] Assert S1 == S2; tests must not share database files to prevent false positives.
- [ ] Maintain consistent naming for SC-6a/SC-6b across tests and documentation.

**Verification:**
- [ ] Integration test: SimulationAdapter payload X -> snapshot S1.
- [ ] Integration test: TwitchAdapter mock IRC payload X -> snapshot S2.
- [ ] Assert S1 == S2.

**Dependencies:** Task 12d, Task 13b

**Files Likely Involved:**
- `tests/Vulperonex.Tests.Integration/Members/MemberEquivalenceTests.cs`
- `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`

**Estimated Size:** M

---

## Task 13d: Overlay DTO Whitelist Contracts

**Description:** Establish overlay payload DTOs and whitelist tests, ensuring that JSON targeted for overlays does not leak MemberId, PlatformUserId, TotalLoyalty, LinkedPlatforms, or other internal fields. SignalR hub implementations are deferred to Task 15; this slice only defines DTO contracts and `System.Text.Json` serialization key sets.

**Acceptance Criteria:**
- [ ] The `OverlayChatPayload` JSON property set is exactly `{schemaVersion, eventId, timestamp, displayName, colorHex, segments, badges}`.
- [ ] The `OverlayAlertPayload` JSON property set is exactly `{schemaVersion, eventId, timestamp, displayName, eventType, tier}`.
- [ ] The `OverlayMemberPayload` JSON property set is exactly `{schemaVersion, displayName, avatarUrl, checkInCount}`.
- [ ] `schemaVersion` is fixed to `1`; `eventId` is a public delivery ID for overlay deduplication (do not use MemberId, PlatformUserId, or other internal IDs). Prioritize platform-provided IDs (IRC `msg-id` / EventSub `message_id`), falling back to an adapter-generated ULID marked as synthetic when missing; `timestamp` is the UTC ISO-8601 event time for overlay sorting.
- [ ] `OverlayMemberPayload` is a state snapshot, not an event stream; it does not contain `eventId` / `timestamp` to avoid mistakenly adding event metadata later for symmetry.
- [ ] DTOs do not contain MemberId, UserId, PlatformUserId, TotalBitsGiven, TotalLoyalty, or LinkedPlatforms.
- [ ] Tests utilize exact matching, not just blacklist filtering.
- [ ] Phase 4 does not verify SignalR hub serialization; Task 15 must reuse the same DTOs and verify the SignalR payload key set.

**Verification:**
- [ ] Unit test: Each DTO type reflection matches JSON serializable attributes exactly.
- [ ] Unit test: `System.Text.Json` serialization key set matches exactly.
- [ ] Architectural/code review: Overlay DTOs do not reference persistent entities.
- [ ] Documentation check: SignalR serialization exact key set verification remains deferred to Task 15 and is not treated as completed in Phase 4.

**Dependencies:** Task 13a

**Files Likely Involved:**
- `src/Vulperonex.Application/Overlay/Dtos/OverlayChatPayload.cs`
- `src/Vulperonex.Application/Overlay/Dtos/OverlayAlertPayload.cs`
- `src/Vulperonex.Application/Overlay/Dtos/OverlayMemberPayload.cs`
- `src/Vulperonex.Application/Overlay/OverlayModule.cs`
- `tests/Vulperonex.Tests.Unit/Overlay/`

**Estimated Size:** S

---

## Task 13e: Phase 4 Checkpoint Review

**Description:** Complete Phase 4 wrap-up verification and review gates, executed only after Tasks 12/13 are completed.

**Acceptance Criteria:**
- [ ] Tasks 12a-12g and 13a-13d are completed and committed in small slices.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings.
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] SC-1, SC-6a, SC-6b, and SC-8 pass.
- [ ] Twitch IRC mock -> `UserSentMessageEvent` -> creates MemberRecord passes.
- [ ] Overlay DTO exact whitelist passes.
- [ ] Domain/Application architectural leakage tests continue to pass.
- [ ] `git status --short` is clean; if verifying ignored files, run `git status --short --ignored` to confirm only expected local files appear.

**Review Threshold:**
- [ ] Manually review Twitch adapter boundaries, OAuth/token processing, display cache ownership, MemberModule dependency directions, and Overlay DTO whitelists before starting Phase 5.

**Dependencies:** Task 13d

**Files Likely Involved:**
- `docs/phases/phase-4-twitch-member/todo.md`
- `tasks/todo.md`

**Estimated Size:** S

---

## Task 13f: SC-6a/SC-6b Equivalence Strengthening Follow-Up

**Description:** Phase 5 checkpoints depend on a stronger Phase 4 equivalence gate to compare simulation and Twitch paths beyond chat echo happy paths.

**Acceptance Criteria:**
- [ ] Add follow, subscribe, and donation payload fixtures for simulation and Twitch adapter equivalence comparison.
- [ ] Assert cache states and member state side-effects, not just published chat/action outputs.
- [ ] Assert `TotalBitsGiven` is monotonic, and subscriber tier states match between simulation and Twitch paths.
- [ ] Retain this as a subsequent backlog item unless explicitly exempted by Phase 5 checkpoints.

**Verification:**
- [ ] Integration test: Tests fail if simulation and Twitch paths diverge in follow/subscribe/donation side-effects.
- [ ] Phase 5 checkpoints link to completed follow-up work or record exemptions.

**Files Likely Involved:**
- `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`
- `tests/Vulperonex.Tests.Integration/Members/MemberEquivalenceTests.cs`

**Estimated Size:** S

---

## Phase 4 Checkpoint

**Acceptance Criteria:**
- [ ] `dotnet test` -> SC-1, SC-6a, SC-6b, and SC-8 pass.
- [ ] Twitch IRC mock -> `UserSentMessageEvent` -> creates MemberRecord.
- [ ] Overlay DTO exact whitelist is correct.
- [ ] OAuth PKCE state/callback/token boundary tests pass.
- [ ] DisplayHints segment allowlist and color/badge normalization tests pass.
- [ ] Adapter cache update idempotency tests pass.
- [ ] Complete Phase 4 review before starting Phase 5.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|------|----------|
| Task 12 becomes too large | High | Split into seven slices: registration, parser, mapping, equivalence, reconnect, OAuth, and cache updates. |
| Real Twitch/OAuth causes non-reproducible tests | High | Use mock payloads, fake sockets, and mock token endpoints in Phase 4; defer real login to subsequent manual verification. |
| OAuth secrets leak into logs/configurations | High | Scan logger receivers in OAuth tests; the adapter only holds access tokens in memory and hands refresh tokens strictly to `IOAuthTokenStore`; auth codes, code verifiers, and raw refresh tokens must not be logged. |
| DisplayHints XSS boundaries are muddy | High | Segment allowlists + exact DTO tests; ensuring text values are not rendered as HTML is a frontend responsibility to be verified in Phase 6. |
| MemberModule accesses adapter cache across boundaries | Medium | Architectural tests block Application/Domain from referencing `Vulperonex.Adapters.Abstractions` / `IPlatformUserInfoCache`. |
| SC-6b false positives | Medium | Simulation/Twitch equivalence tests must utilize separate, clean SQLite test environments. |

## Open Questions

- Does Task 12f require a real HTTP listener implementation, or should we use listener abstractions + unit tests to verify single-use/loopback/path/port behaviors first? Recommendation: Use abstractions first to prevent Phase 4 from getting bogged down in OS socket details.
- Should Overlay DTOs be placed strictly in Application, or does the Phase 5 SignalR hub require host-level DTOs? Recommendation: Put Application contracts in Phase 4 first; Phase 5 hub will serialize this DTO directly.
- Re-evaluate synthetic `eventId` deduplication semantics in subsequent multi-overlay-client scenarios in Phase 5: platform-provided IDs identify identical events across clients; adapter fallback ULIDs only guarantee local single-instance delivery IDs.
