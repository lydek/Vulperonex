# 第 5.5 階段 CLI ID 解析與破壞性操作確認決定

> 父計畫：`docs/phases/phase-5_5-rapid-test/plan.md`
> 對應 todo 項目：
> - 任務 17e（新增）：CLI ID 解析 + 缺 arg UX + 破壞性操作確認

## 狀態

提案 — 待實作。本文件凍結 CLI 在 `rule` / `member` 群組下「以 id 為唯一鍵」操作的人因設計，避免實作期間重複討論。

## 問題範圍

Phase 5 完成的 CLI rule/member 群組以「完整 id」為唯一輸入：

```
rule disable <ruleId>
rule enable  <ruleId>
rule delete  <ruleId>
member show  <memberId>
member delete <memberId>
```

實測痛點：

1. **缺 arg 體驗**：`rule disable`（無 id）→ stderr 僅吐 `UNKNOWN_COMMAND`，無 usage、無 hint，使用者不知該補什麼。
2. **id 取得不便**：每條破壞性操作都要先跑 `rule list` / `member list` 抄 id；id 為 GUID/雜湊字串，難以人工辨識與記憶。
3. **混淆語意**：`UNKNOWN_COMMAND` 同時用於「沒這支子命令」與「子命令存在但缺 arg」，CLI E2E 測試與 cookbook 無法分辨。

## 設計原則

- **REST API 為唯一寫入規範**（SPEC §4.12 不變）。CLI 端的便利化解析（prefix / name）**僅在 CLI 層**完成，不擴張 API 介面。
- **解析過程使用者可見**。任何「prefix 命中 → 解為 id」「name → id」必須在 stderr 列出被選中的完整紀錄，使用者確認後才執行破壞性操作。
- **互動模式才能省略 `--yes`**。one-shot CLI / piped stdin 必須帶 `--yes`，避免腳本誤觸。
- **Name 不唯一**（SPEC L365 明示 `Name` 不唯一），CLI 不能假設 name → 單一 id；命中多筆走 `AMBIGUOUS_ID` 路徑。

## 決定

### 1. 缺 arg → `MISSING_ARGS` + inline usage

新增 error code `MISSING_ARGS`，與 `UNKNOWN_COMMAND` 區分：

- `UNKNOWN_COMMAND`：dispatcher 找不到該名稱的子命令。
- `MISSING_ARGS`：子命令存在但必要 positional arg 缺漏。

行為：

```
$ rule disable
MISSING_ARGS
usage: rule disable <id|prefix|--name <name>> [--yes]
hint: 跑 'rule list' 查可用 id
```

每條需要 id 的子命令都套此格式。usage / hint 文案放入 i18n catalog（`command.<group>.<verb>.missing-args.usage`、`.hint`）。

### 2. ID 解析順序

CLI 在送出 API 前以下列順序解析 positional `<identifier>`：

1. **完整 id 命中**（exact match `Id`）：採用，不查列表。
2. **id prefix 命中唯一**：GET `/api/rules` 或 `/api/members` → 客戶端過濾 `Id.StartsWith(input, Ordinal)` → 唯一命中採用，多重命中走 `AMBIGUOUS_ID`。
3. **`--name <n>` 模式**（僅 `rule` 群組）：GET `/api/rules` → `Name.Equals(n, OrdinalIgnoreCase)`；多重命中走 `AMBIGUOUS_ID`，零命中走 `NOT_FOUND`。

`--name` 與 positional id 互斥；同時提供 → `INVALID_ARGS`。

**`member` 群組不支援 `--name`** — `MemberReadModel` 無 `DisplayName` 欄位（只有 `MemberId` 與 `Identities[].PlatformUserId`）。`member show` / `member delete` 僅接受「完整 MemberId | id prefix」。Phase 6 若需以 `PlatformUserId` 查 member 再評估獨立 `--platform-user-id` 旗標。

**為何不做 fuzzy contains**：使用者期望 `--name foo` 是「我知道完整 name」而非搜尋；contains 容易誤觸破壞性操作。Phase 6 若需 search，新增獨立 `rule search <keyword>` 子命令。

### 3. `AMBIGUOUS_ID` 候選列出

格式：

```
AMBIGUOUS_ID
候選:
  abc12345  echo-rule        enabled
  abc99999  echo-rule-v2     disabled
hint: 用更長 prefix 或完整 id
```

- 候選表寫 stderr（與 error code 同流）。
- 至多列前 10 筆 + 截斷提示 `(... 還有 N 筆)`，避免 prefix 太短時噴整張表。
- 欄位順序：`Id (前 8 字) | Name | Status`（rule）或 `Id (前 8 字) | PlatformUserId | DisplayName`（member）。

