# Phase 7C 實作計畫：Member Card Overlay、Custom HTML Extension、Member-in-Chat

> 父層計畫：`tasks/plan.md`
> 父層待辦：`tasks/todo.md`
> 對照來源：`docs/SPEC.md` §4.14.1 Overlay Preset Contract、`ref/Omni-Commander/OmniCommander.WebApi/wwwroot/member-card.html` (視覺啟發，不直接複用)
> 前置條件：Phase 7B chat overlay preset contract 已完成；Phase 6 Web admin UI + SystemSettingsService 可用。
> 目標：補齊 member card overlay 預設樣式 + admin controller、定義自訂 HTML overlay 上傳擴充機制、提供 chat overlay 內嵌會員卡資料的可選 cross-hub 渲染路徑。
> 邊界：OneComme 樣板匯入 plugin 為獨立 slice（Phase 7D），此 phase 僅落地 contract + 上傳基礎設施。
> 進度來源：本文件 checkbox 為設計/驗收草案；實際完成狀態以 `todo.md` 為準。

---

## 目標

Phase 7B 已建立 chat overlay preset contract。Phase 7C 補齊以下缺口：

1. **Member card overlay 缺乏 first-class 設計**：Phase 6 僅留下 `/overlay/member` 骨架。MVP 後使用者需要 OBS 集點卡視覺 + 可調整背景/印章/簽到次數。
2. **無自訂 HTML overlay 擴充路徑**：Phase 7B 雖開放 Vue preset 切換，但仍要求使用者懂前端編譯。一般實況主想直接拖 `.html` 進 OBS，無 Vite/pnpm 相依。
3. **Member-card 與 chat overlay 無交集**：使用者期望「會員留言時 chat 旁邊可顯示會員卡 chip」，目前 chat hub 與 member hub 完全獨立。
4. **OneComme 樣板生態無接點**：Phase 7B 文件已說明 plugin 路徑，但實際 plugin scaffold 與 importer contract 未落地。

Phase 7C 處理 (1)(2)(3) + (4) 的 contract 部分。實際 OneComme bridge plugin 實作延後到 Phase 7D。

---

## 範圍

### 內含

- Member card overlay Vue 預設 preset (Rotan-checkin 風格重寫，不引用原始版權資產)
- Member card admin controller：簽到次數顯示、卡片背景 URL、印章 URL 設定
- Member card 主題 token 機制（base CSS + theme override 已於前置工作完成）
- Custom HTML overlay 上傳機制：multipart endpoint + zip 解壓縮 + path traversal 防護
- `wwwroot/overlay/custom/{slug}/` 託管 + admin upload UI
- Overlay preset resolver：`overlay.{hub}.preset` 設定支援 `custom:{slug}` 解析
- Chat hub DTO 擴充 `memberSnapshot` 可選欄位 + 反射白名單測試
- `overlay.chat.show_member_card` 旗標控制 chat preset 是否渲染 member chip
- OneComme bridge plugin contract (interface + scaffold only，實作延後)

### 不含

- OneComme bridge plugin 完整實作 (Phase 7D)
- Alerts overlay 樣板化 (留 Phase 7E 或更後)
- 第三方樣板 marketplace
- 樣板熱重載 / file watcher (留後續 polish)
- 上傳檔案執行任意 server-side script
- 雲端樣板同步

---

## 任務分解

## Task 44 - Member Card Overlay Default Preset (Rotan-Checkin)

**描述：** `/overlay/member` 預設視覺從骨架升級為 first-class 集點卡 preset，視覺啟發自 Rotan checkin overlay 但完全重寫（無原始檔案複用）。10 格集章 grid、紫金燙金邊框、爪印 SVG（內聯）、頭像 + 名稱 + VIP 徽章。

