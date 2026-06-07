# 第二階段詳細計畫：事件匯流排 + Infrastructure

> 父計畫：`tasks/plan.md` 第二階段
> 範圍：僅限任務 4-8
> 目標：建立事件匯流排、SQLite/EF Core 基礎架構、TDQ at-least-once 交付、會員解析與系統設定服務，讓後續 Simulation、Workflow、Web Host 與 CLI 可以建立在穩定的 Application ports 與 Infrastructure 實作上。

---

## 執行規則

- 每個切片使用一個小分支開發，驗證後立即提交，合併回 `main` 時使用 `git merge --ff-only`。
- 每個行為需求先寫 BDD-style Given / When / Then scenario，再以 TDD RED / GREEN / REFACTOR 實作。
- Application 邊界遵守 light CQRS：commands/write ports 與 queries/read services 分離；Infrastructure 只實作 Application/Adapter ports，不把 EF Core 型別洩漏到 Domain/Application。
- 未經事先詢問批准，請勿新增新的 NuGet 套件。若 Task 5 需要新增 EF Core SQLite / Design / Tools 套件，先確認目前 `Directory.Packages.props` 是否已存在；若不存在或需新增版本，先詢問。
- `--no-build` 只可緊接在同一任務中成功編譯後使用。
- 保持 `.claude/`、DB 檔、測試輸出與其他本地檔案不進入提交。
- Phase 2 不修改核心 Domain event shape；若發現 Phase 1 port/DTO 不足，先以最小 Application contract 補齊並保留架構測試。

---

## 相依順序

```
任務 4a IStreamEventBus contract
    -> 任務 4b InMemoryStreamEventBus dispatch
    -> 任務 4c WaitForIdleAsync 與測試穩定性

任務 5a EF Core/SQLite 套件與 DbContext
    -> 任務 5b InitialSchema 與配置
    -> 任務 5c DB bootstrap 與 migration classifier

任務 4c + 任務 5c
    -> 任務 6a TDQ schema / repository
    -> 任務 6b overflow / replay
    -> 任務 6c ActionExecutionLog dedup / IClock

任務 5c
    -> 任務 7a IMemberResolver port 與 atomic resolver
    -> 任務 7b PlatformUserDisplayCache L1/L2
    -> 任務 7c display cache TTL cleanup
    -> 任務 8a SystemSettings service
    -> 任務 8c OAuth token encryption / machine.key

任務 4c + 任務 7c + 任務 8a
    -> 任務 8b settings hot reload + Task 4/7 runtime setting wiring
```

---

## 任務 4a：定義 IStreamEventBus 契約

**描述：** 在 Application 層建立事件匯流排 contract，明確定義 publish、subscribe 與測試用 idle 等待語意。

**驗收準則：**
- [ ] `IStreamEventBus` 位於 `Vulperonex.Application`。
- [ ] `PublishAsync(IStreamEvent, CancellationToken)` 表達 fire-and-forget 入隊語意。
- [ ] `IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task>)` 支援 `TEvent : IStreamEvent`，回傳的 subscription 供模組 `StopAsync` 清理。
- [ ] `WaitForIdleAsync(CancellationToken)` 僅作為測試/驗證 hook，不向 caller 暴露 handler error count。

**驗證：**
- [ ] Application 專案可編譯。
- [ ] 架構測試確認 Application 不引用 Infrastructure。

**相依：** Task 2

**可能涉及的檔案：**
- `src/Vulperonex.Application/EventBus/IStreamEventBus.cs`
- `tests/Vulperonex.Tests.Architecture/Dependencies/LayerDependencyTests.cs`

**預估規模：** S

---

## 任務 4b：實作 InMemoryStreamEventBus dispatch

**描述：** 在 Infrastructure 層以 `Channel<IStreamEvent>` 實作事件匯流排，包含 assignable match、handler 例外隔離與 10,000 預設容量。

