# Phase 5 Event ID Decision

> Parent Task: `docs/phases/phase-5-web-signalr-cli/plan.md` Task 15b

## Status

Decided — Task 15b overlay forwarding has been implemented based on this decision.

## Decisions to Record

- The `eventId` in the overlay payload utilizes the domain event's public event ID.
- Platform-provided event IDs allow cross-overlay-client identification of the same source event; fallback ULIDs from adapters serve strictly as local single-instance delivery IDs.
- Overlay public `eventId`s must not contain `MemberId`, `PlatformUserId`, or other internal identity identifiers.
- Phase 5 SignalR contract tests verify that `schemaVersion`, `eventId`, and chat segment payloads appear in the wire payload.

## Review Notes

- Reviewer: Codex
- Date: 2026-05-16
- Decision: Adopt domain event public ID as overlay `eventId`, permitting local synthetic IDs when platform source IDs are missing.
- Follow-up: If Phase 6 introduces multi-overlay client replay/dedup, re-evaluate whether synthetic IDs are sufficient for cross-client deduplication.
