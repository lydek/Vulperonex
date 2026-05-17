# 第 5.5 階段待辦事項 - Rapid-Test Enablement

> 計畫：`docs/phases/phase-5_5-rapid-test/plan.md`
> 父核對清單：`tasks/todo.md`

---

## 任務 17a - CLI rule create / update

- [ ] 任務 17a-1：`RuleCommand` 加 `create <file>` 子命令；讀檔 → POST `/api/rules`；透傳後端 error code。
- [ ] 任務 17a-2：`RuleCommand` 加 `update <id> <file>` 子命令；讀檔 → PUT `/api/rules/{id}`；處理 204 No Content。
- [ ] 任務 17a-3：CLI 端錯誤碼定案：檔不存在 → `FILE_NOT_FOUND`（或借用既有碼，於切片落地時定）。
- [ ] 任務 17a-4：Integration test 涵蓋 method/path/body 透傳與後端 4xx error code 透傳。
- [ ] 任務 17a-5：`cli-e2e-verification.md` 表格擴充 create/update 命令的 PASS 條件。

## 任務 17b - chat.html overlay 移植

- [ ] 任務 17b-1：拷貝/重寫 `chat.html` 至 `src/Hosts/Vulperonex.Web/wwwroot/overlay/chat.html`，移除 Omni-Commander 專屬 cache-bust 與 emote URL 假設。
- [ ] 任務 17b-2：移植 `chat.css` / `chat.js` / `overlay-common.js` 至 `wwwroot/overlay/css|js/`。
- [ ] 任務 17b-3：SignalR 用戶端版本對齊後端（`@microsoft/signalr@10.x`），不引入打包流程。
- [ ] 任務 17b-4：架構測試 `Vulperonex.Tests.Architecture/Overlay/ChatHtmlPayloadKeysTest.cs`，斷言 `chat.js` 引用的 payload 欄位 ⊆ `OverlayChatPayload` 公開屬性集合。
- [ ] 任務 17b-5：手動驗證：OBS 瀏覽器源開 `/overlay/chat` 後執行 `simulate chat`，觀察到 segment 渲染。記錄於 `manual-verification.md`。

## 任務 17c - E2E fixture chat → workflow → overlay

- [ ] 任務 17c-1：`docs/phases/phase-5_5-rapid-test/examples/rule-chat-echo.json` 寫一條最小 chat→SendChatMessage 規則。
- [ ] 任務 17c-2：`tests/Vulperonex.Tests.Integration/RapidTest/ChatReplyChainTests.cs`：以共用 Web fixture 灌入規則 → `POST /api/simulate/chat` → SignalR client 5 秒內收到 payload。
- [ ] 任務 17c-3：測試斷言 `SendChatMessageActionExecutor` 被觸發（記數或 Test Double）。
- [ ] 任務 17c-4：不打 Twitch IRC 出口；僅驗 domain → SignalR 鏈完整。

## 任務 17d - Cookbook 文件

- [ ] 任務 17d-1：`cookbook-chat-reply.md` 逐步章節（起 host → CLI rule create → simulate chat → 瀏覽器觀察 → 清理）。
- [ ] 任務 17d-2：每章節含「觀察點 / 通過條件」表格。
- [ ] 任務 17d-3：由非作者（含 AI agent）跑一輪並回填觀察值；至少一次 PASS 紀錄。

## 檢查點 5.5

- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 0 警告通過。
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過，含 17c 新 fixture。
- [ ] CLI `rule create` / `rule update` 整合測試成功與 4xx 透傳兩條路徑皆綠。
- [ ] `chat.html` 架構測試通過。
- [ ] Cookbook 至少一次外部 PASS 記錄。
- [ ] `tasks/todo.md` 同步更新。
- [ ] 與 Phase 6 spec 對齊：簽到/loyalty action 不在 5.5 範圍，已於 Phase 6 plan 明示繼承 5.5 fixture pattern。

## Phase 5.5 相依項目

- [ ] Phase 5 任務 16f L46 / L60 人工驗證完成（5.5 cookbook 會引用 Phase 5 §1 起 host 流程，必須先確認該流程在本機可重現）。

## Phase 6 解鎖條件

- [ ] 5.5 檢查點全綠。
- [ ] Cookbook PASS 紀錄存在。
- [ ] Phase 6 plan 明確接續 5.5 的 fixture / overlay 資產目錄結構。
