# 第四階段詳細計畫：Twitch Adapter + MemberModule

> 父計畫：`tasks/plan.md` 第四階段
> 範圍：任務 12-13
> 目標：把真實 Twitch adapter 接到 Phase 3 的事件與 workflow 管線，並完成 MemberRecord / Overlay DTO 安全投影。Phase 4 不做 Web API CRUD、SignalR hub 或前端 UI；那些留到 Phase 5/6。

---

## 執行規則

- 每個切片使用一個小分支開發，驗證後立即提交，合併回 `main` 時使用 `git merge --ff-only`。
- 每個行為需求先寫 BDD-style Given / When / Then scenario，再以 TDD RED / GREEN / REFACTOR 實作。
- Task 12 不新增 NuGet 套件；若 OAuth/WebSocket 需要新套件，先依 SPEC ask-first 規則取得批准。
- Twitch adapter 是 adapter implementation，Domain/Application 不可引用 Twitch 型別或 Twitch payload。
- Twitch adapter 只能透過 `IStreamEventBus.PublishAsync` 發布 domain events，不直接呼叫 WorkflowEngine。
- `PlatformUserDisplayCache` 更新由 adapter 負責；`MemberModule` 不引用 `Vulperonex.Adapters.Abstractions` 或 `IPlatformUserInfoCache`。
- OAuth refresh token 儲存只能透過 Task 8 的 `IOAuthTokenStore`；Twitch adapter 不直接呼叫 `ISystemSettingsService`，不自行加密 token。
- DisplayHints 不輸出 raw HTML；安全邊界是 segment type allowlist 與前端 text rendering，不刪改 text 值。
- `--no-build` 只可緊接在同一任務中成功編譯後使用。
- 保持 `.claude/`、DB 檔、測試輸出與其他本地檔案不進入提交。

---

## 依賴順序

```
Task 12a Twitch event type registration
    -> Task 12b IRC message parser and display hints
    -> Task 12c Twitch-to-domain event mapping for all MVP events
    -> Task 12d Twitch/Simulation workflow equivalence
    -> Task 12e connection state and reconnect backoff
    -> Task 12f OAuth PKCE callback and token refresh
    -> Task 12g adapter-owned display cache updates

Task 13a MemberModule event subscription and member resolution
    -> Task 13b member state updates from subscription/follow signals
    -> Task 13c Simulation/Twitch member-state equivalence
    -> Task 13d Overlay DTO whitelist contracts
    -> Task 13e Phase 4 checkpoint review
```

Task 12 depends on Phase 3 event/workflow contracts and Phase 2 token/cache infrastructure. Task 13 depends on Task 12 because SC-6b compares SimulationAdapter and TwitchAdapter output against member state.

---

## Task 12a：Twitch adapter lifecycle and event type registration

**描述：** 建立 `TwitchAdapter` 的最小 lifecycle 與 event type registration，不啟動 OAuth、IRC 或 EventSub socket。此切片只讓真實 adapter 在 `StartAsync` 註冊 Twitch 支援的 seven MVP keys 與 system connection event。

**驗收準則：**
- [ ] `TwitchAdapter` 實作 `IStreamEventSource`，位於 `Vulperonex.Adapters.Twitch`。
- [ ] `StartAsync` 註冊 seven MVP workflow-visible event keys：message、followed、donated、subscribed、gifted subscription、raided、reward redeemed。
- [ ] `StartAsync` 同時註冊 `platform.connection_changed`，但標記為 `IsSystemEvent=true`。
- [ ] `StartAsync` 可重複呼叫：第二次呼叫不得重複註冊 event keys，不開第二組 socket，直接回傳成功。
- [ ] `StopAsync` 可重複呼叫且不拋例外。
- [ ] Adapter constructor 不要求真實 Twitch credentials；測試可用 fakes 啟動。

**驗證：**
- [ ] Unit/integration test：`TwitchAdapter.StartAsync` 後 `IStreamEventTypeRegistry.IsKnown("user.message") = true`。
- [ ] Unit/integration test：`IsKnownForWorkflow("platform.connection_changed") = false`。
- [ ] Unit test：double-start idempotent，event key registration 只發生一次。
- [ ] Architecture test：`Vulperonex.Adapters.Twitch` 可引用 Domain/Application/Adapters.Abstractions，但 Domain/Application 不引用 Twitch。

