# 第四階段待辦清單：Twitch Adapter + MemberModule

> 詳細計畫：`docs/phases/phase-4-twitch-member/plan.md`
> 父待辦清單：`tasks/todo.md`

---

## 任務 12：Twitch Adapter

- [ ] 任務 12a：建立 `TwitchAdapter` lifecycle 與 event type registration
- [ ] 任務 12b：實作 IRC chat parser 與安全 DisplayHints
- [ ] 任務 12c：實作 seven MVP Twitch payload -> domain event mapping
- [ ] 任務 12d：完成 SimulationAdapter/TwitchAdapter WorkflowEngine 副作用等價（SC-6a）
- [ ] 任務 12e：實作 connection state events 與 reconnect backoff
- [ ] 任務 12f：實作 OAuth PKCE callback、state 驗證與 token refresh boundary
- [ ] 任務 12g：實作 adapter-owned display cache updates 與 idempotency

## 任務 13：MemberModule + Overlay DTO

- [ ] 任務 13a：實作 `MemberModule` event subscription 與 member resolution（SC-8 起點）
- [ ] 任務 13b：實作 subscription/follow 等 member state updates
- [ ] 任務 13c：完成 Simulation/Twitch MemberRecord DB state 等價（SC-6b）
- [ ] 任務 13d：建立 Overlay DTO exact whitelist contracts
- [ ] 任務 13e：完成 Phase 4 checkpoint review

## 第四階段檢查點

- [ ] 全方案編譯通過，0 warnings
- [ ] 全方案測試通過
- [ ] SC-1：mock Twitch payload -> seven MVP `IStreamEvent` 全部產生
- [ ] SC-6a：SimulationAdapter 與 TwitchAdapter mock IRC 觸發相同 WorkflowEngine 副作用
- [ ] SC-6b：SimulationAdapter 與 TwitchAdapter mock IRC 產生相同 MemberRecord DB state
- [ ] SC-8：publish `UserSentMessageEvent` -> PlatformIdentity 建立，MemberId 為 ULID
- [ ] Twitch IRC mock -> `UserSentMessageEvent` -> MemberRecord 建立通過
- [ ] `platform.connection_changed` 在 registry 中 `IsKnown=true` 且 `IsKnownForWorkflow=false`
- [ ] `TwitchAdapter.StartAsync` double-start idempotent，不重複註冊 event keys 或開第二組 socket
- [ ] reconnect exponential backoff 含 ±20% jitter；EventSub duplicate delivery 以 `(platform, sourceEventId)` dedup cache 處理（1000 entries 或 10 分鐘 TTL）
- [ ] OAuth PKCE `state` 為 32-byte random Base64Url；10 分鐘 TTL；single-use；`code_verifier` / `code_challenge` / token exchange verifier tests 通過
- [ ] OAuth PKCE `state` 不符、過期或已使用 -> 拒絕且不 exchange code
- [ ] OAuth callback listener 僅接受 `127.0.0.1` / `::1` remote IP 且 Host header allowlist 同時通過、固定 `/auth/callback` path、single-use、port conflict tests 通過
- [ ] access token in-memory only；refresh token 只透過 `IOAuthTokenStore` 儲存；logs 不含 access token、authorization code、code verifier、raw refresh token
- [ ] DisplayHints segment type allowlist、6-digit color format、badge ID/value normalization、`user.avatar`、`user.is_subscriber`、`user.bits_total` tests 通過
- [ ] Adapter display cache update 使用 monotonic absolute replacement，TDQ replay 不重複累加，out-of-order bits 不回退
- [ ] `MemberModule` 不引用 `Vulperonex.Adapters.Abstractions` 或 `IPlatformUserInfoCache`
- [ ] Member state replay 使用 `(platform, sourceEventId)` dedup key；TDQ replay 不造成重複 row 或重複累加
- [ ] Overlay DTO `System.Text.Json` key set exact whitelist 通過；chat/alert payload 含 `schemaVersion`、platform-provided 優先的 public `eventId`、`timestamp` metadata；member payload 為 snapshot，只含 `schemaVersion`
- [ ] Overlay alert/member payload acceptance 覆蓋：alert `{schemaVersion,eventId,timestamp,displayName,eventType,tier}`；member `{schemaVersion,displayName,avatarUrl,checkInCount}`
- [ ] SignalR serialization exact key-set 驗證保留到 Task 15，且使用同一 DTO contract
- [ ] Domain/Application 無 Twitch symbols 持續通過
- [ ] Domain coverage gate >90% 通過
- [ ] Application coverage gate >80% 通過
- [ ] Git 狀態乾淨（忽略的本地檔案除外）
- [ ] 第五階段開始前完成第四階段審查
