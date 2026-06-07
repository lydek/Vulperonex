# 規格書：Vulperonex — 多平台直播自動化平台

> **狀態：** 已批准 v0.5（MVP 已交付；後續功能依各 Phase 文件追蹤）。v0.5 將工作流規則模型、儲存 schema 與 action 預設值對齊 Phase 8 實作（typed trigger filter、metadata 驅動編輯器、schema 整併、NCalc/filter 可觀測性 — 見 §4.26 與 `docs/phases/phase-8-workflow-rule-typed-filter/`）。v0.4 收斂 2026-06-01 spec-vs-impl 審查報告所揭露的英文版 → 程式碼漂移。
> **最後更新：** 2026-06-05
> ⚠️ **翻譯落後英文版：** §4.6 / §4.8 / §4.14 / §4.17 / OQ3 / OQ4 / OQ5 / §4.26 已同步至 v0.5（Phase 8 + 安全模型 + 疊層人物角色/簽到）；但 §2 / §4.4 / §4.7 / §4.13.1 / §4.22 仍有細節落後，請以英文版 `docs/SPEC.md` 為準，zh-TW 全面同步排在後續維護任務。
> **儲存庫：** 全新綠地專案儲存庫。本規格書描述了**目標架構**。無現有程式碼需遷移。
> **前身參考：** Omni-Commander (獨立繼任者 — 借鑑領域邏輯概念，而非程式碼)

---

## 1. 目標

**Vulperonex** 是一個平台無關的直播自動化工具，它聚合來自直播平台的事件，並透過統一的事件驅動架構驅動響應式功能（聊天疊層、工作流、成員追蹤、音效）。MVP 支援 Twitch；架構設計為可擴充至其他平台而不需修改 Domain/Application 層。

**原因：** 現有工具（包括前身專案 Omni-Commander）與 Twitch 特有概念高度耦合。Vulperonex 透過領域事件 (Domain Events) 將平台特定的事件源與功能消費者分離，從而能夠以極少的程式碼新增平台，並將測試/模擬支持作為一等事件源。

### 目標使用者

- **直播主：** 執行 Twitch（MVP 範圍）。
- **外掛程式作者：** 透過 `IVulperonexPlugin` 擴充行為 — 既可以消費事件也可以發布事件。
- **開發者：** 針對與實際平台相同的事件流執行自動化測試。

### MVP 成功願景

- Twitch 適配器 (Adapter) 將領域事件發布到事件匯流排 (Event Bus)。
- 工作流引擎 (WorkflowEngine) 訂閱並執行規則。
- 桌面殼層 (Photino) 託管由 Web 主機提供的 Vue UI。
- CLI 可以模擬事件、管理配置、管理規則並檢查成員。
- 新增平台 Adapter 不需修改 Application / Domain 層（架構隔離）。

---

## 2. 技術棧

### 後端 (.NET 10 LTS)

| 項目 | 選擇 |
|---|---|
| 語言 / 執行時期 | C# 14 / .NET 10 LTS |
| Web 框架 | ASP.NET Core Minimal API (10.0) |
| 即時通訊 | SignalR (10.0) |
| ORM | EF Core 10 (SQLite provider) |
| 桌面殼層 | Photino.NET 3.x |
| 外掛程式系統 | `IVulperonexPlugin` (自訂契約，MVP 階段在啟動時靜態引用) |
| 單元測試 | xUnit 3 / NSubstitute / FluentAssertions 7 |
| 測試方法 | BDD 情境定義 + TDD 紅/綠/重構實作 |

### 前端 (Vue 3.5+ / Vite 7.x)

| 項目 | 選擇 |
|---|---|
| 框架 | Vue 3.5+ (標準 SFC — Vapor Mode 推遲至第二階段效能實驗) |
| 建構 | Vite 7.3 (Rolldown；MVP 釘定 v7，不追 v8 — Vite 8 已發布但 MVP 期間不升級) |
| 語言 | TypeScript 5.8（`vue-tsc` 2.2） |
| UI | PrimeVue 4 (Unstyled) / UnoCSS 66 (Preset Wind) / Reka UI 2.9（headless，Phase 8） |
| 狀態 / 通訊 | Pinia 3 / Axios / @microsoft/signalr 9 / vue-router 4.5 |
| 測試 | Vitest 3 / Vue Test Utils 2.4 / @vitest/coverage-v8 3 / jsdom |
| Lint | oxlint 0.16 |
| 多語言 (i18n) | vue-i18n 11.x |