`NOT_FOUND` 不列候選，只回 code。

### 4. 破壞性操作確認

需確認的子命令：

| 子命令 | 為何需確認 |
|--------|-----------|
| `rule delete` | 不可逆，刪 row |
| `rule disable` | 改 IsEnabled，影響執行配置 |
| `member delete` | 不可逆，刪 row |

**不**需確認：

- `rule enable`：可逆（再 disable 即可），無資料損失
- `rule show` / `member show`：read-only
- `member seed`：增量，無覆蓋

確認流程：

```
即將 disable:
  id:     abc12345-1111-2222-3333-444455556666
  name:   echo-rule
  status: enabled
確認？ [y/N]:
```

- **REPL 互動模式**（`Console.IsInputRedirected == false` 且 `Console.IsOutputRedirected == false`）：印確認 prompt，讀單行；`y` / `yes`（case-insensitive）執行，其餘 → `CANCELLED` + exit 1。
- **one-shot / piped**：若帶 `--yes` 直接執行；否則 `CONFIRMATION_REQUIRED` + 印「即將 X」摘要 + exit 1。
- `--yes` 在互動模式也接受（略過 prompt）。

### 5. 新 error codes

| Code | 情境 |
|------|------|
| `MISSING_ARGS` | 必要 positional arg 缺漏 |
| `AMBIGUOUS_ID` | prefix / name 命中多筆 |
| `NOT_FOUND` | id / prefix / name 零命中 |
| `CONFIRMATION_REQUIRED` | 破壞性操作於非互動模式未帶 `--yes` |
| `CANCELLED` | 互動 prompt 使用者輸入非 `y` |

`INVALID_ARGS` 既有：用於互斥 flag、`--name` 與 positional 同時提供等。
`UNKNOWN_COMMAND` 保留：僅用於 dispatcher 找不到子命令。

### 6. 競態與資料一致性

- 解析時 GET `/api/rules` → 確認 → 操作 API。中間 id 已被外部刪除 → API 回 404 → CLI 直接吐 `NOT_FOUND`，不重試。
- list 結果**不**做客戶端快取（每次解析現抓），避免 cookbook 跨命令的舊資料造成幻象。Phase 6 若性能成問題再評估。

## 為何不採用其他選項

- **自動補完 last list 的 numeric index**（`rule disable 1`）：REPL 跨命令隱藏狀態，使用者跑 `rule list` 後若有第三方寫入，index 1 已非當初看到那筆；破壞性操作風險過高，**不採用**。
- **fuzzy name contains 搜尋**：誤觸風險，列在「不採用」並於 Phase 6 評估獨立 `search` 子命令。
- **強制 `--yes` 不分模式**：互動使用者體驗劣化（每次都要打 flag），且 REPL 已是「人在線上」前提，prompt 比 flag 自然。
- **省略確認，只列 dry-run 預覽**：違反「破壞性操作必須二次確認」原則；使用者意外 Enter 即執行。

## 對 SPEC 的影響

更新 SPEC §4.13 / §5 範例：

- §4.13 CLI 命令清單追加 `--name` / `--yes` 旗標說明與引用本文件。
- §5（CLI 範例區）`rule disable <ruleId>` 等示範擴充為 `rule disable <ruleId|--name <name>> [--yes]`。
- 錯誤碼表（若 SPEC 有彙整節）追加 `MISSING_ARGS` / `AMBIGUOUS_ID` / `NOT_FOUND` / `CONFIRMATION_REQUIRED` / `CANCELLED`。

`docs/phases/phase-5_5-rapid-test/plan.md` 任務切片追加 17e；`todo.md` 同步。

## 測試門檻

- 整合測試（StubHandler）：
  - 缺 arg → `MISSING_ARGS` + stderr 含 usage / hint 字串
  - 完整 id → API 路徑正確
  - prefix 唯一 → 解為完整 id 後送出
  - prefix 多重 → `AMBIGUOUS_ID` + stderr 列候選（≤10）
  - prefix 零命中 → `NOT_FOUND`
  - `--name` exact / 多重 / 零命中 各一路徑
  - `--name` + positional id → `INVALID_ARGS`
- 破壞性確認：
  - one-shot 無 `--yes` → `CONFIRMATION_REQUIRED` + stderr 摘要
  - one-shot `--yes` → API 路徑正確
  - REPL 互動：模擬 stdin 輸入 `y` → 執行；`n` → `CANCELLED`
  - REPL 互動 `--yes` → 跳過 prompt
- `member` 群組鏡像測試（除非 SPEC 路徑差異）

## 待回填的審查筆記

- 審查者：
- 日期：
- 決定：
- 後續：
