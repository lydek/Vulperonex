# 第 5 階段 REPL 取消行為決定

> 父任務：`docs/phases/phase-5-web-signalr-cli/plan.md` 任務 16f / 16g
> 對應 todo 項目：
> - 16f L57：REPL `twitch auth start` 支援 Ctrl+C 取消並輸出 `TWITCH_OAUTH_CANCELLED`
> - 16g L59：Line editor TTY 模式 history、Ctrl+C 清 buffer

## 狀態

提案 — 待實作。本文件凍結 Ctrl+C 在 REPL 內的語意，避免實作期重複討論。

## 問題範圍

REPL (`InteractiveSession`) 內 Ctrl+C 在兩個情境意義不同：

1. **LineEditor 讀取輸入中**：使用者尚未提交命令；Ctrl+C 應「清掉當前 buffer」，REPL 仍存活。
2. **Dispatch 命令執行中**（特別是長時 `twitch auth start` 等待 HttpListener callback）：使用者要中止當前命令；REPL 不應終止，當前命令應觀察到取消並輸出對應 error code。

預設 .NET 行為：Ctrl+C 觸發 `Console.CancelKeyPress`，未抑制則終止 process。`Console.ReadKey(intercept: true)` 不會把 Ctrl+C 當作鍵事件回傳，除非 `Console.TreatControlCAsInput = true`。

## 決定

### 1. REPL 永遠抑制 process 終止

`InteractiveSession.RunAsync` 啟動時 hook `Console.CancelKeyPress`，handler 中設 `e.Cancel = true` 並通知當前 dispatch CTS。Session 退出（`exit` / `quit` / EOF / 例外）時 unhook。

### 2. Dispatch 期間：CancellationToken 取消當前命令

`InteractiveSession` 每次 dispatch 迴圈建立新的 `CancellationTokenSource`，傳入 `dispatcher.DispatchAsync(..., cts.Token)`。`CancelKeyPress` handler 呼叫 `cts.Cancel()`。

命令實作（`twitch auth start` 是首例）必須：

- 把該 token 傳給所有 awaitable 呼叫（HttpClient、`HttpListener.GetContextAsync().WaitAsync(timeout, ct)`、`Task.Delay`）。
- 對「使用者取消」這個情境的 `OperationCanceledException`（`ct.IsCancellationRequested == true`）轉成 stderr 寫出對應的明確 error code，並回傳 exit code `1`。
- 不得吞掉非 REPL token 取消（例如 HttpListener timeout 例外）。

`twitch auth start` 的明確 code 為 `TWITCH_OAUTH_CANCELLED`。

### 3. LineEditor 期間：Ctrl+C 清 buffer

LineEditor 進入時暫時設 `Console.TreatControlCAsInput = true`，離開時還原。讀到 `KeyChar == '\x03'`（或 `ConsoleKey.C` + `ConsoleModifiers.Control`）時：

- 丟棄當前 buffer 內容、視覺上換行、回到 prompt 等下一輪輸入。
- **不**取消 outer dispatch token（因為 LineEditor 在 prompt 階段、外層尚未進入 dispatch）。

LineEditor 期間 `Console.CancelKeyPress` handler 仍要設 `e.Cancel = true`（防止 process 終止），但不觸發 dispatch CTS（dispatch 尚未開始）。實作上以「是否在 LineEditor 內」flag 旗標決定 handler 動作。

### 4. Redirected stdin 不適用

`ShouldUseLineEditor() == false`（stdin redirected）時不裝 `TreatControlCAsInput`，也不裝 CancelKeyPress handler — 該模式下 Ctrl+C 行為由呼叫方/CI 決定，REPL 不自行抑制 process 終止。

## 為何不採用其他選項

- **完全不處理 Ctrl+C**：使用者跑 `twitch auth start` 卡 5 分鐘 HttpListener，唯一逃生是殺 process，refresh token 設定流程體驗劣化。
- **Ctrl+C 直接退出整個 REPL**：違反 16g 對 REPL 持續執行的設計（`exit` / `quit` / EOF 才退）。
- **用 SIGINT pipe 取代 `CancelKeyPress`**：Windows 上不存在 SIGINT 等價概念；現有架構 cross-platform 但 REPL 已假設互動 TTY，不需引入額外抽象。

## 測試門檻

- 整合測試：模擬 dispatch 觸發取消（直接呼叫 CTS.Cancel）時，`StartCommand` stderr 輸出 `TWITCH_OAUTH_CANCELLED`、return `1`。不依賴實體 console。
- 整合測試：`OperationCanceledException` 非由 REPL token 觸發（例如 HttpListener 5 分鐘 timeout）時，不輸出 `TWITCH_OAUTH_CANCELLED`，輸出既有 timeout 對應錯誤。
- LineEditor Ctrl+C 清 buffer 行為以 unit-level test 覆蓋（注入 IConsole 抽象或在 TTY 模式下手動驗證 — 詳見 16g 規劃）。

## 待回填的審查筆記

- 審查者：
- 日期：
- 決定：
- 後續：
