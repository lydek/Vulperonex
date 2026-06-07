# 實作計畫：Vulperonex MVP

> 基於 docs/SPEC.md v0.3
> 建立日期：2026-05-11 / 最後更新：2026-05-13

---

## 概覽

Vulperonex 是平台無關的串流自動化工具。MVP 範圍：Twitch 接收事件 → 事件匯流排 → WorkflowEngine 執行規則 → Overlay 推送 SignalR → Photino 桌面殼。CLI 可模擬事件、管理設定、管理規則（list/show/enable/disable/delete）、查詢會員。

---

## 架構決策摘要

| # | 決策 |
|---|------|
| A1 | Clean Architecture + tactical DDD：Domain → Application → Infrastructure / Adapters → Hosts；Domain 擁有實體、Value Object、Domain Event 與 invariant |
| A2 | 事件匯流排：in-memory `Channel<IStreamEvent>`，10,000 slots，溢位進 TDQ (SQLite) |
| A3 | 會員 ID：ULID，`PlatformIdentity (Platform, PlatformUserId)` 複合唯一鍵 |
| A4 | WorkflowRule：JSON 欄位存 Conditions / Actions，EF Core 10 JSON mapping |
| A5 | 雙埠永遠 loopback-only（`IPAddress.Loopback` + `IPAddress.IPv6Loopback`）：API Port 5000、Overlay Port 5001；兩埠均無需身分驗證，Kestrel bind address 即為安全邊界 |
| A6 | 測試方法：BDD scenario 定義行為，TDD red/green/refactor 實作；Domain >90%，Application >80% |
| A7 | Plugin：MVP 為靜態 DI 註冊，不掃描 DLL |

---

## 全域實作規則

- 每個行為需求先寫成 BDD-style Given / When / Then scenario。
- scenario 必須對應到自動化測試；實作流程使用 TDD：先看見 failing test，再寫最小實作，最後在綠燈下重構。
- Domain 規則以 tactical DDD 落地：invariant 放在 Domain/Application，Repository 為 Application port，EF/Core SDK payload 不外洩到 Domain。
- Application 邊界使用 light CQRS：commands 透過 write repository ports 變更狀態，queries 透過 query service ports 回傳 read DTO；MVP 不引入 command bus、event sourcing 或獨立 read DB。
- Photino、OBS、瀏覽器 runtime 可加手動驗證，但不能取代自動化 acceptance test。
- 每個自動化測試命名符合 `Given_<State>_When_<Action>_Then_<Expected>`（C#）或 `should <expected> when <condition>`（Vitest），或測試體頂部含 `// Given / When / Then` 區塊。每個 Checkpoint 的 code review 應驗證此規範。
- **DCI-inspired Role/Behavior 準則（SPEC §4.1b）**：當 Aggregate 或 Domain service 累積多個使用情境行為時，可用 Role/Behavior 物件拆分；Role/Behavior 必須是純 Domain 邏輯，不得相依 `DbContext`、EF Core 或任何 Infrastructure 型別；Context/Interaction 放在 Application use case（`*Context`/`*UseCase` 類型不得定義於 Domain）；MVP 不做 runtime dynamic role / reflection / mixin。架構測試（`DciRoleIsolationTests`）驗證 Role 物件無 Infrastructure 引用；Context/Interaction 位置規則以 PR code review gate 驗證（非 CI 自動測試）。

---

## 相依圖

```
Vulperonex.Domain
    └── Vulperonex.Application
            ├── Vulperonex.Infrastructure               (實作 Application ports)
            ├── Vulperonex.Plugins.Abstractions         (相依 Domain + Application)
            ├── Vulperonex.Adapters.Simulation          (相依 Domain + Application + Adapters.Abstractions)
            ├── Vulperonex.Adapters.Twitch              (相依 Domain + Application + Adapters.Abstractions)
            └── Hosts
                ├── Vulperonex.Web                      (相依全部)
                ├── Vulperonex.Cli                      (相依全部)
                └── Vulperonex.Desktop                  (包裝 Web)
Vulperonex.Adapters.Abstractions                        (IStreamEventSource、IPlatformUserInfoCache 等)
    ├── Vulperonex.Adapters.Simulation                  (同上，亦相依 Application)
    ├── Vulperonex.Adapters.Twitch                      (同上，亦相依 Application)
    └── (未來平台 Adapter 同樣相依 Domain + Adapters.Abstractions + Application；`IStreamEventBus` 定義於 Application，publish 事件需此相依)
frontend (Vue SPA)
    └── 由 Web/wwwroot 服務，與後端透過 SignalR + REST 溝通
```

---

## 任務清單

### Phase 1：Solution 骨架 + Domain Foundation

> 詳細切片計畫：`docs/phases/phase-1-foundation/plan.md`

#### Task 1：建立 Solution 結構與專案骨架

**描述：** 建立整個 .NET Solution 及所有 csproj，設定專案間引用關係，確認 `dotnet build` 全綠。不含任何業務邏輯。

**驗收標準：**
- [ ] `dotnet build Vulperonex.sln` 無錯誤
- [ ] 每個 csproj 的 `<ProjectReference>` 符合相依圖（Domain 不引用任何其他 Vulperonex 專案）
- [ ] Architecture test 專案存在（NetArchTest 設定完成）

**驗證步驟：**
- [ ] `dotnet build` → 0 errors, 0 warnings (除 nullable 警告外)
- [ ] `dotnet list reference` 確認無循環相依

**相依：** 無

**預計觸及檔案：**
- `Vulperonex.sln`
- `src/Vulperonex.Domain/Vulperonex.Domain.csproj`
- `src/Vulperonex.Application/Vulperonex.Application.csproj`
- `src/Vulperonex.Infrastructure/Vulperonex.Infrastructure.csproj`
- `src/Vulperonex.Plugins.Abstractions/Vulperonex.Plugins.Abstractions.csproj`
- `src/Adapters/Vulperonex.Adapters.Abstractions/...csproj`
- `src/Adapters/Vulperonex.Adapters.Twitch/...csproj`
- `src/Adapters/Vulperonex.Adapters.Simulation/...csproj`
- `src/Hosts/Vulperonex.Web/...csproj`
- `src/Hosts/Vulperonex.Cli/...csproj`
- `src/Hosts/Vulperonex.Desktop/...csproj`
- `tests/Vulperonex.Tests.Unit/...csproj`
- `tests/Vulperonex.Tests.Integration/...csproj`
- `tests/Vulperonex.Tests.Architecture/...csproj`

**規模：** L（結構性，但每個檔案小）

---

#### Task 2：Domain 核心 — IStreamEvent、Domain Events、StreamUser

**描述：** 實作所有 Domain 層型別：`IStreamEvent` 介面、7 個 MVP 事件 record、`StreamUser` value object、`StreamEventKeys` 常數類、`PlatformConnectionChangedEvent`。全部不可變 record。

**驗收標準：**
- [ ] 7 個 MVP 事件 + `PlatformConnectionChangedEvent` 均實作 `IStreamEvent`
- [ ] `StreamEventKeys` 含所有 7 個 canonical key 常數 + `PlatformConnectionChanged = "platform.connection_changed"`
- [ ] `StreamUser` 包含 `Platform`, `UserId`, `DisplayName`
- [ ] Domain 層無任何對 Infrastructure / Adapters 的引用（Architecture test 驗證）
- [ ] 所有 `EventId` 預設為 `Ulid.NewUlid().ToString()`

**驗證步驟：**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → `Domain_HasNoReferenceToTwitchSymbols` 通過
- [ ] `dotnet test tests/Vulperonex.Tests.Unit` → Domain 事件單元測試通過

**相依：** Task 1

**預計觸及檔案：**
- `src/Vulperonex.Domain/Events/IStreamEvent.cs`
- `src/Vulperonex.Domain/Events/StreamEventKeys.cs`
- `src/Vulperonex.Domain/Events/StreamEventDescriptions.cs`
- `src/Vulperonex.Domain/Events/UserSentMessageEvent.cs`
- `src/Vulperonex.Domain/Events/UserFollowedEvent.cs`
- `src/Vulperonex.Domain/Events/UserDonatedEvent.cs`
- `src/Vulperonex.Domain/Events/UserSubscribedEvent.cs`
- `src/Vulperonex.Domain/Events/UserGiftedSubscriptionEvent.cs`
- `src/Vulperonex.Domain/Events/ChannelRaidedEvent.cs`
- `src/Vulperonex.Domain/Events/RewardRedeemedEvent.cs`
- `src/Vulperonex.Domain/Events/PlatformConnectionChangedEvent.cs`
- `src/Vulperonex.Domain/StreamUser.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/`

**規模：** M

---

#### Task 3：Domain — Member 實體與 Value Objects

**描述：** 實作 `MemberRecord`、`PlatformIdentity`、`LoyaltyInfo` 等 Domain 實體。定義 `IMemberRepository`（commands）與 `IMemberQueryService`（queries）介面於 Application 層（輕量 CQRS）。

**驗收標準：**
- [ ] `MemberRecord` 含 `MemberId` (ULID string)、`Identities: List<PlatformIdentity>`
- [ ] `IMemberRepository` 於 Application 層（不在 Domain）
- [ ] `IMemberQueryService` 獨立於 Repository（CQRS 分離）
- [ ] 架構測試：Application 不引用 Infrastructure
- [ ] 架構測試 `DciRoleIsolationTests`：`Vulperonex.Domain` 下類名以 `Role` 或 `Behavior` 結尾的類型，其 assembly 不得引用 `Vulperonex.Infrastructure`、`Microsoft.EntityFrameworkCore` 或任何 `*.Infrastructure.*` 命名空間（SPEC §4.1b）

**驗證步驟：**
- [ ] `dotnet test tests/Vulperonex.Tests.Unit` → Member domain 測試通過
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → 層相依規則 + DciRoleIsolationTests 通過

**相依：** Task 2

**預計觸及檔案：**
- `src/Vulperonex.Domain/Members/MemberRecord.cs`
- `src/Vulperonex.Domain/Members/PlatformIdentity.cs`
- `src/Vulperonex.Domain/Members/LoyaltyInfo.cs`
- `src/Vulperonex.Application/Members/IMemberRepository.cs`
- `src/Vulperonex.Application/Members/IMemberQueryService.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Members/`
- `tests/Vulperonex.Tests.Architecture/Domain/DciRoleIsolationTests.cs`

**規模：** S

---

### Checkpoint：Phase 1

- [ ] `dotnet build` 全綠
- [ ] Architecture tests 通過：Domain 無 Infrastructure / Platform 引用
- [ ] Domain 單元測試覆蓋率 > 90%

---

### Phase 2：事件匯流排 + Infrastructure

> 詳細切片計畫：`docs/phases/phase-2-infrastructure/plan.md`

#### Task 4：IStreamEventBus + In-Memory 實作

**描述：** 在 Application 層定義 `IStreamEventBus` 介面，在 Infrastructure 層實作以 `Channel<IStreamEvent>` 為基礎的 `InMemoryStreamEventBus`。包含：handler 例外隔離（try/catch per handler）、`WaitForIdleAsync` 供測試用、fire-and-forget 語意。TDQ 溢位機制在此 task 中以 stub 形式存在（task 6 補全）。

