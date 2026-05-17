# 第 5 階段補充 Spec - CLI 互動式輸入模式（REPL）

> 父計畫：`docs/phases/phase-5-web-signalr-cli/plan.md`
> 父核對清單：`docs/phases/phase-5-web-signalr-cli/todo.md`
> 範圍：任務 16 擴充（新增任務 16g）
> 狀態：草案；尚未實作

---

## 動機

目前 `Vulperonex.Cli`（`src/Hosts/Vulperonex.Cli/Program.cs`）僅支援單次命令呼叫（one-shot），每次都需重新啟動程序、重新建立 `HttpClient`、重新解析 `VULPERONEX_API_URL`。在開發與手動驗證（特別是 `cli-e2e-verification.md` 列出的流程）會明顯拖慢測試節奏：

- 連續驗證多條規則 / 模擬事件時需反覆敲 `dotnet run --project ... -- rule list`。
- 缺少指令歷史，無法用 ↑/↓ 重跑上一次的 simulate。
- 缺少 Tab 自動補全，使用者必須先記住所有子指令。
- 缺少互動式 help，無法快速列出可用命令。

補上 REPL 後可一次啟動、多次發送請求，並覆寫 OAuth 流程的等待 UX，使第 5 階段 CLI 真正成為「手動驗證主介面」。

---

## 範圍

- 新增互動式輸入模式，啟動方式：
  - `vulperonex` 不帶任何參數 → 進入 REPL。
  - `vulperonex --interactive`（別名 `-i`）→ 顯式進入 REPL（保留語義以便未來與其他全域旗標組合）。
  - 既有 `vulperonex <command> ...` 單次呼叫行為**不得改變**，現有 `CliCommandTests` 必須全綠。
- REPL 共用既有的 `DispatchAsync` 路由，**禁止**為 REPL 另寫一份命令對照表（單一事實來源）。
- 提供下列 REPL 內建命令（不打 API）：
  - `help` / `?`：列出可用一級命令與其子命令。
  - `exit` / `quit` / EOF（Windows: Ctrl+Z then Enter；Unix: Ctrl+D）：離開 REPL，回傳 exit code 0。
  - 空白行：忽略，僅重印 prompt。
- 支援：
  - **指令歷史**：↑/↓ 在 session 內前後切換最近輸入；不持久化到磁碟。
  - **Tab 自動補全**：一級命令（`rule|config|member|simulate|twitch|help|exit`）與已知子命令（`rule list|show|enable|disable|delete` 等）。
  - **非互動式 stdin 後備**：當 `Console.IsInputRedirected == true`（pipe / redirect），降級為逐行讀取迴圈，不啟用按鍵處理；用於整合測試與 `echo cmd | vulperonex` 場景。實作以 `await Task.Run(() => reader.ReadLine(), ct)` 包裝，避免同步阻塞無視 `CancellationToken`；`reader` 為 `RunAsync` 注入的 `TextReader`（測試可餵 `StringReader`）。
- 取消鍵：Ctrl+C 中斷目前 REPL 行（清空 buffer 重印 prompt）；連續第二次 Ctrl+C 或無 buffer 時退出整個 REPL。
  - 實作機制：REPL 啟動時設 `Console.TreatControlCAsInput = true`，按鍵迴圈以 `ConsoleKey.C + ConsoleModifiers.Control` 偵測（不依賴 `Console.CancelKeyPress`，避免該事件預設殺 process 的競態）。離開 REPL 時 `try/finally` 還原 `TreatControlCAsInput = false`。
  - `IsInputRedirected == true` 路徑不啟用此設定（無 TTY），由 stdin EOF 自然結束。

### 非範圍（Out of Scope）

- 不實作多行輸入 / heredoc。
- 不實作 shell-style 引號跳脫（仍以單純 `' '` 切分；若使用者要送含空白的參數值，第 5 階段照舊使用單次模式或等待後續 spec）。
- 不持久化歷史到檔案。
- 不實作 fuzzy / 模糊補全；僅 prefix 比對。
- 不引入第三方套件（Spectre.Console、System.CommandLine、ReadLine.NET 皆**不採用**）。所有 REPL 行為以 BCL `Console` 實作。

---

## 設計

### 程式碼結構

