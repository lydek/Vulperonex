# Phase 1 Detailed Plan: Solution Skeleton + Domain Foundation

> Parent plan: `tasks/plan.md` Phase 1
> Scope: Tasks 1-3 only
> Goal: create a buildable .NET solution, then land the Domain/Application foundation with tests and architecture gates.

---

## Execution Rules

- Work on one small branch per slice, then merge back to `main` with `git merge --ff-only`.
- Commit every verified slice before starting the next slice.
- Do not add packages without ask-first approval, except packages already named by the approved task and needed for that task.
- Keep `.claude/` and other local-only files out of commits.
- Use BDD/TDD for behavior-bearing code. Skeleton-only project setup may use build/reference verification instead of behavior tests.

---

## Dependency Order

```
Task 1a repo/solution config
    -> Task 1b production projects
    -> Task 1c test projects
    -> Task 1d project references
    -> Task 1e baseline build
        -> Task 2a event contracts
        -> Task 2b concrete events
        -> Task 2c event descriptions/tests
        -> Task 2d architecture rule for platform leakage
            -> Task 3a member entities/value objects
            -> Task 3b Application member ports
            -> Task 3c member domain tests
            -> Task 3d DCI role isolation test
```

---

## Task 1a: Create Solution-Level Build Configuration

**Description:** Create the solution file and shared .NET configuration files so all projects inherit consistent language, nullable, analyzers, and package version behavior.

**Acceptance criteria:**
- [ ] `Vulperonex.sln` exists.
- [ ] Shared build settings exist for C# 14 / .NET 10 and nullable enabled.
- [ ] No production or test logic is introduced in this slice.

**Verification:**
- [ ] `dotnet --info` confirms SDK availability.
- [ ] `dotnet sln Vulperonex.sln list` succeeds.

**Dependencies:** None

**Files likely touched:**
- `Vulperonex.sln`
- `Directory.Build.props`
- `Directory.Packages.props` if central package management is used

**Estimated scope:** S

---

## Task 1b: Add Production Project Skeletons

**Description:** Add all production projects named by the approved architecture without business logic.

**Acceptance criteria:**
- [ ] Production `.csproj` files exist for Domain, Application, Infrastructure, Plugins.Abstractions, Adapters.Abstractions, Adapters.Twitch, Adapters.Simulation, Web, Cli, and Desktop.
- [ ] Desktop targets `net10.0-windows`.
- [ ] No project references violate the dependency graph.

**Verification:**
- [ ] `dotnet sln Vulperonex.sln list` shows every production project.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` reaches restore/build stage without project discovery errors.

**Dependencies:** Task 1a

**Files likely touched:**
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

**Estimated scope:** M

---

## Task 1c: Add Test Project Skeletons

**Description:** Add the three test projects and make them ready for unit, integration, and architecture tests.

**Acceptance criteria:**
- [ ] Unit, integration, and architecture test projects exist and are included in the solution.
- [ ] Test package choices follow `docs/SPEC.md` unless a package add requires ask-first approval.
- [ ] Each test project has a placeholder smoke test only if needed to prove runner setup.

**Verification:**
- [ ] `dotnet test tests/Vulperonex.Tests.Unit --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` discovers the test project.
- [ ] `dotnet test tests/Vulperonex.Tests.Integration --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` discovers the test project.
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` discovers the test project.

**Dependencies:** Task 1a

**Files likely touched:**
- `tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj`
- `tests/Vulperonex.Tests.Integration/Vulperonex.Tests.Integration.csproj`
- `tests/Vulperonex.Tests.Architecture/Vulperonex.Tests.Architecture.csproj`

**Estimated scope:** M

---

## Task 1d: Wire Project References and Architecture Baseline

**Description:** Add the allowed project references and the first architecture tests that enforce the dependency graph.

**Acceptance criteria:**
- [ ] Domain references no Vulperonex projects.
- [ ] Application references Domain only.
- [ ] Infrastructure references Application and Domain.
- [ ] Adapters and Hosts follow the dependency graph in `tasks/plan.md`.
- [ ] Architecture tests fail if Domain references Infrastructure or a platform adapter.