**驗收標準：**
- [x] `MemberOverlayView.vue` 渲染完整集點卡，含進場動畫、滿章金光特效
- [x] 卡片在 SignalR `member` hub 收到 event 時觸發顯示，7s 後自動收起
- [x] 集章數依 `checkInCount % stampsPerRound` 決定本輪格數，自動進位下一輪
- [x] CSS base + theme token 架構：`member-card.css` 結構層、`member-card-twitch.css` 純 :root override
- [x] 動畫 keyframe 使用 `var(--mc-*)` 在 box-shadow 中解析顏色，主題切換零重複 keyframe
- [x] 印章位置/旋轉/縮放透過 deterministic hash 由 (member, round, slot) 種子產生

**實作提示：**
- Vue preset 與 standalone HTML 共用同一 CSS。
- 不引用 menber_byRotan 任何原始檔案（避版權）。
- 紅金主題為 default，紫金主題作為 token override 範例。

## Task 45 - Member Card Admin Controller

**描述：** Admin UI `/admin/members` 加入「集點卡視覺配置」面板，允許設定卡片背景圖 URL、印章圖 URL。Settings 經 `ISystemSettingsService` 持久化到 SQLite。

**驗收標準：**
- [x] 新增 SystemSettingKey: `overlay.member.background_url`、`overlay.member.stamp_url`
- [x] `/api/config` AllowedKeys 白名單收納上述 key
- [x] MembersView 渲染設定面板，包含 URL 輸入 + 儲存按鈕 + success/error toast
- [x] 設定改變後 10 秒內 overlay 自動 polling 反映新值（已落地，需後續改 SignalR push）
- [x] i18n 雙語系完整 (zh-TW + en-US)
- [x] URL sanitize：scheme allowlist (`https?:` + `data:image/...`)，禁止 `()'"\;` 等 CSS url() 跳脫字元
- [ ] Vitest 覆蓋 sanitize 邏輯（pending — 待 Stage 3）

**實作提示：**
- URL 注入 CSS `url()` 必須引號跳脫 (`cssUrl()` helper)。
- `setInterval` 必須在 `onUnmounted` clearInterval（防記憶體洩漏）。
- 設定變更 push 改 SignalR `system.config_changed` event 為 polish，當前 10s polling 可接受。

## Task 46 - Custom HTML Overlay Upload Infrastructure

**描述：** Admin UI 提供 HTML/CSS/JS bundle 上傳介面，落地到 `wwwroot/overlay/custom/{slug}/`。Backend 提供 multipart endpoint + zip 解壓縮 + path traversal 防護。OBS 可直接載入 `http://localhost:{port}/overlay/custom/{slug}/index.html`。

**驗收標準：**
- [ ] `POST /api/overlay/custom-presets` multipart endpoint，接受單一 `.html` 或 `.zip`
- [ ] Slug sanitize：`[a-z0-9-]+`，禁止 `..`、絕對路徑、空字串、長度上限 64
- [ ] Zip 解壓縮逐檔 path traversal 驗證（解出絕對路徑必在目標目錄內，否則拒絕整包）
- [ ] 上傳檔案大小上限 5MB（整包）
- [ ] Loopback-only 強制（沿用 Phase 6 安全契約）
- [ ] `DELETE /api/overlay/custom-presets/{slug}` 移除自訂樣板
- [ ] `GET /api/overlay/custom-presets` 列出已安裝 slug + 大小 + 上傳時間
- [ ] Admin UI `/admin/overlay-presets` 新頁面：列表 + 上傳表單 + 刪除確認 dialog
- [ ] i18n 雙語系
- [ ] Integration test：path traversal 攻擊 zip 應被拒絕、超大檔應 413、無效 slug 應 400
- [ ] 反射測試：endpoint 回傳 DTO 排除 server-internal path

**實作提示：**
- 解壓縮使用 `System.IO.Compression.ZipArchive`，逐 entry 驗 `Path.GetFullPath(targetPath).StartsWith(Path.GetFullPath(rootDir))`。
- 不執行 server-side HTML sanitization（HTML overlay 預期含 script），但檔案僅由 loopback OBS 載入。
- 大小上限以 `RequestSizeLimitAttribute` + multipart `MultipartBodyLengthLimit` 雙層守。

