# 第 5.5 階段待辦事項 - Rapid-Test Enablement

> 計畫：`docs/phases/phase-5_5-rapid-test/plan.md`
> 父核對清單：`tasks/todo.md`

---

## 任務 17a - CLI rule create / update

- [x] 任務 17a-1：`RuleCommand` 加 `create <file>` 子命令；讀檔 → POST `/api/rules`；透傳後端 error code。
- [x] 任務 17a-2：`RuleCommand` 加 `update <id> <file>` 子命令；讀檔 → PUT `/api/rules/{id}`；處理 204 No Content。
- [x] 任務 17a-3：CLI 端錯誤碼定案：檔不存在 → `FILE_NOT_FOUND`（或借用既有碼，於切片落地時定）。
- [x] 任務 17a-4：Integration test 涵蓋 method/path/body 透傳與後端 4xx error code 透傳。
- [x] 任務 17a-5：`cli-e2e-verification.md` 表格擴充 create/update 命令的 PASS 條件。

## 任務 17b - chat.html overlay 移植

- [x] 任務 17b-1：拷貝/重寫 `chat.html` 至 `src/Hosts/Vulperonex.Web/wwwroot/overlay/chat.html`，移除 Omni-Commander 專屬 cache-bust 與 emote URL 假設。
- [x] 任務 17b-2：移植 `chat.css` / `chat.js` / `overlay-common.js` 至 `wwwroot/overlay/css|js/`。
- [x] 任務 17b-3：SignalR 用戶端版本對齊後端（`@microsoft/signalr@10.x`），不引入打包流程。
- [x] 任務 17b-4：架構測試 `Vulperonex.Tests.Architecture/Overlay/ChatHtmlPayloadKeysTest.cs`，斷言 `chat.js` 引用的 payload 欄位 ⊆ `OverlayChatPayload` 公開屬性集合。
- [x] 任務 17b-5：手動驗證：OBS 瀏覽器源開 `/overlay/chat` 後執行 `simulate chat`，觀察到 segment 渲染。記錄於 `manual-verification.md`。

## 任務 17c - E2E fixture chat → workflow → overlay

- [x] 任務 17c-1：`docs/phases/phase-5_5-rapid-test/examples/rule-chat-echo.json` 寫一條最小 chat→SendChatMessage 規則。
- [x] 任務 17c-2：`tests/Vulperonex.Tests.Integration/RapidTest/ChatReplyChainTests.cs`：以共用 Web fixture 灌入規則 → `POST /api/simulate/chat` → SignalR client 5 秒內收到 payload。
- [x] 任務 17c-3：測試斷言 `SendChatMessageActionExecutor` 被觸發（記數或 Test Double）。
- [x] 任務 17c-4：不打 Twitch IRC 出口；僅驗 domain → SignalR 鏈完整。

## 任務 17e - CLI ID 解析 + 缺 arg UX + 破壞性操作確認

> 設計凍結：`docs/phases/phase-5_5-rapid-test/cli-id-resolution-decision.md`

- [x] 任務 17e-1：新增 error codes `MISSING_ARGS` / `AMBIGUOUS_ID` / `NOT_FOUND` / `CONFIRMATION_REQUIRED` / `CANCELLED`；i18n catalog 追加 missing-args / confirm 文案 keys。
- [x] 任務 17e-2：`RuleIdentifierResolver` / `MemberIdentifierResolver` 支援完整 id / prefix；`rule` 群組另支援 `--name`。多重命中印候選表（≤10）走 `AMBIGUOUS_ID`，零命中走 `NOT_FOUND`。
- [x] 任務 17e-3：`CliExecutionContext` 加 `ConfirmAsync(messageKey, summaryLines, hasYesFlag)`：互動模式從 `Input` 讀 `[y/N]`，非互動需 `--yes`，否則寫 `CONFIRMATION_REQUIRED` + 摘要 + hint。
- [x] 任務 17e-4：`RuleCommand` `disable` / `enable` / `delete` / `show` / `update` 與 `MemberCommand` `show` / `delete` 改走 resolver；缺 arg 印 usage + hint，exit `MISSING_ARGS`。
- [x] 任務 17e-5：破壞性子命令（`rule disable` / `rule delete` / `member delete`）統一走 `ConfirmAsync`，接受 `--yes` / `-y`。
- [x] 任務 17e-6：整合測試覆蓋缺 arg / 完整 id / prefix 唯一 / prefix 多重 / prefix 零命中 / name exact / `--name` + positional 互斥 / `--yes` / 無 `--yes` 非互動 / 互動 y / 互動 n。
- [x] 任務 17e-7：`cli-e2e-verification.md` 表格擴充 prefix / `--name` / `--yes` / `[y/N]` 路徑 PASS 條件。
- [x] 任務 17e-8：SPEC §4.12 / §5 / §10 (D6a) 補上 `--name` / `--yes` 旗標與新 error codes 引用本文件。

## 任務 17d - Cookbook 文件

- [x] 任務 17d-1：`cookbook-chat-reply.md` 逐步章節（起 host → CLI rule create → simulate chat → 瀏覽器觀察 → 清理）。
- [x] 任務 17d-2：每章節含「觀察點 / 通過條件」表格。
- [x] 任務 17d-3：由非作者（含 AI agent）跑一輪並回填觀察值；至少一次 PASS 紀錄。

## 檢查點 5.5

- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 0 警告通過。
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過，含 17c 新 fixture。
- [x] CLI `rule create` / `rule update` 整合測試成功與 4xx 透傳兩條路徑皆綠。
- [x] 任務 17e 全測試路徑綠（缺 arg / prefix / `--name` / `--yes` / 互動 prompt）。
- [x] `chat.html` 架構測試通過。
- [x] Cookbook 至少一次外部 PASS 記錄。
- [x] `tasks/todo.md` 同步更新。
- [x] 與 Phase 6 spec 對齊：簽到/loyalty action 不在 5.5 範圍，已於 Phase 6 plan 明示繼承 5.5 fixture pattern。

## Phase 5.5 相依項目

- [x] Phase 5 任務 16f L46 / L60 人工驗證完成（5.5 cookbook 會引用 Phase 5 §1 起 host 流程，必須先確認該流程在本機可重現）。

## Phase 6 解鎖條件

- [x] 5.5 檢查點全綠。
- [x] Cookbook PASS 紀錄存在。
- [x] Phase 6 plan 明確接續 5.5 的 fixture / overlay 資產目錄結構。
