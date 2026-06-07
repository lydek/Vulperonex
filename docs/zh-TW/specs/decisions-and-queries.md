# 歷史決策與疑義解答

> [← Back to Master Specification](../SPEC.md)

## 9. 成功準則 (MVP)

> **註：** 下方 `Test_Method_Names` 為**示意性的驗收準則識別碼**，非真實測試方法名。實際測試遵循 §7.4 `Given_<State>_When_<Action>_Then_<Expectation>` 命名，位於 `tests/`（例：SC-3 → `tests/Vulperonex.Tests.Architecture/Adapters/SimulationAdapterIsolationTests.cs`；SC-6 → `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`；快取 → `…/Cache/PlatformUserDisplayCacheTests.cs`）。以意圖比對，非字串比對。

- [ ] **SC-1：** 整合測試 `TwitchAdapter_PublishesAllSevenMvpEvents` 通過：對於七個 MVP `EventTypeKey` 中的每一個，模擬的 Twitch 有效負載都會在匯流排上生成相應的 `IStreamEvent`（透過 `WaitForIdleAsync` + 捕獲的事件列表驗證）。

- [ ] **SC-2：** 整合測試 `WorkflowEngine_ExecutesMatchingRule_OnEventTypeKey` 通過：當發布 `UserSentMessageEvent` 時，`EventTypeKey = "user.message"` 的 `WorkflowRule` 會觸發其 `SendChatMessageAction`；`IPlatformChatSender` 模擬物件恰好接收到一次 `SendAsync` 呼叫。

- [ ] **SC-3：** 整合測試 `SimulationAdapter_DoesNotReferenceTwitchTypes` 通過：`Vulperonex.Adapters.Simulation` 程序集對 `Vulperonex.Adapters.Twitch` 的類型引用為零（透過 NetArchTest 或反射掃描驗證）。

- [ ] **SC-4：** 架構測試 `Domain_HasNoReferenceToTwitchSymbols` 在將任何 `Twitch*` 識別子引入 `Domain` 或 `Application` 專案時，應導致建構失敗（紅燈測試）。

- [ ] **SC-5：** 整合測試 `OverlayHub_ReceivesSignalRPayload_WithinTimeout`：透過 `SimulationAdapter` 發布 `UserSentMessageEvent`，斷言 `/overlay/chat` SignalR Hub 用戶端在 **5 秒**內（CI 安全逾時）接收到 `OverlayChatPayload`。效能目標（非阻塞）：從事件到 SignalR 的延遲在本地機器上 < 500ms，作為基準單獨追蹤，而非判定通過/失敗的門檻。

- [ ] **SC-5b：** 整合測試 `WorkflowSendChatMessage_Simulation_IsObservable`：在 `Simulation` 平台執行含 `SendChatMessage` 的 workflow，斷言可觀測輸出面（記憶體接收端 / Chat Outbox / 歷史檢視）於 **5 秒**內出現 rendered message、platform、channel、dedupKey 與 status，且不相依 `/overlay/chat` 是否有 bridge。

- [ ] **SC-6：** 兩個互補的整合測試共同滿足此準則（拆分於 Task 12 + Task 13 實作）：
  - **SC-6a（WorkflowEngine half，Task 12）：** `SC6a_SimulationAndTwitch_ProduceSameWorkflowSideEffect`：使用相同有效負載分別透過 `SimulationAdapter` 和 `TwitchAdapter`（mock IRC）發布 `UserSentMessageEvent`；斷言兩者在 `WaitForIdleAsync` 後對 `IPlatformChatSender.SendAsync` 的呼叫完全相同。
  - **SC-6b（MemberRecord half，Task 13）：** `SC6b_SimulationAndTwitch_ProduceSameMemberDbState`：相同有效負載分別執行（各使用獨立 fresh SQLite fixture），斷言兩次 `WaitForIdleAsync` 後 `MemberRecord` 資料庫狀態相同。
  - 兩個測試均通過 = SC-6 達成。


- **SC-7：** 已移除出 MVP scope（原為 MockYouTube Adapter < 200 LOC 驗證；Twitch 以外平台 adapter 推遲）。

- [ ] **SC-8：** 整合測試 `MemberResolver_CreatesUlidMemberRecord_WithPlatformIdentity`：發布 `UserSentMessageEvent { Platform="twitch", UserId="test123" }` 後，斷言 `PlatformIdentity` 資料表具有行 `(Platform="twitch", PlatformUserId="test123")` 且 `MemberRecord` 的 `MemberId` 符合 ULID 格式。

- [ ] **SC-9：** 單元測試 `SendChatMessageAction_DefaultsToSourcePlatform` 和 `SendChatMessageAction_RespectsTargetPlatformOverride`：驗證 `IPlatformChatSender` 選擇邏輯。

- [ ] **SC-10：** 整合測試 `Plugin_CanPublishCustomEvent_TriggeringWorkflow`：外掛程式呼叫 `IPluginContext.Events.PublishAsync(customEvent)`；具有匹配 `EventTypeKey` 的 `WorkflowRule` 觸發；`IPlatformChatSender` 模擬物件接收到 `SendAsync`。

