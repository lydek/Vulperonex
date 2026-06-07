# Phase 7B 實作計畫：Chat Output Observability and Overlay Template Presets

> 父層計畫：`tasks/plan.md`
> 父層待辦：`tasks/todo.md`
> 對照來源：`docs/SPEC.md`、`ref/Omni-Commander/OmniCommander.WebApi/wwwroot/chat.html`
> 前置條件：Phase 7 runtime/schema parity 已完成；Phase 7A editor UX 補強不阻塞本 slice 的 backend observability 與 overlay preset contract。
> 目標：補齊 workflow `SendChatMessage` 在 simulation/local 模式下的可觀測性缺口，並把 `/overlay/chat` 從單一實作提升為可切換樣板系統。
> 邊界：OneComme 相容屬於 extension/plugin slice，不直接併入 core runtime。
> 進度來源：本文件中的 checkbox 僅作設計/驗收草案；實際完成狀態以 `docs/phases/phase-7b-chat-overlay-presets/todo.md` 與 `tasks/todo.md` 為準。

---

## 目標

目前 Phase 7 已完成 workflow parity，但仍有兩個明顯缺口：

1. `Simulation` 平台下的 `SendChatMessage` 缺少明確可觀測面，使用者無法直接確認訊息是否成功送出。
2. `/overlay/chat` 仍偏單一實作，缺少 preset/template system，不利於遷移與樣板擴充。

Phase 7B 只處理這兩條線：

- **可觀測性線**：讓 workflow chat output 在 simulation/local 模式下可查、可看、可驗證。
- **樣板線**：建立 chat overlay preset contract，支援多樣板切換，並為 OneComme 相容 extension 預留接點。

---

## 範圍

### 內含

- Simulation/local chat output observable surface
- Chat Outbox / history / memory receiver 至少一種明確可視或可查詢驗證面
- `/overlay/chat` preset/template contract
- 內建至少兩個可切換樣板
- 設定層級樣板切換
- OneComme-compatible extension/import contract

### 不含

- 直接把 OneComme runtime 或完整 UI 內建進 core
- 任意 raw HTML / `v-html` payload 直出
- 新增 workflow schema / 新 executor 類型
- graph/canvas workflow editor

---

## 任務分解

## Task 41 - Simulation Chat Output Observable Surface

**描述：** 補 `Simulation` 平台的 chat sender observable surface。`SendChatMessage` 不得只有 silent no-op；至少要能在 admin / overlay history / memory receiver 中看到 rendered message、platform、channel、dedupKey、status。

**驗收標準：**
- [ ] `Simulation` 平台執行 `SendChatMessage` 後，使用者可在可視介面或明確 API 中查到訊息結果。
- [ ] 結果至少包含 `message`、`platform`、`channel`、`dedupKey`、`status`、timestamp。
- [ ] workflow chat output 驗證不再相依 `/overlay/chat` 是否剛好有 bridge。
- [ ] `sent` / `skipped` / `failed` 狀態可區分。

**實作提示：**
- 可選實作面：admin `Chat Outbox` view、overlay history、simulation memory receiver。
- 優先選擇最能回答「訊息去哪了」的查詢面，而不是只寫 log。

## Task 42 - Chat Overlay Preset System

**描述：** 將 `/overlay/chat` 提升為 preset/template-driven overlay。提供多個內建樣板，並保留後續匯入 / 匯出能力；core 只定義 preset/package contract，不直接耦合 OneComme runtime。

**驗收標準：**
- [ ] 至少提供兩個可切換聊天樣板：Vulperonex 預設樣板 + 另一個內建或可安裝樣板。
- [ ] 樣板切換透過設定或 admin UI 完成，不需直接修改前端原始程式碼。
- [ ] 樣板渲染仍遵守 DTO 白名單與 text binding，不引入 `v-html` raw payload 直出。
- [ ] 同一份 overlay payload contract 可供不同 preset 重複使用。

**實作提示：**
- 先切 `preset metadata + renderer contract`，不要先把樣板系統和訊號來源綁死。
- 樣板可參考 Omni chat overlay 的視覺結構，但資料繫結需維持現有安全邊界。

## Task 43 - OneComme Compatibility Path

**描述：** 以 OneComme 為優先相容目標之一，但以 extension / plugin 方式接入。定義樣板匯入器、目錄掃描器、或 adapter package 的最小契約，降低既有使用者遷移成本，同時維持 core 邊界。

**驗收標準：**
- [ ] 文件明列 OneComme 相容策略：哪些能力透過外掛直接相容、哪些透過對應、哪些暫不支援。
- [ ] 至少有一條 extension/import path 明確標示為 OneComme-compatible / migration-oriented；不要求 core 內建整包整合。
- [ ] 手動驗證記錄 OneComme 樣板目錄結構或 package metadata 的辨識與匯入流程。

**實作提示：**
- 相容單位是「樣板目錄結構 / preset package」，不是綁 OneComme app 本體。
- 若要做 importer，需把 filesystem 掃描與 preset contract 分開。

---

## Checkpoint：Phase 7B

- [ ] 全部 Task 41-43 sub-task `[x]` 完成自檢
- [ ] workflow `SendChatMessage` 在 `Simulation` 模式下可直接觀察結果，不再需要猜測是否送出
- [ ] `/overlay/chat` 至少可切換兩個樣板，且 core preset contract 可承接外掛 / 可安裝樣板
- [ ] `docs/phases/phase-7b-chat-overlay-presets/manual-verification.md` 記錄 observability + preset + extension compatibility PASS/FAIL

---

## 風險與緩解

| 風險 | 影響 | 緩解 |
| --- | --- | --- |
| 可觀測面做成只看得到 log，看不到使用者語意 | 高 | 優先提供 message/platform/channel/status 查詢面 |
| preset system 太早綁死 OneComme 格式 | 中 | 先做 core preset contract，再做 importer/extension |
| overlay 樣板為了自由度突破安全邊界 | 高 | 嚴守 DTO whitelist + text binding，不引入 raw HTML payload |
| simulation sender 與真實 sender 行為差異過大 | 中 | 保持同一 outbox/envelope 模型，只替換最後傳送面 |

---

## Out-of-Scope

- OneComme app 直接內嵌或完整 runtime 相依
- 非 chat overlay 的 alerts/member 樣板市場
- 任意第三方範本腳本執行
- workflow editor UX 續修（留在 Phase 7A）
