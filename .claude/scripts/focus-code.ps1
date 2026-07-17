Add-Type @"
using System;
using System.Runtime.InteropServices;
public class FocusHelper {
    [DllImport("user32.dll")] public static extern bool SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
}
"@

$targetPid = $PID
$code = $null
for ($i = 0; $i -lt 15; $i++) {
    $info = Get-CimInstance Win32_Process -Filter "ProcessId = $targetPid" -Property ParentProcessId -ErrorAction SilentlyContinue
    if (-not $info -or $info.ParentProcessId -le 0) { break }
    $targetPid = $info.ParentProcessId
    $proc = Get-Process -Id $targetPid -ErrorAction SilentlyContinue
    if (-not $proc) { break }
    if ($proc.ProcessName -like 'Code*' -and $proc.MainWindowHandle -ne [IntPtr]::Zero) {
        $code = $proc
        break
    }
}

if (-not $code) {
    $code = Get-Process Code -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
        Sort-Object CPU -Descending |
        Select-Object -First 1
}

if ($code) {
    if ([FocusHelper]::IsIconic($code.MainWindowHandle)) {
        [FocusHelper]::ShowWindow($code.MainWindowHandle, 9) | Out-Null
    }
    [FocusHelper]::SwitchToThisWindow($code.MainWindowHandle, $true)

    Start-Sleep -Milliseconds 300
    try {
        Add-Type -AssemblyName UIAutomationClient
        Add-Type -AssemblyName UIAutomationTypes
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $win = $root.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $code.Id)))

        if ($win) {
            $ab = $win.FindFirst(
                [System.Windows.Automation.TreeScope]::Descendants,
                (New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::NameProperty, "Activity Bar")))
            $scope = if ($ab) { $ab } else { $win }

            foreach ($name in @("Claude Code", "Claude")) {
                $el = $scope.FindFirst(
                    [System.Windows.Automation.TreeScope]::Descendants,
                    (New-Object System.Windows.Automation.PropertyCondition(
                        [System.Windows.Automation.AutomationElement]::NameProperty, $name)))
                if ($el) {
                    try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
                    catch { try { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() } catch {} }
                    break
                }
            }
        }
    } catch {}
}
