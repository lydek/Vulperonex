# Vulperonex MVP — 任務清單

> 詳細說明見 tasks/plan.md
> 更新日期：2026-05-13

---

## 全域執行規則

- [ ] 每個行為先定義 BDD Given / When / Then scenario
- [ ] 每個 scenario 以 TDD 流程落地：RED → GREEN → REFACTOR
- [ ] 每個自動化測試命名符合 `Given_*_When_*_Then_*`（C#）或 `should * when *`（Vitest），或測試體含 `// Given / When / Then` 區塊（每個 Checkpoint code review 驗證）
- [ ] Domain 實作遵守 tactical DDD：Entity / Value Object / Domain Event / invariant 放在正確邊界
- [ ] Application 邊界遵守 light CQRS：command/write ports 與 query/read DTO services 分離
- [ ] DCI Role/Behavior（SPEC §4.1b）：Role 物件純 Domain 邏輯，不依賴 Infrastructure；Context/Interaction 在 Application；MVP 不做 runtime role/reflection/mixin；架構測試驗證 Role 無 Infrastructure 引用
- [ ] 手動驗證只補足 Photino / OBS / browser runtime，不取代自動化測試

---

## Phase 1：Solution 骨架 + Domain Foundation

> 詳細切片清單：`docs/phases/phase-1-foundation/todo.md`

- [x] **Task 1** — 建立 Solution 結構與所有 csproj 骨架（含 `Vulperonex.Adapters.Abstractions`、`Vulperonex.Adapters.Twitch`、`Vulperonex.Adapters.Simulation`）
- [x] **Task 2** — Domain：IStreamEvent、7 個 MVP 事件 record + `PlatformConnectionChangedEvent`、StreamUser、StreamEventKeys（含 `platform.connection_changed` 常數）
- [x] **Task 3** — Domain：MemberRecord、PlatformIdentity、LoyaltyInfo（Entity/VO/invariant）；**Application ports**：IMemberRepository（write）、IMemberQueryService（read）— ports 不在 Domain 層

### ✅ Checkpoint 1
- [x] `dotnet build` 全綠
- [x] Architecture tests：Domain 無 Infrastructure / Platform 引用
- [x] Domain 單元測試覆蓋率 > 90%

---

## Phase 2：事件匯流排 + Infrastructure

> 詳細切片清單：`docs/phases/phase-2-infrastructure/todo.md`

- [x] **Task 4** — IStreamEventBus + InMemoryStreamEventBus（Channel、handler 隔離、WaitForIdleAsync）
- [x] **Task 5** — EF Core + SQLite + 第一批 DB migration（含 MigrationClassifier 架構測試）
- [x] **Task 6** — TDQ 溢出處理 + ActionExecutionLog dedup（at-least-once 保證）
- [x] **Task 7** — MemberResolver（INSERT OR IGNORE 原子 GetOrCreate，**實作 Infrastructure-only**；Application 只引用 `IMemberResolver` port）+ PlatformUserDisplayCache（L1/L2，Infrastructure-only，Application/Domain 不引用）
- [x] **Task 8** — ISystemSettingsService（SQLite-backed、熱重載 IObservable、AES-256-GCM OAuth token + IOAuthTokenStore + SystemSettingKey 常數）

### ✅ Checkpoint 2
- [x] `dotnet test` 全綠
- [x] 事件 publish → bus → handler 端到端
- [x] MemberResolver 並行測試通過
- [x] Task 5：`PRAGMA auto_vacuum` = 2（FULL）bootstrap assertion 通過
- [x] Task 7：`IPlatformUserInfoCache.UpdateAsync` cache miss → default row（`Badges = Array.Empty<string>()`）通過
- [x] Task 8：AES-256-GCM AAD cross-key copy → `CredentialDecryptionException` 通過

---

## Phase 3：Simulation Adapter + WorkflowEngine

> 詳細切片清單：`docs/phases/phase-3-workflow/todo.md`

- [x] **Task 9** — SimulationAdapter + IStreamEventTypeRegistry（SC-3, SC-4）
- [x] **Task 10** — WorkflowEngine：條件評估、Serial/Parallel Actions、ErrorBehavior/Timeout（SC-2, SC-9）
- [x] **Task 11** — Plugin System：IVulperonexPlugin、InvokePluginAction executor（SC-10）