**驗收標準：**
- [ ] `PublishAsync` fire-and-forget，不阻塞呼叫端
- [ ] 單一 handler 例外不影響其他 handler
- [ ] `Subscribe<T>` 使用 **assignable match**：`Subscribe<IStreamEvent>` 接收所有事件；`Subscribe<UserSentMessageEvent>` 只接收該具體型別；`WorkflowModule` / `OverlayModule` / `MemberModule` 均以 `Subscribe<IStreamEvent>` 訂閱
- [ ] module 收到不關心的 event type → 內部 no-op，不拋例外，不寫錯誤 log（module 應對每個 event type 做 switch/pattern match，unknown type 走 default 分支靜默忽略）
- [ ] `WaitForIdleAsync` 在佇列清空且所有 handler 完成後 resolve；handler exception 被 catch + log，不透過 `WaitForIdleAsync` 拋出；`WaitForIdleAsync` 完成後 caller 無法得知 handler 是否出錯（error count 不暴露）
- [ ] Channel 容量預設 10,000（硬編碼常數，Task 8 完成後透過 `ISystemSettingsService` 覆寫；此 task 不相依 Task 8）

**驗證步驟：**
- [ ] `dotnet test` → 事件匯流排單元測試全通過
- [ ] 測試：publish 5 個事件，handler throw，其餘 4 個仍收到
- [ ] 測試（assignable match）：`Subscribe<IStreamEvent>` handler 訂閱 → publish `UserSentMessageEvent` → handler 收到（確認 concrete type 符合 IStreamEvent 介面時 handler 被呼叫）
- [ ] 測試：`Subscribe<UserSentMessageEvent>` handler 訂閱 → publish `UserFollowedEvent` → handler **不被**呼叫（型別不匹配）
- [ ] 測試（fire-and-forget timing）：handler 內 `await Task.Delay(100ms)`；publish 後 `PublishAsync` 應在 < 10ms 內返回（caller 不等待 handler 完成）；handler 完成後 `WaitForIdleAsync` resolve

**相依：** Task 2

**預計觸及檔案：**
- `src/Vulperonex.Application/EventBus/IStreamEventBus.cs`
- `src/Vulperonex.Infrastructure/EventBus/InMemoryStreamEventBus.cs`
- `tests/Vulperonex.Tests.Unit/Infrastructure/EventBus/`

**規模：** M

---

#### Task 5：EF Core + SQLite + DB Migration 基礎架構

**描述：** 設定 `VulperonexDbContext`，加入 EF Core 10 SQLite provider，建立第一批遷移（MemberRecord、PlatformIdentity、WorkflowRules、SystemSettings、AppLogs、PlatformUserDisplayInfo 表格）。實作啟動時自動執行 additive migration 的邏輯。

**驗收標準：**
- [ ] `dotnet ef migrations add InitialSchema` 成功
- [ ] `MigrateAsync()` 於測試中可在 in-memory SQLite（或 temp file SQLite）執行
- [ ] `SystemSettings` 表格含 Key、Value、Category、UpdatedAt 欄位
- [ ] `WorkflowRules` 表格含 `ConditionsJson`、`ActionsJson` TEXT 欄位
- [ ] DB bootstrap 執行 `PRAGMA auto_vacuum = FULL`（在 `MigrateAsync` 之前；確保 AppLogs DELETE 後頁面歸還 OS；此設定只在建庫時有效，事後 VACUUM 才能重建）
- [ ] Architecture test `MigrationClassifier` 正確識別破壞性遷移（raw SQL migration 分類：`migrationBuilder.Sql(...)` 若包含 `DROP`/`DELETE`/`TRUNCATE`/`ALTER`/`RENAME` 任一關鍵字（regex `\b(DROP|DELETE|TRUNCATE|ALTER|RENAME)\b`），`MigrationClassifier` 必須標記 destructive/review-required；任意 `ALTER` 均視為 review-required；不可因為是 raw SQL 就略過分類）

**驗證步驟：**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → MigrationClassifier 測試通過
- [ ] Unit test：`MigrationClassifier` 對含 `DROP TABLE` 的 raw SQL migration → 標記 destructive（確保 classifier 不只掃描 EF operation type，也掃描 raw SQL 內容）
- [ ] Unit test：`MigrationClassifier` 對含 `RENAME TABLE` 的 raw SQL → 標記 destructive
- [ ] Unit test：`MigrationClassifier` 對含 `ALTER TABLE AddColumn`（非 drop）的 raw SQL → 標記 review-required（任意 ALTER 均保守標記）
- [ ] Unit test：`MigrationClassifier` 對含 `DELETE FROM` 的 raw SQL → 標記 destructive
- [ ] Unit test：`MigrationClassifier` 對含 `TRUNCATE` 的 raw SQL → 標記 destructive
- [ ] Integration test：`VulperonexDbContext` 可建立 + migrate
- [ ] Integration test：bootstrap 完成後執行 `PRAGMA auto_vacuum` → 回傳 `2`（FULL）；確認 pragma 已在 `MigrateAsync` 前設定

**相依：** Task 3

**預計觸及檔案：**
- `src/Vulperonex.Infrastructure/Data/VulperonexDbContext.cs`
- `src/Vulperonex.Infrastructure/Migrations/` （自動生成）
- `src/Vulperonex.Infrastructure/Data/Configurations/` (EF config per entity)
- `tests/Vulperonex.Tests.Architecture/Migrations/MigrationClassifierTests.cs`
- `tests/Vulperonex.Tests.Integration/Infrastructure/`

**規模：** M

---

#### Task 6：TDQ（Transient Delivery Queue）+ At-Least-Once 保證

**描述：** 補全事件匯流排的溢位處理：Channel 超過閾值時寫入 SQLite `TransientDeliveryQueue` 表格；啟動時重播未處理項目；實作 `ActionExecutionLog` 表格與完整 dedup 協定（含 `Failed` 狀態、`AttemptCount`、永久失敗語意、`IClock` 抽象）。

