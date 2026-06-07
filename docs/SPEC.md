# Specification: Vulperonex — Multi-Platform Live Stream Automation Platform

> **Status:** Approved v0.5 (MVP shipped; post-MVP feature backlog tracked per phase). v0.5 aligns the spec with shipped code via a commit-range contract audit: the Phase 8 workflow rule model / storage schema / action defaults (typed trigger filters, metadata-driven editor, schema consolidation, NCalc/filter observability — §4.26); the **hardened web-host security model** (`AdminGuardMiddleware` CSRF + Host + Origin/Referer, opt-in LAN overlay with access key — §4.17/OQ5/OQ4); overlay chat personas + workflow chat output routing and daily check-in reset/repeat (§4.14); and the real CLI surface (§4.11/§5). v0.4 closed drift uncovered by the 2026-06-01 spec-vs-impl review.
> **Last Updated:** 2026-06-05
> **Repository:** Brand-new greenfield project repository. This specification describes the **target architecture**. No existing code to migrate.
> **Predecessor Reference:** Omni-Commander (Independent successor — borrowing domain logic concepts, not code)

---

## 1. Goals

**Vulperonex** is a platform-agnostic live stream automation tool that aggregates events from live streaming platforms and drives reactive features (chat overlays, workflows, member tracking, sound effects) via a unified event-driven architecture. The MVP supports Twitch; the architecture is designed to be extensible to other platforms without modifying the Domain/Application layers.

**Why:** Existing tools (including the predecessor project Omni-Commander) are tightly coupled with Twitch-specific concepts. Vulperonex decouples platform-specific event sources from feature consumers via Domain Events, enabling the addition of new platforms with minimal code and supporting testing/simulation as first-class event sources.

### Target Users

- **Streamers:** Running Twitch (MVP scope).
- **Plugin Authors:** Extend behaviors via `IVulperonexPlugin` — capable of both consuming and publishing events.
- **Developers:** Run automated tests against the same event streams as the actual platforms.

### MVP Success Vision

- Twitch Adapter publishes domain events to the Event Bus.
- WorkflowEngine subscribes to and executes rules.
- Desktop shell (Photino) hosts the Vue UI provided by the Web host.
- CLI can simulate events, manage configuration, manage rules, and check members.
- Adding new platform Adapters requires no modification to the Application / Domain layers (architectural isolation).

---

## 2. Tech Stack

### Backend (.NET 10 LTS)

| Item | Choice |
|---|---|
| Language / Runtime | C# 14 / .NET 10 LTS |
| Web Framework | ASP.NET Core Minimal API (10.0) |
| Real-time Communication | SignalR (10.0) |
| ORM | EF Core 10 (SQLite provider) |
| Desktop Shell | Photino.NET 3.x |
| Plugin System | `IVulperonexPlugin` (Custom contract, statically referenced at startup in MVP) |
| Twitch transport (host-only) | `TwitchLib.Client 4.0.1` (IRC chat) + `TwitchLib.EventSub.Websockets 0.8.0` (EventSub WebSocket + typed event handlers). Referenced **only** from `Vulperonex.Web` (host) — `Vulperonex.Adapters.Twitch` stays free of third-party SDKs so the bus boundary remains testable without TwitchLib types. |
| Unit Testing | xUnit 3 / NSubstitute / FluentAssertions 7 |
| Testing Methodology | BDD Scenario Definition + TDD Red/Green/Refactor Implementation |

### Frontend (Vue 3.5+ / Vite 7.x)

| Item | Choice |
|---|---|
| Framework | Vue 3.5+ (Standard SFC — Vapor Mode deferred to Phase 2 performance experiments) |
| Build Tool | Vite 7.3 (Rolldown; MVP pinned to v7, not upgrading to v8 — Vite 8 is released but will not be upgraded to during MVP) |
| Language | TypeScript 5.8 (`vue-tsc` 2.2) |
| UI | PrimeVue 4 (Unstyled) / UnoCSS 66 (Preset Wind) / Reka UI 2.9 (headless, Phase 8) |
| State / Communication | Pinia 3 / Axios / @microsoft/signalr 9 / vue-router 4.5 |
| Testing | Vitest 3 / Vue Test Utils 2.4 / @vitest/coverage-v8 3 / jsdom |
| Lint | oxlint 0.16 |
| i18n | vue-i18n 11.x |

