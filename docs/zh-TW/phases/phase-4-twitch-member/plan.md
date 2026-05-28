# 第四階段詳細計畫：Twitch Adapter + MemberModule

> 父計畫：`tasks/plan.md` 第四階段
> 範圍：任務 12-13
> 目標：將真實的 Twitch adapter 連接到第三階段的事件與工作流管線，並完成 MemberRecord / Overlay DTO 的安全投影。第四階段不實作 Web API CRUD、SignalR hub 或前端 UI；這些留到第五與第六階段。

---

## 執行規則

- 每個切片使用一個小分支開發，驗證後立即提交，合併回 `main` 時使用 `git merge --ff-only`。
- 每個行為需求先撰寫 BDD 風格的 Given / When / Then 場景，再以 TDD RED / GREEN / REFACTOR 實作。
- 任務 12 不新增 NuGet 套件；若 OAuth/WebSocket 需要新套件，先依 SPEC ask-first 規則取得批准。
- Twitch adapter 是 adapter 實作層，Domain/Application 不可引用 Twitch 型別或 Twitch payload。
- Twitch adapter 僅能透過 `IStreamEventBus.PublishAsync` 發布網域事件，不直接呼叫 WorkflowEngine。
- `PlatformUserDisplayCache` 更新由 adapter 負責；`MemberModule` 不引用 `Vulperonex.Adapters.Abstractions` 或 `IPlatformUserInfoCache`。
- OAuth refresh token 儲存僅能透過任務 8 的 `IOAuthTokenStore`；Twitch adapter 不直接呼叫 `ISystemSettingsService`，不自行加密 token。
- DisplayHints 不輸出原始 HTML；安全邊界是區段類型允許清單（segment type allowlist）與前端文字渲染（text rendering），不刪改文字值。
- `--no-build` 僅可緊接在同一任務中成功編譯後使用。
- 保持 `.claude/`、資料庫檔案、測試輸出與其他本地檔案不進入提交。

---

## 依賴順序

```text
任務 12a Twitch 事件類型註冊
    -> 任務 12b IRC 訊息解析與顯示提示 (DisplayHints)
    -> 任務 12c 所有 MVP 事件的 Twitch 到網域事件對應
    -> 任務 12d Twitch/模擬工作流等效性
    -> 任務 12e 連線狀態與重連退避 (backoff)
    -> 任務 12f OAuth PKCE 回呼與權杖重新整理
    -> 任務 12g 由 Adapter 擁有的顯示快取更新

任務 13a MemberModule 事件訂閱與成員解析
    -> 任務 13b 來自訂閱/追隨訊號的成員狀態更新
    -> 任務 13c 模擬/Twitch 成員狀態等效性
    -> 任務 13d Overlay DTO 白名單合約
    -> 任務 13e 第四階段檢查點審查
    -> 任務 13f SC-6a/SC-6b 等效性強化後續
```

任務 12 依賴第三階段事件/工作流合約與第二階段權杖/快取基礎架構。任務 13 依賴任務 12，因為 SC-6b 需要將 SimulationAdapter 與 TwitchAdapter 的輸出與成員狀態進行比較。

---

## 任務 12a：Twitch adapter lifecycle and event type registration

**描述：** 建立 `TwitchAdapter` 的最小生命週期與事件類型註冊，不啟動 OAuth、IRC 或 EventSub socket。此切片僅讓真實 adapter 在 `StartAsync` 註冊 Twitch 支援的七個 MVP 金鑰與系統連線事件。

**驗收準則：**
- [ ] `TwitchAdapter` 實作 `IStreamEventSource`，位於 `Vulperonex.Adapters.Twitch`。
- [ ] `StartAsync` 註冊七個 MVP 工作流可見事件金鑰：message、followed、donated、subscribed、gifted subscription、raided、reward redeemed。
- [ ] `StartAsync` 同時註冊 `platform.connection_changed`，但標記為 `IsSystemEvent=true`。
- [ ] `StartAsync` 可重複呼叫：第二次呼叫不得重複註冊事件金鑰，不開啟第二組 socket，直接回傳成功。
- [ ] `StopAsync` 可重複呼叫且不拋出例外。
- [ ] Adapter 建構函式不要求真實 Twitch 認證資訊；測試可用虛擬對象 (fakes) 啟動。

