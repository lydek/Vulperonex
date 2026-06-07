# 架構核心概念

> [← Back to Master Specification](../SPEC.md)

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

`PlatformIdentity` 具有 `UNIQUE (Platform, PlatformUserId)` 約束。實際 resolver 先 `SELECT`（既有 → 回傳），否則 EF `Add` + `SaveChanges`，並以**行程級 `static SemaphoreSlim` 閘門**序列化，使同一 identity 的並發首事件不會重複插入；UNIQUE 約束為最後防線。*（實作註：早期設計曾提議用 raw `INSERT OR IGNORE` 依賴 SQLite WAL、不加應用層鎖；最終程式碼改用閘門 + EF — 對單寫者桌面情境更簡單且 provider 可攜。）*

**CA 邊界：** Application 只定義 `IMemberResolver` port 介面（`ResolveMemberIdAsync(PlatformIdentity) -> MemberId`）；EF Core 實作（`MemberResolver`）放在 Infrastructure 層，不得出現在 Application 或 Domain。

```csharp
// Application port（介面只在 Application）
public interface IMemberResolver
{
    // 回傳 MemberId (ULID string) — 既有或新建
    Task<string> ResolveMemberIdAsync(PlatformIdentity identity, CancellationToken cancellationToken = default);
}

// Infrastructure 實作（偽程式碼）— 閘門序列化 get-or-create
await Gate.WaitAsync(ct);   // private static readonly SemaphoreSlim Gate = new(1, 1)
var existing = await db.PlatformIdentities
    .Where(x => x.Platform == identity.Platform && x.PlatformUserId == identity.PlatformUserId)
    .Select(x => x.MemberId).SingleOrDefaultAsync(ct);
if (existing is not null) return existing;
var memberId = NewUlidString();
db.Members.Add(new MemberEntity { MemberId = memberId, ... });
db.PlatformIdentities.Add(new PlatformIdentityEntity { MemberId = memberId, Platform = ..., PlatformUserId = ... });
await db.SaveChangesAsync(ct);
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
    DisplayName      TEXT,           -- 快取的顯示名稱
    AvatarUrl        TEXT,
    ColorHex         TEXT,
    BadgesJson       TEXT,           -- 徽章字串的 JSON 陣列
    IsSubscriber     INTEGER NOT NULL DEFAULT 0,
    SubscriptionTier TEXT,           -- "1000" | "2000" | "3000" | null
    TotalBitsGiven   INTEGER NOT NULL DEFAULT 0,
    FetchedAt        TEXT NOT NULL,  -- ISO-8601 DateTimeOffset（EF 預設映射）
    PRIMARY KEY (Platform, PlatformUserId)
);
```

**介面 (Adapters.Abstractions)：**

