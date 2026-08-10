param([string]$BaseUrl = "http://localhost:5000", [int]$TimeoutMinutes = 45)
$ErrorActionPreference = "Stop"
Import-Module "$PSScriptRoot/PrismJobRunner.psm1" -Force
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Ensure-PrismApi -BaseUrl $BaseUrl -RepoRoot $repoRoot
$logPath = "$repoRoot/matching-testlogs.txt"

$beTestsRoot = Join-Path $repoRoot "test\datasets\BE tests"
$targets = @(
    "X HURLEY39",
    "X FITFLOP 109",
    "X DEMEYER31",
    "X MEPAL5"
)

foreach ($name in $targets) {
    $folder = Join-Path $beTestsRoot $name
    if (-not (Test-Path $folder)) {
        Write-Warning "Missing dataset folder: $folder"
        continue
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
}