**驗證：**
- [ ] 單元/整合測試：`TwitchAdapter.StartAsync` 後 `IStreamEventTypeRegistry.IsKnown("user.message") = true`。
- [ ] 單元/整合測試：`IsKnownForWorkflow("platform.connection_changed") = false`。
- [ ] 單元測試：重複啟動具等冪性，事件金鑰註冊僅發生一次。
- [ ] 架構測試：`Vulperonex.Adapters.Twitch` 可引用 Domain/Application/Adapters.Abstractions，但 Domain/Application 不引用 Twitch。

**依賴：** 任務 9a, 任務 9b

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/TwitchAdapter.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/TwitchAdapterEventTypeTests.cs`
- `tests/Vulperonex.Tests.Architecture/Adapters/TwitchAdapterIsolationTests.cs`

**預估規模：** S

---

## 任務 12b：IRC chat parser and DisplayHints

**描述：** 實作 Twitch IRC 訊息負載解析器，將 mock IRC 標籤轉成 `UserSentMessageEvent`，並產生安全的顯示提示 (display hints)。此切片不連接真實 socket。

**驗收準則：**
- [ ] IRC 訊息解析器產生 `UserSentMessageEvent { Platform = "twitch" }`。
- [ ] `StreamUser` 包含平臺使用者 ID、顯示名稱、角色/勳章的正規化結果。
- [ ] `display.segments` 區段類型僅允許 `text | emote | badge | mention`。
- [ ] HTML 風格的文字保留原始字串，不輸出原始 HTML 區段類型。
- [ ] `display.color` 僅接受 `^#[0-9A-Fa-f]{6}$`；不接受三位數縮寫、八位數 alpha、CSS 具名顏色或空字串。
- [ ] 勳章 (badge) 去重並保留首次出現順序，勳章 ID 僅允許 `[A-Za-z0-9_/\-]`，勳章值最多 64 字元，最多 20 個。
- [ ] `user.avatar` 僅由 Twitch 第一方負載填入；MVP 不做 URL 允許清單，overlay 端仍僅能以 DTO 安全投影方式使用。
- [ ] `user.is_subscriber` 輸出 `"true"` / `"false"` 字串，來源為 IRC 勳章/角色正規化結果。
- [ ] `user.bits_total` 輸出整數值字串；未知或非法值不輸出。

**驗證：**
- [ ] 單元測試：IRC 訊息解析 -> `UserSentMessageEvent`。
- [ ] 單元測試：HTML 風格文字僅出現在文字區段值。
- [ ] 單元測試：合法十六進位顏色保留，非法顏色省略。
- [ ] 單元測試：勳章正規化去重、ID/值過濾與截斷。
- [ ] 單元測試：avatar、is_subscriber、bits_total 顯示提示格式正確；非法 bits total 省略。

**依賴：** 任務 12a

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcMessage.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcMessageParser.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Display/`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Irc/`

**預估規模：** M

---

## 任務 12c：Twitch payload mapping for all MVP events

**描述：** 實作 mock Twitch IRC/EventSub 負載到七個 MVP 網域事件的對應 (mapping)。此切片僅處理純對應與發布路徑，不做 socket 重連/OAuth。

**驗收準則：**
- [ ] mock Twitch 負載可產生七個 MVP `IStreamEvent`：message、followed、donated、subscribed、gifted subscription、raided、reward redeemed。
- [ ] 所有事件的 `Platform` 為 `twitch`。
- [ ] 所有 Twitch 特有的負載型別不跨出 adapter 組件。
- [ ] 發布路徑僅透過 `IStreamEventBus.PublishAsync`。
- [ ] SC-1 通過：mock Twitch 負載 -> 七個 MVP 事件全部產生。

**驗證：**
- [ ] 單元測試：每種負載對應到正確的具體事件型別。
- [ ] 單元測試：訂閱層級、贈送計數、bits 總計、raid 觀眾人數、獎勵 ID/標題正確保留。
- [ ] 單元/整合測試：adapter 發布後，匯流排訂閱者收到對應事件。

**依賴：** 任務 12b

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Mapping/`
- `src/Adapters/Vulperonex.Adapters.Twitch/EventSub/`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Mapping/`

**預估規模：** M

---