**依賴：** Task 9a, Task 9b

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/TwitchAdapter.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/TwitchAdapterEventTypeTests.cs`
- `tests/Vulperonex.Tests.Architecture/Adapters/TwitchAdapterIsolationTests.cs`

**預估規模：** S

---

## Task 12b：IRC chat parser and DisplayHints

**描述：** 實作 Twitch IRC message payload parser，將 mock IRC tags 轉成 `UserSentMessageEvent`，並產生安全的 display hints。此切片不連真實 socket。

**驗收準則：**
- [ ] IRC message parser 產生 `UserSentMessageEvent { Platform = "twitch" }`。
- [ ] `StreamUser` 包含 platform user id、display name、roles/badges 的 normalized 結果。
- [ ] `display.segments` segment type 只允許 `text | emote | badge | mention`。
- [ ] HTML-like text 保留原字串，不輸出 raw HTML segment type。
- [ ] `display.color` 只接受 `^#[0-9A-Fa-f]{6}$`；不接受 3-digit shorthand、8-digit alpha、CSS named color 或空字串。
- [ ] badge 去重保留首次出現順序，badge ID 只允許 `[A-Za-z0-9_/\-]`，badge value 最多 64 字元，最多 20 個。
- [ ] `user.avatar` 只由 Twitch first-party payload 填入；MVP 不做 URL allowlist，overlay 端仍只能以 DTO 安全投影使用。
- [ ] `user.is_subscriber` 輸出 `"true"` / `"false"` 字串，來源為 IRC badges/roles normalized 結果。
- [ ] `user.bits_total` 輸出整數值字串；未知或非法值不輸出。

**驗證：**
- [ ] Unit test：IRC 訊息解析 -> `UserSentMessageEvent`。
- [ ] Unit test：HTML-like text 只出現在 text segment value。
- [ ] Unit test：合法 hex color 保留，非法 color 省略。
- [ ] Unit test：badge normalization 去重、ID/value 過濾與截斷。
- [ ] Unit test：avatar、is_subscriber、bits_total display hints 格式正確；非法 bits total 省略。

**依賴：** Task 12a

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcMessage.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcMessageParser.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Display/`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Irc/`

**預估規模：** M

---

## Task 12c：Twitch payload mapping for all MVP events

**描述：** 實作 mock Twitch IRC/EventSub payload 到 seven MVP domain events 的 mapping。此切片只處理純 mapping 與 publish path，不做 socket reconnect/OAuth。

**驗收準則：**
- [ ] mock Twitch payload 可產生 seven MVP `IStreamEvent`：message、followed、donated、subscribed、gifted subscription、raided、reward redeemed。
- [ ] 所有事件的 `Platform` 為 `twitch`。
- [ ] 所有 Twitch-specific payload 型別不跨出 adapter assembly。
- [ ] publish path 只透過 `IStreamEventBus.PublishAsync`。
- [ ] SC-1 通過：mock Twitch payload -> seven MVP events 全部產生。

**驗證：**
- [ ] Unit test：每種 payload 對應到正確 concrete event type。
- [ ] Unit test：subscription tier、gift count、bits total、raid viewer count、reward id/title 正確保留。
- [ ] Unit/integration test：adapter publish 後 bus subscriber 收到對應 event。

**依賴：** Task 12b

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Mapping/`
- `src/Adapters/Vulperonex.Adapters.Twitch/EventSub/`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Mapping/`

**預估規模：** M

---

## Task 12d：Twitch and Simulation workflow equivalence

**描述：** 驗證 TwitchAdapter mock IRC 與 SimulationAdapter 對同一 chat payload 造成相同 WorkflowEngine 副作用，完成 SC-6a WorkflowEngine half。

**驗收準則：**
- [ ] SimulationAdapter 與 TwitchAdapter mock IRC 走相同 `IStreamEventBus -> WorkflowEngine -> IPlatformChatSender` 路徑。
- [ ] 同一 message payload 觸發相同 matching rule。
- [ ] `IPlatformChatSender.SendAsync` 收到相同 message 與 platform routing 結果。
- [ ] 不使用直接呼叫 WorkflowEngine 的測試捷徑。

**驗證：**
- [ ] Integration test：Simulation run 與 Twitch mock run 的 sender call snapshot 相同。
- [ ] `dotnet test` 中 SC-6a 通過。