## Task 47 - Overlay Preset Resolver Backend Route

**描述：** Backend 解析 `/overlay/{hub}` 請求，依 `overlay.{hub}.preset` 設定決定回應內容：
- 內建 preset key (`kapchat`/`compact`/`rotan-checkin`) → 回 Vue SPA route
- `custom:{slug}` → 302 redirect 到 `/overlay/custom/{slug}/index.html`
- 直接訪問 `/overlay/{hub}.html` 不經解析

**驗收標準：**
- [ ] 新增 SystemSettingKey: `overlay.chat.preset`、`overlay.member.preset`、`overlay.alerts.preset`
- [ ] AllowedKeys 白名單收納
- [ ] Resolver endpoint：`GET /overlay/{hub}` 讀設定，回 Vue SPA 或 302
- [ ] Preset key 驗證：unknown key fallback 預設並 log warning
- [ ] Integration test：每組 (hub, preset key) 組合正確 routing
- [ ] Admin UI 加入「Overlay Preset」設定 dropdown（chat/member/alerts 各一）
- [ ] 內建 preset 列表透過 `GET /api/overlay/presets` 端點曝光（含 custom slugs）

**實作提示：**
- Preset registry 同時涵蓋內建 (hardcoded) + custom (filesystem scan `wwwroot/overlay/custom/`)。
- 302 redirect 保留 query string 以支援 OBS 快取破壞 (`?t=timestamp`)。

## Task 48 - Member Snapshot in Chat Hub (Cross-Hub Embed)

**描述：** Chat hub `OverlayChatEvent` DTO 新增可選欄位 `memberSnapshot`，當使用者已在 member 系統時 backend 自動帶上。Chat preset 視 `overlay.chat.show_member_card` 旗標決定是否渲染 inline 會員卡 chip。

**驗收標準：**
- [ ] 新增 SystemSettingKey: `overlay.chat.show_member_card` (bool, 預設 false)
- [ ] `OverlayChatEvent` DTO 加入 `memberSnapshot?: MemberSnapshotDto`
- [ ] `MemberSnapshotDto` 欄位精確等同 member hub 白名單（exclude `memberId`/`totalLoyalty`/`linkedPlatforms`）
- [ ] 反射測試斷言 chat hub payload 含且僅含預期欄位
- [ ] `OverlayModule` 在 chat event 處理路徑查 member cache，附 snapshot
- [ ] ChatPresetDefault (KapChat) 不渲染 chip (極簡)
- [ ] 新內建 preset `ChatPresetMemberCardEmbed`（或既有 preset 加旗標）渲染 inline chip：頭像 + 簽到次數
- [ ] standalone HTML chat.html 同樣支援 (透過 `OverlayCommon` 共用 contract)
- [ ] Vitest 覆蓋 chip 渲染 + show_member_card=false 時隱藏

**實作提示：**
- Member cache 查詢需快取以免每筆 chat 多一次 DB hit。
- 非會員 chat 則 `memberSnapshot=null`，preset 應 graceful skip。
- Chip 視覺：32px 圓形頭像 + 集章次數 badge，不應遮擋 chat 內文。

## Task 49 - OneComme Bridge Plugin Contract (Scaffold Only)

**描述：** 為未來 OneComme template importer plugin 預留 contract 與 plugin scaffold。實際 importer 邏輯延後到 Phase 7D。

