# PrismJobRunner.psm1
# Shared helpers for the per-folder PRISM test scripts.
# Each Run_<FOLDER>.ps1 imports this module, ensures the API is up, then submits its
# jb/Testing/<FOLDER> folder as a PRISM job and logs the Job OK rate to testlogs.txt.

Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Accepted upload extensions (mirror PrismProcessIngressReader: images + .xlsx).
# NOTE: ZIPs are NOT uploaded as-is. The API ingress counts loose images *before* expanding ZIPs
# (PrismProcessIngressReader rejects with INCOMPLETE_PAYLOAD when acceptedImages==0), so ZIP-only
# batches such as HEROAUT2/AUTOMAT2 would fail. Instead we expand ZIPs client-side and upload their
# image/xlsx contents as loose "input" parts.
$script:ImageExtensions = @('.jpg', '.jpeg', '.png', '.tif', '.tiff', '.pdf', '.webp', '.bmp', '.gif')
$script:ExcelExtension  = '.xlsx'
$script:ZipExtension    = '.zip'

# Ingress byte limits (mirror Prism_Config.json — any single out-of-range file rejects the whole job).
$script:MinImageBytes = 2048
$script:MaxImageBytes = 26214400
$script:MinExcelBytes = 9216
$script:MaxExcelBytes = 1048576

function Ensure-PrismApi {
    <#
      Ensures the PRISM API is reachable and ready at $BaseUrl.
      If /PRISM/health is unreachable, launches `dotnet run` for Prism.Api.csproj from $RepoRoot
      in a detached window and waits (up to ~180s) for the API to report it can accept jobs.
    #>
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$RepoRoot
    )

    if (Test-PrismHealthy -BaseUrl $BaseUrl) {
        Write-Host "[Ensure-PrismApi] API already healthy at $BaseUrl"
        return
    }

    # Build synchronously first so build errors fail fast and the detached run cannot race a
    # concurrent build for obj/ file locks; then launch the prebuilt API.
    Write-Host "[Ensure-PrismApi] API not reachable — building Prism.Api ..."
    & dotnet build "$RepoRoot/jb/src/api/Prism.Api.csproj" -clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw "Prism.Api build failed (exit $LASTEXITCODE); cannot start the API." }

    Write-Host "[Ensure-PrismApi] Launching Prism.Api ..."
    $command = "Set-Location '$RepoRoot'; dotnet run --no-build --project jb/src/api/Prism.Api.csproj"
    Start-Process pwsh -ArgumentList '-NoExit', '-Command', $command | Out-Null

    $deadline = (Get-Date).AddSeconds(180)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        if (Test-PrismHealthy -BaseUrl $BaseUrl) {
            Write-Host "[Ensure-PrismApi] API is healthy."
            return
        }
    }

    throw "PRISM API did not become healthy at $BaseUrl within 180s. Check the dotnet run window for build/startup errors."
}

function Test-PrismHealthy {
    param([Parameter(Mandatory)][string]$BaseUrl)
    try {
        $health = Invoke-RestMethod -Uri "$BaseUrl/PRISM/health" -Method Get -TimeoutSec 5
        return [bool]$health.CanAcceptJobs
    } catch {
        return $false
    }
}

