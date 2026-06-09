# 第 5 階段計畫 - Web Host + SignalR + CLI

> 父計畫：`tasks/plan.md`
> 父核對清單：`tasks/todo.md`
> 範圍：任務 14a、14b、15、16

---

## 規劃規則

- 保持此階段以 API 為優先且可整合測試。CLI 與未來的 UI 必須共享相同的 Minimal API 寫入路徑。
- 保留輕量級 CQRS：GET 端點使用查詢/讀取服務；寫入端點使用命令/寫入儲存庫或應用程式服務。
- 後端錯誤回應僅暴露機器可讀的錯誤碼。人類可讀的字串仍屬於 UI/i18n 的範疇。
- Web host 僅限 loopback 且 MVP 階段不做身分驗證。在此階段不要引入 API 金鑰或外部繫結位址。
- 依照場景使用 BDD/TDD。每個實作切片應伴隨重點測試與任務範圍內的 commit。
- 未經事先核准，不得新增任何套件相依項目。
- 確保無關的本地文件不進入 commit，特別是未追蹤的設計草案。

---

## 共享合約

### JSON 與端點慣例

- 所有 Web API 與 SignalR 的 JSON 序列化皆使用 `System.Text.Json` 並配置 `JsonSerializerDefaults.Web`，因此 REST 端點、SignalR 負載與第 4 階段 Overlay DTO 合約皆使用相同的 camelCase 命名原則。
- Minimal API 端點註冊使用 `IEndpointRouteBuilder` 擴充方法，每個功能區域一個擴充。不要在第 5 階段發明自訂的端點探索框架。
- 任務 14a-0 僅在 loopback 上暴露 `AddOpenApi()` 與 `/openapi/v1.json`。CLI 在 MVP 中不需要生成的用戶端，但必須存在 OpenAPI 產出物以供第 6 階段前端/API 對齊使用。

### 第 5 階段錯誤碼

所有第 5 階段 API 與 CLI 路徑皆使用 `src/Hosts/Vulperonex.Web/Errors/ErrorCodes.cs` 中的中央常數（或實作證明常數應屬於 Application 時的等效項）。錯誤封裝使用 `{ "error": "ERROR_CODE", "meta": {...} }`。
命名慣例：`UNKNOWN_*` 表示金鑰或識別碼不在允許清單/註冊表中；`INVALID_*` 表示提交的值已知但格式、範圍、架構或路徑/主體一致性驗證失敗。

| 程式碼 | HTTP 狀態 | 首個任務 | 備註 |
|------|-------------|------------|-------|
| `WORKFLOW_RULE_NOT_FOUND` | 404 | 14a-3 | 規則顯示/刪除/啟用/停用時缺少 ID |
| `UNKNOWN_EVENT_TYPE_KEY` | 400 | 14a-2 | 包含被拒絕作為工作流觸發器的系統事件 |
| `CIRCULAR_WORKFLOW_REFERENCE` | 400 | 14a-2 | 靜態儲存時分析 |
| `UNKNOWN_ACTION_TYPE` | 400 | 14a-2 | Action 架構驗證 |
| `UNKNOWN_CONDITION_TYPE` | 400 | 14a-2 | Condition 架構驗證 |
| `ACTION_MISSING_REQUIRED_PARAM` | 400 | 14a-2 | 例如：缺少 `Template` |
| `INVALID_ACTION_CONFIG` | 400 | 14a-2 | 範圍與列舉驗證 |
| `INVALID_REGEX_PATTERN` | 400 | 14a-2 | 無效或過長的 Regex |
| `INVALID_RULE_ID_MISMATCH` | 400 | 14a-3 | PUT 路徑/主體 ID 不匹配 |
| `UNKNOWN_SIMULATE_EVENT_TYPE` | 400 | 14b-1 | 未知的公開模擬別名 |
| `CONFIG_KEY_SECURITY_NAMESPACE` | 403 | 14b-2 | `security.*` GET/PUT |
| `OAUTH_CREDENTIAL_NAMESPACE` | 403 | 14b-2 | `oauth.*` GET/PUT |
| `UNKNOWN_CONFIG_KEY` | 400 | 14b-2 | 未知的非保護配置金鑰 |
| `INVALID_QUERY_PARAM` | 400 | 14b-3 | 會員列表分頁/過濾驗證 |
| `MEMBER_NOT_FOUND` | 404 | 14b-3 | 會員顯示時缺少 ID |

