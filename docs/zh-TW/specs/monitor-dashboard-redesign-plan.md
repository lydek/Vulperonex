# 規劃：監控儀表板重新設計

> 來源規格書：`docs/specs/monitor-dashboard-redesign.md`
> 追蹤父級：本規劃
> 目標分支：`codex/phase7-workflow-parity`（或如果偏好隔離的切片，則使用後續分支）

本文件將規格書轉化為具有相依關係、並行性、風險和驗證關卡的順序實作計畫。它考量了規格書的 Phase 1 已部分導入於當前差異中的事實。

---

## 當前狀態稽核（自規劃開始時）

| 規格書任務 | 狀態 | 證據 |
|---|---|---|
| 任務 1 — 版面配置合約 | ✅ 已完成 | `MonitorDashboardView.vue` 具有 `WIDE_BREAKPOINT = 1280`，已記錄窄版/桌上型/行動版分支 |
| 任務 2 — 外殼標記 (Shell tokens) | ⚠️ 部分完成 | `styles/monitor-tokens.css` 存在 22 個 `--monitor-*` 標記，但值是硬編碼的亮色十六進位值，而非衍生自啟用的 Vulperonex 主題（違反決策 1） |
| 任務 3 — 標頭 | ✅ 已完成 | 毛玻璃類別、⚙️ 圖示、眉標、具有脈衝點動畫的 3 狀態晶片 |
| 任務 4 — 控制側軌 + 抽屜 + SimulateControlsPanel 參照對等 | ⚠️ 外殼已完成，面板內部未改動 | 側軌可折疊 + 抽屜已導入；`SimulateControlsPanel.vue` 仍是單一別名驅動的表單（無測試模式切換、無視覺 4 區段節奏、未確認批次簽到對等） |
| 任務 5 — 預覽工作區 | ⚠️ 部分完成 | `MonitorOverlayPanel.vue` 中現有的 `monitor-controls-header` 列具有分頁/預設集/環境/重新載入；尚無眉標/更強的工具列框架 |
| 任務 6 — 聊天摘要重構 | ❌ 待處理 | `ChatStreamPanel.vue` 尚未針對新外殼重構 |
| 任務 7 — 響應式過渡 | ⚠️ 部分完成 | 調整大小監聽器正確，但過渡陳舊狀態邊緣僅經過輕微測試 |
| 任務 8 — 國際化 (i18n) + 無障礙 (a11y) | ⚠️ 部分完成 | 標頭/晶片/抽屜鍵已完成；尚未加入重構後控制項/摘要/預覽的新鍵值 |
| 任務 9 — 最終迴歸 | ⚠️ 部分完成 | 新增了 7 個 vitest 案例（本地端 172 個通過）；發現任務 4/5/6 尚有工作 |

結論：Phase 1 大部分已導入，但未遵守決策 1（主題衍生標記），且 Phase 2 仍需要實際工作 —— 特別是任務 4（`SimulateControlsPanel` 參照對等推動），這是規格書中最大的行為差異。

---

## 策略

依據規格書推薦的執行順序進行垂直切片：

1. 在 Phase 2 之前關閉 Phase 1 的缺口（決策 1 標記修正）。
2. 依相依順序導入 Phase 2 區域：任務 4 最優先（最高使用者可見表面積），然後並行進行任務 5 和 6。
3. Phase 3 綁定過渡、無障礙性 (a11y) 和迴歸測試網。

每個批次都以標準的四步驟檢查點（vue-tsc、vitest、build、lint）結束。

---

## 批次

### 批次 0 — 關閉 Phase 1 標記決策缺口

在繼續之前修正該項違反決策的問題。