function Get-PrismJobInputFiles {
    <#
      Collects accepted upload files from $Folder. Loose files are gathered recursively; ZIPs are
      expanded into $ZipExpandDir and their contents gathered too (ZIPs themselves are never
      uploaded — see note at top of module). Files are pre-filtered to the ingress byte limits so
      one out-of-range file cannot reject the whole job, and de-duplicated by leaf filename (loose
      files win) so a folder holding both loose images and a redundant ZIP does not double-count.
      Returns an array of file paths, or $null if no valid .xlsx remains (doomed job).
    #>
    param(
        [Parameter(Mandatory)][string]$Folder,
        [Parameter(Mandatory)][string]$ZipExpandDir
    )

    $loose = @(Get-ChildItem -Path $Folder -Recurse -File | Where-Object { $_.Extension.ToLowerInvariant() -ne $script:ZipExtension })

    $zips = @(Get-ChildItem -Path $Folder -Recurse -File | Where-Object { $_.Extension.ToLowerInvariant() -eq $script:ZipExtension })
    $zipIndex = 0
    foreach ($zip in $zips) {
        $dest = Join-Path $ZipExpandDir "zip$zipIndex"
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
        try {
            [System.IO.Compression.ZipFile]::ExtractToDirectory($zip.FullName, $dest)
        } catch {
            Write-Warning "Failed to extract '$($zip.Name)': $($_.Exception.Message)"
        }
        $zipIndex++
    }
    $extracted = @()
    if (Test-Path $ZipExpandDir) {
        $extracted = @(Get-ChildItem -Path $ZipExpandDir -Recurse -File | Where-Object { $_.Extension.ToLowerInvariant() -ne $script:ZipExtension })
    }

    $accepted = New-Object System.Collections.Generic.List[string]
    $seen = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    $excelCount = 0

    # Loose files first so they win de-duplication over ZIP contents.
    foreach ($file in ($loose + $extracted)) {
        $ext = $file.Extension.ToLowerInvariant()

        if ($script:ImageExtensions -contains $ext) {
            if ($file.Length -lt $script:MinImageBytes -or $file.Length -gt $script:MaxImageBytes) {
                Write-Warning "Skipping image out of size range ($($file.Length) bytes): $($file.Name)"
                continue
            }
            if (-not $seen.Add($file.Name)) { continue }
            $accepted.Add($file.FullName)
        }
        elseif ($ext -eq $script:ExcelExtension) {
            if ($file.Length -lt $script:MinExcelBytes -or $file.Length -gt $script:MaxExcelBytes) {
                Write-Warning "Skipping .xlsx out of size range ($($file.Length) bytes): $($file.Name)"
                continue
            }
            if (-not $seen.Add($file.Name)) { continue }
            $accepted.Add($file.FullName)
            $excelCount++
        }
        # Everything else (.db, .jfif, .pptx, .txt, ...) is silently ignored — not an accepted type.
    }

    if ($excelCount -eq 0) {
        Write-Warning "No valid .xlsx file found in '$Folder' — cannot submit a PRISM job."
        return $null
    }

    return $accepted.ToArray()
}