## 任務 12d：Twitch and Simulation workflow equivalence

**描述：** 驗證 TwitchAdapter mock IRC 與 SimulationAdapter 對同一聊天負載造成相同 WorkflowEngine 副作用，完成 SC-6a WorkflowEngine 部分。

**驗收準則：**
- [ ] SimulationAdapter 與 TwitchAdapter mock IRC 走相同 `IStreamEventBus -> WorkflowEngine -> IPlatformChatSender` 路徑。
- [ ] 同一訊息負載觸發相同的匹配規則。
- [ ] `IPlatformChatSender.SendAsync` 收到相同訊息與平臺路由結果。
- [ ] 不使用直接呼叫 WorkflowEngine 的測試捷徑。

**驗證：**
- [ ] 整合測試：模擬執行與 Twitch mock 執行的發送者呼叫快照 (snapshot) 相同。
- [ ] `dotnet test` 中 SC-6a 通過。

**依賴：** 任務 12c, 任務 10

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/`

**預估規模：** S

---

## 任務 12e：Connection state and reconnect backoff

**描述：** 實作 Twitch adapter 的 socket 生命週期抽象與重連策略。此切片使用虛擬時鐘 (fake clock) 與虛擬 socket，不連接外部 Twitch。

**驗收準則：**
- [ ] IRC WebSocket 斷線時立即發布 `PlatformConnectionChangedEvent { Platform = "twitch", IsConnected = false, Reason = "reconnecting" }`。
- [ ] 重連成功後發布 `PlatformConnectionChangedEvent { IsConnected = true }`。
- [ ] 重連延遲使用 1s -> 2s -> 4s 指數退避，最大 60s，並套用 ±20% 抖動 (jitter) 以避免多個用戶端同步重連。
- [ ] EventSub 10 分鐘重播視窗 (replay window) 內的重播事件不因重播標記而被 adapter 過濾；僅當同一 `(platform, sourceEventId)` 在重複刪除快取 (dedup cache) 內重複送達時才跳過。重複刪除快取上限為 1000 筆條目或 10 分鐘 TTL，任一條件先達成即汰換。
- [ ] EventSub 超過重播視窗後繼續執行並記錄警告 (warning)，不發生當機 (crash) 或死結 (deadlock)。

**驗證：**
- [ ] 單元測試（虛擬時鐘 + 虛擬 socket）：前三次基準延遲約為 1s、2s、4s，套用抖動後仍落在 ±20% 範圍，且不超過 60s。
- [ ] 單元測試：斷線與重連發布連線變更事件，順序正確。
- [ ] 單元測試：EventSub 重播視窗內兩個錯過的事件均已發布。
- [ ] 單元測試：同一 `(platform, sourceEventId)` 在重複刪除快取內重複送達時僅發布一次，且 10 分鐘 TTL 到期後會釋放快取條目。
- [ ] 單元測試：EventSub 重播逾時記錄警告並繼續執行。

**依賴：** 任務 12c

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/EventSub/TwitchEventSubClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Reconnect/`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Reconnect/`

**預估規模：** M

---

## 任務 12f：OAuth PKCE callback and token refresh

**描述：** 實作 OAuth PKCE 流程的本機回呼接聽程式 (callback listener)、狀態驗證、權杖交換與重新整理權杖儲存邊界。此切片使用 mock 權杖端點，不連接 Twitch。

**驗收準則：**
- [ ] PKCE 流程產生 `code_verifier` 與對應的 `code_challenge`；權杖交換必須使用同一個 `code_verifier`。
- [ ] `state` 以加密隨機的 32 位元組產生並以 Base64Url 表示。
- [ ] 回呼接聽程式僅接受 IPv4 `127.0.0.1` 與 IPv6 `::1` loopback 請求；私有 IP 一律拒絕。
- [ ] 回呼接聽程式驗證 Host 標頭，僅接受 `localhost:{port}`、`127.0.0.1:{port}`、`[::1]:{port}`；拒絕其他 Host 以避免 DNS 重繫結 (DNS rebinding)。
- [ ] 遠端 IP 允許清單與 Host 標頭允許清單必須同時通過；僅滿足其中一項仍視為拒絕。
- [ ] 回呼接聽程式僅接受固定回呼路徑 `/auth/callback`，收到有效回呼後單次使用即關閉。
- [ ] `state` TTL 為 10 分鐘且單次使用；不符、過期或已使用時拒絕，不呼叫權杖交換端點。TTL 對齊 EventSub 重播視窗，給予使用者登入與 2FA 緩衝時間。
- [ ] 回呼連接埠預設為 7979；衝突時嘗試 7980、7981；全被占用時明確失敗，不掛起 (hang)。
- [ ] 存取權杖僅儲存在記憶體內，不寫入資料庫、不寫入記錄檔。
- [ ] 重新整理權杖僅透過 `IOAuthTokenStore.StoreRefreshTokenAsync("twitch", rawRefreshToken)` 儲存。
- [ ] `StartAsync` 若 `IOAuthTokenStore.GetRefreshTokenAsync("twitch")` 有值，呼叫 mock 重新整理端點更新記憶體內存取權杖。
- [ ] `GetRefreshTokenAsync` 拋出 `CredentialDecryptionException` 時提示重新授權，不發生當機。
- [ ] 新增或更新 `appsettings.json` 範例，包含 `Auth:CallbackPort: 7979` 與 Twitch 重新導向 URI 說明：Twitch Developer Console 登載 `http://localhost:7979/auth/callback`，並列出 `http://localhost:7980/auth/callback`、`http://localhost:7981/auth/callback` 備用重新導向 URI。
- [ ] 三個回呼連接埠都被占用時，使用者端錯誤訊息必須明確指出連接埠衝突 (port collision)，並提示關閉佔用程式或在 Twitch Developer Console 登載可用的重新導向 URI。

