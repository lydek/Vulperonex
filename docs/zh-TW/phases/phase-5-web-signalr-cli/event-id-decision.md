# 第 5 階段事件 ID 決定

> 父任務：`docs/phases/phase-5-web-signalr-cli/plan.md` 任務 15b

## 狀態

已決定 — 任務 15b overlay 轉寄已依此決策實作。

## 待記錄的決定

- Overlay payload 的 `eventId` 使用 domain event 的 public event id。
- 平台提供的事件 ID 可跨 overlay 用戶端識別同一來源事件；adapter 後備 ULID 僅作為本機單一實例交付 ID。
- Overlay 公開 `eventId` 不得包含 `MemberId`、`PlatformUserId` 或其他內部身分識別碼。
- Phase 5 的 SignalR contract 測試驗證 `schemaVersion`、`eventId` 與 chat segment payload 會在線路負載中出現。

## 審查筆記

- 審查者：Codex
- 日期：2026-05-16
- 決定：採用 domain event public id 作為 overlay `eventId`，缺平台來源 id 時允許本機合成 id。
- 後續：若 Phase 6 引入多 overlay client replay/dedup，需重新評估 synthetic id 是否足以跨 client 重複抑制。
