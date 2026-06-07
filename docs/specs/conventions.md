# Development Conventions & Guidelines

> [← Back to Master Specification](../SPEC.md)

## 5. Commands

```bash
# --- Backend ---
dotnet build
dotnet test
dotnet run --project src/Hosts/Vulperonex.Web
dotnet run --project src/Hosts/Vulperonex.Desktop

# --- CLI (groups: rule / timer / config / member / simulate / twitch) ---
vulperonex simulate chat    "hi" --user-id alice --display-name "Alice"
vulperonex simulate follow  --user-id alice
vulperonex simulate sub     --user-id alice --tier 1000
vulperonex simulate checkin --user-id alice --stamp-count 1 [--skip-cooldown]
vulperonex config get       streaming.platform
vulperonex config set       streaming.platform twitch
vulperonex member list
vulperonex member show      <memberId|prefix>
vulperonex member delete    <memberId|prefix> [--yes]
vulperonex rule list
vulperonex rule show        <ruleId|prefix|--name <name>>
vulperonex rule enable      <ruleId|prefix|--name <name>>
vulperonex rule disable     <ruleId|prefix|--name <name>> [--yes]
vulperonex rule delete      <ruleId|prefix|--name <name>> [--yes]
# Resolution and confirmation flows: docs/phases/phase-5_5-rapid-test/cli-id-resolution-decision.md

# --- Frontend ---
cd src/frontend
pnpm install
pnpm dev          # Vite development server (Photino can point here for hot reload)
pnpm build        # output to ../Hosts/Vulperonex.Web/wwwroot
pnpm test

# --- Quality ---
dotnet format
# Coverage thresholds (see §7.3 for full commands):
dotnet test tests/Vulperonex.Tests.Unit /p:CollectCoverage=true /p:Include="[Vulperonex.Domain]*" /p:Exclude="[*.Tests.*]*" /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=average
dotnet test tests/Vulperonex.Tests.Unit /p:CollectCoverage=true /p:Include="[Vulperonex.Application]*" /p:Exclude="[*.Tests.*]*" /p:Threshold=80 /p:ThresholdType=line /p:ThresholdStat=average
pnpm lint   # Uses oxlint (oxlint.json config, Vue + TypeScript rules)
```

---

## 6. Development Conventions

### 6.1 C# — Domain Events

```csharp
namespace Vulperonex.Domain.Events;

public interface IStreamEvent
{
    /// <summary>
    /// Globally unique event ID. Used for deduplication during TDQ replay upon restart.
    /// Format: ULID string. The adapter must populate this from the platform event ID (if available), otherwise generate a new ULID.
    /// </summary>
    string EventId { get; }

    string EventTypeKey { get; }
    string Platform { get; }
    StreamUser? User { get; }
    DateTimeOffset OccurredAt { get; }
}

public sealed record UserSentMessageEvent : IStreamEvent
{
    // EventId: Use the message ID provided by the platform (if available), otherwise use a new ULID
    public string EventId { get; init; } = Ulid.NewUlid().ToString();
    public string EventTypeKey => StreamEventKeys.UserSentMessage;
    public required string Platform { get; init; }
    public required StreamUser User { get; init; }
    public required string PlainText { get; init; }
    public bool IsFirstChat { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

### 6.2 C# — Bus and Adapter Contracts

```csharp
// Defined in Vulperonex.Adapters.Abstractions (not Application).
// All Adapters (Twitch, Simulation, and future platforms) reference Adapters.Abstractions to implement this interface;
// Application/Domain have no knowledge of the existence of IStreamEventSource.
public interface IStreamEventSource
{
    string Platform { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}

public interface IStreamEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IStreamEvent;
    IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IStreamEvent;