- [ ] **SC-11：** 手動 / 整合驗證 `ChatOverlayTemplatePreset_CanSwitchWithoutCodeEdit`：`/overlay/chat` 可在不修改前端原始程式碼的前提下切換至少兩個樣板，至少包含 Vulperonex 內建預設與另一個可安裝 preset；切換後 payload contract 不變，且渲染仍遵守 DTO 白名單與 text binding。

- [ ] **SC-11b：** 擴充功能驗證 `OneCommeCompatibility_ExtensionContract_Works`：OneComme 相容能力以外掛 / 匯入器 / adapter 形式接入，不要求 core 直接綁定其執行期；驗證可辨識 OneComme 樣板目錄結構或對應 package metadata，並對應到 Vulperonex chat overlay preset contract。

---

## 10. 已解決的設計決策

| # | 決策 |
|---|---|
| D1 | 外掛程式載入：**啟動時靜態引用** (AssemblyLoadContext / 熱載入推遲)。 |
| D2 | 工作流回覆路由：**預設為源平台**，允許每個操作覆寫 `TargetPlatform`。 |
| D3 | 事件持久化：**不儲存**。僅記錄日誌 (LOG)，具有可配置的保留/清理策略。 |
| D4 | 外掛程式範圍：**外掛程式可以同時發布和訂閱**事件（充當完整的配接器）。 |
| D5 | 前端發行：**Web 主機提供 `wwwroot` 服務**，Desktop = Web 主機 + Photino 窗口。 |
| D6 | CLI 範圍 (MVP)：**模擬 + 配置 + 規則 + 會員指令**。 |
| D6a | CLI 識別碼解析 (Phase 5.5)：`rule` positional 接受**完整 id / id prefix / `--name`**；`member` 接受**完整 id / id prefix**；多重命中 → `AMBIGUOUS_ID` + 候選表；破壞性操作（`rule disable` / `rule delete` / `member delete`）互動 REPL 走 `[y/N]` prompt、非互動需 `--yes` 否則 `CONFIRMATION_REQUIRED`。設計凍結於 `docs/phases/phase-5_5-rapid-test/cli-id-resolution-decision.md`。 |
| D7 | 會員身分：`MemberId` 為 ULID；`PlatformIdentity (Platform, PlatformUserId)` 複合鍵。 |
| D8 | 儲存庫層的輕量級 CQRS：`IMemberRepository` (命令) 與 `IMemberQueryService` (查詢) 分離。 |

---

## 11. 超出範圍 (Phase 1)

- Twitch 以外的平台配接器（架構設計已預留，實作推遲）。
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

**決策：規範化標頭 + Trigger / Conditions / Actions / OnFailure 步驟 / Throttle 的 JSON 欄位。**

```sql
-- Phase 8 整併後的 schema（ConsolidateWorkflowRuleSchema migration）
CREATE TABLE WorkflowRules (
    Id                   TEXT PRIMARY KEY,
    Name                 TEXT NOT NULL,
    EventTypeKey         TEXT,                          -- Phase 8 起可為 NULL；sub-workflow rule 為 NULL
    TriggerJson          TEXT,                          -- 序列化的 WorkflowTrigger（typed Filter dict）；sub-workflow 為 NULL
    MatchCondition       TEXT,                          -- 選用的 rule 層級 NCalc 閘門（Phase 8 自 TriggerJson 提升）
    IsSubWorkflow        INTEGER NOT NULL DEFAULT 0,
    ConditionsJson       TEXT NOT NULL DEFAULT '{}',
    ActionsJson          TEXT NOT NULL DEFAULT '[]',
    OnFailureActionsJson TEXT NOT NULL DEFAULT '[]',
    IsEnabled            INTEGER NOT NULL DEFAULT 1,
    Priority             INTEGER NOT NULL DEFAULT 0,
    CreatedAt            TEXT NOT NULL,                 -- ISO-8601 DateTimeOffset（EF 預設對應）
    ExecutionMode        TEXT NOT NULL DEFAULT 'Serial',   -- Phase 8 前名為 ConcurrencyMode
    MaxParallelism       INTEGER NOT NULL DEFAULT 1,
    ThrottleJson         TEXT NOT NULL DEFAULT '{}',
    TimeoutSeconds       INTEGER NOT NULL DEFAULT 30,
    Version              INTEGER NOT NULL DEFAULT 0     -- 樂觀並行 token
);
CREATE INDEX IX_WorkflowRules_CreatedAt ON WorkflowRules (CreatedAt);
```

規則標頭已規範化（可查詢、可索引）。Trigger 過濾、條件、操作、失敗步驟與 throttle policy 作為 JSON（架構流動 — 新外掛程式類型不需要遷移）。使用 EF Core 10 JSON 對應進行類型安全的反序列化。**Phase 8 移除了獨立的 `UpdatedAt` / `PlatformFilter` / `ConcurrencyMode` 欄位，以及先前位於 `TriggerJson` 內的巢狀 `eventTypeKey` / `matchCondition` 欄位；列表索引由 `EventTypeKey` 改為 `CreatedAt`（預設排序鍵）。**

