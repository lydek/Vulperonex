# Workflow Expression Context Terminology

Status: draft decision for Phase 8 cleanup

## Why this exists

The workflow editor currently mixes three different ideas:

1. Trigger event data
2. The user who caused the trigger
3. Persisted member data

Those are not the same thing. When the editor exposes them with overlapping names,
users end up guessing between `Trigger.UserId`, `Member.UserId`, and platform
overrides even though the runtime only guarantees one of them today.

This note defines the current contract and the wording we should use in UI and docs.

## Current runtime contract

### `Trigger.*`

`Trigger` means the current event payload plus event-level metadata.

Examples:

- `Trigger.EventId`
- `Trigger.EventTypeKey`
- `Trigger.Platform`
- `Trigger.OccurredAt`
- `Trigger.MessageText`
- `Trigger.RewardId`
- `Trigger.RewardTitle`
- `Trigger.RedemptionId`
- `Trigger.TotalBitsGiven`
- `Trigger.Tier`
- `Trigger.GiftCount`
- `Trigger.ViewerCount`
- `Trigger.Depth`
- `Trigger.Payload.*`

`Trigger` is event-scoped. It should not be used as a grab bag for user identity
aliases unless the backend actually writes those fields into the event context.

### `Member.*`

`Member` currently means the trigger user snapshot carried by `streamEvent.User`.

Examples:

- `Member.UserId`
- `Member.Platform`
- `Member.DisplayName`
- `Member.Roles`
- `Member.IsSubscriber`
- `Member.IsModerator`
- `Member.IsVip`
- `Member.IsFollower`
- `Member.IsBroadcaster`

Important: in the current implementation this is not a hydrated persistent
member read model. It is the event user snapshot exposed to expressions.

### Persistent member data

Persistent member data belongs to the member domain (`MemberRecord`,
`PlatformIdentity`, loyalty state, audit history). It is not directly available
inside the workflow expression context today.

## Naming rules for UI

Until we do a larger namespace rename, the editor should present the scopes with
clearer wording:

- `Trigger` group label: `Trigger Event Context`
- `Member` group label: `Trigger User Context`

Field labels under `Member.*` should explicitly say "trigger user", not just
"member", because the runtime value is sourced from the current event user.

## Action guidance

### Check-in

For normal chat-triggered check-in:

- user id should default to `Member.UserId`
- platform should default to the trigger event platform

That means the common UX should read as:

`Check in the user who triggered this event`

Advanced overrides can still expose:

- `UserId`
- `Platform`

### Other user-targeted actions

Actions such as lottery ticket updates should also prefer `Member.UserId` as the
default target when they mean "the current triggering user".

## Action audit snapshot

Current review result for user/platform-related actions:

- `triggerCheckIn`
  - Issue: editor previously implied manual target selection was the normal flow
  - Decision: default to implicit trigger user; keep `UserId` and `Platform` in advanced overrides
- `addLotteryTickets`
  - Issue: help text was too vague about the default target
  - Decision: keep the field editable, but document that the default target is `Member.UserId`
- `lookupTwitchUser`
  - Result: no contract mismatch found; both `Login` and `UserId` are explicit lookup inputs
- `sendChatMessage`
  - Result: internal routing fields (`TargetPlatform`, `Channel`) remain intentionally hidden from the visual editor

## Contract cleanup rule

Frontend variable pickers, i18n hints, metadata help text, and backend
`ExpressionContext` must describe the same contract.

If a variable is not populated by the backend, the editor must not suggest it.

## Known stale references

Some older samples still mention placeholders such as `Trigger.Arg0` or
`Trigger.DisplayName`. Those are not part of the current backend
`ExpressionContext` contract and should be treated as stale examples until they
are rewritten.