### ✅ Checkpoint 3
- [x] SC-2, SC-3, SC-4, SC-9, SC-10 通過
- [x] SimulationAdapter → Bus → WorkflowEngine → IPlatformChatSender 端到端

---

## Phase 4：Twitch Adapter + MemberModule

> 詳細切片清單：`docs/phases/phase-4-twitch-member/todo.md`

- [x] **Task 12** — TwitchAdapter：IRC + EventSub + DisplayHints + 指數退避重連（SC-1, SC-6a WorkflowEngine half）
- [x] **Task 13** — MemberModule + OverlayModule DTO 安全過濾（SC-8）

### ✅ Checkpoint 4
- [x] SC-1, SC-6a (Task 12) + SC-6b (Task 13), SC-8 通過
- [x] Overlay DTO 欄位白名單正確（含 `schemaVersion`，chat/alert 含 platform-provided 優先的 public `eventId`/`timestamp`；member 為 snapshot 不含 event metadata）

---

## Phase 5：Web Host + SignalR + CLI

> 詳細切片清單：`docs/phases/phase-5-web-signalr-cli/todo.md`

- [x] **Task 14a** — Minimal API：WorkflowRule CRUD + EventTypes endpoint + i18n 錯誤碼 + 循環引用偵測 + Action schema validation（未知 type / 缺 param / 非法 config）+ CQRS 架構測試
- [x] **Task 14b** — Minimal API：Simulate / Config / Member 端點 + security.* / oauth.* protected namespace 封鎖
- [x] **Task 15** — SignalR Hub + Overlay Push + 雙埠 Kestrel + 埠配對遞增（SC-5）
- [x] **Task 16** — CLI：simulate / config / member / rule 指令（透過 HTTP API，伺服器永遠 loopback-only 無需身分驗證）

### ✅ Checkpoint 5
- [x] SC-2, SC-5, SC-8, SC-9 通過
- [x] WorkflowRule CRUD + 循環引用偵測通過
- [x] `security.*` config key 封鎖（GET + PUT）通過
- [x] `oauth.*` config key 封鎖（GET + PUT → 403 + `OAUTH_CREDENTIAL_NAMESPACE`）+ CLI passthrough 至 stderr 通過
- [x] Phase 5 error codes centralized + HTTP status mapping table covered
- [x] CLI rule / config / member / simulate 全命令 integration test 通過
- [x] CLI simulate chat fixture rule + mock sender 驗證通過
- [x] Task 13f Phase 4 SC-6a/SC-6b equivalence 強化已完成或明確 waive：新增 follow/sub/donate payload，驗證 cache/member state/TotalBitsGiven/subscriber tier 等副作用細節
- [x] CLI simulate → Overlay SignalR 端到端手動測試，結果記錄於 `docs/phases/phase-5-web-signalr-cli/manual-verification.md`
- [x] Phase 5 CLI E2E 收尾：新 SQLite DB 第一次啟動 Web API 後自動 migrate，真實 CLI 可對 loopback API 執行 rule/config/member/simulate smoke，不再需要手動 `dotnet ef database update`（自動化已通過；published CLI 對獨立 Web API process 的人工 terminal smoke 已執行）
- [x] Phase 5 Twitch OAuth CLI 收尾：CLI 提供可手動執行的 Twitch PKCE 授權入口，callback loopback-only，refresh token 經 API/`IOAuthTokenStore` 加密保存，且不經 `/api/config/oauth.*`（自動化已通過；真 Twitch 瀏覽器授權已人工執行）
- [x] Phase 5 CLI REPL 補充：命令樹、`help`、Twitch auth status API、最小 REPL、no-Twitch mode banner、REPL 內 `twitch auth start` 缺 ClientId 保護、Ctrl+C cancellation、TTY line editor、Tab 多候選輪替已完成；人工 terminal 驗證已完成
- [x] 開發者快捷入口：新增 `tools/cli.ps1`，自動偵測 loopback Web host port 並直接進 REPL / 執行 one-shot CLI，降低人工驗證摩擦
- [x] Task 15：兩埠均以 loopback（IPv4 127.0.0.1 + IPv6 ::1）雙重綁定，socket bind test 驗證通過
- [x] Task 14b：`GET/PUT /api/config/oauth.twitch.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE` 通過
- [x] Task 14b：`GET /api/config/oauth.unknown.refresh_token`（未知 key）→ 403 + `OAUTH_CREDENTIAL_NAMESPACE`（prefix denylist 先於 registry lookup）通過
- [x] Task 16：`ASPNETCORE_ENVIRONMENT=Development` + `appsettings.Development.json` + environment variables 不覆蓋 `Database:Path` 通過