**驗證：**
- [ ] 單元測試：state 為 32 個隨機位元組 Base64Url，兩次產生的值不同。
- [ ] 單元測試：state 有 10 分鐘 TTL、過期拒絕、有效回呼後 state 不能重用。
- [ ] 單元測試：code challenge 由 code verifier 產生，權杖交換使用原始 verifier。
- [ ] 單元測試：state 不符時不交換權杖碼。
- [ ] 單元測試：`127.0.0.1` / `::1` 請求被接受；`192.168.x.x` / 非 loopback 請求被拒絕。
- [ ] 單元測試：Host 標頭非 `localhost:{port}` / `127.0.0.1:{port}` / `[::1]:{port}` 時拒絕。
- [ ] 單元測試：非 `/auth/callback` 路徑被忽略。
- [ ] 單元測試：有效回呼後接聽程式單次使用即失效。
- [ ] 單元測試：回呼連接埠衝突時遞增至 7980。
- [ ] 單元測試：7979、7980、7981 全被占用時流程失敗且不掛起。
- [ ] 單元測試：mock 交換後存取權杖僅限記憶體；記錄器不含存取權杖、授權碼、code verifier。
- [ ] 單元測試：重新整理權杖儲存收到原始重新整理權杖；記錄器不含原始重新整理權杖或 `refresh_token` 純文字值。
- [ ] 單元測試：啟動時重新整理權杖流程與解密失敗流程。

**依賴：** 任務 8, 任務 12a

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/OAuthCallbackListener.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/TwitchOAuthClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/TwitchAccessTokenProvider.cs`
- `src/Hosts/Vulperonex.Web/appsettings.json`（若尚不存在則新增範例設定）
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Auth/`

**預估規模：** M

---

## 任務 12g：Adapter-owned display cache updates

**描述：** 讓 Twitch adapter 在處理 Twitch 事件時更新 `IPlatformUserInfoCache`。此責任停留在 adapter 基礎架構層，Application/Domain 不感知顯示快取。

**驗收準則：**
- [ ] `UserSubscribedEvent` 對應的 adapter 回呼呼叫 `IPlatformUserInfoCache.UpdateAsync`，使 `IsSubscriber=true`。
- [ ] `UserDonatedEvent.TotalBitsGiven` 是 Twitch 累計絕對值；快取更新使用單調絕對值取代 (monotonic absolute replacement)：`TotalBitsGiven = max(existing, incoming)`，不使用累加，避免順序錯亂的舊負載造成數值回退。
- [ ] 重播同一個贊助事件不會重複累加 `TotalBitsGiven`。
- [ ] `UserFollowedEvent` 更新後追隨者勳章出現在快取勳章中。
- [ ] Adapter 快取更新不在 `MemberModule` 中實作。
- [ ] 開放問題：若 Twitch 後臺人工調整 bits 累計總計並需要降低本機值，第四階段不自動回退；未來需透過明確的管理員重設流程處理。

