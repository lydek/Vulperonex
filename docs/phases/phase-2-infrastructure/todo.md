# 第二階段待辦清單：事件匯流排 + Infrastructure

> 詳細計畫：`docs/phases/phase-2-infrastructure/plan.md`
> 父待辦清單：`tasks/todo.md`

---

## 任務 4：事件匯流排

- [x] 任務 4a：定義 `IStreamEventBus` 契約
- [x] 任務 4b：實作 `InMemoryStreamEventBus` dispatch、assignable match 與 handler 隔離
- [x] 任務 4c：穩定 `WaitForIdleAsync` 與 dispatch lifecycle

## 任務 5：EF Core + SQLite 基礎設施

- [x] 任務 5a：建立 EF Core / SQLite 基礎與 `VulperonexDbContext`
- [x] 任務 5b：新增 `InitialSchema` migration 與資料表配置
- [x] 任務 5c：實作 DB bootstrap、`PRAGMA auto_vacuum = FULL` 與 `MigrationClassifier`

## 任務 6：TDQ + at-least-once 保證

- [x] 任務 6a：建立 TDQ 與 `ActionExecutionLog` schema/repository
- [x] 任務 6b：實作 Channel overflow 與啟動 replay
- [x] 任務 6c：實作 `ActionExecutionLog` dedup 狀態機與 `IClock`

## 任務 7：MemberResolver + PlatformUserDisplayCache

- [x] 任務 7a：實作 `IMemberResolver` port 與 atomic resolver
- [x] 任務 7b：實作 `PlatformUserDisplayCache` L1/L2
- [x] 任務 7c：完成 display cache `UpdateAsync` default row 與 TTL cleanup

## 任務 8：SystemSettings + token 安全儲存

- [ ] 任務 8a：實作 `ISystemSettingsService` SQLite-backed Get/Set
- [ ] 任務 8b：實作設定熱重載 `Changes` observable，並接上 bus capacity / display cache capacity+TTL 覆寫
- [ ] 任務 8c：實作 OAuth token 加密、`machine.key` 與 `IOAuthTokenStore`

## 第二階段檢查點

- [ ] 全方案編譯通過
- [ ] 全方案測試通過
- [x] 事件 publish → bus → handler 端到端通過
- [x] `MigrationClassifier` raw SQL destructive/review-required tests 通過
- [x] DB bootstrap `PRAGMA auto_vacuum = 2` 通過
- [x] TDQ overflow → replay → delete 通過
- [x] `ActionExecutionLog` Completed/Failed/Pending retry semantics 通過
- [x] `MemberResolver` 並行測試通過
- [x] `IPlatformUserInfoCache.UpdateAsync` cache miss → default row 通過
- [ ] `bus.channel_capacity` 可覆寫 EventBus 預設 10,000 通過
- [ ] `overlay.display_cache_l1_capacity` / `overlay.display_cache_ttl_hours` 可覆寫 display cache 預設 500 / 24h 通過
- [ ] AES-256-GCM tamper 與 AAD cross-key copy 測試通過
- [ ] 架構測試確認 Domain/Application 無 Infrastructure/EF 洩漏
- [ ] Git 狀態乾淨（忽略的本地檔案除外）
- [ ] 第三階段開始前完成第二階段審查
