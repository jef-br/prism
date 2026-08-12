param([string]$BaseUrl = "http://localhost:5000", [int]$TimeoutMinutes = 45)
$ErrorActionPreference = "Stop"
Import-Module "$PSScriptRoot/PrismJobRunner.psm1" -Force
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Ensure-PrismApi -BaseUrl $BaseUrl -RepoRoot $repoRoot
$logPath = "$repoRoot/matching-testlogs.txt"

$datasetroot = Join-Path $repoRoot "test\datasets\"
$targets = @(
                "BE tests\OHMYBAG1",
                "BE tests\WOODWIC12",
                "BE tests\X DEMEYER31",
                "BE tests\X FILA94",
                "BE tests\X FITFLOP 109",
                "BE tests\X FITFLOP113",
                "BE tests\X FNRE46",
                "BE tests\X HURLEY39",
                "BE tests\X JLINE5",
                "BE tests\X KAID244",
                "BE tests\X KNEIPP56",
                "BE tests\X MEPAL5",
                "BE tests\X SMASHEDLEMON45",
                "BE tests\X SPACINI32",
                "BE tests\X VINGINO79",
                "DEWITTE71",
                "HEROAUT3",
                "INPUTMA23",
                "INPUTMA24",
                "INPUTMA27",
                "KNEIPP56",
                "MMERO26",
                "OHMYBAG1",
                "SPACINI29",
                "SPACINI32",
                "VINGINO79",
                "WOODWIC12"
)

foreach ($name in $targets) {
    $folder = Join-Path $datasetroot $name
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
