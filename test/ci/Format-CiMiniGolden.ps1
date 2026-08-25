<#
  Format-CiMiniGolden.ps1 — reformats a CiMini manifest-shaped golden JSON file (SourceReference,
  Status, FamilyId, FinalFileName, DetOrder) into the compact 2-line-per-record house style, instead
  of whatever a raw `ConvertTo-Json` dump produces (one field per line).

  Why this exists: `Invoke-CiPipeline.ps1 -Capture` writes `expected-manifest.json` via
  `ConvertTo-Json`, which expands every field onto its own line — that survives a live re-capture but
  destroys the compact, hand-reviewable layout the file is meant to be kept in. Run this immediately
  after any `-Capture` (or any other tool) has touched the file, before committing.

  Output shape, exactly:
    {
      "SourceReference": "...", "Status": "Ok",
      "FamilyId": "...", "FinalFileName": "...", "DetOrder": 0 },

  Usage:
    pwsh test/ci/Format-CiMiniGolden.ps1 -Path test/datasets/CiMini/expected-manifest.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path
)

$ErrorActionPreference = "Stop"

function ToJsonValue($value) {
    if ($null -eq $value) { return "null" }
    if ($value -is [int] -or $value -is [long] -or $value -is [double]) { return "$value" }
    # String: escape backslash and quote before wrapping. None of these filenames carry control
    # characters, so this covers every real case without pulling in a serializer overload.
    $escaped = $value.ToString().Replace('\', '\\').Replace('"', '\"')
    return '"' + $escaped + '"'
}

$rows = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("[")

for ($i = 0; $i -lt $rows.Count; $i++) {
    $r = $rows[$i]
    $comma = if ($i -lt $rows.Count - 1) { "," } else { "" }
    $lines.Add("  {")
    $lines.Add("    `"SourceReference`": $(ToJsonValue $r.SourceReference), `"Status`": $(ToJsonValue $r.Status),")
    $lines.Add("    `"FamilyId`": $(ToJsonValue $r.FamilyId), `"FinalFileName`": $(ToJsonValue $r.FinalFileName), `"DetOrder`": $(ToJsonValue $r.DetOrder) }$comma")
}

$lines.Add("]")
($lines -join "`n") | Set-Content -LiteralPath $Path -Encoding UTF8 -NoNewline
Write-Host "[Format-CiMiniGolden] Reformatted $($rows.Count) records -> $Path"
