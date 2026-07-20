# PostToolUse hook (Edit|Write): deterministic PRISM C# convention checks on the edited file.
# DELTA-BASED: violations are compared against the file's git HEAD baseline and only categories
# whose count INCREASED are reported — much of the existing codebase predates the CLAUDE.md style
# rules, and pre-existing violations must not trigger out-of-scope reformatting. New (untracked)
# files are checked in full. On findings: writes to stderr and exits 2 so Claude sees the feedback.
#
# Adding a check: emit New-Violation entries inside Get-Violations. Keep checks DETERMINISTIC
# (regex, counts, name/path rules). Judgment-based style rules belong in CLAUDE.md and the
# reviewer agent, not here.

$raw = [Console]::In.ReadToEnd()
try { $hook = $raw | ConvertFrom-Json } catch { exit 0 }
$path = $hook.tool_input.file_path
if (-not $path -or $path -notmatch '\.cs$') { exit 0 }
if ($path -match '[/\\](bin|obj|worktrees)[/\\]') { exit 0 }
if (-not (Test-Path $path)) { exit 0 }

# Top-level or nested type declaration (class/record/enum/interface/struct/delegate).
$typePattern = '^\s*(?:\[[^\]]*\]\s*)?(?:(?:public|internal|private|protected|sealed|abstract|static|partial|file|readonly|ref)\s+)*(?:class|record(?:\s+(?:class|struct))?|enum|interface|struct|delegate)\s+([A-Za-z_]\w*)'

function New-Violation([string]$category, [string]$message) {
    [pscustomobject]@{ Category = $category; Message = $message }
}

function Get-Violations([string[]]$lines, [string]$fileBase, [string]$fullPath) {
    $v = @()

    # -- one type per file / file named after its type
    $decls = @(for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l.TrimStart().StartsWith('//')) { continue }
        if ($l -match $script:typePattern) { [pscustomobject]@{ Line = $i + 1; Name = $Matches[1] } }
    })
    if ($decls.Count -gt 1) {
        for ($k = 1; $k -lt $decls.Count; $k++) {
            $v += New-Violation 'one-type-per-file' "Extra type declaration: $($decls[$k].Name) (line $($decls[$k].Line)) — one type per file; move it to $($decls[$k].Name).cs."
        }
    }
    if ($decls.Count -ge 1 -and $decls[0].Name -ne $fileBase) {
        $v += New-Violation 'file-name-matches-type' "File is '$fileBase.cs' but the first declared type is '$($decls[0].Name)' (line $($decls[0].Line)). File must be named after the type."
    }

    # -- /// XML doc comments on type declarations only, never methods/properties
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if (-not $lines[$i].TrimStart().StartsWith('///')) { continue }
        $start = $i
        while ($i -lt $lines.Count -and $lines[$i].TrimStart().StartsWith('///')) { $i++ }
        $j = $i
        while ($j -lt $lines.Count -and ($lines[$j].Trim() -eq '' -or $lines[$j].Trim() -match '^\[[^\]]*\]$')) { $j++ }
        if ($j -ge $lines.Count -or $lines[$j] -notmatch $script:typePattern) {
            $v += New-Violation 'xml-doc-on-members' "XML doc comment at line $($start + 1) is not on a type declaration — /// <summary> is allowed at class level only."
        }
    }

    # -- method parameters on a single line
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l.TrimStart().StartsWith('//')) { continue }
        if ($l -match '^\s*(?:public|private|protected|internal|static|override|virtual|async|sealed)\b[^=]*\(') {
            $open = ($l.ToCharArray() | Where-Object { $_ -eq '(' }).Count
            $close = ($l.ToCharArray() | Where-Object { $_ -eq ')' }).Count
            if ($open -gt $close) {
                $v += New-Violation 'params-on-one-line' "Parameter list left open at end of line $($i + 1) — method parameters go on a single line."
            }
        }
    }

    # -- K&R braces: no opening brace alone on its own line
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '{') {
            $v += New-Violation 'kr-braces' "Opening brace alone on line $($i + 1) — K&R style puts it on the declaration/statement line."
        }
    }

    # -- config-driven design: new named numeric constants in core code are suspect tunables
    if ($fullPath -match 'jb[/\\]src[/\\]core[/\\]' -and $fullPath -notmatch '[/\\]tests[/\\]') {
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $l = $lines[$i]
            if ($l.TrimStart().StartsWith('//')) { continue }
            if ($l -match '^\s*(?:(?:public|private|internal|protected|static|readonly)\s+)*const\s+(?:int|long|float|double|decimal)\s+(\w+)\s*=') {
                $v += New-Violation 'inline-tunable' "New numeric const '$($Matches[1])' (line $($i + 1)). Empirical tunables belong in a JSON config in jb/src/core/config/ (see the AnalyzerConfig pattern). If this is a structural constant (byte midpoint, mathematical definition), keep it but be sure that is what it is."
            }
        }
    }

    # -- no shadow defaults: Transform/Analyzer config classes must not initialize properties in code
    if ($fullPath -match '[/\\]Services[/\\](Transform|Matching[/\\]Analyzers)[/\\]' -and $fileBase -match 'Config$') {
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $l = $lines[$i]
            if ($l.TrimStart().StartsWith('//')) { continue }
            if ($l -match '\{\s*get;\s*(?:init|set);\s*\}\s*=') {
                $v += New-Violation 'config-shadow-default' "Property initializer at line $($i + 1) in a config class — no in-code defaults allowed (core rule): the value must exist ONLY in the area's JSON config and the property must be declared 'required'."
            }
        }
    }

    # -- ONNX sessions must go through OnnxSessionFactory (T-4110): no bare SessionOptions/InferenceSession
    # construction or direct EP append outside the factory itself.
    if ($fullPath -match 'jb[/\\]src[/\\]' -and $fileBase -ne 'OnnxSessionFactory' -and $fullPath -notmatch '[/\\]tests[/\\]') {
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $l = $lines[$i]
            if ($l.TrimStart().StartsWith('//')) { continue }
            if ($l -match 'new\s+InferenceSession\(|AppendExecutionProvider_DML\(|new\s+SessionOptions\(\)') {
                $v += New-Violation 'onnx-session-bypass' "Direct ONNX session/provider construction at line $($i + 1) — route through OnnxSessionFactory.Create() instead (T-4110)."
            }
        }
    }

    return $v
}