---

## Phase 5.5：Rapid-Test Enablement

> 詳細切片清單：`docs/phases/phase-5_5-rapid-test/todo.md`

- [x] **Task 17a** — CLI rule create / update
- [x] **Task 17b** — chat.html overlay 移植與架構測試
- [x] **Task 17c** — E2E fixture chat → workflow → overlay 整合測試 (ChatReplyChainTests)
- [x] **Task 17d** — Cookbook 文件撰寫與 PASS 記錄
- [x] **Task 17e** — CLI ID 解析 + 缺 arg UX + 破壞性操作確認

### ✅ Checkpoint 5.5
- [x] 0 警告編譯與測試全綠
- [x] CLI `rule create/update` 與 4xx 透傳綠
- [x] CLI 缺 arg / prefix / `--name` / `--yes` 互動流程全綠
- [x] `chat.html` payload key 架構測試綠
- [x] Cookbook AI Agent + 人物理驗證 PASS

---

## Phase 6：日誌 + 前端 + Photino

> 詳細切片清單：`docs/phases/phase-6-web-ui/todo.md`
> [!IMPORTANT]
> **前置條件 Gate**：父計畫中 Phase 5 Checkpoint 的三項手動驗收（包含 CLI E2E 收尾、Twitch OAuth 真實瀏覽器授權、以及 REPL 手動驗收）必須確認已勾選完成，此 Phase 6 方可開工實作。
> **目前優先順序變更**：Phase 6 尚未完成的 Photino/manual verification 等非 workflow parity 項目延後；目前先執行 Phase 7 Workflow Parity。

- ~~**Task 17**~~ — 已移除（原 MockYouTube Adapter，推遲出 MVP scope）
- [x] **Task 19** — Vue 前端骨架：Vite 7.3 + PrimeVue 4 Unstyled + UnoCSS + Pinia + useStreamEvents + 雙語系及 manifest 骨架
- [x] **Task 20** — Web 管理主控台 (Web Admin UI)：四大面板整合、唯讀成員、JSON Textarea Rule CRUD (過濾系統事件)、Twitch OAuth 起始與 `zh-TW` / `en-US` 雙語系
- [x] **Task 18** — Serilog 三 Sink + AppLogs 清理 worker（`log.db_retention_days` + `log.db_max_size_mb` size-based cleanup）+ 熱重載 log level
- [x] **Task 21** — Photino Desktop Shell + 埠衝突處理 + 靜態 fallback