```csharp
public interface IPlatformUserInfoCache
{
    Task<PlatformUserDisplayInfo?> GetAsync(string platform, string platformUserId, CancellationToken ct = default);
    // cache miss → 建立預設 PlatformUserDisplayInfo row
    //   (DisplayName=null, AvatarUrl=null, ColorHex=null, Badges=[], IsSubscriber=false,
    //    SubscriptionTier=null, TotalBitsGiven=0, FetchedAt=UtcNow)，再套用 updater。
    //   套用後不回傳 null。（無獨立 SetAsync — upsert 經 UpdateAsync。）
    Task<PlatformUserDisplayInfo> UpdateAsync(
        string platform, string platformUserId,
        Func<PlatformUserDisplayInfo, PlatformUserDisplayInfo> updater,
        CancellationToken ct = default);
}

public sealed record PlatformUserDisplayInfo(
    string Platform,
    string PlatformUserId,
    string? DisplayName,
    string? AvatarUrl,
    string? ColorHex,           // null 或 ^#[0-9A-Fa-f]{6}$；不接受 CSS function / named color / alpha
    IReadOnlyCollection<string> Badges,
    bool IsSubscriber,
    string? SubscriptionTier,
    long TotalBitsGiven,
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

## 4.6 WorkflowRule 模型

```
WorkflowRule
├── Id: ULID
├── Name: string           // 顯示標籤，**不唯一**；CLI/API list/show 以 Id 為主鍵（Name 可重複）
├── EventTypeKey: string?   // "user.message"、"user.followed" 等。Phase 8 提升至 rule 根層（不再巢狀於 Trigger）。僅當 IsSubWorkflow = true 時為 NULL
├── IsSubWorkflow: bool     // true → 無自身 trigger，只能經 InvokeSubWorkflowAction 調用。EventTypeKey 與 Trigger 必須皆為 null（否則 400 SUB_WORKFLOW_MUST_NOT_HAVE_TRIGGER）
├── Priority: int          // 數字越小 = 優先級越高 (1 在 10 之前執行)
├── CreatedAt: DateTimeOffset
├── IsEnabled: bool
├── Version: int           // 樂觀並行 token；每次儲存遞增。PUT 版本不符 → 409 WORKFLOW_RULE_CONFLICT
├── ExecutionMode: Serial (串行) | Parallel (並行)   // Phase 8 前名為 ConcurrencyMode
├── MaxParallelism: int    // 僅在 ExecutionMode = Parallel 時適用；有效範圍 [1, 64]；超出 → INVALID_ACTION_CONFIG
├── TimeoutSeconds: int    // rule 層級執行預算；有效範圍 [0, 86400]；超出 → INVALID_ACTION_CONFIG
├── Throttle: WorkflowThrottlePolicy   // { MaxConcurrent [0,64], CooldownSeconds [0,86400], PerUserCooldown, PerUserCooldownSeconds [0,86400] }；預設 None
├── MatchCondition: string?   // 選用的 rule 層級 NCalc 閘門，於 typed trigger filter 之後評估（Phase 8 自 Trigger 提升）；評估失敗會記錄 RuleId（§4.26）
│
├── Trigger: WorkflowTrigger?          // sub-workflow rule 為 null
│   └── Filter: Dictionary<string,string>   // 每事件型別的 typed filter 鍵（§4.26）；以 ITriggerMetadataProvider.GetFilterFieldsFor(EventTypeKey) 驗證；未知鍵 → 400 INVALID_FILTER_KEY。Phase 8 取代了舊的巢狀 EventTypeKey/PlatformFilter/MatchCondition；rule 層級平台過濾已移除
│
├── Conditions: List<IWorkflowCondition>   // AND 邏輯 — 必須全部通過
│   ├── UserRoleCondition (使用者身分條件)
│   │   ├── Roles: StreamRole 標記（`Subscriber | Moderator | Vip | Follower | Broadcaster` — 從 adapter badge/role 欄位映射）
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
    │   ├── TargetPlatform: string?    // null = 來源平台；非 null 時 save-time 只驗非空白字串（不驗 adapter 是否啟用）
    │   │                              // Runtime：若無 sender 已注冊該 platform → log warning + skip action（不 crash；adapter 可動態啟停）
    │   ├── Channel: string?           // null = 來源頻道；「內部化」— 留空時 executor 由 trigger event 自動推導
    │   └── DedupKey: string?          // null = 由 execution key 自動產生；少需顯式覆蓋
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

**Rule 層級失敗處理與每步欄位（Phase 8）：**
- `OnFailureSteps: List<IWorkflowAction>` — 當正常步驟失敗時執行的補償步驟；action 目錄與每步欄位與 `Actions` 相同。
- 每個 `IWorkflowAction` 另攜帶 `ExecutionCondition: string?`（選用的每步 NCalc 閘門，評估為 false 時跳過該步）與 `OutputVariable: string?`（選用，命名以供後續步驟取用該步結果）。每步錯誤欄位見 §4.8。
- Trigger 過濾、支撐編輯器的 metadata 合約，以及 NCalc/filter 可觀測性詳見 §4.26。

**優先順序解析：** 按 `Priority ASC`，然後 `CreatedAt ASC`，最後 `Id ASC`（ULID 字典序，確保無 DB 排序不穩定問題）。

