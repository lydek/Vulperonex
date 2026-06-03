# 規格書：Vulperonex — 多平台直播自動化平台

> **狀態：** 已批准 v0.4（MVP 已交付；後續功能依各 Phase 文件追蹤）。v0.4 收斂 2026-06-01 spec-vs-impl 審查報告所揭露的英文版 → 程式碼漂移（詳見 `docs/spec-vs-impl-2026-06-01.md`）。
> **最後更新：** 2026-06-01
> ⚠️ **翻譯落後英文版：** 本檔頭、§4.25、§6.1 已同步至 v0.4；§2 / §4.4 / §4.6 / §4.7 / §4.13.1 / §4.22 的 v0.4 細節更新請以英文版 `docs/SPEC.md` 為準，zh-TW 全面同步排在後續維護任務。
> **儲存庫：** 全新綠地專案儲存庫。本規格書描述了**目標架構**。無現有程式碼需遷移。
> **前身參考：** Omni-Commander (獨立繼任者 — 借鑑領域邏輯概念，而非程式碼)

---

## 1. 目標

**Vulperonex** 是一個平台無關的直播自動化工具，它聚合來自直播平台的事件，並透過統一的事件驅動架構驅動響應式功能（聊天疊層、工作流、成員追蹤、音效）。MVP 支援 Twitch；架構設計為可擴充至其他平台而不需修改 Domain/Application 層。

**原因：** 現有工具（包括前身專案 Omni-Commander）與 Twitch 特有概念高度耦合。Vulperonex 透過領域事件 (Domain Events) 將平台特定的事件源與功能消費者分離，從而能夠以極少的程式碼新增平台，並將測試/模擬支持作為一等事件源。

### 目標使用者

- **直播主：** 執行 Twitch（MVP 範圍）。
- **外掛程式作者：** 透過 `IVulperonexPlugin` 擴充行為 — 既可以消費事件也可以發布事件。
- **開發者：** 針對與實際平台相同的事件流執行自動化測試。

### MVP 成功願景

- Twitch 適配器 (Adapter) 將領域事件發布到事件匯流排 (Event Bus)。
- 工作流引擎 (WorkflowEngine) 訂閱並執行規則。
- 桌面殼層 (Photino) 託管由 Web 主機提供的 Vue UI。
- CLI 可以模擬事件、管理配置、管理規則並檢查成員。
- 新增平台 Adapter 不需修改 Application / Domain 層（架構隔離）。

---

## 2. 技術棧

### 後端 (.NET 10 LTS)

| 項目 | 選擇 |
|---|---|
| 語言 / 執行時期 | C# 14 / .NET 10 LTS |
| Web 框架 | ASP.NET Core Minimal API (10.0) |
| 即時通訊 | SignalR (10.0) |
| ORM | EF Core 10 (SQLite provider) |
| 桌面殼層 | Photino.NET 3.x |
| 外掛程式系統 | `IVulperonexPlugin` (自訂契約，MVP 階段在啟動時靜態引用) |
| 單元測試 | xUnit 3 / NSubstitute / FluentAssertions 7 |
| 測試方法 | BDD 情境定義 + TDD 紅/綠/重構實作 |

### 前端 (Vue 3.5+ / Vite 7.x)

| 項目 | 選擇 |
|---|---|
| 框架 | Vue 3.5+ (標準 SFC — Vapor Mode 推遲至第二階段效能實驗) |
| 建構 | Vite 7.3 (Rolldown；MVP 釘定 v7，不追 v8 — Vite 8 已發布但 MVP 期間不升級) |
| 語言 | TypeScript 6.0 |
| UI | PrimeVue 4 (Unstyled) / UnoCSS (Preset Wind 4) |
| 狀態 / 通訊 | Pinia 2.3 / Axios / @microsoft/signalr 10.0 |
| 測試 | Vitest 3 / Vue Test Utils 2.5 |
| 多語言 (i18n) | vue-i18n 11.x |

---

## 3. 專案結構

```
Vulperonex/
├── src/
│   ├── Vulperonex.Domain/                     # 純領域 — 實體、值對象、事件
│   ├── Vulperonex.Application/                 # 使用案例、介面、事件匯流排契約
│   ├── Vulperonex.Infrastructure/              # EF Core, SQLite, 倉儲 (Repos), 持久化
│   ├── Vulperonex.Plugins.Abstractions/        # IVulperonexPlugin 契約 (來源 + 接收)
│   │
│   ├── Adapters/
│   │   ├── Vulperonex.Adapters.Abstractions/   # 共享適配器介面 (IPlatformUserInfoCache 等)
│   │   ├── Vulperonex.Adapters.Twitch/         # Twitch IRC + EventSub (傳入 + 傳出)
│   │   └── Vulperonex.Adapters.Simulation/     # CLI / UI / 測試模擬
│   │
│   ├── Hosts/
│   │   ├── Vulperonex.Web/                     # ASP.NET Minimal API + SignalR + wwwroot
│   │   ├── Vulperonex.Desktop/                 # Photino 殼層 (包裝 Web)
│   │   └── Vulperonex.Cli/                     # CLI: 模擬 / 配置 / 規則 / 成員指令
│   │
│   └── frontend/                               # Vue 3.5 SPA，建構至 Web/wwwroot
│       ├── src/
│       │   ├── components/
│       │   ├── composables/
│       │   ├── stores/
│       │   ├── views/
│       │   └── i18n/
│       └── tests/
│
├── tests/
│   ├── Vulperonex.Tests.Unit/
│   ├── Vulperonex.Tests.Integration/
│   └── Vulperonex.Tests.Architecture/          # 層級 / 依賴規則強制執行
│
├── docs/                                        # SPEC.md 正式路徑：docs/SPEC.md
│   ├── SPEC.md
│   ├── adr/                                     # 架構決策記錄 (Architecture Decision Records)
│   └── plugins/
│
└── tools/
```

---

## 4. 架構核心概念

### 4.1 分層架構 (整潔架構)

```
┌─────────────────────────────────────────────┐
│ 主機 (Hosts: Web / Desktop / CLI)            │  依賴 ↓
├─────────────────────────────────────────────┤
│ 適配器 (Adapters: Twitch / Simulation)       │  依賴 ↓
│   + Adapters.Abstractions（IStreamEventSource 等）│ 所有 Adapter 依賴 Domain + Adapters.Abstractions + Application
│   （`IStreamEventBus` 定義於 Application；Adapter publish 事件需此依賴）│
├─────────────────────────────────────────────┤
│ 應用層 (Application: UseCases, EventBus, Ports) │  依賴 ↓
├─────────────────────────────────────────────┤
│ 領域層 (Domain: Entities, Events, Value Objects) │  最內層，無外部依賴
└─────────────────────────────────────────────┘
基礎設施 (Infrastructure) 橫切：實作 Application ports，依賴 Application + Domain，不被 Domain/Application 依賴。
箭頭讀法：外層依賴內層（Inner layers know nothing about outer layers）。
```

### 4.1a 戰術 DDD 邊界

Vulperonex 在保護直播自動化模型的地方使用戰術領域驅動設計 (DDD)：

- **領域層 (Domain)** 擁有平台中立的實體、值對象、領域事件和不變式 (Invariants)。
- **應用層 (Application)** 擁有使用案例編排，並定義倉儲/服務埠。
- **基礎設施、適配器、主機和 UI** 不得包含屬於領域層或應用層的領域規則。
- **倉儲介面 (Repository interfaces)** 是應用層的埠；EF Core 實作留在基礎設施層。
- **領域事件** 使用通用語言描述事實 (`UserSentMessageEvent`, `MemberRecord`, `WorkflowRule`)，且不得暴露任何平台 SDK 的有效負載類型。
- **聚合 (Aggregates)** 在 MVP 階段保持微小。除非是為了強制執行真正的連貫性邊界，否則不要引入廣泛的聚合根。

### 4.1b DCI-inspired Role/Behavior 拆分準則

Vulperonex 以 DDD + Clean Architecture 為主架構。**DCI（Data/Context/Interaction）作為 DDD tactical design 的輔助手法**，不取代 Repository、CQRS 或 Clean Architecture 依賴方向。

**觸發條件：** 當 Aggregate 或 Domain service 開始承擔多個使用情境的行為、方法數持續增長時，採用 Role/Behavior 拆分維持 SRP。

**邊界對應：**

```
Domain 邊界（Data + Role）
  Entity / Value Object        — 狀態與不變式
  Role / Behavior objects      — 特定使用情境的純領域行為

Application 邊界（Context + Interaction）
  UseCase / Context class      — 指派 Data 扮演 Role，編排協作
  Ports（Repository / Service）— 與 Infrastructure 解耦

Infrastructure（不在 DCI 範圍內）
  EF Core Repository 實作
  DbContext / SQLite
```

**強制規則：**
- Role/Behavior 物件必須是**純 Domain 邏輯**，不得直接依賴 `DbContext`、EF Core 或任何 Infrastructure 型別
- Context/Interaction 屬於 Application use case，透過 port 介面存取 Infrastructure
- **MVP 不做 runtime dynamic role / reflection / mixin**（Part 3 模式）；Role 為 compile-time 靜態拆分

**架構測試識別規則：**
- 識別目標：`Vulperonex.Domain` 命名空間下類型名稱以 `Role` 或 `Behavior` 結尾的類別（`*Role`, `*Behavior`）
- 測試斷言：上述類別的 assembly 不得引用 `Vulperonex.Infrastructure`、`Microsoft.EntityFrameworkCore` 或任何 `*.Infrastructure.*` 命名空間
- 落點：`tests/Vulperonex.Tests.Architecture/Domain/DciRoleIsolationTests.cs`（Task 3 實作）

### 4.1c 輕量級 CQRS 邊界

Vulperonex 在應用層邊界使用輕量級 CQRS：

- **命令 (Commands)** 變更狀態並強制執行不變式。
- **查詢 (Queries)** 返回讀取優化的 DTO，且不得直接暴露 EF 實體。
- **寫入倉儲埠** 與 **查詢服務埠** 保持分離（`IMemberRepository` 與 `IMemberQueryService` 是基準模式）。
- MVP 使用同一個 SQLite 資料庫進行命令和查詢路徑。
- **不要**在 MVP 中加入命令匯流排、查詢匯流排、事件溯源 (Event Sourcing) 或獨立的讀取資料庫。
- REST 端點可以位於同一主機/控制器組中，但命令端點和查詢端點必須呼叫分開的應用層埠。

### 4.2 事件流

```
[適配器] ─→ 映射 ─→ IStreamEvent ─→ IStreamEventBus
                                          ↓
                               ┌──────────┼──────────┐
                               ↓          ↓          ↓
                           工作流模組    疊層模組    成員模組
                           (Workflow)  (Overlay)  (Member)
```

- 所有流程都經過 `IStreamEventBus`。適配器絕不直接呼叫處理程式 (Handlers)。
- 外掛程式可以同時作為**事件源**（發布到匯流排）和**事件消費者**（訂閱）。
- 模擬適配器是一個真實的適配器，而非側邊通道 — 測試和 CLI 使用與實際平台相同的路徑。

**匯流排語意（已解決）：**

| 項目 | 決策 |
|---|---|
| 順序性 | **不保證。** 無跨處理程式的順序契約。 |
| 例外隔離 | 每個處理程式都包裹在 try/catch 中。例外 → 記錄日誌 (LOG)，其他處理程式繼續執行不受影響。 |
| 背壓 (Backpressure) | 預設：記憶體內 `Channel<IStreamEvent>` (10,000 個槽位)。當深度超過閾值時 → 溢出寫入 **臨時交付佇列 (TDQ)** SQLite 資料表。TDQ 語意：有效負載儲存至處理完成，成功後立即刪除，若未處理則在啟動時重播。TDQ **不是**事件持久化 — 不保留歷史記錄。閾值可透過 `SystemSettings` 配置。 |
| 交付語意 | **至少一次 (At-least-once)。** 啟動時的 TDQ 重播意味著事件可能被處理多次。內建副作用（`SendChatMessageAction`, `InvokePluginAction`）以 `ActionExecutionLog` 去重。**`MemberModule` 使用 `INSERT OR IGNORE` 原子 GetOrCreate — 重播安全。** **`PlatformUserDisplayCache` 更新必須使用「狀態替換」而非「delta 累加」語意**（`TotalBitsGiven` 儲存 platform payload 中的絕對值，不做 `+= amount`；TDQ 重播不造成重複累加）。內建的副作用操作（`SendChatMessageAction`, `InvokePluginAction`）使用**基於狀態的 `ActionExecutionLog`** 資料表。去重唯一鍵為 `(EventId, WorkflowRuleId, ActionIndex)` — 僅靠 `ActionIndex` 是不夠的，因為同一個事件可以匹配多個 `WorkflowRule`。**已知限制：rule 更新後 action 順序可能改變，舊 TDQ event 重播時同一 `ActionIndex` 可能指向不同 action。MVP 接受此限制（TDQ 重播窗口短，rule 更新機率低）；需要 stable action identity 屬 post-MVP。**對於子工作流調用，會追加一個額外的 `InvocationId` (每次 `InvokeSubWorkflowAction` 調用生成的 ULID) 以形成 `(EventId, WorkflowRuleId, ActionIndex, InvocationId?)`。去重協議：(1) `INSERT OR IGNORE (key, Status=Pending, AttemptCount=0)`；(2) 如果被忽略且 `Status=Completed` → 跳過；`Status=Failed` → 跳過（永久失敗，不重試）；`Status=Pending` 且 elapsed > 30秒（stale crash） → 重試，`AttemptCount++`；`AttemptCount >= MaxRetries+1` 且仍失敗 → `UPDATE Status=Failed`（永久停止，下次重播不再重試）；(3) 執行副作用；(4) `UPDATE Status=Completed`。**stale 閾值 30秒透過 `IClock` 抽象注入（Task 6 補 fake clock 實作），不寫死 `DateTime.UtcNow`。** **`InvocationId` 必須在 action 執行前即產生並持久化（或納入 TDQ payload），確保 TDQ 重播時使用同一 InvocationId — 若在每次執行時動態產生新 ULID，重播會得到不同 dedup key，造成 sub-workflow 重複執行。**外掛程式操作透過 `IPluginActionContext.ActionExecutionKey`（見第 6.3 節）接收完整的執行鍵，且文件**必須**要求將此鍵用於任何外部副作用。 |
| 發布模式 | **發後不理 (Fire-and-forget)。** `PublishAsync` 入隊後立即返回。呼叫端執行緒絕不被處理程式執行所阻塞。 |

### 4.3 身分模型 (兩層)

| 層級 | 目的 | 範例 |
|---|---|---|
| `StreamUser` | 平台繫結的身分，攜帶在事件中，用於顯示 | `{ Platform: "twitch", UserId: "12345", DisplayName: "alice" }` |
| `MemberRecord` | 持久成員，在首次事件時自動創建 | `{ MemberId: ULID, Identities: [twitch:12345], Loyalty: {...} }` |

**顯示始終使用 `StreamUser`。聚合/分析使用 `MemberId`。**

- `MemberId` 使用 **ULID**（時間可排序、按字典順序排列、對 SQLite 索引友好）。
- `PlatformIdentity` 資料表具有複合鍵 `(Platform, PlatformUserId) → MemberId`。

**MemberResolver 競態條件處理 (G3)：**

`PlatformIdentity` 具有 `UNIQUE (Platform, PlatformUserId)` 約束。Resolver 使用 SQLite `INSERT OR IGNORE` + `SELECT` — 原子性的 GetOrCreate 模式。不需要應用程式級別的鎖；SQLite WAL 會序列化寫入。

**CA 邊界：** Application 只定義 `IMemberResolver` port 介面（`ResolveAsync(string platform, string platformUserId) -> MemberId`）；實際 EF Core / raw SQL 實作（`MemberResolver`）放在 Infrastructure 層，不得出現在 Application 或 Domain。

```csharp
// Application port（介面只在 Application）
public interface IMemberResolver
{
    /// <returns>MemberId (ULID string) — 既有或新建</returns>
    Task<string> ResolveAsync(string platform, string platformUserId, CancellationToken ct = default);
}

// Infrastructure 實作（偽程式碼）— 無論誰先插入，始終讀取勝利者
await db.ExecuteSqlRawAsync(
    "INSERT OR IGNORE INTO PlatformIdentities (Platform, PlatformUserId, MemberId, ...) VALUES (?,?,?,...)");
var identity = await db.PlatformIdentities
    .FirstAsync(x => x.Platform == platform && x.PlatformUserId == userId);
```

---

### 4.3b 平台使用者顯示快取 (G4)