### ✅ Checkpoint 6（最終）
- [x] `dotnet test` → 所有 active SC 通過（SC-1~SC-6, SC-8~SC-10；SC-7 removed）
- [x] `pnpm test` → 前端測試全通過
- [x] `pnpm lint` → 前端 lint 無錯誤（**oxlint 已安裝則直接執行；若尚未安裝，需先 ask-first 再 npm install**）
- [x] `pnpm build` → wwwroot 建置成功
- [x] 全部 Task 18-21 sub-task `[x]` 完成自檢（確認 `tasks/todo.md` 中 Task 18-21 的所有子待辦項目皆已勾選為 `[x]`）
- [x] 手動驗證：依據 `docs/phases/phase-6-web-ui/manual-verification.md` 之 § Task 20 Browser Manual Checklist、§ Task 20k - Twitch OAuth E2E Checklist、§ Task 21 Desktop Shell Checklist 全項目手動驗證通過，且所有 Dated Entry 的 Result 均為 PASS，無 pending 項目
- [x] 安全 review：Overlay DTO 精確白名單（含 SignalR JSON 序列化驗証）、兩埠以 `IPAddress.Loopback` + `IPAddress.IPv6Loopback` 雙綁定（socket bind test 驗証）、AES-256-GCM token 加密（含 tamper test + AAD binding）、machine.key 檔案權限（Windows ACL / Unix 0600）、`GET/PUT /api/config/oauth.twitch.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE`、**未知 `oauth.*` key（如 `oauth.unknown.refresh_token`）→ 403 + `OAUTH_CREDENTIAL_NAMESPACE`（prefix denylist 先於 registry，不回 400）**、**refresh token envelope 使用標準 Base64（非 Base64Url），解碼用 `Convert.FromBase64String`**、`config set security.*`/`config set oauth.*` → 403 protected namespace write denial、**OAuth `state` 參數 CSRF 驗證：state 不符 → 拒絕且不 exchange code（integration test 驗證）**、**OAuth callback listener：loopback-only（127.0.0.1 / ::1）+ 只接受預設 callback path + 接收後立即關舉（single-use）**

---

## Phase 7：Workflow Parity with Omni-Commander

> 詳細計畫：`docs/phases/phase-7-workflow-parity/plan.md`
> 詳細待辦：`docs/phases/phase-7-workflow-parity/todo.md`
> 對照來源：`ref/Omni-Commander/OmniCommander.Domain/Workflows/` + `ref/Omni-Commander/OmniCommander.Application/Workflows/` + `ref/Omni-Commander/OmniCommander.Application/Workflows/Executors/` + `ref/Omni-Commander/OmniCommander.Tests/Workflows/` + `ref/Omni-Commander/walkthrough.md`
> **前置條件 Gate**：Phase 5 runtime + Phase 6 已完成的 Web UI/rule JSON editor/overlay history 基線可用；不等待完整 Phase 6 Checkpoint。

- [x] **Task 23** — Variable / Expression substrate：`ExpressionContext` + template resolver + NCalc evaluator（ask-first 加 `NCalcSync`）
- [x] **Task 24** — Step `ExecutionCondition` + `OutputVariable`
- [x] **Task 25** — Rule-level throttle + timeout
- [x] **Task 26** — `OnFailureActionsJson` 補救鏈 + replay phase key
- [x] **Task 28** — Hot reload immutable rule snapshot cache
- [x] **Task 29** — Trigger filter + `MatchCondition`
- [x] **Task 27** — Sub-workflow flag + Args plumbing，保留 stable `InvocationId`
- [x] **Task 30** — Executor expansion（30a-30l；overlay/effect executor 必須 strong-typed DTO + whitelist）
- [x] **Task 32** — ChatOutboxService rate limit + observable skipped/failed state
- [x] **Task 31** — WorkflowTimer scheduler（單實例重啟 idempotency；多實例 leader election out-of-scope）
- [x] **Task 33** — Web UI builder upgrade for Phase 7 schema
- [x] **Task 34** — Plugin Action Args surface（backward compatible）
- [x] **Task 35** — Manual verification + Omni parity sign-off

### Checkpoint 7
- [x] Task 23-35 sub-task 全部 `[x]`
- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [x] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [x] Browser manual：5 個典型 rule 配置（trigger filter / cooldown / counter / sub-workflow / timer）全綠
- [x] DTO whitelist / SignalR JSON contract：Phase 7 新 rule schema 與 overlay/effect payload 無 raw JSON 漏網
- [x] `docs/phases/phase-7-workflow-parity/manual-verification.md` 記錄 PASS/FAIL + OC 對照矩陣

## Phase 7A：Workflow Editor UX Alignment with Omni-Commander

> 詳細計畫：`docs/phases/phase-7a-workflow-editor-ux/plan.md`
> 詳細待辦：`docs/phases/phase-7a-workflow-editor-ux/todo.md`
> 目的：補齊目前 Phase 7 workflow editor 的 UX 缺口，讓 editor 不再以 JSON textarea 當主要操作路徑。