| 任務 | 動作 | 檔案 | 規模 |
|---|---|---|---|
| 0.1 | 將 `--monitor-*` 中硬編碼的亮色十六進位值替換為衍生自 `styles/app.css` 主題變數的值（或公開一個 `--vp-theme-*` 來源）。使用 `light-dark()` 或後備鏈，使監控器標記遵循應用程式主題，而非強迫總是亮色。 | `src/frontend/src/styles/monitor-tokens.css`, `styles/app.css`（讀取標準名稱） | S |
| 0.2 | 手動煙霧測試：切換應用程式主題（若切換開關存在）並確認監控器表面隨之改變。若主題切換開關尚未存在，則透過註解記錄深色分支已配置，但目前無法切換。 | （無檔案） | XS |

驗證：vue-tsc + vitest（煙霧測試）；預期無行為變更。

相依項：無。
可並行性：否 —— Phase 2 的關卡。

---

### 批次 1 — Phase 2 區域 1：控制側軌參照對等 (規格書任務 4)

這是規格書中最重的任務。決策 2 明確要求與參照 `MonitorControls.vue` 達成功能 + 版面配置對等，而不僅僅是視覺上的重新組織。

| 任務 | 動作 | 檔案 | 規模 |
|---|---|---|---|
| 1.1 | 將表單分成符合參照節奏的 4 個欄位集 (fieldset)：**(a) 測試模式切換**，**(b) 聊天模擬**，**(c) 簽到模擬（單一 + 批次）**，**(d) 其他事件別名（訂閱/贈送訂閱/小奇點/兌換/追隨）**。保留當前的 `alias` 參照，但根據別名渲染相關的欄位集，*並*允許直接輸入聊天/簽到而無需下拉選單。 | `SimulateControlsPanel.vue` | M |
| 1.2 | 新增 `isTestMode` 參照（鏡像反轉的參照 `isProductionMode`）。暫時將其連接至現有的模擬 API 呼叫作為無動作旗標（後端支援依規格書超出範圍）。側軌頂部顯示 UI 切換開關。 | `SimulateControlsPanel.vue` | S |
| 1.3 | 新增批次簽到區段（數量 + 略過冷卻時間 + 時間戳記）。現有面板已具有 `batchSize` 參照 —— 確認已連接到 `/api/simulate/checkin` 迴圈並顯示進度。 | `SimulateControlsPanel.vue` | S |
| 1.4 | 使用 `--monitor-*` 標記將欄位集樣式設計為緊湊的控制卡片。每個參照都有區段標頭 + 細分隔線。 | `SimulateControlsPanel.vue` | S |
| 1.5 | 抽屜呈現：確保窄版抽屜主體內渲染相同的欄位集節奏（無獨立版面配置）。 | （已繼承） | XS |
| 1.6 | 國際化 (i18n)：新增鍵值 `monitor.controls.section.{testMode,chat,checkin,other}.title`, `monitor.controls.testMode.label`, `monitor.controls.batch.{count,run,progress}`。雙語。 | `i18n/{en-US,zh-TW}.json` | S |
| 1.7 | Vitest：新增涵蓋以下項目的案例：(a) 測試模式切換 ARIA + 狀態，(b) 批次簽到進度發送，(c) 每個別名下的區段渲染。現有的 `SimulateControlsPanel.test.ts` 2 案例檔案擴充至約 6 案例。 | `SimulateControlsPanel.test.ts` | S |

驗證：vue-tsc + vitest + 手動桌上型/窄版檢查。

相依項：批次 0。
與批次 2/3 的可並行性：否 —— 任務 4 在規格書中是獨立的，但使用批次 0 中修正的標記。在批次內，1.1 必須在 1.2-1.5 之前；1.6 可以與 1.4-1.5 一起執行；1.7 最後。

---

### 批次 2 — Phase 2 區域 2：預覽工作區重構 (規格書任務 5)

僅限呈現。無新的即時/草稿產品模式（規格書範圍防護）。

