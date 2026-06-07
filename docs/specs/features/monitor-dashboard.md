# Feature Spec: Monitor Dashboard

> [← Back to Master Specification](../../SPEC.md)

### 4.18 Unified Monitor Page (Phase 7D)

**Background:** Currently, simulating events, viewing the chat overlay, and viewing member cards reside on different tabs/routes. The streamer's debug workflow requires: admin simulate → switch to `/overlay/chat` to see results → switch to `/overlay/member` to see member card → back to simulate to try again. The context switching creates high driver friction.

**Phase 7D Design:** Introduces a unified `/monitor` page; the old standalone simulate and overlay pages are retained for debugging and E2E testing.

**Layout (Widescreen ≥1280px):**

```
┌─────────────────────────────────────────────────────────────┐
│ Header: Platform Connection + SignalR Status + Live/Settings│
├──────────────┬──────────────────────────────┬───────────────┤
│              │                              │               │
│  Simulate    │  Overlay Preview (iframe)    │  Chat Stream  │
│  Controls    │                              │  (live)       │
│  (sider)     │  ┌────────────────────────┐  │ User: message │
│  • chat      │  │ Preview: chat / member │  │ ...           │
│  • follow    │  │                        │  │               │
│  • sub       │  │  iframe                │  │               │
│  • giftsub   │  │                        │  │               │
│  • raid      │  └────────────────────────┘  │               │
│  • bits      │  BG: transparent/green/      │               │
│  • redeem    │      pink/color/image        │               │
│  • checkin   │  Preset Switch Dropdown      │               │
│  • batch     │  Reload Button               │               │
│              │                              │               │
└──────────────┴──────────────────────────────┴───────────────┘
```

**Layout (Narrow Screen <1280px):**
- Simulate controls transform into a right-side drawer, with a toggle button added to the header.
- Overlay preview and chat stream stack vertically.

**Features:**
1. **Simulate Controls Sider:** Contains all simulate event subcommand UIs (chat / follow / sub / giftsub / raid / bits / redeem / checkin), mapping to existing `/api/simulate/*` endpoints. Adds a "Batch Check-In" tool to fire N check-in events with a single click.
2. **Overlay Preview Iframe:**
   - Dynamic iframe `src` switching between chat / member / alerts, embedding a `?preset={key}&t={ts}` query.
   - Preview background switcher (transparent / green screen / pink screen / solid colors / custom background URL) to inspect overlay appearance against different backdrops.
   - Reload button (bumps query timestamp).
3. **Chat Stream Panel:** Subscribes to `/hubs/overlay/chat`, listing the latest N messages (plain text, including member chip previews), without rendering preset CSS (plain table style), allowing streamers to inspect the "data layer" (decoupled from overlay visuals).
4. **Header Status:** Platform connection status (Twitch ✅/❌), SignalR connection status, and current preset settings summary.

**Route Retention:**
- Standalone `/simulate` page is not deleted (used for CLI E2E and automated testing).
- Standing `/overlay/chat` and `/overlay/member` routes are retained as redirect aliases to the static HTML pages; `/overlay/alerts` remains the live Vue route.
- `/monitor` becomes the **new default landing** (replacing `/` default); the existing admin entry remains in the sidebar.

**Real-Time Reaction to Events:** When SignalR is connected, simulated actions trigger immediate, real-time updates in both the preview iframe (notified reversely via the hub) and the chat stream, removing the need for manual reloads.

**i18n:** Full support for zh-TW + en-US.
**a11y:** Sider toggle has `aria-label`, drawer focus trap inherits the ConfirmDialog pattern.

---


---

### 4.20 Module & Plugin Management (Phase 7D)

**Background & Motivation:**
Core services (check-in, counters, lottery points, AV effects, external OneComme Bridge, etc.) execute as Hosted Services or plugins, but lacked a centralized management page to toggle their ON/OFF states. Operating dependent modules blindly when underlying modules are disabled leads to state drift.

**Design & Specifications:**
1. **Module/Plugin Switch State Storage:**
   - Toggle states are stored in the database/system settings via `ISystemSettingsService` using key names `modules.enabled.{moduleName}`.
   - All core Hosted Services dynamically detect this config in `ExecuteAsync` or `StartAsync`, skipping registration, interception, or action execution (entering a No-Op state) if `false`. Already running Hosted Services transition their states immediately upon configuration changes.
   - For `IWorkflowActionExecutor` (e.g. `TriggerCheckInActionExecutor`), actions must be rejected with a `WorkflowExecutionException` if the associated module (e.g. CheckInModule) is disabled.

2. **Dependency Resolution:**
   - **Module registry (`ModuleStateService.Definitions`) — name / display / category / dependencies:**
     - `workflow` "Workflow Engine" (core) -> no dependencies
     - `member` "Member Module" (core) -> no dependencies
     - `checkin` "Check-In Module" (core) -> depends on `workflow` + `member`
     - `lottery` "Lottery Module" (core) -> depends on `workflow` + `member`
     - `onecommebridge` "OneComme Bridge" (plugin) -> no dependencies, requires Core Event Bus
     - (There is no separate toggleable `OverlayModule`; overlay push is `OverlayEventForwarder`, always on. Enabled state is stored as `modules.enabled.{name}` via `ISystemSettingsService`.)
   - **Cascading Disable:**
     - Disabling a module depended on by others (e.g., disabling `MemberModule`) **must** trigger cascading dependency disabling.
     - **UI Cascade Warning Gate:** The frontend displays a warning: "Disabling 'Member Core Module' will also turn off the following dependent modules: Check-In Module, Lottery Module. Confirm disable?".
     - Upon user confirmation, the API writes `false` to all dependent modules and appends an audit log with `ActorKind = 'user'` and `Operation = 'disable_module'`.
   - **Cascading Enable:**
     - When enabling a module with dependencies (e.g. enabling `CheckInModule`), if its dependee (e.g. `MemberModule`) is disabled, the system should **automatically enable the dependee** or **display a warning and block startup**.

