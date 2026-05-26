# Phase 5 Supplemental Spec - CLI Interactive Input Mode (REPL)

> Parent Plan: `docs/phases/phase-5-web-signalr-cli/plan.md`
> Parent Checklist: `docs/phases/phase-5-web-signalr-cli/todo.md`
> Scope: Task 16 extension (adding Task 16g)
> Status: Draft; pending implementation

---

## Motivation

Currently, `Vulperonex.Cli` (`src/Hosts/Vulperonex.Cli/Program.cs`) only supports one-shot command invocations. Each invocation requires restarting the process, recreating the `HttpClient`, and re-resolving the `VULPERONEX_API_URL`. During development and manual verification (especially the processes listed in `cli-e2e-verification.md`), this significantly slows down the testing pace:

- Verifying multiple rules or simulating events consecutively requires repeatedly typing `dotnet run --project ... -- rule list`.
- Lacks command history, preventing users from using ↑/↓ to re-run the last simulation.
- Lacks Tab autocomplete, forcing users to memorize all subcommands beforehand.
- Lacks interactive help, preventing users from quickly listing available commands.

Adding a REPL allows starting once and sending requests multiple times, while overriding the waiting UX of the OAuth flow, making the Phase 5 CLI truly the "primary interface for manual verification."

---

## Scope

- Add an interactive input mode, launched via:
  - `vulperonex` without any arguments → enters the REPL.
  - `vulperonex --interactive` (alias `-i`) → explicitly enters the REPL (preserving semantics for future combinations with other global flags).
  - Existing `vulperonex <command> ...` one-shot invocation behaviors **must not change**; existing `CliCommandTests` must pass 100% green.
- The REPL shares the existing `DispatchAsync` routing. **Do not** write a separate command mapping table for the REPL (Single Source of Truth).
- Provide the following REPL built-in commands (do not call the API):
  - `help` / `?`: lists available first-level commands and their subcommand summaries.
  - `exit` / `quit` / EOF (Windows: Ctrl+Z then Enter; Unix: Ctrl+D): exits the REPL, returning exit code 0.
  - Blank lines: ignored, reprinting the prompt only.
- Support:
  - **Command History**: ↑/↓ switches back and forth between recent inputs within the session; not persisted to disk.
  - **Tab Autocomplete**: first-level commands (`rule|config|member|simulate|twitch|help|exit`) and known subcommands (`rule list|show|create|update|enable|disable|delete`, `member list|show|seed|delete`, `simulate chat|follow|sub`, etc.).
  - **Non-interactive Stdin Fallback**: when `Console.IsInputRedirected == true` (pipe / redirect), downgrade to a line-by-line reading loop without key processing; used for integration tests and `echo cmd | vulperonex` scenarios. Implement this by wrapping `await Task.Run(() => reader.ReadLine(), ct)` to avoid synchronous blocking ignoring the `CancellationToken`. `reader` is the `TextReader` injected into `RunAsync` (tests can feed `StringReader`).
- Cancel key: Ctrl+C interrupts the current REPL line (clears the buffer and reprints the prompt); a second consecutive Ctrl+C or when the buffer is empty exits the entire REPL.
  - Implementation mechanism: when starting the REPL, set `Console.TreatControlCAsInput = true`, detecting key presses using `ConsoleKey.C + ConsoleModifiers.Control` (does not rely on `Console.CancelKeyPress`, preventing race conditions where the event kills the process by default). Restore `TreatControlCAsInput = false` in `try/finally` upon exiting the REPL.
  - `IsInputRedirected == true` paths do not enable this setting (no TTY), terminating naturally on stdin EOF.

### Out of Scope

- Do not implement multi-line inputs / heredoc.
- Do not implement shell-style quote escaping (still splits by simple `' '`; if users want to send argument values containing spaces, Phase 5 continues to use one-shot mode or waits for subsequent specs).
- Do not persist history to files.
- Do not implement fuzzy completion; prefix matching only.
- Do not introduce third-party packages (Spectre.Console, System.CommandLine, and ReadLine.NET are **not adopted**). All REPL behaviors are implemented using BCL `Console`.

---

## Design

### Code Structure