---

## 3. Project Structure

```
Vulperonex/
├── src/
│   ├── Vulperonex.Domain/                     # Pure domain — Entities, Value Objects, Events
│   ├── Vulperonex.Application/                 # Use cases, Interfaces, Event Bus contract
│   ├── Vulperonex.Infrastructure/              # EF Core, SQLite, Repositories, Persistence
│   ├── Vulperonex.Plugins.Abstractions/        # IVulperonexPlugin contract (source + sink)
│   │
│   ├── Adapters/
│   │   ├── Vulperonex.Adapters.Abstractions/   # Shared adapter interfaces (IPlatformUserInfoCache etc.)
│   │   ├── Vulperonex.Adapters.Twitch/         # Twitch IRC + EventSub (Incoming + Outgoing)
│   │   └── Vulperonex.Adapters.Simulation/     # CLI / UI / Test simulation
│   │
│   ├── Hosts/
│   │   ├── Vulperonex.Web/                     # ASP.NET Minimal API + SignalR + wwwroot
│   │   ├── Vulperonex.Desktop/                 # Photino shell (wraps Web)
│   │   └── Vulperonex.Cli/                     # CLI: Simulation / Config / Rules / Member commands
│   │
│   └── frontend/                               # Vue 3.5 SPA, built to Web/wwwroot
│       ├── src/
│       │   ├── components/
│       │   ├── composables/
│       │   ├── stores/
│       │   ├── views/
│       │   └── i18n/
│       └── tests/
│
├── tests/
│   ├── Vulperonex.Tests.Unit/
│   ├── Vulperonex.Tests.Integration/
│   └── Vulperonex.Tests.Architecture/          # Layer / Dependency rule enforcement
│
├── docs/                                        # SPEC.md official path: docs/SPEC.md
│   ├── SPEC.md
│   ├── adr/                                     # Architecture Decision Records
│   └── plugins/
│
└── tools/
```

---


## Modular Specifications (Table of Contents)

The main specification has been split into modular documents to keep it clean and optimized:

- **[Architectural Core Concepts](specs/architecture.md)**: Details on layered architecture, tactical DDD boundaries, DCI-inspired decomposition, event flows, modules, and database migrations (§4.1 - §4.17).
- **Development Conventions & Guidelines**:
  - **[Conventions, Commands & Testing](specs/conventions.md)**: Command reference, coding standards, test pyramid, coverage targets, and BDD+TDD discipline (§5 - §8).
- **Feature Specifications (Phase 7 & Phase 8)**:
  - **[Monitor Dashboard](specs/features/monitor-dashboard.md)**: Specs for Unified Monitor page, simulating controls, module & plugin registry, and layout (§4.18, §4.20 - §4.22).
  - **[Member Admin surface](specs/features/member-admin.md)**: Spec for editing member loyalty, check-in, and append-only SQLite audits (§4.19).
  - **[Twitch Badges & Channel-Point Rewards](specs/features/badge-and-reward-cache.md)**: Helix APIs, in-memory cache sync, simulated user updates, and picker UI (§4.23, §4.25).
  - **[Global Theme System](specs/features/theme-system.md)**: App tokens (`--vp-*`), runtime contract, and settings surface (§4.24).
  - **[Workflow Rule Typed Filters](specs/features/typed-filter-and-observability.md)**: Schema consolidation, typed trigger filter matchers, dynamic metadata providers, and Warning-level expression logging (§4.26).
- **Appendix & Decisions**:
  - **[Resolved Decisions, MVP criteria & OQs](specs/decisions-and-queries.md)**: Success criteria, design decisions (D1 - D8), and resolved queries (OQ1 - OQ6) (§9 - §12).
