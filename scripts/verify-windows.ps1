param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\windows-verification')
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$outputPath = Join-Path $outputRoot (Get-Date -Format 'yyyyMMdd_HHmmss')
$dataPath = Join-Path $outputPath 'data'
$stdoutPath = Join-Path $outputPath 'dotnet-run.stdout.log'
$stderrPath = Join-Path $outputPath 'dotnet-run.stderr.log'
$startedAt = Get-Date
$runner = $null
$app = $null

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
New-Item -ItemType Directory -Path $dataPath -Force | Out-Null

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HealthGoalsUiNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr handle, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr handle, IntPtr deviceContext, uint flags);
}
'@

function Get-AppRoot {
    $process = Get-Process -Name HealthGoalsTracker -ErrorAction SilentlyContinue |
        Where-Object { $_.StartTime -ge $startedAt } |
        Select-Object -First 1

    if ($null -eq $process -or $process.MainWindowHandle -eq 0) {
        return $null
    }

    $script:app = $process
    return [System.Windows.Automation.AutomationElement]::FromHandle(
        $process.MainWindowHandle)
}

function Wait-AppRoot {
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $root = Get-AppRoot
        if ($null -ne $root) {
            return $root
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    throw 'The HealthGoalsTracker window did not appear within 30 seconds.'
}

function Find-ElementById(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$AutomationId,
    [int]$TimeoutSeconds = 10
) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    do {
        $element = $Root.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            $condition)
        if ($null -ne $element) {
            return $element
        }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)

    throw "Automation element '$AutomationId' was not found."
}

function Find-ElementByName(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$Name
) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $Name)
    $element = $Root.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)
    if ($null -eq $element) {
        throw "Automation element named '$Name' was not found."
    }
    return $element
}

function Open-Navigation(
    [System.Windows.Automation.AutomationElement]$Root
) {
    $button = Find-ElementByName $Root 'Open Navigation'
    $button.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
}

function Select-NavigationItem(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$Name
) {
    Open-Navigation $Root
    $item = Find-ElementByName $Root $Name
    $item.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Seconds 1
}

function Set-ElementValue(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$AutomationId,
    [string]$Value
) {
    $element = Find-ElementById $Root $AutomationId
    $element.GetCurrentPattern(
        [System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)
}

function Invoke-Element(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$AutomationId
) {
    $element = Find-ElementById $Root $AutomationId
    $element.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Save-Screenshot([string]$Name) {
    $rect = [HealthGoalsUiNative+RECT]::new()
    [HealthGoalsUiNative]::GetWindowRect(
        $script:app.MainWindowHandle,
        [ref]$rect) | Out-Null
    $bitmap = [System.Drawing.Bitmap]::new(
        ($rect.Right - $rect.Left),
        ($rect.Bottom - $rect.Top))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $deviceContext = $graphics.GetHdc()
        try {
            $captured = [HealthGoalsUiNative]::PrintWindow(
                $script:app.MainWindowHandle,
                $deviceContext,
                2)
            if (!$captured) {
                throw "PrintWindow failed for screenshot '$Name'."
            }
        }
        finally {
            $graphics.ReleaseHdc($deviceContext)
        }
        $bitmap.Save(
            (Join-Path $outputPath "$Name.png"),
            [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

try {
    $previousDataPath = $env:HEALTHGOALSTRACKER_DATA_DIR
    $env:HEALTHGOALSTRACKER_DATA_DIR = $dataPath
    $runner = Start-Process dotnet -PassThru -WorkingDirectory $repoRoot `
        -ArgumentList @(
            'run',
            '--project', '.\HealthGoalsTracker.csproj',
            '-f', 'net10.0-windows10.0.19041.0',
            '--no-build'
        ) `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $root = Wait-AppRoot
    [HealthGoalsUiNative]::SetForegroundWindow($app.MainWindowHandle) | Out-Null

    $dailyScore = Find-ElementById $root 'DailyScore'
    [string]$initialDailyScore = $dailyScore.Current.Name
    if ($initialDailyScore -notmatch '^Today:\s*0\s*/\s*14$') {
        throw "Unexpected initial daily score: '$initialDailyScore'."
    }
    Save-Screenshot '01-home'

    Select-NavigationItem $root '📊  Measurements'
    Set-ElementValue $root 'MeasurementWeight' '180'
    Set-ElementValue $root 'MeasurementBodyFat' '20'
    Set-ElementValue $root 'MeasurementNotes' 'Synthetic Windows verification'
    Invoke-Element $root 'SaveMeasurement'
    Start-Sleep -Seconds 1

    $history = Find-ElementById $root 'MeasurementHistory'
    $historyText = ($history.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition) |
        ForEach-Object { $_.Current.Name }) -join '|'
    if ($historyText -notmatch '180 lbs' -or $historyText -notmatch '20%') {
        throw "Saved measurement was not visible in recent history: '$historyText'."
    }
    Save-Screenshot '02-measurements'

    Select-NavigationItem $root '📅  History'
    $todayId = "HistoryDay$([DateTime]::Today.ToString('yyyyMMdd'))"
    Invoke-Element $root $todayId
    Start-Sleep -Seconds 1

    $weeklySummary = Find-ElementById $root 'SelectedWeekSummary'
    if ($weeklySummary.Current.Name -notmatch '^This week: \d+%') {
        throw "Weekly History summary was not visible: '$($weeklySummary.Current.Name)'."
    }
    Save-Screenshot '03-history'

    $diagnosticLog = Join-Path $dataPath 'diagnostics\healthgoals.log'
    $requiredEvents = @(
        'Application started',
        'Main page loaded',
        'Measurements page loaded',
        'New measurement saved',
        'History page loaded',
        'Notification scheduling skipped on the Windows development target'
    )
    $logContents = Get-Content $diagnosticLog -Raw
    foreach ($requiredEvent in $requiredEvents) {
        if ($logContents -notmatch [regex]::Escape($requiredEvent)) {
            throw "Diagnostic event '$requiredEvent' was not recorded."
        }
    }
    if ($logContents -match 'Synthetic Windows verification|180 lbs|20%') {
        throw 'Diagnostic log contains synthetic health values.'
    }

    @(
        'Windows runtime verification passed.'
        "Daily score: $initialDailyScore"
        "Measurement history: $historyText"
        "History weekly summary: $($weeklySummary.Current.Name)"
        "Diagnostics: $diagnosticLog"
    ) | Set-Content (Join-Path $outputPath 'verification-summary.txt')
}
finally {
    if ($null -ne $app -and !$app.HasExited) {
        Stop-Process -Id $app.Id
    }
    if ($null -ne $runner -and !$runner.HasExited) {
        Stop-Process -Id $runner.Id
    }
    $env:HEALTHGOALSTRACKER_DATA_DIR = $previousDataPath
}
