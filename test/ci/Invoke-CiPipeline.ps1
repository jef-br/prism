<#
  Invoke-CiPipeline.ps1 — CI harness for the committed CiMini golden fixture.

  Submits test/datasets/<Dataset> through the PRISM pipeline and asserts the resulting manifest
  against committed "golden" expectations. Reuses the primitives in test-scripts/PrismJobRunner.psm1
  (Initialize-PrismApi, Get-PrismJobInputFiles, Submit-PrismJob, Wait-PrismResult).

  Modes:
    -Mode Match  Fast PR gate: Transform/Generation off, SkipClassification on, JSON result.
                 Asserts SourceReference -> FamilyId against expected-match.json.
    -Mode Full   Nightly: full classify -> transform -> export (ZIP). Asserts Status / FamilyId /
                 FinalFileName / DetOrder against expected-manifest.json, and that the result ZIP
                 contains every expected FinalFileName.

  -Capture       Instead of asserting, (re)write the golden file for this mode from the current run.
                 Use after a human has verified the output is correct ("re-bless the snapshot").

  Exit code is 0 only when the run produced substantive output AND every expectation held; any KO
  that is not a tolerated reason, an empty manifest, or a single mismatch fails the build.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl        = "http://localhost:5000",
    [ValidateSet('Match', 'Full')][string]$Mode = 'Match',
    [string]$Dataset        = 'CiMini',
    [int]$TimeoutMinutes    = 30,
    [switch]$Capture
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot    = (Resolve-Path "$PSScriptRoot/../..").Path
$datasetDir  = Join-Path $repoRoot "test/datasets/$Dataset"
$goldenMatch = Join-Path $datasetDir 'expected-match.json'
$goldenFull  = Join-Path $datasetDir 'expected-manifest.json'
$goldenPath  = if ($Mode -eq 'Match') { $goldenMatch } else { $goldenFull }

# KO reasons that are legitimate and must NOT fail the build (data-driven, not defects).
$toleratedKo = @('VISUAL_DUPLICATE')

Import-Module "$repoRoot/test/test-scripts/PrismJobRunner.psm1" -Force

if (-not (Test-Path $datasetDir)) { throw "Dataset folder not found: $datasetDir" }
if (-not $Capture -and -not (Test-Path $goldenPath)) {
    throw "Golden file missing: $goldenPath. Run once with -Capture (after verifying output) to create it."
}

Initialize-PrismApi -BaseUrl $BaseUrl -RepoRoot $repoRoot

# ---- Run the job -----------------------------------------------------------------------------
$workDir = Join-Path ([System.IO.Path]::GetTempPath()) "prism-ci-$Dataset-$([Guid]::NewGuid().ToString('N'))"
$files = Get-PrismJobInputFiles -Folder $datasetDir -ZipExpandDir $workDir
if ($null -eq $files) { throw "No submittable files (need at least one .xlsx) in $datasetDir." }

if ($Mode -eq 'Match') {
    $envelope = Submit-PrismJob -BaseUrl $BaseUrl -Token "ci-$Dataset-match" -Files $files -WorkDir $workDir `
        -TimeoutMinutes $TimeoutMinutes -Transform $false -Generation $false -SkipClassification $true -Format 'json'
} else {
    $envelope = Submit-PrismJob -BaseUrl $BaseUrl -Token "ci-$Dataset-full" -Files $files -WorkDir $workDir `
        -TimeoutMinutes $TimeoutMinutes -Transform $true -Generation $true -SkipClassification $false -Format 'zip'
}

$zipSavePath = Join-Path $workDir "result-$Mode.zip"
$manifest = Wait-PrismResult -ResultUrl $envelope.ResultUrl -ZipSavePath $zipSavePath -TimeoutMinutes $TimeoutMinutes
if ($null -eq $manifest) { throw "No result / timeout after ${TimeoutMinutes}m for job $($envelope.JobID)." }

# ---- Normalise manifest rows: drop tolerated KOs, collapse to one winning row per source --------
$rows = @($manifest.ImageRows)
if ($rows.Count -eq 0) {
    # A failed job (PrismService.BuildFailedResult) returns an empty ImageRows but records the real reason
    # in RouteSummaries ("Pipeline failed: <message>"). Surface it instead of a generic "vacuous" message.
    Write-Host "[CI] $Mode job returned an empty manifest (0 image rows)." -ForegroundColor Red
    if ($manifest.PSObject.Properties.Name -contains 'RouteSummaries' -and $manifest.RouteSummaries) {
        Write-Host "  RouteSummaries:" -ForegroundColor Yellow
        @($manifest.RouteSummaries) | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
    }
    if ($manifest.PSObject.Properties.Name -contains 'Summary' -and $manifest.Summary) {
        Write-Host "  Summary: ImageCount=$($manifest.Summary.ImageCount) OkRenamed=$($manifest.Summary.OkRenamed) KoRecords=$($manifest.Summary.KoRecords)"
    }
    throw "$Mode job produced an empty manifest — likely a failed pipeline stage (see RouteSummaries above)."
}