### 模擬別名單一事實來源

- 公開模擬別名存在於一個可注入的 `SimulationAliasRegistry` 或等效的 Singleton 中：`chat -> user.message`, `follow -> user.followed`, `sub -> user.subscribed`。
- `GET /api/event-types`, `POST /api/simulate/{alias}` 以及 CLI 模擬命令皆消耗此共享註冊表。不要在端點處理常式中分別硬編碼別名對照表。

### 手動驗證記錄

- 第 5 階段手動檢查記錄於 `docs/phases/phase-5-web-signalr-cli/manual-verification.md`。
- 每個條目包含日期、驗證者、命令/瀏覽器/OBS 設置、預期行為、觀察到的行為以及通過/失敗結果。

### 第 5 階段待議事項

- SignalR overlay 重新連線/重播行為：由任務 19/第 6 階段前端負責。第 5 階段僅證明即時推送；除非 Task 15b 測試無法穩定驗證即時交付，否則遺漏事件重播延至第 6 階段以後。
- CLI JSON 輸出模式：由任務 16 實作審查負責。第 5 階段可針對成功的 API 格式資料回傳 JSON；除非 CLI 命令需要同時支援人類可讀與機器可讀兩種輸出，否則專用 `--json` 合約延至第 6 階段以後。
- Web host 關閉訊號：由任務 21 Photino 外殼負責。使用者可見的 overlay UX 關閉語意屬於第 6 階段以後。
- 第 4 階段 `TwitchAdapter` 延遲 `??=` 競爭狀況：若第 5 階段涉及實際 OAuth 流程建構，則由任務 14a-0/15a 整合審查負責。
- 非 loopback 繫結的管理/overlay hub 驗證：未來 LAN/遠端 OBS 任務。若未來階段允許 LAN 繫結或連接埠轉寄，則在發布該變更前必須強制執行 hub 驗證。

### 第 5 階段實作前相依項目

- 任務 13f 後續：強化第 4 階段 SC-6a/SC-6b 等效性，包含 follow/sub/donate 負載以及對快取狀態、會員狀態、`TotalBitsGiven` 與訂閱者層級的斷言。第 5 階段檢查點取決於此後續工作完成或明確豁免；這不屬於第 5 階段的實作切片。

---

## 相依圖

```text
任務 14a-0 Web host 組合與 API 測試工具
    -> 任務 14a-1 WorkflowRule 持久化/查詢對齊
    -> 任務 14a-2 WorkflowRule 驗證與錯誤碼
    -> 任務 14a-3 WorkflowRule CRUD 端點與 CQRS 測試
    -> 任務 14a-4 EventTypes 端點

任務 14b-1 模擬端點
    相依 任務 14a-0, 任務 9
任務 14b-2 配置端點
    相依 任務 14a-0, 任務 8
任務 14b-3 會員查詢端點
    相依 任務 14a-0, 任務 7

任務 15a SignalR hub 合約與 host 註冊
    相依 任務 14a-0, 任務 13
任務 15b Overlay 事件轉寄與 SC-5
    相依 任務 15a
任務 15c 雙連接埠 loopback Kestrel 與連接埠分配
    相依 任務 15a

任務 16a CLI HTTP 基礎與錯誤透傳
    相依 任務 14b, 任務 15c
任務 16b 規則命令
    相依 任務 16a, 任務 14a-3
任務 16c 配置與會員命令
    相依 任務 16a, 任務 14b-2, 任務 14b-3
任務 16d 模擬命令與手動 overlay 路徑
    相依 任務 16a, 任務 14b-1, 任務 15b
任務 16e 第 5 階段檢查點審查
    相依 所有第 5 階段切片
```

---

## 任務 14a-0 - Web Host 組合與 API 測試工具

**說明：** 將 `Vulperonex.Web` 從空 host 轉換為可測試的 Minimal API 組合根。註冊端點模組、共享錯誤封裝小幫手、JSON 選項、工作流服務與整合測試掛鉤，尚不實作完整的端點表面。

**驗收標準：**
- [ ] `Program.cs` 暴露一個適合整合測試的組合式應用程式建構器。
- [ ] 端點註冊依功能拆分，使用 `IEndpointRouteBuilder` 擴充方法（`MapWorkflowRuleEndpoints`, `MapEventTypeEndpoints` 等）而非保持內嵌。
- [ ] `System.Text.Json` 配置為 `JsonSerializerDefaults.Web`。
- [ ] 註冊 `AddOpenApi()` 並在 loopback API 表面暴露 `/openapi/v1.json`。
- [ ] 端點與測試可使用中央第 5 階段錯誤碼常數與狀態對應表。
- [ ] 錯誤封裝使用穩定的 `error` 程式碼，且不包含後端人類可讀的說明。
- [ ] 整合測試可以使用記憶體內基礎架構/虛擬物件啟動 Web host。
- [ ] 在此切片中，沒有生產端點繫結到非 loopback 地址。

**驗證：**
- [ ] Web 整合煙霧測試可以透過實際 host 管線呼叫健康端點或 smoke test 端點。
- [ ] 架構檢查確認 Web 引用 Application/Infrastructure，但 Domain/Application 不引用 Web。
- [ ] 架構測試證明直接讀取 `Configuration["Database:Path"]` 僅允許在 `IDatabasePathResolver` 或其實作中；端點與其他啟動服務必須使用解析器而非讀取原始金鑰。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Web/Program.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/`
- `src/Hosts/Vulperonex.Web/Errors/`
- `src/Hosts/Vulperonex.Web/Configuration/IDatabasePathResolver.cs`
- `tests/Vulperonex.Tests.Integration/Web/`

**規模：** S

---

## 任務 14a-1 - WorkflowRule 持久化與查詢對齊

**說明：** 在端點實作前，對齊 `WorkflowRuleEntity`、儲存庫、查詢服務與 DTO 形狀以符合 REST API 合約。保持讀取 DTO 與寫入實體分離。

**驗收標準：**
- [ ] 工作流規則持久化 API/UI 所需的欄位：ID、名稱、事件類型金鑰、啟用標籤、優先順序、條件、動作、`CreatedAt`，以及現有工作流執行所需的中繼資料。
- [ ] 規則列表排序穩定：`Priority ASC, CreatedAt ASC, Id ASC`（優先順序升冪、建立時間升冪、Id 升冪）。
- [ ] 查詢路徑透過 `IWorkflowRuleQueryService` 返回 DTO。
- [ ] 寫入路徑保持在 `IWorkflowRuleRepository` 或應用程式命令服務之後。
- [ ] 現有的 WorkflowEngine 測試持續通過。

**驗證：**
- [ ] 儲存庫/查詢服務測試涵蓋建立、更新、刪除、列表與顯示行為。
- [ ] CQRS 互動虛擬物件證明 GET 路徑不呼叫寫入儲存庫方法。

**可能涉及的檔案：**
- `src/Vulperonex.Application/Workflow/`
- `src/Vulperonex.Infrastructure/Workflow/`
- `tests/Vulperonex.Tests.Integration/Workflow/`

**規模：** M

---

## 任務 14a-2 - WorkflowRule 驗證與錯誤碼

**說明：** 針對事件金鑰、系統事件、循環子工作流引用、Action 架構、Condition 架構、Regex 模式、設定範圍、冷卻範圍、並行限制、範本長度，以及路徑/主體 ID 不符實作儲存時驗證。

**驗收標準：**
- [ ] 驗證失敗使用中央第 5 階段錯誤碼常數與 HTTP 狀態對應。
- [ ] 未知事件金鑰返回 `UNKNOWN_EVENT_TYPE_KEY`。
- [ ] `platform.connection_changed` 已知但在作為工作流觸發器時無效。
- [ ] 循環子工作流引用返回 `CIRCULAR_WORKFLOW_REFERENCE`。
- [ ] 未知 Action/Condition 類型返回 `UNKNOWN_ACTION_TYPE` / `UNKNOWN_CONDITION_TYPE`。
- [ ] 缺少必要的 Action 參數返回 `ACTION_MISSING_REQUIRED_PARAM`。
- [ ] 無效的範圍/配置返回 `INVALID_ACTION_CONFIG`。
- [ ] 無效的 Regex 返回 `INVALID_REGEX_PATTERN`。
- [ ] PUT 路徑/主體 ID 不匹配返回 `INVALID_RULE_ID_MISMATCH`；端點在呼叫更深層的工作流規則驗證器之前會短路處理此問題。

**驗證：**
- [ ] 單元測試以 Given/When/Then 命名涵蓋每個錯誤碼。
- [ ] 驗證測試僅斷言錯誤碼，不驗證在地化副本。
- [ ] 範本渲染持續涵蓋：未知的佔位符（如 `{event.unknown}`）保留原樣，null/空佔位符值渲染為空字串。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Web/Validation/WorkflowRuleValidator.cs`
- `src/Hosts/Vulperonex.Web/Errors/`
- `tests/Vulperonex.Tests.Unit/Web/WorkflowRuleValidatorTests.cs`