function Invoke-PrismFolderJob {
    <#
      Submits $Folder as a PRISM job to $BaseUrl, waits for the result, computes the Job OK rate
      (lenient: images with Status == "Ok"), and appends one line to $LogPath.
    #>
    param(
        [Parameter(Mandatory)][string]$Folder,
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$LogPath,
        [int]$TimeoutMinutes = 30
    )

    $folderName = Split-Path -Path $Folder -Leaf
    Write-Host "[$folderName] Gathering input files ..."

    # Total per-test duration: spans the whole run (gather + submit + processing wait).
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $workDir = Join-Path ([System.IO.Path]::GetTempPath()) "prism-test-$folderName-$([System.Guid]::NewGuid().ToString('N'))"
    try {
        $files = Get-PrismJobInputFiles -Folder $Folder -ZipExpandDir $workDir
        if ($null -eq $files) {
            Write-PrismLogLine -LogPath $LogPath -Folder $folderName -JobId '-' -Total 0 -Ok 0 -DurationSeconds $stopwatch.Elapsed.TotalSeconds -Note 'NO_VALID_XLSX'
            return
        }
        Write-Host "[$folderName] Submitting $($files.Count) files ..."

        try {
            $envelope = Submit-PrismJob -BaseUrl $BaseUrl -Token $folderName -Files $files -WorkDir $workDir -TimeoutMinutes $TimeoutMinutes
        } catch {
            Write-Warning "[$folderName] Submission failed: $($_.Exception.Message)"
            Write-PrismLogLine -LogPath $LogPath -Folder $folderName -JobId '-' -Total 0 -Ok 0 -DurationSeconds $stopwatch.Elapsed.TotalSeconds -Note "SUBMIT_ERROR: $($_.Exception.Message)"
            return
        }

        $resultUrl = $envelope.ResultUrl
        $jobId = "$($envelope.JobID)"
        if (-not $resultUrl) {
            Write-PrismLogLine -LogPath $LogPath -Folder $folderName -JobId $jobId -Total 0 -Ok 0 -DurationSeconds $stopwatch.Elapsed.TotalSeconds -Note 'NO_RESULT_URL'
            return
        }

        Write-Host "[$folderName] Job $jobId queued — polling for result ..."
        $desktop = [Environment]::GetFolderPath('Desktop')
        $zipSavePath = Join-Path $desktop "PRISM-$folderName-$(Get-Date -Format 'yyyyMMdd-HHmmss').zip"
        $manifest = Wait-PrismResult -ResultUrl $resultUrl -ZipSavePath $zipSavePath -TimeoutMinutes $TimeoutMinutes
        if ($null -eq $manifest) {
            Write-PrismLogLine -LogPath $LogPath -Folder $folderName -JobId $jobId -Total 0 -Ok 0 -DurationSeconds $stopwatch.Elapsed.TotalSeconds -Note "TIMEOUT_OR_NO_RESULT (${TimeoutMinutes}m)"
            return
        }

        # Base the rate on the DEDUPLICATED image count: drop images the pipeline removed as visual
        # duplicates (illustrations/labels are exempt upstream and remain), then group the rest by
        # source image so duplicate rows do not inflate the denominator. An image is "ok" when any of
        # its rows survived matching, ordering, and the transform stage (Status == "Ok").
        $rows = @($manifest.ImageRows)
        $deduped = @($rows | Where-Object { $_.KoReasonCode -ne 'VISUAL_DUPLICATE' })
        $dupExcluded = $rows.Count - $deduped.Count
        $bySource = @($deduped | Group-Object -Property SourceReference)
        $total = $bySource.Count
        $ok = @($bySource | Where-Object {
            $_.Group | Where-Object { $_.Status -eq 'Ok' -and $_.FamilyId -and ($null -ne $_.DetOrder) }
        }).Count

        $note = if ($total -eq 0) { 'NO_IMAGE_ROWS' } else { '' }
        Write-PrismLogLine -LogPath $LogPath -Folder $folderName -JobId $jobId -Total $total -Ok $ok -DuplicatesExcluded $dupExcluded -DurationSeconds $stopwatch.Elapsed.TotalSeconds -Note $note
    }
    finally {
        $stopwatch.Stop()
        if (Test-Path $workDir) {
            Remove-Item -Path $workDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Submit-PrismJob {
    <#
      POSTs the multipart job to /PRISM/process using HttpClient + MultipartFormDataContent.

      Images are packed into a single ZIP (stored, no recompression) and one image is sent loose as
      a "seed". This is required because (a) the API ingress counts loose images BEFORE expanding
      ZIPs and rejects with INCOMPLETE_PAYLOAD when there are none, and (b) ASP.NET Core caps a
      multipart form at 1024 values, so one part per image fails for large folders. The seed image
      is excluded from the ZIP to avoid a duplicate filename. xlsx files are sent loose. Result: a
      handful of multipart parts regardless of image count. Returns the parsed start envelope.
    #>
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$Token,
        [Parameter(Mandatory)][string[]]$Files,
        [Parameter(Mandatory)][string]$WorkDir,
        [int]$TimeoutMinutes = 30
    )

    $imageFiles = @($Files | Where-Object { $script:ImageExtensions -contains ([System.IO.Path]::GetExtension($_).ToLowerInvariant()) })
    $excelFiles = @($Files | Where-Object { [System.IO.Path]::GetExtension($_).ToLowerInvariant() -eq $script:ExcelExtension })

    if ($imageFiles.Count -eq 0) {
        throw "No accepted image files to submit."
    }

    $seed = $imageFiles[0]
    $rest = if ($imageFiles.Count -gt 1) { $imageFiles[1..($imageFiles.Count - 1)] } else { @() }

    # Pack the non-seed images into one stored ZIP — leaf filenames are already unique (de-duped in
    # Get-PrismJobInputFiles), so ZIP entry names do not collide.
    $zipPath = $null
    if ($rest.Count -gt 0) {
        New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null
        $zipPath = Join-Path $WorkDir '__prism_upload_images.zip'
        $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($img in $rest) {
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $img, [System.IO.Path]::GetFileName($img), [System.IO.Compression.CompressionLevel]::NoCompression) | Out-Null
            }
        } finally {
            $zip.Dispose()
        }
    }

    # Input is omitted intentionally — the server defaults it to [] and we upload everything as
    # multipart "input" parts instead.
    $requestObject = [ordered]@{
        ClientRequestToken   = $Token
        rename               = $true
        transform            = $true
        generation           = $true
        format               = 'zip'
        ReturnOriginalImages = $false
    }
    $requestJson = $requestObject | ConvertTo-Json -Compress

    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromMinutes([Math]::Max(15, $TimeoutMinutes))
    $content = [System.Net.Http.MultipartFormDataContent]::new()
    $streams = New-Object System.Collections.Generic.List[System.IO.Stream]

    try {
        $requestPart = [System.Net.Http.StringContent]::new($requestJson, [System.Text.Encoding]::UTF8, 'application/json')
        $content.Add($requestPart, 'request')

        $uploadPaths = New-Object System.Collections.Generic.List[string]
        $uploadPaths.Add($seed)              # loose seed image (satisfies ingress min-image gate)
        if ($zipPath) { $uploadPaths.Add($zipPath) }   # all remaining images, expanded by core
        foreach ($x in $excelFiles) { $uploadPaths.Add($x) }

        foreach ($path in $uploadPaths) {
            $stream = [System.IO.File]::OpenRead($path)
            $streams.Add($stream)
            $part = [System.Net.Http.StreamContent]::new($stream)
            $part.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new('application/octet-stream')
            $content.Add($part, 'input', [System.IO.Path]::GetFileName($path))
        }

        $response = $client.PostAsync("$BaseUrl/PRISM/process", $content).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        if (-not $response.IsSuccessStatusCode) {
            throw "HTTP $([int]$response.StatusCode) from /PRISM/process: $body"
        }

        return $body | ConvertFrom-Json
    }
    finally {
        $content.Dispose()
        foreach ($s in $streams) { $s.Dispose() }
        $client.Dispose()
    }
}