**驗收標準：**
- [ ] 模擬 Channel 滿載時，事件寫入 TDQ 而非丟棄
- [ ] 啟動時 TDQ 內未處理事件被重播
- [ ] `ActionExecutionLog` dedup key：一般 actions = `(EventId, WorkflowRuleId, ActionIndex)`；`InvokeSubWorkflowAction` = `(EventId, WorkflowRuleId, ActionIndex, InvocationId)` — **`InvocationId` 必須在 action 執行前即產生並納入 TDQ payload**（不可每次執行時動態產生新 ULID），確保重播使用同一 InvocationId 維持 dedup 正確性
- [ ] `ActionExecutionLog` schema 含 `Status`（Pending/Completed/**Failed**）與 `AttemptCount`
- [ ] stale Pending（> 30s，threshold 透過 `IClock` 抽象注入）→ 重試，`AttemptCount++`；`AttemptCount >= MaxRetries+1` → `Status=Failed`（永久停止，後續重播略過，不再重試）
- [ ] `Status=Completed` 或 `Status=Failed` 的 log entry → 重播時略過

**驗證步驟：**
- [ ] Integration test：強制 Channel 滿 → 事件進 TDQ → 重新啟動 → 事件被重播
- [ ] Integration test：同一 key 重複執行 → 第二次被略過（dedup，`Status=Completed`）
- [ ] Integration test（fake clock）：stale Pending 超過 30s threshold → 重試被觸發（`AttemptCount` 增加）
- [ ] Integration test：`AttemptCount` 達 `MaxRetries+1` → `Status=Failed` → 後續重播不再重試
- [ ] Unit test：`InvocationId` 預先持久化於 TDQ payload → 重播後讀回同一 id（不重新生成）

**相依：** Task 4, Task 5

**預計觸及檔案：**
- `src/Vulperonex.Infrastructure/EventBus/TransientDeliveryQueue.cs`
- `src/Vulperonex.Infrastructure/EventBus/ActionExecutionLog.cs`
- `src/Vulperonex.Infrastructure/Migrations/` (TDQ + ActionExecutionLog 遷移)
- `tests/Vulperonex.Tests.Integration/EventBus/`

**規模：** M

---

#### Task 7：MemberResolver + PlatformUserDisplayCache（Infrastructure-only）

**描述：** 實作 `MemberResolver`（`INSERT OR IGNORE + SELECT` atomic GetOrCreate）與 `PlatformUserDisplayCache`（L1 in-memory LRU + L2 SQLite，`IPlatformUserInfoCache` 介面）。包含 `UserDisplayInfo` record 和 TTL 清理背景 worker。**注意：`PlatformUserDisplayCache` 屬於 Adapter Infrastructure 層，Application/Domain 不知道其存在（不注入至 MemberModule 等 Application 服務）；adapter 在事件回呼中直接呼叫快取更新。**

**驗收標準：**
- [ ] 並行呼叫 `MemberResolver` 不產生重複 `MemberRecord`（ULID 唯一）
- [ ] L1 miss → L2 check → Platform API fetch 路徑正確
- [ ] L1 容量預設 500（hardcoded constant，本 Task 不相依 Task 8；Task 8 完成後 `ISystemSettingsService` 可覆蓋容量/TTL — 連接點在 Task 8，不在本 Task）
- [ ] TTL 預設 24h，過期 rows 由 background worker 清除（TTL 同樣 hardcoded 至 Task 8）

**驗證步驟：**
- [ ] `dotnet test tests/Vulperonex.Tests.Integration` → SC-8 通過（`MemberId` 格式為 ULID）
- [ ] 並行測試：10 個 Task 同時解析同一 PlatformUser → 只建立 1 個 MemberRecord
- [ ] 單元測試（`IPlatformUserInfoCache.UpdateAsync` cache miss）：呼叫 `UpdateAsync` for non-existent user → 建立 default 快取 row（`Badges = Array.Empty<string>()`，所有 nullable 欄位 null，`FetchedAt = UtcNow`）；不拋例外

**相依：** Task 5

**預計觸及檔案：**
- `src/Vulperonex.Application/Members/IMemberResolver.cs`（port 介面，只定義 `ResolveAsync`）
- `src/Vulperonex.Infrastructure/Members/MemberResolver.cs`（EF Core + raw SQL 實作，在 Infrastructure）
- `src/Vulperonex.Infrastructure/Cache/PlatformUserDisplayCache.cs`
- `src/Vulperonex.Infrastructure/Cache/LruCache.cs`
- `tests/Vulperonex.Tests.Integration/Members/`

**規模：** M

---

#### Task 8：SystemSettings 服務 + 設定熱重載

**描述：** 實作 `ISystemSettingsService`：SQLite-backed Get/Set、`IObservable<SettingChangedEvent>` 變更通知、三層設定分離（appsettings.json / SystemSettings table / 加密 OAuth token）。

**驗收標準：**
- [ ] `Get<T>(key, default)` 正確反序列化
- [ ] `SetAsync` 寫入 DB 並觸發 `Changes` observable
- [ ] 訂閱者在設定變更後收到通知（無需重新啟動）
- [ ] OAuth refresh token 以 **AES-256-GCM** 加密存於 SQLite：versioned envelope `"v1:" + Base64(nonce(12B) || ciphertext || tag(16B))` 存入 `SystemSettings.Value TEXT`；key = machine.key raw 32 bytes（無 KDF）；setting key = `SystemSettingKey.OAuthTwitchRefreshToken` (`"oauth.twitch.refresh_token"`)；**AAD = 設定鍵名 UTF-8 bytes**（`"oauth.twitch.refresh_token"`），傳入 `AesGcm.Encrypt()`，解密時重新傳入；AAD 不存入 envelope，繫結密文至鍵名防止跨鍵複製攻擊
- [ ] 首次啟動時 OS app-data 路徑的 `machine.key` 不存在 → 自動建立（cryptographically random 32 bytes）；建立後立即設定限制性權限（Windows: 目前使用者 ACL FullControl，移除繼承；Unix: chmod 0600）；chmod/ACL 失敗 → 拋 `IOException`（fail-fast，不降級繼續）；路徑固定為 OS app-data root（Windows: `%AppData%\Vulperonex\`；macOS: `~/Library/Application Support/Vulperonex/`；Linux: `~/.local/share/Vulperonex/`）；不跟隨 `Database:Path` 自訂設定
- [ ] `machine.key` 遺失（如換裝置）→ AES 解密失敗 → 拋出 `CredentialDecryptionException`（呼叫端提示使用者重新授權，不 crash）

**驗證步驟：**
- [ ] 單元測試：設定 key 後立即讀取返回新值
- [ ] 單元測試：訂閱 Changes → SetAsync → subscriber 收到 SettingChangedEvent
- [ ] Integration test（temp dir）：`machine.key` 不存在 → `MachineKeyProvider` 建立 32-byte key 並設定 OS 限制性權限（Windows ACL user-only / Unix 0600）；此項為 integration test 以驗真實 ACL/chmod 行為（SPEC §7.2：Unit tests 無 I/O）。※ `MachineKeyProvider` 注入 `IFileSystem` 抽象供 unit tests 用 fake — 若使用 `System.IO.Abstractions` NuGet，需依 SPEC §8.2 ask-first 規則確認後再加入 csproj；亦可自訂輕量 `IFileSystem` port（2-3 methods）避免外部相依
- [ ] Integration test（chmod 失敗模擬）：`machine.key` ACL/chmod 操作失敗 → `MachineKeyProvider` 拋 `IOException`（fail-fast）
- [ ] 單元測試（AAD cross-key copy attack）：直接呼叫底層加密基礎架構（`AesGcmEncryptor` 或等效 helper，繞過 `IOAuthTokenStore` 的 MVP platform 限制）：以 AAD=`"oauth.twitch.refresh_token"` 加密一段 token → 取得 envelope → 以 AAD=`"oauth.unknown.refresh_token"` 嘗試解密同一 envelope → 拋 `CredentialDecryptionException`（GCM authentication tag 驗證失敗，因 AAD 不同）。**不透過 `IOAuthTokenStore.GetRefreshTokenAsync("unknown")` 呼叫**（那會先拋 `ArgumentException`，測不到 AAD 失敗）
- [ ] 單元測試：以錯誤 key 解密 → 拋出 `CredentialDecryptionException`（不 crash，呼叫端可 catch）
- [ ] 單元測試：`StoreRefreshTokenAsync("twitch", "raw-token")` → `GetRefreshTokenAsync("twitch")` 回傳 `"raw-token"`（round-trip 正確，確認 platform 參數路由正確）
- [ ] 單元測試：`StoreRefreshTokenAsync("twitch", ...)` 寫入 `SystemSettingKey.OAuthTwitchRefreshToken`（`"oauth.twitch.refresh_token"`）key，且 `SystemSettings.Category = "oauth"`
- [ ] 單元測試：`StoreRefreshTokenAsync("twitch", "raw-token")` 後直接查詢 `SystemSettings.Value TEXT` → 不等於 `"raw-token"`（AES-256-GCM Base64 envelope 已套用，DB 不儲存明文）
- [ ] 單元測試：`IOAuthTokenStore.GetRefreshTokenAsync` 介面 contract — key 不存在 → 回 `null`；key 存在但 machine.key 錯誤 → 丟 `CredentialDecryptionException`（非 null、非 crash）
- [ ] 單元測試：竄改 DB 中 ciphertext 一個 byte 後呼叫 `GetRefreshTokenAsync` → 丟 `CredentialDecryptionException`（GCM authentication tag 驗證失敗）
- [ ] 單元測試：同一 raw token 連續呼叫 `StoreRefreshTokenAsync("twitch", "raw-token")` 兩次 → 兩次儲存的 `SystemSettings.Value` 字串不同（per-token random nonce 確認；若相同代表 nonce 固定，為安全漏洞）
- [ ] 單元測試：`StoreRefreshTokenAsync("unknown-platform", ...)` → 拋 `ArgumentException`（MVP 只允許 "twitch"）

**相依：** Task 5

**預計觸及檔案：**
- `src/Vulperonex.Application/Settings/ISystemSettingsService.cs`
- `src/Vulperonex.Application/Settings/SystemSettingKey.cs`（所有 MVP key 常數，含 `OAuthTwitchRefreshToken = "oauth.twitch.refresh_token"`、`StreamingPlatform = "streaming.platform"`、`LogMinLevel = "log.min_level"`、`LogDbRetentionDays = "log.db_retention_days"`、`LogDbMaxSizeMb = "log.db_max_size_mb"`、`LogFileRetentionDays = "log.file_retention_days"`；Task 18 `AppLogsCleanupWorker` 直接引用，不另建）
- `src/Vulperonex.Application/Auth/IOAuthTokenStore.cs`（介面：`StoreRefreshTokenAsync(platform, rawToken)`、`GetRefreshTokenAsync(platform)` → `string?`；key pattern = `oauth.{platform}.refresh_token`；`GetRefreshTokenAsync` null if missing, throws `CredentialDecryptionException` if key lost；**MVP 只允許 `"twitch"` 作為 platform 值**，其他 platform 拋 `ArgumentException("Unknown OAuth platform: {platform}")`）
- `src/Vulperonex.Infrastructure/Settings/SystemSettingsService.cs`
- `src/Vulperonex.Infrastructure/Security/MachineKeyProvider.cs`
- `src/Vulperonex.Infrastructure/Auth/OAuthTokenStore.cs`（IOAuthTokenStore 實作，呼叫 MachineKeyProvider 加密）
- `tests/Vulperonex.Tests.Unit/Settings/`
- `tests/Vulperonex.Tests.Unit/Auth/`（OAuthTokenStore round-trip 及加密覆寫測試）

**規模：** M

---

### Checkpoint：Phase 2

- [ ] `dotnet test` 全綠
- [ ] Integration test：事件可 publish → bus → handler 收到
- [ ] DB migrate 於 temp SQLite 成功
- [ ] MemberResolver 並行測試通過
- [ ] 人工 review 架構層相依

---

### Phase 3：Simulation Adapter + WorkflowEngine（垂直切片一）

#### Task 9：Simulation Adapter + IStreamEventTypeRegistry

**描述：** 實作 `SimulationAdapter`（`IStreamEventSource`）— 接受 CLI / 測試指令後 publish 對應的 `IStreamEvent`。同時實作 `IStreamEventTypeRegistry`（所有 Adapter 在 `StartAsync` 時 Register 其 EventTypeKey）。**注意：** SimulationAdapter 支援 publish 全部 7 個 MVP 事件（內部 API / 測試用）；REST/CLI `POST /api/simulate/{alias}` 只公開 `chat/follow/sub` 三個 alias（Task 14b），其餘 4 個事件（donated、gifted_sub、channel.raided、reward.redeemed）只透過直接呼叫 `SimulationAdapter.SimulateAsync` 測試，不進 MVP CLI/REST surface。

**驗收標準：**
- [ ] `SimulationAdapter` 可 publish 全部 7 個 MVP 事件
- [ ] `SimulationAdapter` 無任何 `Vulperonex.Adapters.Twitch` 型別引用（SC-3）
- [ ] `IStreamEventTypeRegistry.IsKnown(key)` 在 Adapter StartAsync 後返回 true
- [ ] 重複 `Register` 同一 key（TwitchAdapter + SimulationAdapter 均呼叫）— idempotent，`GetAll()` 不出現重複 key；不同 description 保留先到者並 log warning
- [ ] `Register("platform.connection_changed", ..., isSystemEvent: true)` → `GetAll()` 不包含該 key（系統事件過濾）
- [ ] Architecture test SC-4 通過

**驗證步驟：**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → SC-3, SC-4 通過
- [ ] Integration test：SimulationAdapter.SimulateAsync(UserSentMessageEvent) → bus handler 收到
- [ ] Unit test（registry duplicate first-wins）：先 `Register("user.message", descA)`，再 `Register("user.message", descB)` → `GetAll()` 中 `user.message` 的 description 為 descA（先到者優先）；warning log 記錄；`GetAll()` 只含一筆 `user.message`
- [ ] Unit test（registry idempotent same key + same description）：同 key + 同 description → no warning log；`GetAll()` 只含一筆（確認 StreamEventDescriptions constants 使用時不觸發 warning）

**相依：** Task 4

**預計觸及檔案：**
- `src/Adapters/Vulperonex.Adapters.Abstractions/IStreamEventSource.cs`（**新建**，`IStreamEventSource` 定義於此，非 Application）
- `src/Adapters/Vulperonex.Adapters.Simulation/SimulationAdapter.cs`
- `src/Vulperonex.Application/EventTypes/IStreamEventTypeRegistry.cs`
- `src/Vulperonex.Application/EventTypes/StreamEventTypeRegistry.cs`
- `tests/Vulperonex.Tests.Architecture/Adapters/SimulationAdapterIsolationTests.cs`
- `tests/Vulperonex.Tests.Integration/Adapters/`

**規模：** M

---

#### Task 10：WorkflowEngine — 條件評估 + 基本 Actions

**描述：** 實作 `WorkflowEngine`（`IHostedService`，訂閱 bus）：載入 `WorkflowRule`、評估 `Trigger`、依序評估 `Conditions`（UserRole、MessageContent、Cooldown）、執行 `Actions`（SendChatMessage、InvokeSubWorkflow）。包含 Priority 排序、Serial/Parallel 並行模式、per-action ErrorBehavior/Timeout。

**驗收標準：**
- [ ] `UserSentMessageEvent` 符合 rule → `IPlatformChatSender.SendAsync` 被呼叫（SC-2）
- [ ] 條件短路：第一個失敗後停止評估
- [ ] `CooldownCondition` Global / PerUser 正確計時
- [ ] `StopOnError` → 後續 actions 不執行
- [ ] `RetryOnError` + backoff 正確重試最多 `MaxRetries` 次
- [ ] action timeout 以 `CancellationToken` 訊號，不強殺 thread

**驗證步驟：**
- [ ] `dotnet test` → SC-2 通過（SendChatMessageAction 單次觸發）
- [ ] 單元測試：SC-9（TargetPlatform override / default source platform）
- [ ] 單元測試：`SendChatMessageAction` 的 `TargetPlatform` 指定未在 DI 中注冊的 platform → warning log 被呼叫 + action skip（`IPlatformChatSender` 未被呼叫；WorkflowEngine 不 throw）
- [ ] Integration test：publish event → WorkflowEngine → IPlatformChatSender mock 收到（使用 `InMemoryWorkflowRuleRepository` fake，不相依 Task 14a 的 EF Core 實作）

**相依：** Task 4, Task 5, Task 9

**預計觸及檔案：**
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- `src/Vulperonex.Application/Workflows/Conditions/`
- `src/Vulperonex.Application/Workflows/Actions/SendChatMessageAction.cs`
- `src/Vulperonex.Application/Workflows/Actions/InvokeSubWorkflowAction.cs`
- `src/Vulperonex.Application/Workflows/IWorkflowRuleRepository.cs`（寫入端介面）
- `src/Vulperonex.Application/Workflows/IWorkflowRuleQueryService.cs`（讀取端介面，CQRS 分離）
- `src/Vulperonex.Application/Workflows/Dtos/WorkflowRuleSummaryDto.cs`
- `tests/Vulperonex.Tests.Unit/Workflows/`
- `tests/Vulperonex.Tests.Integration/Workflows/`

**規模：** L → 若需要，可拆成 10a（Condition 評估）+ 10b（Action 執行 + Error Handling）

---

#### Task 11：Plugin System — IVulperonexPlugin + InvokePluginAction

**描述：** 實作 `IVulperonexPlugin`、`IPluginContext`、`IPluginActionContext` 合約（`Plugins.Abstractions`），以及 `WorkflowEngine` 中 `InvokePluginAction` 的執行器。包含：plugin 不存在時 warning + skip、`ActionExecutionKey` 傳遞給 plugin、靜態 DI 注冊模式。

**驗收標準：**
- [ ] Plugin 可透過 `IPluginContext.Events.PublishAsync` 發布自訂事件
- [ ] `WorkflowRule` 的 `InvokePluginAction` 可觸發已註冊 plugin 的 `ExecuteActionAsync`
- [ ] Plugin 缺失時 warning + skip，不 crash
- [ ] SC-10 通過（plugin 發布事件 → rule 觸發 → SendAsync 收到）
- [ ] `IPluginActionContext.Params` 型別為 `IReadOnlyDictionary<string, JsonElement>`；plugin action 讀取 string param 用 `.GetString()`、int 用 `.GetInt32()`、bool 用 `.GetBoolean()`，不發生 `InvalidCastException`（單元測試帶 JSON payload 驗証）

**驗證步驟：**
- [ ] `dotnet test` → SC-10 通過
- [ ] 架構測試：`Plugins.Abstractions` 只相依 Domain + Application
- [ ] 架構測試（reflection）：`IPluginContext` 和 `IPluginActionContext` 介面的所有 property type 均不含 `System.IServiceProvider`（reflection 掃描介面 members，確保 service locator 未透過 property 型別或巢狀 interface 洩漏）

**相依：** Task 10

**預計觸及檔案：**
- `src/Vulperonex.Plugins.Abstractions/IVulperonexPlugin.cs`
- `src/Vulperonex.Plugins.Abstractions/IPluginContext.cs`
- `src/Vulperonex.Plugins.Abstractions/IPluginActionContext.cs`
- `src/Vulperonex.Application/Workflows/Actions/InvokePluginAction.cs`
- `tests/Vulperonex.Tests.Unit/Plugins/`
- `tests/Vulperonex.Tests.Integration/Plugins/`

**規模：** M

---

### Checkpoint：Phase 3

- [ ] `dotnet test` → SC-2, SC-3, SC-4, SC-9, SC-10 全通過
- [ ] SimulationAdapter → Bus → WorkflowEngine → IPlatformChatSender 端到端通過
- [ ] Plugin 可發布事件並觸發規則

---

### Phase 4：Twitch Adapter + MemberModule（垂直切片二）

#### Task 12：Twitch Adapter — IRC + EventSub + DisplayHints

**描述：** 實作 `TwitchAdapter`：Twitch IRC WebSocket（chat messages）+ Twitch EventSub WebSocket（follows, subs, bits, raids, rewards）。所有 Twitch payload 對應至 Domain Events（無 Twitch 型別洩漏至 Domain）。Adapter 在 StartAsync 時注冊 EventTypeKey。包含 DisplayHints 豐富化（`display.segments`、`user.avatar` 等），禁止 raw HTML。指數退避重連（最大 60s，含 ±20% jitter）。

**驗收標準：**
- [ ] SC-1 通過：mock Twitch payload → 7 個 MVP IStreamEvent 全部產生
- [ ] SC-6（WorkflowEngine half）：SimulationAdapter 與 TwitchAdapter（mock IRC）觸發相同 WorkflowEngine 副作用（`IPlatformChatSender.SendAsync` 收到相同呼叫）
- [ ] SC-6（MemberRecord half）由 Task 13 補全，本 Task 不驗 DB state
- [ ] `display.segments` 型別為 `text | emote | badge | mention`，無 raw HTML
- [ ] `display.color` 僅接受 6-digit RGB hex；badge ID/value normalization 有長度與字元限制
- [ ] `StartAsync` double-start idempotent，不重複註冊 event keys 或開第二組 socket
- [ ] 重連：指數退避 1s → 2s → ... max 60s，含 ±20% jitter
- [ ] EventSub duplicate delivery 以 `(platform, sourceEventId)` dedup cache 處理（1000 entries 或 10 分鐘 TTL）；10 分鐘 replay window 內 missed events 不因 replay 標記被過濾
- [ ] 斷線時發布 `PlatformConnectionChangedEvent { IsConnected: false, Reason: "reconnecting" }`，重連成功後發布 `PlatformConnectionChangedEvent { IsConnected: true }`
- [ ] OAuth PKCE callback 監聽 `appsettings.json → Auth:CallbackPort`（預設 7979），衝突時嘗試 7980, 7981（最多 3 次）
- [ ] OAuth callback listener 只接受 loopback remote IP 與 `localhost:{port}` / `127.0.0.1:{port}` / `[::1]:{port}` Host header，且兩者必須同時通過；`state` 10 分鐘 TTL 且 single-use
- [ ] OAuth PKCE code exchange（mock token endpoint）→ access token 只存 in-memory，不寫 DB 或 log；重新啟動後 `TwitchAdapter.StartAsync` 從 `IOAuthTokenStore.GetRefreshTokenAsync("twitch")` 讀取 refresh token 換取新 access token（refresh token 存在但 machine.key 遺失 → `CredentialDecryptionException` → 提示重新授權）
- [ ] OAuth refresh token 透過 `IOAuthTokenStore.StoreRefreshTokenAsync` 加密後持久化；`TwitchAdapter` 不直接呼叫 `ISystemSettingsService`，不自行加密（加密責任在 Task 8 的 `IOAuthTokenStore` 實作）
- [ ] 新增或更新 `appsettings.json` 範例，包含 `Auth:CallbackPort: 7979` 及 Twitch Redirect URI 設定說明

**驗證步驟：**
- [ ] `dotnet test` → SC-1, SC-6 WorkflowEngine half 通過
- [ ] 單元測試：IRC 訊息解析 → UserSentMessageEvent（Platform="twitch"，正確 User）
- [ ] 單元測試：OAuth PKCE `state` 不符、超過 10 分鐘 TTL 或已使用 → callback handler 拒絕並記 warning，不呼叫 token exchange endpoint（CSRF 防護驗証）
- [ ] 單元測試：OAuth callback listener 邊界 — 非 loopback 請求（模擬 RemoteIpAddress = 192.168.1.x）→ 忽略不處理；Host header 非 `localhost:{port}` / `127.0.0.1:{port}` / `[::1]:{port}` → 拒絕；remote IP 與 Host header 需同時通過；非預設 path（如 `/other`）→ 忽略；收到有效 callback 後 listener 關閉（single-use，驗第二次呼叫已無 listener）
- [ ] 單元測試：OAuth callback port 衝突 → 自動遞增至 7980
- [ ] 單元測試：7979、7980、7981 全部被占用 → OAuth flow 失敗（拋例外或回傳 error，不 hang）；應提示使用者手動在 Twitch Developer Console 更新 Redirect URI
- [ ] 單元測試：PKCE exchange mock → access token in-memory only（`ISystemSettingsService.SetAsync` 未以 access_token 類 key 被呼叫；mock logger sink 不含 access_token、authorization code、code_verifier 字串）
- [ ] 單元測試：PKCE exchange → `IOAuthTokenStore.StoreRefreshTokenAsync` 被呼叫，且傳入值 **等於** raw refresh token（加密責任在 OAuthTokenStore 實作內，Adapter 傳入明文；DB 持久化值不等於明文，由 Task 8 驗證）；mock logger sink 不含 raw refresh token 或 `refresh_token` plain value
- [ ] Integration test：TwitchAdapter `StartAsync` 後 `IStreamEventTypeRegistry` 含所有 Twitch EventTypeKeys（SC-1 所需 7 個）；`IsKnown("user.message")` = true；`IsKnownForWorkflow("platform.connection_changed")` = false（確認真實 adapter 而非 FakeTwitchEventTypeRegistrar 正確注冊全部 7 keys + system event）
- [ ] 單元測試：`TwitchAdapter.StartAsync` 時 `IOAuthTokenStore.GetRefreshTokenAsync("twitch")` 有值 → 呼叫 mock token refresh endpoint → access token in-memory 更新（重新啟動 refresh flow 驗証）
- [ ] 單元測試：`TwitchAdapter.StartAsync` 時 `GetRefreshTokenAsync` 拋 `CredentialDecryptionException` → catch 後提示重新授權，不 crash
- [ ] 單元測試（DisplayHints segment type allowlist）：Twitch IRC payload 含 HTML-like 內容（`<script>alert(1)</script>`）→ 產生的 `display.segments` 所有項目的 `type` 欄位只為 `text | emote | badge | mention`；`text` 片段值保留原始字串（含 `<`、`>`）不應被刪除或轉義 — 安全邊界在 type allowlist 與前端 `textContent` 渲染，不在 value 過濾
- [ ] 單元測試（DisplayHints `display.color` 格式）：Twitch IRC 帶合法顏色（`#FF4A4A`）→ `display.color` 值符合 `^#[0-9A-Fa-f]{6}$`；Twitch 回傳 3-digit shorthand、8-digit alpha、CSS named color（如 `red`）或空字串 → adapter 不填入 `display.color`（null / 省略，不帶非法值）
- [ ] 單元測試（fake clock + fake socket）：IRC WebSocket 斷線 → 第一次 base delay ≈ 1s，第二次 ≈ 2s，第三次 ≈ 4s（指數退避）；套用 jitter 後落在 ±20% 範圍；delay 不超過 60s
- [ ] 單元測試：斷線 → 立即 publish `PlatformConnectionChangedEvent { IsConnected: false, Reason: "reconnecting" }`，重連成功後 publish `{ IsConnected: true }`（兩個事件依序驗證）
- [ ] 單元測試（EventSub replay）：模擬 EventSub 斷線後在 10 分鐘窗口內重連，server 重播 2 個已錯過的事件 → 兩個事件均正常 publish 到 bus（adapter 不過濾 replay events）
- [ ] 單元測試（EventSub dedup）：同一 `(platform, sourceEventId)` 在 dedup cache 內重複送達時只 publish 一次，且 10 分鐘 TTL 到期後會釋放 cache entry
- [ ] 單元測試（EventSub replay 逾時）：斷線超過 10 分鐘（fake clock）→ 重連後無 replay event → adapter 繼續正常執行（無 crash / deadlock），並記錄 warning log「events may have been lost」
- [ ] 單元測試（adapter cache update — subscribe）：publish `UserSubscribedEvent` → adapter 事件回呼呼叫 `IPlatformUserInfoCache.UpdateAsync`，使 `IsSubscriber = true`（mock cache 驗証）
- [ ] 單元測試（adapter cache update — donate）：publish `UserDonatedEvent`（含 cumulative `TotalBitsGiven`）→ `IPlatformUserInfoCache.UpdateAsync` 被呼叫，快取 `TotalBitsGiven` 使用 `max(existing, incoming)` monotonic absolute replacement（非 `+= amount`）；重播同一事件 → `TotalBitsGiven` 不變；out-of-order 較小 incoming value 不覆蓋較大 existing value；若平台後台人工調整導致需要降低本地值，Phase 4 不自動回退，未來需走明確 admin reset
- [ ] 單元測試（adapter cache update — follow）：publish `UserFollowedEvent` → `IPlatformUserInfoCache.UpdateAsync` 被呼叫，follower badge 出現於 `Badges`（mock cache 驗証）
- [ ] 單元測試（badge normalization）：Twitch IRC `badges` tag 含重複項目（如 `subscriber/2000,subscriber/2000,vip`）→ 產生的 `user.badges` 重複抑制後保留首次出現順序（`subscriber/2000,vip`）；badge ID 只含 `[A-Za-z0-9_/\-]` 字元；badge value 超過 64 字元會截斷或丟棄；badge 數量超過 20 → 截斷至前 20

**相依：** Task 10, Task 9, Task 8, Task 7

**預計觸及檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/TwitchAdapter.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/OAuthCallbackListener.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/EventSub/TwitchEventSubClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Mapping/`
- `src/Adapters/Vulperonex.Adapters.Abstractions/IPlatformUserInfoCache.cs`
- （相依 Task 8 新增的）`src/Vulperonex.Application/Auth/IOAuthTokenStore.cs`
- `src/Hosts/Vulperonex.Web/appsettings.json`（若尚不存在則新增範例設定；加 Auth:CallbackPort）
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/`
- `tests/Vulperonex.Tests.Integration/Adapters/`

**規模：** L

---

#### Task 13：MemberModule + Overlay DTO 安全過濾

**描述：** 實作 `MemberModule`（`IHostedService`）：訂閱 Domain Events，呼叫 `MemberResolver` 建立/更新 MemberRecord。**`PlatformUserDisplayCache` 更新由 adapter 本身負責**（SPEC §4.3b — 快取僅限 Adapter Infrastructure，Application/Domain 不知道其存在）；`MemberModule` 不直接存取 `IPlatformUserInfoCache`，不引用 `Vulperonex.Adapters.Abstractions`。實作 `OverlayModule` 的 DTO 投影，確保 Overlay DTOs 只含允許欄位（MemberId 等內部欄位不外洩）。

**驗收標準：**
- [ ] SC-8 通過：publish `UserSentMessageEvent` → PlatformIdentity 建立，MemberId 為 ULID 格式
- [ ] SC-6（MemberRecord half）：SimulationAdapter 與 TwitchAdapter（mock IRC）分別 publish 相同 payload → MemberRecord DB state 相同（`WaitForIdleAsync` 後比對）
- [ ] `UserSubscribedEvent` → MemberRecord 的 IsSubscriber 更新
- [ ] Member state replay 使用 `(platform, sourceEventId)` dedup key；TDQ replay 不造成重複 row 或重複累加，且不得以每次處理新產生的 ULID 作為 replay dedup key
- [ ] Overlay DTO `/overlay/chat` 序列化 JSON property set == `{schemaVersion, eventId, timestamp, displayName, colorHex, segments, badges}` 精確白名單（不含 MemberId、UserId、TotalBitsGiven）
- [ ] Overlay DTO `/overlay/alerts` 序列化 JSON property set == `{schemaVersion, eventId, timestamp, displayName, eventType, tier}` 精確白名單（不含 MemberId、**PlatformUserId**）
- [ ] Overlay DTO `/overlay/member` 序列化 JSON property set == `{schemaVersion, displayName, avatarUrl, checkInCount}` 精確白名單（不含 MemberId、**TotalLoyalty**、LinkedPlatforms）
- [ ] `schemaVersion` 固定為 `1`；`eventId` 是 public delivery id，不得使用 MemberId、PlatformUserId 或其他內部 identity；優先來自 platform-provided id（IRC `msg-id` / EventSub `message_id`），缺值時 adapter 生成 ULID 並標記為 synthetic；`timestamp` 為 UTC ISO-8601 event time
- [ ] `OverlayMemberPayload` 是狀態快照，不是事件流；因此不含 `eventId` / `timestamp`

**驗證步驟：**
- [ ] `dotnet test tests/Vulperonex.Tests.Integration` → SC-8 通過
- [ ] Integration test：SC-6 MemberRecord half — **兩次各使用 fresh SQLite fixture（每次測試前清空或新建 DB）**：Sim run: SimulationAdapter publish payload X → `WaitForIdleAsync` → snapshot S1；Twitch run: TwitchAdapter mock IRC 相同 payload → `WaitForIdleAsync` → snapshot S2；assert S1 == S2（防止共用 DB 時第二次命中既有 member 而掩蓋等價問題）
- [ ] 單元測試：對每個 DTO 型別，反射取得所有 JSON-serializable properties，assert set 完全等於 SPEC §4.14 允許清單（exact match；新增未知欄位即報錯）
- [ ] SignalR hub serialization exact key-set 驗證移至 Task 15；Task 13 只驗 DTO contract 與 `System.Text.Json` serialization key set
- [ ] 單元測試：`OverlayMemberPayload` JSON key set **精確等於** `{schemaVersion, displayName, avatarUrl, checkInCount}`（whitelist 靜態驗証）

**相依：** Task 7, Task 10, Task 12

**預計觸及檔案：**
- `src/Vulperonex.Application/Members/MemberModule.cs`
- `src/Vulperonex.Application/Overlay/OverlayModule.cs`
- `src/Vulperonex.Application/Overlay/Dtos/` (OverlayChatPayload, OverlayAlertPayload, OverlayMemberPayload)
- `tests/Vulperonex.Tests.Unit/Overlay/`
- `tests/Vulperonex.Tests.Integration/Members/`

**規模：** M

---

### Checkpoint：Phase 4

- [ ] `dotnet test` → SC-1, **SC-6a**（Task 12 WorkflowEngine half）, **SC-6b**（Task 13 MemberRecord half）, SC-8 通過
- [ ] Twitch IRC mock → UserSentMessageEvent → MemberRecord 建立
- [ ] Overlay DTO 精確白名單（exact JSON key set）正確

---

### Phase 5：Web Host + SignalR + CLI

#### Task 14a：ASP.NET Minimal API — WorkflowRule CRUD + EventTypes

**描述：** 實作 `Vulperonex.Web` WorkflowRule REST API 與 event-types endpoint：WorkflowRule CRUD（GET/POST/PUT/DELETE）、EventTypeKey registry 驗證、循環引用靜態分析（save 時）、i18n 錯誤碼（無人類可讀字串）、CQRS 分離。`GET /api/event-types`（供 UI dropdown）。單一 write path：UI 和 CLI 都走 API。

**驗收標準：**
- [ ] `GET /api/rules` → 回傳所有 rule `WorkflowRuleSummaryDto` 列表（呼叫 `IWorkflowRuleQueryService`）
- [ ] `GET /api/rules/{id}` 存在 → 回傳完整 rule JSON
- [ ] `GET /api/rules/{id}` 404 → `WORKFLOW_RULE_NOT_FOUND`
- [ ] `POST /api/rules` 存入有效 rule
- [ ] `POST /api/rules` 含未知 EventTypeKey → 400 + `UNKNOWN_EVENT_TYPE_KEY`（validator 呼叫 `IsKnownForWorkflow(key)`，排除 system events；`platform.connection_changed` 即使 `IsKnown()=true` 也不允許用於 WorkflowRule trigger）
- [ ] 循環 SubWorkflow 引用 → 400 + `CIRCULAR_WORKFLOW_REFERENCE`
- [ ] `PUT /api/rules/{id}` 更新並回傳更新後 rule
- [ ] `DELETE /api/rules/{id}` 刪除；不存在 → `WORKFLOW_RULE_NOT_FOUND`
- [ ] `POST /api/rules/{id}/enable` → `IsEnabled=true`；不存在 → `WORKFLOW_RULE_NOT_FOUND`
- [ ] `POST /api/rules/{id}/disable` → `IsEnabled=false`；不存在 → `WORKFLOW_RULE_NOT_FOUND`
- [ ] 回應 body 格式：`{ "error": "ERROR_CODE", "meta": {...} }`
- [ ] CQRS 分離：`GET /api/rules` + `GET /api/rules/{id}` 呼叫 `IWorkflowRuleQueryService`；`POST /api/rules`、`PUT /api/rules/{id}`、`DELETE /api/rules/{id}`、`POST /api/rules/{id}/enable`、`POST /api/rules/{id}/disable` 呼叫 `IWorkflowRuleRepository`；兩組路徑不互用（以 fake `IWorkflowRuleRepository` / fake `IWorkflowRuleQueryService` 做 integration-level interaction tests 驗證：每個端點斷言呼叫了正確 port，且未呼叫另一個 port；不相依靜態 assembly 掃描）
- [ ] 未知 Action type → 400 + `UNKNOWN_ACTION_TYPE`
- [ ] 未知 Condition type → 400 + `UNKNOWN_CONDITION_TYPE`
- [ ] Action 缺少 required params（如 SendChatMessage 缺 `Template` 欄位）→ 400 + `ACTION_MISSING_REQUIRED_PARAM`（**欄位名為 `Template`，非 `message`**）
- [ ] Action `timeoutMs` < 0 或超過 60000、`maxRetries` > 10、`backoffMs` < 100 或超過 30000、`errorBehavior` 非合法值 → 400 + `INVALID_ACTION_CONFIG`（完整上下限驗證）
- [ ] `MessageContentCondition.FullRegex` pattern 無效（compile error）或超過 512 字元 → 400 + `INVALID_REGEX_PATTERN`
- [ ] `CooldownCondition.DurationSeconds` < 1 或 > 86400 → 400 + `INVALID_ACTION_CONFIG`
- [ ] `Parallel` mode `MaxParallelism` < 1 或 > 64 → 400 + `INVALID_ACTION_CONFIG`
- [ ] `SendChatMessageAction.Template` 超過 500 字元 → 400 + `INVALID_ACTION_CONFIG`
- [ ] 單元測試（Template rendering）：未知 placeholder `{event.unknown}` → 保留原文（輸出含 `{event.unknown}` 字串）；空值 placeholder（對應值為 `null`/空字串）→ 替換為空字串（驗 WorkflowEngine/SendChatMessageAction 的 template 展開邏輯）
- [ ] `PUT /api/rules/{id}` body id != route id → 400 + `INVALID_RULE_ID_MISMATCH`
- [ ] `POST /api/rules` 成功 → **201 Created** + `Location: /api/rules/{newId}` header
- [ ] `DELETE /api/rules/{id}` 成功 → **204 No Content**（無 body）
- [ ] `GET /api/event-types` 在 SimulationAdapter `StartAsync` 後回傳所有 Simulation EventTypeKeys（TwitchAdapter coverage 移至 Task 12）；endpoint 根據靜態 simulate alias map（`chat→user.message`, `follow→user.followed`, `sub→user.subscribed`）補 `isSimulatable` 欄位（不從 registry 讀取）
- [ ] `POST /api/rules` with `trigger.eventTypeKey: "platform.connection_changed"` → 400 + `UNKNOWN_EVENT_TYPE_KEY`（`IsKnownForWorkflow` 排除系統事件；即使 `IsKnown()=true` 也不允許作為 WorkflowRule 觸發器）

**驗證步驟：**
- [ ] Integration test：WorkflowRule 完整 CRUD 端到端（in-memory SQLite）
- [ ] 循環引用偵測測試
- [ ] Integration test：`GET /api/event-types` Simulation keys
- [ ] Integration test：`GET /api/event-types` 回傳 keys 中**不含** `platform.connection_changed`（系統事件排除驗証）
- [ ] Integration test：`GET /api/event-types` 回傳中 `user.message` 的 `isSimulatable = true`；`user.donated` 的 `isSimulatable = false`（驗 endpoint 根據靜態 alias map 補值正確）
- [ ] Integration test（CQRS separation）：每個 GET 端點以 fake `IWorkflowRuleRepository`（無配置行為）驗 repository 未被呼叫；每個 write 端點以 fake `IWorkflowRuleQueryService`（無配置行為）驗 query service 未被呼叫

**相依：** Task 10, Task 9, Task 5

**預計觸及檔案：**
- `src/Hosts/Vulperonex.Web/Endpoints/WorkflowRuleEndpoints.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/EventTypeEndpoints.cs`
- `src/Hosts/Vulperonex.Web/Validation/WorkflowRuleValidator.cs`
- `src/Hosts/Vulperonex.Web/Program.cs`
- `src/Vulperonex.Infrastructure/Workflows/WorkflowRuleRepository.cs`（IWorkflowRuleRepository 寫入端實作）
- `src/Vulperonex.Infrastructure/Workflows/WorkflowRuleQueryService.cs`（IWorkflowRuleQueryService 讀取端實作）
- `tests/Vulperonex.Tests.Integration/Web/`
- `tests/Vulperonex.Tests.Integration/Web/WorkflowRuleCqrsInteractionTests.cs`（interaction tests with fakes，非靜態 assembly 掃描）

**規模：** M

---

#### Task 14b：ASP.NET Minimal API — Simulate / Config / Member 端點

**描述：** 在 `Vulperonex.Web` 補齊 CLI 所需其餘端點：
- **Simulate**：`POST /api/simulate/{eventType}`（呼叫 `ISimulationAdapter`）。`{eventType}` 只接受短 alias：`chat`→`user.message`、`follow`→`user.followed`、`sub`→`user.subscribed`（對應 §4.4 canonical EventTypeKey）。不接受原始 EventTypeKey 字串，避免與 WorkflowRule 驗證邏輯混用。
- **Config**：`GET|PUT /api/config/{key}`（呼叫 `ISystemSettingsService`）。請求依序檢查：**(1) prefix denylist 先**（`security.*` → 403 `CONFIG_KEY_SECURITY_NAMESPACE`；`oauth.*` → 403 `OAUTH_CREDENTIAL_NAMESPACE`）；**(2) registry lookup**（不在 `SystemSettingKey` → 400 `UNKNOWN_CONFIG_KEY`）。prefix denylist 先於 registry，未知的 `oauth.*` key 回 403 不回 400。
- **Member**：`GET /api/members`、`GET /api/members/{id}`（呼叫 `IMemberQueryService`）。
- `security.*` config key 在本端點封鎖（403 + `CONFIG_KEY_SECURITY_NAMESPACE`）；`/api/settings/security/*` 為保留路徑字首，MVP 不新增 CRUD 端點；Kestrel loopback-only binding 本身已保護這些路徑。

**驗收標準：**
- [x] `POST /api/simulate/chat` → `ISimulationAdapter.SimulateAsync` 被呼叫
- [x] `POST /api/simulate/follow` → `UserFollowedEvent` publish 驗證
- [x] `POST /api/simulate/sub` → `UserSubscribedEvent` publish 驗證
- [x] 不存在的 eventType → 400 + `UNKNOWN_SIMULATE_EVENT_TYPE`
- [x] `GET /api/config/log.min_level` → 回傳目前設定值；未知 key → 400 + `UNKNOWN_CONFIG_KEY`
- [x] `PUT /api/config/log.min_level` with value `"Warning"` → `ISystemSettingsService.SetAsync` 更新設定
- [x] `GET/PUT /api/config/security.*` → 403 + `CONFIG_KEY_SECURITY_NAMESPACE`
- [x] `GET/PUT /api/config/oauth.twitch.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE`
- [x] `GET /api/config/oauth.unknown.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE`（prefix denylist 先於 registry lookup）
- [x] `GET /api/members?platform=twitch&limit=20` → 回傳 JSON 列表；`limit` 預設 50，上限 200；limit > 200 → 400 + `INVALID_QUERY_PARAM`
- [x] `GET /api/members/{id}` 存在 → 回傳 member JSON；不存在 → 404 + `MEMBER_NOT_FOUND`

**驗證步驟：**
- [x] Integration test：simulate / config / member 各一條 happy path + error path
- [x] Integration test：protected namespace 與 unknown oauth prefix 行為
- [x] Integration test：member endpoint 查詢與錯誤路徑

**相依：** Task 14a, Task 9, Task 8, Task 7

**規模：** M

---

#### Task 15：SignalR Hub + Overlay Push + 雙埠 Kestrel

**描述：** 實作 `/hubs/events` SignalR Hub（管理頁面用）和 `/hubs/overlay/*` 各 Overlay Hub（OBS 用）。設定 Kestrel 雙埠（ApiPort 5000 / OverlayPort 5001）、連接埠配對自動遞增邏輯、localhost-only 預設。SC-5 端到端測試。

**驗收標準：**
- [x] SC-5 通過：publish `UserSentMessageEvent` → `/overlay/chat` client 5 秒內收到 `OverlayChatPayload`
- [x] Overlay port 5001 不需驗證即可連線
- [x] 埠衝突時自動嘗試 +2 pair（最多到 5008/5009），全部失敗時回報清晰錯誤
- [x] 兩埠以 `IPAddress.Loopback`（IPv4）+ `IPAddress.IPv6Loopback`（IPv6）雙重繫結
- [x] `/hubs/events` 能轉寄 `PlatformConnectionChangedEvent`
- [x] `/hubs/overlay/chat` 與 `/hubs/overlay/alerts` 的 JSON key set 精確符合白名單；不洩漏 `memberId`、`platformUserId` 等內部識別碼
- [x] `/hubs/overlay/member` 可連線但 MVP 不 publish 事件，作為 post-MVP skeleton

**驗證步驟：**
- [x] Integration test：SignalR overlay 5 秒內收到 chat payload
- [x] Unit/integration tests：port pair allocation、exhaustion、loopback binding
- [x] Integration test：Overlay DTO JSON key set exact match
- [x] 手動：OBS/browser source 連線 localhost overlay route

**相依：** Task 13, Task 14a

**規模：** M

---

#### Task 16：CLI — simulate / config / member / rule 指令

**描述：** 實作 `Vulperonex.Cli` 所有 MVP 指令與 REPL：`simulate chat|follow|sub`、`config get|set`、`member list|show|seed|delete`、`rule list|show|create|update|enable|disable|delete`、`twitch auth status|start|reset`。CLI 透過 HTTP 呼叫 REST API，不直接存取 DB。成功輸出至 stdout；4xx/5xx 錯誤代碼輸出至 stderr + exit code 1。

**DB 路徑解析（CLI 與 Web host 共同規則）：** 讀取 `appsettings.json → Database:Path`（若存在），否則使用 OS app-data 預設路徑。`Database:Path` 不允許透過 `appsettings.{Environment}.json` 或環境變數覆蓋，確保 Web host 與 CLI 讀相同 DB。

**驗收標準：**
- [x] `simulate chat|follow|sub` 印出 JSON acknowledgement，含 `accepted`、`eventTypeKey`、`eventId`、`platformUserId`、`displayName`
- [x] `config get|set log.min_level` 可讀寫非敏感設定；`security.*` / `oauth.*` protected namespace passthrough 至 stderr
- [x] `member list|show|seed|delete` 可支援手動驗證資料建立與清理
- [x] `rule list|show|create|update|enable|disable|delete` 可完成 rule lifecycle
- [x] `twitch auth start` 提供 PKCE 授權入口，callback loopback-only，refresh token 經 `IOAuthTokenStore` 加密儲存
- [x] `twitch auth reset` 清除 refresh token，並可重複 start
- [x] REPL、help、Ctrl+C cancellation、TTY line editor、Tab 多候選輪替已完成

**驗證步驟：**
- [x] Integration test：CLI args → HTTP → API → DB/response 驗證
- [x] Integration test：protected namespace passthrough 至 stderr + exit code 1
- [x] Integration test：DB path env override 禁止
- [x] 手動：published CLI 對獨立 Web API process 執行 rule/config/member/simulate smoke
- [x] 手動：Twitch OAuth 真實瀏覽器授權，完成 code exchange + encrypted refresh_token 儲存
- [x] 手動：REPL 驗證完成

**相依：** Task 14b, Task 15

**規模：** M

---

### Checkpoint：Phase 5

- [x] SC-2, SC-5, SC-8, SC-9 通過
- [x] WorkflowRule CRUD + 循環引用偵測通過
- [x] `security.*` / `oauth.*` config key 封鎖與 CLI passthrough 通過
- [x] CLI rule / config / member / simulate 全命令 integration test 通過
- [x] CLI simulate chat fixture rule + mock sender 驗證通過
- [x] CLI simulate → Overlay SignalR 端到端手動測試，結果記錄於 `docs/phases/phase-5-web-signalr-cli/manual-verification.md`
- [x] Phase 5 CLI E2E 收尾：published CLI 對獨立 Web API process 的人工 terminal smoke 已執行
- [x] Phase 5 Twitch OAuth CLI 收尾：真實瀏覽器授權含完整 code exchange + refresh_token 加密儲存已人工執行
- [x] Phase 5 CLI REPL 補充：人工 terminal 驗證已完成
- [x] 開發者快捷入口：新增 `tools/cli.ps1`
- [x] Task 15：兩埠均以 loopback（IPv4 127.0.0.1 + IPv6 ::1）雙重綁定，socket bind test 驗證通過
- [x] Task 14b：protected config namespace 與 `Database:Path` 規則通過

---

### Phase 6：日誌 + 前端 + Photino

> 詳細切片計畫：`docs/phases/phase-6-web-ui/plan.md`
> 詳細切片清單：`docs/phases/phase-6-web-ui/todo.md`
> 手動驗證紀錄：`docs/phases/phase-6-web-ui/manual-verification.md`
> **前置條件 Gate 已具備開始規劃資格**：Phase 5 Checkpoint 三項手動驗收（CLI E2E 收尾、Twitch OAuth 真實瀏覽器授權含完整 code exchange + refresh_token 儲存、REPL 手動驗收）已在父計畫與 Phase 5 manual verification 中標記完成。
> **目前優先順序變更**：Phase 6 尚未完成的 Photino/manual verification 等非 workflow parity 項目延後處理；目前先執行 Phase 7 Workflow Parity。Phase 7 可使用 Phase 6 已完成的 Web UI/rule JSON editor/overlay history 基線，不等待完整 Phase 6 Checkpoint。

> **Task 17：已移除** — 原 MockYouTube Adapter，推遲出 MVP scope。Task 18 直接接續 Task 16。

#### Task 19：Vue 前端骨架 — Vite + PrimeVue + Pinia + SignalR composable

**描述：** 建立 `src/frontend/` Vue 3.5 SPA：Vite 7.3、PrimeVue 4 Unstyled、UnoCSS Preset Wind 4、Pinia Setup Store、vue-i18n、`useStreamEvents` composable、Overlay route skeleton。首次安裝 npm package 前須 ask-first。

**驗收摘要：**
- [ ] `src/frontend/package.json` 釘定 `"packageManager": "pnpm@9.15.4"`
- [ ] `pnpm dev`、`pnpm vue-tsc --noEmit`、`pnpm test`、`pnpm build`、`pnpm lint` 通過
- [ ] API client 支援 relative base URL 與 `VITE_API_URL` override
- [ ] 管理 Hub `/hubs/events` 透過 `useStreamEvents` 匯入 `useEventStore`，以 `eventId` + `timestamp` 實作 last-write-wins
- [ ] Overlay routes 與 admin route 分離；overlay 不共享 admin store
- [ ] `/overlay/chat`、`/overlay/alerts`、`/overlay/member` route 可掛載；member overlay 為 MVP empty skeleton
- [ ] Overlay 使用 text binding，不使用 `v-html`

**相依：** Task 15

---

#### Task 20：Web 管理主控台 (Web Admin UI)

**描述：** 實作可長時間操作的本機主控台：Dashboard status cards、Simulate panel、Event monitor、Member read-only panel、Rule JSON Textarea CRUD、Twitch auth panel、i18n error handling、SignalR reconnect/polling fallback。

**驗收摘要：**
- [ ] Member panel 僅支援 list/show 唯讀操作，**不提供 seed/delete 按鈕，不新增 member CRUD 端點**；測試資料建立與清理留給 CLI/manual test surface
- [ ] Rule panel 支援 list/show/create/update/enable/disable/delete，destructive 操作使用確認 dialog，409 `WORKFLOW_RULE_CONFLICT` 顯示專屬提示
- [ ] Rule create/update 支援 JSON file/manual textarea；1MB 限制、paste debounce、非響應式大文字暫存、送出失敗保留內容與 refocus
- [ ] EventTypeKey dropdown 排除 `platform.connection_changed`，並用 badge 標示 `chat|follow|sub` 可模擬 keys
- [ ] Twitch auth start/reset 透過後端 OAuth endpoints；callback 後端消費 `code` 並完成 token exchange，不把 OAuth `code`/raw error 帶回 Web UI
- [ ] OAuth 成功/失敗結果由 `platform.connection_changed`、`GET /api/twitch/status` 與 toast/status card 呈現
- [ ] SignalR 中斷後 HTTP polling fallback 使用 30s base、2x factor、300s max，reconnected 時釋放 timer
- [ ] `zh-TW` / `en-US` i18n error code coverage 完整，5xx 顯示 `INTERNAL_ERROR` 並 `console.error`
- [ ] Browser manual E2E 依 `docs/phases/phase-6-web-ui/manual-verification.md` 執行並記錄

**相依：** Task 19, Task 14a, Task 14b, Task 15

---

#### Task 18：Serilog 三 Sink + AppLogs 清理 worker

**描述：** 設定 Console、Rolling File、SQLite AppLogs 三個 sink，加入 EventTypeKey、Platform、MemberId、WorkflowRuleId、ActionType 等結構化欄位，並實作 `log.min_level` 熱重載與 AppLogs retention/size cleanup worker。

**驗收摘要：**
- [ ] Console、rolling file、SQLite AppLogs 均可寫入，且不重複設定 `PRAGMA auto_vacuum`
- [ ] `log.db_max_size_mb` 預設值為 `50`，`log.db_retention_days` 預設值為 `30`
- [ ] `MemberId` 僅記錄 pseudonymized ULID，註明 Non-PII，禁止真實姓名、E-mail、platform raw ID 等 PII
- [ ] cleanup worker 以 retention/size 先觸發者為準；size cleanup 使用 `PRAGMA page_count * page_size` 並在清理後執行 `VACUUM`
- [ ] `AppLogsCleanupWorker.ExecuteOnce()` 可單次觸發測試，不相依 background timing

**相依：** Task 5, Task 8

---

#### Task 21：Photino Desktop Shell + 靜態 fallback

**描述：** `Vulperonex.Desktop` 啟動 Web host 並載入 Vue UI，處理 single-instance mutex、port pair allocation、WebView2 缺失、migration 失敗、Web host crash fallback、Photino/Vue IPC bridge。

**驗收摘要：**
- [ ] `dotnet run --project src/Hosts/Vulperonex.Desktop` 開啟 Photino 視窗載入 Vue UI
- [ ] `<TargetFramework>net10.0-windows</TargetFramework>`，支援 Windows 10 1809+
- [ ] 啟動時使用 .NET named mutex 防止多實例造成 port/SQLite locking 衝突
- [ ] 任一 port 被占用時切到下一組 pair；全部 5 組用盡時顯示 no-ports dialog
- [ ] WebView2 缺失、migration 失敗、Web host crash 均有 dialog/fallback；Web host crash 第 4 次停止自動 retry
- [ ] IPC DTO schema 精確鎖定 `{ type: string, payload: any }`

**相依：** Task 19, Task 20

---

### Checkpoint：Phase 6（最終）

- [ ] 全部 Task 18-21 sub-task `[x]` 完成自檢
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit`
- [ ] `cd src/frontend; pnpm test`
- [ ] `cd src/frontend; pnpm build`
- [ ] `cd src/frontend; pnpm lint`
- [ ] `cd src/frontend; pnpm dev` smoke 通過
- [ ] Browser manual：Task 20 Browser Manual Checklist 全項目 PASS
- [ ] Browser manual：Task 20k Twitch OAuth E2E Checklist 全項目 PASS
- [ ] Desktop manual：Task 21 Desktop Shell Checklist 全項目 PASS
- [ ] `docs/phases/phase-6-web-ui/manual-verification.md` 所有 dated entries 均為 `Result: PASS`，無 pending 項目
- [ ] Git 暫存集限於 Phase 6 任務範圍
- [ ] 安全 review：Overlay DTO exact whitelist、loopback dual bind、OAuth PKCE state/callback/logger scrub、protected config namespace、AES-256-GCM token encryption、machine.key permissions、plugin context 不暴露 `IServiceProvider`

---

### Phase 7：Workflow Parity with Omni-Commander

> 詳細計畫：`docs/phases/phase-7-workflow-parity/plan.md`
> 詳細待辦：`docs/phases/phase-7-workflow-parity/todo.md`
> 對照來源：`ref/Omni-Commander/OmniCommander.Domain/Workflows/` + `ref/Omni-Commander/OmniCommander.Application/Workflows/` + `ref/Omni-Commander/OmniCommander.Application/Workflows/Executors/` + `ref/Omni-Commander/OmniCommander.Tests/Workflows/` + `ref/Omni-Commander/walkthrough.md`
> 優先順序：Phase 7 先行；Phase 6 未完成的 Photino/manual verification 等非 workflow parity 項目延後處理。
> 前置條件：Phase 5 runtime + Phase 6 已完成的 Web UI/rule JSON editor/overlay history 基線可用；不等待完整 Phase 6 Checkpoint。

Phase 7 將 Vulperonex workflow runtime 對齊 Omni-Commander 的常用 workflow 能力，但保留 Vulperonex 已建立的 strong-typed Action、樂觀鎖、TDQ/idempotent replay、Plugin contract、DTO whitelist 與 ask-first dependency discipline。

> 進度來源：本文件中的 checkbox 作為父層設計/驗收草案；實際完成狀態以各 phase `todo.md` 與 `tasks/todo.md` 為準。

#### Task 23：Variable / Expression substrate

**描述：** 建立 `ExpressionContext`、template resolver、NCalc expression evaluator。NCalc 僅用於 `ExecutionCondition` / `MatchCondition` 等條件式；Phase 7 不開任意自訂 function 註冊 API。

**相依：** Phase 5 runtime + Phase 6 已完成的 Web UI/rule JSON editor/overlay history 基線；參考 OmniCommander workflow/executor/tests。

#### Task 24：Step ExecutionCondition + OutputVariable

**描述：** `WorkflowAction` 增加 `ExecutionCondition` 與 `OutputVariable`，executor 回傳 `ActionExecutionResult`，讓後續 step 可透過 `{Step.<OutputVariable>.*}` 讀取輸出。

**相依：** Task 23

#### Task 25：Rule-level Throttle + Timeout

**描述：** `WorkflowRule` 增加 throttle policy 與 rule-level timeout，作為 action-level timeout/retry 的外層 envelope。

**相依：** Task 24

#### Task 26：OnFailureSteps

**描述：** `WorkflowRule` 增加補救鏈，優先以新欄位（如 `OnFailureActionsJson`）持久化，不混入既有 `ActionsJson`。Replay key 需區分 `Main` / `OnFailure` phase。

**相依：** Task 25

#### Task 28：Hot Reload Snapshot Cache

**描述：** Engine 改用 immutable rule snapshot cache，rule CRUD 後 invalidation，執行中 rule 與 EF tracker 隔離。

**相依：** Task 26

#### Task 29：Trigger Filter + MatchCondition

**描述：** 保留頂層 `EventTypeKey` 供 SQL 過濾，新增 trigger filter 與 NCalc `MatchCondition` 作 in-memory 評估。

**相依：** Task 28

#### Task 27：Sub-workflow Flag + InvokeRule Polish

> 註：Task 編號沿用早期排序；實作相依仍以 Task 29 完成後再處理 Task 27 為準。

**描述：** 增加 `IsSubWorkflow` 與 `InvokeSubWorkflowAction.Args`，但不得回退既有 stable `InvocationId` / `ActionExecutionKey` replay 語意。

**相依：** Task 29

#### Task 30：Executor Expansion

**描述：** 分批新增 Delay、StopIf、RandomPicker、Counter、Twitch Helix、Overlay/SystemEvent/Effect、CheckIn/Lottery 等 executor。所有 executor 必須走 strong-typed `WorkflowAction`；overlay/effect 類 executor 必須有 exact DTO whitelist 與 SignalR JSON contract 測試，不得任意 raw payload 穿透。

**相依：** Task 27

#### Task 32：ChatOutboxService

**描述：** `SendChatMessageAction` 改寫進 rate-limited outbox。缺 `IPlatformChatSender` 時不得 silent no-op，必須標示 skipped/failed 並記錄 structured warning。

**相依：** Task 30

#### Task 31：WorkflowTimer Scheduler

**描述：** 新增 WorkflowTimer entity、hosted service、Web API 與 CLI。Phase 7 只驗證單實例重新啟動 idempotency；多實例 leader election 延後。

**相依：** Task 30

#### Task 33：Web UI Builder Upgrade

**描述：** Rule editor 支援 trigger filter、matchCondition、throttle、timeout、OnFailure、step condition/output、sub-workflow toggle 與 timer CRUD。

**相依：** Task 31, Task 32

#### Task 34：Plugin Action Variable Surface

**描述：** Plugin action context 增加 backward-compatible Args surface，既有 plugin 不傳 Args 仍可運作。

**相依：** Task 33

#### Task 35：Manual Verification & Parity Sign-off

**描述：** 建立 12-15 個典型 rule samples，Web UI + CLI 雙路徑驗證，並於 `docs/phases/phase-7-workflow-parity/manual-verification.md` 記錄 OC 對照矩陣與 PASS/FAIL。

**相依：** Task 34

### Checkpoint：Phase 7

- [ ] 全部 Task 23-35 sub-task `[x]` 完成自檢
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual：5 個典型 rule 配置覆蓋 trigger filter / cooldown / counter / sub-workflow / timer，全部 PASS
- [ ] DTO whitelist / SignalR JSON contract：Phase 7 新 rule schema 與 overlay/effect payload 無 raw JSON 漏網
- [ ] `docs/phases/phase-7-workflow-parity/manual-verification.md` 記錄 PASS/FAIL + OC 對照矩陣

### Phase 7A：Workflow Editor UX Alignment with Omni-Commander

> 詳細計畫：`docs/phases/phase-7a-workflow-editor-ux/plan.md`
> 詳細待辦：`docs/phases/phase-7a-workflow-editor-ux/todo.md`
> 優先順序：Phase 7 runtime/schema parity 完成後，下一個 workflow 相關 active slice。Phase 8 其他領域功能擴充先不併進來。

**描述：** 針對目前 Web workflow editor 的可用性缺口補一個獨立 UX slice。目標不是再擴 schema，而是把既有 Phase 7 schema 以可操作、可發現、接近 `ref/Omni-Commander/OmniCommander.UI/src/components/workflow/` 的方式呈現。重點包含：修復 trigger filter「新增無反應」、把 `conditions` / `actions` / `onFailureSteps` 從 JSON-first 改為 visual builder、提供 variable picker 與欄位插入、保留 JSON import/export 作 advanced fallback，而不是主要操作路徑。

#### Task 36 - Workflow Editor UX Baseline Repair

**描述：** 先修掉目前已知的阻塞互動問題，避免後續 builder 仍建立在失效基線上。
**驗收標準：**
- [ ] `TriggerEditor` 的 filter「新增」按鈕按下後立即新增一列可編輯 key/value row；不需先有既有 filter 才能運作。
- [ ] filter row 支援新增、刪除、空 key 不落盤；重載既有規則後顯示一致。
- [ ] 既有 `TriggerEditor` / `ThrottleEditor` / `StepConditionInput` / `OnFailureEditor` 互動行為加 Vitest 覆蓋，至少鎖住目前回報過的 UX regressions。

#### Task 37 - Visual Builder for Conditions, Actions, and OnFailure

**描述：** `conditions`、`actions` 與 `onFailureSteps` 改用 visual builder 作主要 UI。`actions` / `onFailureSteps` 每一步可選 action type、編輯其強型別欄位、設定 `ExecutionCondition`、`OutputVariable`，並支援新增、刪除、排序；`conditions` 則提供對應 schema 的表單式編輯；`OnFailure` 與主流程共用同一 builder shell，但資料源分離。
**驗收標準：**
- [ ] `Conditions` / `Actions` / `OnFailure` 頁籤主畫面不再要求使用者直接編輯原始 JSON array 才能完成常見操作。
- [ ] visual builder 至少支援目前常見 condition 類型；未知 condition type 才退回 raw JSON fallback。
- [ ] step builder 至少支援 Phase 7 已落地的常用 actions：`sendChatMessage`、`randomPicker`、`delay`、`stopIf`、`updateCounter`、`invokeSubWorkflow`、`lookupTwitchUser`、`shoutout`、`refundTwitchRedemption`、`emitOverlayWidget`、`emitSystemEvent`、`triggerEffect`、`triggerCheckIn`、`addLotteryTickets`、`invokePluginAction`。
- [ ] 每個 action 顯示 human-readable 名稱、簡短用途說明、必要欄位提示；未知 action type 才退回 raw JSON fallback card。
- [ ] 支援 step 新增、刪除、上下移動；儲存後順序與 API payload 一致。
- [ ] `OnFailureSteps` 不可巢狀再開 `OnFailureSteps`，UI 要明確限制。

#### Task 38 - Variable Picker and Visual Text Inputs

**描述：** 對齊 Omni-Commander 的可用性核心，不要求完整 drag-and-drop graph，但要提供可發現的 variable picker 與文字欄位插入體驗。
**驗收標準：**
- [ ] variable picker 至少分成 `Trigger`、`Args`、`Step Outputs`、`Member`、`Failure` 幾個群組。
- [ ] picker 可插入到 action 參數、`ExecutionCondition`、`MatchCondition`、filter value、`OutputVariable` 以外的範本文字欄位。
- [ ] 插入格式固定為 Phase 7 expression/template contract：`{Trigger.*}`、`{Args.*}`、`{Step.*}`、`{Member.*}`；不引入第二套 DSL。
- [ ] 針對 `ExecutionCondition` / `MatchCondition` 提供至少一種「視覺化條件模式 + 原始運算式模式」切換；視覺化模式至少涵蓋單一變數、operator、單一比較值的常見條件。
- [ ] 文字欄位可直接打字，也可插入變數 chip；不要求一次做到 ProseMirror/drag-drop editor，但不可比現況 textarea 更難用。

#### Task 39 - JSON Fallback Demotion and Import/Export

**描述：** 保留 JSON-first 能力給進階使用者與 round-trip 驗證，但降級成 fallback surface，不再是主流程唯一入口。
**驗收標準：**
- [ ] 主 editor 預設進入表單/step builder 模式；raw JSON 僅作「進階編輯」或 import/export。
- [ ] 支援從 JSON 載入既有 rule 後對應回表單；無法完整對應的欄位需明確顯示 fallback/unsupported 提示。
- [ ] 支援從表單匯出目前 rule JSON，確保 CLI / API / docs sample 可 round-trip。
- [ ] 保留 1MB guard、parse error、focus/refocus、防止 oversized paste 等既有 Phase 6 安全欄位。

#### Task 40 - Omni Parity Review and Manual Verification

**描述：** 對照 `ref/Omni-Commander` 的 workflow editor，明確記錄哪些 UX 已對齊、哪些刻意不做，避免再次出現「文件寫完成，但互動不直覺」的模糊狀態。
**驗收標準：**
- [ ] `docs/phases/phase-7a-workflow-editor-ux/manual-verification.md` 記錄 editor UX checklist、PASS/FAIL、與 Omni 對照矩陣。
- [ ] Browser manual 至少覆蓋：conditions、trigger filter row、新增 action step、編輯 random picker、配置 onFailure step、插入 step output 變數、切回 raw JSON fallback。
- [ ] 明列 N/A：不做 graph editor、不做完整拖拉變數面板、不做 Phase 8 新 schema。

### Checkpoint：Phase 7A

- [ ] 全部 Task 36-40 sub-task `[x]` 完成自檢
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual：workflow editor 主流程不需直接手寫 JSON 即可建立與修改常見規則
- [ ] Browser manual：variable picker 可插入 trigger / step output / args 變數，儲存後 reload 一致
- [ ] `docs/phases/phase-7a-workflow-editor-ux/manual-verification.md` 記錄 PASS/FAIL + Omni UX 對照矩陣

### Phase 7B：Chat Output Observability and Overlay Template Presets

> 詳細計畫：`docs/phases/phase-7b-chat-overlay-presets/plan.md`
> 詳細待辦：`docs/phases/phase-7b-chat-overlay-presets/todo.md`
> 目的：補齊 workflow `SendChatMessage` 在 simulation / local 模式下的可觀測性缺口，並把聊天 overlay 從單一實作提升為可切換樣板系統。OneComme 相容定位為 extension/plugin slice，不直接併入 core。

#### Task 41 - Simulation Chat Output Observable Surface

**描述：** 補 `Simulation` 平台的 chat sender observable surface。`SendChatMessage` 不得只有 silent no-op；至少要能在 admin / overlay history / memory receiver 中看到 rendered message、platform、channel、dedupKey、status。
**驗收標準：**
- [ ] `Simulation` 平台執行 `SendChatMessage` 後，使用者可在可視介面或明確 API 中查到訊息結果。
- [ ] 結果至少包含 `message`、`platform`、`channel`、`dedupKey`、`status`、timestamp。
- [ ] workflow chat output 驗證不再相依 `/overlay/chat` 是否剛好有 bridge。

#### Task 42 - Chat Overlay Preset System

**描述：** 將 `/overlay/chat` 提升為 preset/template-driven overlay。提供多個內建樣板，並保留後續匯入 / 匯出能力；core 只定義 preset/package contract，不直接耦合 OneComme runtime。
**驗收標準：**
- [ ] 至少提供兩個可切換聊天樣板：Vulperonex 預設樣板 + 另一個內建或可安裝樣板。
- [ ] 樣板切換透過設定或 admin UI 完成，不需直接修改前端原始程式碼。
- [ ] 樣板渲染仍遵守 DTO 白名單與 text binding，不引入 `v-html` raw payload 直出。

#### Task 43 - OneComme Compatibility Path

**描述：** 以 OneComme 為優先相容目標之一，但以 extension / plugin 方式接入。定義樣板匯入器、目錄掃描器、或 adapter package 的最小契約，降低既有使用者遷移成本，同時維持 core 邊界。
**驗收標準：**
- [ ] 文件明列 OneComme 相容策略：哪些能力透過外掛直接相容、哪些透過對應、哪些暫不支援。
- [ ] 至少有一條 extension/import path 明確標示為 OneComme-compatible / migration-oriented；不要求 core 內建整包整合。
- [ ] 手動驗證記錄 OneComme 樣板目錄或 package metadata 的辨識與匯入流程。

### Checkpoint：Phase 7B

- [ ] 全部 Task 41-43 sub-task `[x]` 完成自檢
- [ ] workflow `SendChatMessage` 在 `Simulation` 模式下可直接觀察結果，不再需要猜測是否送出
- [ ] `/overlay/chat` 至少可切換兩個樣板，且 core preset contract 可承接外掛 / 可安裝樣板
- [ ] `docs/.../manual-verification.md` 記錄 observability + preset + extension compatibility PASS/FAIL

---

## 風險與緩解

| 風險 | 影響 | 緩解 |
|------|------|------|
| npm dependencies 撞版本或下載失敗 | 中 | Task 19a 先做 `corepack enable`，若 Windows shim 權限受阻改用 `corepack pnpm@9.15.4 <command>`；以 `corepack pnpm@9.15.4 install --lockfile-only --ignore-scripts` 做預檢；首次安裝前 ask-first |
| Rule visual builder 膨脹 | 中 | MVP 只做 JSON editor + 輕量表單；完整 builder 後續切片 |
| Twitch OAuth 與 Web UI callback UX 混淆 | 中 | OAuth `code` 僅由後端 loopback callback 消費；Web UI 唯讀 status/event |
| SignalR/Polling 測試 flake | 中 | 核心 reducer/backoff 用 fake timer 單元測試；端到端留 manual verification |
| Desktop/Photino 問題遮蔽 Web UI 問題 | 中 | 先以瀏覽器驗證 Web host，再進 Task 21 Desktop shell |
| .NET 10.0 + Photino 3.x 相容性 | 中 | Task 21 做 compatibility 預驗；必要時使用 WebView2 fallback 或獨立 Kestrel 模式 |

## 開放問題（已解答）

- **Q1 ✅：Twitch OAuth PKCE callback port 需要可設定。**
  - 實作：Kestrel 在 OAuth flow 期間臨時監聽 `appsettings.json → Auth:CallbackPort`（預設 7979）。
  - 若 7979 被佔用 → 嘗試 7980, 7981（最多 3 次）→ 全失則顯示 dialog 要求手動設定。
  - Twitch Developer Console 需將 `http://localhost:7979/auth/callback`（及備用埠）列為 Redirect URI。
- **Q2 ✅：pnpm 精確版本鎖定。**
  - `package.json` 加 `"packageManager": "pnpm@9.15.4"`；不接受 `pnpm@9` 或 `pnpm@9.x.x` 模糊字串。
- **Q3 ✅：Windows 10 最低支援。**
  - Photino 3.x 需 WebView2，Windows 10 1809+；缺失時顯示下載連結 dialog。
