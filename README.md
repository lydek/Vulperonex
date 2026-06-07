# Vulperonex

> **Language / 語言**: [English](README.md) | [繁體中文](docs/zh-TW/README.md)

Streaming Assistant Platform — Integrating Twitch event streams, member loyalty, overlay broadcasting, workflow rule engine, and plugin module management.

## Documentation Locale Policy

- English is the default documentation language and keeps the original filename.
- Localized Markdown files live under `docs/<locale>/` with the same relative path and clean filename as the English source.
- Traditional Chinese documentation uses the `docs/zh-TW/` tree.
- Do not use locale suffix naming such as `*.zh-TW.md`; use the locale directory strategy instead.

This project consists of four executable Hosts:

| Host | Purpose | OutputType | TargetFramework |
|---|---|---|---|
| `Vulperonex.Web` | ASP.NET Core API + SignalR Hub + Static Overlay Site | `Exe` | `net10.0` |
| `Vulperonex.Cli` | Console CLI (member / rule / simulate / twitch / timer / config) | `Exe` | `net10.0` |
| `Vulperonex.Desktop` | Windows Desktop Shell (Photino.NET, WebView2-backed) wrapping the embedded Web host | `WinExe` | `net10.0-windows` |
| `frontend/` | Vue 3 SPA Admin UI (Vite + Pinia + PrimeVue) | n/a | n/a |

---

## System Requirements

| Tool | Version | Purpose |
|---|---|---|
| .NET SDK | 10.0+ | Compile all C# projects |
| Node.js | 20.x LTS+ | Frontend toolchain |
| pnpm | 9.15.4 | Frontend package manager (specified in `package.json`) |
| PowerShell | 5.1+ / 7+ | Windows development environment |
| Git | 2.40+ | Version control |

> The Windows Desktop Host (`Vulperonex.Desktop`) is built on **Photino.NET 3.x**, which uses the system WebView2 Runtime — requires Windows 10 1809+ and the WebView2 Runtime installed.

---

## Getting the Source Code

```powershell
git clone <repo-url> Vulperonex
cd Vulperonex
dotnet restore Vulperonex.sln
cd src/frontend
pnpm install --frozen-lockfile
cd ../..
```

---

## Backend — Vulperonex.Web (API + Overlay)

### Build

```powershell
dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false
```

> `/m:1 /nr:false /p:UseSharedCompilation=false` are the project convention flags to avoid MSBuild node reuse which can lead to file locks on Windows.

### Run

```powershell
dotnet run --project src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj
```

Default behavior:

- Listens on a loopback (`127.0.0.1`) port pair (API + Overlay) dynamically allocated by `PortPairAllocator`; the actual URL is printed to the console on startup.
- The environment variable `ASPNETCORE_ENVIRONMENT=Development` launches the developer exception page.
- On the first startup, the following will be generated under the user's AppData directory:
  - `machine-key` (HMAC ETag signature key)
  - `.admin-csrf-token` (per-process random CSRF token)

### Developer Flags