**驗收準則：**
- [ ] `PublishAsync` 入隊後快速返回，不等待 handler 完成。
- [ ] `Subscribe<IStreamEvent>` 可收到所有事件；`Subscribe<UserSentMessageEvent>` 只收到該具體型別。
- [ ] 單一 handler 拋例外不影響其他 handler。
- [ ] module 不關心的事件型別由訂閱者自行 no-op；bus 不把 unknown event 當錯誤。
- [ ] Channel 容量預設為 10,000，後續 Task 8 才改為設定覆寫。

**驗證：**
- [ ] 單元測試：publish 5 個事件，其中一個 handler throw，其餘 handler 仍收到。
- [ ] 單元測試：`Subscribe<IStreamEvent>` 收到 concrete event。
- [ ] 單元測試：`Subscribe<UserSentMessageEvent>` 不收到 `UserFollowedEvent`。
- [ ] 單元測試：handler `Task.Delay(100ms)` 時，`PublishAsync` 在 < 10ms 返回。

**相依：** 任務 4a

**可能涉及的檔案：**
- `src/Vulperonex.Infrastructure/EventBus/InMemoryStreamEventBus.cs`
- `tests/Vulperonex.Tests.Unit/Infrastructure/EventBus/InMemoryStreamEventBusTests.cs`

**預估規模：** M

---

## 任務 4c：穩定 WaitForIdleAsync 與 dispatch lifecycle

**描述：** 補齊測試所需的 idle 偵測，確保佇列清空且所有 handler 完成後才 resolve，且 handler 例外只記錄不外拋。

**驗收準則：**
- [ ] `WaitForIdleAsync` 在佇列清空且所有已派發 handler 完成後 resolve。
- [ ] handler exception 被 catch + log，不透過 `WaitForIdleAsync` 丟出。
- [ ] cancellation token 可中止等待。
- [ ] 測試不相依固定 sleep；使用 idle hook 或 deterministic synchronization。

**驗證：**
- [ ] 單元測試：publish 後立即 `WaitForIdleAsync`，handler 完成才 resolve。
- [ ] 單元測試：handler throw 後 `WaitForIdleAsync` 仍 resolve。
- [ ] 單元測試：取消 token 時等待結束並回報 cancellation。

**相依：** 任務 4b

**可能涉及的檔案：**
- `src/Vulperonex.Infrastructure/EventBus/InMemoryStreamEventBus.cs`
- `tests/Vulperonex.Tests.Unit/Infrastructure/EventBus/InMemoryStreamEventBusTests.cs`

**預估規模：** S

---

## 任務 5a：建立 EF Core / SQLite 基礎與 DbContext

**描述：** 建立 `VulperonexDbContext` 與 Infrastructure data layer 起點，先讓測試可用 SQLite fixture 建立資料庫。

**驗收準則：**
- [ ] `VulperonexDbContext` 位於 Infrastructure。
- [ ] DbContext 暴露 Phase 2 需要的 DbSet 或配置起點：Members、PlatformIdentities、WorkflowRules、SystemSettings、AppLogs、PlatformUserDisplayInfo。
- [ ] EF Core 型別不外洩至 Domain/Application contracts。
- [ ] SQLite 測試 fixture 可建立 transient database。

**驗證：**
- [ ] Integration test：DbContext 可在 temp SQLite 開啟並建立連線。
- [ ] 架構測試：Application 不引用 EF Core provider / Infrastructure。

**相依：** Task 3

**可能涉及的檔案：**
- `src/Vulperonex.Infrastructure/Data/VulperonexDbContext.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/`
- `tests/Vulperonex.Tests.Integration/Infrastructure/SqliteFixture.cs`
- `tests/Vulperonex.Tests.Integration/Infrastructure/VulperonexDbContextTests.cs`

**預估規模：** M

---

## 任務 5b：InitialSchema migration 與資料表配置

**描述：** 新增第一批 migration 與 EF Core configuration，覆蓋 Member、Workflow、Settings、Logs 與 display cache schema。