    /// <summary>
    /// Waits until the in-memory queue is empty and all active handlers have completed.
    /// Semantics: handler exceptions are isolated and logged as warnings; WaitForIdleAsync itself does not aggregate or throw handler errors,
    ///            and returns Task.CompletedTask upon completion (does not reflect whether handlers failed).
    ///            CLI --wait utilizes this method, similarly independent of handler error counts.
    /// Only used for integration tests and CLI --wait mode. Not used in production code paths.
    /// </summary>
    Task WaitForIdleAsync(CancellationToken ct = default);
}

public interface IPlatformChatSender
{
    string Platform { get; }
    Task SendAsync(string text, CancellationToken ct);
}
```

### 6.3 C# — Plugin Contracts

```csharp
/// <summary>
/// A singleton-scoped context provided to plugins for use during their lifecycle.
/// Does not carry per-event or per-operation data.
/// </summary>
public interface IPluginContext
{
    IStreamEventBus Events { get; }    // Subscribe and Publish
    ILogger Logger { get; }
    // Note: Do not expose IServiceProvider — avoids the service locator anti-pattern.
    // If plugins need additional services, add explicit properties to this interface (post-MVP extension point).
}

/// <summary>
/// Per-operation invocation context passed to plugin action handlers by the InvokePluginAction executor.
/// Carries specific event data and is not shared across actions or rules.
/// </summary>
public interface IPluginActionContext
{
    /// <summary>
    /// Fully qualified deduplication key: (EventId, WorkflowRuleId, ActionIndex[, InvocationId]).
    /// Plugins must use this complete key (not just EventId) for ActionExecutionLog entries.
    /// The same EventId may appear in multiple rules; using only EventId leads to deduplication conflicts across rules.
    /// </summary>
    string ActionExecutionKey { get; }

    string EventId { get; }
    string WorkflowRuleId { get; }
    int ActionIndex { get; }
    string EventTypeKey { get; }
    StreamUser? User { get; }
    IReadOnlyDictionary<string, JsonElement> Params { get; } // From WorkflowRule action configuration
    ILogger Logger { get; }
    // Note: Do not expose IServiceProvider — avoids the service locator anti-pattern.
}

public interface IVulperonexPlugin
{
    /// <summary>
    /// Unique identifier for the plugin (equivalent to the WorkflowRule InvokePluginAction.PluginId lookup key).
    /// Naming convention: lowercase-kebab, e.g., "my-plugin"; must not contain spaces or special characters.
    /// Name and PluginId use the same string — the PluginId of InvokePluginAction must equal this value.
    /// </summary>
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(IPluginContext ctx, CancellationToken ct);
    Task ShutdownAsync(CancellationToken ct);

    /// <summary>
    /// Called by the InvokePluginAction executor. The ActionId matches the action identifier defined by this plugin.
    /// Plugins must implement deduplication via IPluginActionContext.ActionExecutionKey to handle any external side effects.
    /// Underneath, the Task may still run after timeout — plugins should log a warning upon CancellationToken trigger,
    /// avoiding the side effects of late completion being mistaken for retry results (causing double side effects).
    /// </summary>
    Task ExecuteActionAsync(string actionId, IPluginActionContext ctx, CancellationToken ct);
}
```

### 6.4 TypeScript — Vue Composable

```ts
// composables/useStreamEvents.ts
import { ref, onMounted, onUnmounted } from 'vue';
import * as signalR from '@microsoft/signalr';