| Environment Variable | Default | Description |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` enables the exception page and detailed logging |
| `Security:CsrfTokenPath` | User AppData | Overrides the CSRF token file path (commonly used in testing) |

### Database Migrations

SQLite automatic `Migrate()` is triggered upon application startup. To apply manually:

```powershell
dotnet ef database update --project src/Vulperonex.Infrastructure --startup-project src/Hosts/Vulperonex.Web
```

To add a new migration:

```powershell
dotnet ef migrations add <Name> --project src/Vulperonex.Infrastructure --startup-project src/Hosts/Vulperonex.Web
```

---

## CLI — Vulperonex.Cli

### Build + Run

```powershell
dotnet run --project src/Hosts/Vulperonex.Cli -- <command> [args]
```

Or self-contained publication:

```powershell
dotnet publish src/Hosts/Vulperonex.Cli -c Release -r win-x64 --self-contained false -o artifacts/cli
artifacts/cli/Vulperonex.Cli.exe --help
```

### Built-in Command Tree

| Group | Subcommand | Purpose |
|---|---|---|
| `member` | `list` / `show` / `seed` / `delete` | Member management (loyalty adjust / reset / audit are API-only, not CLI) |
| `rule` | `list` / `show` / `create` / `update` / `enable` / `disable` / `delete` | Workflow rule management |
| `simulate` | `chat` / `follow` / `sub` / `checkin` | Event simulation / Check-in |
| `twitch` | `auth start` / `auth reset` | OAuth workflow |
| `timer` | `list` / `show` / `create` / `delete` | Timer workflows |
| `config` | `get` / `set` | SystemSetting key-value (no `list`) |

Full reference: [`docs/cli.md`](docs/cli.md). Examples:

```powershell
dotnet run --project src/Hosts/Vulperonex.Cli -- member list
dotnet run --project src/Hosts/Vulperonex.Cli -- simulate checkin --user-id testuser --stamp-count 1 --skip-cooldown
```

### Multi-language

The CLI loads strings through `Resources/I18n/{en-US,zh-TW}.json`; override with the `CULTURE` environment variable:

```powershell
$env:CULTURE = "en-US"; dotnet run --project src/Hosts/Vulperonex.Cli -- --help
```

---

## Desktop — Vulperonex.Desktop (Windows Shell)

### Build + Run

```powershell
dotnet run --project src/Hosts/Vulperonex.Desktop
```

Behavior:

- Launches the embedded `Vulperonex.Web` host (loopback only).
- WebView2 loads the admin SPA.
- Closing the window terminates all background services.

### Publication

```powershell
dotnet publish src/Hosts/Vulperonex.Desktop -c Release -r win-x64 --self-contained true -o artifacts/desktop
```

Produces `artifacts/desktop/Vulperonex.Desktop.exe`.

> Buildable only on Windows (`net10.0-windows` TargetFramework). `dotnet build` will skip this host on Linux/macOS.

---

## Frontend UI — `src/frontend/`

### Installation

```powershell
cd src/frontend
pnpm install --frozen-lockfile
```

### Development Mode

```powershell
pnpm dev
```

- Vite dev server listens on `127.0.0.1:5173` (host locked to loopback).
- Vite proxy forwards `/api/*` + `/hubs/*` to the backend Web host.
- HMR is enabled. The backend must be running to invoke admin APIs.

### Production Build

```powershell
pnpm build
```

Executes two steps:

1. `vue-tsc --noEmit` — Type checking (does not output .d.ts, validation only)
2. `vite build` — Packages to `src/frontend/dist/`

The backend Web host serves this directory directly via `UseStaticFiles` upon startup.

### Lint

```powershell
pnpm lint     # oxlint
pnpm vue-tsc  # Pure type check
```

---

## Testing

### Backend C#

Three test projects exist in the solution:

| Project | Count | Purpose |
|---|---|---|
| `Vulperonex.Tests.Architecture` | 19 | NDepend-style dependency directions, naming conventions, layer boundaries |
| `Vulperonex.Tests.Unit` | 210 | Pure logical unit tests (no DB / no HTTP) |
| `Vulperonex.Tests.Integration` | 219 | `WebApplicationFactory` + SQLite + full HTTP/Hub end-to-end |

Execute the full suite:

```powershell
dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false
```

Unit tests only:

```powershell
dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj
```

Integration tests only:

```powershell
dotnet test tests/Vulperonex.Tests.Integration/Vulperonex.Tests.Integration.csproj
```

Filter a single test:

```powershell
dotnet test --filter "FullyQualifiedName~MemberMutationEndpointTests"
```

> The integration test `CreateClient` will:
> - Allocate a per-test temporary `Security:CsrfTokenPath` to avoid concurrent IO locking
> - Retrieve `AdminCsrfTokenProvider.Token` from DI and set it as the `X-Admin-Csrf` header
> - Inject both `Origin` and `Referer` headers matching the local host

### Frontend Vitest

```powershell
cd src/frontend
pnpm test
```

Executes `vitest run --coverage`:

- 34 test files, 167 cases
- Coverage report output to `src/frontend/coverage/`
- Environment is `jsdom`, automatically detects `MODE === "test"` to skip actual CSRF token fetches

Watch mode:

```powershell
pnpm vitest
```

### One-click All Tests

```powershell
dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false
cd src/frontend; pnpm vue-tsc --noEmit; pnpm test; pnpm build; pnpm lint
```

---

## Complete Checkpoint Process

Must be run before finalizing any phase:

```powershell
# 1. Backend build
dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false

# 2. Backend test
dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false

# 3. Frontend type-check + test + build + lint
cd src/frontend
pnpm vue-tsc --noEmit
pnpm test
pnpm build
pnpm lint
cd ../..
```

All four steps must pass green to allow a merge.

---

## Project Structure

```
Vulperonex/
├── Vulperonex.sln
├── Directory.Build.props        # net10.0 + C# 14 + Nullable enable
├── Directory.Packages.props     # Centralized package version locking
├── src/
│   ├── Vulperonex.Domain/                 # Pure domain models + Events
│   ├── Vulperonex.Application/            # Use cases + interfaces
│   ├── Vulperonex.Infrastructure/         # EF Core + external integrations
│   ├── Adapters/
│   │   ├── Vulperonex.Adapters.Twitch/
│   │   ├── Vulperonex.Adapters.OneComme/
│   │   └── Vulperonex.Adapters.Simulation/
│   ├── Plugins/
│   │   └── Vulperonex.Plugins.Abstractions/
│   ├── Hosts/
│   │   ├── Vulperonex.Web/                # API + SignalR + Static
│   │   ├── Vulperonex.Cli/                # Console CLI
│   │   └── Vulperonex.Desktop/            # WebView2 shell
│   └── frontend/                          # Vue 3 SPA
├── tests/
│   ├── Vulperonex.Tests.Architecture/
│   ├── Vulperonex.Tests.Unit/
│   └── Vulperonex.Tests.Integration/
└── docs/
    └── phases/                            # Phase plan + todo + verification
```

---

## Documentation

- `CONTRIBUTING.md` — Plugin development conventions
- `docs/SPEC.md` — System specifications
- `docs/phases/` — Phase plan / todo / manual-verification
- `docs/cli.md` — CLI complete command reference (if exists)

---

## Troubleshooting

| Symptom | Solution |
|---|---|
| `dotnet build` fails: file is locked | Ensure `/m:1 /nr:false /p:UseSharedCompilation=false` is included |
| Integration tests CSRF 403 | Confirm tests retrieve `AdminCsrfTokenProvider.Token` from DI instead of hardcoding `"true"` |
| Frontend dev cannot connect to API | Start backend first, confirm the vite proxy target port aligns with the URL printed in the console |
| `pnpm install` hangs | Delete `src/frontend/node_modules` + `pnpm-lock.yaml` and reinstall |
| SQLite migration errors | Delete `%LOCALAPPDATA%/Vulperonex/*.db` and restart to trigger automatic migration |
| Desktop starts with white screen | Install [WebView2 Runtime Evergreen](https://developer.microsoft.com/microsoft-edge/webview2/) |

---

## License

See the LICENSE file in the repo root (if exists).
