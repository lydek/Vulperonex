# Phase 1 Detailed Plan: Solution Skeleton + Domain Foundation

> Parent Plan: `tasks/plan.md` Phase 1
> Scope: Tasks 1-3 only
> Goal: Establish a buildable .NET solution, then set up the Domain / Application foundation including tests and architectural guardrails.

---

## Execution Rules

- Develop each slice on a small branch, then merge back into `main` using `git merge --ff-only`.
- Commit each verified slice before beginning the next one.
- Do not add packages without prior inquiry and approval. Phase 1 only permits adding packages named in `docs/SPEC.md` or this plan and required by the current task; all other packages still require prior inquiry and approval.
- The verification of each slice must compile the current slice before executing `--no-build`. The `--no-build` flag is strictly reserved for commands that immediately follow a successful compilation within the same task or checkpoint.
- Ensure that `.claude/` and other local-only files are not included in the commits.
- For code with behavior, use BDD/TDD. Project setups containing only skeletons may use compilation/reference verification instead of behavioral tests.

---

## Dependency Order

```
Task 1a Repository/Solution Configuration
    -> Task 1b Production Projects
    -> Task 1c Test Projects
    -> Task 1d Project References
    -> Task 1e Baseline Compilation
        -> Task 2a Event Contracts
        -> Task 2b Concrete Events
        -> Task 2c Event Descriptions/Tests
        -> Task 2d Architectural Rules to Prevent Platform Leakage
            -> Task 3a Member Entities/Value Objects
            -> Task 3b Application Layer Member Ports
            -> Task 3c Member Domain Tests
            -> Task 3d DCI Role Isolation Tests
```

---

## Task 1a: Establish Solution-Level Build Configuration

**Description:** Create the solution file and shared .NET configuration files so that all projects inherit consistent language versions, nullability, analyzers, and package version behaviors.