$fileBase = [IO.Path]::GetFileNameWithoutExtension($path)
$lines = @(Get-Content $path)
if ($lines.Count -eq 0) { exit 0 }
$current = @(Get-Violations $lines $fileBase $path)
if ($current.Count -eq 0) { exit 0 }

# Baseline: the file as of git HEAD (empty for new/untracked files → full enforcement).
$baseline = @()
try {
    $rel = [IO.Path]::GetRelativePath((Get-Location).Path, (Resolve-Path $path).Path) -replace '\\', '/'
    if ($rel -notmatch '^\.\.') {
        $headLines = git show "HEAD:$rel" 2>$null
        if ($LASTEXITCODE -eq 0 -and $headLines) { $baseline = @(Get-Violations @($headLines) $fileBase $path) }
    }
} catch { }

$baseCounts = @{}
foreach ($b in $baseline) { $baseCounts[$b.Category] = 1 + ($baseCounts[$b.Category] ?? 0) }

$report = @()
foreach ($group in ($current | Group-Object Category)) {
    $before = $baseCounts[$group.Name] ?? 0
    if ($group.Count -le $before) { continue }
    $report += "[$($group.Name)] $($group.Count - $before) new vs HEAD (was $before, now $($group.Count)):"
    $report += ($group.Group | Select-Object -First 8 | ForEach-Object { "  - $($_.Message)" })
    if ($group.Count -gt 8) { $report += "  (+$($group.Count - 8) more in this category)" }
}

if ($report.Count -gt 0) {
    $msg = "PRISM convention check — $fileBase.cs introduced new violations (pre-existing ones are suppressed; fix only what this edit added):`n" + ($report -join "`n")
    [Console]::Error.WriteLine($msg)
    exit 2
}
exit 0