> Refer to `ref/Omni-Commander/OmniCommander.Application/CLI/` and `ref/Omni-Commander/OmniCommander.WebApi/Services/ConsoleCliService.cs`. Adopt the three-tier abstraction (`IConsoleCommand` / `CompositeConsoleCommand` / `ICommandDispatcher` - recursive command tree) to reduce coupling, facilitating future additions of command groups like `lottery`, `overlay`, or `plugin` without touching the dispatcher or REPL loop.

#### Command Abstraction (Shared between One-shot and REPL)

Add the following types under `src/Hosts/Vulperonex.Cli/Commands/` (Single Source of Truth; both one-shot and REPL route through this tree):

```
src/Hosts/Vulperonex.Cli/
  Program.cs                       // Modified to: build command tree → detect args to decide one-shot vs REPL
  Commands/
    IConsoleCommand.cs             // Name / Aliases / Description / ExecuteAsync / GetSuggestions
    ICommandDispatcher.cs          // DispatchAsync(input, ct) + GetSuggestions(input)
    CommandDispatcher.cs           // Root dispatcher; recursively invokes sub-command GetSuggestions
    CompositeConsoleCommand.cs     // Abstract base; holds _subCommands; recursively Executes / Suggests
    Rule/RuleCommand.cs            // Composite: list/show/create/update/enable/disable/delete
    Config/ConfigCommand.cs        // Composite: get/set
    Member/MemberCommand.cs        // Composite: list/show/seed/delete
    Simulate/SimulateCommand.cs    // Composite: chat/follow/sub
    Twitch/TwitchCommand.cs        // Composite: auth (which is Composite: start/reset)
    Builtins/HelpCommand.cs        // Local-only; does not call API
    Builtins/ExitCommand.cs        // Local-only; triggers REPL exit flag
  Repl/
    InteractiveSession.cs          // REPL main loop: calls LineEditor → ICommandDispatcher.DispatchAsync
    LineEditor.cs                  // Key press processing (↑/↓ history, Tab autocomplete, Backspace, Ctrl+C)
  Infrastructure/
    CliHttpContext.cs              // Wraps HttpClient + TextWriter output/error, injected by Command constructors
    TwitchAuthStatusProbe.cs       // Queries status endpoints on startup and before twitch commands
```

#### Key Interfaces

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
    // Execute: prints sub-command table when args.Length == 0; recursively calls ExecuteAsync otherwise
    // GetSuggestions: lists matching sub-commands when args length <=1; recursively calls otherwise
    // DispatchAsync: split → ExecuteAsync
}