**依賴：** Task 12c, Task 10

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/`

**預估規模：** S

---

## Task 12e：Connection state and reconnect backoff

**描述：** 實作 Twitch adapter 的 socket lifecycle abstraction 與 reconnect policy。此切片使用 fake socket/fake clock，不連外部 Twitch。

**驗收準則：**
- [ ] IRC WebSocket 斷線時立即 publish `PlatformConnectionChangedEvent { Platform = "twitch", IsConnected = false, Reason = "reconnecting" }`。
- [ ] 重連成功後 publish `PlatformConnectionChangedEvent { IsConnected = true }`。
- [ ] reconnect delay 使用 1s -> 2s -> 4s 指數退避，最大 60s，並套用 ±20% jitter 避免多 client 同步重連。
- [ ] EventSub 10 分鐘 replay window 內的 replay events 不因 replay 標記被 adapter 過濾；只有同一 `(platform, sourceEventId)` 在 dedup cache 內重複送達時才跳過。Dedup cache 上限為 1000 entries 或 10 分鐘 TTL，任一先到即淘汰。
- [ ] EventSub 超過 replay window 後繼續運行並記錄 warning，不 crash / deadlock。

**驗證：**
- [ ] Unit test（fake clock + fake socket）：前三次 base delay 約為 1s、2s、4s，套用 jitter 後仍落在 ±20% 範圍，且不超過 60s。
- [ ] Unit test：斷線與重連 publish connection changed events，順序正確。
- [ ] Unit test：EventSub replay window 內兩個 missed events 均 publish。
- [ ] Unit test：同一 `(platform, sourceEventId)` 在 dedup cache 內重複送達時只 publish 一次，且 10 分鐘 TTL 到期後會釋放 cache entry。
- [ ] Unit test：EventSub replay 超時記錄 warning 並繼續運行。

**依賴：** Task 12c

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/EventSub/TwitchEventSubClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Reconnect/`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Reconnect/`

**預估規模：** M

---

## Task 12f：OAuth PKCE callback and token refresh

**描述：** 實作 OAuth PKCE flow 的本機 callback listener、state 驗證、token exchange 與 refresh token 儲存邊界。此切片使用 mock token endpoint，不連 Twitch。

**驗收準則：**
- [ ] PKCE flow 產生 `code_verifier` 與對應 `code_challenge`；token exchange 必須使用同一個 `code_verifier`。
- [ ] `state` 以 cryptographically random 32 bytes 產生並以 Base64Url 表示。
- [ ] callback listener 只接受 IPv4 `127.0.0.1` 與 IPv6 `::1` loopback request；LAN/private IP 一律拒絕。
- [ ] callback listener 驗證 Host header，只接受 `localhost:{port}`、`127.0.0.1:{port}`、`[::1]:{port}`；其他 Host 拒絕，避免 DNS rebinding。
- [ ] Remote IP allowlist 與 Host header allowlist 必須同時通過；只滿足其中一項仍視為拒絕。
- [ ] callback listener 只接受固定 callback path `/auth/callback`，收到有效 callback 後 single-use 關閉。
- [ ] `state` TTL 為 10 分鐘且 single-use；不符、過期或已使用時拒絕，不呼叫 token exchange endpoint。TTL 對齊 EventSub replay window，給使用者登入與 2FA 緩衝。
- [ ] callback port 預設 7979；衝突時嘗試 7980、7981；全被占用時明確失敗，不 hang。
- [ ] access token 只存 in-memory，不寫 DB、不寫 log。
- [ ] refresh token 只透過 `IOAuthTokenStore.StoreRefreshTokenAsync("twitch", rawRefreshToken)` 儲存。
- [ ] `StartAsync` 若 `IOAuthTokenStore.GetRefreshTokenAsync("twitch")` 有值，呼叫 mock refresh endpoint 更新 in-memory access token。
- [ ] `GetRefreshTokenAsync` 拋 `CredentialDecryptionException` 時提示重新授權，不 crash。
- [ ] 新增或更新 `appsettings.json` 範例，包含 `Auth:CallbackPort: 7979` 與 Twitch Redirect URI 說明：Twitch Developer Console 登錄 `http://localhost:7979/auth/callback`，並列出 `http://localhost:7980/auth/callback`、`http://localhost:7981/auth/callback` 備用 redirect URI。
- [ ] 三個 callback port 都被占用時，user-facing error 必須明確指出 port collision，並提示關閉占用程式或在 Twitch Developer Console 登錄可用 redirect URI。