| 任務 | 動作 | 檔案 | 規模 |
|---|---|---|---|
| 2.1 | 在現有的 `monitor-controls-header` 上方新增標頭眉標 + 標題列，使預覽讀取為「工作區」（例如：`SCENE PREVIEW` / `t('monitor.preview.title')`）。 | `MonitorOverlayPanel.vue` | XS |
| 2.2 | 將 `monitor-controls-header` 提升為 2 列工具列：第 1 列 = 中心分頁 + 預設集選取器 + 環境切換 + 重新載入；第 2 列 = 背景控制項（透明/綠幕/粉紅幕/顏色/URL）。使用網格/彈性斷點來優雅折行。 | `MonitorOverlayPanel.vue` | M |
| 2.3 | 用更強的表面框住 iframe 畫布：邊框、內陰影、防止在窄寬度上折疊的最小高度。使用 `--monitor-*` 標記。 | `MonitorOverlayPanel.vue` | S |
| 2.4 | 國際化 (i18n)：`monitor.preview.title`, `monitor.preview.eyebrow`, `monitor.preview.toolbar.{reload,env.draft,env.production}` 鍵值（重新命名現有的內嵌字串）。 | `i18n/*` | S |
| 2.5 | Vitest 現有的 `MonitorOverlayPanel` 測試（若有）迴歸。若無，針對標題/工具列渲染新增極簡煙霧測試。 | `MonitorOverlayPanel.test.ts`（新增） | S |

驗證：vue-tsc + vitest + 於 1440/1280 寬度手動檢查。

相依項：批次 0。
與批次 1 和 3 的可並行性：是（不同的檔案，無共享狀態）。建議與批次 3 同時運行。

---

### 批次 3 — Phase 2 區域 3：聊天摘要重構 + SignalR 狀態 Composable (規格書任務 6)

範圍防護：保留當前的 `ChatStreamPanel` 合約；僅重構。為連線狀態新增一個新的 Composable（由聊天面板 + 儀表板標頭晶片使用）。

| 任務 | 動作 | 檔案 | 規模 |
|---|---|---|---|
| 3.0 | 根據「SignalR 連線狀態模式」區段建立 `useHubConnectionState(connection)` Composable。層級 1 回呼 + 層級 3 30 秒被動輪詢。**無** Composable 內部的層級 2 重試；呼叫端配置手動重新連線。 | `src/frontend/src/composables/useHubConnectionState.ts`（新增）, `useHubConnectionState.test.ts`（新增） | M |
| 3.1 | 新增更強的摘要標頭：標題 + 由 `useHubConnectionState` 驅動的即時狀態晶片 + 清除/重新整理按鈕。來自 `--monitor-*` 的標記。 | `ChatStreamPanel.vue` | S |
| 3.2 | 收緊項目框架：頭像/名稱/訊息節奏、細微的列分隔線、懸停狀態。保留列表語義（現有的測試選取器）。 | `ChatStreamPanel.vue` | S |
| 3.3 | 穩定的桌上型寬度：確保父網格遵守欄位最小寬度 `260px`、最大寬度 `360px`。 | `MonitorDashboardView.vue`（聊天面板規則）, `ChatStreamPanel.vue` | XS |
| 3.4 | 窄螢幕堆疊：確認 `< 1024px` 的聊天位於預覽下方，且最小高度不會剪裁標頭。 | （僅限 CSS） | XS |
| 3.5 | 自當前切片延遲：替換儀表板標頭健康晶片資料來源：`getHealth` 10 秒輪詢 → `useHubConnectionState` (SignalR) + 保留 `/api/health` 於 30 秒作為深層健康輔助晶片，或合併最差二選一策略。 | `MonitorDashboardView.vue` | S |
| 3.6 | 新增手動重新連線按鈕：僅在 `state === Disconnected` 時可見。連接到受保護的 `manualReconnect()`（重入保護 + 可見性觸發）。 | `ChatStreamPanel.vue` 或共享標頭 | S |
| 3.7 | 國際化 (i18n)：`monitor.chat.title`, `monitor.chat.clear`, `monitor.chat.live.{connected,reconnecting,disconnected,connecting}`, `monitor.chat.reconnect` 鍵值。 | `i18n/*` | S |
| 3.8 | Vitest：現有的聊天測試保持綠色；新增 (a) 標頭渲染 + 針對每個 `HubConnectionState` 的狀態晶片類別，(b) 清除按鈕點擊，(c) 涵蓋回呼同步 + 30 秒輪詢同步 + 銷毀清理的 `useHubConnectionState` 單元測試，(d) manualReconnect 重入保護。 | `ChatStreamPanel.test.ts` + `useHubConnectionState.test.ts` | M |