**驗收準則：**
- [ ] `InitialSchema` migration 可產生並套用。
- [ ] `SystemSettings` 表含 `Key`, `Value`, `Category`, `UpdatedAt`。
- [ ] `WorkflowRules` 表含 `ConditionsJson`，`ActionsJson` TEXT 欄位。
- [ ] `PlatformIdentities` 具 `(Platform, PlatformUserId)` unique constraint。
- [ ] `PlatformUserDisplayInfo` 具 `(Platform, PlatformUserId)` primary key。
- [ ] migration 不包含未經審核的 destructive operation。

**驗證：**
- [ ] Integration test：套用 migration 後可查到必要 table/index。
- [ ] Integration test：重複建立同一 `(Platform, PlatformUserId)` 會觸發 unique constraint 或被 resolver 的 `INSERT OR IGNORE` 安全處理。

**相依：** 任務 5a

**可能涉及的檔案：**
- `src/Vulperonex.Infrastructure/Migrations/`
- `src/Vulperonex.Infrastructure/Data/Configurations/MemberRecordConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/PlatformIdentityConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/WorkflowRuleConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/SystemSettingConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/AppLogConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/PlatformUserDisplayInfoConfiguration.cs`
- `tests/Vulperonex.Tests.Integration/Infrastructure/SchemaTests.cs`

**預估規模：** M

---

## 任務 5c：DB bootstrap、auto_vacuum 與 MigrationClassifier

**描述：** 實作啟動時 migration bootstrap，並建立 migration safety gate，使用 EF operation 與 raw SQL 內容分類 destructive/review-required migration。

**驗收準則：**
- [ ] DB bootstrap 在 `MigrateAsync()` 前執行 `PRAGMA auto_vacuum = FULL`。
- [ ] bootstrap 後 `PRAGMA auto_vacuum` 回傳 `2`。
- [ ] `MigrationClassifier` 檢查 `MigrationBuilder.Operations`。
- [ ] raw SQL 含 `DROP` / `DELETE` / `TRUNCATE` / `RENAME` 標記 destructive。
- [ ] raw SQL 含任何 `ALTER` 標記 review-required。
- [ ] raw SQL 不可因為不是 EF operation type 就略過分類。

**驗證：**
- [ ] Architecture/unit test：`DROP TABLE` raw SQL -> destructive。
- [ ] Architecture/unit test：`RENAME TABLE` raw SQL -> destructive。
- [ ] Architecture/unit test：`ALTER TABLE AddColumn` raw SQL -> review-required。
- [ ] Architecture/unit test：`DELETE FROM` raw SQL -> destructive。
- [ ] Architecture/unit test：`TRUNCATE` raw SQL -> destructive。
- [ ] Integration test：bootstrap + migrate 後 `PRAGMA auto_vacuum = 2`。

**相依：** 任務 5b

**可能涉及的檔案：**
- `src/Vulperonex.Infrastructure/Data/DatabaseBootstrapper.cs`
- `src/Vulperonex.Infrastructure/Migrations/MigrationClassifier.cs`
- `tests/Vulperonex.Tests.Architecture/Migrations/MigrationClassifierTests.cs`
- `tests/Vulperonex.Tests.Integration/Infrastructure/DatabaseBootstrapperTests.cs`

**預估規模：** M

---

## 任務 6a：建立 TDQ 與 ActionExecutionLog schema/repository

**描述：** 建立 TDQ 和副作用重複抑制所需的持久化模型與 repository，先完成資料結構與基本 CRUD。

**驗收準則：**
- [ ] `TransientDeliveryQueue` 可儲存 event payload、event type、created/updated timestamp 與 replay metadata。
- [ ] `ActionExecutionLog` schema 含 dedup key、`Status` (`Pending`, `Completed`, `Failed`) 與 `AttemptCount`。
- [ ] `ActionExecutionLog` 可查詢 Completed/Failed/Pending 狀態。
- [ ] TDQ payload 可承載預先產生的 `InvocationId`。