**Verification:**
- [ ] `dotnet list src/Vulperonex.Domain reference` returns no project references.
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes.

**Dependencies:** Tasks 1b, 1c

**Files likely touched:**
- production `.csproj` files
- test `.csproj` files
- `tests/Vulperonex.Tests.Architecture/Dependencies/LayerDependencyTests.cs`

**Estimated scope:** M

---

## Task 1e: Verify Baseline Build and Commit

**Description:** Establish the first green build baseline before adding domain behavior.

**Acceptance criteria:**
- [ ] Full solution build passes.
- [ ] Full test command runs all current tests.
- [ ] Git status contains only intended files before commit.

**Verification:**
- [ ] `dotnet restore Vulperonex.sln --configfile NuGet.Config --ignore-failed-sources` if repo-local NuGet config is needed.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `git status --short --ignored`

**Dependencies:** Tasks 1a-1d

**Files likely touched:** none beyond verification-driven fixes

**Estimated scope:** S

---

## Task 2a: Define Event Contract and StreamUser

**Description:** Add the core event abstractions and `StreamUser` value object.

**Acceptance criteria:**
- [ ] `IStreamEvent` exposes `EventId`, `EventTypeKey`, `OccurredAt`, `Platform`, and `StreamUser? User`.
- [ ] `StreamUser` contains `Platform`, `UserId`, and `DisplayName`.
- [ ] Event contract types are immutable.

**Verification:**
- [ ] Unit tests cover basic construction and immutability expectations.
- [ ] `dotnet test tests/Vulperonex.Tests.Unit --no-build /m:1 /nr:false /p:UseSharedCompilation=false`

**Dependencies:** Task 1e

**Files likely touched:**
- `src/Vulperonex.Domain/Events/IStreamEvent.cs`
- `src/Vulperonex.Domain/StreamUser.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/`

**Estimated scope:** S

---

## Task 2b: Implement MVP Domain Events and Keys

**Description:** Add the seven MVP event records plus `PlatformConnectionChangedEvent` and canonical key constants.

**Acceptance criteria:**
- [ ] All seven MVP events implement `IStreamEvent`.
- [ ] `PlatformConnectionChangedEvent` implements `IStreamEvent`.
- [ ] `StreamEventKeys` contains all canonical keys from `docs/SPEC.md`.
- [ ] `EventId` defaults to a ULID string.

**Verification:**
- [ ] Unit tests verify each event's `EventTypeKey`.
- [ ] Unit tests verify default `EventId` shape is ULID-compatible.

**Dependencies:** Task 2a

**Files likely touched:**
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

**Estimated scope:** M

---

## Task 2c: Add Event Descriptions and Domain Coverage Gate

**Description:** Add event descriptions used by API/UI surfaces later and lock Domain coverage measurement.

**Acceptance criteria:**
- [ ] `StreamEventDescriptions` exposes metadata for all workflow-visible MVP event keys.
- [ ] `platform.connection_changed` is marked or represented as system-only where needed.
- [ ] Domain coverage command is documented and runnable.

**Verification:**
- [ ] Unit tests verify every canonical workflow key has a description.
- [ ] Domain coverage command passes the >90% threshold once implementation exists.

**Dependencies:** Task 2b

**Files likely touched:**
- `src/Vulperonex.Domain/Events/StreamEventDescriptions.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/StreamEventDescriptionsTests.cs`

**Estimated scope:** S

---

## Task 2d: Enforce No Platform Leakage in Domain/Application

**Description:** Add architecture tests that prevent Twitch/platform-specific symbols from entering Domain or Application.

**Acceptance criteria:**
- [ ] Architecture test fails if Domain or Application contains `Twitch*` type names.
- [ ] Architecture test fails if Domain references adapter assemblies.
- [ ] Tests are named with the approved BDD style.

