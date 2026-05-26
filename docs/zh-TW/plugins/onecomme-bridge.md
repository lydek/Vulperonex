# 玩部件橋接器 (OneComme Bridge)

階段 7C 僅提供契約與插件主體鷹架。

## 目標

將玩部件 (OneComme) 的 Overlay 預設包資料流匯入至 Vulperonex 自訂樣板配置的代號 (Slug) 中，而無須將契約耦合至本機檔案路徑。

## 契約

- 介面：`Vulperonex.Application.Overlay.Extensions.IOverlayTemplateImporter`
- 進入點：`ImportAsync(Stream package, string targetSlug, CancellationToken ct)`
- 結果：`OverlayTemplateImportResult`

## 映射說明

- `comment.name` 映射至 Overlay 的 `displayName`
- `comment.message` 映射至聊天區塊文字
- 匯入的 HTML 套件應指向 `/overlay/custom/{slug}/index.html`
- 警告資訊應攜帶有損的欄位映射或不受支援的玩部件 (OneComme) 選項

## 階段邊界 (限制)

- 階段 7C 中無實體匯入器實作
- 階段 7C 中無市集封裝支援
- 階段 7C 中無自動 HTML 重寫