**驗證：**
- [ ] Integration test：enqueue TDQ item 後可讀回同一 payload。
- [ ] Integration test：ActionExecutionLog 可 insert pending、mark completed、mark failed。
- [ ] Unit/integration test：TDQ payload 中的 `InvocationId` 讀回不變。

**相依：** 任務 5c

**可能涉及的檔案：**
- `src/Vulperonex.Infrastructure/EventBus/TransientDeliveryQueue.cs`
- `src/Vulperonex.Infrastructure/EventBus/ActionExecutionLog.cs`
- `src/Vulperonex.Infrastructure/Migrations/`
- `tests/Vulperonex.Tests.Integration/EventBus/TransientDeliveryQueueTests.cs`

**預估規模：** M

---

## 任務 6b：實作 Channel overflow 與啟動 replay

**描述：** 將 Task 4 的 TDQ stub 補成實作：Channel 滿載時寫入 TDQ，啟動時重播未處理項目，成功後移除。

**驗收準則：**
- [ ] 模擬 Channel 滿載時事件寫入 TDQ，不丟棄。
- [ ] 啟動 replay 會讀取未處理 TDQ items 並重新 publish。
- [ ] TDQ item 處理成功後刪除。
- [ ] replay 不相依事件歷史持久化語意；TDQ 僅儲存待交付項目。

**驗證：**
- [ ] Integration test：強制 Channel 滿 -> event 進 TDQ。
- [ ] Integration test：重建 bus/bootstrap -> TDQ event 被 replay。
- [ ] Integration test：replay 成功後 TDQ item 被刪除。

**相依：** 任務 6a, 任務 4c

**可能涉及的檔案：**
- `src/Vulperonex.Infrastructure/EventBus/InMemoryStreamEventBus.cs`
- `src/Vulperonex.Infrastructure/EventBus/TdqReplayService.cs`
- `tests/Vulperonex.Tests.Integration/EventBus/TdqReplayTests.cs`

**預估規模：** M

---

## 任務 6c：實作 ActionExecutionLog dedup 與 IClock

**描述：** 實作 at-least-once 副作用重複抑制協定，包含 stale Pending retry、AttemptCount、永久 Failed 與 fake clock 測試。

**驗收準則：**
- [ ] 一般 action dedup key 為 `(EventId, WorkflowRuleId, ActionIndex)`。
- [ ] `InvokeSubWorkflowAction` dedup key 為 `(EventId, WorkflowRuleId, ActionIndex, InvocationId)`，且 `InvocationId` 在 action 執行前產生並持久化於 payload。
- [ ] `Completed` 或 `Failed` log entry replay 時略過。
- [ ] `Pending` 且 elapsed > 30s 時重試並 `AttemptCount++`。
- [ ] `AttemptCount >= MaxRetries+1` 後設為 `Failed`，後續不再重試。
- [ ] stale threshold 使用 `IClock`，不直接寫死 `DateTime.UtcNow`。

**驗證：**
- [ ] Integration test：同一 key 重複執行，第二次略過。
- [ ] Integration test（fake clock）：stale Pending 超過 30s -> retry，`AttemptCount` 增加。
- [ ] Integration test：`AttemptCount` 達上限 -> `Failed`，後續 replay 略過。
- [ ] Unit test：`InvocationId` replay 後不重新產生。

**相依：** 任務 6b

**可能涉及的檔案：**
- `src/Vulperonex.Application/Time/IClock.cs`
- `src/Vulperonex.Infrastructure/Time/SystemClock.cs`
- `src/Vulperonex.Infrastructure/EventBus/ActionExecutionLogStore.cs`
- `tests/Vulperonex.Tests.Integration/EventBus/ActionExecutionLogTests.cs`

**預估規模：** M

---

## 任務 7a：實作 IMemberResolver port 與 atomic resolver

**描述：** 在 Application 定義 `IMemberResolver` port，Infrastructure 以 SQLite `INSERT OR IGNORE + SELECT` 實作原子 GetOrCreate。