**規模：** M

---

## 任務 14a-3 - WorkflowRule CRUD 端點

**說明：** 在經過驗證的應用程式寫入/查詢路徑上實作 WorkflowRule REST CRUD。

**驗收標準：**
- [ ] `GET /api/rules` 透過查詢服務列出規則。
- [ ] `GET /api/rules/{id}` 返回單一規則或 `WORKFLOW_RULE_NOT_FOUND`。
- [ ] `POST /api/rules` 建立規則並返回 `201 Created` 且帶有 `Location: /api/rules/{newId}`。
- [ ] `PUT /api/rules/{id}` 更新規則並返回更新後的規則。
- [ ] `PUT /api/rules/{id}` 且主體 ID 不等於路徑 ID 時，從端點層返回 `INVALID_RULE_ID_MISMATCH`，不呼叫驗證器或儲存庫。
- [ ] `DELETE /api/rules/{id}` 刪除規則並返回 `204`；缺少規則返回 `WORKFLOW_RULE_NOT_FOUND`。
- [ ] 啟用/停用端點僅更新啟用狀態並返回 `204`；缺少規則返回 `WORKFLOW_RULE_NOT_FOUND`。
- [ ] GET 路徑互動測試證明寫入儲存庫未被呼叫。

**驗證：**
- [ ] 記憶體內 SQLite 整合測試涵蓋 CRUD、未找到、路徑/主體 ID 不匹配、驗證失敗與回應狀態碼。
- [ ] SC-2 與 SC-9 現有的工作流行為保持通過（綠色）。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Web/Endpoints/WorkflowRuleEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/WorkflowRuleEndpointTests.cs`

**規模：** M

---

## 任務 14a-4 - EventTypes 端點

**說明：** 為 UI 下拉選單與 CLI 探索實作 `GET /api/event-types`。

**驗收標準：**
- [ ] 端點返回註冊的工作流可見事件金鑰。
- [ ] `platform.connection_changed` 從工作流可見結果中排除。
- [ ] `isSimulatable` 源自共享的 `SimulationAliasRegistry`，且僅對公開別名（`chat`, `follow`, `sub`）為 true。
- [ ] 端點不要求 Twitch OAuth/socket 啟動。

**驗證：**
- [ ] 整合測試使用虛擬註冊表並斷言精確的金鑰以及 `isSimulatable`。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Web/Endpoints/EventTypeEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/EventTypeEndpointTests.cs`

