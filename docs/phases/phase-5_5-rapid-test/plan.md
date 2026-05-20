# 第 5.5 階段計畫 - Rapid-Test Enablement

> 父計畫：`tasks/plan.md`
> 父核對清單：`tasks/todo.md`
> 範圍：補齊 Phase 5 與 Phase 6 之間的「設定能力」缺口
> 狀態：草案；等待審查

---

## 動機

Phase 5 完成了 Web API + SignalR + CLI 的 read/dispatch 路徑，但**寫入路徑**僅限後端：
- CLI `rule` 子命令只有 `list/show/enable/disable/delete`，沒有 `create` / `update`。
- API `POST /api/rules` / `PUT /api/rules/{id}` 已實作，但無 CLI 入口 → 必須直接打 curl 才能裝規則。
- `chat.html` overlay 從 Omni-Commander 未遷入 `src/Hosts/Vulperonex.Web/wwwroot/`；`/overlay/chat` SignalR group 已推送事件，但無頁面消費。
- 沒有可重現的「從零到看到事件流轉」cookbook，新貢獻者（含 AI agent）面對核心忠誠/簽到等功能無從下手。

Phase 6 即將擴張到 simulate→workflow→overlay 全鏈與簽到 SystemEvent，但若連「裝一條 chat-reply rule 給 simulate 觸發」都還要手動 curl，Phase 6 的 spec 審查、E2E 驗證、回歸測試成本會被 setup 雜訊吃光。

Phase 5.5 的目標：**讓 Phase 6 開始前，CLI 與既有 overlay 已能驅動單條完整的 chat→workflow→overlay 鏈，無外部工具依賴**。

---

## 範圍

### In Scope

- CLI 新增 `rule create <file.json>` 與 `rule update <id> <file.json>`，呼叫既有 `POST /api/rules` / `PUT /api/rules/{id}`。
- CLI 新增 `rule show --json-only` 行為等效的「dump」輸出（為 `rule update` 提供來回編輯範本）— 改沿用既有 `rule show` 即可，**不**新增 flag；若必要再加，列入 Phase 6。
- 從 `ref/Omni-Commander/OmniCommander.WebApi/wwwroot/chat.html` 與 `chat.css` / `chat.js` 等共用資產移植到 `src/Hosts/Vulperonex.Web/wwwroot/overlay/chat.html`，並調整以對接 Phase 5 `OverlayChatPayload` 線路鍵集（`schemaVersion`、`eventId`、`timestamp`、`displayName`、`colorHex`、`segments`、`badges`）。SignalR 用戶端使用 `@microsoft/signalr` CDN 或同捆 `wwwroot/libs/`（沿用 Omni-Commander 慣例，不引入打包流程）。
- 移植後加上 OverlayChatPayload 線路鍵集**架構測試**：`chat.html` JS 引用的所有 payload 欄位必須在 `OverlayChatPayload` 白名單內；防呆早期 catch 前後端漂移。
- 整合測試新增 fixture：以 `SimulationAdapter` 發 `user.message`，先建立一條 `SendChatMessage` workflow 規則，斷言：
  - WorkflowEngine 觸發次數
  - SendChatMessage executor 收到的展開 template
  - SignalR overlay/chat 用戶端收到 payload，包含 reply（若 reply 也走 user.message domain event）或不收到（若 reply 為純 Twitch IRC 出口）
- 新增 cookbook 文件 `docs/phases/phase-5_5-rapid-test/cookbook-chat-reply.md`，逐步覆蓋：起 Web host → CLI `rule create` 安裝範例 rule → CLI `simulate chat` → 開瀏覽器 `/overlay/chat` → 觀察事件。
- 在 `tasks/plan.md` / `tasks/todo.md` 加入 Phase 5.5 條目，明確標示與 Phase 6 的界線。

### Out of Scope（Phase 6 或後續）

- **新 workflow action（含 AddLoyaltyAction、IncrementCheckInAction）**：屬簽到/忠誠 SystemEvent 設計，SPEC 標示 post-MVP（[`docs/SPEC.md`](../../SPEC.md) §4.8 line 847）；先在 Phase 6 設計 SystemEvent + DTO + plugin 邊界再實作。
- **CLI `member loyalty add/set`**：上述 action 未定前不開後端 PUT 路徑；CLI 不領先於 spec。
- **`member-card.html` 移植**：依賴尚未定義的 `/overlay/member` 完整 DTO 與 SystemEvent；屬 Phase 6。
- **CLI rule schema JSON Schema 產出**：Phase 5.5 範例由人手寫並由後端 validator 把關；schema 自動產出延後到 OpenAPI/CLI completion 投資時再做。
- **`--json` 輸出旗標、結構化 stderr**：Phase 5 已議延後，維持延後。
- **Tab completion 修復**：已 spawn 獨立 task；不阻擋 5.5。
- **REPL 多行輸入 / heredoc**（`rule create` 從 stdin pipe JSON 可用，但不做行內互動編輯器）。

---

## 共享合約