**驗收準則：**
- [ ] `IMemberResolver` 位於 Application，僅回傳 `MemberId` 或等效 Application DTO，不暴露 EF entity。
- [ ] `MemberResolver` 位於 Infrastructure。
- [ ] 並行解析同一 `(Platform, PlatformUserId)` 只建立一筆 `MemberRecord`。
- [ ] `MemberId` 為 ULID 字串。

**驗證：**
- [ ] Integration test：10 個 Task 同時解析同一 user，只建立 1 筆 MemberRecord。
- [ ] Integration test：回傳 MemberId 符合 ULID 格式。
- [ ] 架構測試：Application 不引用 Infrastructure resolver。

**相依：** 任務 5c

**可能涉及的檔案：**
- `src/Vulperonex.Application/Members/IMemberResolver.cs`
- `src/Vulperonex.Infrastructure/Members/MemberResolver.cs`
- `tests/Vulperonex.Tests.Integration/Members/MemberResolverTests.cs`

**預估規模：** M

---

## 任務 7b：實作 PlatformUserDisplayCache L1/L2

**描述：** 在 Adapter Infrastructure 邊界實作 `IPlatformUserInfoCache`，包含 L1 LRU、L2 SQLite 與 display info 狀態替換。

**驗收準則：**
- [ ] `IPlatformUserInfoCache` contract 位於 `Adapters.Abstractions`。
- [ ] `PlatformUserDisplayCache` 位於 Infrastructure 或 adapter infrastructure 實作層，Application/Domain 不引用。
- [ ] L1 miss -> L2 check -> platform fetch/update route 可被測試替身驗證。
- [ ] L1 容量預設 500，Task 8 後才接設定覆寫。
- [ ] `TotalBitsGiven` 等 display state 使用替換語意，不做 delta 累加。

**驗證：**
- [ ] Unit test：L1 hit 不查 L2。
- [ ] Unit/integration test：L1 miss 後從 L2 回填 L1。
- [ ] Unit test：狀態更新以新絕對值替換既有值。

