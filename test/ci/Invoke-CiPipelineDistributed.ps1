<#
  Invoke-CiPipelineDistributed.ps1 — runs the golden dataset through a fully distributed PRISM (T-3300).

  Topology (all processes on this machine — the core co-deployment contract requires one shared
  filesystem; see jb/docs/PRISM-io-import.md):
    API        http://localhost:5100  ingest + export in-process; matching/generate/transform remote
    matching   http://localhost:5101  PRISM_SERVICE=matching  (loads CLIP)
    generate   http://localhost:5102  PRISM_SERVICE=generate
    transform  http://localhost:5103  PRISM_SERVICE=transform (delegates upscaling via PRISM_UPSCALE_URL)
    upscale    http://localhost:5104  PRISM_SERVICE=upscale   (loads Real-ESRGAN)

  The API discovers the remote services via PRISM_*_URL env vars (PipelineServiceFactory). Run and
  golden assertion are delegated to Invoke-CiPipeline.ps1 against the same expected files as the
  in-process run — identical goldens passing in both modes IS the distributed-correctness proof.
  Exit code mirrors Invoke-CiPipeline.ps1. Host/API console output lands in distributed-testlogs/.
#>
[CmdletBinding()]
param(
    [ValidateSet('Match', 'Full')][string]$Mode = 'Full',
    [string]$Dataset        = 'CiMini',
    [int]$TimeoutMinutes    = 30
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$apiUrl   = 'http://localhost:5100'
$ports    = @{ matching = 5101; generate = 5102; transform = 5103; upscale = 5104 }
$logDir   = Join-Path $repoRoot 'distributed-testlogs'

function Wait-HttpOk {
    param([Parameter(Mandatory)][string]$Url, [int]$TimeoutSec = 240, [string]$What = 'host')
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try { Invoke-RestMethod -Uri $Url -TimeoutSec 5 | Out-Null; return } catch { Start-Sleep -Seconds 2 }
    }
    throw "$What at $Url not healthy within ${TimeoutSec}s."
}

function Wait-ApiReady {
    param([Parameter(Mandatory)][string]$BaseUrl, [int]$TimeoutSec = 240)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-RestMethod -Uri "$BaseUrl/PRISM/health" -TimeoutSec 5
            if ($health.CanAcceptJobs) { return }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "API at $BaseUrl did not report CanAcceptJobs within ${TimeoutSec}s."
}

function Clear-ServiceEnv {
    $env:PRISM_SERVICE       = $null
    $env:ASPNETCORE_URLS     = $null
    $env:PRISM_MATCHING_URL  = $null
    $env:PRISM_GENERATE_URL  = $null
    $env:PRISM_TRANSFORM_URL = $null
    $env:PRISM_UPSCALE_URL   = $null
}

# A pre-existing API on the distributed port cannot be trusted to carry the right service wiring.
$alreadyUp = $false
try { Invoke-RestMethod -Uri "$apiUrl/PRISM/health" -TimeoutSec 3 | Out-Null; $alreadyUp = $true } catch { }
if ($alreadyUp) { throw "Something already answers at $apiUrl - stop it first; distributed mode must start its own API." }

Write-Host "[Distributed] Building PRISM.sln ..." -ForegroundColor Cyan
& dotnet build "$repoRoot/jb/src/PRISM.sln" -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw "PRISM.sln build failed (exit $LASTEXITCODE)." }

$hostProject = Join-Path $repoRoot 'jb/src/services/Prism.ServiceHost'
$hostDll     = Join-Path $hostProject 'bin/Debug/net10.0/Prism.ServiceHost.dll'
$apiProject  = Join-Path $repoRoot 'jb/src/api'
$apiDll      = Join-Path $apiProject 'bin/Debug/net10.0/Prism.Api.dll'
if (-not (Test-Path $hostDll)) { throw "ServiceHost binary missing: $hostDll" }
if (-not (Test-Path $apiDll))  { throw "API binary missing: $apiDll" }

New-Item -ItemType Directory -Force $logDir | Out-Null
$procs = @()
$exit  = 1

try {
    foreach ($svc in 'matching', 'generate', 'transform', 'upscale') {
        Clear-ServiceEnv
        $env:PRISM_SERVICE   = $svc
        $env:ASPNETCORE_URLS = "http://localhost:$($ports[$svc])"
        if ($svc -eq 'transform') { $env:PRISM_UPSCALE_URL = "http://localhost:$($ports.upscale)" }

        Write-Host "[Distributed] Starting $svc host on port $($ports[$svc]) ..." -ForegroundColor Cyan
        $procs += Start-Process dotnet -ArgumentList $hostDll -WorkingDirectory $hostProject -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $logDir "$svc.out.log") -RedirectStandardError (Join-Path $logDir "$svc.err.log")
    }

    foreach ($svc in 'matching', 'generate', 'transform', 'upscale') {
        Wait-HttpOk -Url "http://localhost:$($ports[$svc])/health" -What "$svc host"
        Write-Host "[Distributed] $svc host healthy." -ForegroundColor Green
    }

    Clear-ServiceEnv
    $env:PRISM_MATCHING_URL  = "http://localhost:$($ports.matching)"
    $env:PRISM_GENERATE_URL  = "http://localhost:$($ports.generate)"
    $env:PRISM_TRANSFORM_URL = "http://localhost:$($ports.transform)"
    $env:ASPNETCORE_URLS     = $apiUrl

    Write-Host "[Distributed] Starting API on $apiUrl with remote service wiring ..." -ForegroundColor Cyan
    $procs += Start-Process dotnet -ArgumentList $apiDll -WorkingDirectory $apiProject -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logDir 'api.out.log') -RedirectStandardError (Join-Path $logDir 'api.err.log')
    Clear-ServiceEnv
    Wait-ApiReady -BaseUrl $apiUrl
    Write-Host "[Distributed] API ready. Running $Mode on $Dataset ..." -ForegroundColor Green

    pwsh -File (Join-Path $PSScriptRoot 'Invoke-CiPipeline.ps1') -BaseUrl $apiUrl -Mode $Mode -Dataset $Dataset -TimeoutMinutes $TimeoutMinutes
    $exit = $LASTEXITCODE
}
finally {
    Clear-ServiceEnv
    foreach ($p in $procs) {
        try { if (-not $p.HasExited) { $p.Kill($true) } } catch { }
    }
    Write-Host "[Distributed] Stopped $($procs.Count) processes." -ForegroundColor Cyan
}

if ($exit -eq 0) { Write-Host "[Distributed] PASS - distributed run matched the golden." -ForegroundColor Green }
else             { Write-Host "[Distributed] FAIL (exit $exit)." -ForegroundColor Red }
exit $exit