export function useStreamEvents() {
  const events = ref<StreamEvent[]>([]);
  const conn = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/events')
    .build();

  onMounted(() => conn.start());
  onUnmounted(() => conn.stop());

  conn.on('event', (e: StreamEvent) => events.value.push(e));
  return { events };
}
```

### 6.5 Coding Standards

- **C#:** PascalCase for types and methods, camelCase for local variables, `_camelCase` for private fields. File-scoped namespaces and Primary Constructors where appropriate.
- **TypeScript:** camelCase for identifiers, PascalCase for components, kebab-case for view filenames.
- **Key Naming Rules:** The `Domain` and `Application` projects must not contain any `Twitch*` (or other platform-specific) prefixes. Platform vocabularies are strictly restricted to their corresponding `Adapters.<Platform>` projects.
- **Enforcement:** `tests/Vulperonex.Tests.Architecture/` runs two complementary NetArchTest gates:
  - `PlatformLeakageTests.Given_DomainAndApplicationAssemblies_When_TypesAreInspected_Then_PlatformSpecificTypeNamesAreAbsent` — rejects any type whose name starts with `Twitch` in the Domain or Application assemblies.
  - `PlatformPrefixIsolationTests` — extends the same rule to `YouTube`, `Kick`, and `OneComme` prefixes, plus an explicit comment whitelisting the platform-neutral `Vulperonex.Application.Twitch` *namespace* (whose types — `IHelixClient`, `PlatformUserProfile`, `PlatformRewardDescriptor` — are intentionally platform-agnostic by name).
- **Resolution log (Phase 7G refactor):** The three previously violating action types were renamed platform-neutral while preserving their JSON type discriminator strings so saved rules continue to load:
  - `LookupTwitchUserAction` → `LookupPlatformUserAction` (discriminator stays `"lookupTwitchUser"`)
  - `RefundTwitchRedemptionAction` → `RefundRewardRedemptionAction` (discriminator stays `"refundTwitchRedemption"`)
  - `TwitchRewardDescriptor` → `PlatformRewardDescriptor`
  - `ShoutoutAction` was already platform-neutral, kept as-is.

---

## 7. Testing Strategy

### 7.1 Test Pyramid

```
                 ╱╲
                ╱  ╲    Architectural Tests (NetArchTest)
               ╱────╲   - Domain has no infrastructure dependencies
              ╱      ╲  - No "Twitch" strings in Domain/Application
             ╱        ╲
            ╱──────────╲ Integration Tests
           ╱            ╲ - SimulationAdapter → Bus → WorkflowEngine → DB
          ╱              ╲
         ╱────────────────╲ Unit Tests (Vast Majority)
        ╱                  ╲ - Domain logic, mapping, handlers, executors
```

### 7.2 Directory Layout

- `tests/Vulperonex.Tests.Unit/` — Pure unit tests, zero I/O.
- `tests/Vulperonex.Tests.Integration/` — In-memory SQLite + SimulationAdapter End-to-End tests.
- `tests/Vulperonex.Tests.Architecture/` — Architecture layer boundary enforcement.
- `src/frontend/tests/` — Vitest + Vue Test Utils.

### 7.3 Coverage Targets

- **Domain Layer (Domain):** > 90% — measured solely against `Vulperonex.Tests.Unit` (domain contains pure logic, zero I/O).
- **Application Layer (Application):** > 80% — measured solely against `Vulperonex.Tests.Unit`. Integration tests **do not** factor into this threshold (coverlet.msbuild cannot merge reports from separate test projects in a single command). If Application unit coverage drops below 80% because behaviors are covered by integration tests, the solution is to write focused unit tests using Fakes/Mocks rather than relaxing thresholds or switching to merged reports.
- **Adapters (Adapters):** Integrated and verified via SimulationAdapter equivalence tests (real adapters share the same domain mapping logic).

**Enforcement:** Uses **`coverlet.msbuild`** (rather than `coverlet.collector`) to fail builds upon dropping below thresholds. Explicit versions are pinned to prevent deviations — using central package management or `<PackageReference Include="coverlet.msbuild" Version="6.0.2" />` (pinned to the latest stable version during project setup). Wildcard versions are **not accepted** for threshold tools.

Two CI commands (both must pass):
```bash
# Domain ≥ 90%
dotnet test tests/Vulperonex.Tests.Unit \
    /p:CollectCoverage=true \
    /p:Include="[Vulperonex.Domain]*" \
    /p:Exclude="[*.Tests.*]*" \
    /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=average