**並行語意：**
- `Serial` (預設)：**作用域為單一 WorkflowRule**（每個 rule 有獨立 queue，rule A 排隊不影響 rule B）。同一 rule 的事件一次執行一個。
- `Parallel`：同一 rule 的事件最多 `MaxParallelism` 個並行執行。`MaxParallelism` 有效範圍 `[1, 64]`；超出範圍在 rule 儲存時拒絕（400 + `INVALID_ACTION_CONFIG`）。
- 匹配同一事件的不同規則始終獨立執行（無跨規則序列化）。

**操作由外掛程式驅動（可熱插拔）：** `SendChatMessage` 和 `InvokeSubWorkflow` 是內建的。所有領域特定的操作（`TriggerEffect`, `AddPoints`, `PlaySound`）都是 `InvokePluginAction` — 它們需要載入相應的外掛程式。如果外掛程式缺失，操作會記錄警告並跳過。

**條件評估短路：** 第一個失敗的條件會停止評估（建議按「成本從低到高」的順序排列）。

---

## 4.7 平台適配器韌性 (G5)

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

## 4.8 工作流操作錯誤處理 (G6)

每個操作具有 `ErrorBehavior` 以及全域操作超時：

```csharp
public enum ErrorBehavior
{
    ContinueOnError,   // 失敗 → 記錄日誌 → 繼續下一個操作
    StopOnError,       // (預設) 失敗 → 停止此規則中的剩餘操作
    RetryOnError,      // 失敗 → 在放棄前按退避策略重試
}
```

每個 `IWorkflowAction` 基類攜帶：

```
ErrorBehavior:      ErrorBehavior = StopOnError   // 預設改為 fail-fast（Phase 8 前為 ContinueOnError）
MaxRetries:         int = 0       // 有效範圍 [0, 10]；超出 → `INVALID_ACTION_CONFIG`
BackoffMs:          int = 500     // 有效範圍 [100, 30000]；超出 → `INVALID_ACTION_CONFIG`
TimeoutMs:          int = 10000   // 有效範圍 [0, 60000]；超出 → `INVALID_ACTION_CONFIG`
                                  // 超過此時間 CancellationToken 被取消；執行器停止等待
                                  // .NET 無法強行終止非同步任務 — 外掛程式必須遵守 CancellationToken
ExecutionCondition: string?       // 選用的每步 NCalc 閘門；評估為 false 時跳過該步
OutputVariable:     string?       // 選用，命名以供後續步驟取用該步結果
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

## 4.9 配置儲存 (G7)

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

## 4.10 模組生命週期 (G8)

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
5. 模組 (Modules)             — MemberModuleHostedService + WorkflowEngine + OverlayEventForwarder（疊層推送）
6. 適配器 (Adapters)           — TwitchAdapter (僅在模組就緒後開始發布)
7. Web / SignalR Hub
```

**關閉順序則相反。** 適配器首先停止（不再有新的傳入事件） → 模組清空進行中的工作 → 基礎設施關閉。

---

## 4.11 資料庫遷移策略 (G9)

**啟動時自動執行（已實作）。顯式 CLI 執行為規劃中，尚未交付。**

