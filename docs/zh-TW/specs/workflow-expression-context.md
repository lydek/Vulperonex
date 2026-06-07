# 工作流運算式內容詞彙 (Workflow Expression Context Terminology)

狀態：針對 Phase 8 清理的決策草案

## 為什麼存在此文件

工作流編輯器目前混合了三種不同的概念：

1. 觸發事件資料 (Trigger event data)
2. 引起觸發的使用者 (The user who caused the trigger)
3. 持久化的會員資料 (Persisted member data)

這三者並不相同。當編輯器使用重疊的名稱公開它們時，使用者最終會在 `Trigger.UserId`、`Member.UserId` 和平台覆寫之間猜測，儘管目前執行階段只保證其中之一。

本說明定義了目前的合約以及我們在 UI 和文件中應使用的用詞。

## 目前執行階段合約

### `Trigger.*`

`Trigger` 表示目前的事件承載資料 (Payload) 加上事件級別的元資料 (Metadata)。

範例：

- `Trigger.EventId`
- `Trigger.EventTypeKey`
- `Trigger.Platform`
- `Trigger.OccurredAt`
- `Trigger.MessageText`
- `Trigger.RewardId`
- `Trigger.RewardTitle`
- `Trigger.RedemptionId`
- `Trigger.TotalBitsGiven`
- `Trigger.Tier`
- `Trigger.GiftCount`
- `Trigger.ViewerCount`
- `Trigger.Depth`
- `Trigger.Payload.*`

`Trigger` 的作用域是事件級別的。除非後端確實將這些欄位寫入事件內容中，否則不應將其用作使用者身分識別別名的暫存處。

### `Member.*`

`Member` 目前表示由 `streamEvent.User` 所攜帶的觸發使用者快照。

範例：

- `Member.UserId`
- `Member.Platform`
- `Member.DisplayName`
- `Member.Roles`
- `Member.IsSubscriber`
- `Member.IsModerator`
- `Member.IsVip`
- `Member.IsFollower`
- `Member.IsBroadcaster`

重要提示：在目前的實作中，這**不是**一個已載入的持久化會員讀取模型 (Hydrated persistent member read model)。它是公開給運算式的事件使用者快照。

### 持久化會員資料 (Persistent member data)

持久化會員資料屬於會員網域 (`MemberRecord`、`PlatformIdentity`、忠誠度狀態、稽核歷史紀錄)。目前在工作流運算式內容中無法直接取得。

## UI 命名規則

在我們進行更大規模的命名空間重新命名之前，編輯器應使用更清晰的用詞來呈現這些作用域：

- `Trigger` 群組標籤：`觸發事件內容 (Trigger Event Context)`
- `Member` 群組標籤：`觸發使用者內容 (Trigger User Context)`

`Member.*` 底下的欄位標籤應明確說明為「觸發使用者」，而不僅僅是「會員」，因為執行階段的值是源自於目前的事件使用者。

## 行動指引 (Action guidance)

### 簽到 / 打卡 (Check-in)

對於一般的聊天觸發簽到：

- 使用者識別碼預設應為 `Member.UserId`
- 平台預設應為觸發事件的平台

這意味著常見的 UX 應讀作：

`簽到觸發此事件的使用者`

進階覆寫仍然可以公開：

- `UserId`
- `Platform`

### 其他針對使用者的行動

當其他針對使用者的行動（例如抽獎券更新）代表「目前觸發的使用者」時，也應偏好 `Member.UserId` 作為預設目標。

## 行動稽核快照

目前針對使用者/平台相關行動的審查結果：

- `triggerCheckIn`
  - 問題：編輯器先前暗示手動選擇目標是正常流程
  - 決策：預設為隱式觸發使用者；將 `UserId` 和 `Platform` 保留在進階覆寫中
- `addLotteryTickets`
  - 問題：說明文字對於預設目標過於模糊
  - 決策：保持欄位可編輯，但說明預設目標為 `Member.UserId`
- `lookupTwitchUser`
  - 結果：未發現合約不匹配；`Login` 和 `UserId` 都是明確的查詢輸入
- `sendChatMessage`
  - 結果：內部路由欄位（`TargetPlatform`、`Channel`）在視覺編輯器中仍刻意隱藏

## 合約清理規則

前端變數選取器、i18n 提示、元資料說明文字以及後端 `ExpressionContext` 必須描述相同的合約。

如果變數未被後端填入，編輯器就不得建議該變數。

## 已知的過期引用

一些較舊的範例仍然提到諸如 `Trigger.Arg0` 或 `Trigger.DisplayName` 之類的預留位置。這些不屬於目前後端 `ExpressionContext` 合約的一部分，在被重寫之前應被視為過期的範例。
