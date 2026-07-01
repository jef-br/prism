param([string]$BaseUrl = "http://localhost:5000", [int]$TimeoutMinutes = 30)
$ErrorActionPreference = "Stop"
Import-Module "$PSScriptRoot/PrismJobRunner.psm1" -Force
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Ensure-PrismApi -BaseUrl $BaseUrl -RepoRoot $repoRoot
$logPath = "$repoRoot/matching-testlogs.txt"

Get-ChildItem "$repoRoot/test/datasets" -Directory | Sort-Object Name | ForEach-Object {
    Invoke-PrismFolderJob `
        -Folder             $_.FullName `
        -BaseUrl            $BaseUrl `
        -LogPath            $logPath `
        -TimeoutMinutes     $TimeoutMinutes `
        -Transform          $false `
        -Generation         $false `
        -SkipClassification $true `
        -Format             'json'
}