public sealed class ReplExitToken
{
    public bool ExitRequested { get; private set; }
    public void RequestExit() => ExitRequested = true;
}
```

- Removed Omni-Commander's `Category` field (YAGNI; `help` does not categorize in this phase, listing all commands flatly). Aliases are not configured for Phase 5 commands (empty array); retained for future extensions (e.g. `rm` → `delete`).
- `ExitCommand` injects `ReplExitToken` via its constructor, calling `RequestExit()` in `ExecuteAsync` and returning `0`. The REPL loop checks `token.ExitRequested` at the end of each round to decide whether to terminate. **Do not** utilize sentinel exit codes or throw exceptions to express exits.
- One-shot command trees **do not register** `ExitCommand` / `HelpCommand` (both are required strictly for REPL paths); `vulperonex exit` routes through standard "unknown command" flows, returning `UNKNOWN_COMMAND` exit 1.

> The algorithm mimics the `endsWithSpace` check and `prefix` recombination rules in `ref/Omni-Commander/OmniCommander.Application/CLI/CommandDispatcher.cs`. `ExecuteAsync` returns `int` instead of `Task` (Omni-Commander returns `void` Task), allowing one-shot mode to return exit codes directly via the root dispatcher.

#### Integration with Existing `VulperonexCli.DispatchAsync` (Breaking Refactoring)

- The existing `switch` dispatching inside `VulperonexCli` is **removed**, replaced by constructing the root `CommandDispatcher` and feeding `string.Join(' ', args)` to `DispatchAsync`.
- HTTP calls, JSON pretty-printing, and error code passthroughs are refactored to invoke the shared `CliHttpContext.WriteResponseAsync` inside each leaf node command (extracted from the existing `WriteResponseAsync` with unchanged behavior).
- Existing `CliCommandTests` route through the `VulperonexCli.RunAsync(args, client, output, error)` entry point. This entry point signature remains unchanged, with only internal dispatching routing through the command tree, avoiding the need to modify test assertions.
- The OAuth waiting loop (`HttpListener`) for `twitch auth start` is encapsulated in `TwitchAuthStartCommand.ExecuteAsync`; the REPL prompt pauses during this command.
- **Canceling Authorization inside the REPL**: `TwitchAuthStartCommand.ExecuteAsync` must accept the outer `CancellationToken`, passing it to `listener.GetContextAsync().WaitAsync(timeout, ct)`. The REPL creates a `CancellationTokenSource` for this command, and when the user presses Ctrl+C, it calls `cts.Cancel()`. This causes `WaitAsync` to throw `OperationCanceledException`. The `finally` block calls `listener.Stop()` and prints `TWITCH_OAUTH_CANCELLED` to stderr, returning the prompt. One-shot mode continues to utilize a 5-minute timeout or browser callbacks.
- **REPL `--no-browser` Semantics**: `twitch auth start --no-browser` behaves identically to one-shot mode — does not open the browser, does not start `HttpListener`, and prints only `authorizeUrl` for the user to open manually. The user must **open another** terminal to run `vulperonex twitch auth complete <state> <code>` (if such a command exists) or complete it via one-shot mode; the REPL itself does not support receiving callbacks. Banner copy clearly distinguishes between the two UX paths (see banner table row 2).

**Known Behavioral Changes (Explicitly Listed):**

1. `vulperonex` (empty args) originally returned `UNKNOWN_COMMAND` exit 1; modified to enter the REPL (TTY) or read stdin lines (redirected). Affected: no existing automated tests cover this branch, but it is a public behavioral change, documented in CHANGELOG and `cli-e2e-verification.md`.
2. `vulperonex --interactive` / `-i`: previously treated as an unknown command; new behavior strips flags in `Program.cs` **before** entering the command tree. Parse rule: if `args[0]` is `--interactive` or `-i` → set the flag, discard args[0], and assert that remaining args must be empty (otherwise print `INVALID_ARGS` to stderr and exit with 1), entering the REPL. The flag in other positions is treated as a standard token.
3. `ExitCommand` / `HelpCommand` are registered strictly in the REPL command tree; the one-shot tree does not contain them.

### Pre-startup Checks (Twitch Configurations / OAuth Status)

After entering the REPL and **before** printing the first prompt, query the status endpoint, adding a banner below the welcome message based on results. The status endpoint is newly added:

#### New Backend Endpoint: `GET /api/twitch/auth/status`

Returns 200:

```json
{
  "clientIdConfigured": true,
  "hasRefreshToken": false
}
```

Implementation details (`src/Hosts/Vulperonex.Web/Endpoints/TwitchAuthEndpoints.cs`):

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

- If `IOAuthTokenStore` lacks `HasRefreshTokenAsync`, add it in the Application layer; implementation asserts "whether `GetRefreshTokenAsync` result is non-empty," **not** returning the token itself (preventing plaintext token leakage in the status endpoint).
- No auth required; loopback restrictions are already enforced by the host.
- Added to `Phase5EndpointTests`: verifies 4 combinations of both `clientIdConfigured` states and `hasRefreshToken` states.

#### REPL Startup Banner Rules

Querying `GET /api/twitch/auth/status` branches into:

| `clientIdConfigured` | `hasRefreshToken` | Banner |
|---|---|---|
| false | * | `[WARN] Twitch:ClientId is not configured. Restart the Web host after setting it in appsettings.json or the environment variable Twitch__ClientId.` |
| true | false | `[WARN] Twitch OAuth is not authorized. Enter 'twitch auth start' to begin the authorization flow (or 'twitch auth start --no-browser' to retrieve the link for manual authorization).` |
| true | true | (Do not print any banner) |

- **Do not** pre-generate `authorize_url` in the banner. Reason: if the REPL queries `POST /api/twitch/auth/start` on startup to fetch the URL, it binds `redirect_uri` to a fixed callback port, but the REPL has not started `HttpListener`, causing connection refused when the user clicks the URL. Furthermore, subsequent runs of `twitch auth start` establish new states and new PKCE verifiers, permanently invalidating the pre-generated URL. The banner strictly prompts commands, leaving session creation and listener startup to the `twitch auth start` command itself.
- Connected conclusion: `TwitchOAuthSessionStore` will not be occupied by the REPL startup flow (no memory bloat risks).
- If the status endpoint itself fails (HTTP 5xx / connection failure / timeout): print `[WARN] Failed to retrieve Twitch status (<error_code>).` and continue into the REPL (does not block). Status probes **share** the `HttpClient` injected by `RunAsync` (interceptable by the same stub, controllable by integration tests), with timeouts implemented via `using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt); cts.CancelAfter(TimeSpan.FromSeconds(2));` in `client.SendAsync(request, cts.Token)`. **Do not** modify global `HttpClient.Timeout` properties, avoiding pollution of subsequent commands.
- `twitch auth start` command's `ExecuteAsync` entry point checks the state again: if `clientIdConfigured == false`, print `TWITCH_CLIENT_ID_MISSING` and configuration instructions directly to stderr, without calling `POST /api/twitch/auth/start`.
- One-shot mode (`vulperonex twitch auth start`) behaviors **remain unchanged**: call the API directly, passing through errors returned by the backend. The banner is utilized strictly in the REPL.
- `twitch auth reset` / `clear` / `logout` calls `DELETE /api/twitch/auth/token`, clearing only the stored refresh token; does not modify `Twitch:ClientId` or `Twitch:ClientSecret` configurations. This command is used to repeat manual verifications of OAuth start/complete flows.

#### Security Considerations

- `clientIdConfigured` / `hasRefreshToken` are booleans, **not** returning client_id strings or token summaries, preventing identifiable data in loopback logs.
- Banners do not contain authorize URLs (see above section), so no `client_id` or `state` is printed.
- **FYI (Subsequent Phases)**: `/api/twitch/auth/status` exposes the fact of "whether the machine is authorized." Phase 5 accepts this under loopback restrictions (dictated by `plan.md`); if LAN/remote bindings are opened in subsequent phases (already warned in `plan.md`), this endpoint must be secured with an auth gate along with the hub.

### CLI Help UX and i18n

- `help` must not print flat debug dumps. Global help categorizes first-level commands, displaying aliases, descriptions, and subcommand summaries.
- When entering composite commands without subcommands, such as `member`, `rule`, `simulate`, or `twitch auth`, display partial help and usage for that command group, rather than returning `UNKNOWN_COMMAND`.
- CLI copy must not be hardcoded in command classes. Command descriptions, usages, category names, and help static copies all utilize i18n keys.
- i18n utilizes file-based loading, supporting external extensions:
  - manifest: `src/Hosts/Vulperonex.Cli/Resources/I18n/manifest.json`
  - locale files: `src/Hosts/Vulperonex.Cli/Resources/I18n/<culture>.json`
  - `manifest.json`'s `supportedCultures` must correspond exactly to actual filenames, e.g. `zh-TW` corresponds to `zh-TW.json`.
  - Adding locales strictly requires adding `<culture>.json` and registering the culture in the manifest; no changes to C# command classes are needed.
  - When a key is missing in the current locale, fallback to `defaultCulture`; if the key is missing in the default locale as well, display the key itself, facilitating discovery of missing keys during development.
- Locale selection order: `VULPERONEX_CLI_LANG` -> `CultureInfo.CurrentUICulture` -> manifest's `defaultCulture`.

### Prompt and Output

- Prompt string: `vulperonex> ` (ASCII, avoiding display issues with Windows code pages).
- Welcome message printed on entry (matching Traditional Chinese and plan.md / banner styles): `Welcome to Vulperonex CLI interactive mode. Enter 'help' to list commands, 'exit' to leave.`
- Command outputs match one-shot modes exactly (identical JSON pretty-printing / stderr error codes), facilitating user copying and pasting for comparative verification.

---

## Acceptance Criteria

- [x] `vulperonex` (no arguments, interactive TTY) enters the REPL; prints a welcome message and the first prompt.
- [x] `vulperonex --interactive` and `vulperonex -i` behave identically.
- [x] `vulperonex rule list` (existing one-shot mode) behaviors and outputs remain entirely unchanged; `CliCommandTests` pass.
- [x] Typing `rule list` in the REPL routes through the full HTTP path and prints the identical JSON to one-shot mode.
- [x] Backend error codes (such as `OAUTH_CREDENTIAL_NAMESPACE`) in the REPL write to stderr, leaving stdout empty, while keeping the prompt active without terminating the REPL.
- [x] The REPL queries `GET /api/twitch/auth/status` on startup:
  - `clientIdConfigured == false` → prints the ClientId missing banner, prompting configuration paths and continuing in no-Twitch mode.
  - `clientIdConfigured && !hasRefreshToken` → prints the OAuth unauthorized banner, prompting execution of `twitch auth start` (does not contain authorize URLs, reason in the design section).
  - `clientIdConfigured && hasRefreshToken` → prints no banner.
  - Endpoint failure → prints warnings + error codes, continuing into the REPL.
- [x] Re-verify state before executing `twitch auth start` in the REPL; do not call `/start` when `clientIdConfigured == false`, printing `TWITCH_CLIENT_ID_MISSING` and setup hints directly to stderr.
- [x] The new backend endpoint `GET /api/twitch/auth/status` returns `{ clientIdConfigured, clientSecretConfigured, hasRefreshToken }`, not returning client_id strings, client secrets, or tokens.
- [x] The command tree utilizes `IConsoleCommand` / `CompositeConsoleCommand` / `ICommandDispatcher` recursive abstractions; one-shot and REPL share the same tree, **forbidding** retaining old `switch` dispatch paths.
- [x] `help` lists all first-level commands and their subcommands (via recursive `GetAllCommands` + `Description`); does not call the API.
- [x] `simulate` without arguments displays partial help for `chat|follow|sub`, no longer returning `UNKNOWN_COMMAND`.
- [x] `rule create <rule.json>` / `rule update <rule-id> <rule.json>` permit creating and updating rules from files, reducing friction during manual verification of rule CRUD.
- [x] `member seed <platform-user-id> [display-name]` creates test members via simulation pipelines; `member delete <member-id>` clears members and platform identities.
- [x] `twitch auth reset` clears stored refresh tokens, facilitating repeated verifications of Twitch OAuth.
- [x] `exit` / `quit` / EOF (Windows: Ctrl+Z+Enter; Unix: Ctrl+D) terminates the REPL, returning exit code 0.
- [x] Blank lines / whitespace-only inputs: ignored, reprinting the prompt without calling the API.
- [x] ↑/↓ switches back and forth in session history; when entering duplicate commands consecutively, deduplicate only when the **last entry** matches the new input, retaining intermediate duplicates, matching `ConsoleCliService` behaviors in Omni-Commander.
- [x] Tab: pressed at first-level command positions completes the unique prefix; identical behavior at subcommand positions. Completing leaf nodes (no subcommands) **does not** automatically append a space; completing Composites appends a space to facilitate subsequent subcommand completions.
- [x] Tab: supports cycling through multiple candidates.
- [x] Downgrade to `ReadLine` loop (no key processing) when `Console.IsInputRedirected == true`, terminating on EOF.
- [x] When any single command inside the REPL throws an unexpected exception (non-`HttpRequestException` / `CliApiUrlNotLoopbackException`), print `CLI_UNEXPECTED_ERROR` to stderr, and the REPL remains active (must not crash the entire process).
- [x] If `VULPERONEX_API_URL` is not loopback on startup → the REPL **does not start**, behaving identically to one-shot mode: prints `CLI_API_URL_NOT_LOOPBACK` to stderr and exits with 1.
- [x] The entire REPL shares a single `HttpClient` instance.

### New Error Codes

| Code | Trigger Point | Remarks |
|------|--------|------|
| `CLI_UNEXPECTED_ERROR` | REPL dispatches throwing unclassified exceptions | Used strictly inside REPL paths; `CLI_` prefix indicates client-side origin, not confusing with `src/Hosts/Vulperonex.Web/Errors/ErrorCodes.cs` dictionaries |
| `INVALID_ARGS` | Residual arguments remain after `--interactive` / `-i` flags | Used for one-shot entry flag parses; does not enter the command tree |
| `TWITCH_OAUTH_CANCELLED` | User presses Ctrl+C while `twitch auth start` waits for callbacks inside the REPL | Used strictly on the CLI side; printed after `HttpListener` is aborted via `cts.Cancel()` |

> When one-shot command modes encounter unclassified exceptions, preserve existing `throw` → process exit behaviors, **not** adding this code.

---

## Verification

- [x] **Unit/Integration Tests** (`tests/Vulperonex.Tests.Integration/Cli/`) additions:
  - `Given_NoArgs_When_StdinIsRedirectedWithCommands_Then_DispatchesEachLine`: feeds `"rule list\nexit\n"` via `StringReader`, asserting two HTTP calls and a final exit code of 0.
  - `Given_ReplLine_When_ApiReturnsError_Then_StderrGetsCodeAndReplContinues`: feeds `"config get oauth.x\nexit\n"`, asserting `OAUTH_CREDENTIAL_NAMESPACE` appears in stderr and `exit` on the second line is still processed.
  - `Given_HelpCommand_When_Executed_Then_ListsKnownCommandsAndDoesNotCallApi`: injects a failing `HttpClient`, confirming `help` does not call the API.
  - `Given_BlankLines_When_Entered_Then_DispatchNotCalled`.
  - `Given_NonLoopbackBaseUrl_When_NoArgs_Then_ReplDoesNotStart`.
  - `Given_ReplStart_When_ClientIdMissing_Then_BannerPrintedAndAuthStartNotProbed`: stubs the status endpoint to return `{ clientIdConfigured: false }`, asserting the banner contains setup hints and does not call `/api/twitch/auth/start`. (Covered)
  - `Given_ReplStart_When_OAuthNotAuthorized_Then_BannerInstructsAuthStartCommandWithoutCallingStart`: stubs status to return `{ clientIdConfigured: true, hasRefreshToken: false }`, asserting the banner contains the `twitch auth start` string and **does not** call `POST /api/twitch/auth/start`.
  - `Given_ReplStart_When_FullyAuthorized_Then_NoBanner`.
  - `Given_TwitchAuthStartInRepl_When_ClientIdMissing_Then_StderrCodeWithoutCallingStartEndpoint`. (Covered)
  - `Given_CommandTreeAbstraction_When_HelpExecuted_Then_OutputsAllRegisteredCompositesRecursively` (protects recursive abstractions).
  - `Given_InteractiveFlag_When_FollowedByExtraArgs_Then_StderrInvalidArgs`.
  - `Given_ExitCommand_When_Executed_Then_ReplExitTokenIsRequested` (directly asserts token states, independent of stdin EOF).
  - `Given_TwitchAuthStartInRepl_When_CtrlCDuringWait_Then_ListenerClosedAndStderrCancelled`: simulates the outer ct being cancelled after listener startup, asserting `TWITCH_OAUTH_CANCELLED` is printed and `HttpListener` has called `Stop()` (also covered by manual verifications).
  - `Given_StatusProbe_When_ApiSlow_Then_TimesOutAt2sAndPrintsWarningBanner`: stubs endpoint to sleep for 5s, asserting a banner is returned within 2s without blocking subsequent inputs.
  - All existing `CliCommandTests` remain green (regression protection).
- [x] **Web Integration Tests** (`tests/Vulperonex.Tests.Integration/Web/Phase5EndpointTests.cs` or new files):
  - `Given_StatusEndpoint_When_NoClientId_Then_ClientIdConfiguredFalse`.
  - `Given_StatusEndpoint_When_ClientIdSetAndNoToken_Then_ClientIdConfiguredTrueAndHasRefreshTokenFalse`.
  - `Given_StatusEndpoint_When_ClientIdSetAndTokenStored_Then_BothTrue`.
  - `Given_StatusEndpoint_When_Called_Then_ResponseDoesNotContainClientIdOrTokenStrings` (leak protection).
- [ ] **Manual Verification**: add entries to `docs/phases/phase-5-web-signalr-cli/manual-verification.md` covering ↑/↓, Tab, and Ctrl+C behaviors in TTY (automated integration tests cannot verify physical key presses).
  - Environments: Windows Terminal, PowerShell 7, and cmd.exe verified once each (minimum threshold: Windows Terminal).
- [x] **Architectural Checks**: `Vulperonex.Cli.csproj` must not add any `<PackageReference>`; structural changes like `<Folder Include>` or file globs are permitted.
- [x] **OpenAPI**: add `Given_OpenApi_When_Fetched_Then_ContainsTwitchAuthStatusEndpoint`, asserting `/openapi/v1.json` contains the `/api/twitch/auth/status` path.
- [x] **Regressions**: one-shot commands listed in `cli-e2e-verification.md` remain executable, with cross-references "REPL workflows see `supplemental-cli-repl.md`" appended to that document.

---

## Files Likely Involved

CLI (refactored + added):
- `src/Hosts/Vulperonex.Cli/Program.cs` (removes `switch` dispatching, constructs command tree instead)
- `src/Hosts/Vulperonex.Cli/Commands/IConsoleCommand.cs` (new)
- `src/Hosts/Vulperonex.Cli/Commands/ICommandDispatcher.cs` (new)
- `src/Hosts/Vulperonex.Cli/Commands/CommandDispatcher.cs` (new)
- `src/Hosts/Vulperonex.Cli/Commands/CompositeConsoleCommand.cs` (new)
- `src/Hosts/Vulperonex.Cli/Commands/{Rule,Config,Member,Simulate,Twitch}/*Command.cs` (new; migrated from existing private methods like `RuleAsync`)
- `src/Hosts/Vulperonex.Cli/Commands/Builtins/{HelpCommand,ExitCommand}.cs` (new)
- `src/Hosts/Vulperonex.Cli/Infrastructure/CliHttpContext.cs` (new; wraps `HttpClient` and IO writers)
- `src/Hosts/Vulperonex.Cli/Infrastructure/TwitchAuthStatusProbe.cs` (new)
- `src/Hosts/Vulperonex.Cli/Repl/InteractiveSession.cs` (new)
- `src/Hosts/Vulperonex.Cli/Repl/LineEditor.cs` (new)

Web (new endpoints):
- `src/Hosts/Vulperonex.Web/Endpoints/TwitchAuthEndpoints.cs` (appends `GET /status`)
- `src/Vulperonex.Application/Auth/IOAuthTokenStore.cs` (appends `HasRefreshTokenAsync`)
- `src/Vulperonex.Infrastructure/Auth/OAuthTokenStore.cs` (implements `HasRefreshTokenAsync`: queries `GetRefreshTokenAsync` and checks `!string.IsNullOrWhiteSpace`, avoiding raw token returns to endpoint layers)

Tests / Documents:
- `tests/Vulperonex.Tests.Integration/Cli/CliReplTests.cs` (new)
- `tests/Vulperonex.Tests.Integration/Web/Phase5EndpointTests.cs` (appends status endpoint tests)
- `docs/phases/phase-5-web-signalr-cli/manual-verification.md` (appends entries)
- `docs/phases/phase-5-web-signalr-cli/todo.md` (appends Task 16g)

---

## Size

M (extended inside single host project; command routing shares existing `DispatchAsync`, no API changes).

---

## Task 16g - CLI Interactive Input Mode

**Description:** Add a REPL startup branch inside `Vulperonex.Cli` (no arguments or `--interactive` / `-i`), sharing the existing `DispatchAsync`, supporting command history, Tab autocomplete, `help` / `exit`, stdin redirection fallback, and Ctrl+C behaviors, covering integration tests and manual verification entries.

**Size:** M

**Acceptance:** Same as "Acceptance Criteria" section above.

**Verification:** Same as "Verification" section above.

---

## Reference Implementation (Omni-Commander)

Key files:

- `ref/Omni-Commander/OmniCommander.WebApi/Services/ConsoleCliService.cs`
  - `ReadLineWithCompletionAsync`: `Console.IsInputRedirected` fallback, ↑/↓ history, cycling Tab autocomplete, and Backspace with CJK width adjustments. Vulperonex MVP **does not require** CJK width handling (command names and subcommands are all ASCII), simplifying this section.
- `ref/Omni-Commander/OmniCommander.Application/CLI/CommandDispatcher.cs`
  - `GetSuggestions(string input)`: algorithm determining first-level vs sub-level command suggestions based on "whether input ends with a space". Borrowed directly.
- `ref/Omni-Commander/OmniCommander.Application/CLI/CompositeConsoleCommand.cs`
  - Recursive dispatch and completion of subcommands. **Adopted in full**: represents the entire tree via `IConsoleCommand` + `CompositeConsoleCommand` (supporting 3-tier depth like `twitch auth start`), avoiding the need to modify dispatcher or REPL loops when adding commands like `lottery export csv` or `plugin enable <id>` later.

**Differences:** Omni-Commander registers the dispatcher in the DI container, running it continuously via a `BackgroundService`. Vulperonex CLI is an independent console host, **not** introducing a generic host or `Microsoft.Extensions.Hosting`. The command tree is manually constructed and assembled by `Program.cs` (explicit dependencies, no DI reflections), with `HttpClient` and IO writers injected via constructors using `CliHttpContext` into each Command. `ExecuteAsync` returns `int` to preserve one-shot exit code semantics.