**驗證：**
- [ ] Unit test：state 為 32 random bytes Base64Url，兩次產生值不同。
- [ ] Unit test：state 10 分鐘 TTL、過期拒絕、valid callback 後 state 不能重用。
- [ ] Unit test：code challenge 由 code verifier 產生，token exchange 使用原始 verifier。
- [ ] Unit test：state mismatch 不 exchange code。
- [ ] Unit test：`127.0.0.1` / `::1` request 被接受；`192.168.x.x` / 非 loopback request 被拒絕。
- [ ] Unit test：Host header 非 `localhost:{port}` / `127.0.0.1:{port}` / `[::1]:{port}` 時拒絕。
- [ ] Unit test：非 `/auth/callback` path 被忽略。
- [ ] Unit test：valid callback 後 listener single-use。
- [ ] Unit test：callback port conflict 遞增至 7980。
- [ ] Unit test：7979、7980、7981 全被占用時 flow 失敗且不 hang。
- [ ] Unit test：mock exchange 後 access token in-memory only；logger 不含 access token、authorization code、code verifier。
- [ ] Unit test：refresh token store 收到 raw refresh token；logger 不含 raw refresh token 或 `refresh_token` plain value。
- [ ] Unit test：startup refresh token flow 與 decryption failure flow。

**依賴：** Task 8, Task 12a

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/OAuthCallbackListener.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/TwitchOAuthClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/TwitchAccessTokenProvider.cs`
- `src/Hosts/Vulperonex.Web/appsettings.json`（若尚不存在則新增範例設定）
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/Auth/`

**預估規模：** M

---

## Task 12g：Adapter-owned display cache updates

**描述：** 讓 Twitch adapter 在處理 Twitch events 時更新 `IPlatformUserInfoCache`。此責任停留在 adapter infrastructure，Application/Domain 不知道 display cache。

**驗收準則：**
- [ ] `UserSubscribedEvent` 對應的 adapter callback 呼叫 `IPlatformUserInfoCache.UpdateAsync`，使 `IsSubscriber=true`。
- [ ] `UserDonatedEvent.TotalBitsGiven` 是 Twitch cumulative absolute value；cache update 使用 monotonic absolute replacement：`TotalBitsGiven = max(existing, incoming)`，不使用累加，避免 out-of-order 舊 payload 回退。
- [ ] replay 同一 donation event 不會重複累加 `TotalBitsGiven`。
- [ ] `UserFollowedEvent` update 後 follower badge 出現在 cache badges。
- [ ] Adapter cache update 不在 `MemberModule` 中實作。
- [ ] 開放問題：若 Twitch 後台人工調整 bits cumulative total 並需要降低本地值，Phase 4 不自動回退；未來需透過明確 admin reset 流程處理。

**驗證：**
- [ ] Unit test：subscribe event update cache。
- [ ] Unit test：donation total 使用 monotonic absolute replacement，replay idempotent，out-of-order 較小 incoming value 不覆蓋較大 existing value。
- [ ] Unit test：follow event update follower badge。
- [ ] Architecture test 或 dependency inspection：Application/Domain 不引用 `IPlatformUserInfoCache`。

