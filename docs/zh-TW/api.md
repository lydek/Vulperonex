# Vulperonex Web API 參考文件

> **Language / 語言**: [English](../api.md) | [繁體中文](api.md)

`Vulperonex.Web` Host 的完整對接面:REST 端點與 SignalR Hub。供管理 UI 用戶端作為契約參考。

機器可讀規格於執行期隨時可取:**`GET /openapi/v1.json`** — 建議直接以此產生用戶端(如 openapi-typescript、orval),不要手寫請求程式碼。

## 慣例

- JSON 一律 **camelCase**;enum 序列化為**字串**;輸入屬性名稱不分大小寫。
- 錯誤格式為 `{ "error": "<ERROR_CODE>", "meta": null }`,搭配對應 HTTP 狀態碼(見 `ErrorCodes` / `ErrorCodeStatusMap`)。
- API port **僅綁定 loopback**。Overlay port 在 `Overlay:Lan:Enabled` 開啟時可額外綁定 LAN 位址。

## 驗證與請求防護

由 `AdminGuardMiddleware` 強制執行:

| 對象 | 要求 |
|---|---|
| `GET /api/*`(`/api/overlay/*` 除外) | 僅限 loopback 來源 |
| 非 GET 的 `/api/*`、全部 `/api/overlay/*` | Loopback + `X-Admin-Csrf` header + Host 白名單 + Origin/Referer 相符 |
| LAN 請求 | 僅 overlay 靜態檔(GET)、SignalR hub(需 access key)、六個 overlay-safe config key(需 access key) |

管理用戶端流程:

1. `GET /api/overlay/csrf-token` → `{ token }`。Token 於 **Kestrel 每次重啟時重新產生**;收到 400 `MISSING_OR_INVALID_CSRF_HEADER` 時重新取得。
2. 所有變更請求與所有 `/api/overlay/*` 請求帶上 `X-Admin-Csrf: <token>`。

LAN overlay 用戶端(另一台機器上的 OBS)以 access key 驗證:`?k=<key>` 查詢參數或 `X-Overlay-Key` header。

## 系統

| Method | Path | 說明 |
|---|---|---|
| GET | `/health` | `{ "status": "ok" }` |
| GET | `/openapi/v1.json` | OpenAPI 文件 |
| GET | `/api/overlay/csrf-token` | `{ token }` — 管理 session CSRF token |

## 會員 — `/api/members`

| Method | Path | 說明 |
|---|---|---|
| GET | `/` | 會員列表。Query:`platform`、`limit`(1–200,預設 50)、`offset`。每筆含 `eTag`。 |
| GET | `/{id}` | 單筆會員;回應帶 `ETag` header。 |
| GET | `/{id}/audit` | 稽核紀錄。Query:`limit`、`offset`。 |
| PATCH | `/{id}/loyalty` | 調整點數。需 `If-Match`(缺 → 428,過期 → 409)。Body:`{ totalLoyalty?, checkInCount?, reason }`。 |
| POST | `/{id}/reset` | 重置計數。需 `If-Match`。Body:`{ resetLoyalty, resetCheckIn, reason }`。 |
| POST | `/{id}/delete-token` | 取得一次性刪除 token → `{ token }`。 |
| DELETE | `/{id}` | 兩段式刪除。Body:`{ token, reason }`。 |

## 工作流規則 — `/api/rules`

| Method | Path | 說明 |
|---|---|---|
| GET | `/` | 規則摘要列表。 |
| GET | `/{id}` | 完整規則 DTO。 |
| POST | `/` | 建立。Body **不可**帶 `id` → 201 並回 `Location`。 |
| PUT | `/{id}` | 更新。以儲存版本做樂觀並發 → 衝突回 409;`invoke-sub-workflow` action 做循環偵測 → 400 `CIRCULAR_WORKFLOW_REFERENCE`。 |
| DELETE | `/{id}` | 刪除。 |
| PUT | `/{id}/enable` | 啟用規則。 |
| PUT | `/{id}/disable` | 停用規則。 |

## 工作流定時器 — `/api/timers`

DTO:`{ id, ruleId, intervalSeconds, isEnabled, nextFireAt, version }`。

| Method | Path | 說明 |
|---|---|---|
| GET | `/` | 定時器列表。 |
| GET | `/{id}` | 單筆定時器。 |
| POST | `/` | 建立 → 201。Body:`{ ruleId, intervalSeconds, isEnabled, nextFireAt }`。 |
| PUT | `/{id}` | 更新。需 `If-Match: "<version>"`(缺 → 428,過期 → 409)。 |
| DELETE | `/{id}` | 刪除。 |

## 規則編輯器中繼資料

