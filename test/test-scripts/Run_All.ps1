param([string]$BaseUrl = "http://localhost:5000", [int]$TimeoutMinutes = 30)
$ErrorActionPreference = "Stop"
Import-Module "$PSScriptRoot/PrismJobRunner.psm1" -Force
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$logPath = "$repoRoot/matching-testlogs.txt"

# Smallest-first so a config break surfaces before the multi-minute large batches.
$folders = @(
    'HEROAUT2',
    'SPACINI29',
    'INPUTMA25',
    'AUTOMAT2',
    'INPUTMA24',
    'INPUTMA27',
    'INPUTMA23',
    'HEROAUT3',
    'MMERO26'
)

Ensure-PrismApi -BaseUrl $BaseUrl -RepoRoot $repoRoot

foreach ($folder in $folders) {
    Write-Host "==== $folder ===="
    # MaxConcurrentJobs=1 — jobs run sequentially.
    Invoke-PrismFolderJob -Folder "$repoRoot/jb/Testing/$folder" -BaseUrl $BaseUrl -LogPath $logPath -TimeoutMinutes $TimeoutMinutes
}

Write-Host "All folders processed. See $logPath"
