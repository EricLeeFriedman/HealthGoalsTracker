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
$requiredFeatures = @(
    'app.launch',
    'home.initial-state',
    'navigation.flyout',
    'goals.complete-and-reset',
    'measurements.save-and-display',
    'history.calendar',
    'notifications.configuration',
    'diagnostics.runtime'
)

. (Join-Path $PSScriptRoot 'live-tests\common.ps1')
Initialize-LiveTestRun 'windows' $requiredFeatures

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

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
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
    [string]$Name,
    [int]$TimeoutSeconds = 10
) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $Name)
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

    throw "Automation element named '$Name' was not found."
}

function Wait-ElementNameMatches(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$AutomationId,
    [string]$Pattern,
    [int]$TimeoutSeconds = 30
) {
    $element = Find-ElementById $Root $AutomationId
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    do {
        [string]$name = $element.Current.Name
        if ($name -match $Pattern) {
            return $element
        }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)

    throw "Automation element '$AutomationId' did not match '$Pattern'. Last value: '$name'."
}

function Assert-ElementVisible(
    [System.Windows.Automation.AutomationElement]$Element,
    [string]$Description
) {
    $bounds = $Element.Current.BoundingRectangle
    if ($Element.Current.IsOffscreen -or $bounds.Width -lt 1 -or $bounds.Height -lt 1) {
        throw "'$Description' is not visibly rendered. Bounds: $bounds."
    }
}

function Assert-VisibleName(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$Name
) {
    $element = Find-ElementByName $Root $Name
    Assert-ElementVisible $element $Name
    return $element
}

function Assert-VisibleId(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$AutomationId
) {
    $element = Find-ElementById $Root $AutomationId
    Assert-ElementVisible $element $AutomationId
    return $element
}