- [x] **Task 36** — Workflow editor baseline repair：trigger filter「新增」互動修復、現有 editor regression tests
- [x] **Task 37** — Visual builder：Conditions / Actions / OnFailure 主流程改為表單式 editor
- [x] **Task 38** — Variable picker：提供 Trigger / Args / Step outputs / Member / Failure 變數插入
- [x] **Task 39** — JSON fallback demotion：JSON 改為 advanced fallback / import-export surface
- [x] **Task 40** — Omni parity verification：editor UX checklist + Omni 對照矩陣

### Checkpoint：Phase 7A

- [x] 全部 Task 36-40 sub-task `[x]` 完成自檢
- [x] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [x] Browser manual：workflow editor 主流程不需直接手寫 JSON 即可建立常見規則
- [x] Browser manual：variable picker 插入與 reload round-trip 全部 PASS
- [x] `docs/phases/phase-7a-workflow-editor-ux/manual-verification.md` 記錄 PASS/FAIL + Omni UX 對照矩陣

---

## Success Criteria 對照

| SC | Task |
|----|------|
| SC-1：7 個 MVP 事件 | Task 12 |
| SC-2：WorkflowEngine 執行 rule | Task 10 |
| SC-3：SimulationAdapter 無 Twitch 引用 | Task 9 |
| SC-4：Domain 無 Twitch symbols | Task 2 (持續) |
| SC-5：Overlay SignalR 5s 內收到 | Task 15 |
| SC-6a：Simulation ≡ Twitch 副作用（WorkflowEngine half） | Task 12 |
| SC-6b：Simulation ≡ Twitch 副作用（MemberRecord DB state half） | Task 13 |
| SC-7：已移除（MockYouTube Adapter，推遲出 MVP scope）| — |
| SC-8：ULID MemberRecord 建立 | Task 13 |
| SC-9：SendChatMessage platform 路由 | Task 10 |
| SC-10：Plugin 發布事件觸發 rule | Task 11 |

## Phase 7B：Chat Output Observability and Overlay Template Presets

> 詳細計畫：`docs/phases/phase-7b-chat-overlay-presets/plan.md`
> 詳細待辦：`docs/phases/phase-7b-chat-overlay-presets/todo.md`

- [x] **Task 41** — Simulation chat output observable surface：workflow `SendChatMessage` 在 simulation/local 模式下可直接看到 message / platform / channel / dedupKey / status
- [x] **Task 42** — Chat overlay preset system：`/overlay/chat` 支援至少兩個可切換內建樣板，且切換不需改前端原始程式碼
- [x] **Task 43** — OneComme compatibility path：以 extension/plugin 方式提供 OneComme 相容 / 匯入 / 映射策略，不直接併入 core

### Checkpoint：Phase 7B

- [x] 全部 Task 41-43 sub-task `[x]` 完成自檢
- [x] workflow `SendChatMessage` 在 `Simulation` 模式下可直接觀察結果，不再需要猜測是否送出
- [x] `/overlay/chat` 至少可切換兩個樣板，且 core preset contract 可承接外掛 / 可安裝樣板
- [x] `docs/.../manual-verification.md` 記錄 observability + preset + extension compatibility PASS/FAIL

## Phase 7C：Member Card Overlay、Custom HTML Extension、Member-in-Chat

> 詳細計畫：`docs/phases/phase-7c-member-overlay-extension/plan.md`
> 詳細待辦：`docs/phases/phase-7c-member-overlay-extension/todo.md`
> SPEC 對應：`docs/SPEC.md` §4.14.1 Overlay Preset Contract
> 補建立背景：Task 44/45 大部分先實作後補 spec / plan / task；Task 46-49 為新 scope，待 ACK 後開工。

