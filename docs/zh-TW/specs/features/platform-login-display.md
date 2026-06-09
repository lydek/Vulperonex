# 功能規格書：平台登入名稱顯示

> [← Back to Master Specification](../../SPEC.md)

### 4.28 平台登入名稱顯示 Platform Login Handle Display

**背景與動機：**

會員管理頁面（`src/frontend/src/views/admin/MembersView.vue`，「Twitch 帳號」欄位）原本顯示 `@{platformUserId}`。對 Twitch 而言，`platformUserId` 是不可變的**數字使用者 ID**（例如 `@109565589`），而非操作者熟悉的人類可讀帳號名稱（login / nick）。

領域層的會員身分（`PlatformIdentity`）僅以 `(platform, platformUserId)` 組成，刻意不含平台特定欄位；顯示用的 `displayName` / `avatarUrl` / `isSubscriber` 並非存於會員身分表，而是查詢時從**顯示資訊快取**（`PlatformUserDisplayInfo`）join 取得。Twitch 的 **login**（人類可讀帳號）先前完全沒有被捕捉、儲存或呈現。

本功能將 `login` 端對端補齊：由 IRC 訊息捕捉、寫入顯示資訊快取、經讀取模型曝露至 API，最終於管理 UI 以 `@{login}` 呈現。

**設計與規格：**

1. **領域事件捕捉 login（不汙染身分模型）：**
   - `StreamUser`（`Vulperonex.Domain`）新增尾端可選參數 `string? Login = null`。此名稱為泛用名詞，Domain / Application 層不相依任何 Twitch 特定型別。
   - Twitch IRC parser（`TwitchIrcMessageParser.Parse`）將 `message.UserName`（IRC login / nick，亦即 `platformUserId` 與 `displayName` 的 fallback 來源）填入 `StreamUser.Login`。

2. **顯示資訊快取（沿用既有 join 路徑，而非新增會員身分欄位）：**
   - Adapter 抽象層的 `PlatformUserDisplayInfo` record（`IPlatformUserInfoCache`）新增尾端可選 `string? Login = null`。
   - `TwitchDisplayCacheUpdater.ApplyChatAsync` 於 `with { ... }` 中以 `Login = streamEvent.User.Login ?? current.Login` 寫入（採 coalesce 保留既有值，避免事件缺漏時抹除）。
   - Infrastructure：`PlatformUserDisplayInfoEntity` 新增 `string? Login`；`PlatformUserDisplayInfoConfiguration` 設定 `Login` 為 `TEXT`;`PlatformUserDisplayCache.FromEntity` / `Apply` 雙向對應。

3. **讀取模型與 API：**
   - Application 層 `PlatformIdentityReadModel`（`MemberDtos`）新增尾端可選 `string? Login = null`。
   - Application 層 `IPlatformUserDisplayInfoProvider.PlatformUserDisplayInfo` 新增尾端可選 `string? Login`，`PlatformUserDisplayInfoProvider` 由快取讀出後一併回傳。
   - `MemberQueryService`（`ListAsync` / `FindByMemberIdAsync`）建構 `PlatformIdentityReadModel` 時帶入 `displayInfo?.Login`。
   - Web `MemberEndpoints` 直接回傳 `MemberReadModel`（System.Text.Json camelCase），故 `Login` 自動序列化為 `login`，無需額外 Web DTO 對應。

4. **前端呈現：**
   - `client.ts` 的 `PlatformIdentity` interface 新增 `login?: string | null`。
   - `MembersView.vue` 新增 helper `getLoginHandle(member)`，回傳 `primary.login || primary.platformUserId || member.memberId`。
   - 「Twitch 帳號」清單欄位（身分管理表與簽到管理表）由 `@{{ getPrimaryIdentity(member)?.platformUserId || member.memberId }}` 改為 `@{{ getLoginHandle(member) }}`。
   - 詳情面板的數字 ID 欄位維持顯示 `platformUserId`（該面板為標準 / 標準正規 ID 的呈現處）。

**驗收：**
- 會員清單「Twitch 帳號」欄於該會員產生過事件後顯示 `@{login}`（人類可讀帳號），而非數字 ID。
- 尚未產生事件的既有會員，`login` 為空時 fallback 顯示 `@{platformUserId}`，UI 不破版。
- API `GET /api/members` 與 `GET /api/members/{id}` 之 identity 物件包含 `login` 欄位（有值時）。
- EF migration `AddPlatformUserLogin` 於 `PlatformUserDisplayInfo` 新增可空 `Login` TEXT 欄位，且 snapshot 同步更新。
- 單元測試覆蓋：`TwitchIrcMessageParser.Parse` 由 `message.UserName` 設定 `StreamUser.Login`；`PlatformUserDisplayCache` 之 `Login` 寫入後可讀回（L1 + L2 round-trip）。

**邊界與回填（back-fill）：**
- `login` 僅儲存於顯示資訊快取，**不**新增至會員身分表，因此無需會員身分 schema migration。
- 屬純加性（additive）/ 可空（nullable）變更，無破壞性資料變更；所有新增參數皆為尾端可選參數，既有定位式建構不受影響。
- **回填策略：** 既有會員在下次產生事件（聊天等）前，仍顯示數字 ID；`login` 隨事件抵達逐步回填。本期**不**做歷史 Helix 批次解析回填。

---
