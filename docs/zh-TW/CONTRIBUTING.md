# 貢獻指南

> **Language / 語言**: [English](../../CONTRIBUTING.md) | [繁體中文](CONTRIBUTING.md)

## 文件語系策略

- 英文為預設文件語言，保留原始檔名。
- 在地化 Markdown 文件放在 `docs/<locale>/` 之下，並與英文來源維持相同的相對路徑與純淨檔名。
- 繁體中文文件使用 `docs/zh-TW/` 目錄樹。
- 不使用 `*.zh-TW.md` 這類語系後綴命名；改採專用語系資料夾策略。

## 外掛程式骨架

- 新增的 Overlay 匯入器外掛應依賴 `Vulperonex.Application` 契約與 `Vulperonex.Plugins.Abstractions`。
- 保持匯入器契約基於資料串流 (Stream)。請勿要求呼叫端交付檔案系統路徑。
- 對於 OneComme 風格的匯入，將外部欄位對應至 Vulperonex 疊層 DTO 名稱，並對有損轉換返回警告。
