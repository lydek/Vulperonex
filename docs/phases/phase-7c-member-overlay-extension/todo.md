# Phase 7C 待辦：Member Card Overlay、Custom HTML Extension、Member-in-Chat

> 對應規劃：`docs/phases/phase-7c-member-overlay-extension/plan.md`
> 父層待辦：`tasks/todo.md`

## Task 44 - Member Card Overlay Default Preset (Rotan-Checkin)

- [ ] Task 44a：實作 `MemberOverlayView.vue` 渲染完整集點卡（已落地，含進場動畫 + 滿章金光）
- [ ] Task 44b：SignalR `member` hub event-driven 顯示，7s 自動收起 + queue 機制
- [ ] Task 44c：deterministic stamp 位置/旋轉/縮放 hash 邏輯，抽出 `utils/deterministicRandom.ts` 共用
- [ ] Task 44d：CSS base + theme token 架構 refactor (`member-card.css` + `member-card-twitch.css` 縮 93%)
- [ ] Task 44e：動畫 keyframe 使用 `var(--mc-*)` 解析顏色，零重複
- [ ] Task 44f：standalone `wwwroot/overlay/member-card.html` 同視覺
- [ ] Task 44g：移除 inline 重複 `getDeterministicRandom` 複製（Vue + JS 各保留一份 canonical）

## Task 45 - Member Card Admin Controller

- [ ] Task 45a：SystemSettingKey `overlay.member.background_url`、`overlay.member.stamp_url` 註冊
- [ ] Task 45b：`/api/config` AllowedKeys 白名單收納
- [ ] Task 45c：MembersView 「集點卡視覺配置」面板（i18n 化）
- [ ] Task 45d：URL sanitize：scheme allowlist + 字元黑名單 + `cssUrl()` helper
- [ ] Task 45e：`MemberOverlayView` setInterval lifecycle (onUnmounted clearInterval)
- [ ] Task 45f：i18n zh-TW + en-US locale keys 補齊
- [ ] Task 45g：Vitest 覆蓋 sanitize 邏輯 (3 cases: valid http/data、invalid scheme、含跳脫字元)
- [ ] Task 45h：設定變更改 SignalR `system.config_changed` push (取代 10s polling) — polish，可延後

## Task 46 - Custom HTML Overlay Upload Infrastructure

- [ ] Task 46a：`POST /api/overlay/custom-presets` multipart endpoint
- [ ] Task 46b：Slug sanitize regex + 長度上限 64
- [ ] Task 46c：Zip 解壓縮 path traversal 防護（逐 entry 絕對路徑驗證）
- [ ] Task 46d：5MB 大小上限（`RequestSizeLimitAttribute` + multipart limit）
- [ ] Task 46e：Loopback-only binding enforcement
- [ ] Task 46f：`DELETE /api/overlay/custom-presets/{slug}` endpoint
- [ ] Task 46g：`GET /api/overlay/custom-presets` 列表 endpoint（含 slug/size/uploadedAt，不曝光絕對路徑）
- [ ] Task 46h：Admin UI `/admin/overlay-presets` 頁面（列表 + 上傳 + 刪除 dialog）
- [ ] Task 46i：i18n zh-TW + en-US 雙語
- [ ] Task 46j：Integration test 涵蓋 path traversal、超大檔 413、無效 slug 400、loopback-only
- [ ] Task 46k：反射測試 endpoint DTO 不曝光 server-internal path

## Task 47 - Overlay Preset Resolver Backend Route

- [ ] Task 47a：SystemSettingKey `overlay.chat.preset`、`overlay.member.preset`、`overlay.alerts.preset`
- [ ] Task 47b：AllowedKeys 白名單收納
- [ ] Task 47c：Resolver endpoint `GET /overlay/{hub}` (讀 setting + 回 Vue SPA 或 302 redirect)
- [ ] Task 47d：Unknown preset key fallback 預設 + structured log warning
- [ ] Task 47e：`GET /api/overlay/presets` 列表（內建 hardcoded + custom filesystem scan）
- [ ] Task 47f：Admin UI 「Overlay Preset」設定區塊 (chat/member/alerts dropdown)
- [ ] Task 47g：Integration test 涵蓋每組 (hub, preset key) routing
- [ ] Task 47h：302 redirect 保留 query string

## Task 48 - Member Snapshot in Chat Hub (Cross-Hub Embed)

- [ ] Task 48a：SystemSettingKey `overlay.chat.show_member_card` (bool, default false)
- [ ] Task 48b：`OverlayChatEvent` DTO 加可選 `memberSnapshot`
- [ ] Task 48c：`MemberSnapshotDto` 欄位精確等同 member hub 白名單
- [ ] Task 48d：反射測試斷言 chat hub payload 含且僅含預期欄位
- [ ] Task 48e：`OverlayModule` chat event 處理路徑查 member cache 並附 snapshot
- [ ] Task 48f：`PlatformUserDisplayCache` (或新 cache) 覆蓋 member snapshot 查詢
- [ ] Task 48g：新 preset `ChatPresetMemberCardEmbed` 或既有 preset 加旗標渲染 chip
- [ ] Task 48h：standalone HTML chat.html chip 渲染支援
- [ ] Task 48i：Vitest 覆蓋 chip 渲染 / show_member_card=false 隱藏 / 非會員 graceful skip
- [ ] Task 48j：效能驗證：1000 events 連續 burst 下 chat hub latency 無顯著退化

## Task 49 - OneComme Bridge Plugin Contract (Scaffold Only)

- [ ] Task 49a：新增 `src/Plugins/Vulperonex.Plugins.OneCommeBridge/` 空 project + csproj
- [ ] Task 49b：`IOverlayTemplateImporter` interface 定義於 `Vulperonex.Application.Overlay.Extensions`
- [ ] Task 49c：`ImportResult` record (success + warnings list)
- [ ] Task 49d：`docs/plugins/onecomme-bridge.md` OneComme 變數對照表骨架
- [ ] Task 49e：Solution 註冊 + build 通過（plugin 內無實作）
- [ ] Task 49f：CONTRIBUTING.md 加入 plugin 開發指引段落

---

## Checkpoint：Phase 7C

- [ ] 全部 Task 44-49 sub-task `[x]` 完成自檢
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual：上傳 HTML 樣板 + 切 preset + OBS 載入驗證
- [ ] Browser manual：簽到事件 + 自訂背景/印章 URL 生效
- [ ] Browser manual：member card chip 在 chat overlay 顯示/隱藏切換
- [ ] Security review：
  - [ ] Path traversal zip 攻擊測試 PASS
  - [ ] CSS url() injection 測試 PASS
  - [ ] Member snapshot 反射白名單 PASS
  - [ ] Upload endpoint loopback-only 確認
  - [ ] Upload 檔案大小限制 enforce
- [ ] `manual-verification.md` 記錄所有 PASS/FAIL + dated entries + evidence commits

---

## Retroactive Notes（事後補建立）

此 phase doc 為事後補。Task 44 + 45 大部分子項已在無 spec 狀況下實作，後續以 Stage 1+2 review 補齊：
- SPEC §4.14.1 Overlay Preset Contract 章節
- CSS base+token refactor
- `cssUrl()` URL sanitize helper
- setInterval lifecycle fix
- `getDeterministicRandom` 統合
- i18n 補完

Task 46-49 為新 scope，待 ACK 後開工 Stage 3。
