# 已知合法工作流規則 Filter Key 對照表

本文件作為 Phase 8 實作期間（在 Phase B Metadata 服務層上線前）的人工維護對照表，記錄了目前系統中所有合法的 `WorkflowTrigger.Filter` Key 及其類型與設計用途。

## 1. 簡介

在 Vulperonex 目前的設計中，每一種流事件（Stream Event）都對應固定的篩選槽位。若在規則設定中使用了不在本表列出的 Key，系統會觸發 `Warning` 警示，且對應的規則將無法順利匹配。

---

## 2. 合法 Filter Key 清單

當前系統支持的所有合法 Filter Key 如下：

| 事件類型 Key (`EventTypeKey`) | 合法 Filter Key | 資料型別 | 範例與語意 |
|---|---|---|---|
| **`user.message`** (聊天訊息) | `CommandName` | 字串 | `"!checkin"` (限定特定指令，支持自動邊界檢查防止 `!so` 誤匹 `!sorry`) |
| | `Prefix` | 字串 | `"!help"` (以特定前綴開頭的訊息) |
| **`user.donated`** (捐贈/Bits) | `MinAmount` | 數字 | `100` (捐贈的最低 Bits/金額門檻限制) |
| **`user.subscribed`** (訂閱) | `Tier` | 字串 | `"1000"`, `"2000"`, `"3000"` (限定訂閱層級) |
| **`user.gifted_sub`** (贈送訂閱) | `Tier` | 字串 | `"1000"` (贈送的訂閱層級) |
| | `MinGiftCount` | 數字 | `50` (單次最低贈送份數門檻) |
| **`channel.raided`** (Raid 襲擊) | `MinViewers` | 數字 | `5` (Raid 帶來的最低觀眾數門檻) |
| **`reward.redeemed`** (忠誠點數兌換) | `RewardName` | 字串 | `"Lottery Ticket"` (限定特定的兌換項目名稱) |
| **`workflow.timer`** (計時器觸發) | `TimerId` | 字串 | `"hourly-alert"` (限定特定的計時器 id) |

---

## 3. 防錯建議

1. **避免隨意手寫 Key**：請務必對齊上表中的大小寫。例如，`CommandName` 不能寫成 `commandname` 或 `Command`。
