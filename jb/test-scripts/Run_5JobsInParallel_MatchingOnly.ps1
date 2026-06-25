param([string]$BaseUrl = "http://localhost:5000", [int]$TimeoutMinutes = 30)
$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
Import-Module "$scriptDir/PrismJobRunner.psm1" -Force
$repoRoot = (Resolve-Path "$scriptDir/../..").Path
Ensure-PrismApi -BaseUrl $BaseUrl -RepoRoot $repoRoot
$logPath = "$repoRoot/matching-testlogs.txt"

Get-ChildItem "$repoRoot/jb/testing" -Directory | Sort-Object Name | ForEach-Object -Parallel {
    Import-Module "$using:scriptDir/PrismJobRunner.psm1" -Force
    Invoke-PrismFolderJob `
        -Folder             $_.FullName `
        -BaseUrl            $using:BaseUrl `
        -LogPath            $using:logPath `
        -TimeoutMinutes     $using:TimeoutMinutes `
        -Transform          $false `
        -Generation         $false `
        -SkipClassification $true `
        -Format             'json'
} -ThrottleLimit 5