**規模：** S

---

## 任務 14b-1 - 模擬端點

**說明：** 實作 `POST /api/simulate/{alias}` 作為 CLI/手動測試的 REST 介面。

**驗收標準：**
- [ ] 僅接受 `chat`, `follow`, 與 `sub` 別名。
- [ ] 原始規範金鑰（如 `user.message`）在此端點會被拒絕。
- [ ] Request body schema 固定為：`chat` 接受 `{ platformUserId?, displayName?, roles?, message? }`；`follow` 接受 `{ platformUserId?, displayName?, roles? }`；`sub` 接受 `{ platformUserId?, displayName?, roles?, tier? }`。
- [ ] 未知別名返回 `UNKNOWN_SIMULATE_EVENT_TYPE`。
- [ ] 別名驗證使用共享的 `SimulationAliasRegistry`。
- [ ] 端點呼叫 `ISimulationAdapter` 並透過正常匯流排路徑發布。

**驗證：**
- [ ] 整合測試涵蓋接受的別名、未知別名、規範金鑰拒絕以及聊天室傳送者的模擬副作用。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Web/Endpoints/SimulateEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/SimulateEndpointTests.cs`

**規模：** S

---

## 任務 14b-2 - 配置端點

**說明：** 在註冊表查找前實作 `GET|PUT /api/config/{key}` 並進行受保護命名空間檢查。

**驗收標準：**
- [ ] 對於 GET 與 PUT，`security.*` 返回 `403` + `CONFIG_KEY_SECURITY_NAMESPACE`。
- [ ] 對於 GET 與 PUT，`oauth.*` 返回 `403` + `OAUTH_CREDENTIAL_NAMESPACE`。
- [ ] 未知的非保護金鑰返回 `400` + `UNKNOWN_CONFIG_KEY`。
- [ ] 未知的受保護金鑰仍返回受保護命名空間錯誤，而非未知金鑰錯誤。
- [ ] 雖然目前的 OAuth 重新整理權杖由 `IOAuthTokenStore` 擁有且不存於配置註冊表中，但在註冊表查找前仍會檢查黑名單。這防止了未來的 `oauth.*` 註冊表條目意外地變得可透過 `/api/config` 讀取或寫入。
- [ ] 允許的金鑰使用 `ISystemSettingsService`。

**驗證：**
- [ ] 整合測試涵蓋已知受保護金鑰、未知受保護金鑰、未知非保護金鑰、GET、PUT 與成功路徑。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Web/Endpoints/ConfigEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/ConfigEndpointTests.cs`

**規模：** S

---

## 任務 14b-3 - 會員查詢端點

**說明：** 透過 `IMemberQueryService` 實作唯讀會員列表/顯示 API。

**驗收標準：**
- [ ] `GET /api/members` 支援 `limit`（預設 50，最大 200）與 `offset`。
- [ ] 會員列表回應包含用於分頁 UI 的 `total`。
- [ ] 會員列表排序穩定。Phase 5 若尚未提供 `LastSeen` 欄位，預設為 `MemberId ASC`；未來加入 `LastSeen` 時改為 `LastSeen DESC, MemberId ASC`。
- [ ] 無效的查詢參數返回 `INVALID_QUERY_PARAM`。
- [ ] `GET /api/members/{id}` 返回單一會員或 `MEMBER_NOT_FOUND`。
- [ ] 端點不呼叫會員寫入儲存庫。

**驗證：**
- [ ] 整合測試植入資料庫資料並涵蓋列表、分頁、無效分頁、顯示與缺少會員的情況。

**可能涉及的檔案：**
- `src/Vulperonex.Application/Members/IMemberQueryService.cs`
- `src/Vulperonex.Infrastructure/Members/MemberQueryService.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/MemberEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/MemberEndpointTests.cs`

**規模：** M

---

## 任務 15a - SignalR Hub 合約與 Host 註冊

