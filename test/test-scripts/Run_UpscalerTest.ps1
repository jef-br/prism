param(
    [string[]]$Image,
    [double]$Scale = 2.0
)
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$project  = Join-Path $repoRoot "test\UpscalerTestClient"
$desktop  = [Environment]::GetFolderPath("Desktop")

if (-not $Image -or $Image.Count -eq 0) {
    $Image = @()
    Write-Host "Enter image path(s) to upscale (one per line, blank line to finish):"
    while ($true) {
        $path = Read-Host "Image path"
        if ([string]::IsNullOrWhiteSpace($path)) { break }
        $Image += $path.Trim('"')
    }
}

if ($Image.Count -eq 0) {
    Write-Host "No images provided. Nothing to do."
    exit 0
}

$runArgs = @("run", "--project", $project, "--")
foreach ($img in $Image) { $runArgs += @("--image", $img) }
$runArgs += @("--scale", $Scale.ToString([System.Globalization.CultureInfo]::InvariantCulture))
$runArgs += @("--out", $desktop)

Write-Host "Upscaler test | images=$($Image.Count) | scale=$Scale | out=$desktop"
Write-Host ""

Push-Location $repoRoot
try {
    Write-Host "Upscaling now... This can take a while → 1000x1000px = ±2m | 2000x2000px = ±40m"
    & dotnet @runArgs
} finally {
    Write-Host "Upscaling done."
    Pop-Location
}