**驗收標準：**
- [ ] 新增 `src/Plugins/Vulperonex.Plugins.OneCommeBridge/` project（空 scaffold）
- [ ] 定義 `IOverlayTemplateImporter` interface 於 `Vulperonex.Application.Overlay.Extensions`
- [ ] Interface 方法：`Task<ImportResult> ImportAsync(Stream package, string targetSlug, CancellationToken ct)`
- [ ] 文件 `docs/plugins/onecomme-bridge.md` 寫 OneComme 變數對照表（`comment.name` → `displayName` 等）骨架
- [ ] Project 註冊於 solution，build 成功（即使 plugin 內無實作）
- [ ] CONTRIBUTING 文件加入 plugin 開發指引

**實作提示：**
- Contract 必須走 stream 而非 file path（plugin 可能接收網路上傳的 zip）。
- `ImportResult` 包含成功/失敗 + 警告清單（哪些 OneComme 變數無對應映射）。

---

## Checkpoint：Phase 7C

- [ ] 全部 Task 44-49 sub-task 達成驗收標準
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual：上傳一份 OneComme-style HTML 樣板 → 設 `overlay.chat.preset=custom:test` → `/overlay/chat` 顯示自訂樣板
- [ ] Browser manual：簽到事件觸發 → `/overlay/member` 顯示卡片，自訂背景/印章 URL 生效
- [ ] Browser manual：開啟 `overlay.chat.show_member_card` → 會員留言時 chat 旁顯示 chip
- [ ] Security review：
  - [ ] Path traversal zip 攻擊測試 PASS
  - [ ] CSS url() injection 測試 PASS
  - [ ] Member snapshot 反射白名單 PASS
  - [ ] Upload endpoint loopback-only binding 確認
  - [ ] Upload 檔案大小限制 enforce
- [ ] `docs/phases/phase-7c-member-overlay-extension/manual-verification.md` 記錄所有手動驗證 PASS/FAIL

---

## 風險與緩解

| 風險 | 影響 | 緩解 |
| --- | --- | --- |
| 自訂 HTML 上傳成 attack vector（path traversal、zip bomb） | 高 | 嚴格 slug sanitize + 逐 entry 路徑驗證 + 大小上限 + loopback-only |
| CSS `url()` 使用者設定值被注入跳脫 | 中 | scheme allowlist + 引號字元黑名單 + 字串引號跳脫 helper |
| Chat hub DTO 加 memberSnapshot 破壞既有 Phase 6 反射白名單 | 中 | 反射測試擴充涵蓋新欄位，CI gate 鎖白名單 |
| Member snapshot 每筆 chat 都查 DB 造成熱路徑效能問題 | 中 | `PlatformUserDisplayCache` 既有快取機制延伸覆蓋 snapshot |
| OneComme contract 過早定型，未來 plugin 實作時不合用 | 中 | Contract 只定 stream + result + 警告 list，實際映射 plugin 自行決定 |
| 自訂 HTML preset 與 Vue preset 兩條路徑長期同步成本高 | 中 | SignalR DTO 為唯一真理；preset 渲染只允許讀已白名單欄位 |

---

## Out-of-Scope

- OneComme bridge plugin 完整 importer 實作（Phase 7D）
- Alerts overlay preset / customizer（Phase 7E 或後）
- 樣板 marketplace / 雲端同步
- 樣板熱重載 file watcher
- 上傳樣板 sandbox 化執行
- 多使用者 / 多頻道 overlay 設定分離

---

## 對 SPEC §4.14.1 的對應

| SPEC 元素 | 落地 Task |
| --- | --- |
| 雙軌渲染管道 (Vue + 靜態 HTML) | Task 44 + Task 46 |
| Preset 選擇優先順序 | Task 47 |
| HTML upload 機制 | Task 46 |
| 靜態 HTML SignalR 資料契約 | Task 44 (overlay-common.js 既有) + Task 48 |
| Member Card in Chat | Task 48 |
| OneComme plugin 路徑 | Task 49 (contract only) |
| Member 集章卡 Controller | Task 45 |
| URL safety | Task 45 + Task 46 |
| `twitch.client_id` namespace ADR | 已落地於 Phase 6 補丁，本 phase 無相關 |
