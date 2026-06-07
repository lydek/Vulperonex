# Phase 7B 待辦：Chat Output Observability and Overlay Template Presets

> 對應規劃：`docs/phases/phase-7b-chat-overlay-presets/plan.md`
> 父層待辦：`tasks/todo.md`

## Task 41 - Simulation Chat Output Observable Surface

- [x] Task 41a：確認 `Simulation` 平台下 `SendChatMessage` 的最終可觀測面（admin view / history / memory receiver）。
- [x] Task 41b：把 rendered message、platform、channel、dedupKey、status、timestamp 寫入可查詢模型。
- [x] Task 41c：提供最小 UI 或 API 檢視面，能直接回答「訊息有沒有送」。
- [x] Task 41d：補 unit/integration/manual 驗證，覆蓋 `sent` / `skipped` / `failed`。

## Task 42 - Chat Overlay Preset System

- [x] Task 42a：定義 chat overlay preset metadata / renderer contract。
- [x] Task 42b：實作至少兩個 preset：Vulperonex 預設 + 第二個內建或可安裝樣板。
- [x] Task 42c：提供設定層級切換入口，不需改前端原始程式碼。
- [x] Task 42d：驗證所有 preset 仍只使用 DTO whitelist + text binding。

## Task 43 - OneComme Compatibility Path

- [x] Task 43a：文件化 OneComme compatibility strategy。
- [x] Task 43b：定義 extension/import path 與樣板目錄結構或 package metadata 對應。
- [x] Task 43c：補手動驗證流程，記錄辨識、匯入、切換結果。

## Checkpoint：Phase 7B

- [x] 全部 Task 41-43 sub-task `[x]` 完成自檢
- [x] workflow `SendChatMessage` 在 `Simulation` 模式下可直接觀察結果，不再需要猜測是否送出
- [x] `/overlay/chat` 至少可切換兩個樣板，且 core preset contract 可承接外掛 / 可安裝樣板
- [x] `docs/phases/phase-7b-chat-overlay-presets/manual-verification.md` 記錄 observability + preset + extension compatibility PASS/FAIL