| Method | Path | 說明 |
|---|---|---|
| GET | `/api/event-types` | `[{ key, description, isSimulatable }]` — 系統事件已於伺服器端過濾。 |
| GET | `/api/metadata/triggers` | 每事件型別:`{ key, displayName, description, filterFields, validVariables }`。 |
| GET | `/api/metadata/actions` | 可用 action 型別與設定 schema。 |

## 系統設定 — `/api/config`

僅白名單 key 可存取;`security.*` 與 `oauth.*` namespace 一律回 403。

| Method | Path | 說明 |
|---|---|---|
| GET | `/{key}` | `{ key, value }`(value 為字串或 null)。 |
| PUT | `/{key}` | Body:`{ value }`。值依該 key 宣告型別(int / bool / enum)驗證 → 不符回 400 `INVALID_CONFIG_VALUE`。 |

## 插件模組 — `/api/plugins-modules`

| Method | Path | 說明 |
|---|---|---|
| GET | `/` | `[{ name, displayName, kind, enabled, dependencies, dependents }]` |
| POST | `/{name}/toggle` | Body:`{ enabled }`。回傳切換的模組與連動變更的模組。 |

## 模擬 — `/api/simulate`

| Method | Path | 說明 |
|---|---|---|
| POST | `/checkin` | Body:`{ platformUserId?, displayName?, skipCooldown?, stampCount?, isTest? }` → 202。`isTest: true` 完全不落 DB。 |
| POST | `/{alias}` | 模擬平台事件(`message`、`follow`、`subscribe`、`donate`、`gift-sub`、`raid`、`reward-redeem`…,見 `SimulationAliasRegistry`)。Body 支援 `roles`、`badges`、`colorHex`、`bits`、`viewers`、`rewardId`、`rewardTitle`… → 202。 |

## Twitch 授權 — `/api/twitch/auth`

| Method | Path | 說明 |
|---|---|---|
| GET | `/status` | `{ clientIdConfigured, clientSecretConfigured, hasRefreshToken }` |
| POST | `/start` | Authorization Code + PKCE。Body:`{ callbackPort? }` → `{ authorizeUrl, state, callbackPort }`。 |
| POST | `/complete` | Body:`{ state, code }` → 204。 |
| POST | `/device/start` | Device Code 流程 → 裝置授權資訊。 |
| POST | `/device/complete` | Body:`{ deviceCode }` → 完成 204 / 等待中 202。 |
| DELETE | `/token` | 清除儲存的 refresh token(中斷連線)。 |
| GET | `/auth/callback`、`/api/auth/twitch/callback` | 瀏覽器 OAuth redirect 目標(非 AJAX);完成後導回 `/?oauth=<result>`。 |

## Twitch 資料

| Method | Path | 說明 |
|---|---|---|
| GET | `/api/twitch/badges` | `{ ready, global[], channel[] }` |
| GET | `/api/twitch/rewards` | `{ ready, lastRefreshedAt, rewards[] }` |
| POST | `/api/twitch/rewards/refresh` | 強制刷新快取。 |

## 聊天送出佇列 — `/api/chat-outbox`

| Method | Path | 說明 |
|---|---|---|
| GET | `/` | 觀測待送聊天佇列。Query:`status`、`platform`、`limit`(≤500)。 |

## Overlay 管理

| Method | Path | 說明 |
|---|---|---|
| GET | `/api/overlay/presets` | 內建版型列表。 |
| POST | `/api/overlay/assets` | Multipart 圖片上傳(驗證格式、大小上限)→ `{ url }`。 |
| GET | `/api/overlay/lan-info` | 跨機 OBS 資訊:`{ enabled, bindAddress, overlayPort, accessKey?, suggestedHosts }`。 |
| DELETE | `/api/overlay/chat/messages` | 清除聊天歷史;於 hub 廣播 `cleared`。 |
| DELETE | `/api/overlay/alerts/messages` | 同上(alerts)。 |
| DELETE | `/api/overlay/member/messages` | 同上(member)。 |
| GET | `/overlay/{chat\|member\|alerts}` | OBS browser source 入口;redirect 至設定的 preset 頁。 |

## SignalR Hubs

所有 hub 推播訊息名為 `event`;清除歷史時廣播 `cleared` 與 `{ hubName }`。

| Hub | 說明 |
|---|---|
| `/hubs/events` | 通用事件(`platform.connection_changed`、設定變更轉發)。 |
| `/hubs/overlay/chat` | 聊天 overlay;連線時重播近期歷史(預設 30 筆)。 |
| `/hubs/overlay/alerts` | 警示;重播項目帶 `replayed: true`。 |
| `/hubs/overlay/member` | 簽到/會員卡。 |
| `/hubs/overlay/effects` | 特效觸發(無歷史)。 |
| `/hubs/overlay/widgets` | 自訂 widget;連線時重播歷史。 |