**相依：** 任務 7a

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Abstractions/IPlatformUserInfoCache.cs`
- `src/Vulperonex.Infrastructure/Cache/PlatformUserDisplayCache.cs`
- `src/Vulperonex.Infrastructure/Cache/LruCache.cs`
- `tests/Vulperonex.Tests.Unit/Infrastructure/Cache/LruCacheTests.cs`
- `tests/Vulperonex.Tests.Integration/Cache/PlatformUserDisplayCacheTests.cs`

**預估規模：** M

---

## 任務 7c：完成 display cache UpdateAsync 與 TTL cleanup

**描述：** 補齊 cache miss default row、`UpdateAsync` updater 語意與 24h TTL 清理背景 worker。

**驗收準則：**
- [ ] `UpdateAsync` 對不存在 user 建立 default row：`AvatarUrl=null`、`ColorHex=null`、`Badges=Array.Empty<string>()`、`IsSubscriber=false`、`SubscriptionTier=null`、`TotalBitsGiven=0`、`FetchedAt=UtcNow`；建立後立即套用 updater，最終不回傳 null。
- [ ] `UpdateAsync` 對存在 user 套用 updater 並持久化。
- [ ] TTL 預設 24h。
- [ ] 過期 rows 由 background worker 清除。
- [ ] TTL 仍為 hardcoded default，Task 8 後才接設定覆寫。

**驗證：**
- [ ] Integration test：cache miss `UpdateAsync` 建立 default row 且不拋例外。
- [ ] Integration test：過期 row 被 cleanup worker 刪除，未過期 row 保留。
- [ ] Unit test：`Badges` default 為 empty array 而非 null。

**相依：** 任務 7b

**可能涉及的檔案：**
- `src/Vulperonex.Infrastructure/Cache/PlatformUserDisplayCache.cs`
- `src/Vulperonex.Infrastructure/Cache/PlatformUserDisplayCacheCleanupWorker.cs`
- `tests/Vulperonex.Tests.Integration/Cache/PlatformUserDisplayCacheTests.cs`

**預估規模：** S

---

## 任務 8a：實作 ISystemSettingsService SQLite-backed Get/Set

**描述：** 建立系統設定 Application port 與 SQLite-backed Infrastructure 實作，支援 typed get/set 與 default fallback。

**驗收準則：**
- [ ] `ISystemSettingsService` 位於 Application。
- [ ] `Get<T>(key, default)` 可正確反序列化。
- [ ] `SetAsync` upsert 至 `SystemSettings`。
- [ ] `SystemSettingKey` 常數包含以下 Phase 2 擁有或已被後續 MVP task 直接相依的 key：
  - `OAuthTwitchRefreshToken = "oauth.twitch.refresh_token"`
  - `StreamingPlatform = "streaming.platform"`
  - `BusChannelCapacity = "bus.channel_capacity"`
  - `OverlayDisplayCacheL1Capacity = "overlay.display_cache_l1_capacity"`
  - `OverlayDisplayCacheTtlHours = "overlay.display_cache_ttl_hours"`
  - `LogMinLevel = "log.min_level"`
  - `LogDbRetentionDays = "log.db_retention_days"`
  - `LogDbMaxSizeMb = "log.db_max_size_mb"`
  - `LogFileRetentionDays = "log.file_retention_days"`
  - （Task 10/14/18 新增的 workflow/API/log 行為若需要額外 runtime key，必須在對應 task 擴充 `SystemSettingKey`，不得使用自由文字 key）
- [ ] 所有 key 均為 canonical lowercase；DB 儲存時使用 lowercase。
- [ ] protected namespace 的 REST 封鎖不在本 task 實作，留給 Task 14b；本 task 只提供 service 與 token store 基礎。

**驗證：**
- [ ] Unit/integration test：missing key 回傳 default。
- [ ] Integration test：set 後 get 回傳 typed value。
- [ ] Unit/integration test：key 寫入時正規化為 lowercase。
- [ ] Integration test：updated timestamp 變更。

**相依：** 任務 5c

**可能涉及的檔案：**
- `src/Vulperonex.Application/Settings/ISystemSettingsService.cs`
- `src/Vulperonex.Application/Settings/SystemSettingKey.cs`
- `src/Vulperonex.Infrastructure/Settings/SystemSettingsService.cs`
- `tests/Vulperonex.Tests.Integration/Settings/SystemSettingsServiceTests.cs`

**預估規模：** M

---

## 任務 8b：設定熱重載 Changes observable

**描述：** 在 settings service 補上 `IObservable<SettingChangedEvent>`，並把 Task 4/7 的硬編碼預設值接到 Phase 2 runtime settings。Task 18 之後才接 log level 熱重載，但本 task 先建立相同通知機制。

**驗收準則：**
- [ ] `SetAsync` 寫入 DB 後發出 `SettingChangedEvent`。
- [ ] 訂閱者收到 key、old value/new value 或足以重新載入的 payload。
- [ ] observable 不因單一 subscriber 例外破壞 settings 寫入。
- [ ] `InMemoryStreamEventBus` 初始化時讀取 `bus.channel_capacity`，missing key 使用 Task 4 的 10,000 預設值；設定變更後可更新後續 publish 使用的 capacity/overflow threshold。
- [ ] `PlatformUserDisplayCache` 初始化時讀取 `overlay.display_cache_l1_capacity` 與 `overlay.display_cache_ttl_hours`，missing key 使用 Task 7 的 500 / 24h 預設值；設定變更後更新 L1 capacity 與 TTL。
- [ ] 後續 Task 18 可用同一 `Changes` 機制覆寫 log level，但 Phase 2 不實作 Serilog/log-level subscriber。

**驗證：**
- [ ] Unit/integration test：訂閱後 `SetAsync` 會收到通知。
- [ ] Unit test：subscriber throw 時其他 subscriber 仍收到，`SetAsync` 不失敗。
- [ ] Unit/integration test：設定 `bus.channel_capacity` 後建立 bus，capacity/overflow threshold 使用設定值；missing key 時使用 10,000。
- [ ] Unit/integration test：設定 `overlay.display_cache_l1_capacity` / `overlay.display_cache_ttl_hours` 後建立 cache，L1 capacity/TTL 使用設定值；missing key 時使用 500 / 24h。
- [ ] Unit/integration test：變更 display cache capacity 後，L1 cache 會依新容量裁切或在後續 insert 時維持新容量上限。

**相依：** 任務 8a, 任務 4c, 任務 7c

**可能涉及的檔案：**
- `src/Vulperonex.Application/Settings/SettingChangedEvent.cs`
- `src/Vulperonex.Infrastructure/Settings/SystemSettingsService.cs`
- `tests/Vulperonex.Tests.Unit/Settings/SystemSettingsChangeTests.cs`

**預估規模：** S

---

## 任務 8c：OAuth token 加密、machine.key 與 IOAuthTokenStore

**描述：** 實作 OAuth refresh token 的安全儲存：AES-256-GCM versioned envelope、AAD 綁定設定鍵、machine.key 建立/權限與解密錯誤。

**驗收準則：**
- [ ] `IOAuthTokenStore` 位於 Application/Auth，介面定義：
  - `StoreRefreshTokenAsync(string platform, string rawToken)` — 加密後 upsert 至 SystemSettings。
  - `GetRefreshTokenAsync(string platform)` → `string?`（key 不存在回傳 null；machine.key 錯誤/遺失拋 `CredentialDecryptionException`）。
  - MVP 只允許 `"twitch"` 作為 platform 值；其他值拋 `ArgumentException("Unknown OAuth platform: {platform}")`。
- [ ] refresh token 以 `"v1:" + Base64(nonce(12B) || ciphertext || tag(16B))` 存入 `SystemSettings.Value`。
- [ ] 使用標準 Base64，後續解碼使用 `Convert.FromBase64String`，非 Base64Url。
- [ ] AES-GCM key 來自 OS app-data root 的 `machine.key` raw 32 bytes，無 KDF。
- [ ] AAD = setting key name UTF-8 bytes，傳入 `AesGcm.Encrypt()`，解密時重新傳入相同鍵名；AAD **不存入 envelope**（繫結密文至鍵名，防止跨鍵複製攻擊）；cross-key copy 解密失敗。
- [ ] `machine.key` 不存在時自動建立 cryptographically random 32 bytes。
- [ ] 建立後立即設定限制性權限：Windows 目前使用者 ACL FullControl 並移除繼承；Unix chmod 0600。
- [ ] chmod/ACL 失敗拋 `IOException`，fail-fast。
- [ ] machine.key 遺失或錯誤造成解密失敗時拋 `CredentialDecryptionException`，由呼叫端提示重新授權。
- [ ] key 路徑固定 OS app-data root，不隨 `Database:Path` 變動。平台路徑：
  - Windows: `%AppData%\Vulperonex\machine.key`（`Environment.SpecialFolder.ApplicationData`）
  - macOS: `~/Library/Application Support/Vulperonex/machine.key`
  - Linux: 遵循 `XDG_DATA_HOME`（未設定時 `~/.local/share/Vulperonex/machine.key`）
- [ ] `MachineKeyProvider` 注入 `IFileSystem` 抽象（自訂輕量 port 或 `System.IO.Abstractions` NuGet — 若用後者需依 SPEC §8.2 ask-first 規則確認後再加入 csproj），確保 unit tests 可用 fake 驗證 ACL/chmod 行為，無需真實 I/O。

**驗證：**
- [ ] Unit test：`StoreRefreshTokenAsync("twitch", "raw-token")` → `GetRefreshTokenAsync("twitch")` 回傳 `"raw-token"`（round-trip 正確）。
- [ ] Unit test：`StoreRefreshTokenAsync("twitch", ...)` 寫入 `SystemSettingKey.OAuthTwitchRefreshToken` key，且 `SystemSettings.Category = "oauth"`。
- [ ] Unit test：`GetRefreshTokenAsync` — key 不存在回傳 `null`；machine.key 錯誤拋 `CredentialDecryptionException`（非 null、非 crash）。
- [ ] Unit test：`StoreRefreshTokenAsync("unknown-platform", ...)` → 拋 `ArgumentException`（MVP 只允許 `"twitch"`）。
- [ ] Unit test：同一 token 兩次加密 ciphertext 不同（nonce randomness）。
- [ ] Unit test：tamper ciphertext/tag -> `CredentialDecryptionException`。
- [ ] Unit test：AAD cross-key copy（不透過 `IOAuthTokenStore`，直接呼叫底層加密 helper）-> `CredentialDecryptionException`。
- [ ] Unit/integration test：DB persisted value 不等於 raw refresh token。
- [ ] Unit test：machine.key 建立為 32 bytes。
- [ ] Integration test（temp dir）：machine.key 不存在 → 建立 32 bytes 並設定 OS 限制性權限（Windows ACL user-only / Unix 0600）。
- [ ] Platform-specific test 或抽象測試：ACL/chmod failure -> `IOException`。

**相依：** 任務 8a

**可能涉及的檔案：**
- `src/Vulperonex.Application/Auth/IOAuthTokenStore.cs`
- `src/Vulperonex.Application/Auth/CredentialDecryptionException.cs`
- `src/Vulperonex.Infrastructure/Auth/OAuthTokenStore.cs`
- `src/Vulperonex.Infrastructure/Security/MachineKeyProvider.cs`
- `tests/Vulperonex.Tests.Unit/Auth/OAuthTokenStoreTests.cs`
- `tests/Vulperonex.Tests.Integration/Auth/OAuthTokenStorePersistenceTests.cs`

**預估規模：** M

---

## 第二階段檢查點

**驗收準則：**
- [ ] 任務 4a-8c 已完成並以小切片形式提交。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 通過。
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過。
- [ ] 事件 publish -> bus -> handler 端到端通過。
- [ ] MigrationClassifier raw SQL destructive/review-required tests 通過。
- [ ] DB bootstrap `PRAGMA auto_vacuum = 2` 通過。
- [ ] TDQ overflow -> replay -> delete 通過。
- [ ] ActionExecutionLog Completed/Failed/Pending retry semantics 通過。
- [ ] MemberResolver 並行測試通過。
- [ ] Display cache miss `UpdateAsync` default row 通過。
- [ ] AES-256-GCM tamper 與 AAD cross-key copy 測試通過。
- [ ] 架構測試確認 Domain/Application 無 Infrastructure/EF 洩漏。
- [ ] `git status --short --ignored` 僅顯示預期忽略的本地檔案。

**審查門檻：**
- [ ] 在開始第三階段之前，人工 review 架構層相依：確認 Domain/Application 無 Infrastructure 引用洩漏、EF Core 型別未外露、TDQ/dedup 確實 at-least-once safe。

---

## 風險與緩解

| 風險 | 影響 | 緩解措施 |
|------|------|----------|
| EF Core / SQLite 套件或工具版本不匹配 | 高 | 開始 Task 5 前確認中央套件版本；新增套件先詢問；使用 repo-local restore config。 |
| EventBus fire-and-forget 測試 flake | 中 | 使用 `WaitForIdleAsync` 和 deterministic synchronization，避免固定 sleep。 |
| TDQ replay 造成重複副作用 | 高 | 先完成 `ActionExecutionLog` 狀態機與 fake clock 測試，再接 Workflow action executor。 |
| SQLite 並行寫入競爭狀況 | 中 | Resolver 使用 `INSERT OR IGNORE + SELECT`，測試以多 Task 同時呼叫覆蓋。 |
| token 加密跨平台檔案權限差異 | 中 | 將 machine key path/permission 抽象化，Windows ACL 與 Unix chmod 分開測試。 |

---

## 開放問題

- Task 5 是否已具備 EF Core SQLite / Design / Tools 版本與套件參考；若尚未存在，開始實作前需先詢問批准新增 NuGet 套件。