**說明：** 新增管理與 overlay SignalR hub，在 host 中註冊 SignalR，並在轉寄事件前保持 hub 合約明確。

**驗收標準：**
- [ ] 為管理用戶端提供 `/hubs/events`。
- [ ] 規範的 overlay hub 路徑為 `/hubs/overlay/chat`, `/hubs/overlay/alerts`, 與 `/hubs/overlay/member`，以符合 SPEC overlay URL 命名。
- [ ] 管理 hub 可以接收所有 `IStreamEvent` 類別，包含 `PlatformConnectionChangedEvent`。
- [ ] Overlay hub DTO 保持為第 4 階段公開負載 DTO，而非領域實體。
- [ ] 第 5 階段假設僅限 loopback/無驗證；非 loopback 情境引用前述 open question，不在本階段開放。
- [ ] 管理 hub 不暴露 client-to-server invokable method；第 5 階段只允許 server push 至 client。

**驗證：**
- [ ] Hub 連線測試證明每個路徑皆接受 SignalR 連線。
- [ ] Hub 合約測試斷言 Domain/Application Twitch 符號不會洩漏到 hub 負載中。
- [ ] 確認 `event-id-decision.md` 審查筆記欄位（審查者/日期/決定）填寫完成，方可進入 Task 15b。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Web/Hubs/EventHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayChatHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayAlertsHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayMemberHub.cs`
- `tests/Vulperonex.Tests.Integration/Web/SignalRHubTests.cs`

**規模：** M

---

## 任務 15b - Overlay 事件轉寄與 SC-5

**說明：** 訂閱串流事件並透過適當的 overlay hub 轉寄 overlay 安全的 DTO。

**驗收標準：**
- [ ] 聊天事件在 5 秒內到達 `/hubs/overlay/chat` 以符合 SC-5。
- [ ] 效能預算與 SC 通過/失敗逾時分開追蹤：發布至 hub 傳送目標 < 500ms，hub 至用戶端目標 < 500ms，完整本地路徑 P95 目標 < 1s。SC-5 仍使用 5s 作為 CI 安全上限。
- [ ] Alert event 集合明確限定為 `user.followed`, `user.subscribed`, `user.donated`, `user.gifted_subscription`, `channel.raided`；第 5 階段至少實作 follow/sub 的 overlay alert 轉寄，其他事件可保留為後續 adapter 覆蓋。
- [ ] 會員 hub 作為 MVP 骨架連線，但不發明不受支援的事件。
- [ ] SignalR JSON 金鑰集測試精確匹配 overlay DTO 公開合約。
- [ ] 合成 `eventId` 語意記錄於 `docs/phases/phase-5-web-signalr-cli/event-id-decision.md`：平台提供的 ID 跨用戶端識別相同事件；後備 ULID 僅保證本地單一實例交付 ID。

**驗證：**
- [ ] SC-5 整合測試透過 `SimulationAdapter` 發布聊天事件，走真實 event bus path，並在 5 秒內觀察到 overlay hub 負載。
- [ ] 測試輸出記錄從發布到 hub 傳送、以及 hub 到用戶端的耗時，即使 5s SC 逾時仍通過，超過 1s 本地目標的效能退化仍可見。
- [ ] 精確的 JSON 金鑰集測試透過反序列化 SignalR 線路負載（而非透過反射）涵蓋聊天、提醒與會員負載。這是對第 4 階段 DTO `System.Text.Json` 金鑰集測試的深度防禦。
- [ ] 在實作 overlay 轉寄前審查事件 ID 決定文件。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Web/Overlay/`
- `tests/Vulperonex.Tests.Integration/Web/OverlaySignalRTests.cs`

**規模：** M

---

## 任務 15c - 雙連接埠 Loopback Kestrel 與連接埠分配

**說明：** 實作 API/overlay 連接埠對分配與僅限 loopback 的 Kestrel 繫結。