```
啟動時 (始終)：
  await db.Database.MigrateAsync()
  → 僅自動執行增量遷移
  → 應用程式絕不會以過時的架構啟動

CLI (手動控制) — 規劃中，尚未實作：
  vulperonex db migrate        → 執行掛起的遷移
  vulperonex db status         → 列出已應用 / 掛起的遷移
  vulperonex db rollback <id>  → 回滾（需要確認提示）
  （CLI 僅提供 rule / timer / config / member / simulate / twitch 群組；
   目前沒有 `db` 命令群組。遷移僅於 host 啟動時執行。）
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

1. **MigrationClassifier**（`Vulperonex.Infrastructure.Migrations`）— 純函式 `Classify(IReadOnlyList<MigrationOperation>) → MigrationRisk`，將遷移操作分級為 `Safe` / `ReviewRequired` / `Destructive`：
   ```csharp
   var risk = MigrationClassifier.Classify(migrationBuilder.Operations);
   // DropTable/DropColumn/Rename + raw DROP|DELETE|TRUNCATE  → Destructive
   // raw SQL 含 ALTER（如 ALTER TABLE … ADD COLUMN）          → ReviewRequired（保守）
   // 僅 CreateTable/AddColumn                                 → Safe
   ```
   單元測試於 `tests/Vulperonex.Tests.Unit/Infrastructure/Migrations/MigrationClassifierTests.cs`。**注意：raw SQL 中只要含 `ALTER` 即視為 review-required（保守策略）。**
2. **PR 審查要求** — 新增遷移時由審查者執行/檢視分類器；`Destructive`/`ReviewRequired` 結果在合併前需手動審查把關。

> **漂移註：** 早期設計曾提議在 `Tests.Architecture` 中實例化每個 `Migration`、執行 `Up()`，並在遷移未標記 `[DestructiveMigration]` 屬性時**讓 CI 失敗**。最終程式碼只交付可重用的 `MigrationClassifier` + 其單元測試 — **不存在 `[DestructiveMigration]` 屬性，也無針對真實遷移的自動失敗 CI 閘門**；破壞性遷移安全為審查制，非由 build 中斷強制。

EF Core 遷移檔案在儲存庫中的路徑為 `src/Vulperonex.Infrastructure/Migrations/`。

**SQLite 檔案位置（透過 `appsettings.json → Database:Path` 配置）：**

```
Windows : %AppData%\Vulperonex\vulperonex.db
Linux   : ~/.local/share/Vulperonex/vulperonex.db
macOS   : ~/Library/Application Support/Vulperonex/vulperonex.db
```

---

## 4.12 EventTypeKey 類型安全 (G10)

基於字串的 `EventTypeKey` 很靈活（外掛程式可以定義新類型）但很脆弱 — 拼寫錯誤會導致規則無聲地失效。

**保護機制：`IStreamEventTypeRegistry` + 儲存時驗證。**

```csharp
public interface IStreamEventTypeRegistry
{
    void Register(StreamEventTypeMetadata metadata);   // （原為 Register(key, description, isSystemEvent)）
    bool IsKnown(string key);             // 含 system events（用於路由/dispatch）
    bool IsKnownForWorkflow(string key);  // 排除 system events（用於 WorkflowRule 驗證）
    IReadOnlyCollection<StreamEventTypeMetadata> GetAll();  // 不含 IsSystemEvent=true 的項目
                                                            // API endpoint 再投影成 EventTypeDescriptor（補 IsSimulatable）
}

// Registry 儲存/記錄型別（Vulperonex.Application.EventTypes）— 取代先前的 internal RegistryDescriptor
public sealed record StreamEventTypeMetadata(
    string Key,
    string Description,
    bool IsSystemEvent = false);  // true for platform.connection_changed；GetAll() 排除。實作：InMemoryStreamEventTypeRegistry

// API DTO — GET /api/event-types endpoint 回傳（由 endpoint 從 StreamEventTypeMetadata 投影）
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
_registry.Register(new StreamEventTypeMetadata("user.message",  StreamEventDescriptions.UserMessage));
_registry.Register(new StreamEventTypeMetadata("user.followed", StreamEventDescriptions.UserFollowed));

