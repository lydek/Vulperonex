# 功能規格書：全域應用程式主題系統

> [← Back to Master Specification](../../SPEC.md)

### 4.24 全域應用程式主題系統 (Phase 7F)

**目標：** Vulperonex 管理 UI 必須支援全域主題模型，而非相依散落且硬編碼的亮色。主題系統涵蓋 Vue 管理外殼、共享管理元件、監控器框架 (monitor chrome)、設定 UI 以及嵌入式預覽框架。OBS 疊層預設集 (OBS overlay presets) 除非渲染為管理框架，否則保持預設集範圍的獨立樣式。

**主題模式：**

| 模式 | 行為 |
| --- | --- |
| `light` | 強制使用亮色主題標記。 |
| `dark` | 強制使用深色主題標記。 |
| `system` | 遵循 `prefers-color-scheme`，並在作業系統偏好變更時即時更新。 |

**標記合約 (Token contract)：**

- `--vp-*` 標記是管理 UI 表面、文字、邊框、焦點環、陰影、狀態顏色以及互動狀態的標準應用程式級標記。
- `src/frontend/src/styles/app.css` 擁有預設的應用程式標記定義與應用程式外殼使用方式。
- 可以存在功能本機的標記檔案，但除非明確屬於預設集範圍，否則它們必須衍生自 `--vp-*`。
- `src/frontend/src/styles/monitor-tokens.css` 絕不能保持為 disconnected 的永久調色盤；監控器標記在 Phase 7F 後必須衍生自應用程式標記。

**執行期合約 (Runtime contract)：**

- 執行期主題是透過 `document.documentElement.dataset.theme` 套用。
- 儲存的使用者偏好為 `light`、`dark` 或 `system` 其中之一。
- 設定 UI 需提供主題選取器。
- 持久化儲存在後端連線可用時使用系統設定。僅在主題服務/Composable 後方允許前端的 local-storage 橋接，以便在不變更頁面元件的情況下進行替換。
- 主題切換不需要引入新的前端相依套件。

**範圍劃分：**

- 管理應用程式範圍：`/monitor`、`/settings`、`/simulate`、`/events`、`/members`、`/overlay-presets`、`/rules`、`/timers`、`/chat-outbox`、`/twitch`，位於 `src/frontend/src/components/admin` 下的共享元件，以及管理預覽框架。
- 疊層預設集範圍：`src/frontend/public/overlay/**` 下用於聊天/會員的靜態資產，加上 `/overlay/alerts` 及其位於 `src/frontend/src/views/overlay` 下的 Vue 元件。疊層預設集範圍可以使用自己的品牌或 OBS 專屬標記，除非後續新增了明確的預設集設定，否則不需要遵循應用程式主題。

**驗收準則：**

- 切換主題即可即時更新管理 UI，無須重新載入頁面。
- `system` 模式遵循作業系統偏好。
- 應用程式外殼與常用基礎元件使用 `--vp-*` 標記，而非硬編碼的應用程式調色盤數值。
- 監控器頁面遵循啟用的應用程式主題。
- 每個路由在 `docs/phases/phase-7f-app-theme-system/manual-verification.md` 中都有一個 Phase 7F 主題驗證狀態。
- 前端類型檢查、測試、建置與 lint 皆通過，或記錄了無關的既有阻塞點。

---