function Wait-PrismResult {
    <#
      Polls $ResultUrl until it returns 200 (job complete) or the timeout elapses.
      On a zip-format completion the response is the binary archive: it is saved to $ZipSavePath
      and the manifest is read back out of the archive's manifest.json entry. A non-zip 200 (e.g. a
      failed job that the API serializes as JSON) is parsed inline. Returns the parsed Manifest
      object, or $null on timeout / missing manifest.
    #>
    param(
        [Parameter(Mandatory)][string]$ResultUrl,
        [string]$ZipSavePath,
        [int]$TimeoutMinutes = 30
    )

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        $response = Invoke-WebRequest -Uri $ResultUrl -Method Get -SkipHttpErrorCheck -TimeoutSec 120
        if ($response.StatusCode -eq 200) {
            $contentType = "$($response.Headers['Content-Type'])"
            if ($contentType -match 'zip' -and $ZipSavePath) {
                Save-ResponseBytes -Response $response -Path $ZipSavePath
                Write-Host "[Wait-PrismResult] Saved result zip → $ZipSavePath"
                return Read-ManifestFromZip -ZipPath $ZipSavePath
            }
            # Non-zip completion (failed job serialized as JSON): parse the envelope inline.
            $parsed = ([System.Text.Encoding]::UTF8.GetString($response.Content)) | ConvertFrom-Json
            return $parsed.Manifest
        }
        if ($response.StatusCode -ne 202) {
            Write-Warning "Unexpected HTTP $($response.StatusCode) from result endpoint."
            return $null
        }
        Start-Sleep -Seconds 3
    }
    return $null
}

function Save-ResponseBytes {
    <# Writes the raw bytes of an Invoke-WebRequest response to $Path. #>
    param(
        [Parameter(Mandatory)]$Response,
        [Parameter(Mandatory)][string]$Path
    )

    $stream = $Response.RawContentStream
    $stream.Position = 0
    $fileStream = [System.IO.File]::Create($Path)
    try {
        $stream.CopyTo($fileStream)
    } finally {
        $fileStream.Dispose()
    }
}

function Read-ManifestFromZip {
    <# Extracts manifest.json from the result archive and returns it parsed, or $null if absent. #>
    param([Parameter(Mandatory)][string]$ZipPath)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -eq 'manifest.json' } | Select-Object -First 1
        if ($null -eq $entry) {
            Write-Warning "Result zip '$ZipPath' has no manifest.json."
            return $null
        }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try {
            return ($reader.ReadToEnd() | ConvertFrom-Json)
        } finally {
            $reader.Dispose()
        }
    } finally {
        $archive.Dispose()
    }
}

function Write-PrismLogLine {
    <#
      Appends one timestamped line to the shared test log. Always emits the literal "Job OK rate:".
    #>
    param(
        [Parameter(Mandatory)][string]$LogPath,
        [Parameter(Mandatory)][string]$Folder,
        [Parameter(Mandatory)][string]$JobId,
        [Parameter(Mandatory)][int]$Total,
        [Parameter(Mandatory)][int]$Ok,
        [int]$DuplicatesExcluded = 0,
        [double]$DurationSeconds = 0,
        [string]$Note = ''
    )

    $rate = if ($Total -gt 0) { [Math]::Round(($Ok / $Total) * 100, 1) } else { 0 }
    $duration = [TimeSpan]::FromSeconds($DurationSeconds).ToString('hh\:mm\:ss')
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "$timestamp | $Folder | JobID=$JobId | images=$Total ok=$Ok dedup-excluded=$DuplicatesExcluded | Job OK rate: $rate% | duration: $duration ($([Math]::Round($DurationSeconds, 1))s)"
    if ($Note) { $line += " | $Note" }

    Add-Content -Path $LogPath -Value $line
    Write-Host $line
}

Export-ModuleMember -Function Ensure-PrismApi, Get-PrismJobInputFiles, Invoke-PrismFolderJob, Write-PrismLogLine
