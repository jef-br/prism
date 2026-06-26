param([string]$BaseUrl = "http://localhost:5000", [int]$TimeoutMinutes = 30)
$ErrorActionPreference = "Stop"
Import-Module "$PSScriptRoot/PrismJobRunner.psm1" -Force
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Ensure-PrismApi -BaseUrl $BaseUrl -RepoRoot $repoRoot
Invoke-PrismFolderJob -Folder "$repoRoot/jb/Testing/INPUTMA25" -BaseUrl $BaseUrl -LogPath "$repoRoot/matching-testlogs.txt" -TimeoutMinutes $TimeoutMinutes