**驗證：**
- [ ] 單元測試：訂閱事件更新快取。
- [ ] 單元測試：贊助總額使用單調絕對值取代，重播具等冪性，較小的順序錯亂傳入值不覆蓋較大的現有值。
- [ ] 單元測試：追隨事件更新追隨者勳章。
- [ ] 架構測試或相依性檢查：Application/Domain 不引用 `IPlatformUserInfoCache`。

**依賴：** 任務 7, 任務 12c

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Mapping/`
- `src/Adapters/Vulperonex.Adapters.Abstractions/IPlatformUserInfoCache.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/DisplayCache/`

**預估規模：** S

---

## 任務 13a：MemberModule event subscription and member resolution

**描述：** 實作 `MemberModule`，訂閱網域事件並透過 `IMemberResolver` 建立/解析 MemberRecord。此切片先完成 SC-8 的基本成員建立。

**驗收準則：**
- [ ] `MemberModule` 位於 Application 或符合現有裝載服務 (hosted service) 模式的邊界中。
- [ ] 訂閱 `IStreamEvent` 或明確的 MVP 使用者事件，收到 `UserSentMessageEvent` 時呼叫 `IMemberResolver.ResolveAsync`。
- [ ] 發布 `UserSentMessageEvent` 後建立 `PlatformIdentity`。
- [ ] MemberId 為 ULID 格式。
- [ ] `MemberModule` 不引用 `Vulperonex.Adapters.Abstractions` 或 `IPlatformUserInfoCache`。

**驗證：**
- [ ] 整合測試：發布 `UserSentMessageEvent` -> 建立 MemberRecord。
- [ ] 整合測試：MemberId 符合 ULID 格式。
- [ ] 架構測試：MemberModule 不引用 adapter 抽象/顯示快取。

**依賴：** 任務 7, 任務 10

**可能涉及的檔案：**
- `src/Vulperonex.Application/Members/MemberModule.cs`
- `tests/Vulperonex.Tests.Integration/Members/MemberModuleTests.cs`
- `tests/Vulperonex.Tests.Architecture/Members/MemberModuleDependencyTests.cs`

**預估規模：** M

---

## 任務 13b：Member state updates from stream events

**描述：** 擴充 MemberModule 對訂閱/追隨等事件的成員狀態更新。顯示快取仍由 adapter 更新；MemberModule 僅處理 MemberRecord 狀態。

**驗收準則：**
- [ ] `UserSubscribedEvent` 更新 MemberRecord 訂閱者狀態。
- [ ] 追隨/訂閱等事件先解析成員，再更新狀態。
- [ ] 更新邏輯具等冪性，TDQ 重播不造成重複累加或重複資料列；重複刪除金鑰使用 `(platform, sourceEventId)`，mock 事件必須提供穩定的 `sourceEventId`，不得使用每次處理新產生的 ULID 作為重播重複刪除金鑰。
- [ ] MemberModule 不讀取顯示提示，不使用 Twitch 負載。

**驗證：**
- [ ] 整合測試：`UserSubscribedEvent` -> MemberRecord 訂閱者狀態已更新。
- [ ] 整合測試：重播同一個 `(platform, sourceEventId)` 訂閱事件會保持相同的成員身分與狀態。
- [ ] 單元/整合測試：缺少成員的路徑在更新前會使用解析器。

**依賴：** 任務 13a

**可能涉及的檔案：**
- `src/Vulperonex.Application/Members/MemberModule.cs`
- `src/Vulperonex.Application/Members/`
- `tests/Vulperonex.Tests.Integration/Members/`

**預估規模：** S

---

## 任務 13c：Simulation/Twitch member-state equivalence

**描述：** 完成 SC-6b MemberRecord 部分：SimulationAdapter 與 TwitchAdapter mock IRC 對同一負載造成相同的 MemberRecord 資料庫狀態。

**驗收準則：**
- [ ] 模擬執行使用乾淨的 SQLite 測試環境 (fixture)。
- [ ] Twitch mock 執行使用另一個乾淨的 SQLite 測試環境。
- [ ] 兩次各自發布相同的使用者/訊息負載，等待匯流排閒置後快照 MemberRecord 狀態。
- [ ] S1 == S2；測試不得共用資料庫以免造成偽陽性 (false positive)。
- [ ] SC-6a/SC-6b 的命名在測試與文件中保持一致。

**驗證：**
- [ ] 整合測試：SimulationAdapter 負載 X -> 快照 S1。
- [ ] 整合測試：TwitchAdapter mock IRC 負載 X -> 快照 S2。
- [ ] 斷言 S1 == S2。

**依賴：** 任務 12d, 任務 13b

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Integration/Members/MemberEquivalenceTests.cs`
- `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`

