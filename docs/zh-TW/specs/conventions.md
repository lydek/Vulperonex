# 開發指南與測試規範

> [← Back to Master Specification](../SPEC.md)

## 5. 指令

```bash
# --- 後端 ---
dotnet build
dotnet test
dotnet run --project src/Hosts/Vulperonex.Web
dotnet run --project src/Hosts/Vulperonex.Desktop

# --- CLI（群組：rule / timer / config / member / simulate / twitch）---
vulperonex simulate chat    "hi" --user-id alice --display-name "Alice"
vulperonex simulate follow  --user-id alice
vulperonex simulate sub     --user-id alice --tier 1000
vulperonex simulate checkin --user-id alice --stamp-count 1 [--skip-cooldown]
vulperonex config get streaming.platform
vulperonex config set streaming.platform twitch
vulperonex member list
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
    /// 全域唯一事件 ID。用於重新啟動時 TDQ 重播的重複抑制。
    /// 格式：ULID 字串。配接器必須從平台事件 ID（如有）填充，否則生成新的 ULID。
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

### 6.2 C# — 匯流排與配接器契約

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
    ///       CLI --wait 使用此方法，同樣不相依 handler 錯誤計數。
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
/// InvokePluginAction 執行器傳遞給外掛程式操作處理程式的每個操作呼叫上下文。
/// 攜帶特定事件的資料，且不在操作或規則之間共享。
/// </summary>
public interface IPluginActionContext
{
    /// <summary>
    /// 完全限定的重複抑制鍵：(EventId, WorkflowRuleId, ActionIndex[, InvocationId])。
    /// 外掛程式必須將此完整鍵（而非僅 EventId）用於 ActionExecutionLog 條目。
    /// 同一 EventId 可能出現在多個規則中；僅使用 EventId 會導致跨規則的重複抑制衝突。
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
    /// 外掛程式必須透過 IPluginActionContext.ActionExecutionKey 實作重複抑制，以應對任何外部副作用。
    /// Timeout 逾時後底層 Task 可能仍在執行 — 外掛程式應在 CancellationToken 觸發後記錄 warning，
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
               ╱────╲   - Domain 無基礎架構相依
              ╱      ╲  - Domain/Application 中無 "Twitch" 字串
             ╱        ╲
            ╱──────────╲ 整合測試
           ╱            ╲ - SimulationAdapter → Bus → WorkflowEngine → DB
          ╱              ╲
         ╱────────────────╲ 單元測試 (絕大部分)
        ╱                  ╲ - 領域邏輯、對應、處理程式、執行器
```

### 7.2 位置

- `tests/Vulperonex.Tests.Unit/` — 純單元測試，無 I/O。
- `tests/Vulperonex.Tests.Integration/` — 記憶體內 SQLite + Simulation 配接器端對端測試。
- `tests/Vulperonex.Tests.Architecture/` — 層級規則強制執行。
- `src/frontend/tests/` — Vitest + Vue Test Utils。

### 7.3 覆蓋率目標

- 領域層 (Domain)：> 90% — 僅針對 `Vulperonex.Tests.Unit` 測量（領域是純邏輯，無 I/O）。
- 應用層 (Application)：> 80% — 僅針對 `Vulperonex.Tests.Unit` 測量。整合測試**不**併入此門檻（coverlet.msbuild 無法在單個指令中合併兩個測試專案的報告）。如果因為應用層行為僅被整合測試覆蓋而導致單元測試覆蓋率低於 80%，解決方案是新增聚焦的單元測試（使用 Fakes/Mocks），而非放寬門檻或切換到合併報告。
- 配接器 (Adapters)：透過 SimulationAdapter 等效性進行整合測試（真實配接器使用相同的領域對應邏輯）。

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

> **漂移註（CI 未接線）：** 已引用 `coverlet.msbuild` 6.0.2，但 repo 目前**無 `.github/workflows` / CI pipeline** — 這些覆蓋率指令、NetArchTest 閘門（§6.1/§8.1）與遷移分類器（§4.11）皆以本地 `dotnet test` 執行，並非自動 build 中斷。本規格各處的「CI」描述為預期閘門；實際 workflow 接線尚待完成。

### 7.4 BDD + TDD 紀律

- 每個行為都從 BDD 風格的情境開始：Given / When / Then (給定 / 當 / 那麼)。
- 情境是驗收契約；在實作被視為完成前，它必須對應到一個或多個自動化測試。
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

- 在任何提交前執行所有適用的測試套件：始終執行 `dotnet test`；一旦 `src/frontend/` 存在，則必須執行 `pnpm test` 與 `pnpm build`（在任務 19 之前的後端任務中可略過）。`pnpm lint` 為**手動驗證步驟**（CI 不強制），於各 Checkpoint 手動執行一次。
- 新事件實作 `IStreamEvent` 且為不可變的 `record`。
- 配接器程式碼位於 `Adapters/Vulperonex.Adapters.<Platform>/`。
- 平台特定的術語**遠離** `Domain` 和 `Application` 專案。
- 使用 `MemberId` (ULID) 作為規範的會員鍵，絕不使用平台 UserId。
- 在 CI 中執行架構測試。

### 8.2 需先諮詢 (Ask first)

- 向解決方案新增頂級專案（**Task 1 初始專案已授權，不需逐一詢問；Task 1 以外的額外新專案才 ask-first**）。
- 刪除或重新命名欄位的架構遷移。
- 新增 NuGet / npm **相依套件**（包含 oxlint 等 dev tool — 詢問後安裝一次，**已安裝後執行 lint 屬驗證步驟，不需再詢問**）。例外：Phase 1 Task 1c 所需且本 SPEC 已命名的測試/coverage 套件已預先授權，不需逐一詢問：`xUnit 3`、`NSubstitute`、`FluentAssertions 7`、`NetArchTest`、`coverlet.msbuild 6.0.2`。
- 更改公共外掛程式契約 (`IVulperonexPlugin`)。
- 在第二階段之後修改核心領域事件的形狀。

### 8.3 嚴禁執行 (Never do)

- 在 `Application` 或 `Domain` 專案中引用 `Twitch*`（或任何平台特定）類型。
- 在發布後變更事件物件（狀態變更應產生新事件）。
- 在同一個儲存庫上混合命令和查詢操作（輕量級 CQRS）。
- 繞過事件匯流排 — 配接器絕不直接呼叫處理程式。
- 將事件持久化到資料庫（僅限日誌記錄）。
- 提交機密、OAuth 權杖或 `App_Data/*.db`。

---
