param(
    [Parameter(Position = 0)]
    [ValidateSet(
        "help",
        "restore",
        "install",
        "build",
        "build-backend",
        "build-frontend",
        "test",
        "test-backend",
        "test-frontend",
        "typecheck",
        "lint",
        "run-web",
        "run-frontend",
        "run-desktop"
    )]
    [string]$Task = "help",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$Filter = "",

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot "Vulperonex.sln"
$FrontendDir = Join-Path $RepoRoot "src\frontend"
$WebProject = Join-Path $RepoRoot "src\Hosts\Vulperonex.Web\Vulperonex.Web.csproj"
$DesktopProject = Join-Path $RepoRoot "src\Hosts\Vulperonex.Desktop\Vulperonex.Desktop.csproj"
$MsBuildSafetyArgs = @("/m:1", "/nr:false", "/p:UseSharedCompilation=false")
$PnpmCommand = $null
$PnpmPrefixArgs = @()

function Write-Section {
    param([string]$Title)

    Write-Host ""
    Write-Host "==> $Title" -ForegroundColor Cyan
}

function Invoke-Tool {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    Write-Host ("$Command " + ($Arguments -join " ")) -ForegroundColor DarkGray
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE."
    }
}

function Invoke-DotNet {
    param([string[]]$Arguments)

    Invoke-Tool -Command "dotnet" -Arguments $Arguments
}

function Invoke-Pnpm {
    param([string[]]$Arguments)

    if ($null -eq $script:PnpmCommand) {
        $pnpm = Get-Command "pnpm" -ErrorAction SilentlyContinue
        if ($null -ne $pnpm) {
            $script:PnpmCommand = $pnpm.Source
            $script:PnpmPrefixArgs = @()
        }
        else {
            $corepack = Get-Command "corepack" -ErrorAction SilentlyContinue
            if ($null -eq $corepack) {
                throw "pnpm was not found. Install pnpm or enable Corepack, then rerun this task."
            }

            $script:PnpmCommand = $corepack.Source
            $script:PnpmPrefixArgs = @("pnpm")
        }
    }

    Invoke-Tool -Command $script:PnpmCommand -Arguments ($script:PnpmPrefixArgs + @("--dir", $FrontendDir) + $Arguments)
}

function Get-RestoreArg {
    if ($NoRestore) {
        return @("--no-restore")
    }

    return @()
}

function Show-Help {
    Write-Host "Vulperonex development script"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\scripts\dev.ps1 <task> [options]"
    Write-Host ""
    Write-Host "Tasks:"
    Write-Host "  help            Show this help."
    Write-Host "  restore         Restore NuGet packages and install frontend packages."
    Write-Host "  install         Install frontend packages only."
    Write-Host "  build           Build backend solution and frontend assets."
    Write-Host "  build-backend   Build Vulperonex.sln."
    Write-Host "  build-frontend  Build src/frontend."
    Write-Host "  test            Run backend and frontend tests."
    Write-Host "  test-backend    Run dotnet test for the solution."
    Write-Host "  test-frontend   Run frontend Vitest coverage."
    Write-Host "  typecheck       Run frontend TypeScript checks."
    Write-Host "  lint            Run frontend oxlint."
    Write-Host "  run-web         Start Vulperonex.Web."
    Write-Host "  run-frontend    Start the Vite dev server."
    Write-Host "  run-desktop     Start Vulperonex.Desktop."
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Configuration Debug|Release   Backend configuration. Default: Debug."
    Write-Host "  -Filter <test filter>          dotnet test filter for test-backend."
    Write-Host "  -NoRestore                     Pass --no-restore to backend build/test tasks."
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\scripts\dev.ps1 build"
    Write-Host "  .\scripts\dev.ps1 build -Configuration Release"
    Write-Host "  .\scripts\dev.ps1 test-backend -Filter `"WorkflowEngineTests`""
    Write-Host "  .\scripts\dev.ps1 run-web"
}

function Restore-All {
    Write-Section "Restore backend packages"
    Invoke-DotNet -Arguments @("restore", $Solution)

    Write-Section "Install frontend packages"
    Invoke-Pnpm -Arguments @("install", "--frozen-lockfile")
}

function Install-Frontend {
    Write-Section "Install frontend packages"
    Invoke-Pnpm -Arguments @("install", "--frozen-lockfile")
}

function Build-Backend {
    Write-Section "Build backend"
    Invoke-DotNet -Arguments (@("build", $Solution, "-c", $Configuration) + (Get-RestoreArg) + $MsBuildSafetyArgs)
}

function Build-Frontend {
    Write-Section "Build frontend"
    Invoke-Pnpm -Arguments @("build")
}

function Test-Backend {
    Write-Section "Test backend"

    $args = @("test", $Solution, "-c", $Configuration) + (Get-RestoreArg) + $MsBuildSafetyArgs
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $args += @("--filter", $Filter)
    }

    Invoke-DotNet -Arguments $args
}

function Test-Frontend {
    Write-Section "Test frontend"
    Invoke-Pnpm -Arguments @("test")
}

function Typecheck-Frontend {
    Write-Section "Type-check frontend"
    Invoke-Pnpm -Arguments @("type-check")
}

function Lint-Frontend {
    Write-Section "Lint frontend"
    Invoke-Pnpm -Arguments @("lint")
}

function Run-Web {
    Write-Section "Run web host"
    if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) {
        $env:ASPNETCORE_ENVIRONMENT = "Development"
    }

    Invoke-DotNet -Arguments @("run", "--project", $WebProject)
}

function Run-Frontend {
    Write-Section "Run frontend dev server"
    Invoke-Pnpm -Arguments @("dev")
}

function Run-Desktop {
    Write-Section "Run desktop host"
    Invoke-DotNet -Arguments @("run", "--project", $DesktopProject)
}

Push-Location $RepoRoot
try {
    switch ($Task) {
        "help" { Show-Help }
        "restore" { Restore-All }
        "install" { Install-Frontend }
        "build" {
            Build-Backend
            Build-Frontend
        }
        "build-backend" { Build-Backend }
        "build-frontend" { Build-Frontend }
        "test" {
            Test-Backend
            Test-Frontend
        }
        "test-backend" { Test-Backend }
        "test-frontend" { Test-Frontend }
        "typecheck" { Typecheck-Frontend }
        "lint" { Lint-Frontend }
        "run-web" { Run-Web }
        "run-frontend" { Run-Frontend }
        "run-desktop" { Run-Desktop }
    }
}
finally {
    Pop-Location
}