$kept = @($rows | Where-Object { $toleratedKo -notcontains $_.KoReasonCode })
$actual = @{}
foreach ($grp in ($kept | Group-Object -Property SourceReference)) {
    # Prefer an Ok row (matched + ordered); otherwise keep the first row so KOs are still visible.
    $win = $grp.Group | Where-Object { $_.Status -eq 'Ok' } | Select-Object -First 1
    if ($null -eq $win) { $win = $grp.Group | Select-Object -First 1 }
    $actual[$grp.Name] = $win
}

# ---- Capture mode: write golden and exit -------------------------------------------------------
if ($Capture) {
    $snapshot = foreach ($src in ($actual.Keys | Sort-Object)) {
        $r = $actual[$src]
        if ($Mode -eq 'Match') {
            [ordered]@{ SourceReference = $src; FamilyId = $r.FamilyId }
        } else {
            [ordered]@{ SourceReference = $src; Status = $r.Status; FamilyId = $r.FamilyId; FinalFileName = $r.FinalFileName; DetOrder = $r.DetOrder }
        }
    }
    New-Item -ItemType Directory -Path $datasetDir -Force | Out-Null
    ($snapshot | ConvertTo-Json -Depth 6) | Set-Content -Path $goldenPath -Encoding UTF8
    Write-Host "[CI] Captured golden ($Mode) -> $goldenPath ($($snapshot.Count) sources). Verify by hand before committing."
    exit 0
}

# ---- Assert against golden ---------------------------------------------------------------------
$expected = Get-Content $goldenPath -Raw | ConvertFrom-Json
$failures = New-Object System.Collections.Generic.List[string]

$okCount = 0
foreach ($exp in $expected) {
    $src = $exp.SourceReference
    if (-not $actual.ContainsKey($src)) { $failures.Add("MISSING source in result: $src"); continue }
    $got = $actual[$src]

    if ($got.FamilyId -ne $exp.FamilyId) {
        $failures.Add("FamilyId mismatch for ${src}: expected '$($exp.FamilyId)' got '$($got.FamilyId)' (Status=$($got.Status), KO=$($got.KoReasonCode))")
    }
    if ($Mode -eq 'Full') {
        if ($got.Status        -ne $exp.Status)        { $failures.Add("Status mismatch for ${src}: expected '$($exp.Status)' got '$($got.Status)'") }
        if ($got.FinalFileName -ne $exp.FinalFileName) { $failures.Add("FinalFileName mismatch for ${src}: expected '$($exp.FinalFileName)' got '$($got.FinalFileName)'") }
        if ("$($got.DetOrder)" -ne "$($exp.DetOrder)") { $failures.Add("DetOrder mismatch for ${src}: expected '$($exp.DetOrder)' got '$($got.DetOrder)'") }
    }
    if ($got.Status -eq 'Ok') { $okCount++ }
}

# Vacuous-green guard: a golden with expected Ok rows but a run that matched nothing must fail even
# if (pathologically) the loop above found no explicit mismatches.
$expectedOk = @($expected | Where-Object { $Mode -eq 'Match' -or $_.Status -eq 'Ok' }).Count
if ($expectedOk -gt 0 -and $okCount -eq 0) {
    $failures.Add("VACUOUS RESULT: expected $expectedOk Ok rows but 0 images came back Ok.")
}

# Full mode: every expected FinalFileName must physically exist in the result ZIP.
if ($Mode -eq 'Full' -and (Test-Path $zipSavePath)) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipSavePath)
    try {
        $entryNames = $archive.Entries | ForEach-Object { [System.IO.Path]::GetFileName($_.FullName) }
        foreach ($exp in ($expected | Where-Object { $_.FinalFileName })) {
            if ($entryNames -notcontains $exp.FinalFileName) { $failures.Add("Result ZIP missing output image: $($exp.FinalFileName)") }
        }
    } finally { $archive.Dispose() }
}

# ---- Report ------------------------------------------------------------------------------------
if ($failures.Count -gt 0) {
    Write-Host "[CI] $Mode FAILED with $($failures.Count) issue(s):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    if (Test-Path $workDir) { Remove-Item $workDir -Recurse -Force -ErrorAction SilentlyContinue }
    exit 1
}

Write-Host "[CI] $Mode PASSED: $($expected.Count) sources match golden, $okCount Ok." -ForegroundColor Green
if (Test-Path $workDir) { Remove-Item $workDir -Recurse -Force -ErrorAction SilentlyContinue }
exit 0