**Verification:**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture --no-build /m:1 /nr:false /p:UseSharedCompilation=false`

**Dependencies:** Task 2b

**Files likely touched:**
- `tests/Vulperonex.Tests.Architecture/Domain/PlatformLeakageTests.cs`

**Estimated scope:** S

---

## Task 3a: Implement Member Domain Model

**Description:** Add `MemberRecord`, `PlatformIdentity`, and `LoyaltyInfo` as small Domain types with invariants.

**Acceptance criteria:**
- [ ] `MemberRecord` contains `MemberId` and `Identities`.
- [ ] `PlatformIdentity` models `(Platform, PlatformUserId)`.
- [ ] Domain constructors or factories reject invalid empty identity values.

**Verification:**
- [ ] Unit tests cover valid construction.
- [ ] Unit tests cover invalid platform/user id rejection.

**Dependencies:** Task 2d

**Files likely touched:**
- `src/Vulperonex.Domain/Members/MemberRecord.cs`
- `src/Vulperonex.Domain/Members/PlatformIdentity.cs`
- `src/Vulperonex.Domain/Members/LoyaltyInfo.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Members/`

**Estimated scope:** M

---

## Task 3b: Define Application Member Ports

**Description:** Add Application-layer member ports for command and query paths without Infrastructure implementation.

**Acceptance criteria:**
- [ ] `IMemberRepository` exists in Application and is write-focused.
- [ ] `IMemberQueryService` exists in Application and returns read DTOs or query result contracts, not EF entities.
- [ ] Domain does not reference Application ports.

**Verification:**
- [ ] Architecture tests confirm Domain does not reference Application.
- [ ] Unit compile/build confirms Application can reference Domain member types.

**Dependencies:** Task 3a

**Files likely touched:**
- `src/Vulperonex.Application/Members/IMemberRepository.cs`
- `src/Vulperonex.Application/Members/IMemberQueryService.cs`
- `src/Vulperonex.Application/Members/MemberDtos.cs`

**Estimated scope:** S

---

## Task 3c: Complete Member Tests and Coverage

**Description:** Add focused tests for member behavior and run the Phase 1 coverage gate.

**Acceptance criteria:**
- [ ] Member tests use Given/When/Then naming or body comments.
- [ ] Domain coverage remains >90%.
- [ ] Application coverage command is documented even if Application behavior remains thin.

**Verification:**
- [ ] `dotnet test tests/Vulperonex.Tests.Unit /p:CollectCoverage=true /p:Include="[Vulperonex.Domain]*" /p:Exclude="[*.Tests.*]*" /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=average`

**Dependencies:** Task 3b

**Files likely touched:**
- `tests/Vulperonex.Tests.Unit/Domain/Members/`

**Estimated scope:** S

---

## Task 3d: Add DCI Role Isolation Gate

**Description:** Add the architecture test specified by `docs/SPEC.md` for `*Role` and `*Behavior` Domain classes.

**Acceptance criteria:**
- [ ] `DciRoleIsolationTests` scans `Vulperonex.Domain` for `*Role` and `*Behavior` types.
- [ ] Matching types must not reference Infrastructure, EF Core, or `*.Infrastructure.*`.
- [ ] The test passes when no Role/Behavior types exist yet and remains meaningful for future additions.

**Verification:**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture --no-build /m:1 /nr:false /p:UseSharedCompilation=false`

**Dependencies:** Task 3b

**Files likely touched:**
- `tests/Vulperonex.Tests.Architecture/Domain/DciRoleIsolationTests.cs`

**Estimated scope:** S

---

## Phase 1 Checkpoint

**Acceptance criteria:**
- [ ] Tasks 1a-3d are complete and committed in small slices.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] Domain coverage threshold command passes.
- [ ] Architecture tests pass.
- [ ] `git status --short --ignored` shows only intended ignored local files.

**Review gate:**
- [ ] Review for dependency direction, naming, task sizing, and test quality before starting Phase 2.

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| .NET 10 package availability or SDK mismatch | High | Verify `dotnet --info` before implementation; keep package adds explicit and ask-first when needed. |
| Architecture tests become brittle early | Medium | Start with assembly/reference rules and symbol checks that match the approved SPEC exactly. |
| Task 1 becomes too broad | Medium | Keep 1a-1e as separate commits; stop after baseline build before adding Domain behavior. |
| Coverage gate fails due to generated or trivial code | Medium | Keep Domain code small and test behavior directly; exclude test assemblies only. |

---

## Open Questions

- None for Phase 1 planning. Package installation remains governed by the ask-first rule in `docs/SPEC.md`.
