param([string]$Message = "Needs your attention")

# Debounce: suppress if another hook already fired within the last 30 seconds
$lock = "$env:TEMP\claude-notify.lock"
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
if (Test-Path $lock) {
    $last = [long](Get-Content $lock -Raw -ErrorAction SilentlyContinue)
    if (($now - $last) -lt 30) { exit 0 }
}
$now | Set-Content $lock

$safeMsg = $Message -replace "'", "''"
$toastScript = @"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType=WindowsRuntime] | Out-Null
`$xml = [Windows.Data.Xml.Dom.XmlDocument]::new()
`$xml.LoadXml('<toast duration="long" activationType="protocol" launch="focus-vscode://"><visual><binding template="ToastGeneric"><text>Claude Code</text><text>$safeMsg</text></binding></visual><actions><action content="Open Chat" activationType="protocol" arguments="focus-vscode://"/></actions></toast>')
`$toast = [Windows.UI.Notifications.ToastNotification]::new(`$xml)
`$toast.Priority = [Windows.UI.Notifications.ToastNotificationPriority]::High
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Microsoft.VisualStudioCode').Show(`$toast)
"@

& "$PSScriptRoot\focus-code.ps1"
powershell.exe -NonInteractive -Command $toastScript 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("Claude Code: $Message", "Claude Code") | Out-Null
}
