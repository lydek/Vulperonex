# Phase 7C 待辦清單 (Todo)

## 任務 44 - 會員卡 Overlay (Member Card Overlay)

- [x] 靜態 `member-card.html` 預設預設檔做為會員 Overlay 介面運作正常。
- [x] 會員 Hub 事件佇列驅動卡片動畫流程。
- [x] 確定性的蓋章隨機產生器被抽離至 `utils/deterministicRandom.ts`。
- [x] 會員卡資產實作 CSS 基礎/主題分離。
- [x] 獨立的 `wwwroot/overlay/member-card.html` 保留做為靜態參考路徑。

## 任務 45 - 會員卡管理控制器 (Member Card Admin Controller)

- [x] `overlay.member.background_url`
- [x] `overlay.member.stamp_url`
- [x] `/api/config` 白名單已更新。
- [x] 會員管理面板已儲存此兩項設定。
- [x] URL 淨化協助程式由 vitest 涵蓋。
- [x] 已新增 `system.config_changed` 廣播。

## 任務 46 - 自訂 HTML Overlay 上傳 (Custom HTML Overlay Upload)

- [x] `POST /api/overlay/custom-presets`
- [x] 代號 (Slug) 驗證規則 `[a-z0-9-]{1,64}`
- [x] 拒絕 zip 檔的路徑穿越 (Path Traversal)。
- [x] 上傳與解壓縮大小限制為 `5 MB`。
- [x] 強制執行僅限 loopback。
- [x] `DELETE /api/overlay/custom-presets/{slug}`
- [x] `GET /api/overlay/custom-presets`
- [x] 管理端上傳/列出/刪除頁面。
- [x] 管理端頁面的 i18n 翻譯。
- [x] 針對無效代號 / 檔案過大 / 路徑穿越 / 重新導向流程的整合測試。
- [x] DTO 避免暴露伺服器內部路徑。

## 任務 47 - Overlay 預設檔解析器 (Overlay Preset Resolver)

- [x] `overlay.chat.preset`
- [x] `overlay.member.preset`
- [x] `overlay.alerts.preset`
- [x] 設定白名單已更新。
- [x] `GET /overlay/{hub}` 解析器。
- [x] `GET /api/overlay/presets`
- [x] 管理端預設設定頁面。
- [x] 重新導向時保留查詢字串 (Query String)。

## 任務 48 - 聊天中的會員快照 (Member Snapshot In Chat)

- [x] `overlay.chat.show_member_card`
- [x] `OverlayChatPayload.memberSnapshot`
- [x] 白名單測試已更新。
- [x] 聊天事件路徑透過會員查詢 + 顯示快取解析會員快照。
- [x] `ChatPresetMemberCardEmbed`
- [x] 預設聊天預設檔可渲染會員晶片。
- [x] 獨立的 `wwwroot/overlay/chat.html` 可渲染會員晶片。
- [x] vitest 涵蓋晶片渲染與淨化協助程式。
- [ ] 1000 次事件併發觀測基準。

## 任務 49 - OneComme 橋接器契約 (OneComme Bridge Contract)

- [x] `src/Plugins/Vulperonex.Plugins.OneCommeBridge/`
- [x] `IOverlayTemplateImporter`
- [x] `OverlayTemplateImportResult`
- [x] `docs/plugins/onecomme-bridge.md`
- [x] 方案 (Solution) 註冊。
- [x] `CONTRIBUTING.md` 插件貢獻說明。

## 驗證

- [x] `dotnet build src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj --no-restore`
- [x] `dotnet build src/Plugins/Vulperonex.Plugins.OneCommeBridge/Vulperonex.Plugins.OneCommeBridge.csproj --no-restore`
- [x] `dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~OverlayDtoWhitelistTests|FullyQualifiedName~SystemSettingKeyTests"`
- [x] `dotnet test tests/Vulperonex.Tests.Integration/Vulperonex.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Phase7cOverlayPresetTests|FullyQualifiedName~SignalRHubTests"`
- [x] `corepack pnpm vue-tsc --noEmit`
- [x] 針對性驗證：`MonitorOverlayPanel.test.ts`、`overlayAssetUrl.test.ts`
- [x] `vite build`
- [ ] 完整的 `dotnet build Vulperonex.sln`
- [ ] 完整的 `dotnet test Vulperonex.sln --no-build`
- [ ] 完整的前端 `pnpm test`
- [ ] 完整的前端 `pnpm lint`

## 目前阻礙因素 (Current Blockers)

- 由於 `src/Hosts/Vulperonex.Desktop/Program.cs` 存在先前已有的語法錯誤，因此完整方案建置尚未通過。
- 完整前端 `pnpm test` 仍然在 `MembersView` 和 `TwitchAuthView` 中存在先前已有的失敗。
- 完整前端 `pnpm lint` 仍然掃描 `public/overlay/**` 底下的舊有靜態 Overlay 資產，並報告了許多先前已有的問題。