**驗收標準：**
- [ ] 連接埠對嘗試從 `5000/5001` 開始，接著是 `5002/5003`，一直到 `5008/5009`。
- [ ] 當所有連接埠對耗盡時，`PortPairAllocator.TryAllocate()` 返回 `null`。
- [ ] Host 啟動時將 null 分配轉換為帶有清晰訊息的 `PortExhaustedException`。
- [ ] 兩個連接埠皆繫結到 `IPAddress.Loopback` 與 `IPAddress.IPv6Loopback`。
- [ ] 測試拒絕非 loopback 通訊端（socket）嘗試。
- [ ] `PortPairAllocator` 與 OAuth 回呼連接埠選擇器共享 `IPortAvailabilityProbe.IsAvailable(int port)` 以避免發散的通訊端可用性行為。

**驗證：**
- [ ] 單元測試涵蓋第一個可用對、部分佔用、全部耗盡，以及配置器中不丟出例外。
- [ ] 整合/通訊端測試驗證 IPv4/IPv6 loopback 繫結與非 loopback 拒絕。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Web/Infrastructure/PortPairAllocator.cs`
- `src/Hosts/Vulperonex.Web/Infrastructure/IPortAvailabilityProbe.cs`
- `src/Hosts/Vulperonex.Web/Infrastructure/PortExhaustedException.cs`
- `src/Hosts/Vulperonex.Web/Program.cs`
- `tests/Vulperonex.Tests.Unit/Web/PortPairAllocatorTests.cs`

**規模：** M

---

## 任務 16a - CLI HTTP 基礎與錯誤透傳

**說明：** 圍繞對 loopback API 的 HTTP 呼叫建構 CLI 命令基礎。CLI 不得直接存取 SQLite 或應用程式儲存庫。

**驗收標準：**
- [ ] CLI API 基本 URL 解析優先序為 `VULPERONEX_API_URL` 環境變數，否則使用預設 loopback URL `http://localhost:5000`。
- [ ] 2xx 回應僅將成功輸出寫入 stdout。
- [ ] 4xx/5xx 回應將回應的 `error` 程式碼寫入 stderr 且退出碼為 `1`；網路/連接失敗在 MVP 中退出碼也為 `1`。
- [ ] CLI 保留後端程式碼，例如 `WORKFLOW_RULE_NOT_FOUND`, `MEMBER_NOT_FOUND` 以及受保護命名空間錯誤。
- [ ] CLI 沒有直接的資料庫存取路徑。

**驗證：**
- [ ] CLI 測試使用虛擬 HTTP 伺服器/處理常式並斷言 stdout、stderr 以及退出碼行為。
- [ ] 架構測試在不允許的情況下拒絕 CLI 引用 Infrastructure 持久化。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Cli/Program.cs`
- `src/Hosts/Vulperonex.Cli/Http/`
- `tests/Vulperonex.Tests.Integration/Cli/`

**規模：** M

---

## 任務 16b - CLI 規則命令

**說明：** 在 WorkflowRule API 上實作 `rule list|show|enable|disable|delete`。

**驗收標準：**
- [ ] `rule list` 呼叫 `GET /api/rules`。
- [ ] `rule show` 透傳 `WORKFLOW_RULE_NOT_FOUND`。
- [ ] `rule enable` 與 `rule disable` 呼叫 API 狀態端點。
- [ ] `rule delete` 呼叫 DELETE 並將 `204` 視為成功。

**驗證：**
- [ ] CLI 整合測試涵蓋每個命令與錯誤透傳。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Cli/Commands/RuleCommands.cs`
- `tests/Vulperonex.Tests.Integration/Cli/RuleCommandTests.cs`

**規模：** S

---

## 任務 16c - CLI 配置與會員命令

**說明：** 在 REST API 上實作 `config get|set` 與 `member list|show`。

**驗收標準：**
- [ ] 受保護的配置命名空間錯誤透傳至 stderr 並以非零狀態退出。
- [ ] 未知的配置金鑰透傳為 `UNKNOWN_CONFIG_KEY`。
- [ ] `member list` 支援 limit/offset 參數。
- [ ] `member show` 透傳 `MEMBER_NOT_FOUND`。

