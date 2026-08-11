param([string]$BaseUrl = "http://localhost:5000", [int]$TimeoutMinutes = 10)
$ErrorActionPreference = "Stop"
Import-Module "$PSScriptRoot/PrismJobRunner.psm1" -Force
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Ensure-PrismApi -BaseUrl $BaseUrl -RepoRoot $repoRoot
$logPath = "$repoRoot/matching-testlogs.txt"

$folder = Join-Path $repoRoot "test\datasets\BE tests\X SMASHEDLEMON45"
if (-not (Test-Path $folder)) {
    throw "Missing dataset folder: $folder"
}

Invoke-PrismFolderJob `
    -Folder             $folder `
    -BaseUrl            $BaseUrl `
    -LogPath            $logPath `
    -TimeoutMinutes     $TimeoutMinutes `
    -Transform          $false `
    -Generation         $false `
    -SkipClassification $true `
    -Format             'json'