> 參考 `ref/Omni-Commander/OmniCommander.Application/CLI/` 與 `ref/Omni-Commander/OmniCommander.WebApi/Services/ConsoleCliService.cs`。完整採用 `IConsoleCommand` / `CompositeConsoleCommand` / `ICommandDispatcher` 三層抽象（遞迴命令樹）以降低耦合，便於未來新增 `lottery`、`overlay`、`plugin` 等命令族而無需動到 dispatcher 或 REPL 迴圈。

#### 命令抽象（共用於 one-shot 與 REPL）

新增以下類型於 `src/Hosts/Vulperonex.Cli/Commands/`（單一事實來源，one-shot 與 REPL 皆走此樹）：

```
src/Hosts/Vulperonex.Cli/
  Program.cs                       // 改為：建構命令樹 → 偵測 args 決定 one-shot vs REPL
  Commands/
    IConsoleCommand.cs             // Name / Aliases / Description / ExecuteAsync / GetSuggestions
    ICommandDispatcher.cs          // DispatchAsync(input, ct) + GetSuggestions(input)
    CommandDispatcher.cs           // 頂層調度器；遞迴呼叫 sub-command 的 GetSuggestions
    CompositeConsoleCommand.cs     // 抽象基底；持有 _subCommands；遞迴 Execute / Suggest
    Rule/RuleCommand.cs            // Composite：list/show/enable/disable/delete
    Config/ConfigCommand.cs        // Composite：get/set
    Member/MemberCommand.cs        // Composite：list/show
    Simulate/SimulateCommand.cs    // Composite：chat/follow/sub
    Twitch/TwitchCommand.cs        // Composite：auth (再 Composite：start)
    Builtins/HelpCommand.cs        // 純本地；不打 API
    Builtins/ExitCommand.cs        // 純本地；觸發 REPL 退出旗標
  Repl/
    InteractiveSession.cs          // REPL 主迴圈：呼叫 LineEditor → ICommandDispatcher.DispatchAsync
    LineEditor.cs                  // 按鍵處理（↑/↓ 歷史、Tab 補全、Backspace、Ctrl+C）
  Infrastructure/
    CliHttpContext.cs              // 包 HttpClient + TextWriter output/error，由 Command 建構子注入
    TwitchAuthStatusProbe.cs       // 啟動時與 twitch 命令前查詢狀態端點
```

#### 關鍵介面

```csharp
public interface IConsoleCommand
{
    string Name { get; }
    string[] Aliases => Array.Empty<string>();
    string Description { get; }
    Task<int> ExecuteAsync(string triggerName, string[] args, CancellationToken ct);
    IReadOnlyList<string> GetSuggestions(string[] args);
}

public interface ICommandDispatcher
{
    Task<int> DispatchAsync(string input, CancellationToken ct);
    IReadOnlyList<string> GetSuggestions(string input);
    IEnumerable<IConsoleCommand> GetAllCommands();
}

public abstract class CompositeConsoleCommand : IConsoleCommand, ICommandDispatcher
{
    protected readonly List<IConsoleCommand> SubCommands = [];
    protected void AddSubCommand(IConsoleCommand command);
    // Execute：args.Length == 0 印子指令表；否則遞迴 ExecuteAsync
    // GetSuggestions：args 長度 <=1 列出符合前綴的子命令；否則遞迴
    // DispatchAsync：split → ExecuteAsync
}

public sealed class ReplExitToken
{
    public bool ExitRequested { get; private set; }
    public void RequestExit() => ExitRequested = true;
}
```

- 移除 Omni-Commander 的 `Category` 欄位（YAGNI；本階段 `help` 不分組，所有命令平鋪列出）。Aliases 第 5 階段命令暫無設定（空陣列）；保留欄位以利未來擴充（例：`rm` → `delete`）。
- `ExitCommand` 透過建構子注入 `ReplExitToken`，`ExecuteAsync` 呼叫 `RequestExit()` 後回 `0`。REPL 迴圈每輪結束檢查 `token.ExitRequested` 決定是否結束。**禁止**用 sentinel exit code 或拋例外表達退出。
- One-shot 命令樹**不註冊** `ExitCommand` / `HelpCommand`（兩者僅 REPL 路徑需要）；`vulperonex exit` 走一般「未知命令」流程回 `UNKNOWN_COMMAND` exit 1。

