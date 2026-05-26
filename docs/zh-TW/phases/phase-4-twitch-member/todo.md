# 第四階段待辦事項 - Twitch Adapter + MemberModule

> 計畫：`docs/phases/phase-4-twitch-member/plan.md`
> 父核對清單：`tasks/todo.md`

---

## 任務 12 - Twitch Adapter

- [x] 任務 12a：實作 `TwitchAdapter` 生命週期與事件類型註冊。
- [x] 任務 12b：實作 IRC 聊天解析器與顯示提示 (DisplayHints) 正規化。
- [x] 任務 12c：實作七個 MVP Twitch 負載 -> 網域事件對應。
- [x] 任務 12d：針對 SC-6a 證明 SimulationAdapter/TwitchAdapter WorkflowEngine 等效性。
- [x] 任務 12e：實作連線狀態支援原語、重連退避 (backoff)，以及 EventSub 重複刪除快取。
- [x] 任務 12f：實作 OAuth PKCE 回呼/狀態/權杖重新整理邊界。
- [x] 任務 12g：實作由 adapter 擁有的顯示快取更新與等冪的絕對值取代。

## 任務 13 - MemberModule + Overlay DTO

- [x] 任務 13a：針對 SC-8 實作 `MemberModule` 事件訂閱與成員解析。
- [x] 任務 13b：實作訂閱/追隨串流狀態更新。
- [x] 任務 13c：針對 SC-6b 證明模擬/Twitch MemberRecord 資料庫狀態等效性。
- [x] 任務 13d：實作 Overlay DTO 精確白名單合約。
- [x] 任務 13e：完成第四階段檢查點審查。

## 任務 13 後續積壓工作

- [x] 任務 13f：強化 SC-6a/SC-6b 等效性，包含追隨/訂閱/贊助負載，以及對快取狀態、成員狀態、`TotalBitsGiven` 與訂閱者層級的斷言。

## 檢查點 4

- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 以 0 警告通過。
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過。
- [x] SC-1：mock Twitch 負載 -> 七個 MVP `IStreamEvent` 具體對應通過。
- [x] SC-6a：SimulationAdapter 與 TwitchAdapter mock 訊息負載產生匹配的 WorkflowEngine 聊天副作用。
- [x] SC-6b：SimulationAdapter 與 TwitchAdapter mock 訊息負載產生匹配的 MemberRecord 資料庫狀態。
- [x] SC-8：發布 `UserSentMessageEvent` 解析出 `PlatformIdentity` 並透過解析器建立 ULID 成員 ID。
- [x] Twitch IRC mock -> `UserSentMessageEvent` -> MemberRecord 建立通過。
- [x] `platform.connection_changed` 註冊行為為 `IsKnown=true` 且 `IsKnownForWorkflow=false`。
- [x] `TwitchAdapter.StartAsync` 重複啟動具等冪性。
- [x] 重連指數退避為 1s -> 2s -> 4s，抖動限制在 +/-20%，且上限為 60s。
- [x] EventSub 重複交付使用具有 1000 筆容量與 10 分鐘 TTL 的 `(platform, sourceEventId)` 重複刪除快取。
- [x] OAuth PKCE `state` 為 32 位元組隨機 Base64Url，10 分鐘 TTL，單次使用；`code_verifier` / `code_challenge` 測試通過。
- [x] OAuth PKCE `state` 不符會阻止回呼驗證，進而阻止權杖交換。
- [x] OAuth 回呼驗證接受 loopback 遠端 IP、Host 標頭允許清單、固定 `/auth/callback` 路徑以及單次使用狀態。
- [x] OAuth 回呼連接埠退避使用 7979 -> 7980 -> 7981，並在全部不可用時回報使用者端錯誤。
- [x] 存取權杖保留在記憶體中；重新整理權杖僅交給 `IOAuthTokenStore`；啟動時解密失敗要求重新授權而不當機。
- [x] DisplayHints 區段類型允許清單、六位數顏色格式、勳章 ID/值正規化、`user.avatar`、`user.is_subscriber` 與 `user.bits_total` 測試通過。
- [x] Adapter 顯示快取更新對累計 bits 使用單調絕對值取代，且對於順序錯亂的值具備重播安全性。
- [x] `MemberModule` 不引用 `Vulperonex.Adapters.Abstractions` 或 `IPlatformUserInfoCache`。
- [x] 成員狀態重播使用 `(platform, sourceEventId)` 重複刪除行為，且不重複成員身分。
- [x] Overlay DTO `System.Text.Json` 金鑰集精確白名單測試通過。
- [x] Overlay 提醒/成員負載驗收通過：
  - 提醒 `{schemaVersion,eventId,timestamp,displayName,eventType,tier}`
  - 成員 `{schemaVersion,displayName,avatarUrl,checkInCount}`
- [x] SignalR 序列化精確金鑰集驗證仍保留於任務 15；第四階段僅擁有 DTO 合約測試。
- [x] 網域/應用程式 Twitch 符號洩漏測試通過。
- [x] 網域涵蓋率閘口 >90% 通過。
- [x] 應用程式涵蓋率閘口 >80% 通過。
- [x] Git 暫存集限於任務範圍；無關的 `docs/design/` 保持未追蹤且不提交。
- [x] 第四階段已準備好進行第五階段計畫/審查移交。
