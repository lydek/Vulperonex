[CmdletBinding()]
param(
    [string]$ApiUrl,
    [switch]$Published,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CliArgs = @()
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Test-VulperonexApiUrl {
    param([string]$Candidate)

    try {
        $healthUrl = [Uri]::new([Uri]::new($Candidate), '/health')
        $response = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 1
        return $response.status -eq 'ok'
    }
    catch {
        return $false
    }
}

function Find-VulperonexApiUrl {
    if (-not [string]::IsNullOrWhiteSpace($env:VULPERONEX_API_PORT)) {
        $candidate = "http://127.0.0.1:$($env:VULPERONEX_API_PORT)"
        if (Test-VulperonexApiUrl $candidate) {
            return $candidate
        }

        return $null
    }

    $ports = 5000, 5002, 5004, 5006, 5008
    foreach ($port in $ports) {
        $candidate = "http://127.0.0.1:$port"
        if (Test-VulperonexApiUrl $candidate) {
            return $candidate
        }
    }

    return $null
}

if ([string]::IsNullOrWhiteSpace($ApiUrl)) {
    $ApiUrl = Find-VulperonexApiUrl
}

if ([string]::IsNullOrWhiteSpace($ApiUrl)) {
    throw "No running Vulperonex.Web host was found on 127.0.0.1:5000/5002/5004/5006/5008. Start src\Hosts\Vulperonex.Web first, pass -ApiUrl http://127.0.0.1:<port>, or set `$env:VULPERONEX_API_PORT to target a single port."
}

$env:VULPERONEX_API_URL = $ApiUrl
Write-Host "[cli] Using API $ApiUrl"

if ($Published) {
    $exePath = Join-Path $repoRoot 'artifacts\cli-manual\Vulperonex.Cli.exe'
    if (-not (Test-Path $exePath)) {
        throw "Published CLI was not found at $exePath. Run: dotnet publish src\Hosts\Vulperonex.Cli -c Release -o artifacts\cli-manual"
    }

    & $exePath @CliArgs
    exit $LASTEXITCODE
}

$projectPath = Join-Path $repoRoot 'src\Hosts\Vulperonex.Cli'
if ($CliArgs.Count -eq 0) {
    & dotnet run --project $projectPath -- --interactive
}
else {
    & dotnet run --project $projectPath -- @CliArgs
}

exit $LASTEXITCODE
