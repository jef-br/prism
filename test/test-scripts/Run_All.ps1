param([string]$BaseUrl = "http://localhost:5000", [int]$TimeoutMinutes = 120)
$ErrorActionPreference = "Stop"
Import-Module "$PSScriptRoot/PrismJobRunner.psm1" -Force
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$logPath = "$repoRoot/matching-testlogs.txt"

# Smallest-first so a config break surfaces before the multi-minute large batches.
$folders = @(
    'SPACINI29',
    'INPUTMA24',
    'INPUTMA27',
    'INPUTMA23',
    'HEROAUT3',
    'MMERO26'
)

Initialize-PrismApi -BaseUrl $BaseUrl -RepoRoot $repoRoot

foreach ($folder in $folders) {
    Write-Host "==== $folder ===="
    # MaxConcurrentJobs=1 — jobs run sequentially.
    Invoke-PrismFolderJob -Folder "$repoRoot/test/datasets/$folder" -BaseUrl $BaseUrl -LogPath $logPath -TimeoutMinutes $TimeoutMinutes
}

Write-Host "All folders processed. See $logPath"