**預估規模：** M

---

## 任務 13d：Overlay DTO whitelist contracts

**描述：** 建立 Overlay 負載 DTO 與白名單測試，確保面向 Overlay 的 JSON 不會洩漏 MemberId、PlatformUserId、TotalLoyalty、LinkedPlatforms 或其他內部欄位。SignalR hub 實作留到任務 15；此切片僅定義 DTO 合約與 `System.Text.Json` 序列化金鑰集。

**驗收準則：**
- [ ] `OverlayChatPayload` JSON 屬性集精確等於 `{schemaVersion, eventId, timestamp, displayName, colorHex, segments, badges}`。
- [ ] `OverlayAlertPayload` JSON 屬性集精確等於 `{schemaVersion, eventId, timestamp, displayName, eventType, tier}`。
- [ ] `OverlayMemberPayload` JSON 屬性集精確等於 `{schemaVersion, displayName, avatarUrl, checkInCount}`。
- [ ] `schemaVersion` 固定為 `1`；`eventId` 是公開交付 ID，用於 overlay 去重，不得使用 MemberId、PlatformUserId 或其他內部身分；優先採用平臺提供的 ID（IRC `msg-id` / EventSub `message_id`），缺值時由 adapter 生成 ULID 並標記為合成 (synthetic)；`timestamp` 為 UTC ISO-8601 事件時間，用於 overlay 排序。
- [ ] `OverlayMemberPayload` 是狀態快照，不是事件串流；因此不含 `eventId` / `timestamp`，避免後續為了對稱而誤加事件中繼資料。
- [ ] DTO 不包含 MemberId、UserId、PlatformUserId、TotalBitsGiven、TotalLoyalty、LinkedPlatforms。
- [ ] 測試使用精確匹配，不只做黑名單過濾。
- [ ] 第四階段不驗證 SignalR hub 序列化；任務 15 必須重用同一個 DTO 並驗證 SignalR 負載金鑰集。

**驗證：**
- [ ] 單元測試：每個 DTO 型別反射 JSON 可序列化屬性精確匹配。
- [ ] 單元測試：System.Text.Json 序列化金鑰集精確匹配。
- [ ] 架構/程式碼審查：Overlay DTO 不引用持久化實體。
- [ ] 文件檢查：任務 15 的 SignalR 序列化精確金鑰集驗證仍保留，不視為第四階段已完成。

**依賴：** 任務 13a

**可能涉及的檔案：**
- `src/Vulperonex.Application/Overlay/Dtos/OverlayChatPayload.cs`
- `src/Vulperonex.Application/Overlay/Dtos/OverlayAlertPayload.cs`
- `src/Vulperonex.Application/Overlay/Dtos/OverlayMemberPayload.cs`
- `src/Vulperonex.Application/Overlay/OverlayModule.cs`
- `tests/Vulperonex.Tests.Unit/Overlay/`

**預估規模：** S

---

## 任務 13e：Phase 4 checkpoint review

**描述：** 完成第四階段的收尾驗證與審查閘口，僅在任務 12/13 完成後執行。

**驗收準則：**
- [ ] 任務 12a-12g、任務 13a-13d 已完成並以小切片提交。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 通過，0 警告。
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過。
- [ ] SC-1、SC-6a、SC-6b、SC-8 通過。
- [ ] Twitch IRC mock -> `UserSentMessageEvent` -> 建立 MemberRecord 通過。
- [ ] Overlay DTO 精確白名單通過。
- [ ] 網域/應用程式架構洩漏測試持續通過。
- [ ] `git status --short` 乾淨；若需檢查忽略檔案，另執行 `git status --short --ignored` 並確認僅出現預期的本地檔案。