疊層 (Overlay) 需要顯示資料（頭像、顏色、徽章、訂閱狀態、Bit 總計），這些資料並不隨每個平台事件傳遞，且從平台 API 獲取每個事件的資料成本很高。

**兩級快取 — 僅限適配器基礎設施。應用層/領域層不知道其存在。**

```
L1：受限的記憶體內 LRU 快取
    預設最大值：500 條項目 (~150 KB)。可配置。
    溢出時逐出最近最少使用的項目。

L2：SQLite 資料表 — PlatformUserDisplayInfo
    L1 未命中 → 檢查資料庫 (對 FetchedAt 進行 TTL 檢查)
    資料庫未命中 → 從平台 API 獲取 → 寫入資料庫 + 回填 L1
    預設 TTL：24小時。後台工作程式清理過期的行。
```

**資料庫表：**

```sql
CREATE TABLE PlatformUserDisplayInfo (
    Platform         TEXT NOT NULL,
    PlatformUserId   TEXT NOT NULL,
    AvatarUrl        TEXT,
    ColorHex         TEXT,
    BadgesJson       TEXT,           -- 徽章字串的 JSON 陣列
    IsSubscriber     INTEGER NOT NULL DEFAULT 0,
    SubscriptionTier TEXT,           -- "1000" | "2000" | "3000" | null
    TotalBitsGiven   INTEGER NOT NULL DEFAULT 0,
    FetchedAt        INTEGER NOT NULL,  -- Unix 時間戳
    PRIMARY KEY (Platform, PlatformUserId)
);
```

**介面 (Adapters.Abstractions)：**

```csharp
public interface IPlatformUserInfoCache
{
    ValueTask<UserDisplayInfo?> GetAsync(string platform, string userId);
    Task SetAsync(string platform, string userId, UserDisplayInfo info);
    // cache miss → create default UserDisplayInfo row
    //   (AvatarUrl=null, ColorHex=null, Badges=Array.Empty<string>(), IsSubscriber=false,
    //    SubscriptionTier=null, TotalBitsGiven=0, FetchedAt=UtcNow), then apply updater.
    //   Never returns null post-update.
    Task UpdateAsync(string platform, string userId, Func<UserDisplayInfo, UserDisplayInfo> updater);
}

public record UserDisplayInfo(
    string? AvatarUrl,
    string? ColorHex,           // null 或 ^#[0-9A-Fa-f]{6}$；不接受 CSS function / named color / alpha
    IReadOnlyList<string> Badges,
    bool IsSubscriber,
    string? SubscriptionTier,
    int TotalBitsGiven,
    DateTimeOffset FetchedAt);
```

**相關事件時主動更新快取：**

適配器訂閱領域事件並主動更新快取 — 無需等待 TTL 到期：

| 領域事件 | 快取更新 |
|---|---|
| `UserSubscribedEvent` | `IsSubscriber=true`, `SubscriptionTier`, 新增訂閱者徽章 |
| `UserDonatedEvent` | `TotalBitsGiven = max(existing, event.TotalBitsGiven)`（**monotonic 絕對值替換**，非 `+= amount`；event payload 攜帶 cumulative total，TDQ 重播不重複累加，out-of-order 舊 payload 不得讓累積值回退；若平台後台人工調整導致需要降低本地值，需走未來明確 admin reset 流程）|
| `UserFollowedEvent` | 新增追隨者徽章 |

**事件上的顯示提示 (DisplayHints)：**

適配器在發布前使用結構化的顯示提示豐富事件。**禁止原始 HTML** — 疊層是 OBS 瀏覽器源，XSS 是真實的攻擊面。訊息內容表示為類型化片段；前端安全渲染。

標準化的提示鍵：

| 鍵 | 類型 | 範例值 |
|---|---|---|
| `display.color` | 十六進位字串，格式 `^#[0-9A-Fa-f]{6}$`（6 位 RGB，不接受 3-digit shorthand、CSS function、named color、8 位 alpha 或空字串）| `#FF4A4A` |
| `display.segments` | JSON 片段陣列 | 見下方 |
| `user.avatar` | URL 字串 | `https://cdn.twitch.tv/...` |
| `user.badges` | 逗號分隔的徽章 `id/value`；每個 badge ID 字元集 `[A-Za-z0-9_/\-]`；badge value 最多 64 字元；最多 20 個；重複項去重（保留首次出現順序）| `subscriber/2000,vip` |
| `user.is_subscriber` | `"true"` / `"false"` | `"true"` |
| `user.bits_total` | 整數值字串 | `"5000"` |

**`display.segments` 格式（無原始 HTML）：**

```json
[
  { "type": "text",  "value": "hello " },
  { "type": "emote", "id": "Kappa", "url": "https://static-cdn.jtvnw.net/..." },
  { "type": "text",  "value": " world" }
]
```

允許的片段類型：`text`, `emote`, `badge`, `mention`。前端安全地渲染每種類型，使用 `textContent`（或等效 DOM API），**禁止 `innerHTML`**。`text` 片段值可包含任意 Unicode（含 `<`、`>`）— 它們是文字資料，不是 markup；前端渲染時瀏覽器自動轉義，不視為 HTML。**安全邊界在於 `type` 欄位的 allowlist 與渲染方式，而非過濾 `text` 片段的值**。`emote`、`badge` 片段只允許受信任的 `id` + `url`，不允許任意 HTML 屬性。

**emote / badge URL 信任邊界（MVP 明確不作為）：** `url` 欄位由 TwitchAdapter 直接從平台 API 填入（Twitch CDN URL）；MVP 不在 Domain 或 Overlay 層執行 scheme/domain allowlist 校驗。信任邊界為：**只有 first-party adapters（MVP：TwitchAdapter）才可在事件攜帶 overlay DisplayHints**。Plugin 可透過 `IPluginContext.Events.PublishAsync` 發布自訂事件（SC-10），但 **MVP plugin 自訂事件不含 overlay DisplayHints**（`OverlayModule` 不把 plugin 事件推送到 overlay SignalR group）；`OverlayModule` 僅訂閱 Domain 層定義的 7 個 MVP 事件型別（見 §4.4）。若日後 plugin 需要驅動 overlay，必須加入 URL allowlist 驗證（post-MVP）。

疊層直接從事件中讀取 `DisplayHints` — 在熱路徑中零額外的資料庫或 API 呼叫。

### 4.4 領域事件 (MVP 集合)

| 事件 | EventTypeKey | 觸發條件 |
|---|---|---|
| `UserSentMessageEvent` | `user.message` | 聊天訊息 |
| `UserFollowedEvent` | `user.followed` | 新追隨 |
| `UserDonatedEvent` | `user.donated` | Twitch Bits / YT SuperChat |
| `UserSubscribedEvent` | `user.subscribed` | 訂閱 |
| `UserGiftedSubscriptionEvent` | `user.gifted_sub` | 贈送訂閱 |
| `ChannelRaidedEvent` | `channel.raided` | 突襲 (目前僅限 Twitch 概念) |
| `RewardRedeemedEvent` | `reward.redeemed` | 頻道點數兌換 / 同等功能 |

所有事件都是實作 `IStreamEvent` 的**不可變 `record` 類型**。事件**不會持久化** — 僅寫入日誌 (LOG)（具有可配置的保留期）。

### 4.5 傳出 (回覆路由)

- 每個平台一個 `IPlatformChatSender`。Twitch → Twitch IRC, Simulation → 記憶體內接收端。
- `WorkflowAction.SendChatMessage` 預設在**源平台回覆**（來自事件的 `Platform` 欄位）。
- 操作可以透過明確的 `TargetPlatform` 設定**覆寫至特定平台**（如 `"twitch"`）；**「廣播到所有平台」為 post-MVP**（MVP 的 `TargetPlatform` 只接受具體平台名稱或 null；不接受 `"all"` sentinel）。
- `Simulation` 模式下的 `SendChatMessage` **不得只有 silent no-op**。即使未接到真實聊天室，也必須寫入可觀測的記憶體接收端 / Chat Outbox / 歷史檢視面，讓使用者能確認 rendered message、platform、channel、dedupKey、status（sent / skipped / failed）。
- `/overlay/chat` 的 event-driven 聊天事件顯示與 workflow `SendChatMessage` 是**兩個不同的資料流**。除非明確配置 bridge，否則不能把 chat overlay 視為 workflow chat output 的唯一驗證面。

---

### 4.6 WorkflowRule 模型

```
WorkflowRule
├── Id: ULID
├── Name: string           // 顯示標籤，**不唯一**；CLI/API list/show 以 Id 為主鍵（Name 可重複）
├── Priority: int          // 數字越小 = 優先級越高 (1 在 10 之前執行)
├── CreatedAt: DateTimeOffset
├── IsEnabled: bool
├── ConcurrencyMode: Serial (串行) | Parallel (並行)
├── MaxParallelism: int    // 僅在 ConcurrencyMode = Parallel 時適用；限制 API 衝擊範圍
│
├── Trigger (觸發器)
│   ├── EventTypeKey: string          // "user.message", "user.followed" 等
│   └── PlatformFilter: string?       // null = 所有平台；save-time normalize：trim → 空字串 → null；lowercase canonical（"Twitch" → "twitch"）
│
├── Conditions: List<IWorkflowCondition>   // AND 邏輯 — 必須全部通過
│   ├── UserRoleCondition (使用者身分條件)
│   │   ├── Roles: StreamRole 標記（`Subscriber | Moderator | Vip | Follower` — 從 adapter badge/role 欄位映射）
│   │   │   // **`CustomRoleId[]` 為 post-MVP**（需要自訂角色 CRUD + 查詢 port，不在 MVP 任務範圍）
│   │   └── Mode: HasAny | HasAll | NotHave
│   │
│   ├── MessageContentCondition (訊息內容條件) // 僅當事件攜帶純文字時適用
│   │   ├── MatchMode: PrefixMatch | ContainsMatch | FullRegex
│   │   ├── Pattern: string            // 例如 "!checkin", "hello", "^!\\w+"
│   │   │   // FullRegex：pattern 長度上限 512 字元；評估 timeout 500ms（Regex.Match with CancellationToken）
│   │   │   // 超時或 pattern 過長 → 條件結果為 false，記錄 warning；不拋例外
│   │   │   // FullRegex 在 rule 儲存時預先編譯驗證；無效 pattern → 400 + `INVALID_REGEX_PATTERN`
│   │   └── 非文字事件行為：event 無 MessageContent 欄位 → condition 評估為 **false**（跳過，不 throw）
│   │
│   └── CooldownCondition (冷卻條件)
│       ├── Scope: Global | PerUser
│       ├── DurationSeconds: int        // 有效範圍 [1, 86400]（最長 24h）；超出 → `INVALID_ACTION_CONFIG`
│       └── 持久化：**僅記憶體**（應用重啟後冷卻狀態重置）— MVP 可接受；如需持久冷卻，屬 post-MVP
│
└── Actions: List<IWorkflowAction>     // 按列表順序依序執行
    ├── SendChatMessageAction (發送聊天訊息)
    │   ├── Template: string           // 變數佔位符：{user.displayName}, {event.amount}
    │   │   // 最大長度 500 字元；超出 → `INVALID_ACTION_CONFIG`（save time 驗證）
    │   │   // 未知 placeholder（如 {event.unknown}）→ 保留原文（不替換、不 throw）；空值 placeholder → 空字串
    │   └── TargetPlatform: string?    // null = 來源平台；非 null 時 save-time 只驗非空白字串（不驗 adapter 是否啟用）
    │                                  // Runtime：若無 sender 已注冊該 platform → log warning + skip action（不 crash；adapter 可動態啟停）
    │
    ├── InvokeSubWorkflowAction (調用子工作流)
    │   └── WorkflowId: ULID
    │
    └── InvokePluginAction (調用外掛程式操作)      // 涵蓋：觸發特效、加點、播放音效等
        ├── PluginId: string
        ├── ActionId: string
        └── Params: IReadOnlyDictionary<string, JsonElement>
              // **反序列化注意：** `System.Text.Json` 將 JSON object/array value 反序列化為 `JsonElement`
              // 外掛程式不可直接 cast 為 int/string/bool — 應用 `.GetString()`, `.GetInt32()` 等
              // IPluginActionContext.Params 應定義為 `IReadOnlyDictionary<string, JsonElement>` 或包裝類，
              // 而非 `object`，以避免 runtime cast exception
```

**優先順序解析：** 按 `Priority ASC`，然後 `CreatedAt ASC`，最後 `Id ASC`（ULID 字典序，確保無 DB 排序不穩定問題）。

**並行語意：**
- `Serial` (預設)：**作用域為單一 WorkflowRule**（每個 rule 有獨立 queue，rule A 排隊不影響 rule B）。同一 rule 的事件一次執行一個。
- `Parallel`：同一 rule 的事件最多 `MaxParallelism` 個並行執行。`MaxParallelism` 有效範圍 `[1, 64]`；超出範圍在 rule 儲存時拒絕（400 + `INVALID_ACTION_CONFIG`）。
- 匹配同一事件的不同規則始終獨立執行（無跨規則序列化）。

**操作由外掛程式驅動（可熱插拔）：** `SendChatMessage` 和 `InvokeSubWorkflow` 是內建的。所有領域特定的操作（`TriggerEffect`, `AddPoints`, `PlaySound`）都是 `InvokePluginAction` — 它們需要載入相應的外掛程式。如果外掛程式缺失，操作會記錄警告並跳過。

**條件評估短路：** 第一個失敗的條件會停止評估（建議按「成本從低到高」的順序排列）。

---

### 4.7 平台適配器韌性 (G5)

**重連策略（完全在適配器內處理 — 應用層/領域層不知情）：**

```
IRC WebSocket 斷開：
  → 立即發布 PlatformConnectionChangedEvent { IsConnected: false, Reason: "reconnecting" }
  → 指數退避：1s → 2s → 4s → 8s → ... 最高 60s
  → 停機期間的訊息：靜默丟失 (IRC 是盡力而為服務)
  → 重連成功時：發布 PlatformConnectionChangedEvent { IsConnected: true }

EventSub WebSocket 斷開：
  → 立即發布 PlatformConnectionChangedEvent { IsConnected: false, Reason: "reconnecting" }
  → 相同的退避策略，並套用 ±20% jitter
  → Twitch 保證在 10 分鐘重連窗口內重播錯過的事件
  → 適配器接收重播事件並正常發布；僅對同一 (platform, sourceEventId) 在 dedup cache 內的 duplicate delivery 跳過；dedup cache 上限為 1000 entries 或 10 分鐘 TTL
  → 超過 10 分鐘：事件永久丟失 — 不自行建構重播機制
```

**連線狀態事件（新增至領域事件）：**

```csharp
public sealed record PlatformConnectionChangedEvent : IStreamEvent
{
    public string EventId { get; init; } = Ulid.NewUlid().ToString();
    public string EventTypeKey => "platform.connection_changed";
    public required string Platform { get; init; }
    public required bool IsConnected { get; init; }
    public string? Reason { get; init; }   // "reconnecting" | "auth_failed" | null
                                           // auth_failed = 平台 IRC/OAuth 認證失敗，非 Web host 身分驗證
    public StreamUser? User => null;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

UI 透過 SignalR 訂閱以顯示連線狀態指示器。`PlatformConnectionChangedEvent` 是**系統事件**，不公開於 `GET /api/event-types` 且不出現在 WorkflowRule 事件類型 dropdown — 預設沒有工作流規則可以觸發它（僅供 UI 狀態顯示用）。

---

### 4.8 工作流操作錯誤處理 (G6)

每個操作具有 `ErrorBehavior` 以及全域操作超時：

```csharp
public enum ErrorBehavior
{
    ContinueOnError,   // (預設) 失敗 → 記錄日誌 → 繼續下一個操作
    StopOnError,       // 失敗 → 停止此規則中的剩餘操作
    RetryOnError,      // 失敗 → 在放棄前按退避策略重試
}
```

每個 `IWorkflowAction` 基類攜帶：

```
ErrorBehavior: ErrorBehavior = ContinueOnError
MaxRetries:    int = 0     // 有效範圍 [0, 10]；超出 → `INVALID_ACTION_CONFIG`
BackoffMs:     int = 500   // 有效範圍 [100, 30000]；超出 → `INVALID_ACTION_CONFIG`
TimeoutMs:     int = 5000  // 有效範圍 [100, 60000]；超出 → `INVALID_ACTION_CONFIG`
                            // 超過此時間 CancellationToken 被取消；執行器停止等待
                            // .NET 無法強行終止非同步任務 — 外掛程式必須遵守 CancellationToken
