# Vulperonex

> **Language / 語言**: [English](README.md) | [繁體中文](docs/zh-TW/README.md)

Vulperonex is a streaming assistant platform for Twitch workflows, member loyalty, check-in cards, OBS overlays, timers, rules, and modular integrations.

This repository contains the ASP.NET Core host, Vue admin UI, desktop host, CLI host, workflow runtime, tests, and supporting documentation.

## Quick Start

Use the development script from the repository root.

First-time setup:

```powershell
.\scripts\dev.ps1 restore
.\scripts\dev.ps1 build
```

Daily startup:

```powershell
.\scripts\dev.ps1 run-web
```

The web host starts the API, admin UI static host, SignalR hubs, and overlay endpoints.

Open the local URL printed by the console after startup. The API port normally starts at `5000`, but Vulperonex can automatically choose the next available port pair if the default port is already in use.

Typical first URL:

```text
http://localhost:5000
```

## Requirements

- Windows PowerShell 5.1 or PowerShell 7+
- .NET SDK 10+
- Node.js 20+
- pnpm 9.15.4
- Git

The frontend package declares `pnpm@9.15.4`. If Corepack is available, enable it once:

```powershell
corepack enable
```

## Development Script

The main entry point is [scripts/dev.ps1](scripts/dev.ps1). It keeps the common build, test, and run commands in one place.

| Task | Command | Purpose |
| --- | --- | --- |
| Help | `.\scripts\dev.ps1 help` | Show available tasks. |
| Restore | `.\scripts\dev.ps1 restore` | Restore NuGet packages and install frontend packages. |
| Frontend install | `.\scripts\dev.ps1 install` | Run pnpm install only. |
| Build all | `.\scripts\dev.ps1 build` | Build backend solution and frontend assets. |
| Build backend | `.\scripts\dev.ps1 build-backend` | Build `Vulperonex.sln`. |
| Build frontend | `.\scripts\dev.ps1 build-frontend` | Run `pnpm build` in `src/frontend`. |
| Test all | `.\scripts\dev.ps1 test` | Run backend and frontend tests. |
| Test backend | `.\scripts\dev.ps1 test-backend` | Run `dotnet test` for the solution. |
| Test frontend | `.\scripts\dev.ps1 test-frontend` | Run Vitest with coverage. |
| Type-check UI | `.\scripts\dev.ps1 typecheck` | Run Vue TypeScript checks. |
| Lint UI | `.\scripts\dev.ps1 lint` | Run oxlint. |
| Run web host | `.\scripts\dev.ps1 run-web` | Start `Vulperonex.Web`. |
| Run frontend dev server | `.\scripts\dev.ps1 run-frontend` | Start Vite on `127.0.0.1`. |
| Run desktop host | `.\scripts\dev.ps1 run-desktop` | Start `Vulperonex.Desktop`. |

Examples:

```powershell
.\scripts\dev.ps1 build -Configuration Release
.\scripts\dev.ps1 test-backend -Filter "WorkflowEngineTests"
.\scripts\dev.ps1 run-web
.\scripts\dev.ps1 run-frontend
```

The script automatically uses `pnpm` when available. If `pnpm` is not on `PATH`, it falls back to `corepack pnpm`.

The backend build and test tasks use Windows-friendly MSBuild flags to reduce file-lock issues:

```text
/m:1 /nr:false /p:UseSharedCompilation=false
```

## Common Workflows

### First-Time Setup

```powershell
.\scripts\dev.ps1 restore
.\scripts\dev.ps1 build
```

### Run the App

```powershell
.\scripts\dev.ps1 run-web
```

Open the URL printed by the web host. It is usually `http://localhost:5000`, but it may be another nearby port when `5000` is already occupied.

### Run Frontend Dev Server

```powershell
.\scripts\dev.ps1 run-frontend
```

This is useful for UI iteration when the API host is already running.

### Run Tests

```powershell
.\scripts\dev.ps1 test
```

For a focused backend test run:

```powershell
.\scripts\dev.ps1 test-backend -Filter "TriggerMetadataProviderTests"
```

For frontend-only checks:

```powershell
.\scripts\dev.ps1 typecheck
.\scripts\dev.ps1 test-frontend
.\scripts\dev.ps1 lint
```

## OBS Overlay URLs

Start the web host first:

```powershell
.\scripts\dev.ps1 run-web
```

Then open the admin UI URL shown in the console and copy OBS browser-source URLs from the overlay/settings area.

Use the local URL when OBS runs on the same machine. Use the LAN URL only when OBS runs on another machine on the same network and LAN overlay access is enabled in settings. LAN copy actions should generate the URL from the current detected IP because local IP addresses can change over time.

## Manual Commands

The script is preferred. These commands are useful when you need to debug the toolchain directly.

```powershell
dotnet restore .\Vulperonex.sln
dotnet build .\Vulperonex.sln -c Debug /m:1 /nr:false /p:UseSharedCompilation=false
dotnet test .\Vulperonex.sln -c Debug /m:1 /nr:false /p:UseSharedCompilation=false
corepack pnpm --dir .\src\frontend install --frozen-lockfile
corepack pnpm --dir .\src\frontend build
dotnet run --project .\src\Hosts\Vulperonex.Web\Vulperonex.Web.csproj
```

## Repository Layout

```text
src/
  Hosts/
    Vulperonex.Web/      ASP.NET Core API, SignalR, admin UI host, overlays
    Vulperonex.Desktop/  Desktop host
    Vulperonex.Cli/      CLI host
  frontend/              Vue 3 admin UI
  Vulperonex.*           Domain, application, infrastructure, plugin modules
tests/
  Vulperonex.Tests.Unit/
  Vulperonex.Tests.Integration/
  Vulperonex.Tests.Architecture/
docs/                    Specs, phase notes, and localized documentation
scripts/                 Local development scripts
```

## Troubleshooting

### PowerShell Execution Policy

If PowerShell blocks the script, run it with bypass for the current invocation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 help
```

### pnpm Store Mismatch

If pnpm reports an unexpected store location, reinstall frontend dependencies:

```powershell
.\scripts\dev.ps1 install
```

### Locked .NET Build Files

Stop any running `Vulperonex.Web` or `Vulperonex.Desktop` process, then rerun:

```powershell
.\scripts\dev.ps1 build-backend
```

### Twitch Credentials

Twitch integration settings are managed from the admin UI. Local development can still run without production credentials, but Twitch-specific features require configured app credentials and authorized broadcaster or bot accounts.

## Documentation

Project specs and phase notes live in [docs](docs/). Traditional Chinese documentation is under [docs/zh-TW](docs/zh-TW/).
