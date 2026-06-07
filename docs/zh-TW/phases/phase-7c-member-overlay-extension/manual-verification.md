# Phase 7C 手動驗證 (Manual Verification)

## 狀態矩陣 (Status Matrix)

| 區域 | 狀態 | 證據 |
| --- | --- | --- |
| 會員卡 Overlay 預設檔 | 部分通過 (PARTIAL) | 靜態 `member-card.html` / 共用的 member-card 資產已重建，且 `vite build` 通過。 |
| 會員管理設定 | 部分通過 (PARTIAL) | 設定端點 + 管理面板已落地。 |
| URL 淨化協助程式 | 通過 (PASS) | `src/utils/overlayAssetUrl.test.ts`。 |
| 自訂 HTML 上傳後端 | 通過 (PASS) | `Phase7cOverlayPresetTests`。 |
| 預設檔解析器後端 | 通過 (PASS) | `Phase7cOverlayPresetTests`。 |
| 聊天承載資料中的會員快照 | 通過 (PASS) | `SignalRHubTests` + `OverlayDtoWhitelistTests`。 |
| 聊天會員晶片預設檔 | 通過 (PASS) | 靜態 `chat.html` 會員晶片路徑 + `overlayAssetUrl.test.ts` 驗證。 |
| OneComme 橋接器契約 | 通過 (PASS) | 插件 csproj + 契約 + 文件。 |

## 執行的指令

前端：

```powershell
cd src/frontend
corepack pnpm vue-tsc --noEmit
.\node_modules\.bin\vitest.cmd run src/components/admin/MonitorOverlayPanel.test.ts src/utils/overlayAssetUrl.test.ts
.\node_modules\.bin\vite.cmd build
```

後端：

```powershell
dotnet build src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false
dotnet build src/Plugins/Vulperonex.Plugins.OneCommeBridge/Vulperonex.Plugins.OneCommeBridge.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false
dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~OverlayDtoWhitelistTests|FullyQualifiedName~SystemSettingKeyTests" /m:1 /nr:false /p:UseSharedCompilation=false
dotnet test tests/Vulperonex.Tests.Integration/Vulperonex.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Phase7cOverlayPresetTests|FullyQualifiedName~SignalRHubTests" /m:1 /nr:false /p:UseSharedCompilation=false
```

## 未決的手動檢查項目

| 流程 | 預期結果 | 狀態 |
| --- | --- | --- |
| 在沒有事件的情況下造訪 `/overlay/member-card.html` | 卡片隱藏，無執行階段錯誤 | 待定 (PENDING) |
| 模擬會員打卡併發 (Burst) | 佇列依序播放動畫 | 待定 (PENDING) |
| 透過管理頁面上傳自訂預設檔 | 代號 (Slug) 出現在清單中，且自訂 URL 可以開啟 | 待定 (PENDING) |
| 設定 `overlay.chat.preset=custom:{slug}` | `/overlay/chat` 重新導向至自訂 HTML | 待定 (PENDING) |
| 切換 `overlay.chat.show_member_card` | 聊天會員晶片在瀏覽器中正確顯示/隱藏 | 待定 (PENDING) |

## 已知阻礙因素 (Known Blockers)

- 由於 `src/Hosts/Vulperonex.Desktop/Program.cs` 中先前存在的語法錯誤，完整的 `dotnet build Vulperonex.sln` 仍然失敗。
- 由於先前存在的 `MembersView` / `TwitchAuthView` 測試，完整的前端 `pnpm test` 仍然失敗。
- 由於先前存在於 `public/overlay/**` 底下的舊有靜態 Overlay 資產，完整的前端 `pnpm lint` 仍然失敗。

## 附日期之記錄 (Dated Entry)

### 2026-05-24 - Phase 7C 實作輪次

- 驗證者：Codex
- 環境：Windows, .NET 10 SDK, Vite 7
- 指令 / 步驟：
  1. 新增後端自訂預設檔上傳/列出/刪除/解析器 API。
  2. 新增會員快照承載資料與系統設定變更廣播。
  3. 新增前端 Overlay 預設檔管理頁面、會員晶片預設檔、淨化協助程式測試。
  4. 新增 OneComme 橋接器主體與文件。
- 預期結果：Phase 7C 程式碼路徑可編譯，且針對性測試通過。
- 實際結果：針對性的 Web/插件建置以及針對性的單元/整合/前端測試通過；完整方案/前端測試套件仍因無關的先前存在失敗而受阻。
- 結果評級：部分通過 (PARTIAL)
