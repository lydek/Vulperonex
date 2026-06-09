# Functional Specification: Platform Login Handle Display

> [← Back to Master Specification](../../SPEC.md)

### 4.28 Platform Login Handle Display (Phase 9)

**Background & Motivation:**

The member management view (`src/frontend/src/views/admin/MembersView.vue`, in the "Twitch Account" column) originally displayed `@{platformUserId}`. For Twitch, `platformUserId` is an immutable **numeric user ID** (e.g. `@109565589`), rather than the human-readable login handle (login / nick) familiar to operators.

The Domain layer member identity (`PlatformIdentity`) is composed solely of `(platform, platformUserId)`, intentionally avoiding platform-specific fields. Display-oriented fields such as `displayName`, `avatarUrl`, and `isSubscriber` are not stored in the identity table; instead, they are retrieved via a JOIN with the **display info cache** (`PlatformUserDisplayInfo`) at query time. The Twitch **login** (human-readable account name) was previously not captured, stored, or presented at all.

This feature bridges `login` end-to-end: capturing it from IRC messages, writing it to the user display info cache, exposing it to the API through read models, and finally rendering it as `@{login}` in the admin UI.

---

**Design & Specifications:**

1. **Domain Event Capture of login (without polluting the identity model):**
   - `StreamUser` (`Vulperonex.Domain`) adds a trailing optional parameter `string? Login = null`. This is a generic term, ensuring the Domain/Application layers do not depend on any Twitch-specific types.
   - The Twitch IRC parser (`TwitchIrcMessageParser.Parse`) populates `StreamUser.Login` using `message.UserName` (the IRC login/nick, which also acts as the fallback source for both `platformUserId` and `displayName`).

2. **Display Info Cache (reusing existing JOIN path instead of adding fields to Member Identity):**
   - The `PlatformUserDisplayInfo` record in the Adapter abstraction layer (`IPlatformUserInfoCache`) adds a trailing optional `string? Login = null`.
   - `TwitchDisplayCacheUpdater.ApplyChatAsync` writes it via `with { ... }` as `Login = streamEvent.User.Login ?? current.Login` (using a coalesce operator to preserve the existing value, preventing erasure if an event lacks it).
   - Infrastructure: `PlatformUserDisplayInfoEntity` adds `string? Login`; `PlatformUserDisplayInfoConfiguration` configures `Login` as `TEXT`; `PlatformUserDisplayCache.FromEntity` / `Apply` support bi-directional mapping.

3. **Read Model and API:**
   - `PlatformIdentityReadModel` (`MemberDtos`) in the Application layer adds a trailing optional `string? Login = null`.
   - `IPlatformUserDisplayInfoProvider.PlatformUserDisplayInfo` in the Application layer adds a trailing optional `string? Login`, which is returned by `PlatformUserDisplayInfoProvider` after reading from cache.
   - `MemberQueryService` (`ListAsync` / `FindByMemberIdAsync`) passes `displayInfo?.Login` when constructing `PlatformIdentityReadModel`.
   - Web `MemberEndpoints` directly returns `MemberReadModel` (serialized via System.Text.Json camelCase), so `Login` is automatically serialized as `login` without requiring extra Web DTO mapping.

4. **Frontend Presentation:**
   - The `PlatformIdentity` interface in `client.ts` adds `login?: string | null`.
   - `MembersView.vue` adds a helper `getLoginHandle(member)`, returning `primary.login || primary.platformUserId || member.memberId`.
   - The "Twitch Account" column in both the identity management table and the check-in management table is updated from `@{{ getPrimaryIdentity(member)?.platformUserId || member.memberId }}` to `@{{ getLoginHandle(member) }}`.
   - The numeric ID column in the details panel remains unchanged, displaying `platformUserId` (as the details panel is the appropriate place for standard, normalized IDs).

---

**Acceptance Criteria:**

- The "Twitch Account" column in the member list displays `@{login}` (the human-readable login handle) instead of the numeric ID after the member has generated at least one event.
- For existing members who have not generated any events, `login` defaults to null and falls back to displaying `@{platformUserId}` without breaking the UI layout.
- The identity object returned by the APIs `GET /api/members` and `GET /api/members/{id}` includes the `login` field (when populated).
- The EF migration `AddPlatformUserLogin` adds a nullable `Login` TEXT column to `PlatformUserDisplayInfo`, and the database snapshot is updated accordingly.
- Unit test coverage: `TwitchIrcMessageParser.Parse` populates `StreamUser.Login` from `message.UserName`; `Login` written to `PlatformUserDisplayCache` can be read back successfully (L1 + L2 cache round-trip).

---

**Boundaries & Back-fill:**

- The `login` is only stored in the user display info cache and is **not** added to the member identity table, meaning no member identity schema migration is required.
- This is a purely additive and nullable change with no destructive data modifications. All new parameters are trailing optional parameters, leaving existing positional constructors unaffected.
- **Back-fill Strategy:** Existing members will continue to display their numeric IDs until they generate a new event (such as sending a chat message); `login` will be progressively back-filled as events arrive. Batch resolution/back-filling using Helix APIs for historical records is **out of scope** for this phase.