**審查門檻：**
- [ ] 開始第五階段前人工審查 Twitch adapter 邊界、OAuth/權杖處理、顯示快取擁有權、MemberModule 依賴方向、Overlay DTO 白名單。

**依賴：** 任務 13d

**可能涉及的檔案：**
- `docs/phases/phase-4-twitch-member/todo.md`
- `tasks/todo.md`

**預估規模：** S

---

## 任務 13f - SC-6a/SC-6b equivalence strengthening follow-up

**說明：** 第五階段檢查點取決於更強大的第四階段等效性閘口，以便在聊天回顯快樂路徑（happy path）之外比較模擬與 Twitch 路徑。

**驗收標準：**
- [ ] 新增用於模擬與 Twitch adapter 等效性比較的追隨、訂閱與贊助負載測試資料 (fixtures)。
- [ ] 斷言快取狀態與成員狀態副作用，而不僅是發出的聊天/動作輸出。
- [ ] 斷言 `TotalBitsGiven` 呈單調性，且訂閱者層級狀態在模擬與 Twitch 路徑之間匹配。
- [ ] 除非第五階段檢查點明確豁免，否則將此保留為後續積壓條目。

**驗證：**
- [ ] 整合測試：若模擬與 Twitch 路徑在追隨/訂閱/贊助副作用上發散，則測試失敗。
- [ ] 第五階段檢查點連結到已完成的後續工作或記錄豁免情形。

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`
- `tests/Vulperonex.Tests.Integration/Members/MemberEquivalenceTests.cs`

**預估規模：** S

---

## 第四階段檢查點

**驗收準則：**
- [ ] `dotnet test` -> SC-1、SC-6a、SC-6b、SC-8 通過。
- [ ] Twitch IRC mock -> `UserSentMessageEvent` -> 建立 MemberRecord。
- [ ] Overlay DTO 精確白名單正確。
- [ ] OAuth PKCE state/callback/token 邊界測試通過。
- [ ] DisplayHints 區段允許清單與顏色/勳章正規化測試通過。
- [ ] Adapter 快取更新等冪性測試通過。
- [ ] 第五階段開始前完成第四階段審查。

---

## 風險與緩解

| 風險 | 影響 | 緩解措施 |
|------|------|----------|
| 任務 12 過大 | 高 | 拆分為註冊、解析器、對應、等效性、重連、OAuth、快取更新七個切片。 |
| 真實 Twitch/OAuth 造成不可重現的測試 | 高 | 第四階段全部使用 mock 負載、虛擬 socket、mock 權杖端點；真實登入留給後續手動驗證。 |
| OAuth 祕密洩漏至記錄/設定 | 高 | OAuth 測試明確掃描記錄器接收端，adapter 僅持有記憶體內存取權杖，重新整理權杖僅交給 `IOAuthTokenStore`；授權碼、code verifier、原始重新整理權杖不進入記錄。 |
| DisplayHints XSS 邊界混亂 | 高 | 區段類型允許清單 + 精確 DTO 測試；文字值不作為 HTML 渲染是前端責任，第六階段需再驗證。 |
| MemberModule 越界存取 adapter 快取 | 中 | 架構測試阻擋 Application/Domain 引用 `Vulperonex.Adapters.Abstractions` / `IPlatformUserInfoCache`。 |
| SC-6b 偽陽性 (false positive) | 中 | 模擬/Twitch 等效性測試必須使用兩個乾淨的 SQLite 測試環境。 |

## 開放問題

- 任務 12f 是否需要真實的 HTTP listener 實作，還是先以 listener 抽象 + 單元測試驗證單次使用/loopback/路徑/連接埠行為。建議先做抽象，避免第四階段卡在作業系統 socket 細節。
- Overlay DTO 是否僅放在 Application，還是第五階段 SignalR hub 需要 host 層級的 DTO。建議第四階段先放 Application 合約，第五階段 hub 僅序列化該 DTO。
- 第五階段多 overlay 客戶端場景需重新評估合成 `eventId` 去重語義：平臺提供的 ID 可跨客戶端識別同一事件；adapter 後備 ULID 僅保證本機單實例交付 ID。