// MyPlugin.InitializeAsync()
_registry.Register(new StreamEventTypeMetadata("plugin.my_plugin.event", "我的外掛程式自訂事件"));
```

**安全檢查點：**

| 時機 | 行為 |
|---|---|
| WorkflowRule 儲存 (API / CLI) | 呼叫 `IsKnownForWorkflow(key)` — 未知或系統事件（`platform.connection_changed`）均拒絕；不使用 `IsKnown()`（`IsKnown` 包含系統事件，會誤放行） |
| UI 規則編輯器 | 僅顯示已註冊鍵的下拉選單 — 無自由文字輸入 |
| 從資料庫載入具有未知鍵的規則 (外掛程式已解除安裝) | 記錄警告，跳過規則 — 不崩潰 |

資料庫中的未知鍵會優雅地降級，而非致命錯誤 — 允許移除外掛程式而不損壞應用程式。

---

## 4.13 WorkflowRule 編輯介面 (G11)

REST API 是唯一規範的寫入路徑。UI 和 CLI 都呼叫 API — 兩者都不直接寫入資料庫。API 介面僅 loopback，並由 `AdminGuardMiddleware` 把關（無使用者帳號，但對 mutation 做 CSRF + Host + Origin/Referer 檢查 — 見 §4.17）。

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

所有 UI 和 CLI 均專門透過 REST 存取 Web 主機，兩端用戶端均無直接資料庫存取權限。API 埠僅 loopback（IPv4 `127.0.0.1` + IPv6 `::1`）；無使用者帳號，但對 mutation 請求做把關（CSRF + Host + Origin/Referer — 見 §4.17）。疊層埠可額外開放 LAN，並以疊層存取金鑰保護。

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

## 4.14 疊層架構 (G12)

`OverlayModule` 訂閱相關的領域事件，將其轉換為 `OverlayPayload` DTO，並推送到 SignalR 組。前端疊層頁面作為 OBS 瀏覽器源連線。

**瀏覽器源 URL：**
```
http://localhost:5001/overlay/chat.html — 滾動聊天
http://localhost:5001/overlay/alerts    — 追隨 / 訂閱 / 突襲警報
http://localhost:5001/overlay/member-card.html — 成員卡片顯示
```

每個 URL 都是一個獨立的 Vue 路由，在掛載時連線到其 SignalR 組。不需要身份驗證（OBS 必須直接連線）。

**聊天 overlay 樣板系統：**

- `/overlay/chat` 必須支援**多個內建樣板 / preset**，至少包含 Vulperonex 預設樣板；內建樣板需可對應「單一樣板目錄 / 單一樣板封包」概念，而不是寫死單版面。
- 樣板選擇必須是**設定層級**能力，而不是要求使用者直接改前端原始程式碼；後續可擴充為樣板清單、預覽、匯入 / 匯出。
- 樣板渲染仍必須遵守 MVP 安全界線：使用 DTO 白名單與 text binding；**不得以 `v-html` 或任意 raw HTML 直接穿透 event payload**。
- **OneComme 相容屬於擴充功能 / 外掛程式類型能力，不屬於 core 直接內建整合。** Core 只需提供可擴充的樣板 preset / package contract；OneComme 相容可透過外掛、樣板匯入器、或 adapter 套件實作。
- 以 **OneComme** 作為優先相容目標之一。目的不是 1:1 複製其內部實作，而是提供足夠接近的樣板結構 / 匯入映射 / 相容契約，降低既有 OneComme 使用者的遷移成本，同時維持 core 與第三方樣板生態的邊界。

#### 4.14.1 Overlay Preset Contract (Vue/靜態內建 preset)

> **⚠️ 部分已被取代（見 §4.14.3）：** **Static HTML Override** 軌道（`/overlay/custom/{slug}.html`、`custom:{slug}` preset 值）、下方的 **Custom HTML Upload** 子節，以及 **OneComme 經上傳匯入** 路徑，已隨 custom-preset pipeline 一併**移除**。僅保留 **Built-in Presets** 列與 config-driven 自訂（文字 + 會員卡圖片經 `POST /api/overlay/assets`，§4.14.3）。下方 custom-HTML 內容僅作歷史參考。

**動機：** 一般實況主想客製 overlay 視覺，不應被迫安裝 Node.js / pnpm / Vite。同時 Vulperonex 仍要提供高品質預設 Vue 版本。

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
| `overlay.chat.preset` | string | `kapchat` | Chat preset key（僅內建 key；`custom:{slug}` 已隨 custom-preset pipeline 移除，§4.14.3） |
| `overlay.member.preset` | string | `rotan-checkin` | Member preset key |
| `overlay.alerts.preset` | string | (預設) | Alerts preset key |

**工作流聊天輸出與疊層人物角色（§4.14.4）：**

| 設定 key | 型別 | 預設 | 說明 |
|---------|------|------|------|
| `workflow.chat.output_destination` | string | `dual` | `SendChatMessageAction` 輸出去向：`dual` / `platform_only` / `overlay_only`。含 overlay 時，訊息經 `IWorkflowChatOverlaySink` 直接渲染進 `/overlay/chat`（不需平台往返）。 |
| `overlay.chat.assistant_display_name` | string | (內建) | 疊層中工作流/助理發出之聊天訊息顯示的名稱。 |
| `overlay.chat.assistant_avatar_url` | string (URL) | 空 | 助理訊息的頭像。 |
| `overlay.chat.checkin_display_name` | string | (內建) | 渲染進 chat overlay 的簽到卡所用顯示名稱。 |
| `checkin.reset_time_local` | string `HH:mm` | `05:00` | 每日簽到計數翻轉的本地時刻。 |
| `checkin.repeat_card_enabled` | bool | true | true 時同日重複簽到仍發出疊層卡（以 event id 去重）；false 則每週期僅首次顯示。 |

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

**背景：** Phase 7C 已建立 static member-card preset + `OverlayMemberHub`，但 `TriggerCheckInActionExecutor` 僅寫入 SQLite `MemberStreamState`，從未發布事件給 `OverlayEventForwarder`，使 `/overlay/member-card.html` / `/overlay/member` 在實際運作時收不到任何 push。Phase 7C cross-hub chat embed (`memberSnapshot`) 也只在 chat 事件路徑查 DB，與 checkin action 無連動。

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

#### 4.14.3 自訂 HTML Overlay 編輯與部署 Pipeline（已移除 / Superseded）

> **⚠️ 已移除：** 本節描述的線上 Monaco 編輯器、zip/HTML 上傳、custom-preset draft/deploy/validate/history/rollback pipeline 已**整體移除**（含後端 `OverlayPresetStore` 相關方法、`/api/overlay/custom-presets/*` 端點、`wwwroot/overlay/custom/` 目錄與既有自訂 preset）。
>
> **原因：** 任意上傳/編輯 HTML/JS/CSS 難以保證安全與格式正確，且對絕大多數使用者過於進階。
>
> **取代方案 —— Overlay 受限自訂（Overlay Customization）：** 改提供安全、受限的自訂介面（`OverlayPresetsView` 內），只允許：
> - **文字替換**：助理顯示名稱 / 簽到顯示名稱 / 助理頭像網址，寫入既有 config keys（`overlay.chat.assistant_display_name`、`overlay.chat.checkin_display_name`、`overlay.chat.assistant_avatar_url`）。
> - **圖片替換（會員卡）**：背景圖 + 印章圖，經 `POST /api/overlay/assets`（僅限影像、≤2MB、副檔名+content-type 驗證）存到 `wwwroot/overlay/assets/{guid}.{ext}`，回傳的 URL 寫入既有 config keys（`overlay.member.background_url`、`overlay.member.stamp_url`）。
> - Overlay 端維持既有 config-driven 讀取（`member-card.js` 既已 fetch 上述 image keys 並套用）。
> - 內建 preset 選擇（chat/member/alerts）與 OBS/LAN URL 複製維持不變。
>
> 以下原 pipeline 設計內容僅作歷史紀錄，**不再實作**。

**背景（歷史）：** Phase 7C 已落地 `POST /api/overlay/custom-presets` 純 zip 上傳，但有兩個結構性問題：

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
// Program.cs (MVP) — 外掛以 IVulperonexPlugin DI service 註冊；
// StaticPluginRegistry(IEnumerable<IVulperonexPlugin>) 以 IPluginRegistry 收集。
builder.Services.AddSingleton<IVulperonexPlugin, OneCommeBridgePlugin>();
builder.Services.AddSingleton<IPluginRegistry, StaticPluginRegistry>();
// （無 AddVulperonexPlugin<T> 擴充；每個外掛實作 IVulperonexPlugin.InitializeAsync(IPluginContext) 並自行註冊其 event-type key。）
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

**雙埠架構：loopback-only 的 API 埠 + 預設 loopback-only、可選擇對外開放 LAN 供跨機 OBS 的疊層埠。** 埠以成對方式從 `(5000, 5001)` 起自動分配（見 OQ6）；下列為預設值。

```
appsettings.json:
  "Web": { "ApiPort": 5000, "OverlayPort": 5001 }   // 預設值；實際成對由自動分配決定
  "Overlay": { "Lan": { "Enabled": false, "BindAddress": "0.0.0.0" } }  // 選擇性開放 LAN 疊層
  "Security": { "CsrfTokenPath": null }              // 選擇性覆蓋 admin CSRF token 檔路徑
```

**埠繫結（Kestrel）：**
```
ApiPort     → 永遠僅 loopback（127.0.0.1 + ::1）。Admin SPA API、所有 mutation、
              OAuth 與 overlay 編輯器都在此，永不對外 LAN。
OverlayPort → 永遠 loopback（admin 預覽），且當 Overlay:Lan:Enabled = true 時，
              額外繫結 Overlay:Lan:BindAddress（預設 0.0.0.0 = 所有介面），
              讓另一台機器的 OBS 可載入即時疊層。
```

**存取控制不再只靠「bind address」。** 單一 `AdminGuardMiddleware` 強制兩種規制（先前「無認證 / 無 middleware」模型已被取代）：

1. **Loopback admin/mutation 請求** — 所有非 GET 的 `/api/*`，以及*全部* `/api/overlay/*`，必須通過：
   - **Loopback** remote IP（null remote IP 直接拒絕 — 防 Unix-socket/proxy 偽冒）。
   - **Host 白名單**（`localhost` / `127.0.0.1` / `[::1]`，忽略 port）— 防 DNS rebinding → 400 `ORIGIN_MISMATCH`。
   - **`X-Admin-Csrf`** header 比對每行程 admin CSRF token（constant-time）→ 400 `MISSING_OR_INVALID_CSRF_HEADER`。
   - **`Origin`/`Referer`** 至少一個存在且 host 相符 → 400 `MISSING_ORIGIN_OR_REFERER_HEADER` / `ORIGIN_MISMATCH` / `INVALID_ORIGIN_HEADER` / `REFERER_MISMATCH` / `INVALID_REFERER_HEADER`。
   - **`GET /api/overlay/csrf-token`** 為 bootstrap 例外：僅檢查 loopback + Host（豁免 CSRF），讓 SPA 取得 token。回傳當前 token。

2. **非 loopback（LAN）請求** — 限定於即時疊層介面，並以**疊層 LAN 存取金鑰**把關：
   - 靜態 SPA/疊層 HTML 與資產：僅 GET、不需金鑰（公開用戶端程式碼；子資源載入無法附 header）。
   - SignalR hubs（`/hubs/*`）與一小組 overlay-safe config GET（`overlay.{chat,member,alerts}.preset`、`overlay.chat.show_member_card`、`overlay.member.background_url`、`overlay.member.stamp_url`）：需金鑰，以 `?k=<key>` query 或 `X-Overlay-Key` header 傳遞。
   - 其餘（admin API、任何 mutation、OAuth、編輯器、`/health`、`/openapi`）→ 403。

**Admin CSRF token：** 256-bit Base64Url，**每次行程啟動重新產生**，寫入 `.admin-csrf-token`（或 `Security:CsrfTokenPath`）並設 owner-only ACL，關閉時刪除。重啟會使已開啟的 admin 分頁失效（需刷新重取）。*已知取捨：* 任何能對 `/api/overlay/csrf-token` 發 loopback 請求的本機行程都能讀到 token — 對無中央認證的單機桌面工具為可接受妥協。

**疊層 LAN 存取金鑰：** 256-bit Base64Url，存於 `SystemSettings`（`overlay.lan.access_key`），首次啟用 LAN 存取時產生一次並**跨重啟穩定**，使 OBS URL 持續可用。`GET /api/overlay/lan-info`（admin/loopback）回傳金鑰 + 建議 LAN host URL 供貼入 OBS。

**Overlay DTO 安全：** 疊層 DTO 必須是公共安全投影 — 即使伺服器永遠 loopback-only，仍需嚴格 DTO 白名單（防止日後擴充時誤加欄位、防止 SignalR 序列化過度曝光）：

| 疊層 | 允許的欄位 | 禁止的欄位 |
|---|---|---|
| `/overlay/chat`（`OverlayChatPayload`） | SchemaVersion, EventId, Timestamp, DisplayName, ColorHex, Segments, Badges, Roles?, AvatarUrl?, MemberSnapshot?, Variant? | MemberId, UserId, TotalBitsGiven |
| `/overlay/alerts`（`OverlayAlertPayload`） | SchemaVersion, EventId, Timestamp, DisplayName, EventType, Tier, Replayed | MemberId, PlatformUserId |
| `/overlay/member`（`OverlayMemberPayload`） | SchemaVersion, EventId, Timestamp, DisplayName, AvatarUrl?, CheckInCount (僅限當前會話), RoundIndex, StampSlotInRound | MemberId, TotalLoyalty, LinkedPlatforms |

`SchemaVersion` 固定為 `1`。`EventId` 是 overlay public delivery id，用於前端去重，不得使用 MemberId、PlatformUserId 或其他內部 identity；優先使用 platform-provided id（IRC `msg-id` / EventSub `message_id`），缺值時 adapter 生成 ULID 並標記為 synthetic。`Timestamp` 為 UTC ISO-8601 event time，用於前端排序。**三種 payload 現皆帶 `EventId` + `Timestamp`（member 為了前端去重重複簽到卡而新增 — §4.14.2）。** `MemberSnapshot`（chat）為選用的跨 hub 會員 chip（§4.14，由 `overlay.chat.show_member_card` 控制）；`Variant`/`Roles` 為渲染提示；`Replayed`（alerts）標記 EventSub 重播重送。`OverlayEventForwarder` 在 SignalR 推送前將領域事件轉換為這些受限的 DTO。允許列表在 DTO 類型級別強制執行 — 無動態映射。

**OBS 瀏覽器源 URL：**
```
http://localhost:5001/overlay/chat.html
http://localhost:5001/overlay/alerts
http://localhost:5001/overlay/member-card.html
```

**DB 路徑解析規則（CLI 與 Web host 共同遵守）：** DB path 解析：`appsettings.json → Database:Path`（若存在），否則使用 OS app-data 預設路徑（見 §4.11）。**`Database:Path` 不允許透過 `appsettings.{Environment}.json` 或環境變數覆蓋** — Web host 與 CLI 均只讀主要 `appsettings.json`，確保兩者永遠讀相同 DB。開發環境若需自訂路徑，直接修改 `appsettings.json`（不使用 Development override）。

**Kestrel 繫結（API 僅 loopback；Overlay loopback + 選擇性 LAN）：**
```csharp
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // API 埠：永遠僅 loopback — admin/mutation/auth 介面永不對外 LAN。
    kestrel.Listen(IPAddress.Loopback,     apiPort);
    kestrel.Listen(IPAddress.IPv6Loopback, apiPort);

    // Overlay 埠：永遠 loopback（admin 預覽）…
    kestrel.Listen(IPAddress.Loopback,     overlayPort);
    kestrel.Listen(IPAddress.IPv6Loopback, overlayPort);

    // …外加選擇性 LAN 繫結供跨機 OBS（應用層以疊層金鑰把關）。
    if (lanEnabled && TryParseBindAddress(lanBindAddress, out var bindIp) && !IPAddress.IsLoopback(bindIp))
        kestrel.Listen(bindIp, overlayPort);   // 例如 0.0.0.0 → IPAddress.Any
});
```

---