> 演算法照搬 `ref/Omni-Commander/OmniCommander.Application/CLI/CommandDispatcher.cs` 的 `endsWithSpace` 判斷與 `prefix` 重組規則。`ExecuteAsync` 回傳 `int` 而非 `Task`（Omni-Commander 版回 `void` Task），讓 one-shot 模式能直接以 root dispatcher 回傳 exit code。

#### 與既有 `VulperonexCli.DispatchAsync` 的整合（破壞性重構）

- `VulperonexCli` 內現有 `switch` 分發**移除**，改為建構 root `CommandDispatcher` 並以 `string.Join(' ', args)` 餵入 `DispatchAsync`。
- HTTP 呼叫、JSON pretty-print、錯誤碼透傳改為各葉節點 command 內呼叫共用 `CliHttpContext.WriteResponseAsync`（從現有 `WriteResponseAsync` 抽出，行為不變）。
- 既有 `CliCommandTests` 透過 `VulperonexCli.RunAsync(args, client, output, error)` 入口呼叫；該入口簽名維持不變，僅內部改為走命令樹，測試斷言不需修改。
- `twitch auth start` 的 OAuth 等待迴圈（`HttpListener`）封裝在 `TwitchAuthStartCommand.ExecuteAsync`；REPL prompt 在該命令期間暫停。
- **REPL 內取消授權**：`TwitchAuthStartCommand.ExecuteAsync` 必須接收外層 `CancellationToken`，並以 `listener.GetContextAsync().WaitAsync(ct)` 傳入；REPL 為該命令建立 `CancellationTokenSource`，使用者按 Ctrl+C 時 `cts.Cancel()` → `WaitAsync` 拋 `OperationCanceledException`，`finally` 區塊呼叫 `listener.Stop()` 並印 `TWITCH_OAUTH_CANCELLED` 至 stderr，prompt 返回。One-shot 模式照舊 5 分鐘逾時或瀏覽器回呼。
- **REPL 內 `--no-browser` 語意**：`twitch auth start --no-browser` 與 one-shot 相同 — 不開瀏覽器、不啟 `HttpListener`、僅印 `authorizeUrl` 供使用者手動開啟。使用者必須**另開一個** `vulperonex twitch auth complete <state> <code>`（若有此命令）或 one-shot 完成；REPL 本身不支援接收 callback。Banner 文案明確區分兩種 UX（見 banner 表格 row 2）。

**已知行為變更（顯式列出）：**

1. `vulperonex`（空 args）原本回 `UNKNOWN_COMMAND` exit 1；改為進 REPL（TTY）或讀 stdin 行（redirected）。受影響：無既有自動化測試覆蓋此分支，但仍是公開行為變更，CHANGELOG 與 `cli-e2e-verification.md` 須註記。
2. `vulperonex --interactive` / `-i`：先前會被當作未知命令；新行為由 `Program.cs` 在進入命令樹**前**剝離旗標。Parse 規則：`args[0]` 為 `--interactive`、`-i` → 設旗標、丟棄 args[0]、剩餘 args 必須為空（否則 stderr `INVALID_ARGS` exit 1），進 REPL。其他位置出現此旗標視為一般 token。
3. `ExitCommand` / `HelpCommand` 僅註冊於 REPL 命令樹；one-shot 樹不含。

### 啟動前置檢查（Twitch 設定 / OAuth 狀態）

REPL 進入後、首個 prompt 印出**前**，呼叫狀態端點，依結果在歡迎訊息下方加 banner。狀態端點為新增：

#### 新後端端點：`GET /api/twitch/auth/status`

回應 200：

```json
{
  "clientIdConfigured": true,
  "hasRefreshToken": false
}
```

實作要點（`src/Hosts/Vulperonex.Web/Endpoints/TwitchAuthEndpoints.cs`）：

```csharp
group.MapGet("/status", async (
    IConfiguration configuration,
    IOAuthTokenStore tokenStore,
    CancellationToken ct) =>
{
    var clientIdConfigured = !string.IsNullOrWhiteSpace(configuration["Twitch:ClientId"]);
    var hasRefreshToken = await tokenStore.HasRefreshTokenAsync("twitch", ct);
    return Results.Ok(new { clientIdConfigured, hasRefreshToken });
});
```