### CLI `rule create` / `rule update` 介面

```
rule create <path-to-json>
rule update <id> <path-to-json>
```

- `<path-to-json>` 為本機檔案路徑；CLI 讀檔 → 不做 schema 校驗 → 直接以 `application/json` POST/PUT。
- 透傳後端 error code（既有 `WriteResponseAsync` 邏輯）。
- 成功 POST：印出後端回傳的 `{ id, ... }`（既有 endpoint 行為），exit 0。
- 成功 PUT：後端回 204 → CLI 印空、exit 0。

**禁止 stdin pipe**（Phase 5.5 範圍縮限）：若 `<path-to-json>` 為 `-` 視為 `UNKNOWN_COMMAND`，避免 redirected stdin 與 REPL line editor 互動。

### 範例規則 JSON

附於 `docs/phases/phase-5_5-rapid-test/examples/rule-chat-echo.json`，內容為一條 chat trigger + SendChatMessage action 的最小規則，作為 cookbook 與整合測試共用 fixture。Phase 6 起會擴充更多範例，5.5 只放 echo 一支。

### Overlay 資產目錄

- 統一存放於 `src/Hosts/Vulperonex.Web/wwwroot/overlay/`。
- `chat.html` 內**不**引用任何來自 `ref/Omni-Commander/` 的相對路徑；資產（CSS / JS / SignalR 用戶端）就近放置或經 CDN。
- SignalR client 版本與後端 SignalR server NuGet 主版本對齊（SPEC §2 後端表「即時通訊 = SignalR (10.0)」→ 用 `@microsoft/signalr@10.x`）。

### 錯誤碼

5.5 不新增錯誤碼；CLI 只透傳既有後端代碼。

---

## 依賴圖

```text
任務 17a CLI rule create / update
    -> 任務 17c E2E fixture
        -> 任務 17d cookbook 完稿
            -> 檢查點 5.5

任務 17b chat.html 移植 + 架構測試
    -> 任務 17c（共享 fixture 驗證 overlay payload）

任務 17e CLI ID 解析 + 缺 arg UX + 確認流程
    -> 任務 17d cookbook（cookbook 章節示範 --yes 與 prefix 解析）
```

任務 17a / 17b / 17e 可並行；17c 必須在 17a + 17b 通過後執行；17d 必須在 17a + 17c + 17e 全綠後撰寫。

---

## 任務切片

### 任務 17a - CLI rule create / update

- 在 `src/Hosts/Vulperonex.Cli/Commands/RuleCommand.cs` 新增 `create` 與 `update` 子命令。
- `create`：讀檔 → `client.PostAsync("/api/rules", new StringContent(json, Encoding.UTF8, "application/json"))` → `WriteResponseAsync`。
- `update`：讀檔 → `client.PutAsync($"/api/rules/{id}", ...)`. 處理 204 No Content（既有 `WriteResponseAsync` 已支援空 body）。
- 檔案不存在或非合法路徑 → `INVALID_ACTION_CONFIG`（CLI 端碼）或新增 `FILE_NOT_FOUND` — 在實作切片時定案，文件中先預留。
- Integration test：StubHandler 驗證 method、path、body 直通。
- 更新 `cli-e2e-verification.md` 表格加入 create/update 命令的 PASS 條件。

### 任務 17b - chat.html 移植

- 拷貝/重寫 `chat.html` 至 `src/Hosts/Vulperonex.Web/wwwroot/overlay/chat.html`。
- CSS / JS 同步移植到 `wwwroot/overlay/css/chat.css`、`wwwroot/overlay/js/chat.js`、`wwwroot/overlay/js/overlay-common.js`（保留 Omni-Commander 命名以利對照，但路徑指 Vulperonex `wwwroot`）。
- JS 連線 `/overlayHub`（Phase 5 已掛 SignalR hub；以實際 hub 路徑為準），訂閱 chat group 並渲染。
- 移除 Omni-Commander 專有的 `?v=1.0.1` cache-bust 串、Twitch 專有的 emote URL 假設 — 改用 `OverlayChatPayload.segments` 中 `imageUrl` 直接渲染。
- **架構測試**：新增 `tests/Vulperonex.Tests.Architecture/Overlay/ChatHtmlPayloadKeysTest.cs` 解析 `chat.js` 提取 `payload.xxx` 引用清單，斷言每個欄位都在 `OverlayChatPayload` 公開屬性集合內。實作上以 regex 抓 `\bpayload\.([a-zA-Z_]+)` 即可，避免引入 JS parser。
- 手動驗證：OBS 瀏覽器源 → `http://localhost:5001/overlay/chat`（port 取自 Phase 5 OverlayPort 分配）。

### 任務 17c - E2E fixture chat → workflow → overlay

- 在 `tests/Vulperonex.Tests.Integration/` 新增 `RapidTest/ChatReplyChainTests.cs`：
  - 啟動測試 Web host（共用 Phase 5 fixture），灌入 `examples/rule-chat-echo.json` 規則。
  - 透過 `POST /api/simulate/chat` 發 message。
  - SignalR overlay/chat client 在 5 秒內收到 payload，欄位匹配。
  - 斷言 `SendChatMessageActionExecutor` 被叫過（透過 Test Double 或記數）。
