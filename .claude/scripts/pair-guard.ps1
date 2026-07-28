# PreToolUse hook (Edit|Write): gate judgment-heavy PRISM code behind the /pair socratic protocol.
# Blocks (exit 2) when the target is a high-stakes .cs file that has not yet cleared the /pair
# consensus gate. The gate is recorded in .claude/.pair-consent — one file name per line, appended
# by the skill at Phase 3, cleared by the SessionStart hook.
#
# Adding a path: add an entry to $highStakes. Keep the list TIGHT. The criterion is "wrong here
# still compiles and still looks plausible" — a guard that fires on plumbing trains the model to
# route around it.

$raw = [Console]::In.ReadToEnd()
try { $hook = $raw | ConvertFrom-Json } catch { exit 0 }
$path = $hook.tool_input.file_path
if (-not $path -or $path -notmatch '\.cs$') { exit 0 }
if ($path -match '[/\\](bin|obj|worktrees|tests)[/\\]') { exit 0 }

$highStakes = @(
    @{ Pattern = '[/\\]Analyzer_[^/\\]+\.cs$';                Reason = 'a per-feature analyzer — its thresholds and scoring decide the phenotype' },
    @{ Pattern = '[/\\]Tx_[^/\\]+\.cs$';                      Reason = 'a Tx_* transform class — governed, never introduced without explicit user approval' },
    @{ Pattern = '[/\\]Services[/\\]Matching[/\\]Match[/\\]'; Reason = 'waterfall matching — a wrong tier silently mis-assigns FamilyID' },
    @{ Pattern = '[/\\]Services[/\\]Matching[/\\]Order[/\\]'; Reason = '_det ordering — a wrong preference silently reorders every product' },
    @{ Pattern = '[/\\]Services[/\\]Upscale[/\\]Engine[/\\]'; Reason = 'upscale model adaptation — shape and padding errors surface as artifacts, not exceptions' },
    @{ Pattern = '[/\\]SubjectDetector[^/\\]*\.cs$';          Reason = 'subject geometry — feeds every downstream crop decision' },
    @{ Pattern = '[/\\]ImageFeatureAnalyzer\.cs$';            Reason = 'feature analysis fan-out — drives the whole ImageNGP state' }
)

$hit = $highStakes | Where-Object { $path -match $_.Pattern } | Select-Object -First 1
if (-not $hit) { exit 0 }

$name = Split-Path $path -Leaf
$consentFile = Join-Path $PSScriptRoot '../.pair-consent'
if (Test-Path $consentFile) {
    foreach ($line in Get-Content $consentFile) {
        if ($line.Trim() -and $line.Trim().ToLowerInvariant() -eq $name.ToLowerInvariant()) { exit 0 }
    }
}

[Console]::Error.WriteLine(@"
pair-guard: $name is judgment-heavy code: $($hit.Reason).
Writing it straight past the user defeats the point: this is the category they asked to be walked
through rather than handed. Invoke the 'pair' skill and run the protocol — understand, review out
loud, reach consensus, then implement segment by segment with one probing question per segment.
After the user agrees the approach, append this exact line to .claude/.pair-consent to unblock:
$name
"@)
exit 2