**Acceptance Criteria:**
- [ ] `Vulperonex.sln` exists.
- [ ] Shared compilation settings are established (C# 14 / .NET 10 with nullability enabled).
- [ ] No production or test logic is introduced in this slice.

**Verification:**
- [ ] `dotnet --info` confirms SDK availability.
- [ ] `dotnet sln Vulperonex.sln list` executes successfully.

**Dependencies:** None

**Files Likely Involved:**
- `Vulperonex.sln`
- `Directory.Build.props`
- `Directory.Packages.props` (if using Central Package Management)

**Estimated Size:** S (Small)

---

## Task 1b: Add Production Project Skeletons

**Description:** Add all production projects named in the approved architecture without business logic.

**Acceptance Criteria:**
- [ ] `.csproj` files for Domain, Application, Infrastructure, Plugins.Abstractions, Adapters.Abstractions, Adapters.Twitch, Adapters.Simulation, Web host, Cli host, and Desktop host exist.
- [ ] The TargetFramework for the Desktop host is `net10.0-windows`.
- [ ] Project references do not violate the dependency graph.

**Verification:**
- [ ] `dotnet sln Vulperonex.sln list` displays all production projects.
- [ ] `dotnet restore Vulperonex.sln /m:1 /nr:false /p:UseSharedCompilation=false` executes successfully. If the environment requires repository-local NuGet isolation, use `--configfile NuGet.Config` after creating or confirming that file.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` reaches the compilation phase without project discovery errors.

**Dependencies:** Task 1a

**Files Likely Involved:**
- `src/Vulperonex.Domain/Vulperonex.Domain.csproj`
- `src/Vulperonex.Application/Vulperonex.Application.csproj`
- `src/Vulperonex.Infrastructure/Vulperonex.Infrastructure.csproj`
- `src/Vulperonex.Plugins.Abstractions/Vulperonex.Plugins.Abstractions.csproj`
- `src/Adapters/Vulperonex.Adapters.Abstractions/Vulperonex.Adapters.Abstractions.csproj`
- `src/Adapters/Vulperonex.Adapters.Twitch/Vulperonex.Adapters.Twitch.csproj`
- `src/Adapters/Vulperonex.Adapters.Simulation/Vulperonex.Adapters.Simulation.csproj`
- `src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj`
- `src/Hosts/Vulperonex.Cli/Vulperonex.Cli.csproj`
- `src/Hosts/Vulperonex.Desktop/Vulperonex.Desktop.csproj`

**Estimated Size:** M (Medium)

---

## Task 1c: Add Test Project Skeletons

**Description:** Add three test projects and prepare them for unit, integration, and architectural testing.

**Acceptance Criteria:**
- [ ] Unit, integration, and architectural test projects exist and are included in the solution.
- [ ] Test suite selection follows `docs/SPEC.md`; Phase 1 authorizes SPEC-named testing and coverage suites required for this task (`xUnit 3`, `NSubstitute`, `FluentAssertions 7`, `NetArchTest`, `coverlet.msbuild 6.0.2`).
- [ ] Each test project contains placeholder smoke tests only as needed to prove successful environment setup.

**Verification:**
- [ ] `dotnet restore Vulperonex.sln /m:1 /nr:false /p:UseSharedCompilation=false` executes successfully. If the environment requires repository-local NuGet isolation, use `--configfile NuGet.Config` after creating or confirming that file.
- [ ] `dotnet test tests/Vulperonex.Tests.Unit /m:1 /nr:false /p:UseSharedCompilation=false` discovers the test project.
- [ ] `dotnet test tests/Vulperonex.Tests.Integration /m:1 /nr:false /p:UseSharedCompilation=false` discovers the test project.
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false` discovers the test project.

**Dependencies:** Task 1a

**Files Likely Involved:**
- `tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj`
- `tests/Vulperonex.Tests.Integration/Vulperonex.Tests.Integration.csproj`
- `tests/Vulperonex.Tests.Architecture/Vulperonex.Tests.Architecture.csproj`

**Estimated Size:** M (Medium)

---

## Task 1d: Connect Project References and Architectural Baseline

**Description:** Add permitted project references and the first architectural tests enforcing the dependency graph.

**Acceptance Criteria:**
- [ ] The Domain layer does not reference any Vulperonex projects.
- [ ] The Application layer references only the Domain layer.
- [ ] The Infrastructure layer references the Application and Domain layers.
- [ ] Adapters and Hosts follow the dependency graph in `tasks/plan.md`.
- [ ] The architectural tests fail if the Domain layer references the Infrastructure layer or platform adapters.

**Verification:**
- [ ] `dotnet list src/Vulperonex.Domain reference` returns no project references.
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false` passes.

**Dependencies:** Task 1b, 1c

**Files Likely Involved:**
- Production project `.csproj` files
- Test project `.csproj` files
- `tests/Vulperonex.Tests.Architecture/Dependencies/LayerDependencyTests.cs`

**Estimated Size:** M (Medium)

---

## Task 1e: Verify Baseline Compilation and Commit

**Description:** Establish the first green-light compilation baseline before adding domain behavior.

**Acceptance Criteria:**
- [ ] The entire solution compiles successfully.
- [ ] The full test suite command runs all current tests.
- [ ] Prior to committing, Git status contains only expected files.

**Verification:**
- [ ] `dotnet restore Vulperonex.sln --configfile NuGet.Config --ignore-failed-sources` (if repository-local NuGet configuration is required).
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `git status --short --ignored`

**Dependencies:** Tasks 1a-1d

**Files Likely Involved:** None (verification-driven fixes only)

**Estimated Size:** S (Small)

---

## Task 2a: Define Event Contracts and StreamUser

**Description:** Add core event abstractions and the `StreamUser` value object.

**Acceptance Criteria:**
- [ ] `IStreamEvent` exposes `EventId`, `EventTypeKey`, `OccurredAt`, `Platform`, and `StreamUser? User`.
- [ ] `StreamUser` contains `Platform`, `UserId`, and `DisplayName`.
- [ ] Event contract types are immutable.

**Verification:**
- [ ] Unit tests cover basic construction and immutability expectations.
- [ ] `dotnet test tests/Vulperonex.Tests.Unit /m:1 /nr:false /p:UseSharedCompilation=false`

**Dependencies:** Task 1e

**Files Likely Involved:**
- `src/Vulperonex.Domain/Events/IStreamEvent.cs`
- `src/Vulperonex.Domain/StreamUser.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/`

**Estimated Size:** S (Small)

---

## Task 2b: Implement MVP Domain Events and Keys

**Description:** Add seven MVP event records, `PlatformConnectionChangedEvent`, and canonical key constants.

**Acceptance Criteria:**
- [ ] All seven MVP events implement `IStreamEvent`.
- [ ] `PlatformConnectionChangedEvent` implements `IStreamEvent`.
- [ ] `StreamEventKeys` contains all canonical keys from `docs/SPEC.md`.
- [ ] `EventId` defaults to a ULID string.

**Verification:**
- [ ] Unit tests verify the `EventTypeKey` for each event.
- [ ] Unit tests verify that default `EventId` format conforms to ULID.

**Dependencies:** Task 2a

**Files Likely Involved:**
- `src/Vulperonex.Domain/Events/StreamEventKeys.cs`
- `src/Vulperonex.Domain/Events/UserSentMessageEvent.cs`
- `src/Vulperonex.Domain/Events/UserFollowedEvent.cs`
- `src/Vulperonex.Domain/Events/UserDonatedEvent.cs`
- `src/Vulperonex.Domain/Events/UserSubscribedEvent.cs`
- `src/Vulperonex.Domain/Events/UserGiftedSubscriptionEvent.cs`
- `src/Vulperonex.Domain/Events/ChannelRaidedEvent.cs`
- `src/Vulperonex.Domain/Events/RewardRedeemedEvent.cs`
- `src/Vulperonex.Domain/Events/PlatformConnectionChangedEvent.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/`

**Estimated Size:** M (Medium)

---

## Task 2c: Add Event Descriptions and Domain Coverage Gatekeeping

**Description:** Add event descriptions for later API/UI consumption and lock down domain coverage measurement.

**Acceptance Criteria:**
- [ ] `StreamEventDescriptions` exposes metadata for all workflow-visible MVP event keys.
- [ ] `platform.connection_changed` is marked or represented as system-only where needed.
- [ ] Domain coverage commands are documented and executable.

**Verification:**
- [ ] Unit tests verify that every canonical workflow key has a description.
- [ ] Following implementation, domain coverage commands pass the >90% threshold.

**Dependencies:** Task 2b

**Files Likely Involved:**
- `src/Vulperonex.Domain/Events/StreamEventDescriptions.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/StreamEventDescriptionsTests.cs`

**Estimated Size:** S (Small)

---

## Task 2d: Enforce No Platform Leakage in Domain/Application Layers

**Description:** Add architectural tests preventing Twitch/platform-specific symbols from leaking into the Domain or Application layers.

**Acceptance Criteria:**
- [ ] Architectural tests fail if the Domain or Application layers contain type names starting with `Twitch*`.
- [ ] Architectural tests fail if the Domain layer references adapter assemblies.
- [ ] Tests are named using the approved BDD style.

**Verification:**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false`

**Dependencies:** Task 2b

**Files Likely Involved:**
- `tests/Vulperonex.Tests.Architecture/Domain/PlatformLeakageTests.cs`

**Estimated Size:** S (Small)

---

## Task 3a: Implement Member Domain Model

**Description:** Add `MemberRecord`, `PlatformIdentity`, and `LoyaltyInfo` as small domain types with invariants.

**Acceptance Criteria:**
- [ ] `MemberRecord` contains `MemberId` and `Identities`.
- [ ] `PlatformIdentity` models `(Platform, PlatformUserId)`.
- [ ] Domain constructors or factories reject invalid empty identity values.

**Verification:**
- [ ] Unit tests cover valid construction.
- [ ] Unit tests cover rejection of invalid platform/user IDs.

**Dependencies:** Task 2d

**Files Likely Involved:**
- `src/Vulperonex.Domain/Members/MemberRecord.cs`
- `src/Vulperonex.Domain/Members/PlatformIdentity.cs`
- `src/Vulperonex.Domain/Members/LoyaltyInfo.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Members/`

**Estimated Size:** M (Medium)

---

## Task 3b: Define Application Member Ports

**Description:** Add Application layer ports for command and query paths without infrastructure implementations.

**Acceptance Criteria:**
- [ ] `IMemberRepository` exists in the Application layer and focuses on writes.
- [ ] `IMemberQueryService` exists in the Application layer and returns read DTOs or query result contracts, not EF entities.
- [ ] The Domain layer does not reference Application layer ports.

**Verification:**
- [ ] Architectural tests confirm that the Domain layer does not reference the Application layer.
- [ ] Compilation/build confirms that the Application layer can reference Domain layer member types.

**Dependencies:** Task 3a

**Files Likely Involved:**
- `src/Vulperonex.Application/Members/IMemberRepository.cs`
- `src/Vulperonex.Application/Members/IMemberQueryService.cs`
- `src/Vulperonex.Application/Members/MemberDtos.cs`

**Estimated Size:** S (Small)

---

## Task 3c: Complete Member Tests and Coverage

**Description:** Add tests for member behaviors and run Phase 1 coverage gatekeeping.

**Acceptance Criteria:**
- [ ] Member tests use Given/When/Then naming or body comments.
- [ ] Domain coverage remains >90%.
- [ ] Application coverage commands are documented and executable, even if application behavior remains thin.

**Verification:**
- [ ] `dotnet test tests/Vulperonex.Tests.Unit /p:CollectCoverage=true /p:Include="[Vulperonex.Domain]*" /p:Exclude="[*.Tests.*]*" /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=average`
- [ ] `dotnet test tests/Vulperonex.Tests.Unit /p:CollectCoverage=true /p:Include="[Vulperonex.Application]*" /p:Exclude="[*.Tests.*]*"`

**Dependencies:** Task 3b

**Files Likely Involved:**
- `tests/Vulperonex.Tests.Unit/Domain/Members/`

**Estimated Size:** S (Small)

---

## Task 3d: Add DCI Role Isolation Guardrails

**Description:** Add architectural tests specified by `docs/SPEC.md` for `*Role` and `*Behavior` domain classes.

**Acceptance Criteria:**
- [ ] `DciRoleIsolationTests` scan for `*Role` and `*Behavior` types in `Vulperonex.Domain`.
- [ ] Matching types must not reference Infrastructure, EF Core, or `*.Infrastructure.*`.
- [ ] Tests pass when no role/behavior types exist yet, remaining meaningful for future additions.

**Verification:**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false`

**Dependencies:** Task 3b

**Files Likely Involved:**
- `tests/Vulperonex.Tests.Architecture/Domain/DciRoleIsolationTests.cs`

**Estimated Size:** S (Small)

---

## Phase 1 Checkpoint

**Acceptance Criteria:**
- [ ] Tasks 1a-3d are completed and committed in small slices.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] Domain coverage threshold commands pass.
- [ ] Architectural tests pass.
- [ ] `git status --short --ignored` displays only expected ignored local files.

**Review Threshold:**
- [ ] Review dependency directions, naming, task sizes, and test quality before beginning Phase 2.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| .NET 10 package availability or SDK mismatch | High | Verify `dotnet --info` before implementation; keep package additions explicit and ask when needed. |
| Architectural tests become fragile too early | Medium | Start with assembly/reference rules and symbol checks that strictly align with approved SPEC. |
| Task 1 scope becomes too large | Medium | Treat 1a-1e as independent commits; stop at baseline compilation before adding domain behavior. |
| Coverage gatekeeping fails on generated or trivial code | Medium | Keep domain code minimal and test behaviors directly; exclude only test assemblies. |

---

## Open Questions

- No open questions for Phase 1 planning. Package installation remains constrained by prior inquiry rules in `docs/SPEC.md`; Phase 1 only authorizes SPEC-named packages required for its active tasks.