```

**特殊情況：**

| 情況 | 行為 |
|---|---|
| 外掛程式缺失 | 警告日誌 → 跳過（**視為 skip，不計為 action error**；`ErrorBehavior` 不套用 — 外掛程式未載入屬 configuration issue，不同於 action 執行失敗；`StopOnError` 不因此停止後續 actions） |
| InvokeSubWorkflow 目標未找到 | 警告日誌 → 跳過 |
| 循環工作流引用 | 在規則儲存時偵測（靜態分析），進入資料庫前攔截 |
| Twitch API 429 | 對 `SendChatMessageAction` 使用 `RetryOnError` + 退避 |
| 操作超時 | 發訊號給 `CancellationToken` → 停止等待任務 → 視為錯誤 → 應用 `ErrorBehavior`。執行器不會中止任務執行緒。外掛程式必須協作觀察取消，否則底層工作可能在超時後仍完成。 |

---

### 4.9 配置儲存 (G7)

三層分離：

| 類型 | 儲存方式 | 熱載入 (Hot reload) | 備註 |
|---|---|---|---|
| 基礎設施配置 | `appsettings.json` | 需重啟 | 埠、檔案路徑、功能旗標 |
| 執行時期設定 | SQLite `SystemSettings` 表 | ✅ 即時生效 | 匯流排閾值、日誌 TTL、工作流限制 |
| OAuth 刷新權杖 | SQLite AES-256-GCM 加密，versioned envelope `v1:<Base64>`，金鑰 = 本地 `machine.key` | 需重啟 | 唯一需要保護的憑據 |
| 平台 client_id | `appsettings.json` (公開，可提交) | 需重啟 | 公開用戶端 — 無發行金鑰 (secret) |

**SystemSettings 資料表（CLI `config get/set` 的對象）：**

> **注意：** `SystemSettings` 並非所有 key 均可透過 `/api/config/{key}` 或 CLI `config get/set` 存取。`security.*`、`oauth.*` 等受保護 namespace 在 API 層攔截（見 §4.13 安全命名空間）。CLI `config get/set` 的操作範圍限縮於非受保護 key（如 `log.*`、`workflow.*`、`overlay.*`、`streaming.*`、`bus.*`）。

```sql
CREATE TABLE SystemSettings (
    Key       TEXT PRIMARY KEY,
    Value     TEXT NOT NULL,
    Category  TEXT NOT NULL,   -- "streaming" | "workflow" | "overlay" | "log" | "bus" | "oauth"
                               -- 注意："oauth" category 為受保護命名空間，不可透過 /api/config 或 CLI config get/set 存取
    UpdatedAt INTEGER NOT NULL
);
```

**OAuth 憑據模型（Twitch 公開用戶端 — PKCE）：**

```
無 Client Secret — Twitch 不為公開應用程式發行金鑰。

OAuth PKCE 流程：
  1. 應用程式生成 code_verifier + code_challenge + **cryptographically random state（32 bytes Base64Url）**
  2. 開啟瀏覽器 → Twitch 授權頁面（附帶 state 參數）
  3. 使用者授權 → 重定向到 localhost 回呼（帶回 code + state）
  4. 應用程式驗證 state 與步驟 1 相同、未過期（TTL 10 分鐘）且未使用（CSRF 防護）；不符、過期或已使用 → 拒絕，記 warning，不 exchange code
  5. 應用程式接收 auth_code → 交換（使用 code_verifier）取得權杖
  6. 加密儲存 refresh_token；僅在記憶體中保留 access_token
  
  OAuth callback listener 邊界：
  - 只接受 loopback（127.0.0.1 / ::1）請求
  - Host header 只接受 localhost:{port}、127.0.0.1:{port}、[::1]:{port}，拒絕其他 Host 以避免 DNS rebinding
  - Remote IP allowlist 與 Host header allowlist 必須同時通過；只滿足其中一項仍拒絕
  - 只接受預設 callback path（如 `/auth/callback`）；其他 path → 忽略
  - callback **只接受一次**（接收後立即關閉 listener）
  - access_token、authorization code、code_verifier、refresh_token plain value 不寫 log；refresh_token 以明文傳入 IOAuthTokenStore，由 Task 8 加密

憑據儲存：
  machine.key   → 首次執行時期生成，固定儲存在 OS 應用程式資料根目錄：
                    Windows: %AppData%\Vulperonex\machine.key
                    macOS  : ~/Library/Application Support/Vulperonex/machine.key
                    Linux  : ~/.local/share/Vulperonex/machine.key
                  （在 Windows/macOS 上使用 Environment.SpecialFolder.ApplicationData；
                   在 Linux 上使用 XDG_DATA_HOME）
                  **machine.key 路徑固定在 OS 應用程式資料根目錄，不隨
                  `appsettings.json → Database:Path` 的自訂 DB 路徑變動。**
                  若使用者將 DB 移至自訂路徑，machine.key 仍留在 OS app-data。
                  此設計確保 machine.key 在 DB 遷移或自訂儲存佈局下保持穩定。
                  絕不離開機器
                  **檔案權限（建立時立即設定）：**
                    Windows：目前使用者 ACL FullControl，移除繼承權限
                    Unix/macOS/Linux：chmod 0600（僅擁有者可讀寫）
                  違反此設定表示 machine.key 對其他 OS 使用者可見，等同於 refresh_token 洩漏
                  **chmod/ACL 設定失敗 → fail-fast（拋 `IOException`），不降級繼續。**
                  安全高於可用性：machine.key 無法保護時不應繼續加密/解密 OAuth token。
  refresh_token → AES-256-GCM 加密；作為 TEXT 儲存在 SystemSettings.Value 中
                  加密封裝（為未來遷移安全而版本化）：
                    "v1:" + Base64( nonce(12B) || ciphertext || tag(16B) )
                  （**注意：此 envelope 使用標準 Base64（含 `+`/`/`/`=`），非 Base64Url；解碼時使用 `Convert.FromBase64String`，不用 `WebEncoders.Base64UrlDecode`）
                  - 版本字首 "v1:" 允許未來在不破壞儲存資料的情況下變更格式
                  - 每次加密呼叫生成 12 字節的加密隨機 nonce
                  - 包含 16 字節的身份驗證標籤 (GCM 同時提供機密性 + 完整性)
                  - 金鑰 = machine.key 原始 32 字節 (無需額外的 KDF；金鑰已經是隨機的)
                  - **AAD = 設定鍵名 UTF-8 bytes**（即 `"oauth.twitch.refresh_token"`），傳入 `AesGcm.Encrypt()`；解密時重新傳入相同鍵名；AAD 不加入 envelope 儲存。繫結密文至鍵名，防止跨鍵複製攻擊。
                  - 儲存在 SystemSettingKey "oauth.twitch.refresh_token" 下

跨設備行為：
  新機器安裝 → machine.key 缺失 → 解密失敗
  → 應用程式偵測到失敗 → 提示重新授權 → 再次執行 OAuth PKCE
  → 這是預期的正確行為，而非錯誤
```

**熱載入介面：**

```csharp
public interface ISystemSettingsService
{
    T Get<T>(string key, T defaultValue);
    Task SetAsync(string key, string value);
    IObservable<SettingChangedEvent> Changes { get; }  // 變更時通知訂閱者
}
```

---

### 4.10 模組生命週期 (G8)

每個模組（`WorkflowModule`, `OverlayModule`, `MemberModule`）都實作 `IHostedService`。在 `StartAsync` 時獲取訂閱，在 `StopAsync` 時釋放訂閱並等待進行中的 handler 完成（不丟棄尚在處理的事件）。**若 `ct` 在等待完成前觸發取消 → 記錄 warning log「shutdown timeout: {count} handlers still running」後強制返回（不拋例外）；handler 可能在行程關閉後繼續執行，屬系統限制，不視為錯誤。**

```csharp
public class WorkflowModule : IHostedService
{
    private readonly IStreamEventBus _bus;
    private readonly List<IDisposable> _subscriptions = new();

    public Task StartAsync(CancellationToken ct)
    {
        _subscriptions.Add(_bus.Subscribe<IStreamEvent>(HandleAsync));
        // Subscribe<T> 使用 assignable match（協變）：
        // Subscribe<IStreamEvent> 接收所有事件；Subscribe<UserSentMessageEvent> 只接收該具體型別。
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _subscriptions.ForEach(s => s.Dispose());
        await _pendingWork.WaitForCompletionAsync(ct);
    }
}
```

**啟動順序（由 DI 註冊順序控制）：**

```
1. 基礎設施 (Infrastructure)  — 資料庫上下文, EF 遷移
2. IStreamEventBus            — 必須在任何訂閱者之前存在
3. ISystemSettingsService     — 需先於 cache（cache 讀取容量/TTL 設定）
4. IPlatformUserInfoCache     — 從 ISystemSettingsService 讀取 L1 容量/TTL 初始值
5. 模組 (Modules)             — MemberModule → OverlayModule → WorkflowModule
6. 適配器 (Adapters)           — TwitchAdapter (僅在模組就緒後開始發布)
7. Web / SignalR Hub
```

**關閉順序則相反。** 適配器首先停止（不再有新的傳入事件） → 模組清空進行中的工作 → 基礎設施關閉。

---

### 4.11 資料庫遷移策略 (G9)

**雙模式：啟動時自動執行 + 顯式 CLI 執行。**

```
啟動時 (始終)：
  await db.Database.MigrateAsync()
  → 僅自動執行增量遷移
  → 應用程式絕不會以過時的架構啟動

CLI (手動控制)：
  vulperonex db migrate        → 執行掛起的遷移
  vulperonex db status         → 列出已應用 / 掛起的遷移
  vulperonex db rollback <id>  → 回滾（需要確認提示）
```

**遷移安全規則：**

| 遷移類型 | 啟動時自動執行 |
|---|---|
| 新增資料表 (ADD TABLE) | ✅ 是 |
| 新增欄位 (可為空或具有預設值) | ✅ 是 |
| 新增欄位 (NOT NULL, 無預設值) | ❌ 僅限 CLI |
| 刪除欄位 / 刪除資料表 | ❌ 僅限 CLI — 需要顯式確認 |
| 重新命名欄位 / 資料表 | ❌ 僅限 CLI |

**重要提示：** `EF Core MigrateAsync()` 會執行所有掛起的遷移，而不檢查它們是否具有破壞性。上表描述的是**策略**，而非自動強制執行。強制執行透過以下方式實現：

1. **CI 遷移分類器** — `Vulperonex.Tests.Architecture` 中的一項測試，透過**實例化**每個 `Migration` 類別並針對真實的 `MigrationBuilder` 實體**執行**其 `Up(MigrationBuilder)` 方法來偵測破壞性遷移，然後檢查 `MigrationBuilder.Operations` 中的破壞性操作類型：
   ```csharp
   var builder = new MigrationBuilder(activeProvider: "Microsoft.EntityFrameworkCore.Sqlite");
   migration.Up(builder);  // 呼叫實際的 Up() 方法
   var destructive = builder.Operations.Any(op =>
       op is DropTableOperation or DropColumnOperation
           or RenameTableOperation or RenameColumnOperation or AlterColumnOperation
       || op is SqlOperation sql
           && Regex.IsMatch(sql.Sql, @"\b(DROP|DELETE|TRUNCATE|ALTER|RENAME)\b",
               RegexOptions.IgnoreCase));
   ```
   如果發現任何破壞性操作且該遷移類別未標記 `[DestructiveMigration]` 屬性，則測試失敗。這種方法避免了不可靠的方法體反射，並產生了 EF Core 將執行的實際 `Operations` 列表。**注意：raw SQL 中只要含 `ALTER` 即視為 review-required（保守策略）** — 涵蓋 `ALTER TABLE ... DROP COLUMN`、`ALTER TABLE ... RENAME` 等所有 ALTER 變體；不做進一步細分。
2. **PR 審查要求** — 任何標記為 `[DestructiveMigration]` 的遷移在合併前都需要手動審查。

EF Core 遷移檔案在儲存庫中的路徑為 `src/Vulperonex.Infrastructure/Migrations/`。

**SQLite 檔案位置（透過 `appsettings.json → Database:Path` 配置）：**

```
Windows : %AppData%\Vulperonex\vulperonex.db
Linux   : ~/.local/share/Vulperonex/vulperonex.db
macOS   : ~/Library/Application Support/Vulperonex/vulperonex.db
```

---

### 4.12 EventTypeKey 類型安全 (G10)

基於字串的 `EventTypeKey` 很靈活（外掛程式可以定義新類型）但很脆弱 — 拼寫錯誤會導致規則無聲地失效。

**保護機制：`IStreamEventTypeRegistry` + 儲存時驗證。**

```csharp
public interface IStreamEventTypeRegistry
{
    void Register(string key, string description, bool isSystemEvent = false);
    bool IsKnown(string key);             // 含 system events（用於路由/dispatch）
    bool IsKnownForWorkflow(string key);  // 排除 system events（用於 WorkflowRule 驗證）
    IReadOnlyList<RegistryDescriptor> GetAll();  // 回傳 RegistryDescriptor（不含 isSystemEvent=true 的項目）
                                                  // API endpoint 再投影成 EventTypeDescriptor（補 IsSimulatable）
}

// Registry 內部儲存型別（不暴露至 API 層）
internal record RegistryDescriptor(
    string Key,
    string Description,
    bool IsSystemEvent);  // true for platform.connection_changed; excluded from GetAll()

// API DTO — GET /api/event-types endpoint 回傳（由 endpoint 從 RegistryDescriptor 投影）
// 不暴露 IsSystemEvent（GetAll() 已排除；前端不應以此欄位判斷 system event）
public record EventTypeDescriptor(
    string Key,
    string Description,
    bool IsSimulatable);  // 由 endpoint 根據靜態 alias map 補值（不由 Register() 傳入）
                          // chat→user.message, follow→user.followed, sub→user.subscribed = true; 其餘 = false
```

**重複 key 註冊行為：** 相同 key 重複 `Register(...)` 為 **first-wins**；後到者 no-op。所有 metadata 欄位（description、isSystemEvent）衝突時保留先到者，記錄 warning log，不拋例外。TwitchAdapter 與 SimulationAdapter 都會呼叫 `Register("user.message", ...)` — 兩者使用相同 canonical description（由 `StreamEventDescriptions` constants 提供，見下方），因此先到者即正確值，不觸發 warning。`GetAll()` 每 key 只出現一次。

**`GetAll()` 行為：** 排除所有 `isSystemEvent=true` 的 key（如 `platform.connection_changed`）；回傳結果依 `Key` 字母排序；每個 key 最多一筆。`IsSimulatable` 欄位由 `GET /api/event-types` endpoint 在回傳前根據靜態 simulate alias map 補值（不存入 registry）。

**`StreamEventDescriptions` 常數類（`Vulperonex.Domain`）：** 所有 adapter 使用相同常數作為 description，確保 first-wins duplicate 不觸發 warning：
```csharp
public static class StreamEventDescriptions
{
    public const string UserMessage   = "使用者發送了聊天訊息";
    public const string UserFollowed  = "使用者追隨了頻道";
    // … 其餘 7 個 MVP event descriptions
}
```

```csharp
// 原介面（無 isSystemEvent 參數）
```

適配器和外掛程式在 `StartAsync` / `InitializeAsync` 期間註冊它們的鍵：

```csharp
// TwitchAdapter.StartAsync()
_registry.Register("user.message",  "使用者發送了聊天訊息");
_registry.Register("user.followed", "使用者追隨了頻道");

// MyPlugin.InitializeAsync()
_registry.Register("plugin.my_plugin.event", "我的外掛程式自訂事件");
```

**安全檢查點：**

| 時機 | 行為 |
|---|---|
| WorkflowRule 儲存 (API / CLI) | 呼叫 `IsKnownForWorkflow(key)` — 未知或系統事件（`platform.connection_changed`）均拒絕；不使用 `IsKnown()`（`IsKnown` 包含系統事件，會誤放行） |
| UI 規則編輯器 | 僅顯示已註冊鍵的下拉選單 — 無自由文字輸入 |
| 從資料庫載入具有未知鍵的規則 (外掛程式已解除安裝) | 記錄警告，跳過規則 — 不崩潰 |

資料庫中的未知鍵會優雅地降級，而非致命錯誤 — 允許移除外掛程式而不損壞應用程式。

---

### 4.13 WorkflowRule 編輯介面 (G11)

REST API 是唯一規範的寫入路徑。UI 和 CLI 都呼叫 API — 兩者都不直接寫入資料庫。伺服器永遠以 loopback-only 執行，無需身分驗證。

```
REST API  ← 唯一寫入點，強制執行所有驗證
              (EventTypeKey 註冊表檢查, 循環引用偵測, 架構驗證)
  ↑
  ├── Vue UI   — 規則建構表單 → Axios → API
  └── CLI      — vulperonex rule list / show / enable / disable / delete → HTTP → API