---

### OQ4 — i18n 覆蓋範圍 ✅

**決策：後端返回錯誤程式碼，UI 負責翻譯。後端日誌始終使用英文。**

```json
// API 錯誤回應 — 無人類可讀字串
{ "error": "WORKFLOW_RULE_NOT_FOUND", "meta": { "ruleId": "01HK..." } }
```

Vue UI 透過 vue-i18n 將錯誤程式碼對應到在地化字串。後端不具備地區感知能力。日誌始終為英文（機器可讀，跨部署一致）。

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
| `INVALID_FILTER_KEY` | 400 | `POST/PUT /api/rules` — Trigger filter 含不在該事件型別 typed filter metadata 內的鍵（§4.26） |
| `SUB_WORKFLOW_MUST_NOT_HAVE_TRIGGER` | 400 | `POST/PUT /api/rules` — `isSubWorkflow=true` 卻提供了 `eventTypeKey` / `trigger` |
| `WORKFLOW_RULE_CONFLICT` | 409 | `PUT /api/rules/{id}` — 樂觀並行 `Version` 不符 |
| `MISSING_OR_INVALID_CSRF_HEADER` | 400 | 任何受保護請求（loopback mutation / `/api/overlay/*`）缺少或帶錯誤 `X-Admin-Csrf`（§4.17） |
| `MISSING_ORIGIN_OR_REFERER_HEADER` | 400 | 受保護請求 `Origin`、`Referer` 皆缺 |
| `ORIGIN_MISMATCH` | 400 | `Origin`/Host 不在 loopback 白名單（防 DNS rebinding） |
| `INVALID_ORIGIN_HEADER` | 400 | `Origin` 非合法絕對 URI |
| `REFERER_MISMATCH` | 400 | `Referer` host 不在 loopback 白名單 |
| `INVALID_REFERER_HEADER` | 400 | `Referer` 非合法絕對 URI |

CLI 端專用代碼 `CLI_API_URL_NOT_LOOPBACK`：當 `VULPERONEX_API_URL` 非 loopback URL 時於用戶端發出。

---

### OQ5 — Web 主機的身分驗證模型 ✅（已修訂 — 見 §4.17）

已在第 4.17 節 (G15) 中解決。原「兩埠皆 loopback-only、無認證」模型於 Phase 6+ 強化後**已被取代**：
- **API 埠**：僅 loopback（IPv4 127.0.0.1 + IPv6 ::1）。**Overlay 埠**：預設僅 loopback，可選擇繫結 LAN（`Overlay:Lan:Enabled`）供跨機 OBS。
- **並非無認證。** `AdminGuardMiddleware` 對 loopback mutation 與全部 `/api/overlay/*` 要求：Host 白名單 + 每行程 `X-Admin-Csrf` token + 相符的 `Origin`/`Referer`。LAN 請求僅限疊層介面且需疊層存取金鑰（`?k=` / `X-Overlay-Key`）。
- 本機 OBS 用 `http://localhost:5001/overlay/chat.html` / `…/member-card.html`；遠端 OBS 用 `http://<lan-host>:5001/overlay/chat.html?k=<overlay-key>`（金鑰與 URL 由 `GET /api/overlay/lan-info` 取得）。`/overlay/chat`、`/overlay/member` 僅為相容 redirect alias。

---

### OQ6 — Photino 離線場景 ✅

三種失敗場景及其處理：

**埠衝突（API 或疊層埠被占用）：**
```
埠始終成對分配 (ApiPort, OverlayPort)。
預設對：(5000, 5001)。

啟動時，如果對中的任一埠不可用：
  嘗試下一對：(5002, 5003) → (5004, 5005) → (5006, 5007) → (5008, 5009)
  成對嘗試 — 防止 API 自動重新導向到疊層的預設埠。
  所有嘗試均失敗 → Photino 對話方塊：
    "埠 5000–5009 不可用。請在設定中配置不同的埠對。"

可配置：appsettings.json →
  "Web": { "ApiPort": 5000, "OverlayPort": 5001 }
  (手動配置略過自動遞增；使用者需自行解決衝突。)
```

**Web 主機在工作階段中崩潰：**
```
Photino 失去連線 → 顯示嵌入式靜態回退 HTML
（打包在 Photino 二進位檔案中，無 Web 主機相依）
回退頁面顯示：錯誤描述 + [重新啟動] 按鈕
```

**啟動時資料庫遷移失敗：**
```
遷移在 Web 主機啟動前執行
失敗 → 中止啟動 → Photino 對話方塊：
  "資料庫更新失敗：{error}"
  按鈕：[開啟日誌資料夾] [退出]
不進行自動修復以防止資料損壞。
```

---

## 下一步

1. **準備好進行第一階段實作。** SPEC.md 和 plan.md 已完成多輪審查；所有 P1 問題已解決。從任務 1 開始。
2. plan.md 包含完整的任務列表、驗收準則、相依圖和文件產出。
3. todo.md 是執行清單 — 在那裡追蹤進度。
4. 根據 BDD 情境和 TDD 紅/綠/重構，逐任務實作。