- `IOAuthTokenStore` 若無 `HasRefreshTokenAsync`，於 Application 層新增；實作以「`GetRefreshTokenAsync` 結果非空」判定，**不**回傳 token 本身（避免在 status 端點外洩明文）。
- 不需 auth；loopback 限制已由 host 強制。
- 加進 `Phase5EndpointTests`：驗證 `clientIdConfigured` 兩種狀態 + `hasRefreshToken` 兩種狀態的 4 組組合。

#### REPL 啟動 Banner 規則

呼叫 `GET /api/twitch/auth/status` 結果分支：

| `clientIdConfigured` | `hasRefreshToken` | Banner |
|---|---|---|
| false | * | `[WARN] Twitch:ClientId 未設定。請於 appsettings.json 或環境變數 Twitch__ClientId 設定後重啟 Web host。` |
| true | false | `[WARN] Twitch OAuth 尚未授權。輸入 'twitch auth start' 開始授權流程（或 'twitch auth start --no-browser' 取得授權連結手動開啟）。` |
| true | true | （不印任何 banner） |

- **不**在 banner 預建 `authorize_url`。原因：REPL 啟動時若預先呼叫 `POST /api/twitch/auth/start` 取 URL，會把 `redirect_uri` 綁到固定 callback port，但 REPL 未啟 `HttpListener`，使用者點該 URL 後瀏覽器將收到 connection refused；且後續使用者執行 `twitch auth start` 會建新 state + 新 PKCE verifier，預建 URL 永久作廢。Banner 僅提示命令，由 `twitch auth start` 命令本身負責建 session 與啟 listener。
- 連帶結論：`TwitchOAuthSessionStore` 不會被 REPL 啟動流程額外佔用 slot（無記憶體膨脹風險）。
- 若狀態端點本身失敗（HTTP 5xx / 連線失敗 / 逾時）：印 `[WARN] 無法取得 Twitch 狀態（<error_code>）。` 並繼續進入 REPL（不阻斷）。狀態 probe **共用** `RunAsync` 注入的 `HttpClient`（同一 stub 可攔截，整合測試可控），timeout 以 `using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt); cts.CancelAfter(TimeSpan.FromSeconds(2));` 配合 `client.SendAsync(request, cts.Token)` 實作；**不**修改 `HttpClient.Timeout` 全域屬性，避免污染後續命令。
- `twitch auth start` 命令 `ExecuteAsync` 入口處再次檢查狀態：若 `clientIdConfigured == false`，直接印 `TWITCH_CLIENT_ID_MISSING` 至 stderr 並指示設定方式，不送 `POST /api/twitch/auth/start`。
- One-shot 模式（`vulperonex twitch auth start`）行為**不變**：直接打 API，依後端回的錯誤碼透傳。Banner 僅 REPL 使用。

#### 安全考量

- `clientIdConfigured` / `hasRefreshToken` 為 boolean，**不**回傳 client_id 字串或 token 摘要，避免在 loopback log 留下可辨識資料。
- Banner 不含 authorize URL（見上節），因此無 `client_id` / `state` 印出。
- **FYI（後續階段）**：`/api/twitch/auth/status` 揭露「機器是否已授權」事實。Phase 5 受 loopback 限制（`plan.md` 規定）可接受；若未來開放 LAN/遠端繫結（plan.md 已警告），此端點需與 hub 一同加 auth gate。

### Prompt 與輸出

- Prompt 字串：`vulperonex> `（ASCII，避免 Windows code page 顯示問題）。
- 進入時印一行歡迎（繁中與 plan.md / banner 風格一致）：`歡迎使用 Vulperonex CLI 互動模式。輸入 'help' 列出指令，'exit' 離開。`
- 命令輸出與 one-shot 完全一致（同樣的 JSON pretty-print / stderr error code），方便使用者複製貼上做對比驗證。

---

## 驗收標準

- [ ] `vulperonex`（無參數，互動式 TTY）進入 REPL；列印歡迎訊息與第一個 prompt。
- [ ] `vulperonex --interactive` 與 `vulperonex -i` 行為同上。
- [ ] `vulperonex rule list`（既有單次模式）行為與輸出完全未變；`CliCommandTests` 不需修改即通過。
- [ ] REPL 中輸入 `rule list` 走完整 HTTP 路徑並印出與單次模式相同的 JSON。
- [ ] REPL 中後端錯誤碼（如 `OAUTH_CREDENTIAL_NAMESPACE`）寫到 stderr、stdout 為空、prompt 繼續可用，**不**結束 REPL。
- [ ] REPL 啟動時呼叫 `GET /api/twitch/auth/status`：
  - `clientIdConfigured == false` → 印 ClientId 未設定 banner，提示設定路徑。
  - `clientIdConfigured && !hasRefreshToken` → 印 OAuth 未授權 banner，指示執行 `twitch auth start`（**不**含 authorize URL，原因見設計段）。
  - `clientIdConfigured && hasRefreshToken` → 不印 banner。
  - 端點失敗 → 印警告 + 錯誤碼，繼續進 REPL。
