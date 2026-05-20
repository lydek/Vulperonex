# 實作計畫：Vulperonex MVP

> 基於 docs/SPEC.md v0.3
> 建立日期：2026-05-11 / 最後更新：2026-05-13

---

## 概覽

Vulperonex 是平台無關的串流自動化工具。MVP 範圍：Twitch 接收事件 → 事件匯流排 → WorkflowEngine 執行規則 → Overlay 推送 SignalR → Photino 桌面殼。CLI 可模擬事件、管理設定、管理規則（list/show/enable/disable/delete）、查詢成員。

---

## 架構決策摘要

| # | 決策 |
|---|------|
| A1 | Clean Architecture + tactical DDD：Domain → Application → Infrastructure / Adapters → Hosts；Domain 擁有實體、Value Object、Domain Event 與 invariant |
| A2 | 事件匯流排：in-memory `Channel<IStreamEvent>`，10,000 slots，溢出進 TDQ (SQLite) |
| A3 | 成員 ID：ULID，`PlatformIdentity (Platform, PlatformUserId)` 複合唯一鍵 |
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
- **DCI-inspired Role/Behavior 準則（SPEC §4.1b）**：當 Aggregate 或 Domain service 累積多個使用情境行為時，可用 Role/Behavior 物件拆分；Role/Behavior 必須是純 Domain 邏輯，不得依賴 `DbContext`、EF Core 或任何 Infrastructure 型別；Context/Interaction 放在 Application use case（`*Context`/`*UseCase` 類型不得定義於 Domain）；MVP 不做 runtime dynamic role / reflection / mixin。架構測試（`DciRoleIsolationTests`）驗證 Role 物件無 Infrastructure 引用；Context/Interaction 位置規則以 PR code review gate 驗證（非 CI 自動測試）。

---

## 依賴圖

```
Vulperonex.Domain
    └── Vulperonex.Application
            ├── Vulperonex.Infrastructure               (實作 Application ports)
            ├── Vulperonex.Plugins.Abstractions         (依賴 Domain + Application)
            ├── Vulperonex.Adapters.Simulation          (依賴 Domain + Application + Adapters.Abstractions)
            ├── Vulperonex.Adapters.Twitch              (依賴 Domain + Application + Adapters.Abstractions)
            └── Hosts
                ├── Vulperonex.Web                      (依賴全部)
                ├── Vulperonex.Cli                      (依賴全部)
                └── Vulperonex.Desktop                  (包裝 Web)
Vulperonex.Adapters.Abstractions                        (IStreamEventSource、IPlatformUserInfoCache 等)
    ├── Vulperonex.Adapters.Simulation                  (同上，亦依賴 Application)
    ├── Vulperonex.Adapters.Twitch                      (同上，亦依賴 Application)
    └── (未來平台 Adapter 同樣依賴 Domain + Adapters.Abstractions + Application；`IStreamEventBus` 定義於 Application，publish 事件需此依賴)
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
- [ ] 每個 csproj 的 `<ProjectReference>` 符合依賴圖（Domain 不引用任何其他 Vulperonex 專案）
- [ ] Architecture test 專案存在（NetArchTest 設定完成）

**驗證步驟：**
- [ ] `dotnet build` → 0 errors, 0 warnings (除 nullable 警告外)
- [ ] `dotnet list reference` 確認無循環依賴

**依賴：** 無

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

**依賴：** Task 1

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
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → 層依賴規則 + DciRoleIsolationTests 通過

**依賴：** Task 2

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

**描述：** 在 Application 層定義 `IStreamEventBus` 介面，在 Infrastructure 層實作以 `Channel<IStreamEvent>` 為基礎的 `InMemoryStreamEventBus`。包含：handler 例外隔離（try/catch per handler）、`WaitForIdleAsync` 供測試用、fire-and-forget 語意。TDQ 溢出機制在此 task 中以 stub 形式存在（task 6 補全）。

**驗收標準：**
- [ ] `PublishAsync` fire-and-forget，不阻塞呼叫端
- [ ] 單一 handler 例外不影響其他 handler
- [ ] `Subscribe<T>` 使用 **assignable match**：`Subscribe<IStreamEvent>` 接收所有事件；`Subscribe<UserSentMessageEvent>` 只接收該具體型別；`WorkflowModule` / `OverlayModule` / `MemberModule` 均以 `Subscribe<IStreamEvent>` 訂閱
- [ ] module 收到不關心的 event type → 內部 no-op，不拋例外，不寫錯誤 log（module 應對每個 event type 做 switch/pattern match，unknown type 走 default 分支靜默忽略）
- [ ] `WaitForIdleAsync` 在佇列清空且所有 handler 完成後 resolve；handler exception 被 catch + log，不透過 `WaitForIdleAsync` 拋出；`WaitForIdleAsync` 完成後 caller 無法得知 handler 是否出錯（error count 不暴露）
- [ ] Channel 容量預設 10,000（硬編碼常數，Task 8 完成後透過 `ISystemSettingsService` 覆寫；此 task 不依賴 Task 8）

**驗證步驟：**
- [ ] `dotnet test` → 事件匯流排單元測試全通過
- [ ] 測試：publish 5 個事件，handler throw，其餘 4 個仍收到
- [ ] 測試（assignable match）：`Subscribe<IStreamEvent>` handler 訂閱 → publish `UserSentMessageEvent` → handler 收到（確認 concrete type 符合 IStreamEvent 介面時 handler 被呼叫）
- [ ] 測試：`Subscribe<UserSentMessageEvent>` handler 訂閱 → publish `UserFollowedEvent` → handler **不被**呼叫（型別不匹配）
- [ ] 測試（fire-and-forget timing）：handler 內 `await Task.Delay(100ms)`；publish 後 `PublishAsync` 應在 < 10ms 內返回（caller 不等待 handler 完成）；handler 完成後 `WaitForIdleAsync` resolve

**依賴：** Task 2

**預計觸及檔案：**
- `src/Vulperonex.Application/EventBus/IStreamEventBus.cs`
- `src/Vulperonex.Infrastructure/EventBus/InMemoryStreamEventBus.cs`
- `tests/Vulperonex.Tests.Unit/Infrastructure/EventBus/`

**規模：** M

---

#### Task 5：EF Core + SQLite + DB Migration 基礎設施

**描述：** 設定 `VulperonexDbContext`，加入 EF Core 10 SQLite provider，建立第一批遷移（MemberRecord、PlatformIdentity、WorkflowRules、SystemSettings、AppLogs、PlatformUserDisplayInfo 表格）。實作啟動時自動執行 additive migration 的邏輯。

**驗收標準：**
- [ ] `dotnet ef migrations add InitialSchema` 成功
- [ ] `MigrateAsync()` 於測試中可在 in-memory SQLite（或 temp file SQLite）執行
- [ ] `SystemSettings` 表格含 Key、Value、Category、UpdatedAt 欄位
- [ ] `WorkflowRules` 表格含 `ConditionsJson`、`ActionsJson` TEXT 欄位
- [ ] DB bootstrap 執行 `PRAGMA auto_vacuum = FULL`（在 `MigrateAsync` 之前；確保 AppLogs DELETE 後頁面歸還 OS；此設定只在建庫時有效，事後 VACUUM 才能重建）
- [ ] Architecture test `MigrationClassifier` 正確識別破壞性遷移（raw SQL migration 分類：`migrationBuilder.Sql(...)` 若包含 `DROP`/`DELETE`/`TRUNCATE`/`ALTER`/`RENAME` 任一關鍵字（regex `\b(DROP|DELETE|TRUNCATE|ALTER|RENAME)\b`），`MigrationClassifier` 必須標記 destructive/review-required；任意 `ALTER` 均視為 review-required；不可因為是 raw SQL 就跳過分類）

**驗證步驟：**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → MigrationClassifier 測試通過
- [ ] Unit test：`MigrationClassifier` 對含 `DROP TABLE` 的 raw SQL migration → 標記 destructive（確保 classifier 不只掃描 EF operation type，也掃描 raw SQL 內容）
- [ ] Unit test：`MigrationClassifier` 對含 `RENAME TABLE` 的 raw SQL → 標記 destructive
- [ ] Unit test：`MigrationClassifier` 對含 `ALTER TABLE AddColumn`（非 drop）的 raw SQL → 標記 review-required（任意 ALTER 均保守標記）
- [ ] Unit test：`MigrationClassifier` 對含 `DELETE FROM` 的 raw SQL → 標記 destructive
- [ ] Unit test：`MigrationClassifier` 對含 `TRUNCATE` 的 raw SQL → 標記 destructive
- [ ] Integration test：`VulperonexDbContext` 可建立 + migrate
- [ ] Integration test：bootstrap 完成後執行 `PRAGMA auto_vacuum` → 回傳 `2`（FULL）；確認 pragma 已在 `MigrateAsync` 前設定

**依賴：** Task 3

**預計觸及檔案：**
- `src/Vulperonex.Infrastructure/Data/VulperonexDbContext.cs`
- `src/Vulperonex.Infrastructure/Migrations/` （自動生成）
- `src/Vulperonex.Infrastructure/Data/Configurations/` (EF config per entity)
- `tests/Vulperonex.Tests.Architecture/Migrations/MigrationClassifierTests.cs`
- `tests/Vulperonex.Tests.Integration/Infrastructure/`

**規模：** M

---

#### Task 6：TDQ（Transient Delivery Queue）+ At-Least-Once 保證

**描述：** 補全事件匯流排的溢出處理：Channel 超過閾值時寫入 SQLite `TransientDeliveryQueue` 表格；啟動時重播未處理項目；實作 `ActionExecutionLog` 表格與完整 dedup 協定（含 `Failed` 狀態、`AttemptCount`、永久失敗語意、`IClock` 抽象）。

**驗收標準：**
- [ ] 模擬 Channel 滿載時，事件寫入 TDQ 而非丟棄
- [ ] 啟動時 TDQ 內未處理事件被重播
- [ ] `ActionExecutionLog` dedup key：一般 actions = `(EventId, WorkflowRuleId, ActionIndex)`；`InvokeSubWorkflowAction` = `(EventId, WorkflowRuleId, ActionIndex, InvocationId)` — **`InvocationId` 必須在 action 執行前即產生並納入 TDQ payload**（不可每次執行時動態產生新 ULID），確保重播使用同一 InvocationId 維持 dedup 正確性
- [ ] `ActionExecutionLog` schema 含 `Status`（Pending/Completed/**Failed**）與 `AttemptCount`
- [ ] stale Pending（> 30s，threshold 透過 `IClock` 抽象注入）→ 重試，`AttemptCount++`；`AttemptCount >= MaxRetries+1` → `Status=Failed`（永久停止，後續重播跳過，不再重試）
- [ ] `Status=Completed` 或 `Status=Failed` 的 log entry → 重播時跳過

**驗證步驟：**
- [ ] Integration test：強制 Channel 滿 → 事件進 TDQ → 重啟 → 事件被重播
- [ ] Integration test：同一 key 重複執行 → 第二次被跳過（dedup，`Status=Completed`）
- [ ] Integration test（fake clock）：stale Pending 超過 30s threshold → 重試被觸發（`AttemptCount` 增加）
- [ ] Integration test：`AttemptCount` 達 `MaxRetries+1` → `Status=Failed` → 後續重播不再重試
- [ ] Unit test：`InvocationId` 預先持久化於 TDQ payload → 重播後讀回同一 id（不重新生成）

**依賴：** Task 4, Task 5

**預計觸及檔案：**
- `src/Vulperonex.Infrastructure/EventBus/TransientDeliveryQueue.cs`
- `src/Vulperonex.Infrastructure/EventBus/ActionExecutionLog.cs`
- `src/Vulperonex.Infrastructure/Migrations/` (TDQ + ActionExecutionLog 遷移)
- `tests/Vulperonex.Tests.Integration/EventBus/`

**規模：** M

---

#### Task 7：MemberResolver + PlatformUserDisplayCache（Infrastructure-only）

**描述：** 實作 `MemberResolver`（`INSERT OR IGNORE + SELECT` atomic GetOrCreate）與 `PlatformUserDisplayCache`（L1 in-memory LRU + L2 SQLite，`IPlatformUserInfoCache` 介面）。包含 `UserDisplayInfo` record 和 TTL 清理背景 worker。**注意：`PlatformUserDisplayCache` 屬於 Adapter Infrastructure 層，Application/Domain 不知道其存在（不注入至 MemberModule 等 Application 服務）；adapter 在事件回調中直接呼叫快取更新。**

**驗收標準：**
- [ ] 並行呼叫 `MemberResolver` 不產生重複 `MemberRecord`（ULID 唯一）
- [ ] L1 miss → L2 check → Platform API fetch 路徑正確
- [ ] L1 容量預設 500（hardcoded constant，本 Task 不依賴 Task 8；Task 8 完成後 `ISystemSettingsService` 可覆蓋容量/TTL — 連接點在 Task 8，不在本 Task）
- [ ] TTL 預設 24h，過期 rows 由 background worker 清除（TTL 同樣 hardcoded 至 Task 8）

**驗證步驟：**
- [ ] `dotnet test tests/Vulperonex.Tests.Integration` → SC-8 通過（`MemberId` 格式為 ULID）
- [ ] 並行測試：10 個 Task 同時解析同一 PlatformUser → 只建立 1 個 MemberRecord
- [ ] 單元測試（`IPlatformUserInfoCache.UpdateAsync` cache miss）：呼叫 `UpdateAsync` for non-existent user → 建立 default 快取 row（`Badges = Array.Empty<string>()`，所有 nullable 欄位 null，`FetchedAt = UtcNow`）；不拋例外

**依賴：** Task 5

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
- [ ] 訂閱者在設定變更後收到通知（無需重啟）
- [ ] OAuth refresh token 以 **AES-256-GCM** 加密存於 SQLite：versioned envelope `"v1:" + Base64(nonce(12B) || ciphertext || tag(16B))` 存入 `SystemSettings.Value TEXT`；key = machine.key raw 32 bytes（無 KDF）；setting key = `SystemSettingKey.OAuthTwitchRefreshToken` (`"oauth.twitch.refresh_token"`)；**AAD = 設定鍵名 UTF-8 bytes**（`"oauth.twitch.refresh_token"`），傳入 `AesGcm.Encrypt()`，解密時重新傳入；AAD 不存入 envelope，繫結密文至鍵名防止跨鍵複製攻擊
- [ ] 首次啟動時 OS app-data 路徑的 `machine.key` 不存在 → 自動建立（cryptographically random 32 bytes）；建立後立即設定限制性權限（Windows: 目前使用者 ACL FullControl，移除繼承；Unix: chmod 0600）；chmod/ACL 失敗 → 拋 `IOException`（fail-fast，不降級繼續）；路徑固定為 OS app-data root（Windows: `%AppData%\Vulperonex\`；macOS: `~/Library/Application Support/Vulperonex/`；Linux: `~/.local/share/Vulperonex/`）；不跟隨 `Database:Path` 自訂設定
- [ ] `machine.key` 遺失（如換裝置）→ AES 解密失敗 → 拋出 `CredentialDecryptionException`（呼叫端提示使用者重新授權，不 crash）

**驗證步驟：**
- [ ] 單元測試：設定 key 後立即讀取返回新值
- [ ] 單元測試：訂閱 Changes → SetAsync → subscriber 收到 SettingChangedEvent
- [ ] Integration test（temp dir）：`machine.key` 不存在 → `MachineKeyProvider` 建立 32-byte key 並設定 OS 限制性權限（Windows ACL user-only / Unix 0600）；此項為 integration test 以驗真實 ACL/chmod 行為（SPEC §7.2：Unit tests 無 I/O）。※ `MachineKeyProvider` 注入 `IFileSystem` 抽象供 unit tests 用 fake — 若使用 `System.IO.Abstractions` NuGet，需依 SPEC §8.2 ask-first 規則確認後再加入 csproj；亦可自訂輕量 `IFileSystem` port（2-3 methods）避免外部依賴
- [ ] Integration test（chmod 失敗模擬）：`machine.key` ACL/chmod 操作失敗 → `MachineKeyProvider` 拋 `IOException`（fail-fast）
- [ ] 單元測試（AAD cross-key copy attack）：直接呼叫底層加密基礎設施（`AesGcmEncryptor` 或等效 helper，繞過 `IOAuthTokenStore` 的 MVP platform 限制）：以 AAD=`"oauth.twitch.refresh_token"` 加密一段 token → 取得 envelope → 以 AAD=`"oauth.unknown.refresh_token"` 嘗試解密同一 envelope → 拋 `CredentialDecryptionException`（GCM authentication tag 驗證失敗，因 AAD 不同）。**不透過 `IOAuthTokenStore.GetRefreshTokenAsync("unknown")` 呼叫**（那會先拋 `ArgumentException`，測不到 AAD 失敗）
- [ ] 單元測試：以錯誤 key 解密 → 拋出 `CredentialDecryptionException`（不 crash，呼叫端可 catch）
- [ ] 單元測試：`StoreRefreshTokenAsync("twitch", "raw-token")` → `GetRefreshTokenAsync("twitch")` 回傳 `"raw-token"`（round-trip 正確，確認 platform 參數路由正確）
- [ ] 單元測試：`StoreRefreshTokenAsync("twitch", ...)` 寫入 `SystemSettingKey.OAuthTwitchRefreshToken`（`"oauth.twitch.refresh_token"`）key，且 `SystemSettings.Category = "oauth"`
- [ ] 單元測試：`StoreRefreshTokenAsync("twitch", "raw-token")` 後直接查詢 `SystemSettings.Value TEXT` → 不等於 `"raw-token"`（AES-256-GCM Base64 envelope 已套用，DB 不儲存明文）
- [ ] 單元測試：`IOAuthTokenStore.GetRefreshTokenAsync` 介面 contract — key 不存在 → 回 `null`；key 存在但 machine.key 錯誤 → 丟 `CredentialDecryptionException`（非 null、非 crash）
- [ ] 單元測試：竄改 DB 中 ciphertext 一個 byte 後呼叫 `GetRefreshTokenAsync` → 丟 `CredentialDecryptionException`（GCM authentication tag 驗證失敗）
- [ ] 單元測試：同一 raw token 連續呼叫 `StoreRefreshTokenAsync("twitch", "raw-token")` 兩次 → 兩次儲存的 `SystemSettings.Value` 字串不同（per-token random nonce 確認；若相同代表 nonce 固定，為安全漏洞）
- [ ] 單元測試：`StoreRefreshTokenAsync("unknown-platform", ...)` → 拋 `ArgumentException`（MVP 只允許 "twitch"）

**依賴：** Task 5

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
- [ ] 人工 review 架構層依賴

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

**依賴：** Task 4

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
- [ ] Integration test：publish event → WorkflowEngine → IPlatformChatSender mock 收到（使用 `InMemoryWorkflowRuleRepository` fake，不依賴 Task 14a 的 EF Core 實作）

**依賴：** Task 4, Task 5, Task 9

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
- [ ] 架構測試：`Plugins.Abstractions` 只依賴 Domain + Application
- [ ] 架構測試（reflection）：`IPluginContext` 和 `IPluginActionContext` 介面的所有 property type 均不含 `System.IServiceProvider`（reflection 掃描介面 members，確保 service locator 未透過 property 型別或巢狀 interface 洩漏）

**依賴：** Task 10

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
- [ ] OAuth PKCE code exchange（mock token endpoint）→ access token 只存 in-memory，不寫 DB 或 log；重啟後 `TwitchAdapter.StartAsync` 從 `IOAuthTokenStore.GetRefreshTokenAsync("twitch")` 讀取 refresh token 換取新 access token（refresh token 存在但 machine.key 遺失 → `CredentialDecryptionException` → 提示重新授權）
- [ ] OAuth refresh token 透過 `IOAuthTokenStore.StoreRefreshTokenAsync` 加密後持久化；`TwitchAdapter` 不直接呼叫 `ISystemSettingsService`，不自行加密（加密責任在 Task 8 的 `IOAuthTokenStore` 實作）
- [ ] 新增或更新 `appsettings.json` 範例，包含 `Auth:CallbackPort: 7979` 及 Twitch Redirect URI 設定說明

**驗證步驟：**
- [ ] `dotnet test` → SC-1, SC-6 WorkflowEngine half 通過
- [ ] 單元測試：IRC 訊息解析 → UserSentMessageEvent（Platform="twitch"，正確 User）
- [ ] 單元測試：OAuth PKCE `state` 不符、超過 10 分鐘 TTL 或已使用 → callback handler 拒絕並記 warning，不呼叫 token exchange endpoint（CSRF 防護驗証）
- [ ] 單元測試：OAuth callback listener 邊界 — 非 loopback 請求（模擬 RemoteIpAddress = 192.168.1.x）→ 忽略不處理；Host header 非 `localhost:{port}` / `127.0.0.1:{port}` / `[::1]:{port}` → 拒絕；remote IP 與 Host header 需同時通過；非預設 path（如 `/other`）→ 忽略；收到有效 callback 後 listener 關閉（single-use，驗第二次呼叫已無 listener）
- [ ] 單元測試：OAuth callback port 衝突 → 自動遞增至 7980
- [ ] 單元測試：7979、7980、7981 全部被占用 → OAuth flow 失敗（拋異常或回傳 error，不 hang）；應提示使用者手動在 Twitch Developer Console 更新 Redirect URI
- [ ] 單元測試：PKCE exchange mock → access token in-memory only（`ISystemSettingsService.SetAsync` 未以 access_token 類 key 被呼叫；mock logger sink 不含 access_token、authorization code、code_verifier 字串）
- [ ] 單元測試：PKCE exchange → `IOAuthTokenStore.StoreRefreshTokenAsync` 被呼叫，且傳入值 **等於** raw refresh token（加密責任在 OAuthTokenStore 實作內，Adapter 傳入明文；DB 持久化值不等於明文，由 Task 8 驗證）；mock logger sink 不含 raw refresh token 或 `refresh_token` plain value
- [ ] Integration test：TwitchAdapter `StartAsync` 後 `IStreamEventTypeRegistry` 含所有 Twitch EventTypeKeys（SC-1 所需 7 個）；`IsKnown("user.message")` = true；`IsKnownForWorkflow("platform.connection_changed")` = false（確認真實 adapter 而非 FakeTwitchEventTypeRegistrar 正確注冊全部 7 keys + system event）
- [ ] 單元測試：`TwitchAdapter.StartAsync` 時 `IOAuthTokenStore.GetRefreshTokenAsync("twitch")` 有值 → 呼叫 mock token refresh endpoint → access token in-memory 更新（重啟 refresh flow 驗証）
- [ ] 單元測試：`TwitchAdapter.StartAsync` 時 `GetRefreshTokenAsync` 拋 `CredentialDecryptionException` → catch 後提示重新授權，不 crash
- [ ] 單元測試（DisplayHints segment type allowlist）：Twitch IRC payload 含 HTML-like 內容（`<script>alert(1)</script>`）→ 產生的 `display.segments` 所有項目的 `type` 欄位只為 `text | emote | badge | mention`；`text` 片段值保留原始字串（含 `<`、`>`）不應被刪除或轉義 — 安全邊界在 type allowlist 與前端 `textContent` 渲染，不在 value 過濾
- [ ] 單元測試（DisplayHints `display.color` 格式）：Twitch IRC 帶合法顏色（`#FF4A4A`）→ `display.color` 值符合 `^#[0-9A-Fa-f]{6}$`；Twitch 回傳 3-digit shorthand、8-digit alpha、CSS named color（如 `red`）或空字串 → adapter 不填入 `display.color`（null / 省略，不帶非法值）
- [ ] 單元測試（fake clock + fake socket）：IRC WebSocket 斷線 → 第一次 base delay ≈ 1s，第二次 ≈ 2s，第三次 ≈ 4s（指數退避）；套用 jitter 後落在 ±20% 範圍；delay 不超過 60s
- [ ] 單元測試：斷線 → 立即 publish `PlatformConnectionChangedEvent { IsConnected: false, Reason: "reconnecting" }`，重連成功後 publish `{ IsConnected: true }`（兩個事件依序驗證）
- [ ] 單元測試（EventSub replay）：模擬 EventSub 斷線後在 10 分鐘窗口內重連，server 重播 2 個已錯過的事件 → 兩個事件均正常 publish 到 bus（adapter 不過濾 replay events）
- [ ] 單元測試（EventSub dedup）：同一 `(platform, sourceEventId)` 在 dedup cache 內重複送達時只 publish 一次，且 10 分鐘 TTL 到期後會釋放 cache entry
- [ ] 單元測試（EventSub replay 超時）：斷線超過 10 分鐘（fake clock）→ 重連後無 replay event → adapter 繼續正常運行（無 crash / deadlock），並記錄 warning log「events may have been lost」
- [ ] 單元測試（adapter cache update — subscribe）：publish `UserSubscribedEvent` → adapter 事件回調呼叫 `IPlatformUserInfoCache.UpdateAsync`，使 `IsSubscriber = true`（mock cache 驗証）
- [ ] 單元測試（adapter cache update — donate）：publish `UserDonatedEvent`（含 cumulative `TotalBitsGiven`）→ `IPlatformUserInfoCache.UpdateAsync` 被呼叫，快取 `TotalBitsGiven` 使用 `max(existing, incoming)` monotonic absolute replacement（非 `+= amount`）；重播同一事件 → `TotalBitsGiven` 不變；out-of-order 較小 incoming value 不覆蓋較大 existing value；若平台後台人工調整導致需要降低本地值，Phase 4 不自動回退，未來需走明確 admin reset
- [ ] 單元測試（adapter cache update — follow）：publish `UserFollowedEvent` → `IPlatformUserInfoCache.UpdateAsync` 被呼叫，follower badge 出現於 `Badges`（mock cache 驗証）
- [ ] 單元測試（badge normalization）：Twitch IRC `badges` tag 含重複項目（如 `subscriber/2000,subscriber/2000,vip`）→ 產生的 `user.badges` 去重後保留首次出現順序（`subscriber/2000,vip`）；badge ID 只含 `[A-Za-z0-9_/\-]` 字元；badge value 超過 64 字元會截斷或丟棄；badge 數量超過 20 → 截斷至前 20