驗證：vue-tsc + vitest + 於 1440/1200/800 手動測試 + 混亂測試（DevTools 節流網路 → 離線 → 線上，確認晶片能反映狀態而不會有無限制的重新連線垃圾郵件）。

相依項：批次 0。
可並行性：是，與批次 2 並行。

---

### 批次 4 — Phase 3：過渡 + 無障礙性 (a11y) + 最終迴歸

| 任務 | 動作 | 檔案 | 規模 |
|---|---|---|---|
| 4.1 | 收緊大小調整處理：當在中途跨越斷點時，確保 `showDrawer` 僅在進入寬版時重設，而 `isSiderOpen` 僅在進入窄版時重設。在過渡上加入 `prefers-reduced-motion` 防護。 | `MonitorDashboardView.vue` | S |
| 4.2 | 無障礙性 (a11y)：鍵盤傳遞穿過標頭 → 側欄切換 → 抽屜關閉。確認 Tab 鍵順序。在抽屜中加入焦點捕捉（基本：開啟時焦點在關閉按鈕，關閉時返回切換按鈕）。 | `MonitorDashboardView.vue` | S |
| 4.3 | i18n 掃描：確認儀表板外殼 + 3 個面板中沒有留下硬編碼字串。 | grep + 手動 | S |
| 4.4 | Vitest：加入過渡狀態案例（側欄開啟的寬版→窄版調整大小可正確關閉；抽屜開啟的窄版→寬版調整大小可正確關閉）。若有，更新 a11y 測試。 | `MonitorDashboardView.test.ts`, `a11y.test.ts` | S |
| 4.5 | 最終 4 步驟檢查點：vue-tsc + vitest + build + lint。 | （指令） | XS |
| 4.6 | 在 1440/1280/1024/800 手動驗證矩陣。在 `docs/phases/phase-7d-*/manual-verification.md`（或新檔案）中記錄帶日期的項目。 | docs/phases | XS |

驗證：所有四個命令均為綠色；記錄手動矩陣。

相依項：批次 1, 2, 3。
可並行性：4.1 + 4.2 + 4.3 內部並行；4.4-4.6 最後順序執行。

---

## 風險暫存器

