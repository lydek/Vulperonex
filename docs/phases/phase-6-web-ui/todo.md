# 第 6 階段待辦清單：Web UI + 日誌 + Desktop Shell

> 詳細計畫：`docs/phases/phase-6-web-ui/plan.md`
> 父待辦清單：`tasks/todo.md`

---

## Task 18 - Serilog + AppLogs

- [ ] Task 18a：設定 Console、rolling file、SQLite AppLogs sink。
- [ ] Task 18b：加入 EventTypeKey、Platform、MemberId、WorkflowRuleId、ActionType 結構化欄位。
- [ ] Task 18c：實作 `log.min_level` 熱重載。
- [ ] Task 18d：實作 AppLogs retention/size cleanup worker，size cleanup 後執行 `VACUUM`。
- [ ] Task 18e：補齊 logging integration tests。

## Task 19 - Vue 前端骨架

- [ ] Task 19a：建立 `src/frontend` package、Vite、Vue、TypeScript、test/build/lint scripts。
- [ ] Task 19b：建立 router、Pinia、layout shell、API client 與 `VITE_API_URL` override。
- [ ] Task 19c：建立 vue-i18n manifest + `zh-TW` / `en-US` 語系檔，缺 key 顯示 key。
- [ ] Task 19d：建立 dashboard status cards：API health、Twitch auth status、no-Twitch mode。
- [ ] Task 19e：建立 `useStreamEvents` composable 與 `/hubs/events` envelope state。
- [ ] Task 19f：建立 `/overlay/chat`、`/overlay/alerts`、`/overlay/member` route skeleton。
- [ ] Task 19g：補齊 frontend foundation tests 與 browser smoke。

## Task 20 - 管理 Console

- [ ] Task 20a：Simulate panel 支援 chat/follow/sub，成功後顯示 ack，不靜默。
- [ ] Task 20b：Event monitor 顯示 SignalR envelope 與最近事件列表。
- [ ] Task 20c：Member panel 支援 list/show/seed/delete 與成功/錯誤狀態。
- [ ] Task 20d：Rule panel 支援 list/show，並顯示 enabled/version/priority/createdAt。
- [ ] Task 20e：Rule enable/disable/delete 支援確認 dialog、409 conflict 與成功後狀態更新。
- [ ] Task 20f：Rule create/update 支援 JSON file/manual JSON textarea 與 validation error display。
- [ ] Task 20g：Twitch auth panel 支援 status/start/reset；缺 ClientId 時顯示 no-Twitch mode。
- [ ] Task 20h：補齊 MVP error code i18n coverage test。
- [ ] Task 20i：完成 browser manual verification，覆蓋 simulate/member/rule/Twitch flows。

## Task 21 - Photino Desktop Shell

- [ ] Task 21a：Desktop host 啟動 Web host 並載入 Vue UI。
- [ ] Task 21b：整合 port pair allocation，任一 port 被占用時切到下一組 pair。
- [ ] Task 21c：WebView2 缺失 dialog。
- [ ] Task 21d：Migration failure dialog，包含 Open log folder / Exit。
- [ ] Task 21e：Web host crash fallback HTML + Restart。
- [ ] Task 21f：補齊 Desktop shell unit tests 與 manual smoke。

## Phase 6 Checkpoint

- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm test`
- [ ] `cd src/frontend; pnpm build`
- [ ] `cd src/frontend; pnpm lint`（dependency 已存在時）
- [ ] Browser manual：Web UI dashboard 載入且 API/Twitch 狀態正確。
- [ ] Browser manual：simulate chat/follow/sub 顯示 ack 並推送事件。
- [ ] Browser manual：member list/show/seed/delete 可操作。
- [ ] Browser manual：rule create/show/enable/disable/delete 可操作。
- [ ] Browser manual：Twitch auth no-Twitch mode、reset、start 可操作。
- [ ] Desktop manual：Photino shell 載入 Web UI 並完成核心 smoke。
- [ ] `docs/phases/phase-6-web-ui/manual-verification.md` 記錄人工驗證結果。
- [ ] Git 暫存集限於 Phase 6 任務範圍；Phase 5.5/CLI resolver 既有 dirty diff 不混入。