```

相關 CLI 指令：
```bash
vulperonex rule list
vulperonex rule show    <ruleId|prefix|--name <name>>
vulperonex rule enable  <ruleId|prefix|--name <name>>
vulperonex rule disable <ruleId|prefix|--name <name>> [--yes]
vulperonex rule delete  <ruleId|prefix|--name <name>> [--yes]
```

- `<ruleId>` 接受完整 id、唯一 id prefix 或 `--name <name>`（互斥於 positional id）。多重命中 → `AMBIGUOUS_ID` + 候選表；零命中 → `NOT_FOUND`。
- 破壞性操作（`disable` / `delete`）於互動 REPL 印 `[y/N]` prompt；非互動模式需帶 `--yes`，否則 `CONFIRMATION_REQUIRED`。
- CLI 解析與確認流程設計凍結於 [`docs/phases/phase-5_5-rapid-test/cli-id-resolution-decision.md`](phases/phase-5_5-rapid-test/cli-id-resolution-decision.md)。新增 error codes：`MISSING_ARGS` / `AMBIGUOUS_ID` / `NOT_FOUND` / `CONFIRMATION_REQUIRED` / `CANCELLED`。

複雜規則創建（多條件、多操作）在 MVP 階段僅限 UI。CLI 負責列出/顯示/啟用/禁用/刪除。

---

### 4.13.1 完整 MVP REST API 介面

所有 UI 和 CLI 均專門透過 REST 存取 Web 主機，兩端用戶端均無直接資料庫存取權限。伺服器永遠以 loopback-only 執行（IPv4 `127.0.0.1` + IPv6 `::1`），無需身分驗證。

| 群組 | 方法 | 路徑 | 應用層埠 (Port) |
|-------|--------|------|-----------------|
| WorkflowRule | GET | `/api/rules` | `IWorkflowRuleQueryService` — **MVP 無分頁**，回傳全部 rule；排序：`Priority ASC, CreatedAt ASC, Id ASC` |
| WorkflowRule | GET | `/api/rules/{id}` | `IWorkflowRuleQueryService` |
| WorkflowRule | POST | `/api/rules` | `IWorkflowRuleRepository` — **201 Created** + `Location: /api/rules/{newId}` header；body 含新建 rule |
| WorkflowRule | PUT | `/api/rules/{id}` | `IWorkflowRuleRepository` — 200 OK；body id 與 route id 不一致 → **400 `INVALID_RULE_ID_MISMATCH`**（不靜默忽略 body id，避免意外覆蓋錯誤 rule）|
| WorkflowRule | DELETE | `/api/rules/{id}` | `IWorkflowRuleRepository` — **204 No Content**（無 body）|
| WorkflowRule | POST | `/api/rules/{id}/enable` | `IWorkflowRuleRepository` — 200 OK；更新 `IsEnabled=true` + `UpdatedAt` |
| WorkflowRule | POST | `/api/rules/{id}/disable` | `IWorkflowRuleRepository` — 200 OK；更新 `IsEnabled=false` + `UpdatedAt` |
| 事件類型 | GET | `/api/event-types` | `IStreamEventTypeRegistry` |
| 模擬 | POST | `/api/simulate/{eventType}` | `ISimulationAdapter` — `{eventType}` 僅限短別名：`chat` / `follow` / `sub` |
| 配置 | GET | `/api/config/{key}` | `ISystemSettingsService` |
| 配置 | PUT | `/api/config/{key}` | `ISystemSettingsService` |
| 成員 | GET | `/api/members` | `IMemberQueryService` |
| 成員 | GET | `/api/members/{id}` | `IMemberQueryService` |

**模擬別名 → EventTypeKey 映射**（由端點強制執行，而非呼叫端；使用 §4.4 中的規範鍵）：
- `chat` → `user.message`
- `follow` → `user.followed`
- `sub` → `user.subscribed`

僅接受別名值；拒絕原始 EventTypeKey 字串，以保持 CLI/REST/WorkflowRule 命名清晰。

**配置鍵註冊表：** `ISystemSettingsService` 對在 `SystemSettingKey` 中定義的類型化常數進行操作（而非任意字串）。任何註冊表中缺失的鍵都會返回 `UNKNOWN_CONFIG_KEY`。新設定需要新增常數 — 不允許自由文字鍵。

**Config key 大小寫規則：** 所有 key 為 **canonical lowercase**（`log.min_level`、`oauth.twitch.refresh_token`）。API 在 prefix denylist 比對與 registry lookup 前，均先將傳入 `{key}` 做 `ToLowerInvariant()` 正規化 — `OAuth.Twitch.Refresh_Token` 與 `oauth.twitch.refresh_token` 視為同一 key，同樣觸發 403 denylist。DB 儲存時亦使用 lowercase。

**攔截優先順序（重要）：** `/api/config/{key}` 請求依序執行以下檢查：(1) key 正規化（`ToLowerInvariant()`）；(2) **受保護 prefix denylist**（`security.*` → 403 `CONFIG_KEY_SECURITY_NAMESPACE`；`oauth.*` → 403 `OAUTH_CREDENTIAL_NAMESPACE`）；(3) **registry lookup**（`UNKNOWN_CONFIG_KEY`）。prefix denylist **先於** registry 執行 — 未知的 `oauth.*` key（如未來新增的 `oauth.unknown.refresh_token` 尚未在 registry 中）同樣回 403，不回 400 `UNKNOWN_CONFIG_KEY`。

**安全命名空間配置鍵** (`security.*`) 在 `/api/config/{key}` 被**攔截** — 返回 403 + `CONFIG_KEY_SECURITY_NAMESPACE`。**OAuth 憑據鍵** (`oauth.*`，例如 `oauth.twitch.refresh_token`) 在 `/api/config/{key}` 同樣被**攔截** — 返回 403 + `OAUTH_CREDENTIAL_NAMESPACE`（OAuth token 只透過 PKCE 流程寫入，不允許 REST CRUD）。對於 MVP，Twitch OAuth 憑據由任務 12 中的 PKCE 流程儲存，無法透過配置端點進行 CRUD 存取。`/api/settings/security/*` 是保留的路徑字首，MVP 階段不新增 CRUD 端點；Kestrel loopback-only binding 本身已保護這些路徑，無需額外 middleware。

---

### 4.14 疊層架構 (G12)

`OverlayModule` 訂閱相關的領域事件，將其轉換為 `OverlayPayload` DTO，並推送到 SignalR 組。前端疊層頁面作為 OBS 瀏覽器源連線。

**瀏覽器源 URL：**
```
http://localhost:5001/overlay/chat      — 滾動聊天
http://localhost:5001/overlay/alerts    — 追隨 / 訂閱 / 突襲警報
http://localhost:5001/overlay/member    — 成員卡片顯示
```

每個 URL 都是一個獨立的 Vue 路由，在掛載時連線到其 SignalR 組。不需要身份驗證（OBS 必須直接連線）。

**聊天 overlay 樣板系統：**

- `/overlay/chat` 必須支援**多個內建樣板 / preset**，至少包含 Vulperonex 預設樣板；內建樣板需可對應「單一樣板目錄 / 單一樣板封包」概念，而不是寫死單版面。
- 樣板選擇必須是**設定層級**能力，而不是要求使用者直接改前端原始程式碼；後續可擴充為樣板清單、預覽、匯入 / 匯出。
- 樣板渲染仍必須遵守 MVP 安全界線：使用 DTO 白名單與 text binding；**不得以 `v-html` 或任意 raw HTML 直接穿透 event payload**。
- **OneComme 相容屬於擴充功能 / 外掛程式類型能力，不屬於 core 直接內建整合。** Core 只需提供可擴充的樣板 preset / package contract；OneComme 相容可透過外掛、樣板匯入器、或 adapter 套件實作。
- 以 **OneComme** 作為優先相容目標之一。目的不是 1:1 複製其內部實作，而是提供足夠接近的樣板結構 / 匯入映射 / 相容契約，降低既有 OneComme 使用者的遷移成本，同時維持 core 與第三方樣板生態的邊界。

#### 4.14.1 Overlay Preset Contract (Vue 預設 + 自訂 HTML 擴充)

**動機：** 一般實況主想客製 overlay 視覺，不應被迫安裝 Node.js / pnpm / Vite。同時 Vulperonex 仍要提供高品質預設 Vue 版本，並支援第三方擴充（含未來 OneComme 範本匯入）。

**雙軌渲染管道：**

| 管道 | 適用對象 | 路徑模式 | 實作位置 |
|------|---------|----------|----------|
| **內建預設 Preset** | 一般使用者，零設定可用 | `/overlay/chat.html` (聊天)、`/overlay/member-card.html` (會員)、`/overlay/alerts` (警報) | `src/frontend/public/overlay/**` (靜態 HTML/JS) + `src/frontend/src/views/overlay/**` (警報 Vue 元件) |
| **靜態 HTML 覆蓋 (Override)** | 進階使用者 / 第三方樣板 | `/overlay/custom/{slug}.html` | 後端 `wwwroot/overlay/` 目錄；對應原始程式碼為 `src/frontend/public/overlay/`（Vite build 時複製） |

**Preset 選擇優先順序（後端解析）：**

1. URL 直接指向 `*.html` → 載入靜態檔案
2. URL 指向 `/overlay/{hub}` 且 `overlay.{hub}.preset` 系統設定指向 `custom:{slug}` → redirect 到 `/overlay/custom/{slug}.html`
3. URL 指向 `/overlay/{hub}` 且 `overlay.{hub}.preset` 指向內建 preset key：
   - `chat` 與 `member` → 解析至內建靜態 HTML `/overlay/chat.html` 或 `/overlay/member-card.html`（並帶入對應 query string 如 `?preset={key}`）
   - `alerts` → 載入對應 Vue preset 元件（或 redirect 到 `/overlay/alerts`）
4. 預設 fallback → `/overlay/chat.html` 的 `vulperonex-default` 預設（聊天）、`/overlay/member-card.html` 的 `rotan-checkin` 預設（會員）、Vulperonex 預設警報 Vue 頁面（警報）

**自訂 HTML 上傳（Phase 7C — 此 PR 之後）：**

- 管理 UI 在「Overlay 設定」頁提供 HTML/CSS/JS bundle 上傳介面。
- Bundle 格式：單一 `.html` 檔（自包含 inline CSS/JS）或 zip 含 `index.html` + 相對引用資源。
- 上傳目標：`wwwroot/overlay/custom/{slug}/`（slug 由檔名 sanitize，禁止 `..`、絕對路徑、非 `[a-z0-9-]` 字元）。
- 安全：
  - 上傳者必須為已通過 admin auth 的本機使用者（loopback only，沿用 Phase 6 安全契約）。
  - 上傳檔案大小上限 5MB。
  - Zip 解壓使用 path traversal 防護，逐檔驗證解出路徑必在目標目錄內。
  - 上傳檔案 **不執行伺服器端 sanitization**（HTML overlay 預期含 script），但檔案僅由 loopback OBS 載入，不對外曝光。

**靜態 HTML SignalR 資料契約：**

靜態 HTML 透過 `js/overlay-common.js` 提供的 `OverlayCommon.initSignalRConnection(hubUrl, handlers)` 連線至 `/hubs/overlay/{chat|alerts|member}`。事件 payload 結構**與 Vue preset 共用同一 DTO 白名單**（見 4.14 事件 → 疊層映射、Phase 6 Task 15 反射驗證）。任何新增欄位必須先過反射測試。

**Member Card in Chat Overlay（cross-hub 嵌入）：**

當會員觸發 chat 訊息時，後端 `OverlayModule` 在 chat hub payload 額外帶可選 `memberSnapshot` 欄位（DTO 同 member hub 白名單，excludes `memberId`/`totalLoyalty`/`linkedPlatforms`）。前端 / 靜態 HTML preset 可選擇是否渲染 inline 會員卡 chip（顯示頭像 + 簽到次數）。

控制旗標：`overlay.chat.show_member_card`（bool，預設 `false`，於系統設定切換）。預設 false 以維持 KapChat 的極簡視覺。

**OneComme 範本匯入（外掛擴充路徑，非 core）：**

- Core 僅提供「自訂 HTML 上傳」契約；OneComme 範本匯入由獨立 plugin (`Vulperonex.Plugins.OneCommeBridge`) 實作。
- Plugin 職責：解析 OneComme `template.html` + `template.css`，映射 OneComme `comment.*` 變數到 Vulperonex 事件 DTO 欄位，產生 standalone HTML 並透過上傳契約落地到 `wwwroot/overlay/custom/oc-{slug}/`。
- 映射表與 OneComme 變數對照詳列於 `docs/plugins/onecomme-bridge.md`（待寫）。

**內建 Preset 視覺基線：**

| Preset | Hub | 視覺特色 | 來源啟發 |
|--------|-----|---------|----------|
| `kapchat` (預設) | chat | 透明無框、單行緊湊、`text-shadow` 描邊保證遊戲畫面可讀性。徽章 → 名稱 → 冒號 → 內容 | nightdev.com/kapchat |
| `compact` | chat | 兩行緊縮、最近 10 則 | Vulperonex 自製 |
| `rotan-checkin` (預設) | member | 紫金燙金流光邊框 + SVG 爪印章 + 網點圖騰背景；左頭像/名稱、右 10 格集章 grid | menber_byRotan（重寫，未直接引用任何原始資產） |

**會員集章卡 Controller（admin 設定）：**

| 設定 key | 型別 | 預設 | 說明 |
|---------|------|------|------|
| `overlay.member.background_url` | string (URL) | 空 | 卡片背景圖。空值則用內建漸層 |
| `overlay.member.stamp_url` | string (URL) | 空 | 自訂印章圖。空值則用內建 SVG 爪印 |
| `overlay.member.stamps_per_round` | int | 10 | 一輪集滿格數 |
| `overlay.chat.show_member_card` | bool | false | 是否在 chat overlay 內嵌會員卡 chip |
| `overlay.chat.preset` | string | `kapchat` | Chat preset key（內建 key 或 `custom:{slug}`） |
| `overlay.member.preset` | string | `rotan-checkin` | Member preset key |

**URL 安全（前端 sanitization）：** 任何由設定注入到 CSS `url()` 的值，僅接受 `https?:` 或 `data:image/(png\|jpe?g\|gif\|svg+xml\|webp);` scheme，並禁止含 `"`、`'`、`(`、`)`、`\`、`;` 等可跳出 `url()` 的字元（防 CSS injection）。

**Twitch Client ID 設定 namespace 歸屬（ADR）：**

`twitch.client_id` 不屬於 `oauth.*` 受保護命名空間（refresh token 才屬於 `oauth.*`，由 PKCE 流程獨家寫入）。`twitch.client_id` 為一般使用者可透過 admin UI 設定的公開值（OAuth client_id 在 PKCE public client 流程下本來就會曝光在前端授權 URL），因此歸入一般 `twitch.*` namespace 允許 `/api/config` CRUD。Authorization gate（admin only + loopback only）仍由 Phase 6 既有安全契約覆蓋。

**事件 → 疊層映射：**

| 領域事件 / Workflow Action | 主要疊層目標 | 可選嵌入點 |
|---|---|---|
| `UserSentMessageEvent` | `/overlay/chat` | `memberSnapshot` 帶入 chat payload（依 `overlay.chat.show_member_card`）|
| `UserFollowedEvent`, `UserSubscribedEvent`, `UserGiftedSubscriptionEvent`, `ChannelRaidedEvent` | `/overlay/alerts` | — |
| `TriggerCheckInAction` 執行成功 | `/overlay/member` 主視覺集點卡（Phase 7D 起 first-class）| 同步 `memberSnapshot` 出現在後續該會員的 chat payload |
| *(未來 `SystemEvent` — 抽獎等)* | `/overlay/member` 視 preset 而定 | — |

疊層頁面從事件負載中讀取 `DisplayHints` 以獲取頭像、顏色、徽章 — 在渲染路徑中無額外的 API 呼叫。

---

#### 4.14.2 CheckIn → Member Overlay 綁定（Phase 7D）

**背景：** Phase 7C 已建立 `MemberOverlayView` + member-card preset + `OverlayMemberHub`，但 `TriggerCheckInActionExecutor` 僅寫入 SQLite `MemberStreamState`，從未發布事件給 `OverlayEventForwarder`，使 `/overlay/member` 在實際運作時收不到任何 push。Phase 7C cross-hub chat embed (`memberSnapshot`) 也只在 chat 事件路徑查 DB，與 checkin action 無連動。

**Phase 7D 設計：**

1. **新增領域事件 `MemberCheckedInEvent`**（於 `Vulperonex.Domain.Events`）：
   - 欄位：`EventId`、`OccurredAt`、`Platform`、`PlatformUserId`、`DisplayName`、`AvatarUrl?`、`CheckInCount`、`TotalLoyalty`、`RoundIndex`、`StampSlotInRound`。
   - `RoundIndex = ceil(CheckInCount / overlay.member.stamps_per_round)`；`StampSlotInRound = ((CheckInCount - 1) mod stampsPerRound) + 1`。
   - 由 `TriggerCheckInActionExecutor` 在 `IncrementCheckInAsync` 成功後 publish 到 `IStreamEventBus`。
2. **`OverlayEventForwarder` 訂閱 `MemberCheckedInEvent`**，映射為 `OverlayMemberPayload` 推到 `OverlayMemberHub` group，並寫入 `IOverlayHistoryService<OverlayMemberPayload>`。
3. **Chat embed sync 重用同一路徑**：chat hub 仍在 `UserSentMessageEvent` 處理路徑查 member cache 取 snapshot（既有 Phase 7C 行為），不依賴 `MemberCheckedInEvent`，但 cache TTL 必須短於 stamps_per_round 週期，使 chat chip 在 checkin 後立即反映新次數。`PlatformUserInfoCache` 既有 TTL 確認。
4. **顯示控制：**
   - `/overlay/member` 預設**啟用**接收（OBS browser source 主要落點）。
   - `/overlay/chat` 是否內嵌 chip 由 `overlay.chat.show_member_card`（bool，預設 false）控制（既有 Phase 7C 設定）。
   - 兩條路徑互不阻擋；任一單獨啟用皆可。
5. **DTO 白名單擴充：** `OverlayMemberPayload` 新增 `RoundIndex`、`StampSlotInRound` 欄位後，反射測試（既有 `OverlayDtoWhitelistTests`）必須同步擴充並維持 `memberId/totalLoyalty/linkedPlatforms` 排除規則。`TotalLoyalty` **不**進 overlay payload（仍敏感）；overlay 只看 `CheckInCount`。
6. **CLI 端：** `simulate checkin` CLI 子指令需發布 `MemberCheckedInEvent`（而非直接呼叫 repository），以便走完整 overlay 推播鏈路驗證。

**驗證：**
- 反射測試：`OverlayMemberPayload` JSON key set 精確符合新白名單。
- Integration test：`TriggerCheckInActionExecutor` 執行後，`OverlayMemberHub` group 收到 `OverlayMemberPayload`，且 history endpoint 可查到。
- Browser manual：simulate checkin → `/overlay/member` 在 5 秒內顯示卡片。

---

#### 4.14.3 自訂 HTML Overlay 編輯與部署 Pipeline（Phase 7D 取代 Phase 7C 純 zip upload）

**背景：** Phase 7C 已落地 `POST /api/overlay/custom-presets` 純 zip 上傳，但有兩個結構性問題：

- **無法驗證樣板合法性**：上傳即落地 `wwwroot/overlay/custom/{slug}/`，HTML 是否能正常掛 SignalR / 是否符合 DTO 契約完全靠使用者自己跑 OBS 才知道。
- **使用者修改成本高**：改一個 CSS 顏色就要重新打包 zip 再上傳，無線上 iterate 體驗。

Phase 7D 引入**雙模式 pipeline**：

| 模式 | 入口 | 目標 |
|------|------|------|
| **線上 Monaco 編輯器 (主)** | `/admin/overlay-editor` | 線上編輯 HTML/CSS/JS，draft/production 雙環境，iframe live preview，部署前 lint+probe |
| **Zip upload (fallback)** | 既有 `POST /api/overlay/custom-presets` | 整包匯入第三方樣板。落地後可在 Monaco editor 開啟繼續調 |

**檔案存放結構：**

```
wwwroot/overlay/custom/{slug}/
├── production/        # 已部署版本，OBS 載入路徑：/overlay/custom/{slug}/index.html → /overlay/custom/{slug}/production/index.html
│   ├── index.html
│   ├── styles.css
│   └── ...
├── draft/             # 草稿版本，編輯中。預覽路徑：/overlay/custom/{slug}/draft/index.html
│   ├── index.html
│   └── ...
└── history/           # 部署歷史，留最近 N 份（預設 10）
    └── {iso-timestamp}/
```

**API 增補：**

| Method | Path | 用途 |
|--------|------|------|
| `GET` | `/api/overlay/custom-presets/{slug}/files` | 列 slug 內所有相對檔案路徑 + draft/production 差異 |
| `GET` | `/api/overlay/custom-presets/{slug}/files/{path}?env=draft\|production` | 讀單檔內容 (UTF-8 text) |
| `PUT` | `/api/overlay/custom-presets/{slug}/files/{path}` body=raw | 寫單檔到 draft |
| `DELETE` | `/api/overlay/custom-presets/{slug}/files/{path}` | 刪 draft 單檔 |
| `POST` | `/api/overlay/custom-presets/{slug}/validate` | 對 draft 跑 validation gate，回 issues list |
| `POST` | `/api/overlay/custom-presets/{slug}/deploy` | draft → production 原子複製，舊 production 移到 history/{ts} |
| `POST` | `/api/overlay/custom-presets/{slug}/rollback?to={ts}` | history/{ts} → production |
| `GET` | `/api/overlay/custom-presets/{slug}/history` | 列 history 時間戳 + size |

**Validation Gate：**

部署前 `POST /validate` 必過，否則 `POST /deploy` 回 422 + issues list。檢查項：

1. **檔案結構：** `index.html` 必存在於 draft 根目錄。
2. **HTML 語法：** 用 `AngleSharp` parse `index.html`，parse error 阻斷。
3. **CSS 語法：** 用 `ExCSS` parse 所有 `*.css`，parse error 阻斷。
4. **JS 語法：** 用 `Jint` 嘗試 parse 所有 `*.js`（只 parse，不執行），syntax error 阻斷。
5. **SignalR contract probe：** 對 `index.html` 內所有 `<script>` 與外部 `*.js` regex 偵測：
   - 必須出現 `OverlayCommon.initSignalRConnection(` 或 `signalR.HubConnectionBuilder` 字串（確認有掛 hub）。
   - 必須引用至少一個 `/hubs/overlay/{chat|alerts|member}` URL pattern。
   - 缺一者降為 warning，不阻斷部署（樣板可能是純展示）。
6. **檔案大小：** 單檔上限 2MB，整 slug 上限 10MB（含 history）。超過阻斷。
7. **路徑安全：** 所有檔案路徑必相對且不含 `..`；`PUT /files/{path}` server-side 重新驗證。
8. **外部資源警告：** HTML/CSS 內 `<script src="http...">`、`<link href="http...">`、`@import url(http...)`、`url(http...)` 等外部 URL 一律列 warning（不阻斷），提醒實況主離線環境會失效。

**Draft/Production 隔離：**

- Draft 編輯不影響 OBS 載入的 production。
- `GET /overlay/custom/{slug}/index.html`（無 path 字尾）統一映射到 `production/index.html`。OBS 不直接觸碰 draft。
- 預覽 iframe 走 `/overlay/custom/{slug}/draft/index.html`，與 production 隔離。
- Deploy 為**原子操作**：先驗 history 寫入完成，再把 draft 內容**整目錄複製**到 production（不是 rename，避部分檔案失效）；複製完成才回 200。

**Zip upload fallback 整合：**

- `POST /api/overlay/custom-presets` 既有 zip upload 仍可用。解壓縮目標改為 `wwwroot/overlay/custom/{slug}/draft/`（不直接 production）。
- 上傳後自動跑一次 validation，回傳結果到管理 UI。
- 使用者可在 Monaco editor 開啟調整，按 deploy 才上線。

**安全：**
- 沿用 Phase 6 loopback-only 契約。
- 所有 path 參數需經 slug + relative path sanitize（無 `..`，無絕對路徑，無控制字元）。
- 寫檔前驗最終絕對路徑必在 `wwwroot/overlay/custom/{slug}/draft/` 內。
- History 留存上限以筆數（10）+ 整 slug 大小（10MB）雙重防爆。

---

---

### 4.15 日誌結構 (G13)

**使用具有三個接收端 (Sinks) 的 Serilog：**

```
Serilog
├── 接收端: Console       — 有顏色的，開發使用，預設級別：Debug
├── 接收端: Rolling file  — 每日輪換，預設保留：7 天
└── 接收端: SQLite 資料表 AppLogs — 應用程式內日誌檢視器，預設保留：30 天
```

**每個日誌條目豐富了結構化上下文欄位：**

```csharp
Log.ForContext("EventTypeKey",   evt.EventTypeKey)
   .ForContext("Platform",       evt.Platform)
   .ForContext("MemberId",       memberId)
   .ForContext("WorkflowRuleId", ruleId)
   .ForContext("ActionType",     actionType)
   .Information("工作流操作已執行");
```

**日誌級別和保留期可透過 `SystemSettings` 配置（熱載入，無需重啟）：**

```
log.min_level             = "Information"   // Debug | Information | Warning | Error
log.file_retention_days   = 7
log.db_retention_days     = 30
log.db_max_size_mb        = 100             // 次要上限；超過則刪除最舊 rows（以先觸發者為準）
```

`AppLogs` SQLite 資料表支援應用程式內的日誌檢視器（可按級別、EventTypeKey、MemberId 過濾）。後台工作程式清除邏輯（以先觸發者為準）：(1) 清除早於 `log.db_retention_days` 的行；(2) 若 SQLite 頁面計數 × page_size 超過 `log.db_max_size_mb`，持續刪除最舊的行直到估算大小降至閾值以下。**注意：SQLite DELETE 不立即縮減實體檔案大小**，需在大小清除後執行 `PRAGMA auto_vacuum = FULL`（建庫時設定）或顯式 `VACUUM`；建議建庫時設定 `auto_vacuum = FULL`，確保刪除後 page 歸還 OS。測試驗證以 `PRAGMA page_count * page_size` 估算大小，不依賴實體 `FileInfo.Length`。

---

### 4.16 外掛程式發現 (G14)

**MVP：僅限靜態 DI 註冊。** 外掛程式作為專案/套件依賴項被引用，並在編譯時於 `Program.cs` 中註冊。MVP 階段不進行執行時期 DLL 掃描或 `Assembly.LoadFrom()`。

```csharp
// Program.cs (MVP)
builder.Services.AddVulperonexPlugin<SoundPlugin>();
builder.Services.AddVulperonexPlugin<MyCustomPlugin>();
```

**第二階段（推遲）：目錄掃描 + 執行時期發現。**

```
超出範圍 (第一階段)：
  - {app_dir}/plugins/*.dll 目錄掃描
  - Assembly.LoadFrom() / AssemblyLoadContext
  - 外掛程式的熱載入 / 熱卸載
```

第二階段將新增：掃描 `{app_dir}/plugins/`，`appsettings.json` 中的可選白名單/黑名單，用於卸載的 `AssemblyLoadContext`。一個 DLL 載入失敗 → 記錄錯誤 → 跳過 → 其他 DLL 不受影響。

**啟動順序 (MVP)：**
1. 從 DI 解析靜態註冊的外掛程式
2. 每個外掛程式呼叫 `InitializeAsync(IPluginContext)`
3. 外掛程式將其 `EventTypeKey` 註冊到 `IStreamEventTypeRegistry`

---

### 4.17 Web 主機安全 (G15)

**雙埠架構：API 埠 + 疊層埠，皆 loopback-only、無需身份驗證。**

```
appsettings.json:
  "Web": {
    "ApiPort":     5000,   // 可配置
    "OverlayPort": 5001    // 可配置
  }
```

**永遠以 loopback-only 執行（無遠端存取）：**
```
ApiPort     5000 → 僅限 loopback（127.0.0.1 與 ::1），無需身分驗證
OverlayPort 5001 → 僅限 loopback（127.0.0.1 與 ::1），無需身分驗證
OBS 瀏覽器源：http://localhost:5001/overlay/chat  (乾淨的 URL, 無權杖)
```

Kestrel 以 `IPAddress.Loopback`（IPv4）+ `IPAddress.IPv6Loopback`（IPv6）雙重繫結兩個埠。安全邊界 = bind address 本身；不需要 ApiKeyMiddleware 或 X-Vulperonex-Key header。

**Overlay DTO 安全：** 疊層 DTO 必須是公共安全投影 — 即使伺服器永遠 loopback-only，仍需嚴格 DTO 白名單（防止日後擴充時誤加欄位、防止 SignalR 序列化過度曝光）：

| 疊層 | 允許的欄位 | 禁止的欄位 |
|---|---|---|
| `/overlay/chat` | SchemaVersion, EventId, Timestamp, DisplayName, ColorHex, Segments, Badges | MemberId, UserId, TotalBitsGiven |
| `/overlay/alerts` | SchemaVersion, EventId, Timestamp, DisplayName, EventType, Tier | MemberId, PlatformUserId |
| `/overlay/member` | SchemaVersion, DisplayName, AvatarUrl, CheckInCount (僅限當前會話) | MemberId, TotalLoyalty, LinkedPlatforms |

`SchemaVersion` 固定為 `1`。`EventId` 是 overlay public delivery id，用於前端去重，不得使用 MemberId、PlatformUserId 或其他內部 identity；優先使用 platform-provided id（IRC `msg-id` / EventSub `message_id`），缺值時 adapter 生成 ULID 並標記為 synthetic。`Timestamp` 為 UTC ISO-8601 event time，用於前端排序。`/overlay/member` 是狀態快照，不是事件流，因此不含 `EventId` / `Timestamp`。`OverlayModule` 在 SignalR 推送前將領域事件轉換為這些受限的 DTO。允許列表在 DTO 類型級別強制執行 — 無動態映射。

**OBS 瀏覽器源 URL：**
```
http://localhost:5001/overlay/chat
http://localhost:5001/overlay/alerts
http://localhost:5001/overlay/member
```

**DB 路徑解析規則（CLI 與 Web host 共同遵守）：** DB path 解析：`appsettings.json → Database:Path`（若存在），否則使用 OS app-data 預設路徑（見 §4.11）。**`Database:Path` 不允許透過 `appsettings.{Environment}.json` 或環境變數覆蓋** — Web host 與 CLI 均只讀主要 `appsettings.json`，確保兩者永遠讀相同 DB。開發環境若需自訂路徑，直接修改 `appsettings.json`（不使用 Development override）。

**Kestrel 雙 loopback 繫結：**
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback,       apiPort);      // IPv4 loopback
    options.Listen(IPAddress.IPv6Loopback,   apiPort);      // IPv6 loopback
    options.Listen(IPAddress.Loopback,       overlayPort);
    options.Listen(IPAddress.IPv6Loopback,   overlayPort);
});
```

---

### 4.18 統一即時監控頁 Unified Monitor Page（Phase 7D）

**背景：** 目前 simulate 事件、看 chat overlay、看 member-card 各在不同分頁/路由。實況主 debug 流程要：admin simulate → 切到 `/overlay/chat` 看結果 → 切到 `/overlay/member` 看 member 卡 → 回 simulate 再試。Context switch 多，driver friction 高。

**Phase 7D 設計：** 新增 `/monitor` 統一頁，舊 simulate / overlay 獨立頁保留作 debug / e2e 用途。

**版型（寬螢幕 ≥1280px）：**

```
┌─────────────────────────────────────────────────────────────┐
│ Header: 平台連線狀態 + SignalR 狀態 + Live/Settings 切換     │
├──────────────┬──────────────────────────────┬───────────────┤
│              │                              │               │
│  Simulate    │  Overlay Preview (iframe)    │  Chat Stream  │
│  Controls    │                              │  (live)       │
│  (sider)     │  ┌────────────────────────┐  │               │
│              │  │ 預覽切換: chat / member│  │  使用者: 訊息 │
│  • chat      │  │                        │  │  ...          │
│  • follow    │  │  iframe                │  │               │
│  • sub       │  │                        │  │               │
│  • giftsub   │  │                        │  │               │
│  • raid      │  └────────────────────────┘  │               │
│  • bits      │  背景: transparent/green/    │               │
│  • redeem    │       pink/color/image       │               │
│  • checkin   │  Preset 切換 dropdown        │               │
│  • batch     │  Reload 按鈕                 │               │
│              │                              │               │
└──────────────┴──────────────────────────────┴───────────────┘
```

**版型（窄螢幕 <1280px）：**

- Simulate controls 改為右側 drawer，header 加開關按鈕。
- Overlay preview + chat stream 上下堆疊。

**功能：**

1. **Simulate controls sider**：包含所有 simulate event subcommand UI（chat / follow / sub / giftsub / raid / bits / redeem / checkin），對應後端既有 `/api/simulate/*` 端點。新增「批次模擬打卡」工具單鍵發 N 個 checkin。
2. **Overlay preview iframe**：
   - 動態 iframe `src` 切換 chat / member / alerts，內含 `?preset={key}&t={ts}` query。
   - 預覽背景切換（transparent / green key / pink key / 純色 / 自訂背景圖 URL），給 OBS 預先看不同背景下視覺。
   - Reload 按鈕（bump query timestamp）。
   - 進階：選 custom preset 時可切 draft / production 預覽（與 §4.14.3 整合）。
3. **Chat stream panel**：訂閱 `/hubs/overlay/chat`，列最新 N 則訊息（純文字、含 member chip 預覽），不渲染 preset CSS（純表格樣式），讓實況主看「資料層」是否正確（與 overlay 視覺解耦）。
4. **Header 狀態**：平台連線狀態（Twitch ✅/❌）、SignalR 連線狀態、目前 preset 設定摘要。

**路由保留：**

- `/simulate` 既有獨立 simulate 頁不刪（CLI E2E、自動化 test 用）。
- `/overlay/chat`、`/overlay/member`、`/overlay/alerts` 既有獨立 overlay route 不刪（OBS browser source 仍直接走這幾個 URL）。
- `/monitor` 為**新預設 landing**（取代目前 `/` 預設）；既有 admin 入口仍在 sidebar。

**事件 → UI 即時反應：** SignalR 已連線時，simulate 動作觸發後，預覽 iframe（透過 hub 反向通知）+ chat stream 同步更新，無須手動 reload。

**i18n：** 完整 zh-TW + en-US。

**a11y：** sider 開關有 `aria-label`，drawer focus trap 沿用 ConfirmDialog 模式。

---

### 4.19 會員管理可編輯介面 Member Admin Editable Surface（Phase 7D）

**背景：** Phase 6 `/admin/members` 為唯讀檢視，理由是 MVP 安全防呆。但實況主真實使用情境需要：

- 手動調整某會員 loyalty / checkin 次數（補錯誤、活動補償）
- 查特定會員的歷史變動軌跡（誰、何時、為何改）
- 重設特定會員的 loyalty 不刪除身份
- 完全刪除測試會員

CLI 雖能做但實況中切視窗成本高。

**Phase 7D 設計：** Member admin 改為可編輯。**所有變動寫 audit log**，保留可追溯性。

**新增 endpoint：**

| Method | Path | 用途 | 必填 body |
|--------|------|------|----------|
| `PATCH` | `/api/members/{memberId}/loyalty` | 調整 totalLoyalty / checkInCount | `{ totalLoyalty?: int, checkInCount?: int, reason: string }` |
| `POST` | `/api/members/{memberId}/reset` | 重設 loyalty 歸零（保留 identity）| `{ resetLoyalty: bool, resetCheckIn: bool, reason: string }` |
| `DELETE` | `/api/members/{memberId}` | 完全刪除會員（含 identity）| `{ reason: string }` |
| `GET` | `/api/members/{memberId}/audit` | 取會員變動歷史 | query: `?limit=50&offset=0` |

**Audit table：** 新增 `MemberAuditLogs` SQLite table：

```
MemberAuditLogs:
  Id              ULID PK
  MemberId        ULID FK
  OccurredAt      DateTimeOffset
  ActorKind       enum { 'user' | 'workflow' | 'cli' | 'system' }
  ActorId         string?         -- workflow rule id / cli session id / null for user
  Operation       enum { 'adjust_loyalty' | 'adjust_checkin' | 'reset' | 'delete' | 'create' }
  BeforeJson      string?         -- snapshot before
  AfterJson       string?         -- snapshot after
  Reason          string          -- required, non-empty
```

**Concurrency：** 所有 mutation endpoint 採 `If-Match` header 帶 `etag`（基於 `MemberRecord.UpdatedAt` ticks hash）。版本不符 → 409 Conflict。前端遇 409 → 提示 reload。

**Validation：**
- `totalLoyalty >= 0`, `checkInCount >= 0`
- `reason.Length in [3, 500]`
- DELETE 需 confirm token：先 `POST /api/members/{id}/delete-token` 拿 30s token，DELETE body 必帶 token，防誤點

**前端 UI：**

| 元件 | 對應 endpoint | 模式 |
|------|--------------|------|
| AdjustLoyaltyModal | `PATCH /loyalty` | 表單：新數值 + 變更原因。顯示 before/after diff。 |
| ResetModal | `POST /reset` | 確認 dialog：重設 loyalty / checkIn checkboxes + 原因 |
| DeleteConfirmDialog | `DELETE /` | 二段確認：第一段拿 token，第二段確認執行 |
| AuditLogDrawer | `GET /audit` | 右側 drawer，timeline 列變動歷史，含 actor + before/after + reason |

**Workflow integration：** `TriggerCheckInAction` 在 increment 後寫一筆 audit log，ActorKind='workflow'，ActorId=ruleId。`TriggerAdjustLoyaltyAction`（若 Phase 7D 新增）同理。

**安全：**
- 沿用 loopback-only。
- DELETE token + reason required 防誤刪。
- Audit log 不可刪改（append-only），保留期沿用 `log.db_retention_days`（但會員 audit 獨立計算，預設 365 天）。
- 反射測試：endpoint 回傳 DTO 不包含 `MemberId` 以外的內部 PK。

### 4.20 模組與外掛程式管理系統 Module & Plugin Management (Phase 7D)

**背景與動機：**
目前核心服務（打卡、計數器、抽獎點數、音效畫面、外部 OneComme Bridge 等）雖然作為 Hosted Services 或外掛程式執行，但沒有提供集中管理其啟用/停用（ON/OFF）狀態的頁面。當特定模組關閉時，其相依之模組仍盲目執行可能導致狀態漂移。

**設計與規格：**
1. **模組/外掛程式開關狀態儲存**：
   - 透過 `ISystemSettingsService` 將模組啟用狀態儲存在資料庫/系統設定檔中，使用鍵名 `modules.enabled.{moduleName}`。
   - 所有核心 Hosted Services 在 `ExecuteAsync` 或 `StartAsync` 時需動態偵測該設定值，若為 `false` 則跳過註冊、攔截或動作執行（No-Op 狀態），已在執行的 Hosted Services 在偵測到設定變更時應即時切換狀態。
   - 對於 `IWorkflowActionExecutor`（例如 `TriggerCheckInActionExecutor`），若關聯的模組（如打卡模組）已關閉，則應拒絕執行動作並拋出對應的 `WorkflowExecutionException`。

2. **模組相依性解析 (Dependency Resolution)**：
   - **相依定義**：
     - `CheckInModule` (打卡模組) -> 相依於 `MemberModule` (會員核心)
     - `LotteryModule` (抽獎點數) -> 相依於 `MemberModule` (會員核心)
     - `OneCommeBridge` (OneComme 外掛程式) -> 無相依，但需 Core Event Bus
     - `OverlayModule` (畫面模組) -> 無相依
   - **拓撲聯鎖關閉 (Cascading Disable)**：
     - 當使用者在 UI 上**停用**一個被其他模組相依的模組時（例如停用 `MemberModule`），系統**必須**觸發拓撲依賴關閉。
     - **UI 聯鎖警告閘門**：前端將跳出警告確認：「停用『會員核心模組』將一併關閉以下相依模組：打卡模組，抽獎模組。是否確認關閉？」。
     - 使用者確認後，API 將同時對相依模組寫入 `false`，並在系統 Audit Log 留下 `ActorKind = 'user'`、`Operation = 'disable_module'` 記錄。
   - **自動聯鎖啟用 (Cascading Enable)**：
     - 當使用者在 UI 上**啟用**一個具有相依性的模組時（例如啟用 `CheckInModule`），若其相依模組（例如 `MemberModule`）為關閉狀態，系統應**自動一併開啟**其相依模組，或**彈出警示並拒絕啟動**。

3. **模組管理端點 (API)**：
   - `GET /api/plugins-modules`：列出所有模組/外掛名稱、中文顯示名稱、說明、目前是否執行中 (`IsActive`) 以及相依列表。
   - `POST /api/plugins-modules/{name}/toggle`：參數為 `enabled: bool`。觸發相依性計算，成功執行後回傳拓撲變動後的所有模組狀態清單。

4. **UI 管理頁面**：
   - 於 `/admin/settings` 下新增「功能模組與外掛程式」分頁，以卡片化 Grid 呈現各功能之 ON/OFF 開關、分類標籤（核心服務、互動功能、視聽媒體、外部外掛程式）以及相依性圖示提示。

---

### 4.21 事件與忠誠度模擬功能擴充 Event & Loyalty Simulation (Phase 7D)

**背景與動機：**
現存事件模擬僅限於 `chat`, `follow`, `sub` 等基礎行為，對於關鍵的「身分組（Custom Roles/StreamRole）」與「忠誠點數/打卡」缺乏 UI 與 API 的模擬支援，導致實況主無法直接在 Admin 面板驗證與除錯複雜的工作流（例如：只允許 Moderator 參與的打卡獎勵、只對 VIP 觸發的特效等）。

**設計與規格：**
1. **身分組模擬 (StreamRole flags)**：
   - 擴充 `/api/simulate/*` 所接受的 `SimulateRequest` DTO：其 `Roles` 屬性不僅可為 single string/number，亦支援字串陣列（如 `["subscriber", "moderator", "vip"]`），允許將多重 `StreamRole` 標誌包裝進模擬事件的 User payload 中。
   - 模擬器 UI 提供勾選框 (Checkbox Group)，讓實況主可任意勾選與疊加身份別。

2. **打卡與忠誠度模擬端點**：
   - 新增 `POST /api/simulate/checkin` 端點。
   - 接收參數：
     - `platformUserId`: string (要模擬打卡的會員 ID，預設隨機)
     - `displayName`: string (要模擬打卡的會員顯示名稱，預設隨機)
     - `skipCooldown`: bool (是否繞過打卡冷卻限制，預設 true)
     - `stampCount`: int (本次要直接累積的印章/打卡次數，預設 1)
   - **行為**：端點直接調用 `IMemberResolver` 與 `IMemberStreamStateRepository` 將打卡次數增量，並在 SQLite 中成功變更後，發布 `MemberCheckedInEvent` 事件到事件匯流排 (Event Bus)，以便 OBS Overlay 與預覽 Hub 能即時觸發集點卡視覺效果。

---

### 4.22 視覺化與直覺化工作流設定 UI Intuitive Workflow Rule Editor (Phase 7D)

**背景與動機：**
現有的工作流規則設定介面要求使用者手動輸入特定 JSON 或純文字運算式，對不熟悉技術的實況主而言極不直覺。此階段將引進視覺化引導編輯介面，徹底摒棄低效的自由文字設定。

**設計與規格：**
1. **條件建構器 (Condition Builder)**：
   - 摒棄純手寫 NCalc 文字，改用視覺化規則列表（Row-based list）。
   - 每筆條件由三大下拉選單組成：`[變數選擇器]` -> `[比較運算子]` -> `[目標值/常數]`。
   - 變數選擇器將動態讀取 `StreamEventTypeRegistry` 及 Workflow 預先提供的 Context 變數列表（如 `user.name`, `message.text`, `member.stamps`），以點選下拉式清單的方式防呆。
   - 前端元件最終自動將視覺化設定轉換並輸出成標準的 NCalc 運算式（例如：`member.stamps >= 10`）傳給後端 API。

2. **動態動作表單 (Dynamic Action Form)**：
   - 針對每個 Action 類型（例如 `TriggerCheckIn`、`RefundTwitchRedemption`、`TriggerEffect` 等），根據後端註冊的 `ActionParameterMetadata`（型別含 string, number, boolean, select, text）動態生成對應的強型別輸入控制項。
   - 元件內整合「變數選擇器浮動面板」，使用者於輸入框內游標點選或輸入 `{` 時，即時跳出可用變數列表，點擊即可插入變數範本字串（如 `{user.displayName}`），避免拼寫錯誤。

### 4.23 Twitch 徽章快取與模擬器徽章 UI Twitch Badge Cache & Simulator Badge UI (Phase 7E)

**背景與動機：**
聊天 overlay 必須能正確顯示 Twitch 原生徽章圖示（VIP / Moderator / Subscriber / Founder / 頻道自訂徽章如「繪師」「贊助者」）。當前實作存在兩個缺陷：

1. **真實 Twitch 路徑徽章圖示壞掉**：IRC parser 將 `badges` IRC tag 解析為 `subscriber/0`、`vip/1` 等 *key* 字串並寫入 `PlatformUserDisplayInfo.Badges`。`OverlayEventForwarder.ForwardChatEventAsync` 直接將這些 key 當作 URL 透過 SignalR 廣播給 overlay，導致 `<img :src>` 永遠為 key 字串而非真實圖片 URL。
2. **模擬器無徽章選擇能力**：`SimulateControlsPanel.vue` 僅能勾選 `Subscriber/Moderator/VIP/Follower` 文字角色，overlay 渲染為 `chat-role-pill` 文字膠囊；無法觸發徽章圖示路徑，也無法選擇自訂頻道徽章。

對照來源：`ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchChannelApiService.cs`（`GetGlobalBadgesAsync` / `GetChannelBadgesAsync`）、`ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/IdentityService.cs`（`SyncBadgesAsync` / `GetBadgeUrl` 以 7 天 TTL 快取 `badge_{set}_{ver}` → URL）、`ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchMessageEnricher.cs`（在訊息 enrich 時將 badges KVP 解析為 URL list 寫入 ChatMessage）。

**設計與規格：**

1. **`ITwitchHelixClient` 徽章端點**：
   - 新增 `Task<IReadOnlyDictionary<string, TwitchBadgeDescriptor>> GetGlobalBadgesAsync(CancellationToken)`：對應 `GET helix/chat/badges/global`。
   - 新增 `Task<IReadOnlyDictionary<string, TwitchBadgeDescriptor>> GetChannelBadgesAsync(string broadcasterId, CancellationToken)`：對應 `GET helix/chat/badges?broadcaster_id={id}`。
   - 回傳字典 key 格式為 `{set_id}_{version}`，value 為 descriptor（含 `SetId`, `Version`, `ImageUrl1x`, `Title?`, `Description?`）。

2. **`ITwitchBadgeCache`（Application 介面）+ `TwitchBadgeCache`（Infrastructure 實作）**：
   - 介面：`string? Get(string key)`、`IReadOnlyList<TwitchBadgeDescriptor> ListAll()`、`Task SyncGlobalAsync(CancellationToken)`、`Task SyncChannelAsync(string broadcasterId, CancellationToken)`、`bool IsReady`。
   - 實作：`ConcurrentDictionary<string, TwitchBadgeDescriptor>` thread-safe；sync 失敗時保留舊資料、log warning。
   - 注意：cache 為 in-memory，應用重啟需重新同步；不做磁碟持久化。

3. **`TwitchBadgeSyncHostedService`**：
   - `StartAsync` 中 fire-and-forget 呼叫 `SyncGlobalAsync` + `SyncChannelAsync(broadcasterId)`（broadcaster id 來自 `Twitch:BroadcasterId` config，未設定則略過 channel sync）。
   - sync 失敗不阻擋應用啟動。

4. **`OverlayEventForwarder` 徽章解析**：
   - `ForwardChatEventAsync` 與 `TryResolveMemberSnapshotAsync` 注入 `ITwitchBadgeCache`，將 `display?.Badges` 之 key list 透過 `cache.Get` 轉換為 URL list，無對應 URL 之 key 被過濾（避免 overlay 出現破圖）。
   - 廣播至 SignalR 之 `OverlayChatPayload.Badges` 改為已解析之 URL 列表（contract schemaVersion 不變，僅內容語意修正）。
   - `ExtractRoles` 文字角色維持輸出（`event.roles`），供 future preset 使用；本期 `ChatPresetDefault.vue` 不再渲染文字角色 chips。

5. **模擬路徑徽章/顏色透傳**：
   - `SimulationRequest`（`SimulationKind.Message`）新增 `IReadOnlyCollection<string> Badges` 與 `string? ColorHex`。
   - `SimulateEndpoints` 接受 request body 之 `badges: string[]`、`colorHex: string?`；在 `adapter.SimulateAsync` 之前對 sim user (`simulation:{userId}`) 呼叫 `IPlatformUserInfoCache.UpsertAsync`，將 `Badges` + `ColorHex` 寫入快取，使後續 forwarder 之解析路徑與真實 Twitch 路徑統一。
   - 此設計避免在 domain event 上新增徽章欄位污染領域模型。

6. **新 API endpoint `GET /api/twitch/badges`**：
   - 回傳 `{ global: TwitchBadgeDescriptor[], channel: TwitchBadgeDescriptor[] }`，供前端 picker UI 列出可選徽章。
   - cache 未就緒時回傳空 array 並附 `Cache-Control: no-store` header。
   - 受 admin auth 限制（與其他 `/api/twitch/*` 端點一致）。

7. **前端 `SimulateControlsPanel.vue` UI 改造**：
   - onMounted 呼叫 `getTwitchBadges()` 並依 `setId` 分組顯示為徽章 chip grid，每 chip 為 `<img>` + tooltip 標題；點擊 toggle 加入 `selectedBadges`。
   - 新增「名稱顏色」欄位：hex input 配對即時 color swatch（預設 `#FFCA28`）。
   - submit 時於 request body 帶入 `badges: selectedBadges` 與 `colorHex`。
   - 移除「Streamer Roles」文字角色多選區（向後相容：若舊測試傳 `roles` 仍解析為對應 badge key，由後端 derive；本期前端 UI 不曝光）。

8. **前端 `ChatPresetDefault.vue` 調整**：
   - 移除 `chat-role-pill` 文字角色 chip 渲染段；徽章圖示為唯一身份標示。
   - badge `<img>` 渲染保持現狀；新增 `onerror` fallback 隱藏破圖。

**驗收：**
- 模擬器傳送含徽章之聊天訊息，`/overlay/chat` 顯示 Twitch 原生徽章 PNG 而非文字 chips。
- 真實 Twitch IRC 聊天訊息（VIP / Moderator / Subscriber）overlay 圖示正常。
- `GET /api/twitch/badges` 回傳 global 集合（含 broadcaster / moderator / vip / subscriber / founder 等）與 channel 自訂徽章。
- 名稱顏色 hex 套用至 overlay `chat-username` 之 `style="color"`。
- Cache miss 時 overlay 不出現破圖。
- 單元測試覆蓋：`TwitchBadgeCacheTests`、`OverlayEventForwarderBadgeResolutionTests`、`SimulateEndpointsBadgeTests`、frontend `SimulateControlsPanel.test.ts`。

**邊界：**
- 不下載徽章圖檔做 base64 內嵌，全程走 Twitch CDN URL。
- 不支援上傳自訂徽章（僅顯示已於 Twitch 註冊者）。
- 不整合 BTTV / 7TV / FFZ 徽章與表情。
- 不做徽章變更 audit log。
- Cache 無背景定期 refresh（自訂徽章新增後需重啟；後續 phase 可加 24h refresh hosted service）。

---

### 4.25 Twitch 頻道點數獎勵快取 + 獎勵選單 UI (Phase 7G)

> 完整內容請見英文版 `docs/SPEC.md` §4.25。以下為中文摘要。

**動機：** `reward.redeemed` 觸發器的 `RewardName` 篩選原本是純文字輸入，操作者得自己背獎勵名稱，常打錯也無從查起；模擬面板的 `Reward ID` 文字輸入同樣難用。

**設計重點：**

1. **Helix 查詢** — `IHelixClient.GetCustomRewardsAsync(broadcasterId, ct)` 取 `channel_points/custom_rewards`，回傳 `TwitchRewardDescriptor { Id, Title, Cost, IsEnabled, ImageUrl? }`。所需 scope `channel:read:redemptions` 已含於 Phase 7G 後的預設 scope 集。
2. **`ITwitchRewardCache` 單例**（`Vulperonex.Web.TwitchAuth`）— 純記憶體快取；refresh 流程：UI `/refresh` 端點 + OAuth `/complete` 完成後自動 `QueueRefresh()`。broadcaster 解析鏈與 §4.7 一致（`Twitch:BroadcasterId` → `SystemSettingKey.TwitchChannelName` → `Twitch:ChannelName` → `LookupUserAsync`）。401/403 直接吃掉並以 `ready=false` 表示。
3. **HTTP 端點**：
   - `GET /api/twitch/rewards` — 回傳 `{ ready, lastRefreshedAt, rewards }` 快取快照，不打網路。
   - `POST /api/twitch/rewards/refresh` — 強制重整。未授權狀態回 `200 { ready:false }`，讓 UI 顯示「請先授權」而非錯誤橫幅。
4. **觸發器篩選 dynamic options source** — `FilterFieldDto.OptionsSource` 新欄位；`RewardName` 宣告 `OptionsSource: "twitch.rewards"`。前端 `TriggerEditor.vue` 改用嚴格 `<select>`，含「任何」選項 + 獎勵清單 + 對於資料庫已存但目前快取沒有的舊選擇加上「（已失效）」尾綴選項。
5. **模擬面板共用同一快取** — `SimulateControlsPanel.vue` 將原本的 `rewardId` 文字輸入換成同樣的 `<select>`（value=id, label=title）。送出時同時帶 `rewardId` 與 `rewardTitle`；後端 `SimulateRequest` 新增可選 `RewardTitle`，缺則回退到 `rewardId`。此舉修正了一個潛在 bug：先前 `SimulateEndpoints.ToSimulationRequest` 把 `RewardTitle` 直接複製自 `rewardId`，導致 `MatchRewardRedeemed`（以 `RewardTitle` 比對）在模擬路徑上幾乎不可能正確觸發。

**邊界：**
- 圖示尚未渲染（DTO 帶 `ImageUrl` 但 UI 仍純文字）。
- 無自動排程 refresh（僅手動 + OAuth 完成）。
- Refund action 不在範圍內。

---

## 5. 指令

```bash
# --- 後端 ---
dotnet build
dotnet test
dotnet run --project src/Hosts/Vulperonex.Web
dotnet run --project src/Hosts/Vulperonex.Desktop

# --- CLI ---
vulperonex simulate chat   --user alice --message "hi"
vulperonex simulate follow --user alice
vulperonex simulate sub    --user alice --tier 1000
vulperonex config get streaming.platform
vulperonex config set streaming.platform twitch
vulperonex member list --platform twitch --limit 20
vulperonex member show   <memberId|prefix>
vulperonex member delete <memberId|prefix> [--yes]
vulperonex rule list
vulperonex rule show    <ruleId|prefix|--name <name>>
vulperonex rule enable  <ruleId|prefix|--name <name>>
vulperonex rule disable <ruleId|prefix|--name <name>> [--yes]
vulperonex rule delete  <ruleId|prefix|--name <name>> [--yes]
# 解析與確認流程：docs/phases/phase-5_5-rapid-test/cli-id-resolution-decision.md

# --- 前端 ---
cd src/frontend
pnpm install
pnpm dev          # Vite 開發伺服器 (Photino 可以指向此處以進行熱載入)
pnpm build        # 輸出到 ../Hosts/Vulperonex.Web/wwwroot
pnpm test

# --- 品質 ---
dotnet format
# 覆蓋率門檻 (詳見 §7.3 完整指令)：
dotnet test tests/Vulperonex.Tests.Unit /p:CollectCoverage=true /p:Include="[Vulperonex.Domain]*" /p:Exclude="[*.Tests.*]*" /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=average
dotnet test tests/Vulperonex.Tests.Unit /p:CollectCoverage=true /p:Include="[Vulperonex.Application]*" /p:Exclude="[*.Tests.*]*" /p:Threshold=80 /p:ThresholdType=line /p:ThresholdStat=average
pnpm lint   # 使用 oxlint（oxlint.json 設定，Vue + TypeScript rules）
```

---

## 6. 程式碼風格

### 6.1 C# — 領域事件

```csharp
namespace Vulperonex.Domain.Events;

public interface IStreamEvent
{
    /// <summary>
    /// 全域唯一事件 ID。用於重啟時 TDQ 重播的去重。
    /// 格式：ULID 字串。適配器必須從平台事件 ID（如有）填充，否則生成新的 ULID。
    /// </summary>
    string EventId { get; }

    string EventTypeKey { get; }
    string Platform { get; }
    StreamUser? User { get; }
    DateTimeOffset OccurredAt { get; }
}

public sealed record UserSentMessageEvent : IStreamEvent
{
    // EventId: 使用平台提供的訊息 ID（如有），否則使用新的 ULID
    public string EventId { get; init; } = Ulid.NewUlid().ToString();
    public string EventTypeKey => StreamEventKeys.UserSentMessage;
    public required string Platform { get; init; }
    public required StreamUser User { get; init; }
    public required string PlainText { get; init; }
    public bool IsFirstChat { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

### 6.2 C# — 匯流排與適配器契約

```csharp
// 定義於 Vulperonex.Adapters.Abstractions（非 Application）。
// 所有 Adapter（Twitch、Simulation，未來其他平台）reference Adapters.Abstractions 以實作此介面；
// Application/Domain 不知道 IStreamEventSource 的存在。
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
    /// 等待直到記憶體佇列清空且所有活動的處理程式都已完成。
    /// 語意：handler 例外被隔離後以 warning 記錄；WaitForIdleAsync 本身不聚合或拋出 handler 錯誤，
    ///       完成後回傳 Task.CompletedTask（不反映 handler 是否出錯）。
    ///       CLI --wait 使用此方法，同樣不依賴 handler 錯誤計數。
    /// 僅用於整合測試和 CLI --wait 模式。不用於生產程式碼路徑。
    /// </summary>
    Task WaitForIdleAsync(CancellationToken ct = default);
}

public interface IPlatformChatSender
{
    string Platform { get; }
    Task SendAsync(string text, CancellationToken ct);
}
```

### 6.3 C# — 外掛程式契約

```csharp
/// <summary>
/// 提供給外掛程式在其生命週期內使用的單例範圍上下文。
/// 不攜帶每個事件或每個操作的資料。
/// </summary>
public interface IPluginContext
{
    IStreamEventBus Events { get; }    // 訂閱和發布
    ILogger Logger { get; }
    // 注意：不暴露 IServiceProvider — 避免 service locator 反模式。
    // 外掛程式需要額外服務時，透過此 interface 新增明確屬性（post-MVP 擴充點）。
}

/// <summary>
/// InvokePluginAction 執行器傳遞給外掛程式操作處理程式的每個操作調用上下文。
/// 攜帶特定事件的資料，且不在操作或規則之間共享。
/// </summary>
public interface IPluginActionContext
{
    /// <summary>
    /// 完全限定的去重鍵：(EventId, WorkflowRuleId, ActionIndex[, InvocationId])。
    /// 外掛程式必須將此完整鍵（而非僅 EventId）用於 ActionExecutionLog 條目。
    /// 同一 EventId 可能出現在多個規則中；僅使用 EventId 會導致跨規則的去重衝突。
    /// </summary>
    string ActionExecutionKey { get; }

    string EventId { get; }
    string WorkflowRuleId { get; }
    int ActionIndex { get; }
    string EventTypeKey { get; }
    StreamUser? User { get; }
    IReadOnlyDictionary<string, JsonElement> Params { get; } // 來自 WorkflowRule 操作配置
    ILogger Logger { get; }
    // 注意：不暴露 IServiceProvider — 避免 service locator 反模式。
}

public interface IVulperonexPlugin
{
    /// <summary>
    /// 外掛程式唯一識別符（等同於 WorkflowRule InvokePluginAction.PluginId lookup key）。
    /// 命名規範：lowercase-kebab，如 "my-plugin"；不得含空白或特殊字元。
    /// Name 與 PluginId 使用相同字串 — InvokePluginAction 的 PluginId 必須等於此值。
    /// </summary>
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(IPluginContext ctx, CancellationToken ct);
    Task ShutdownAsync(CancellationToken ct);

    /// <summary>
    /// 由 InvokePluginAction 執行器呼叫。ActionId 匹配此外掛程式定義的操作識別子。
    /// 外掛程式必須透過 IPluginActionContext.ActionExecutionKey 實作去重，以應對任何外部副作用。
    /// Timeout 超時後底層 Task 可能仍在執行 — 外掛程式應在 CancellationToken 觸發後記錄 warning，
    /// 避免 late completion 的副作用被誤判為重試結果（造成雙副作用）。
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

### 6.5 慣例

- **C#：** 類型/方法使用 PascalCase，區域變數使用 camelCase，私有欄位使用 `_camelCase`，使用檔案範圍命名空間，適當處使用主要構造函數 (Primary Constructors)。
- **TypeScript：** 識別子使用 camelCase，元件使用 PascalCase，檢視檔名使用 `kebab-case`。
- **關鍵命名規則：** `Domain` 或 `Application` 專案內不得有 `Twitch*`（或任何平台特定）字首。平台詞彙僅存在於其 `Adapters.<Platform>` 專案中。

---

## 7. 測試策略

### 7.1 測試金字塔

```
                 ╱╲
                ╱  ╲    架構測試 (NetArchTest)
               ╱────╲   - Domain 無基礎設施依賴
              ╱      ╲  - Domain/Application 中無 "Twitch" 字串
             ╱        ╲
            ╱──────────╲ 整合測試
           ╱            ╲ - SimulationAdapter → Bus → WorkflowEngine → DB
          ╱              ╲
         ╱────────────────╲ 單元測試 (絕大部分)
        ╱                  ╲ - 領域邏輯、映射、處理程式、執行器
```

### 7.2 位置

- `tests/Vulperonex.Tests.Unit/` — 純單元測試，無 I/O。
- `tests/Vulperonex.Tests.Integration/` — 記憶體內 SQLite + Simulation 適配器端到端測試。
- `tests/Vulperonex.Tests.Architecture/` — 層級規則強制執行。
- `src/frontend/tests/` — Vitest + Vue Test Utils。

### 7.3 覆蓋率目標

- 領域層 (Domain)：> 90% — 僅針對 `Vulperonex.Tests.Unit` 測量（領域是純邏輯，無 I/O）。
- 應用層 (Application)：> 80% — 僅針對 `Vulperonex.Tests.Unit` 測量。整合測試**不**併入此門檻（coverlet.msbuild 無法在單個指令中合併兩個測試專案的報告）。如果因為應用層行為僅被整合測試覆蓋而導致單元測試覆蓋率低於 80%，解決方案是新增聚焦的單元測試（使用 Fakes/Mocks），而非放寬門檻或切換到合併報告。
- 適配器 (Adapters)：透過 SimulationAdapter 等效性進行整合測試（真實適配器使用相同的領域映射邏輯）。

**強制執行：** 使用 **`coverlet.msbuild`**（而非 `coverlet.collector`）來根據閾值判定建構失敗。固定明確版本以避免偏差 — 使用中央套件管理或 `<PackageReference Include="coverlet.msbuild" Version="6.0.2" />`（在專案設定時固定到最新穩定版）。對於門檻工具，**不接受**萬用字元版本。

兩個 CI 指令（均必須通過）：
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
任一指令在覆蓋率低於閾值時會以非零值退出，導致 CI 建構失敗。可以新增 `reportgenerator` 用於 HTML 報告，但它不是門檻機制。

### 7.4 BDD + TDD 紀律

- 每個行為都從 BDD 風格的情境開始：Given / When / Then (給定 / 當 / 那麼)。
- 情境是驗收契約；在實作被視為完成前，它必須映射到一個或多個自動化測試。
- 實作遵循 TDD：先寫失敗測試，確認「紅燈」，編寫通過測試的最少程式碼，確認「綠燈」，然後在測試通過的情況下重構。
- 新的領域邏輯 → 首先根據 BDD 情境編寫失敗的單元測試。
- 錯誤修復 → 在更改生產程式碼前，先用失敗測試重現。
- 重構 → 確保測試維持綠燈。
- 整合情境盡可能使用 SimulationAdapter。
- 手動驗證可以作為 Photino、OBS 和瀏覽器執行時期行為的 BDD+TDD 補充，但它不能替代自動化驗收測試。

**測試命名慣例（最低標準）：**
- C# 測試方法名稱：`Given_<狀態>_When_<操作>_Then_<預期>` (使用底線, PascalCase 段落)  
  範例：`Given_ValidRule_When_EventMatches_Then_SendChatMessageCalled`
- 如果未使用專門的情境檔案，BDD 情境**必須**出現在測試方法體頂部的 `// Given / When / Then` 註釋區塊中。
- 前端 (Vitest)：`describe` = 元件/Composable 名稱；`it` = `should <預期> when <條件>`

---

## 8. 邊界

### 8.1 務必執行 (Always do)

- 在任何提交前執行所有適用的測試套件：始終執行 `dotnet test`；一旦 `src/frontend/` 存在，則必須執行 `pnpm test` 與 `pnpm build`（在任務 19 之前的後端任務中可跳過）。`pnpm lint` 為**手動驗證步驟**（CI 不強制），於各 Checkpoint 手動執行一次。
- 新事件實作 `IStreamEvent` 且為不可變的 `record`。
- 適配器程式碼位於 `Adapters/Vulperonex.Adapters.<Platform>/`。
- 平台特定的術語**遠離** `Domain` 和 `Application` 專案。
- 使用 `MemberId` (ULID) 作為規範的成員鍵，絕不使用平台 UserId。
- 在 CI 中執行架構測試。

### 8.2 需先諮詢 (Ask first)

- 向解決方案新增頂級專案（**Task 1 初始專案已授權，不需逐一詢問；Task 1 以外的額外新專案才 ask-first**）。
- 刪除或重新命名欄位的架構遷移。
- 新增 NuGet / npm **相依套件**（包含 oxlint 等 dev tool — 詢問後安裝一次，**已安裝後執行 lint 屬驗證步驟，不需再詢問**）。例外：Phase 1 Task 1c 所需且本 SPEC 已命名的測試/coverage 套件已預先授權，不需逐一詢問：`xUnit 3`、`NSubstitute`、`FluentAssertions 7`、`NetArchTest`、`coverlet.msbuild 6.0.2`。
- 更改公共外掛程式契約 (`IVulperonexPlugin`)。
- 在第二階段之後修改核心領域事件的形狀。

### 8.3 嚴禁執行 (Never do)

- 在 `Application` 或 `Domain` 專案中引用 `Twitch*`（或任何平台特定）類型。
- 在發布後變更事件對象（狀態變更應產生新事件）。
- 在同一個倉儲上混合命令和查詢操作（輕量級 CQRS）。
- 繞過事件匯流排 — 適配器絕不直接呼叫處理程式。
- 將事件持久化到資料庫（僅限日誌記錄）。
- 提交機密、OAuth 權杖或 `App_Data/*.db`。

---

## 9. 成功準則 (MVP)

- [ ] **SC-1：** 整合測試 `TwitchAdapter_PublishesAllSevenMvpEvents` 通過：對於七個 MVP `EventTypeKey` 中的每一個，模擬的 Twitch 有效負載都會在匯流排上生成相應的 `IStreamEvent`（透過 `WaitForIdleAsync` + 捕獲的事件列表驗證）。

- [ ] **SC-2：** 整合測試 `WorkflowEngine_ExecutesMatchingRule_OnEventTypeKey` 通過：當發布 `UserSentMessageEvent` 時，`EventTypeKey = "user.message"` 的 `WorkflowRule` 會觸發其 `SendChatMessageAction`；`IPlatformChatSender` 模擬對象恰好接收到一次 `SendAsync` 呼叫。

- [ ] **SC-3：** 整合測試 `SimulationAdapter_DoesNotReferenceTwitchTypes` 通過：`Vulperonex.Adapters.Simulation` 程序集對 `Vulperonex.Adapters.Twitch` 的類型引用為零（透過 NetArchTest 或反射掃描驗證）。

- [ ] **SC-4：** 架構測試 `Domain_HasNoReferenceToTwitchSymbols` 在將任何 `Twitch*` 識別子引入 `Domain` 或 `Application` 專案時，應導致建構失敗（紅燈測試）。

- [ ] **SC-5：** 整合測試 `OverlayHub_ReceivesSignalRPayload_WithinTimeout`：透過 `SimulationAdapter` 發布 `UserSentMessageEvent`，斷言 `/overlay/chat` SignalR Hub 用戶端在 **5 秒**內（CI 安全超時）接收到 `OverlayChatPayload`。效能目標（非阻塞）：從事件到 SignalR 的延遲在本地機器上 < 500ms，作為基準單獨追蹤，而非判定通過/失敗的門檻。

- [ ] **SC-5b：** 整合測試 `WorkflowSendChatMessage_Simulation_IsObservable`：在 `Simulation` 平台執行含 `SendChatMessage` 的 workflow，斷言可觀測輸出面（記憶體接收端 / Chat Outbox / 歷史檢視）於 **5 秒**內出現 rendered message、platform、channel、dedupKey 與 status，且不依賴 `/overlay/chat` 是否有 bridge。

- [ ] **SC-6：** 兩個互補的整合測試共同滿足此準則（拆分於 Task 12 + Task 13 實作）：
  - **SC-6a（WorkflowEngine half，Task 12）：** `SC6a_SimulationAndTwitch_ProduceSameWorkflowSideEffect`：使用相同有效負載分別透過 `SimulationAdapter` 和 `TwitchAdapter`（mock IRC）發布 `UserSentMessageEvent`；斷言兩者在 `WaitForIdleAsync` 後對 `IPlatformChatSender.SendAsync` 的呼叫完全相同。
  - **SC-6b（MemberRecord half，Task 13）：** `SC6b_SimulationAndTwitch_ProduceSameMemberDbState`：相同有效負載分別執行（各使用獨立 fresh SQLite fixture），斷言兩次 `WaitForIdleAsync` 後 `MemberRecord` 資料庫狀態相同。
  - 兩個測試均通過 = SC-6 達成。


- **SC-7：** 已移除出 MVP scope（原為 MockYouTube Adapter < 200 LOC 驗證；Twitch 以外平台 adapter 推遲）。

- [ ] **SC-8：** 整合測試 `MemberResolver_CreatesUlidMemberRecord_WithPlatformIdentity`：發布 `UserSentMessageEvent { Platform="twitch", UserId="test123" }` 後，斷言 `PlatformIdentity` 資料表具有行 `(Platform="twitch", PlatformUserId="test123")` 且 `MemberRecord` 的 `MemberId` 符合 ULID 格式。

- [ ] **SC-9：** 單元測試 `SendChatMessageAction_DefaultsToSourcePlatform` 和 `SendChatMessageAction_RespectsTargetPlatformOverride`：驗證 `IPlatformChatSender` 選擇邏輯。

- [ ] **SC-10：** 整合測試 `Plugin_CanPublishCustomEvent_TriggeringWorkflow`：外掛程式呼叫 `IPluginContext.Events.PublishAsync(customEvent)`；具有匹配 `EventTypeKey` 的 `WorkflowRule` 觸發；`IPlatformChatSender` 模擬對象接收到 `SendAsync`。

- [ ] **SC-11：** 手動 / 整合驗證 `ChatOverlayTemplatePreset_CanSwitchWithoutCodeEdit`：`/overlay/chat` 可在不修改前端原始程式碼的前提下切換至少兩個樣板，至少包含 Vulperonex 內建預設與另一個可安裝 preset；切換後 payload contract 不變，且渲染仍遵守 DTO 白名單與 text binding。

- [ ] **SC-11b：** 擴充功能驗證 `OneCommeCompatibility_ExtensionContract_Works`：OneComme 相容能力以外掛 / 匯入器 / adapter 形式接入，不要求 core 直接綁定其執行期；驗證可辨識 OneComme 樣板目錄結構或對應 package metadata，並映射到 Vulperonex chat overlay preset contract。

---

## 10. 已解決的設計決策

| # | 決策 |
|---|---|
| D1 | 外掛程式載入：**啟動時靜態引用** (AssemblyLoadContext / 熱載入推遲)。 |
| D2 | 工作流回覆路由：**預設為源平台**，允許每個操作覆寫 `TargetPlatform`。 |
| D3 | 事件持久化：**不儲存**。僅記錄日誌 (LOG)，具有可配置的保留/清理策略。 |
| D4 | 外掛程式範圍：**外掛程式可以同時發布和訂閱**事件（充當完整的適配器）。 |
| D5 | 前端發行：**Web 主機提供 `wwwroot` 服務**，Desktop = Web 主機 + Photino 窗口。 |
| D6 | CLI 範圍 (MVP)：**模擬 + 配置 + 規則 + 成員指令**。 |
| D6a | CLI 識別碼解析 (Phase 5.5)：`rule` positional 接受**完整 id / id prefix / `--name`**；`member` 接受**完整 id / id prefix**；多重命中 → `AMBIGUOUS_ID` + 候選表；破壞性操作（`rule disable` / `rule delete` / `member delete`）互動 REPL 走 `[y/N]` prompt、非互動需 `--yes` 否則 `CONFIRMATION_REQUIRED`。設計凍結於 `docs/phases/phase-5_5-rapid-test/cli-id-resolution-decision.md`。 |
| D7 | 成員身分：`MemberId` 為 ULID；`PlatformIdentity (Platform, PlatformUserId)` 複合鍵。 |
| D8 | 倉儲層的輕量級 CQRS：`IMemberRepository` (命令) 與 `IMemberQueryService` (查詢) 分離。 |

---

## 11. 超出範圍 (第一階段)

- Twitch 以外的平台適配器（架構設計已預留，實作推遲）。
- 觀眾帳號跨平台繫結（本機桌面工具無對外伺服器，不適用）。
- 事件重播 / 事件溯源。
- 多租戶 / SaaS 部署。
- 移動用戶端。
- AI 驅動的工作流建議。
- 熱載入外掛程式模型 (AssemblyLoadContext)。

---

## 12. 已解決的疑問

所有最初推遲的問題現在都已解決並納入規格書中。

### OQ1 — 日誌保留期預設值 ✅

```
log.file_retention_days = 7     (輪換檔案)
log.db_retention_days   = 30    (SQLite AppLogs 表)
log.db_max_size_mb      = 100   (次要上限 — 以先觸發者為準)
```

超過大小上限 → 刪除最舊的行直到低於限制。所有內容均可透過 `SystemSettings` 配置（熱載入）。見第 4.15 節。

---

### OQ2 — 外掛程式沙箱化 ✅

**決策：MVP 階段在行程內執行，完全信任。**

- 個人桌面工具 — 外掛程式作者是直播主或信任的開發者。
- `IPluginContext` 限制了暴露的表面（僅限 EventBus + Logger，不暴露 `IServiceProvider`）。
- 行程外沙箱化：除非 SaaS 多租戶成為目標，否則無限期推遲。
- 未來：使用 `AssemblyLoadContext` 進行熱卸載（而非沙箱化）。
- **文件要求：** 外掛程式以完全的 CLR 信任執行。僅安裝來自信任來源的外掛程式。

---

### OQ3 — 工作流規則儲存 ✅

**決策：規範化標頭 + 條件 (Conditions) 和操作 (Actions) 的 JSON 欄位。**

```sql
CREATE TABLE WorkflowRules (
    Id              TEXT PRIMARY KEY,
    Name            TEXT NOT NULL,
    Priority        INTEGER NOT NULL DEFAULT 100,
    IsEnabled       INTEGER NOT NULL DEFAULT 1,
    ConcurrencyMode TEXT NOT NULL DEFAULT 'Serial',
    MaxParallelism  INTEGER NOT NULL DEFAULT 1,
    EventTypeKey    TEXT NOT NULL,
    PlatformFilter  TEXT,
    ConditionsJson  TEXT NOT NULL,
    ActionsJson     TEXT NOT NULL,
    CreatedAt       INTEGER NOT NULL,  -- Unix milliseconds (DateTimeOffset.ToUnixTimeMilliseconds())
    UpdatedAt       INTEGER NOT NULL   -- Unix milliseconds; enable/disable 也更新此欄位
);
CREATE INDEX IX_WorkflowRules_EventTypeKey ON WorkflowRules (EventTypeKey);
```

規則標頭已規範化（可查詢、可索引）。條件/操作作為 JSON（架構流動 — 新外掛程式類型不需要遷移）。使用 EF Core 10 JSON 映射進行類型安全的反序列化。

---

### OQ4 — i18n 覆蓋範圍 ✅

**決策：後端返回錯誤程式碼，UI 負責翻譯。後端日誌始終使用英文。**

```json
// API 錯誤回應 — 無人類可讀字串
{ "error": "WORKFLOW_RULE_NOT_FOUND", "meta": { "ruleId": "01HK..." } }
```

Vue UI 透過 vue-i18n 將錯誤程式碼映射到在地化字串。後端不具備地區感知能力。日誌始終為英文（機器可讀，跨部署一致）。

**MVP 錯誤程式碼契約：**

| 程式碼 | HTTP | 端點 |
|------|------|-------------|
| `WORKFLOW_RULE_NOT_FOUND` | 404 | `GET/PUT/DELETE /api/rules/{id}`, `POST /api/rules/{id}/enable|disable` |
| `UNKNOWN_EVENT_TYPE_KEY` | 400 | `POST/PUT /api/rules` |
| `CIRCULAR_WORKFLOW_REFERENCE` | 400 | `POST/PUT /api/rules` |
| `UNKNOWN_SIMULATE_EVENT_TYPE` | 400 | `POST /api/simulate/{eventType}` |
| `UNKNOWN_CONFIG_KEY` | 400 | `GET/PUT /api/config/{key}` |
| `CONFIG_KEY_SECURITY_NAMESPACE` | 403 | `GET/PUT /api/config/{key}` — `security.*` 鍵被攔截 |
| `MEMBER_NOT_FOUND` | 404 | `GET /api/members/{id}` |
| `UNKNOWN_ACTION_TYPE` | 400 | `POST/PUT /api/rules` |
| `ACTION_MISSING_REQUIRED_PARAM` | 400 | `POST/PUT /api/rules` |
| `INVALID_ACTION_CONFIG` | 400 | `POST/PUT /api/rules` — `timeoutMs < 0`, 無效的 `errorBehavior` |
| `OAUTH_CREDENTIAL_NAMESPACE` | 403 | `GET/PUT /api/config/{key}` — `oauth.*` 鍵被攔截（不允許 REST CRUD）|
| `INVALID_REGEX_PATTERN` | 400 | `POST/PUT /api/rules` — `MessageContentCondition.FullRegex` pattern 無效或超過 512 字元 |
| `INVALID_QUERY_PARAM` | 400 | `GET /api/members` — `limit` 超過 200 或其他 query 參數非法 |
| `UNKNOWN_CONDITION_TYPE` | 400 | `POST/PUT /api/rules` — Conditions JSON 含未知 condition type |
| `INVALID_RULE_ID_MISMATCH` | 400 | `PUT /api/rules/{id}` — request body id 與 route id 不一致 |

---

### OQ5 — Web 主機的身分驗證模型 ✅

已在第 4.17 節 (G15) 中解決。摘要：
- 兩個埠（5000 API + 5001 Overlay）永遠以 loopback-only（IPv4 127.0.0.1 + IPv6 ::1）執行，不支援遠端存取。
- 無需身分驗證 — Kestrel bind address 本身即為安全邊界。
- 疊層執行在專用埠 5001。OBS 使用 `http://localhost:5001/overlay/*` — 乾淨的 URL，無權杖。

---

### OQ6 — Photino 離線場景 ✅

三種失敗場景及其處理：

**埠衝突（API 或疊層埠被占用）：**
```
埠始終成對分配 (ApiPort, OverlayPort)。
預設對：(5000, 5001)。

啟動時，如果對中的任一埠不可用：
  嘗試下一對：(5002, 5003) → (5004, 5005) → (5006, 5007) → (5008, 5009)
  成對嘗試 — 防止 API 自動跳轉到疊層的預設埠。
  所有嘗試均失敗 → Photino 對話框：
    "埠 5000–5009 不可用。請在設定中配置不同的埠對。"

可配置：appsettings.json →
  "Web": { "ApiPort": 5000, "OverlayPort": 5001 }
  (手動配置跳過自動遞增；使用者需自行解決衝突。)
```

**Web 主機在會話中崩潰：**
```
Photino 失去連線 → 顯示嵌入式靜態回退 HTML
（打包在 Photino 二進位檔案中，無 Web 主機依賴）
回退頁面顯示：錯誤描述 + [重啟] 按鈕
```

**啟動時資料庫遷移失敗：**
```
遷移在 Web 主機啟動前執行
失敗 → 中止啟動 → Photino 對話框：
  "資料庫更新失敗：{error}"
  按鈕：[開啟日誌資料夾] [退出]
不進行自動修復以防止資料損壞。
```

---

## 下一步

1. **準備好進行第一階段實作。** SPEC.md 和 plan.md 已完成多輪審查；所有 P1 問題已解決。從任務 1 開始。
2. plan.md 包含完整的任務列表、驗收準則、依賴圖和文件產出。
3. todo.md 是執行清單 — 在那裡追蹤進度。
4. 根據 BDD 情境和 TDD 紅/綠/重構，逐任務實作。