| # | 風險 | 可能性 | 影響 | 緩解措施 |
|---|---|---|---|---|
| R1 | 標記主題衍生兔子的洞（`app.css` 中尚不存在全域主題變數） | 高 | 中 | 若 `app.css` 缺少主題標記，則將批次 0 範圍限制為記錄監控器標記在此切片中刻意僅為亮色 + 發送未來全域主題規格書的 TODO。不要阻礙 Phase 2。 |
| R2 | `SimulateControlsPanel` 參照對等推動破壞了現有的測試/API 合約 | 中 | 高 | 保留 `alias` 參照 + 發送合約；僅在視覺上進行分區；後端模擬 API 保持不變。測試變更是相加的。 |
| R3 | 預覽工具列 2 列在窄寬度時折行顯得尷尬 | 中 | 中 | 為每個斷點定義明確的網格樣板；最後一個彈性折行；先在 1280 測試。 |
| R4 | 聊天面板最小寬度在 1280 上限制過於激進 | 低 | 中 | 使用 `clamp(260px, 22vw, 360px)`；手動檢查。 |
| R5 | 調整大小抖動時響應式過渡拍動 | 低 | 中 | 使用 `requestAnimationFrame` 防抖動 `updateLayout`。 |
| R6 | 範圍蔓延到參照設定分頁（模組/聲音/樂透） | 高 | 高 | 透過規格書「超出範圍」進行硬性防護；拒絕未對應到任務 1-9 的子任務。 |
| R7 | 新的 i18n 鍵值導入時沒有 zh-TW 對照 | 中 | 低 | 每個批次的 i18n 步驟在同一個 commit 中同時修改這兩個檔案；vue-tsc + 缺少鍵值警告會捕捉到漂移。 |
| R8 | SignalR 重新連線風暴（自監聽器/間隔呼叫 `start()`） | 低（配合 Composable 合約） | 高 | Composable 將 `start()` 隱藏在具有重入保護的 `manualReconnect()` 後方。批次 3 PR 的 Lint 審查強制執行「在 Composable 之外不得呼叫 `connection.start()`」。 |
| R9 | 監控檢視在重新連線中途卸載時，未銷毀的計時器造成記憶體洩漏 | 低 | 中 | `onUnmounted` 清除 `pollTimer` + `reconnectTimer`；測試 3.8 涵蓋此點。 |
| R10 | 背景分頁節流無限期延遲 L1 回呼 → 晶片陳舊 | 高 | 低 | L3 30 秒輪詢在分頁獲得焦點時喚醒；可見性監聽器在返回時觸發重新連線嘗試。 |

---

## SignalR 連線狀態模式

直接從 `useOverlayHub` 驅動即時晶片 / 重新連線 UX 通常會在兩種失敗模式之間擺盪：(a) 當重試迴圈存在於監聽器內部時，會產生無限的重新連線風暴，以及 (b) 當回呼被節流（背景分頁、掛起的 worker）時，會產生無聲的陳舊狀態。以下模式消除了這兩者。

### 三層結構，嚴格分離

| 層級 | 機制 | 其功能 | 絕對不能做的事 |
|---|---|---|---|
| L1 — 被動回呼 | 來自 `HubConnection` 的 `onclose` / `onreconnecting` / `onreconnected` | 在事件發生時立即同步參照 | 觸發 `start()`、重試或任何網路呼叫 |
| L2 — 增量重新連線 | 手動 `setTimeout` 配合退避陣列，受重入旗標 + 可見性保護 | 當狀態為 `Disconnected` 時，每次使用者/可見性觸發嘗試 `start()` 一次 | 在沒有外部觸發的情況下自動排程下一次嘗試 |
| L3 — 防禦性輪詢同步 | `setInterval` 於 30 秒讀取 `connection.state` 並在變更時更新參照 | 補償遺漏的回呼（背景節流、作業系統睡眠） | 呼叫 `start()`、在每次 tick 分配，或觸發網路 |

### Composable 合約