**依賴：** Task 10, Task 9, Task 8, Task 7

**預計觸及檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/TwitchAdapter.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/OAuthCallbackListener.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/EventSub/TwitchEventSubClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Mapping/`
- `src/Adapters/Vulperonex.Adapters.Abstractions/IPlatformUserInfoCache.cs`
- （依賴 Task 8 新增的）`src/Vulperonex.Application/Auth/IOAuthTokenStore.cs`
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

**依賴：** Task 7, Task 10, Task 12

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
- [ ] CQRS 分離：`GET /api/rules` + `GET /api/rules/{id}` 呼叫 `IWorkflowRuleQueryService`；`POST /api/rules`、`PUT /api/rules/{id}`、`DELETE /api/rules/{id}`、`POST /api/rules/{id}/enable`、`POST /api/rules/{id}/disable` 呼叫 `IWorkflowRuleRepository`；兩組路徑不互用（以 fake `IWorkflowRuleRepository` / fake `IWorkflowRuleQueryService` 做 integration-level interaction tests 驗證：每個端點斷言呼叫了正確 port，且未呼叫另一個 port；不依賴靜態 assembly 掃描）
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

**依賴：** Task 10, Task 9, Task 5

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
- **Config**：`GET|PUT /api/config/{key}`（呼叫 `ISystemSettingsService`）。請求依序檢查：**(1) prefix denylist 先**（`security.*` → 403 `CONFIG_KEY_SECURITY_NAMESPACE`；`oauth.*` → 403 `OAUTH_CREDENTIAL_NAMESPACE`）；**(2) registry lookup**（不在 `SystemSettingKey` → 400 `UNKNOWN_CONFIG_KEY`）。prefix denylist 先於 registry — 未知的 `oauth.*` key 回 403 不回 400。
- **Member**：`GET /api/members`、`GET /api/members/{id}`（呼叫 `IMemberQueryService`）。
- `security.*` config key 在本端點封鎖（403 + `CONFIG_KEY_SECURITY_NAMESPACE`）；`/api/settings/security/*` 為保留路徑字首，MVP 不新增 CRUD 端點；Kestrel loopback-only binding 本身已保護這些路徑。

**驗收標準：**
- [ ] `POST /api/simulate/chat` → `ISimulationAdapter.SimulateAsync` 被呼叫
- [ ] `POST /api/simulate/follow` → `UserFollowedEvent` publish 驗證
- [ ] `POST /api/simulate/sub` → `UserSubscribedEvent` publish 驗證
- [ ] 不存在的 eventType → 400 + `UNKNOWN_SIMULATE_EVENT_TYPE`
- [ ] `GET /api/config/log.min_level` → 回傳目前設定值（happy path 使用非敏感 registered key）；未知 key → 400 + `UNKNOWN_CONFIG_KEY`
- [ ] `PUT /api/config/log.min_level` with value `"Warning"` → `ISystemSettingsService.SetAsync` 更新設定（happy path 使用非敏感 registered key `log.min_level`，避免測試誤用 protected key）
- [ ] `GET /api/config/security.someKey` → 403 + `CONFIG_KEY_SECURITY_NAMESPACE`
- [ ] `PUT /api/config/security.someKey` → 403 + `CONFIG_KEY_SECURITY_NAMESPACE`
- [ ] `GET /api/config/oauth.twitch.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE`
- [ ] `PUT /api/config/oauth.twitch.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE`
- [ ] `GET /api/members?platform=twitch&limit=20` → 回傳 JSON 列表（呼叫 `IMemberQueryService`）；`limit` 預設 50，上限 200；`offset` 支援；limit > 200 → 400 + `INVALID_QUERY_PARAM`
- [ ] `GET /api/members/{id}` 存在 → 回傳 member JSON
- [ ] `GET /api/members/{id}` 不存在 → 404 + `MEMBER_NOT_FOUND`

**驗證步驟：**
- [ ] Integration test：simulate / config / member 各一條 happy path + 一條 error path（member 測試 seed DB 直接插入 MemberRecord；不依賴事件驅動的 MemberModule）
- [ ] Integration test：`security.*` config key 封鎖（GET + PUT → 403 + `CONFIG_KEY_SECURITY_NAMESPACE`）
- [ ] Integration test：`oauth.*` config key 封鎖（GET + PUT → 403 + `OAUTH_CREDENTIAL_NAMESPACE`）
- [ ] Integration test：`GET /api/config/oauth.unknown.refresh_token`（未知 key，不在 registry）→ 403 + `OAUTH_CREDENTIAL_NAMESPACE`（prefix denylist 優先於 registry lookup，回 403 不回 400 `UNKNOWN_CONFIG_KEY`）

**依賴：** Task 14a, Task 9, Task 8, Task 7（Task 13 MemberModule 不要求；member endpoint 測試 seed DB 即可）

**預計觸及檔案：**
- `src/Hosts/Vulperonex.Web/Endpoints/SimulateEndpoints.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/ConfigEndpoints.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/MemberEndpoints.cs`
- `src/Hosts/Vulperonex.Web/Program.cs`
- `src/Vulperonex.Application/Members/IMemberQueryService.cs`（讀取端介面）
- `src/Vulperonex.Infrastructure/Members/MemberQueryService.cs`（IMemberQueryService 實作）
- `tests/Vulperonex.Tests.Integration/Web/`

**規模：** M

---

#### Task 15：SignalR Hub + Overlay Push + 雙埠 Kestrel

**描述：** 實作 `/hubs/events` SignalR Hub（管理頁面用）和 `/hubs/overlay/*` 各 Overlay Hub（OBS 用）。設定 Kestrel 雙埠（ApiPort 5000 / OverlayPort 5001），連接埠配對自動遞增邏輯，localhost-only 預設。SC-5 端到端測試。

**驗收標準：**
- [ ] SC-5 通過：publish UserSentMessageEvent → `/overlay/chat` client 5 秒內收到 OverlayChatPayload
- [ ] Overlay port 5001 不需驗證即可連線
- [ ] Phase 5 implementation 前重評 synthetic `eventId` 去重語意：platform-provided id 可跨 overlay client 識別同一事件；adapter fallback ULID 只保證本機單實例 delivery id。
- [ ] 埠衝突時自動嘗試 +2 pair（最多到 5008/5009），全部失敗時 `PortPairAllocator.TryAllocate()` 回傳 `null`（不 throw）；呼叫端（Web host 啟動邏輯）檢查 null → 拋 `PortExhaustedException`（含清晰錯誤訊息）；Photino dialog 由 Task 21 catch 並顯示
- [ ] 兩埠以 `IPAddress.Loopback`（IPv4）+ `IPAddress.IPv6Loopback`（IPv6）雙重繫結（Kestrel 設定驗証；socket bind test 確認非 loopback 連線被拒絕）

**驗證步驟：**
- [ ] `dotnet test` → SC-5 通過（5s 逾時）
- [ ] 單元測試（使用 `IPortAvailabilityProbe` fake，不開真實 socket）：`PortPairAllocator` — fake probe 回報全部 5 對均佔用 → allocator 回傳 null（exhaustion）
- [ ] 單元測試：fake probe 回報 5001 佔用（5000 閒置）→ allocator 跳過 5000/5001 pair，選 5002/5003
- [ ] 單元測試（host-level）：Web host 啟動邏輯（`WebHostPortBootstrapper` 或等效類）— `PortPairAllocator.TryAllocate()` 回傳 null → 啟動邏輯拋 `PortExhaustedException`（含清晰錯誤訊息）；不依賴真實 socket，mock allocator
- [ ] Integration test：`PlatformConnectionChangedEvent` publish to bus → `/hubs/events` SignalR subscribers 收到該事件（UI 連線狀態指示器所需；task 依 SignalR hub 轉寄所有 `IStreamEvent` to management group）
- [ ] Integration test：`UserSentMessageEvent` 經 SignalR hub 發至 `/overlay/chat` → 序列化 JSON key set **精確等於** `{schemaVersion, eventId, timestamp, displayName, colorHex, segments, badges}`（exact match，非僅 denylist），且 `eventId` 優先沿用 platform-provided id，缺值才使用 synthetic ULID
- [ ] Integration test：`UserFollowedEvent` 或 `UserSubscribedEvent` → `/overlay/alerts` SignalR payload JSON key set **精確等於** `{schemaVersion, eventId, timestamp, displayName, eventType, tier}`；不含 `memberId`、`platformUserId`
- [ ] Integration test：SignalR client 連線 `/hubs/overlay/member` group → 連線成功不 crash（group registered as MVP skeleton；server 不 publish 任何 MVP event 至此 group；post-MVP `SystemEvent` 才會驅動它）
- [ ] 手動：OBS browser source 連線 localhost:5001/overlay/chat

**依賴：** Task 13, Task 14a

**預計觸及檔案：**
- `src/Hosts/Vulperonex.Web/Hubs/EventHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayChatHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayAlertsHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayMemberHub.cs`
- `src/Hosts/Vulperonex.Web/Infrastructure/PortPairAllocator.cs`
- `tests/Vulperonex.Tests.Integration/Web/OverlayHubTests.cs`

**規模：** M

---

#### Task 16：CLI — simulate / config / member / rule 指令

**描述：** 實作 `Vulperonex.Cli` 所有 MVP 指令（SPEC 5. Commands）：`simulate chat|follow|sub`、`config get|set`、`member list|show`、`rule list|show|enable|disable|delete`。CLI 透過 HTTP 呼叫 REST API（不直接存取 DB）。伺服器永遠 loopback-only 無需身分驗證，CLI 不需 API key bootstrap。REST 錯誤碼統一映射規則：4xx 回應 → `stderr` 印出 `error` 欄位值 + exit code 1；`WORKFLOW_RULE_NOT_FOUND`/`MEMBER_NOT_FOUND` 等 not-found 錯誤不另行包裝，直接輸出錯誤碼讓上層工具解析。

**DB 路徑解析（CLI 與 Web host 共同規則）：** 讀取 `appsettings.json → Database:Path`（若存在），否則使用 OS app-data 預設路徑（見 SPEC §4.11）。**`Database:Path` 不允許透過 `appsettings.{Environment}.json` 或環境變數覆蓋** — Web host 與 CLI 均只讀主要 `appsettings.json`，確保兩者永遠讀相同 DB。

**驗收標準：**
- [ ] `vulperonex simulate chat --user alice --message "hi"` → `UserSentMessageEvent` publish → 預先建立的 SendChatMessage rule 觸發 → `IPlatformChatSender.SendAsync` 收到呼叫（integration test 中 mock sender 驗證）
- [ ] `vulperonex simulate follow --user bob` → `UserFollowedEvent` publish 驗證
- [ ] `vulperonex simulate sub --user carol --tier 1000` → `UserSubscribedEvent` publish 驗證
- [ ] `vulperonex config get log.min_level` → 回傳目前設定值 JSON
- [ ] `vulperonex config set log.min_level Warning` → SystemSettings 更新，熱重載生效
- [ ] `vulperonex member list --platform twitch --limit 20` → 回傳 JSON 列表
- [ ] `vulperonex member show <memberId>` → 回傳單一 member JSON；不存在 → exit code 1 + `MEMBER_NOT_FOUND`
- [ ] `vulperonex rule list` → 回傳所有 rule 的 `WorkflowRuleSummaryDto` JSON 列表（按 `Priority ASC, CreatedAt ASC, Id ASC` 排序）；MVP 不分頁，所有 rule 一次回傳
- [ ] `vulperonex rule show <id>` → 回傳單一 rule 詳細 JSON；不存在 → exit code 1 + `WORKFLOW_RULE_NOT_FOUND`
- [ ] `vulperonex rule enable <id>` → rule `IsEnabled=true`，DB 更新驗證
- [ ] `vulperonex rule disable <id>` → rule `IsEnabled=false`，DB 更新驗證
- [ ] `vulperonex rule delete <id>` → rule 從 DB 移除；不存在 → exit code 1 + `WORKFLOW_RULE_NOT_FOUND`
**驗證步驟：**
- [ ] Integration test：模擬 CLI args → HTTP → API → DB 驗證（Web host loopback-only，無需 API key）
- [ ] Integration test（protected namespace passthrough）：CLI `config get oauth.twitch.refresh_token` → API 回 403 `OAUTH_CREDENTIAL_NAMESPACE` → CLI **stderr** 輸出 `OAUTH_CREDENTIAL_NAMESPACE` + exit code 1（4xx/5xx 錯誤均輸出至 stderr；stdout 只輸出成功結果）
- [ ] Integration test：CLI `config set security.someKey value` → API 回 403 `CONFIG_KEY_SECURITY_NAMESPACE` → CLI **stderr** 輸出 error code + exit code 1（REST error 碼直接 pass-through）
- [ ] Integration test：CLI `config set oauth.twitch.refresh_token value` → API 回 403 `OAUTH_CREDENTIAL_NAMESPACE` → CLI **stderr** 輸出 error code + exit code 1
- [ ] **CLI 輸出規則（統一）：** 成功結果輸出至 stdout；4xx/5xx 錯誤代碼輸出至 stderr + exit code 1；exit code 0 = 成功（不含 handler failure 語意）
- [ ] Integration test（DB path env override 禁止）：同一 temp SQLite，`ASPNETCORE_ENVIRONMENT=Development` + `appsettings.Development.json` 含不同 `Database:Path` → Web host 仍使用主 `appsettings.json` 的路徑（不受 environment-specific override 影響）
- [ ] 手動：`dotnet run --project src/Hosts/Vulperonex.Cli -- simulate chat --user test --message "hello"`

**依賴：** Task 14b, Task 15（Task 14b 提供 simulate/config/member 端點；Task 15 完成雙埠 Kestrel Program.cs）

**預計觸及檔案：**
- `src/Hosts/Vulperonex.Cli/Program.cs`
- `src/Hosts/Vulperonex.Cli/Commands/`
- `tests/Vulperonex.Tests.Integration/Cli/`

**規模：** M

---


### Checkpoint：Phase 5

- [ ] `dotnet test` → SC-2, SC-5, SC-8, SC-9 全通過
- [ ] WorkflowRule CRUD 端到端（in-memory SQLite）通過
- [ ] `GET/PUT /api/config/security.*` → 403 通過
- [ ] CLI rule list/show/enable/disable/delete integration test 通過
- [ ] CLI simulate chat（含 fixture rule + mock sender 驗證）通過
- [ ] CLI simulate → SignalR overlay 端到端手動測試通過
- [ ] Task 15：兩埠均以 loopback（IPv4 127.0.0.1 + IPv6 ::1）雙重繫結，socket bind test 驗證通過
- [ ] Task 14b：`GET/PUT /api/config/oauth.twitch.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE` 通過
- [ ] Task 14b：`GET /api/config/oauth.unknown.refresh_token`（未知 key）→ 403 + `OAUTH_CREDENTIAL_NAMESPACE`（prefix denylist 先於 registry lookup）通過
- [ ] Task 16：CLI `config get oauth.twitch.refresh_token` → stderr 輸出 `OAUTH_CREDENTIAL_NAMESPACE` 錯誤，exit code ≠ 0（CLI passthrough 通過）
- [ ] Task 16：`ASPNETCORE_ENVIRONMENT=Development` + `appsettings.Development.json` 不覆蓋 `Database:Path` 通過

---

### Phase 6：Serilog + 日誌 + 前端 Vue SPA

> 詳細切片計畫：`docs/phases/phase-6-web-ui/plan.md`

> **Task 17：已移除** — 原 MockYouTube Adapter，推遲出 MVP scope（對應 todo.md SC-7 removed）。Task 18 直接接續 Task 16。

#### Task 18：Serilog 三 Sink + AppLogs 表格

**描述：** 設定 Serilog（Console + Rolling File + SQLite AppLogs），加結構化欄位 enricher（EventTypeKey、Platform、MemberId、WorkflowRuleId、ActionType）。AppLogs 背景清理 worker（`log.db_retention_days` 時間清除 + `log.db_max_size_mb` 大小清除，以先觸發者為準）。`log.min_level` 熱重載。

**驗收標準：**
- [ ] 三個 Sink 均運作（Console dev / File daily / SQLite）
- [ ] `ForContext` 欄位正確寫入 AppLogs 表格
- [ ] 修改 `log.min_level` via SystemSettings → 立即生效，無需重啟
- [ ] 超過 `log.db_max_size_mb` → 自動刪除最舊 rows；size-based cleanup 後 worker **明確執行 `PRAGMA VACUUM`**（讓 auto_vacuum 釋放頁面回 OS，縮減實體 DB 大小）；大小估算以 `PRAGMA page_count * page_size` 計算，不依賴實體 `FileInfo.Length`；`PRAGMA auto_vacuum = FULL` 已在 Task 5 DB bootstrap 設定，本 Task 不重複設定

**驗證步驟：**
- [ ] Integration test：publish event → AppLogs 含 EventTypeKey 欄位
- [ ] Integration test：SetAsync log.min_level=Warning → Debug 日誌不再寫入
- [ ] Integration test（size-based cleanup）：temp SQLite DB（`auto_vacuum = FULL` 已在 Task 5 bootstrap 設定）；設定 `log.db_max_size_mb = 1`（1MB 測試閾值）；插入足夠多 AppLogs rows 超過 1MB → 呼叫 `AppLogsCleanupWorker.ExecuteOnce()`（或等效單次觸發）→ **VACUUM 後**再查 `PRAGMA page_count * page_size` 低於 1MB × 1.05（5% 容差，允許 SQLite metadata overhead）；測試不依賴 FileInfo.Length

**依賴：** Task 8, Task 5

**預計觸及檔案：**
- `src/Hosts/Vulperonex.Web/Logging/SerilogSetup.cs`
- `src/Vulperonex.Infrastructure/Logging/AppLogsCleanupWorker.cs`
- `tests/Vulperonex.Tests.Integration/Logging/`

**規模：** S

---

#### Task 19：Vue 前端骨架 — Vite + PrimeVue + Pinia + SignalR composable

**描述：** 建立 `src/frontend/` Vue 3.5 SPA：Vite 7.3 設定、PrimeVue 4 Unstyled + UnoCSS Preset Wind 4、Pinia store 骨架、`useStreamEvents` composable（SPEC 6.4）、Overlay 頁面路由（`/overlay/chat`、`/overlay/alerts`、`/overlay/member`）。`pnpm build` 輸出至 `Web/wwwroot`。**注意：** `/overlay/member` 為 MVP skeleton — 頁面建立 SignalR 連線但不收取任何事件（`SystemEvent` 為 post-MVP）；應顯示空狀態 UI，不期待資料到來。

**驗收標準：**
- [ ] `pnpm dev` 啟動 Vite dev server 無錯誤
- [ ] `pnpm build` 輸出至 `../Hosts/Vulperonex.Web/wwwroot`
- [ ] `useStreamEvents` composable 連線 `/hubs/events`，收到事件後 `events.value` 更新
- [ ] `/overlay/chat` 路由掛載時連線對應 SignalR group
- [ ] TypeScript 無型別錯誤（`tsc --noEmit`）
- [ ] `package.json` 含 `"packageManager": "pnpm@9.15.4"`（精確版本釘定，`pnpm --version` 回傳 `9.15.4`；不使用 `9.x.x` 萬用版本）
- [ ] `package.json` 含 `"lint": "oxlint --config oxlint.json"` script；`oxlint.json` 含 Vue 3 + TypeScript rule set；`pnpm lint` 在 initial 骨架上無錯誤（oxlint 為指定 linter，不使用 ESLint）；**首次新增 oxlint npm package 前需先詢問**（SPEC §8.2 — 新增依賴需 ask-first）；oxlint **已安裝後**直接執行 `pnpm lint` 是驗證步驟，不需再詢問

**驗證步驟：**
- [ ] `pnpm test` → composable 單元測試通過
- [ ] `pnpm build` → wwwroot 有 index.html + assets
- [ ] 手動：瀏覽器開 `localhost:5001/overlay/chat` → 頁面可連線 SignalR（Overlay port 為 5001，非 API port 5000）
- [ ] Vitest：`/overlay/alerts` 路由掛載不 crash、連線 SignalR group
- [ ] Vitest：`/overlay/member` 路由掛載 → 顯示 skeleton/空狀態 UI（無資料、無錯誤；確認 post-MVP 頁面不因未收到事件而 crash）
- [ ] Vitest（textContent 渲染安全）：將含 `<script>alert(1)</script>` 的 `text` 片段傳入 ChatOverlay 元件 → 渲染後 DOM 中存在文字節點「`<script>alert(1)</script>`」但不存在 `<script>` 元素（驗 `textContent` 而非 `innerHTML`；確認 XSS 邊界在前端渲染層）

**依賴：** Task 15

**預計觸及檔案：**
- `src/frontend/vite.config.ts`
- `src/frontend/src/main.ts`
- `src/frontend/src/composables/useStreamEvents.ts`
- `src/frontend/src/views/overlay/` (ChatOverlay.vue, AlertsOverlay.vue, MemberOverlay.vue)
- `src/frontend/src/stores/`
- `src/frontend/tests/`

**規模：** M

---

#### Task 20：WorkflowRule UI — 規則建立表單 + 事件類型 Dropdown

**描述：** 實作 Vue 規則編輯器：事件類型 dropdown（從 API 取已注冊 keys）、Condition 建立（UserRole、MessageContent、Cooldown）、Action 建立（SendChatMessage、InvokePlugin）。Axios 呼叫 REST API。i18n 錯誤碼對應。

**驗收標準：**
- [ ] EventTypeKey dropdown 只顯示 `GET /api/event-types` 回傳的已注冊 keys；`platform.connection_changed`（系統事件）**不出現**在 dropdown 中（`IStreamEventTypeRegistry` 以 `isSystemEvent: true` 注冊此 key；`GetAll()` 排除所有 `isSystemEvent=true` 的 key；`GET /api/event-types` endpoint 因此不含此 key）；REST simulate 只公開 `chat/follow/sub` 三個 alias，dropdown 應以 badge / tooltip 標示哪些 keys 可用 `POST /api/simulate/*` 觸發（防止手動測試選 `donated/raided/reward.redeemed` 後誤判功能壞了）
- [ ] 填寫並送出表單 → `POST /api/rules` → 規則出現在列表
- [ ] API 回 400 `UNKNOWN_EVENT_TYPE_KEY` → UI 顯示在地化錯誤訊息（**MVP 只支援 zh-TW**；locale 寫死為 `zh-TW`；vue-i18n fallback locale 設為 `zh-TW`，不存在的 key 顯示 key 名稱而非 crash）
- [ ] API 回 400 `CIRCULAR_WORKFLOW_REFERENCE` → UI 顯示在地化錯誤訊息
- [ ] API 回 400 `ACTION_MISSING_REQUIRED_PARAM` → UI 顯示在地化錯誤訊息
- [ ] `src/frontend/src/i18n/zh-TW.ts` 包含 SPEC §11.OQ4 列出的**所有** MVP error codes（含 `OAUTH_CREDENTIAL_NAMESPACE`、`INVALID_REGEX_PATTERN`、`INVALID_QUERY_PARAM`、`UNKNOWN_CONDITION_TYPE`、`INVALID_RULE_ID_MISMATCH`）；Vitest 從本地 `errorCodes.ts` 常數列表逐一驗 zh-TW key 存在且非空字串（`errorCodes.ts` 需與 SPEC OQ4 表格完整同步，否則 CI 前會漏掉新 code）
- [ ] `pnpm test` → UI 元件測試通過

**驗證步驟：**
- [ ] Vitest + Vue Test Utils：表單提交 mock API → 成功回調
- [ ] Vitest：EventTypeKey dropdown simulate badge — `user.message`、`user.followed`、`user.subscribed` 渲染含 simulate badge；`user.donated`、`user.gifted_sub`、`channel.raided`、`reward.redeemed` 不含 simulate badge（覆蓋全部 3 可模擬 + 4 不可模擬 alias map）
- [ ] Integration test（Web host level）：`FakeTwitchEventTypeRegistrar`（只呼叫 `_registry.Register(...)` for all Twitch keys，不啟動 OAuth/WebSocket）+ SimulationAdapter 均 StartAsync 後，`GET /api/event-types` 包含全部 Twitch + Simulation canonical keys；**每個 key 只出現一次**；**`platform.connection_changed` 不出現**（使用 Fake 避免 OAuth/socket 副作用）
- [ ] Vitest：simulate badge flow — 建立 `user.followed` rule，點擊 follow badge → `POST /api/simulate/follow` mock 被呼叫；建立 `user.subscribed` rule，點擊 sub badge → `POST /api/simulate/sub` mock 被呼叫（驗 badge → simulate alias 對應正確）
- [ ] 手動：全流程測試（建立 `user.message` rule → `POST /api/simulate/chat` → Overlay 顯示）；手動驗收固定使用 `chat/follow/sub` 三個可 simulate alias，不選 `donated/raided/reward.redeemed`（REST 不支援）

**依賴：** Task 19, Task 14a, Task 14b, Task 9（`FakeTwitchEventTypeRegistrar` 定義於 Task 9，供 `GET /api/event-types` integration test 使用，不需啟動真實 OAuth/socket）；手動全流程驗收仍需 Task 12（提供真實 TwitchAdapter EventTypeKey 注冊）

**預計觸及檔案：**
- `src/frontend/src/views/rules/RuleEditor.vue`
- `src/frontend/src/components/rule/`
- `src/frontend/src/i18n/`
- `src/frontend/tests/rules/`

**規模：** M

---

#### Task 21：Photino 桌面殼 + 埠衝突處理 + 靜態 fallback

**描述：** 實作 `Vulperonex.Desktop`：Photino window 包裝 Web host、埠衝突自動嘗試 pair（5000/5001 → 5008/5009）、Web host crash → 顯示嵌入靜態 fallback HTML（含 Restart 按鈕）、DB migration 失敗 → Photino dialog。

**驗收標準：**
- [ ] `dotnet run --project src/Hosts/Vulperonex.Desktop` 開啟 Photino 視窗載入 Vue UI
- [ ] 模擬埠 5000 被佔用 → 自動切換到 5002/5003
- [ ] 模擬埠 5001 被佔用（5000 閒置）→ 仍跳到 5002/5003（pair 必須同時閒置才能使用）
- [ ] 所有埠對（5000/5001 → 5008/5009）用盡 → Photino dialog 提示
- [ ] migration 失敗 → dialog 含 [Open log folder] + [Exit] 按鈕
- [ ] 啟動時偵測 WebView2 是否安裝；缺失則顯示含下載連結的 dialog（支援 Windows 10 1809+）
- [ ] `Vulperonex.Desktop.csproj` 標注 `<TargetFramework>net10.0-windows</TargetFramework>`

**驗證步驟：**
- [ ] `PortPairAllocator` 單元測試已在 Task 15 補齊；本 Task 只補 Photino 整合驗証
- [ ] 單元測試（mock `IWebView2Detector`）：detector 回報 WebView2 未安裝 → dialog callback 被觸發（含下載連結字串）；不依賴真實 WebView2 環境
- [ ] 單元測試（mock Web host）：Web host crash（mock 拋 `InvalidOperationException`）→ Photino shell 顯示嵌入 fallback HTML（含 Restart 按鈕字串）；Restart 按鈕觸發 → Web host 重啟嘗試被呼叫（mock 驗証）
- [ ] 單元測試（mock migration）：EF Core migration 拋異常 → Photino dialog callback 被觸發，包含 [Open log folder] + [Exit] 按鈕字串；不依賴真實 DB
- [ ] 手動：佔用 5000（5001 閒置）→ 確認 app 切換到 5002/5003（pair 必須同時閒置）
- [ ] 手動：同時佔用 5001（5000 閒置）→ 確認 app 仍切換到 5002/5003（pair 必須同時閒置）
- [ ] 手動：佔用全部 5 對（5000-5009）→ Photino dialog 顯示「無可用埠」提示
- [ ] 手動（Web host crash）：啟動後強制終止 Web host 進程（或透過 debug hook 模擬 crash）→ Photino window 切換至 fallback HTML，顯示 Restart 按鈕；點擊 Restart → Web host 重啟嘗試（UI 恢復或再次顯示 fallback）
- [ ] 手動：app 啟動載入 UI，基本功能可用

**依賴：** Task 15, Task 19

**預計觸及檔案：**
- `src/Hosts/Vulperonex.Desktop/Program.cs`（引用 `PortPairAllocator`，實作在 Task 15 的 `Vulperonex.Web`）
- `src/Hosts/Vulperonex.Desktop/Resources/fallback.html`

**規模：** S

---

### Checkpoint：Phase 6（最終）

- [ ] `dotnet test` → 所有 active SC 通過（SC-1~SC-6, SC-8~SC-10；SC-7 removed）
- [ ] `pnpm test` → 前端測試全通過
- [ ] `pnpm lint` → 前端 lint 無錯誤
- [ ] `pnpm build` → wwwroot 建置成功
- [ ] Photino 視窗手動測試：模擬 chat → Overlay 顯示
- [ ] 人工 review 安全性（Overlay DTO 精確白名單（含 SignalR JSON 序列化驗証）、兩埠以 `IPAddress.Loopback` + `IPAddress.IPv6Loopback` 雙繫結（socket bind test 驗証）、AES-256-GCM token 加密（含 tamper test + AAD binding）、machine.key 檔案權限（Windows ACL / Unix 0600）、`GET/PUT /api/config/oauth.twitch.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE`、**未知 `oauth.*` key（如 `oauth.unknown.refresh_token`）→ 403 + `OAUTH_CREDENTIAL_NAMESPACE`（prefix denylist 先於 registry，不回 400）**、**refresh token envelope 使用標準 Base64（含 `+`/`/`/`=`），解碼用 `Convert.FromBase64String` 而非 `WebEncoders.Base64UrlDecode`**、`config set security.*`/`config set oauth.*` → 403 protected namespace write denial、**OAuth `state` 參數 CSRF 驗證：state 不符、超過 10 分鐘 TTL 或已使用 → 拒絕不 exchange code**、**OAuth callback listener：loopback-only（127.0.0.1 / ::1）+ Host header allowlist，兩者皆需通過 + 只接受預設 callback path + single-use**、**OAuth logger scrub list 包含 access token、authorization code、code_verifier、raw refresh token**、**plugin context 不暴露 `IServiceProvider`（reflection 架構測試通過 + PR code review 確認）**）

---

## 風險與緩解

| 風險 | 影響 | 緩解 |
|------|------|------|
| Twitch EventSub WebSocket API 頻繁變動 | 高 | 用 mock HTTP handler 隔離 TwitchAdapter 測試；real integration 在 CI 中為可選 |
| EF Core JSON mapping 在 SQLite 的限制 | 中 | Task 5 早期驗證 JSON column query；備案：手動序列化 |
| Photino 3.x Windows 相容性 | 中 | Task 21 排在最後；先確保 Web host 正常運作 |
| SignalR 在 OBS Browser Source 的限制 | 中 | Task 15 盡早手動測試 OBS 連線 |
| WorkflowEngine 並行 + dedup 複雜度 | 高 | Task 6 + Task 10 均有專屬 integration test；TDQ replay 測試強制模擬 crash 場景 |

## 開放問題（已解答）

- **Q1 ✅：Twitch OAuth PKCE callback port 需要可設定。**
  - 實作：Kestrel 在 OAuth flow 期間臨時監聽 `appsettings.json → Auth:CallbackPort`（預設 7979）。
  - 若 7979 被佔用 → 嘗試 7980, 7981（最多 3 次）→ 全失則顯示 dialog 要求手動設定。
  - Twitch Developer Console 需將 `http://localhost:7979/auth/callback`（及備用埠）列為 Redirect URI。
  - **Task 12 補充：** `appsettings.json` 加 `Auth:CallbackPort: 7979`；TwitchAdapter OAuth handler 使用此設定。

- **Q2 ✅：pnpm 精確版本鎖定**（`package.json` 加 `"packageManager": "pnpm@9.15.4"`；Task 19 驗收要求精確版本，不接受 `pnpm@9` 或 `pnpm@9.x.x` 模糊字串）

- **Q3 ✅：Windows 10 最低支援**（Photino 3.x 需 WebView2，Windows 10 1809+）
  - Desktop app 啟動時偵測 WebView2 是否安裝，缺失則顯示下載連結 dialog。
  - `Vulperonex.Desktop` 專案標注 `<TargetFramework>net10.0-windows</TargetFramework>`。