# Application ≥ 80%
dotnet test tests/Vulperonex.Tests.Unit \
    /p:CollectCoverage=true \
    /p:Include="[Vulperonex.Application]*" \
    /p:Exclude="[*.Tests.*]*" \
    /p:Threshold=80 /p:ThresholdType=line /p:ThresholdStat=average
```
Any command dropping below these thresholds exits with a non-zero code, failing the CI build. `reportgenerator` can be added for HTML reports, but does not act as a threshold gate.

> **Drift note (CI not wired):** `coverlet.msbuild` 6.0.2 is referenced, but the repo currently has **no `.github/workflows` / CI pipeline** — these coverage commands, the NetArchTest gates (§6.1/§8.1), and the migration classifier (§4.11) run via local `dotnet test`, not an automated build break. "CI" throughout this spec describes the intended gate; wiring an actual workflow is outstanding.

### 7.4 BDD + TDD Discipline

- Every behavior starts from BDD-style scenarios: Given / When / Then.
- Scenarios serve as acceptance contracts; they must map to one or more automated tests before implementation is considered complete.
- Follow TDD: write a failing test first ("Red"), write the minimal code to pass ("Green"), and then refactor under passing tests.
- New domain logic → write failing unit tests based on BDD scenarios first.
- Bug fixes → reproduce using failing tests before modifying production code.
- Refactoring → guarantee tests remain green.
- Integration scenarios should leverage `SimulationAdapter` as much as possible.
- Manual validation supplements BDD+TDD for Photino, OBS, and browser runtime behaviors, but does not replace automated acceptance tests.

**Test Naming Conventions (Minimum Standard):**
- C# test method names: `Given_<State>_When_<Action>_Then_<Expectation>` (using underscores, PascalCase sections)  
  Example: `Given_ValidRule_When_EventMatches_Then_SendChatMessageCalled`
- If standalone scenario files are not used, BDD scenarios **must** be documented in `// Given / When / Then` comment blocks at the top of the test method body.
- Frontend (Vitest): `describe` = Component/Composable Name; `it` = `should <expectation> when <condition>`

---

## 8. Boundaries

### 8.1 Always Do

- Run all applicable test suites prior to any commits: always execute `dotnet test`; once `src/frontend/` exists, `pnpm test` and `pnpm build` must be executed (can be skipped in backend tasks before Task 19). `pnpm lint` is a **manual validation step** (not enforced by CI), executed once at each checkpoint.
- New events must implement `IStreamEvent` and be immutable `record` types.
- Adapter code must reside in `Adapters/Vulperonex.Adapters.<Platform>/`.
- Platform-specific terms must remain **strictly outside** `Domain` and `Application` projects.
- Use `MemberId` (ULID) as the canonical member key, never platform UserIds.
- Execute architectural tests in CI.

### 8.2 Ask First

- Adding top-level projects to the solution (**Task 1 initial projects are pre-authorized, no need to ask individually; only additional new projects outside Task 1 require asking first**).
- Schema migrations that drop or rename columns.
- Adding NuGet / npm **dependency packages** (including dev tools like oxlint — ask once to install; executing lint after installation is a validation step and does not require asking again). Exception: testing/coverage packages required in Phase 1 Task 1c and explicitly named in this SPEC are pre-authorized and do not require asking individually: `xUnit 3`, `NSubstitute`, `FluentAssertions 7`, `NetArchTest`, and `coverlet.msbuild 6.0.2`.
- Modifying the public plugin contract (`IVulperonexPlugin`).
- Modifying the shape of core domain events after Phase 2.

### 8.3 Never Do

- Reference `Twitch*` (or any platform-specific) types in `Application` or `Domain` projects.
- Modify event objects after publishing (state changes should spawn new events).
- Mix command and query operations on the same repository (lightweight CQRS).
- Bypass the event bus — adapters must never call handlers directly.
- Persist events into the database (logging only).
- Commit credentials, OAuth tokens, or `App_Data/*.db` files.

---