```ts
// src/frontend/src/composables/useHubConnectionState.ts
import { ref, onMounted, onUnmounted } from "vue";
import { HubConnectionState, type HubConnection } from "@microsoft/signalr";

const POLL_INTERVAL_MS = 30_000;
const RECONNECT_BACKOFF_MS = [0, 2_000, 10_000, 30_000, 60_000];

export function useHubConnectionState(connection: HubConnection) {
  const state = ref<HubConnectionState>(connection.state);
  const lastChangedAt = ref<number>(Date.now());
  const reconnectAttempt = ref<number>(0);
  let pollTimer: number | null = null;
  let reconnectTimer: number | null = null;

  function syncFromConnection(): void {
    const next = connection.state;
    if (next !== state.value) {
      state.value = next;
      lastChangedAt.value = Date.now();
      if (next === HubConnectionState.Connected) reconnectAttempt.value = 0;
    }
  }

  // L1 — 被動
  connection.onreconnecting(syncFromConnection);
  connection.onreconnected(syncFromConnection);
  connection.onclose(syncFromConnection);

  // L2 — 手動重新連線（呼叫端呼叫）
  async function manualReconnect(): Promise<void> {
    if (connection.state !== HubConnectionState.Disconnected) return;
    if (reconnectTimer !== null) return; // 重入保護

    const idx = Math.min(reconnectAttempt.value, RECONNECT_BACKOFF_MS.length - 1);
    const delay = RECONNECT_BACKOFF_MS[idx];

    reconnectTimer = window.setTimeout(async () => {
      reconnectTimer = null;
      try {
        await connection.start();
        syncFromConnection();
      } catch {
        reconnectAttempt.value++;
        // 刻意不自動排程下一次嘗試
      }
    }, delay);
  }

  // L3 — 防禦性 30 秒輪詢（唯讀）
  function startPolling(): void {
    if (pollTimer !== null) return;
    pollTimer = window.setInterval(syncFromConnection, POLL_INTERVAL_MS);
  }

  function stopPolling(): void {
    if (pollTimer !== null) {
      clearInterval(pollTimer);
      pollTimer = null;
    }
  }

  function stopReconnect(): void {
    if (reconnectTimer !== null) {
      clearTimeout(reconnectTimer);
      reconnectTimer = null;
    }
  }

  function onVisibilityChange(): void {
    if (!document.hidden && connection.state === HubConnectionState.Disconnected) {
      void manualReconnect();
    }
  }

  onMounted(() => {
    startPolling();
    document.addEventListener("visibilitychange", onVisibilityChange);
  });

  onUnmounted(() => {
    stopPolling();
    stopReconnect();
    document.removeEventListener("visibilitychange", onVisibilityChange);
  });

  return { state, lastChangedAt, reconnectAttempt, manualReconnect };
}
```

### 記憶體 + CPU 預算

| 成本 | 每次 tick | 每天 (2,880 次 tick) |
|---|---|---|
| `connection.state` 取得器 | ~10 ns | ~28 µs |
| `!==` 比較 + 也許 `ref.value =` | ~50 ns | ~150 µs |
| 配置 | 0 位元組 | 0 位元組 |

比較：當前的 `getHealth` 10 秒輪詢 = 每天 8,640 次 `fetch` + JSON 解析。新模式便宜約 300 倍，且每次 tick 零配置。

### 硬性規則（等同於 lint）

1. **絕不**自 L1 回呼呼叫 `connection.start()`
2. **絕不**自 L3 間隔主體呼叫 `connection.start()`
3. **絕不**從前一次重新連線的失敗處理常式內部排程下一次重新連線
4. **務必**在 `onUnmounted` / `onScopeDispose` 中銷毀計時器
5. **務必**以 `reconnectTimer !== null` 重入檢查來保護 `manualReconnect`
6. 僅在成功的 `Connected` 過渡時重設 `reconnectAttempt`
7. 可見性驅動的重新連線必須檢查 `document.hidden` 且 `state === Disconnected`

### 測試覆蓋率 (批次 3.8)

| 案例 | 判斷式 |
|---|---|
| 初始掛載將參照同步至當前 `connection.state` | 參照相符 |
| `onclose` 回呼將參照翻轉至 `Disconnected` | 參照 + `lastChangedAt` 已更新 |
| 當回呼被抑制時，30 秒輪詢會取得狀態（模擬未發送事件但 `connection.state` 改變） | 參照反映當前狀態 |
| 當處於 `Connected` 時進行 `manualReconnect` 為無動作 | `start()` 未被呼叫 |
| 當計時器擱置時進行 `manualReconnect` 重入為無動作 | 僅排程一個 `setTimeout` |
| 重新連線成功將 `reconnectAttempt` 重設為 0 | 計數器已重設 |
| 重新連線失敗會遞增 `reconnectAttempt` | 計數器遞增，不自動重新排程 |
| `onUnmounted` 清除兩個計時器 + 監聽器 | 無洩漏，無進一步的狀態寫入 |