---

## 3. 專案結構

```
Vulperonex/
├── src/
│   ├── Vulperonex.Domain/                     # 純領域 — 實體、值對象、事件
│   ├── Vulperonex.Application/                 # 使用案例、介面、事件匯流排契約
│   ├── Vulperonex.Infrastructure/              # EF Core, SQLite, 倉儲 (Repos), 持久化
│   ├── Vulperonex.Plugins.Abstractions/        # IVulperonexPlugin 契約 (來源 + 接收)
│   │
│   ├── Adapters/
│   │   ├── Vulperonex.Adapters.Abstractions/   # 共享適配器介面 (IPlatformUserInfoCache 等)
│   │   ├── Vulperonex.Adapters.Twitch/         # Twitch IRC + EventSub (傳入 + 傳出)
│   │   └── Vulperonex.Adapters.Simulation/     # CLI / UI / 測試模擬
│   │
│   ├── Hosts/
│   │   ├── Vulperonex.Web/                     # ASP.NET Minimal API + SignalR + wwwroot
│   │   ├── Vulperonex.Desktop/                 # Photino 殼層 (包裝 Web)
│   │   └── Vulperonex.Cli/                     # CLI: 模擬 / 配置 / 規則 / 成員指令
│   │
│   └── frontend/                               # Vue 3.5 SPA，建構至 Web/wwwroot
│       ├── src/
│       │   ├── components/
│       │   ├── composables/
│       │   ├── stores/
│       │   ├── views/
│       │   └── i18n/
│       └── tests/
│
├── tests/
│   ├── Vulperonex.Tests.Unit/
│   ├── Vulperonex.Tests.Integration/
│   └── Vulperonex.Tests.Architecture/          # 層級 / 依賴規則強制執行
│
├── docs/                                        # SPEC.md 正式路徑：docs/SPEC.md
│   ├── SPEC.md
│   ├── adr/                                     # 架構決策記錄 (Architecture Decision Records)
│   └── plugins/
│
└── tools/
```

---


## 模組化規格書 (導覽目錄)

主規格書已拆分為多個模組化文件，以保持簡潔並優化 token 損耗：

- **[架構核心概念](specs/architecture.md)**：分層架構、戰術 DDD 邊界、DCI 拆分準則、事件流、模組與資料庫遷移等細節 (§4.1 - §4.17)。
- **開發指南與測試規範**：
  - **[指令慣例與測試策略](specs/conventions.md)**：開發指令、編碼風格、測試金字塔、覆蓋率目標與 BDD+TDD 紀律 (§5 - §8)。
- **功能模組規格書 (Phase 7 & Phase 8)**：
  - **[統一監控面板](specs/features/monitor-dashboard.md)**：統一即時監控頁、模擬控制、模組與外掛程式管理系統、版面配置 (§4.18, §4.20 - §4.22)。
  - **[會員管理編輯介面](specs/features/member-admin.md)**：調整會員忠誠度、打卡次數與 SQLite 稽核日誌 (§4.19)。
  - **[Twitch 徽章與頻道點數獎勵快取](specs/features/badge-and-reward-cache.md)**：Helix API、記憶體快取同步、模擬器徽章與獎勵選擇器 UI (§4.23, §4.25)。
  - **[全域應用程式主題系統](specs/features/theme-system.md)**：應用程式設計語彙標記 (`--vp-*`)、執行期合約與設定 UI (§4.24)。
  - **[工作流規則強型別過濾器](specs/features/typed-filter-and-observability.md)**：Schema 整併、強型別比對器、動態元資料提供者與運算式 Warn 日誌 (§4.26)。
- **附錄與歷史決策**：
  - **[歷史決策、MVP 指標與疑義解答](specs/decisions-and-queries.md)**：成功準則、設計決策 (D1 - D8) 與 Resolved Queries (OQ1 - OQ6) (§9 - §12)。
