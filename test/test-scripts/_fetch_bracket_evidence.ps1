param([Parameter(Mandatory)][string]$JobId, [string]$BaseUrl = "http://localhost:5000")

$resp = Invoke-RestMethod -Uri "$BaseUrl/PRISM/jobs/$JobId/result" -Method Get
$rows = $resp.Manifest.ImageRows

Write-Host "Total rows: $($rows.Count)"
$rows | Group-Object -Property MatchedBy | Sort-Object Count -Descending | ForEach-Object {
    "{0,-40} {1,6}" -f ($_.Name ?? '<null/KO>'), $_.Count
}

$outPath = [System.IO.Path]::Combine($env:TEMP, "prism-manifest-$JobId.json")
$rows | ConvertTo-Json -Depth 6 | Set-Content -Path $outPath -Encoding utf8
Write-Host "Full rows saved to $outPath"
