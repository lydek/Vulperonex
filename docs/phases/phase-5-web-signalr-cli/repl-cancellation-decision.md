# Phase 5 REPL Cancellation Behavior Decision

> Parent Task: `docs/phases/phase-5-web-signalr-cli/plan.md` Tasks 16f / 16g
> Corresponding todo items:
> - 16f L57: REPL `twitch auth start` supports Ctrl+C cancellation, outputting `TWITCH_OAUTH_CANCELLED`
> - 16g L59: Line editor TTY mode history, Ctrl+C clears buffer

## Status

Proposed — Pending implementation. This document freezes the semantics of Ctrl+C inside the REPL to avoid repetitive discussions during implementation.

## Problem Scope

Ctrl+C inside the REPL (`InteractiveSession`) has two distinct meanings depending on the context:

1. **Inside the LineEditor (Reading Input)**: The user has not submitted a command yet; Ctrl+C should "clear the current buffer" and keep the REPL alive.
2. **Inside Dispatch (Command Executing)**: Especially during the long-running `twitch auth start` waiting for the HttpListener callback; the user wants to abort the current command. The REPL should not terminate; the active command should observe the cancellation and output the corresponding error code.

Default .NET behavior: Ctrl+C triggers `Console.CancelKeyPress`, which terminates the process unless suppressed. `Console.ReadKey(intercept: true)` does not return Ctrl+C as a key event unless `Console.TreatControlCAsInput = true`.

## Decisions

### 1. The REPL Always Suppresses Process Termination

On startup, `InteractiveSession.RunAsync` hooks into `Console.CancelKeyPress`, setting `e.Cancel = true` in the handler and notifying the active dispatch CTS. The hook is unhooked upon session exit (`exit` / `quit` / EOF / exception).

### 2. During Dispatch: CancellationToken Cancels Current Command

The `InteractiveSession` creates a new `CancellationTokenSource` for each dispatch loop, passing it to `dispatcher.DispatchAsync(..., cts.Token)`. The `CancelKeyPress` handler calls `cts.Cancel()`.

Command implementations (`twitch auth start` being the first example) must:

- Forward this token to all awaitable calls (HttpClient, `HttpListener.GetContextAsync().WaitAsync(timeout, ct)`, `Task.Delay`).
- For the "user cancelled" scenario where `OperationCanceledException` is thrown (`ct.IsCancellationRequested == true`), map it to write the corresponding explicit error code to stderr and return exit code `1`.
- Must not swallow non-REPL token cancellations (such as HttpListener timeout exceptions).

The explicit code for `twitch auth start` is `TWITCH_OAUTH_CANCELLED`.

### 3. During LineEditor: Ctrl+C Clears Buffer

Upon entering the LineEditor, temporarily set `Console.TreatControlCAsInput = true`, restoring it upon exit. When reading `KeyChar == '\x03'` (or `ConsoleKey.C` + `ConsoleModifiers.Control`):

- Discard the current buffer content, visual line break, and return to the prompt to wait for the next input.
- **DO NOT** cancel the outer dispatch token (since the LineEditor is in the prompt stage, and the outer layer has not entered dispatch yet).

During the LineEditor, the `Console.CancelKeyPress` handler must still set `e.Cancel = true` (to prevent process termination) but must not trigger the dispatch CTS (as dispatch has not started). Implement this by using a "whether inside LineEditor" flag to determine the handler action.

### 4. Redirected stdin Does Not Apply

When `ShouldUseLineEditor() == false` (stdin redirected), `TreatControlCAsInput` is not loaded, and the `CancelKeyPress` handler is not attached. In this mode, Ctrl+C behavior is determined by the caller/CI, and the REPL does not suppress process termination itself.

## Why Not Adopt Other Options

- **Do Not Handle Ctrl+C at All**: The user gets stuck in `twitch auth start` for 5 minutes waiting on the HttpListener; the only escape is killing the process, which degrades the refresh token setup experience.
- **Ctrl+C Exits the Entire REPL Directly**: Violates the continuous execution design of 16g for REPL (`exit` / `quit` / EOF to exit).
- **Replace `CancelKeyPress` with a SIGINT Pipe**: No SIGINT equivalent concept exists on Windows. The existing architecture is cross-platform but the REPL already assumes interactive TTY, avoiding additional abstractions.

## Testing Thresholds

- Integration test: Simulate dispatch triggering cancellation (calling CTS.Cancel directly), asserting that `StartCommand` outputs `TWITCH_OAUTH_CANCELLED` to stderr and returns `1`. Does not rely on physical consoles.
- Integration test: When `OperationCanceledException` is not triggered by the REPL token (such as a 5-minute HttpListener timeout), do not output `TWITCH_OAUTH_CANCELLED`, outputting the existing timeout-corresponding error instead.
- LineEditor Ctrl+C clearing buffer behavior covered by unit-level tests (injecting IConsole abstractions or manually verifying in TTY mode — see 16g plans for details).

## Pending Review Notes

- Reviewer:
- Date:
- Decision:
- Follow-up:
