# Vulperonex Rapid Test: Chat-Echo Rule Debugging Guide (Cookbook)

This guide walks developers and test agents through debugging the complete E2E chat-echo chain — from starting the server, creating a rule, subscribing to the SignalR overlay, to sending a simulated message and observing the echoed result.

---

## 1. Rapid Debugging Flow

### Step 1: Start the Web host (Server)
Start the local API and the dual-port host. The server automatically applies the SQLite database migrations — no manual migrate command needed.
* **Command**:
  ```powershell
  # switch to the project root
  dotnet run --project src/Hosts/Vulperonex.Web --launch-profile "http"
  ```
* **Observation points & pass conditions**:
  | Observed | Pass condition / expected result |
  | :--- | :--- |
  | Console log | Shows Kestrel successfully binding to both loopback endpoints (e.g. `http://127.0.0.1:5000` and `http://[::1]:5000`) |
  | Database state | On a fresh environment, automatically creates SQLite `Vulperonex.db` at the database path and completes table initialization |

---

### Step 2: Create the chat-echo rule with the CLI (Create Rule)
Use the CLI to load the default minimal echo rule file `rule-chat-echo.json`.
* **Command**:
  ```powershell
  $env:VULPERONEX_API_URL = "http://127.0.0.1:5000"
  .\artifacts\cli-manual\Vulperonex.Cli.exe rule create docs/phases/phase-5_5-rapid-test/examples/rule-chat-echo.json
  ```
* **Observation points & pass conditions**:
  | Observed | Pass condition / expected result |
  | :--- | :--- |
  | CLI output | Prints the created rule JSON summary, including the newly generated `id` (e.g. a `01H...` ULID) |
  | Exit code | `exit 0` (success) |

---

### Step 3: Subscribe to the overlay chat box in the browser (Overlay Subscription)
Open a browser, or add a browser source in OBS, connecting to the SignalR Chat Hub page.
* **URL**:
  `http://127.0.0.1:5000/overlay/chat.html`
* **Observation points & pass conditions**:
  | Observed | Pass condition / expected result |
  | :--- | :--- |
  | Browser main view | Page loads normally, showing a clean transparent or dark background, the Vue client loaded successfully |
  | DevTools (Console) | `SignalR connected`, with no connection-failure or 4xx/5xx error logs |

---

### Step 4: Simulate a chat message input (Simulate Chat)
Use the CLI to simulate the platform publishing a user chat message, triggering the workflow engine.
* **Command**:
  ```powershell
  .\artifacts\cli-manual\Vulperonex.Cli.exe simulate chat "hello world from cookbook"
  ```
* **Observation points & pass conditions**:
  | Observed | Pass condition / expected result |
  | :--- | :--- |
  | CLI output | Prints a JSON response containing `eventId` and `accepted: true` |
  | Overlay browser view | Within **0.5 seconds**, a chat message containing `Echo: hello world from cookbook` dynamically appears, with clean styling |

---

### Step 5: Clean up the test rule (Cleanup)
After the test, delete the temporary echo rule to keep the environment clean.
* **Command**:
  ```powershell
  .\artifacts\cli-manual\Vulperonex.Cli.exe rule delete <rule-id> --yes
  ```
* **Observation points & pass conditions**:
  | Observed | Pass condition / expected result |
  | :--- | :--- |
  | CLI output | Shows `OK rule deleted: <rule-id>` |
  | Exit code | `exit 0` |

---

## 2. Debugging & Verification Record

### 2026-05-24 — AI Agent automated integration-chain verification (Equivalence & SignalR Integration)
* **Verifier**: Antigravity (AI Coding Agent)
* **Test environment**: Windows 11, SQLite in-memory integration test sandbox, Kestrel instance on a randomly assigned socket port
* **Test method**:
  Ran `tests/Vulperonex.Tests.Integration/RapidTest/ChatReplyChainTests.cs`. The test automates the full Steps 1–5 flow above and makes strong assertions on the SignalR real-time echo payload, the ULID, and the outbox side effects.
* **Verification command**:
  ```powershell
  rtk dotnet test --filter "FullyQualifiedName=Vulperonex.Tests.Integration.RapidTest.ChatReplyChainTests"
  ```
* **Expected result**:
  The SignalR client captures the `"Echo: hello"` message precisely within 5 seconds, and the outbox persists this echo.
* **Actual result**:
  `ok dotnet test: 1 tests passed`
* **Verification status**: **PASS**

### 2026-05-20 — Human physical E2E manual integration & Twitch OAuth acceptance
* **Verifier**: lydek (project owner)
* **Test environment**: Windows PowerShell, local `Vulperonex.Web` host (`http://127.0.0.1:5000`)
* **Test method**:
  Manually started the Web API, connected to the REPL interactive terminal via PowerShell, entered `rule create`, `simulate chat`, and visually observed the overlay rendering in the browser; also successfully completed the Twitch PKCE OAuth authorization and the SQLite-encrypted storage.
* **Verification status**: **PASS** (see the `manual-verification.md` history for details)