- [ ] REPL 中執行 `twitch auth start` 前重新檢查狀態；`clientIdConfigured == false` 時不打 `/start`，直接 stderr 印 `TWITCH_CLIENT_ID_MISSING` + 設定提示。
- [ ] 新後端端點 `GET /api/twitch/auth/status` 回 `{ clientIdConfigured, hasRefreshToken }`，不回傳 client_id 字串或 token。
- [ ] 命令樹採用 `IConsoleCommand` / `CompositeConsoleCommand` / `ICommandDispatcher` 完整遞迴抽象；one-shot 與 REPL 共用同一棵樹，**禁止**保留舊 `switch` 分發路徑。
- [ ] `help` 列出所有一級命令與其子命令（透過遞迴 `GetAllCommands` + `Description`）；不打 API。
- [ ] `exit` / `quit` / EOF（Windows: Ctrl+Z+Enter；Unix: Ctrl+D）結束 REPL 並回傳 exit code 0。
- [ ] 空白行 / 純空白輸入：忽略並重印 prompt，不送 API。
- [ ] ↑/↓ 在 session 歷史中前後切換；連續輸入相同命令時，僅當**最後一筆**（push 前比對 `_history.Last()`，非 read 時比對）等於新輸入才去重，中間重複的歷史保留，照搬 Omni-Commander `ConsoleCliService` 行為。
- [ ] Tab：在一級命令位置按下，補出唯一前綴或循環候選；在子命令位置同樣行為。補完葉節點（無子命令）後**不**自動追加空白；補完 Composite 後追加空白以便繼續補子命令。
- [ ] `Console.IsInputRedirected == true` 時降級為 `ReadLine` 迴圈（無按鍵處理），讀到 EOF 結束。
- [ ] REPL 中任何單一命令丟出未預期例外（非 `HttpRequestException` / `CliApiUrlNotLoopbackException`）時，印出 `CLI_UNEXPECTED_ERROR` 至 stderr，REPL 繼續存活（不得讓整個程序崩潰）。
- [ ] 啟動時 `VULPERONEX_API_URL` 非 loopback → REPL **不啟動**，行為等同單次模式：stderr 印 `CLI_API_URL_NOT_LOOPBACK`、exit 1。
- [ ] 整個 REPL 共用單一 `HttpClient` 實例。

### 新錯誤碼

| 代碼 | 觸發點 | 備註 |
|------|--------|------|
| `CLI_UNEXPECTED_ERROR` | REPL 內 `DispatchAsync` 拋出未分類例外 | 僅在 REPL 路徑使用；`CLI_` 前綴明示來自 client，不混淆 `src/Hosts/Vulperonex.Web/Errors/ErrorCodes.cs` 字典 |
| `INVALID_ARGS` | `--interactive` / `-i` 旗標後仍有殘餘 args | 用於 one-shot 入口的旗標 parse；不進命令樹 |
| `TWITCH_OAUTH_CANCELLED` | REPL 內 `twitch auth start` 等待 callback 期間使用者按 Ctrl+C | 僅 CLI 端使用；`HttpListener` 被 `cts.Cancel()` 中止後印出 |

> 一次性命令模式遇到未分類例外時，沿用既有 `throw` → process exit 行為，**不**新增此碼。

---

## 驗證

- [ ] **單元/整合測試**（`tests/Vulperonex.Tests.Integration/Cli/`）新增：
  - `Given_NoArgs_When_StdinIsRedirectedWithCommands_Then_DispatchesEachLine`：以 `StringReader` 餵 `"rule list\nexit\n"`，斷言兩次 HTTP 呼叫、最終 exit 0。
  - `Given_ReplLine_When_ApiReturnsError_Then_StderrGetsCodeAndReplContinues`：餵 `"config get oauth.x\nexit\n"`，斷言 stderr 出現 `OAUTH_CREDENTIAL_NAMESPACE` 且第二行 `exit` 仍被處理。
  - `Given_HelpCommand_When_Executed_Then_ListsKnownCommandsAndDoesNotCallApi`：注入會 fail 的 `HttpClient`，確認 `help` 不打 API。
  - `Given_BlankLines_When_Entered_Then_DispatchNotCalled`。
  - `Given_NonLoopbackBaseUrl_When_NoArgs_Then_ReplDoesNotStart`。
  - `Given_ReplStart_When_ClientIdMissing_Then_BannerPrintedAndAuthStartNotProbed`：stub status 端點回 `{ clientIdConfigured: false }`，斷言 banner 含設定提示且未呼叫 `/api/twitch/auth/start`。
  - `Given_ReplStart_When_OAuthNotAuthorized_Then_BannerInstructsAuthStartCommandWithoutCallingStart`：stub status 回 `{ clientIdConfigured: true, hasRefreshToken: false }`，斷言 banner 含 `twitch auth start` 字串、**未**呼叫 `POST /api/twitch/auth/start`。
  - `Given_ReplStart_When_FullyAuthorized_Then_NoBanner`。
  - `Given_TwitchAuthStartInRepl_When_ClientIdMissing_Then_StderrCodeWithoutCallingStartEndpoint`。
  - `Given_CommandTreeAbstraction_When_HelpExecuted_Then_OutputsAllRegisteredCompositesRecursively`（保護遞迴抽象）。
  - `Given_InteractiveFlag_When_FollowedByExtraArgs_Then_StderrInvalidArgs`。
  - `Given_ExitCommand_When_Executed_Then_ReplExitTokenIsRequested`（直接斷言 token 狀態，不依賴 stdin EOF）。
  - `Given_TwitchAuthStartInRepl_When_CtrlCDuringWait_Then_ListenerClosedAndStderrCancelled`：模擬 listener 啟動後外層 ct 被 cancel，斷言印出 `TWITCH_OAUTH_CANCELLED` 且 `HttpListener` 已 `Stop()`（手動驗證亦覆蓋）。
  - `Given_StatusProbe_When_ApiSlow_Then_TimesOutAt2sAndPrintsWarningBanner`：stub 端點 sleep 5s，斷言 2s 內回 banner、未阻塞後續輸入。
  - 既有 `CliCommandTests` 全部維持綠燈（回歸保護）。
- [ ] **Web 整合測試**（`tests/Vulperonex.Tests.Integration/Web/Phase5EndpointTests.cs` 或新檔）：
  - `Given_StatusEndpoint_When_NoClientId_Then_ClientIdConfiguredFalse`。
  - `Given_StatusEndpoint_When_ClientIdSetAndNoToken_Then_ClientIdConfiguredTrueAndHasRefreshTokenFalse`。
  - `Given_StatusEndpoint_When_ClientIdSetAndTokenStored_Then_BothTrue`。
  - `Given_StatusEndpoint_When_Called_Then_ResponseDoesNotContainClientIdOrTokenStrings`（防洩漏）。
- [ ] **手動驗證**：新增條目於 `docs/phases/phase-5-web-signalr-cli/manual-verification.md`，覆蓋 TTY 下的 ↑/↓、Tab、Ctrl+C 行為（無法以整合測試自動驗證按鍵）。
  - 環境：Windows Terminal、PowerShell 7、cmd.exe 各驗證一次（最低門檻：Windows Terminal）。
- [ ] **架構檢查**：`Vulperonex.Cli.csproj` 不得新增任何 `<PackageReference>`；允許 `<Folder Include>` / 檔案 glob 等結構性變動。
- [ ] **OpenAPI**：新增 `Given_OpenApi_When_Fetched_Then_ContainsTwitchAuthStatusEndpoint`，斷言 `/openapi/v1.json` 包含 `/api/twitch/auth/status` 路徑。
- [ ] **回歸**：`cli-e2e-verification.md` 列出的單次命令仍可獨立執行，並在該文件追加交叉引用「REPL 流程見 `supplemental-cli-repl.md`」。

---

## 可能涉及的檔案