- [/] **Task 44** — Member Card Overlay Default Preset (Rotan-Checkin)：Vue preset + CSS base+theme tokens + standalone HTML 一致視覺
- [/] **Task 45** — Member Card Admin Controller：背景/印章 URL 設定面板 + i18n + URL sanitize + setInterval lifecycle
- [ ] **Task 46** — Custom HTML Overlay Upload Infrastructure：multipart upload + zip path traversal 防護 + admin UI
- [ ] **Task 47** — Overlay Preset Resolver Backend Route：`overlay.{hub}.preset` 設定支援 `custom:{slug}` + 302 redirect
- [ ] **Task 48** — Member Snapshot in Chat Hub：cross-hub DTO + 反射白名單 + chip preset + 旗標控制
- [ ] **Task 49** — OneComme Bridge Plugin Contract (Scaffold Only)：interface + project scaffold，importer 實作延後 Phase 7D

> 圖例：`[x]` 完成、`[/]` 部分完成（Task 44/45 約 85%；剩餘子項見子 todo.md）、`[ ]` 未開工

### Checkpoint：Phase 7C

- [ ] 全部 Task 44-49 sub-task 達成驗收標準
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual：HTML 上傳 / member card / chip embed 三項手動驗證 PASS
- [ ] Security review：path traversal、CSS injection、member snapshot 白名單、loopback-only、size cap 全 PASS
- [ ] `docs/phases/phase-7c-member-overlay-extension/manual-verification.md` 紀錄 dated entries + evidence commits

## Architecture / Security / Testing Gates

| Gate | Task |
|------|------|
| Clean Architecture 層依賴不違規 | Task 3（持續，架構測試）|
| Domain > 90% 覆蓋率 | Task 2 起持續（coverlet.msbuild `/p:Include=[Vulperonex.Domain]* /p:Exclude=[*.Tests.*]* /p:Threshold=90`，CI fail on drop）|
| Application > 80% 覆蓋率 | Task 10/11/14 持續（coverlet.msbuild `/p:Threshold=80 /p:Include=[Vulperonex.Application]* /p:Exclude=[*.Tests.*]*`；Unit tests only；Application behavior in integration → 補 unit tests with fakes）|
| CQRS：GET 路徑不呼叫 IWorkflowRuleRepository | Task 14a（**interaction/integration test with fakes**，非靜態 assembly 掃描）|
| WorkflowRule 循環引用偵測 | Task 14a |
| i18n：後端零 human-readable 字串 | Task 14a |
| security.* config key 封鎖（GET + PUT）| Task 14b |
| 兩埠以 `IPAddress.Loopback` + `IPAddress.IPv6Loopback` 雙綁定（永遠 loopback-only，socket bind test 驗証）| Task 15 |
| Overlay DTO 不洩漏 MemberId/内部欄位，且 public metadata 欄位固定 | Task 13 |
| AES-256-GCM refresh_token 加密（versioned envelope `v1:<Base64>`、per-token nonce randomness、tamper → CredentialDecryptionException）+ machine.key 生命週期 | Task 8 |
| machine.key 檔案權限（Windows ACL user-only / Unix 0600）；chmod/ACL 失敗 → fail-fast `IOException` | Task 8 |
| AES-256-GCM AAD = setting key name UTF-8；cross-key copy → CredentialDecryptionException | Task 8 |
| oauth.* config namespace 封鎖（GET + PUT → 403 + `OAUTH_CREDENTIAL_NAMESPACE`） | Task 14b |
| BDD 命名規範：`Given_*_When_*_Then_*`（C#）/ `should * when *`（Vitest）每 Checkpoint code review 驗證 | Task 2 起持續 |
| DCI Role/Behavior isolation：`*Role`/`*Behavior` 類型（Domain 命名空間）不得引用 `*.Infrastructure.*` 或 EF Core（架構測試 `DciRoleIsolationTests`）| Task 3 |
| DCI Context/Interaction 位置：`*Context`/`*UseCase` 不定義於 Domain；PR code review gate（非 CI 自動測試）| 持續（PR review）|
| Plugin/Action context 不暴露 `IServiceProvider`（service locator 反模式）；review 確認 plugin host 只注入明確 interface | Task 11（PR code review gate）|
| TDQ replay 不造成重複副作用（ActionExecutionLog dedup）+ 不造成重複 MemberRecord（INSERT OR IGNORE）+ cache 更新用狀態替換不累加（注意：rule 更新後舊 TDQ replay 的 action index drift 屬 MVP 已知限制，不在此 gate 範圍內）| Task 6/7/12 |