- 該測試**不**斷言 IRC 出口（沒有 Twitch live 連線）；僅驗 domain → SignalR 鏈完整。
- 將 fixture 標為 `Phase5_5_ChatReplyChain`，未來 Phase 6 SystemEvent 鏈會以類似 pattern 擴充。

### 任務 17e - CLI ID 解析 + 缺 arg UX + 破壞性操作確認

> 設計凍結於 [`cli-id-resolution-decision.md`](./cli-id-resolution-decision.md)。

- `RuleCommand` / `MemberCommand` 的 `disable` / `enable` / `delete` / `show` 子命令：
  - 缺 positional arg → stderr 印 usage + hint，exit code `MISSING_ARGS`。
  - positional 接受「完整 id | id prefix」；`rule` 群組另支援 `--name <n>` 為替代輸入；兩者互斥（同時提供 → `INVALID_ARGS`）。
  - prefix / name 多重命中 → `AMBIGUOUS_ID` + stderr 列候選表（至多 10 筆，超出加截斷提示）。
  - 零命中 → `NOT_FOUND`。
- 破壞性操作（`rule disable` / `rule delete` / `member delete`）統一走 `CliExecutionContext.ConfirmAsync(summary)`：
  - 互動 REPL（`!Console.IsInputRedirected && !Console.IsOutputRedirected`）：印「即將 X」摘要 + `[y/N]` prompt；`y` 執行，其餘 → `CANCELLED`。
  - one-shot / piped：必須帶 `--yes`，否則 `CONFIRMATION_REQUIRED` + 印摘要。
- 新 error codes：`MISSING_ARGS` / `AMBIGUOUS_ID` / `NOT_FOUND` / `CONFIRMATION_REQUIRED` / `CANCELLED`。
- i18n catalog（`en-US.json` / `zh-TW.json`）追加 missing-args usage / hint、confirm prompt / summary 文案。
- 整合測試覆蓋 decision doc 「測試門檻」全部路徑。
- 與 17a CLI `rule create` / `update` 共用 `ConfirmAsync`（兩者非破壞，但 update 可選 opt-in 確認，列為 stretch）。

### 任務 17d - Cookbook 文件

- `docs/phases/phase-5_5-rapid-test/cookbook-chat-reply.md`：
  - 章節 1：起 Web host（沿用 Phase 5 §1）。
  - 章節 2：CLI `rule create examples/rule-chat-echo.json` → 預期 stdout JSON + exit 0。
  - 章節 3：CLI `simulate chat "hello"`。
  - 章節 4：開瀏覽器 `http://localhost:<overlay_port>/overlay/chat` → 預期看到一筆 chat segment。
  - 章節 5：CLI `rule delete <id>` 清理。
- 每節含一張表格列出觀察點與通過條件。

---

## 檢查點 5.5

- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 0 警告通過。
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過，含新 fixture。
- [ ] CLI `rule create` / `rule update` 整合測試覆蓋成功與後端錯誤碼透傳兩條路徑。
- [ ] `chat.html` 架構測試通過：JS 引用的欄位 ⊆ `OverlayChatPayload` 公開屬性。
- [ ] Cookbook 由非作者跑過一輪（含 AI agent），每步驟回填觀察值，至少一次 PASS 紀錄。
- [ ] `tasks/todo.md` 與 `docs/phases/phase-5_5-rapid-test/todo.md` 同步更新。

---

## 規模

S+（單一 host 內 CLI 子命令擴充 + 一份 overlay 靜態頁 + 一條 E2E fixture + 一份 cookbook；不動 Domain / Application 介面）。

---

## 待議事項

- 範例規則 JSON 是否要納入 `tools/` 而非 `docs/phases/`：若 Phase 6 起會有 `tools/seed/` 風格的 seed 資料夾，5.5 範例可一併搬入；目前無相應結構，先放 `docs/phases/`。
- `rule update` 對於 PUT 失敗（驗證錯誤）時要不要回顯 `meta`：Phase 5 既有 `WriteResponseAsync` 只印 `error` code；若 cookbook 顯示「為何更新失敗」是必要，需擴充 stderr 格式，列入待議，**不**在 5.5 實作。
- chat.html 移植時是否同步加 dark/light theme 切換：Phase 6 OBS 用例為主，5.5 不做 theme，保留 Omni-Commander 預設外觀。

---

## 完成後解鎖

- Phase 6 簽到 SystemEvent / `/overlay/member` DTO 設計可直接套用 Phase 5.5 fixture pattern。
- 新貢獻者上手第一條路徑為「跑一遍 cookbook」，不需理解 Phase 1-5 完整脈絡。
- CLI 從「只讀工具」升格為「可建構規則的設定面板」，為 Phase 6 起的 spec-driven 開發提供寫入入口。