CLI（重構 + 新增）：
- `src/Hosts/Vulperonex.Cli/Program.cs`（移除 `switch` 分發，改建構命令樹）
- `src/Hosts/Vulperonex.Cli/Commands/IConsoleCommand.cs`（新）
- `src/Hosts/Vulperonex.Cli/Commands/ICommandDispatcher.cs`（新）
- `src/Hosts/Vulperonex.Cli/Commands/CommandDispatcher.cs`（新）
- `src/Hosts/Vulperonex.Cli/Commands/CompositeConsoleCommand.cs`（新）
- `src/Hosts/Vulperonex.Cli/Commands/{Rule,Config,Member,Simulate,Twitch}/*Command.cs`（新；遷移自既有 `RuleAsync` 等私有方法）
- `src/Hosts/Vulperonex.Cli/Commands/Builtins/{HelpCommand,ExitCommand}.cs`（新）
- `src/Hosts/Vulperonex.Cli/Infrastructure/CliHttpContext.cs`（新；包 `HttpClient` 與 IO writer）
- `src/Hosts/Vulperonex.Cli/Infrastructure/TwitchAuthStatusProbe.cs`（新）
- `src/Hosts/Vulperonex.Cli/Repl/InteractiveSession.cs`（新）
- `src/Hosts/Vulperonex.Cli/Repl/LineEditor.cs`（新）

Web（新端點）：
- `src/Hosts/Vulperonex.Web/Endpoints/TwitchAuthEndpoints.cs`（追加 `GET /status`）
- `src/Vulperonex.Application/Auth/IOAuthTokenStore.cs`（追加 `HasRefreshTokenAsync`）
- `src/Vulperonex.Infrastructure/Auth/OAuthTokenStore.cs`（實作 `HasRefreshTokenAsync`：呼叫 `GetRefreshTokenAsync` 後判 `!string.IsNullOrWhiteSpace`，避免明文 token 回傳給端點層）

測試 / 文件：
- `tests/Vulperonex.Tests.Integration/Cli/CliReplTests.cs`（新）
- `tests/Vulperonex.Tests.Integration/Web/Phase5EndpointTests.cs`（追加 status 端點測試）
- `docs/phases/phase-5-web-signalr-cli/manual-verification.md`（追加條目）
- `docs/phases/phase-5-web-signalr-cli/todo.md`（追加任務 16g）

---

## 規模

M（單一 host 專案內擴充；命令路由共用既有 `DispatchAsync`，無 API 變動）。

---

## 任務 16g - CLI 互動式輸入模式

**說明：** 於 `Vulperonex.Cli` 加入 REPL 啟動分支（無參數或 `--interactive` / `-i`），共用既有 `DispatchAsync`，支援指令歷史、Tab 補全、`help` / `exit`、stdin 重新導向後備、Ctrl+C 行為，並涵蓋整合測試與手動驗證條目。

**規模：** M

**驗收：** 同上「驗收標準」段落。

**驗證：** 同上「驗證」段落。

---

## 參考實作（Omni-Commander）

關鍵檔案：

- `ref/Omni-Commander/OmniCommander.WebApi/Services/ConsoleCliService.cs`
  - `ReadLineWithCompletionAsync`：`Console.IsInputRedirected` 後備、↑/↓ 歷史、Tab 循環補全、CJK 寬度修正的 Backspace。Vulperonex MVP **不需要** CJK 寬度處理（命令名與子命令皆 ASCII），可簡化該段。
- `ref/Omni-Commander/OmniCommander.Application/CLI/CommandDispatcher.cs`
  - `GetSuggestions(string input)`：依「是否以空白結尾」判斷在補一級或子級命令的演算法。直接借用。
- `ref/Omni-Commander/OmniCommander.Application/CLI/CompositeConsoleCommand.cs`
  - 子命令遞迴調度與補全。**完整採用**：以 `IConsoleCommand` + `CompositeConsoleCommand` 表達整棵樹（含 `twitch auth start` 這類 3 層深度），未來新增 `lottery export csv` / `plugin enable <id>` 等命令不需改 dispatcher。

**差異說明：** Omni-Commander 將 dispatcher 註冊到 DI 容器並由 `BackgroundService` 持續執行。Vulperonex CLI 為獨立 console host，**不**引入 generic host / `Microsoft.Extensions.Hosting`；命令樹由 `Program.cs` 手動 `new` 並組裝（明確相依，無 DI 反射），`HttpClient` 與 IO writer 透過 `CliHttpContext` 建構子注入到各 Command。`ExecuteAsync` 回傳 `int` 以保留 one-shot exit code 語意。
