# System Specification: Vulperonex — Multi-Platform Live Stream Automation Platform

> **Status:** Approved v0.3 (MVP Scope — Multi-round review completed, ready for Phase 1 implementation)
> **Last Updated:** 2026-05-13
> **Repository:** Greenfield project. This specification describes the **target architecture**.
> **Predecessor Reference:** Omni-Commander (Independent successor — borrowing domain logic concepts, not code)

---

## 1. Goals

**Vulperonex** is a platform-agnostic live streaming automation platform that aggregates events from streaming platforms and drives reactive features (chat overlays, workflows, member tracking, sound effects) via a unified event-driven architecture. The MVP supports Twitch; the architecture is designed to be extensible to other platforms without modifying the Domain or Application layers.

---

## 2. Tech Stack

### Backend (.NET 10 LTS)
* **Language & Runtime:** C# 14 / .NET 10 LTS
* **Web Framework:** ASP.NET Core Minimal API (10.0)
* **Real-time Communication:** SignalR (10.0)
* **ORM:** EF Core 10 (SQLite provider)
* **Desktop Shell:** WebView2 (inside Vulperonex.Desktop host)
* **Testing:** xUnit 3 / NSubstitute / FluentAssertions 7

### Frontend (Vue 3.5+ / Vite 7.x)
* **Framework:** Vue 3.5+ (Standard SFC)
* **Build Tool:** Vite 7.3
* **Language:** TypeScript 6.0
* **UI:** PrimeVue 4 (Unstyled) / UnoCSS
* **State / Communication:** Pinia 2.3 / Axios / `@microsoft/signalr` 10.0
* **Testing:** Vitest 3 / Vue Test Utils 2.5
* **i18n:** vue-i18n 11.x

---

## 3. Project Structure

```
Vulperonex/
├── src/
│   ├── Vulperonex.Domain/                     # Pure domain - Entities, Value Objects, Events
│   ├── Vulperonex.Application/                 # Use cases, Interfaces, Event Bus contract
│   ├── Vulperonex.Infrastructure/              # EF Core, SQLite, Repositories, Persistence
│   ├── Vulperonex.Plugins.Abstractions/        # IVulperonexPlugin contracts
│   ├── Adapters/
│   │   ├── Vulperonex.Adapters.Abstractions/   # Shared adapter interfaces
│   │   ├── Vulperonex.Adapters.Twitch/         # Twitch IRC & EventSub
│   │   └── Vulperonex.Adapters.Simulation/     # Simulation adapter for CLI & UI testing
│   ├── Hosts/
│   │   ├── Vulperonex.Web/                     # ASP.NET Minimal API + SignalR + wwwroot
│   │   ├── Vulperonex.Desktop/                 # Photino/WebView2 shell
│   │   └── Vulperonex.Cli/                     # CLI host
│   └── frontend/                               # Vue 3.5 SPA
└── tests/
    ├── Vulperonex.Tests.Unit/
    ├── Vulperonex.Tests.Integration/
    └── Vulperonex.Tests.Architecture/          # Enforces clean dependency structure
```

---

## 4. Architectural Core Concepts

### 4.1 Clean Architecture
* **Domain:** Contains pure domain entities, value objects, domain events, and invariants. No external dependencies.
* **Application:** Contains use case orchestrations and defines repository/service ports.
* **Infrastructure:** Implements Application ports (EF Core, SQLite).
* **Adapters & Hosts:** Depend on Domain, Application, and Abstractions.

### 4.2 Event Flow & Event Bus
* All stream events flow through `IStreamEventBus`. Adapters never call handlers directly.
* **Ordering:** Unchecked. No guaranteed processing order between different handlers.
* **Isolation:** Exception in one handler is isolated (try/catch), logged, and does not impact other handlers.
* **Backpressure:** Memory channel defaults to 10,000 slots. Excess events write to Transient Delivery Queue (TDQ) SQLite table.
* **Delivery Semantics:** At-least-once. Action execution utilizes `ActionExecutionLog` to deduplicate side effects based on unique key: `(EventId, WorkflowRuleId, ActionIndex, InvocationId?)`.

### 4.3 Identity Model
* **StreamUser:** Platform-specific identity carrying display information (Platform, UserId, DisplayName).
* **MemberRecord:** Persistent member aggregate, created automatically upon first event via `MemberResolver` atomic `INSERT OR IGNORE` SQLite pattern. Identifiers use time-sortable **ULID**.

### 4.4 Domain Events (MVP Collection)
* `UserSentMessageEvent` (`user.message`)
* `UserFollowedEvent` (`user.followed`)
* `UserDonatedEvent` (`user.donated`)
* `UserSubscribedEvent` (`user.subscribed`)
* `UserGiftedSubscriptionEvent` (`user.gifted_sub`)
* `ChannelRaidedEvent` (`channel.raided`)
* `RewardRedeemedEvent` (`reward.redeemed`)

### 4.5 Security and Cryptography
* **Credentials Store:** OAuth Refresh Tokens are encrypted via **AES-256-GCM** inside SQLite `SystemSettings` table.
* **machine.key:** Generated cryptographically random (32 bytes) on first run, stored under OS application data directory with restrictive permissions (`0600` / User-only ACL). Failed permissions setup will fail-fast.
* **PKCE OAuth:** Standard OAuth code flow with Proof Key for Code Exchange (PKCE). Loopback redirection is strictly restricted to `127.0.0.1` and `localhost` with remote IP validation to prevent DNS rebinding.

### 4.6 Database Migrations
* Automated execution of SQLite `Database.MigrateAsync()` on startup (incremental safe migrations only).
* Non-safe destructive migrations (e.g. `DropTable`, `DropColumn`) must go through explicit CLI execution and PR reviews, classified dynamically by the architectural tests.