---

## 並行化地圖

```
批次 0 (關卡)
  │
  ▼
批次 1 ──── (獨立檔案：SimulateControlsPanel.vue)
  ║
  ╠═══ 批次 2 (獨立檔案：MonitorOverlayPanel.vue) ─┐
  ║                                                          │
  ╚═══ 批次 3 (獨立檔案：ChatStreamPanel.vue) ──────┤
                                                             │
                                            全部匯聚 ─────▼
                                                       批次 4 (最終)
```

在批次 0 導入後，三位工程師可以同時進行批次 1、2、3。單一工程師順序：0 → 1 → 2 → 3 → 4（最小的認知上下文切換成本）或 0 → 2 → 3 → 1 → 4（先進行較小的批次以建立動力）。

---

## 驗證關卡

在每個批次之後：

```powershell
cd src/frontend
.\node_modules\.bin\vue-tsc.CMD --noEmit
.\node_modules\.bin\vitest.CMD run
.\node_modules\.bin\vite.CMD build
.\node_modules\.bin\oxlint.CMD --config oxlint.json
```

在批次 4 (最終) 之後：還要運行完整的後端測試套件，以確認任何聊天/模擬測試存根漂移沒有引發整合迴歸：

```powershell
dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false
```

手動驗證矩陣記錄在 `docs/phases/phase-7d-checkin-binding-editor-monitor-member/manual-verification.md`（或僅針對監控器切片的同級檔案）下方帶有日期的項目中。

---

## 預估工時

| 批次 | 工程師小時 (專注) |
|---|---|
| 批次 0 | 1-2 小時 (或如果應用 R1 緩解措施則為 30 分鐘) |
| 批次 1 | 4-6 小時 |
| 批次 2 | 3-4 小時 |
| 批次 3 | 4-5 小時 (Composable + 混亂測試比先前估計增加約 2 小時) |
| 批次 4 | 2-3 小時 |
| **總計** | 單一工程師 **14-20 小時**；3 路並行 **10-14 小時** |

---

## 已解決的開放式問題

| # | 解析 | 動作 |
|---|---|---|
| Q1 | `app.css` **沒有** CSS 變數（已透過 grep 確認）。應用 R1 緩解措施。 | 保持 `--monitor-*` 為僅亮色，並配置 `[data-theme="dark"]` 區塊；加入參考未來全域主題規格書的 TODO 註解。 |
| Q2 | 使用確定的 PrimeVue ProgressBar。 | 批次 1 匯入 `primevue/progressbar`（或同等項目），並將 `:value` 驅動為 `batchProgress / batchSize * 100` |
| Q3 | 後端 **沒有** `isTest`/`IsTest`/`testMode` 旗標（已在整個 `src/` 中透過 grep 確認）。 | 在批次 1 中加入僅 UI 的切換開關，並附帶註解 `// TODO: backend support pending — currently UI label only`。在後端導入前，請勿在請求主體中傳遞該旗標。 |
| Q4 | 三層狀態模式：**(L1) 被動回呼** + **(L2) 增量重新連線** + **(L3) 防禦性 30 秒輪詢同步（唯讀）**。請參閱下方的「SignalR 連線狀態模式」區段。 | 批次 3 導入了新的 `useHubConnectionState` Composable。**禁止**：在監聽器/間隔中編寫重試迴圈、呼叫 `start()`，或在沒有使用者/可見性觸發的情況下自動重新啟動 `Disconnected`。 |

---

## 推薦的首要行動

1. 閱讀 `src/frontend/src/styles/app.css` 以解決 Q1。
2. 若主題標記存在：正確執行批次 0.1 (10 分鐘)。
3. 若否：執行 R1 緩解措施 (5 分鐘) + 繼續進行批次 1。
4. 在開啟批次 2-3 之前，完整導入批次 1 (單一最大批次)。

計畫已準備好執行。