3. **Module Management Endpoints (API):**
   - `GET /api/plugins-modules`: Lists all module/plugin names, Chinese display names, descriptions, current running status (`IsActive`), and dependencies.
   - `POST /api/plugins-modules/{name}/toggle`: Parameter: `enabled: bool`. Computes dependencies, and returns the complete list of module states after topological changes.

4. **UI Management Page:**
   - Adds a "Functional Modules & Plugins" tab under `/admin/settings` displaying ON/OFF toggles in a card-based grid, categorized with tags (Core Services, Interactions, Audiovisual, External Plugins) and dependency icons.

---

### 4.21 Event & Loyalty Simulation (Phase 7D)

**Background & Motivation:**
Existing event simulation is limited to basic actions like `chat`, `follow`, and `sub`. Crucial support for "StreamRole flags / custom roles" and "loyalty points / check-ins" was missing from the UI and API, leaving streamers unable to verify and debug complex workflows directly inside the Admin panel (e.g., check-in rewards restricted to Moderators, special effects triggered only for VIPs).

**Design & Specifications:**
1. **StreamRole flags Simulation:**
   - Expands the `SimulateRequest` DTO accepted by `/api/simulate/*`: its `Roles` property supports both single string/number and string arrays (e.g., `["subscriber", "moderator", "vip"]`), allowing multiple `StreamRole` flags to be packed into simulated events' User payloads.
   - The simulator UI provides a Checkbox Group allowing streamers to toggle and stack roles arbitrarily.

2. **Check-In & Loyalty Simulation Endpoint:**
   - Adds `POST /api/simulate/checkin` endpoint.
   - Parameters:
     - `platformUserId`: string (ID of the member to simulate check-in, defaults to random)
     - `displayName`: string (Display name of the member to simulate, defaults to random)
     - `skipCooldown`: bool (Whether to bypass check-in cooldown, **defaults to false**; CLI passes `--skip-cooldown` to enable)
     - `stampCount`: int (Number of stamps/check-ins to accumulate, defaults to 1)
   - **Behavior:** The endpoint directly invokes `IMemberResolver` and `IMemberStreamStateRepository` to increment check-in counts. Upon successful persistence in SQLite, it publishes a `MemberCheckedInEvent` to the Event Bus, triggering immediate stamp board visuals on the OBS Overlay and Preview Hub.

---

### 4.22 Intuitive Workflow Rule Editor (Phase 7D)

**Background & Motivation:**
The existing workflow rule configuration interface required users to write specific JSON or raw text expressions, which was highly unintuitive for non-technical streamers. This phase introduces a visual guided editor, completely discarding inefficient free-text configurations.

**Design & Specifications:**
1. **Condition Builder:**
   - Discards raw NCalc text in favor of a row-based visual rule list.
   - Each condition consists of three dropdown menus: `[Variable Selector]` -> `[Comparison Operator]` -> `[Target Value/Constant]`.
   - The Variable Selector dynamically reads registered keys from `StreamEventTypeRegistry` and context variables pre-provided by the Workflow (such as `user.name`, `message.text`, `member.stamps`), avoiding typos via clickable dropdown selections.
   - The frontend component automatically compiles the visual configuration and outputs a standard NCalc expression (e.g., `member.stamps >= 10`) to the backend API.

2. **Dynamic Action Form:**
   - Dynamically generates strong-typed input controls for each Action type (e.g., `TriggerCheckIn`, `RefundTwitchRedemption`, `TriggerEffect`, etc.) based on `ActionParameterMetadata` (types including string, number, boolean, select, text) registered by the backend.
   - Integrates a floating variable selector panel. When focusing on input fields or typing `{`, a list of available variables pops up, allowing users to click and insert variable template strings (such as `{user.displayName}`), preventing typos.

3. **`ActionParameterMetadata.Advanced` flag (post-Phase-7D refinement):**
   - The backend `ActionParamAttribute` exposes an `advanced: bool` flag (default `false`); the API surfaces it as `ActionParameterMetadataDto.Advanced`.
   - Frontend `WorkflowActionsEditor.vue` partitions a definition's fields into **basic** (rendered inline by default) and **advanced** (wrapped in a `<details>` "Advanced options" disclosure, collapsed by default).
   - Intent: fields whose default value works in ≥99% of cases — but the executor still honors when set — stay out of the default form. Example: `TriggerCheckInAction.Platform` is marked advanced because the executor auto-derives platform from the trigger event when blank.
   - The advanced flag is metadata only: it does **not** change save-time validation or the JSON shape sent to the API. JSON-edit power-users still see and edit advanced fields directly.

4. **Filter-aware variable picker:** the `{x}` picker in each field narrows its variable suggestions by the field's `key`. `UserId`-typed keys show only `*.UserId` paths (`Trigger.UserId`, `Args.UserId`, `Member.UserId`). Platform-typed keys show only platform-bearing paths. Channel-typed keys show channel paths. This is a UI-only filter — no backend contract change.

---