function Assert-CalendarGeometry(
    [System.Windows.Automation.AutomationElement]$Root,
    [DateTime]$Month
) {
    $columnX = @{}
    $rowY = @{}
    $daysInMonth = [DateTime]::DaysInMonth($Month.Year, $Month.Month)

    for ($day = 1; $day -le $daysInMonth; $day++) {
        $date = [DateTime]::new($Month.Year, $Month.Month, $day)
        $element = Assert-VisibleId $Root "HistoryDay$($date.ToString('yyyyMMdd'))"
        $bounds = $element.Current.BoundingRectangle
        $column = [int]$date.DayOfWeek
        $row = [int][Math]::Floor(
            (([int]([DateTime]::new($Month.Year, $Month.Month, 1).DayOfWeek)) + $day - 1) / 7)

        if ($columnX.ContainsKey($column)) {
            if ([Math]::Abs($columnX[$column] - $bounds.Left) -gt 3) {
                throw "Calendar day $day is not aligned in weekday column $column."
            }
        }
        else {
            $columnX[$column] = $bounds.Left
        }

        if ($rowY.ContainsKey($row)) {
            if ([Math]::Abs($rowY[$row] - $bounds.Top) -gt 3) {
                throw "Calendar day $day is not aligned in calendar row $row."
            }
        }
        else {
            $rowY[$row] = $bounds.Top
        }
    }

    if ($columnX.Count -ne 7) {
        throw "Calendar rendered $($columnX.Count) columns instead of 7."
    }

    $orderedColumns = 0..6 | ForEach-Object { $columnX[$_] }
    for ($column = 1; $column -lt 7; $column++) {
        if ($orderedColumns[$column] -le $orderedColumns[$column - 1]) {
            throw 'Calendar weekday columns are not ordered left to right.'
        }
    }
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

function Click-ElementCenter(
    [System.Windows.Automation.AutomationElement]$Element
) {
    $bounds = $Element.Current.BoundingRectangle
    $x = [int]($bounds.Left + ($bounds.Width / 2))
    $y = [int]($bounds.Top + ($bounds.Height / 2))
    [HealthGoalsUiNative]::SetCursorPos($x, $y) | Out-Null
    [HealthGoalsUiNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [HealthGoalsUiNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
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

function Assert-ScreenshotRegionHasDarkPixels(
    [string]$ScreenshotName,
    [System.Windows.Automation.AutomationElement]$Element,
    [string]$Description,
    [double]$LeftInset = 0
) {
    $windowBounds = [HealthGoalsUiNative+RECT]::new()
    [HealthGoalsUiNative]::GetWindowRect(
        $script:app.MainWindowHandle,
        [ref]$windowBounds) | Out-Null
    $bounds = $Element.Current.BoundingRectangle
    $image = [System.Drawing.Bitmap]::FromFile(
        (Join-Path $outputPath "$ScreenshotName.png"))
    try {
        $left = [Math]::Max(
            0,
            [int]($bounds.Left - $windowBounds.Left + ($bounds.Width * $LeftInset)))
        $top = [Math]::Max(0, [int]($bounds.Top - $windowBounds.Top))
        $right = [Math]::Min(
            $image.Width - 1,
            [int]($bounds.Right - $windowBounds.Left))
        $bottom = [Math]::Min(
            $image.Height - 1,
            [int]($bounds.Bottom - $windowBounds.Top))
        $darkPixels = 0

        for ($x = $left; $x -le $right; $x += 2) {
            for ($y = $top; $y -le $bottom; $y += 2) {
                $pixel = $image.GetPixel($x, $y)
                $luminance = (0.2126 * $pixel.R) + (0.7152 * $pixel.G) + (0.0722 * $pixel.B)
                if ($luminance -lt 140) {
                    $darkPixels++
                }
            }
        }

        if ($darkPixels -lt 10) {
            throw "'$Description' has no readable dark content in screenshot '$ScreenshotName'."
        }
    }
    finally {
        $image.Dispose()
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
    Complete-LiveTestFeature 'app.launch' 'Windows process launched and Home automation tree became ready.'

    $dailyScore = Wait-ElementNameMatches $root 'DailyScore' '^Today:\s*\d+\s*/\s*\d+$'
    [string]$initialDailyScore = $dailyScore.Current.Name
    if ($initialDailyScore -notmatch '^Today:\s*0\s*/\s*14$') {
        throw "Unexpected initial daily score: '$initialDailyScore'."
    }

    foreach ($requiredHomeText in @(
        'Slept at least 7 hours',
        'Ate less than 2200 Calories',
        'Ate at least 150g of Protein',
        'Movement'
    )) {
        Assert-VisibleName $root $requiredHomeText | Out-Null
    }
    Save-Screenshot '01-home'
    Complete-LiveTestFeature 'home.initial-state' '01-home.png; default goals and Today: 0 / 14.'

    Open-Navigation $root
    foreach ($flyoutItem in @(
        '🏠  Home',
        '📅  History',
        '📊  Measurements',
        '🔔  Notifications',
        '🔁  Reset Today',
        '📤  Export Data',
        '🩺  Export Diagnostics',
        'ℹ️  About'
    )) {
        Assert-VisibleName $root $flyoutItem | Out-Null
    }
    Save-Screenshot '02-flyout'
    foreach ($flyoutItem in @(
        '🏠  Home',
        '📅  History',
        '📊  Measurements',
        '🔔  Notifications'
    )) {
        $element = Find-ElementByName $root $flyoutItem
        Assert-ScreenshotRegionHasDarkPixels '02-flyout' $element $flyoutItem 0.25
    }
    $homeItem = Find-ElementByName $root '🏠  Home'
    $homeItem.GetCurrentPattern(
        [System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 500
    Complete-LiveTestFeature 'navigation.flyout' '02-flyout.png; all eight items present with screenshot contrast.'

    Invoke-Element $root 'ToggleGoal'
    $completedScore = Wait-ElementNameMatches $root 'DailyScore' '^Today:\s*3\s*/\s*14$'
    [string]$completedScoreText = $completedScore.Current.Name
    Open-Navigation $root
    $resetToday = Find-ElementByName $root '🔁  Reset Today'
    Click-ElementCenter $resetToday
    Start-Sleep -Milliseconds 500
    $resetConfirmation = Find-ElementByName $root 'Reset'
    $resetConfirmation.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $resetScore = Wait-ElementNameMatches $root 'DailyScore' '^Today:\s*0\s*/\s*14$'
    [string]$resetScoreText = $resetScore.Current.Name
    Start-Sleep -Seconds 1
    Save-Screenshot '03-reset-today'
    Complete-LiveTestFeature 'goals.complete-and-reset' '03-reset-today.png; score changed 0/14 -> 3/14 -> 0/14.'

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
    Assert-VisibleName $root 'Log a body measurement' | Out-Null
    Find-ElementByName $root 'Trend' | Out-Null
    Find-ElementByName $root 'Recent Entries' | Out-Null
    Save-Screenshot '04-measurements'
    Complete-LiveTestFeature 'measurements.save-and-display' '04-measurements.png; saved values visible in recent history.'

    Select-NavigationItem $root '📅  History'
    $historyMonth = Assert-VisibleId $root 'HistoryMonth'
    foreach ($weekday in @('Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa')) {
        Assert-VisibleName $root $weekday | Out-Null
    }
    foreach ($legend in @('100%', '50–99%', '1–49%', '0%', 'No data')) {
        Assert-VisibleName $root $legend | Out-Null
    }
    Assert-CalendarGeometry $root ([DateTime]::Today)
    Save-Screenshot '05-history-calendar'
    $firstDayId = "HistoryDay$(([DateTime]::new(
        [DateTime]::Today.Year,
        [DateTime]::Today.Month,
        1)).ToString('yyyyMMdd'))"
    Assert-ScreenshotRegionHasDarkPixels `
        '05-history-calendar' `
        (Find-ElementById $root $firstDayId) `
        'First calendar day'
    $todayId = "HistoryDay$([DateTime]::Today.ToString('yyyyMMdd'))"
    Invoke-Element $root $todayId
    Start-Sleep -Seconds 1

    $weeklySummary = Find-ElementById $root 'SelectedWeekSummary'
    if ($weeklySummary.Current.Name -notmatch '^This week: \d+%') {
        throw "Weekly History summary was not visible: '$($weeklySummary.Current.Name)'."
    }
    [string]$weeklySummaryText = $weeklySummary.Current.Name
    Save-Screenshot '06-history-detail'
    Complete-LiveTestFeature 'history.calendar' '05-history-calendar.png and 06-history-detail.png; labels, geometry, contrast, and detail passed.'

    Select-NavigationItem $root '🔔  Notifications'
    foreach ($notificationText in @(
        'Push Notifications',
        'Enable or disable all reminders',
        'Nudge — first reminder',
        'Nudge — second reminder',
        'Daily summary reminder',
        'Morning recap'
    )) {
        Assert-VisibleName $root $notificationText | Out-Null
    }
    Start-Sleep -Seconds 1
    Save-Screenshot '07-notifications'
    Complete-LiveTestFeature 'notifications.configuration' '07-notifications.png; all configured notification types visible.'

    $diagnosticLog = Join-Path $dataPath 'diagnostics\healthgoals.log'
    $requiredEvents = @(
        'Application started',
        'Main page loaded',
        "Today's goal completion state reset",
        'Reset Today completed',
        'Measurements page loaded',
        'New measurement saved',
        'History page loaded',
        'Notifications page loaded',
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
    Complete-LiveTestFeature 'diagnostics.runtime' 'Diagnostic events present without synthetic health values.'
    Write-LiveTestReport `
        (Join-Path $outputPath 'live-test-results.json') `
        $requiredFeatures

    @(
        'Windows runtime verification passed.'
        "Daily score: $initialDailyScore"
        "Completed score before reset: $completedScoreText"
        "Score after reset: $resetScoreText"
        "Measurement history: $historyText"
        "History weekly summary: $weeklySummaryText"
        'Notifications page: loaded'
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