**依賴：** Task 7, Task 12c

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Twitch/Mapping/`
- `src/Adapters/Vulperonex.Adapters.Abstractions/IPlatformUserInfoCache.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/DisplayCache/`

**預估規模：** S

---

## Task 13a：MemberModule event subscription and member resolution

**描述：** 實作 `MemberModule`，訂閱 domain events 並透過 `IMemberResolver` 建立/解析 MemberRecord。此切片先完成 SC-8 的基本 member creation。

**驗收準則：**
- [ ] `MemberModule` 位於 Application 或符合既有 hosted service pattern 的邊界中。
- [ ] 訂閱 `IStreamEvent` 或明確 MVP user events，收到 `UserSentMessageEvent` 時呼叫 `IMemberResolver.ResolveAsync`。
- [ ] publish `UserSentMessageEvent` 後建立 `PlatformIdentity`。
- [ ] MemberId 為 ULID 格式。
- [ ] `MemberModule` 不引用 `Vulperonex.Adapters.Abstractions` 或 `IPlatformUserInfoCache`。

**驗證：**
- [ ] Integration test：publish `UserSentMessageEvent` -> MemberRecord created。
- [ ] Integration test：MemberId ULID 格式。
- [ ] Architecture test：MemberModule 不引用 adapter abstractions/display cache。

**依賴：** Task 7, Task 10

**可能涉及的檔案：**
- `src/Vulperonex.Application/Members/MemberModule.cs`
- `tests/Vulperonex.Tests.Integration/Members/MemberModuleTests.cs`
- `tests/Vulperonex.Tests.Architecture/Members/MemberModuleDependencyTests.cs`

**預估規模：** M

---

## Task 13b：Member state updates from stream events

**描述：** 擴充 MemberModule 對 subscription/follow 等事件的 member state 更新。Display cache 仍由 adapter 更新；MemberModule 只處理 MemberRecord 狀態。

**驗收準則：**
- [ ] `UserSubscribedEvent` 更新 MemberRecord subscriber 狀態。
- [ ] follow/subscription 等事件先 resolve member，再更新狀態。
- [ ] 更新邏輯 idempotent，TDQ replay 不造成重複累加或重複 row；dedup key 使用 `(platform, sourceEventId)`，mock events 必須提供穩定 `sourceEventId`，不得使用每次處理新產生的 ULID 作為 replay dedup key。
- [ ] MemberModule 不讀取 display hints，不使用 Twitch payload。

**驗證：**
- [ ] Integration test：`UserSubscribedEvent` -> MemberRecord subscriber state updated。
- [ ] Integration test：replay same `(platform, sourceEventId)` subscription event keeps same member identity and state。
- [ ] Unit/integration test：missing member path uses resolver before update。

**依賴：** Task 13a

**可能涉及的檔案：**
- `src/Vulperonex.Application/Members/MemberModule.cs`
- `src/Vulperonex.Application/Members/`
- `tests/Vulperonex.Tests.Integration/Members/`

**預估規模：** S

---

## Task 13c：Simulation/Twitch member-state equivalence

**描述：** 完成 SC-6b MemberRecord half：SimulationAdapter 與 TwitchAdapter mock IRC 對同一 payload 造成相同 MemberRecord DB state。

**驗收準則：**
- [ ] Simulation run 使用 fresh SQLite fixture。
- [ ] Twitch mock run 使用另一個 fresh SQLite fixture。
- [ ] 兩次各自 publish 相同 user/message payload，等待 bus idle 後 snapshot MemberRecord state。
- [ ] S1 == S2；測試不得共用 DB 造成 false positive。
- [ ] SC-6a/SC-6b 的命名在測試與 docs 中保持一致。

**驗證：**
- [ ] Integration test：SimulationAdapter payload X -> snapshot S1。
- [ ] Integration test：TwitchAdapter mock IRC payload X -> snapshot S2。
- [ ] Assert S1 == S2。

**依賴：** Task 12d, Task 13b

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Integration/Members/MemberEquivalenceTests.cs`
- `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`

**預估規模：** M

---

## Task 13d：Overlay DTO whitelist contracts

**描述：** 建立 Overlay payload DTO 與 whitelist tests，確保 Overlay-facing JSON 不洩漏 MemberId、PlatformUserId、TotalLoyalty、LinkedPlatforms 或其他內部欄位。SignalR hub 實作留到 Task 15；此切片只定義 DTO contract 與 `System.Text.Json` serialization key set。

**驗收準則：**
- [ ] `OverlayChatPayload` JSON property set 精確等於 `{schemaVersion, eventId, timestamp, displayName, colorHex, segments, badges}`。
- [ ] `OverlayAlertPayload` JSON property set 精確等於 `{schemaVersion, eventId, timestamp, displayName, eventType, tier}`。
- [ ] `OverlayMemberPayload` JSON property set 精確等於 `{schemaVersion, displayName, avatarUrl, checkInCount}`。
- [ ] `schemaVersion` 固定為 `1`；`eventId` 是 public delivery id，用於 overlay 去重，不得使用 MemberId、PlatformUserId 或其他內部 identity；優先來自 platform-provided id（IRC `msg-id` / EventSub `message_id`），缺值時 adapter 生成 ULID 並標記為 synthetic；`timestamp` 為 UTC ISO-8601 event time，用於 overlay 排序。
- [ ] `OverlayMemberPayload` 是狀態快照，不是事件流；因此不含 `eventId` / `timestamp`，避免後續為了對稱而誤加事件 metadata。
- [ ] DTO 不包含 MemberId、UserId、PlatformUserId、TotalBitsGiven、TotalLoyalty、LinkedPlatforms。
- [ ] tests 使用 exact match，不只做 denylist。
- [ ] Phase 4 不驗 SignalR hub serialization；Task 15 必須重用同一 DTO 並驗證 SignalR payload key set。