**驗證：**
- [ ] CLI 測試涵蓋配置與會員命令的成功與後端錯誤透傳。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Cli/Commands/ConfigCommands.cs`
- `src/Hosts/Vulperonex.Cli/Commands/MemberCommands.cs`
- `tests/Vulperonex.Tests.Integration/Cli/ConfigCommandTests.cs`
- `tests/Vulperonex.Tests.Integration/Cli/MemberCommandTests.cs`

**規模：** S

---

## 任務 16d - CLI 模擬命令與資料庫路徑規則

**說明：** 實作 `simulate chat|follow|sub` 並驗證 web host 與 CLI 配置的共享資料庫路徑規則。

**驗收標準：**
- [ ] `simulate chat`, `simulate follow`, 與 `simulate sub` 呼叫 REST 模擬端點。
- [ ] 未知的模擬別名在透過 API 前或過程中因 `UNKNOWN_SIMULATE_EVENT_TYPE` 而失敗。
- [ ] Web host 與 CLI 僅從主 `appsettings.json` 讀取 `Database:Path`。
- [ ] `appsettings.{Environment}.json` 與環境變數不能覆蓋 `Database:Path`。
- [ ] 實作機制是明確的：資料庫路徑解析透過專用的解析器讀取主 `appsettings.json`，該解析器會忽略此金鑰的環境特定提供者與環境變數，然後將解析後的值提供給 Web host 與 CLI 啟動。

**驗證：**
- [ ] CLI 模擬聊天固定規則透過 API 路徑觸發虛擬傳送者。
- [ ] 手動測試：CLI 模擬聊天到達 overlay SignalR 用戶端，結果記錄在 `docs/phases/phase-5-web-signalr-cli/manual-verification.md`。
- [ ] 整合測試證明 `ASPNETCORE_ENVIRONMENT=Development`, `appsettings.Development.json` 以及環境變數不會覆蓋主 `appsettings.json` 的 `Database:Path`。

**可能涉及的檔案：**
- `src/Hosts/Vulperonex.Cli/Commands/SimulateCommands.cs`
- `src/Hosts/Vulperonex.Cli/Configuration/`
- `src/Hosts/Vulperonex.Web/Configuration/`
- `tests/Vulperonex.Tests.Integration/Cli/SimulateCommandTests.cs`

**規模：** M

---

## 任務 16e - 第 5 階段檢查點審查

**說明：** 執行完整的第 5 階段驗證閘口並在進入第 6 階段前處理審查後續。

**驗收標準：**
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 以 0 warning 通過。
- [ ] `dotnet test` 通過 SC-2、SC-5、SC-8、SC-9。
- [ ] WorkflowRule CRUD 與循環引用偵測端對端通過。
- [ ] 配置受保護命名空間測試通過 `security.*` 與 `oauth.*`。
- [ ] CLI rule/config/member/simulate 命令通過整合測試。
- [ ] CLI 模擬聊天固定規則與虛擬傳送者通過。
- [ ] CLI 模擬到 overlay SignalR 手動路徑已記錄。
- [ ] 僅限 loopback 的 IPv4/IPv6 雙重繫結測試通過。
- [ ] 任務 13f 第 4 階段 SC-6a/SC-6b 後續在第 5 階段關閉前完成或明確豁免。

**驗證：**
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] 手動 overlay SignalR 檢查記錄在階段待辦事項中。
- [ ] 手動驗證條目記錄在 `docs/phases/phase-5-web-signalr-cli/manual-verification.md`。
- [ ] Git 暫存集限於任務範圍，且排除無關的未追蹤檔案。

**可能涉及的檔案：**
- `docs/phases/phase-5-web-signalr-cli/todo.md`
- `docs/phases/phase-5-web-signalr-cli/manual-verification.md`
- `tasks/todo.md`
- 來自先前切片的第 5 階段源碼/測試檔案

**規模：** S