**驗證：**
- [ ] Unit test：每個 DTO 型別 reflection JSON-serializable properties exact match。
- [ ] Unit test：System.Text.Json serialized key set exact match。
- [ ] Architecture/code review：Overlay DTO 不引用 persistence entity。
- [ ] Documentation check：Task 15 的 SignalR serialization exact key-set 驗證仍保留，不視為 Phase 4 已完成。

**依賴：** Task 13a

**可能涉及的檔案：**
- `src/Vulperonex.Application/Overlay/Dtos/OverlayChatPayload.cs`
- `src/Vulperonex.Application/Overlay/Dtos/OverlayAlertPayload.cs`
- `src/Vulperonex.Application/Overlay/Dtos/OverlayMemberPayload.cs`
- `src/Vulperonex.Application/Overlay/OverlayModule.cs`
- `tests/Vulperonex.Tests.Unit/Overlay/`

**預估規模：** S

---

## Task 13e：Phase 4 checkpoint review

**描述：** 完成 Phase 4 的收尾驗證與 review gate，只在 Task 12/13 完成後執行。

**驗收準則：**
- [ ] Task 12a-12g、Task 13a-13d 已完成並以小切片提交。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 通過，0 warnings。
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過。
- [ ] SC-1、SC-6a、SC-6b、SC-8 通過。
- [ ] Twitch IRC mock -> `UserSentMessageEvent` -> MemberRecord 建立通過。
- [ ] Overlay DTO exact whitelist 通過。
- [ ] Domain/Application architecture leakage tests 持續通過。
- [ ] `git status --short` 乾淨；若需檢查 ignored files，另跑 `git status --short --ignored` 並確認只出現預期本地檔案。

**審查門檻：**
- [ ] 開始 Phase 5 前人工 review Twitch adapter boundary、OAuth/token handling、display cache ownership、MemberModule dependency direction、Overlay DTO whitelist。

**依賴：** Task 13d

**可能涉及的檔案：**
- `docs/phases/phase-4-twitch-member/todo.md`
- `tasks/todo.md`

**預估規模：** S

---

## 第四階段檢查點

**驗收準則：**
- [ ] `dotnet test` -> SC-1、SC-6a、SC-6b、SC-8 通過。
- [ ] Twitch IRC mock -> `UserSentMessageEvent` -> MemberRecord 建立。
- [ ] Overlay DTO 精確白名單正確。
- [ ] OAuth PKCE state/callback/token boundary tests 通過。
- [ ] DisplayHints segment allowlist 與 color/badge normalization tests 通過。
- [ ] Adapter cache update idempotency tests 通過。
- [ ] Phase 5 開始前完成 Phase 4 review。

---

## 風險與緩解

| 風險 | 影響 | 緩解措施 |
|------|------|----------|
| Task 12 過大 | 高 | 拆成 registration、parser、mapping、equivalence、reconnect、OAuth、cache update 七個切片。 |
| 真實 Twitch/OAuth 造成不可重現測試 | 高 | Phase 4 全部使用 mock payload、fake socket、mock token endpoint；真實登入留給後續手動驗證。 |
| OAuth secrets 洩漏到 logs/settings | 高 | OAuth tests 明確掃 logger sink，adapter 只持有 in-memory access token，refresh token 只交給 `IOAuthTokenStore`；authorization code、code verifier、raw refresh token 不進 log。 |
| DisplayHints XSS 邊界混亂 | 高 | segment type allowlist + exact DTO tests；text 值不當 HTML 渲染是前端責任，Phase 6 需再驗。 |
| MemberModule 越界存取 adapter cache | 中 | Architecture test 阻擋 Application/Domain 引用 `Vulperonex.Adapters.Abstractions` / `IPlatformUserInfoCache`。 |
| SC-6b false positive | 中 | Simulation/Twitch equivalence tests 必須使用兩個 fresh SQLite fixtures。 |

## 開放問題

- Task 12f 是否需要真實 HTTP listener implementation，還是先以 listener abstraction + unit tests 驗證 single-use/loopback/path/port 行為。建議先做 abstraction，避免 Phase 4 卡在 OS socket 細節。
- Overlay DTO 是否只放 Application，還是 Phase 5 SignalR hub 需要 host-level DTO。建議 Phase 4 先放 Application contract，Phase 5 hub 只序列化該 DTO。
- Phase 5 多 overlay client 場景需重評 synthetic `eventId` 去重語意：platform-provided id 可跨 client 識別同一事件；adapter fallback ULID 只保證本機單實例 delivery id。
