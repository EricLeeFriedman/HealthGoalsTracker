param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\android-verification'),
    [string]$AdbPath = 'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$outputPath = Join-Path $outputRoot (Get-Date -Format 'yyyyMMdd_HHmmss')
$packageName = 'com.companyname.healthgoalstracker'
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
Initialize-LiveTestRun 'android' $requiredFeatures

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

function Invoke-Adb([string[]]$Arguments) {
    $output = & $AdbPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "adb $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
    return $output
}

function Save-AndroidScreenshot([string]$Name) {
    & $AdbPath exec-out screencap -p > (Join-Path $outputPath "$Name.png")
    if ($LASTEXITCODE -ne 0) {
        throw "Could not capture Android screenshot '$Name'."
    }
}

function Get-AndroidUi {
    Invoke-Adb @('shell', 'uiautomator', 'dump', '/sdcard/window.xml') | Out-Null
    $localPath = Join-Path $outputPath 'window.xml'
    Invoke-Adb @('pull', '/sdcard/window.xml', $localPath) | Out-Null
    return [xml](Get-Content $localPath -Raw)
}

function Find-AndroidNode(
    [xml]$Ui,
    [string]$Text = '',
    [string]$AutomationId = '',
    [string]$ContentDescription = ''
) {
    $nodes = $Ui.SelectNodes('//node')
    $node = $nodes | Where-Object {
        ($Text -eq '' -or $_.text -eq $Text) -and
        ($AutomationId -eq '' -or $_.'resource-id' -eq "$packageName`:id/$AutomationId") -and
        ($ContentDescription -eq '' -or $_.'content-desc' -eq $ContentDescription)
    } | Select-Object -First 1

    if ($null -eq $node) {
        $selector = @($Text, $AutomationId, $ContentDescription) |
            Where-Object { $_ -ne '' } |
            Join-String -Separator ', '
        throw "Android UI element '$selector' was not found."
    }
    return $node
}

function Wait-AndroidNode {
    param(
        [string]$Text = '',
        [string]$AutomationId = '',
        [string]$ContentDescription = '',
        [int]$TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $ui = Get-AndroidUi
        try {
            return Find-AndroidNode $ui $Text $AutomationId $ContentDescription
        }
        catch {
            Start-Sleep -Milliseconds 300
        }
    } while ((Get-Date) -lt $deadline)

    throw "Android UI element '$Text$AutomationId$ContentDescription' did not appear."
}

function Invoke-AndroidNode([System.Xml.XmlElement]$Node) {
    if ($Node.bounds -notmatch '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$') {
        throw "Android element has invalid bounds '$($Node.bounds)'."
    }
    $left = [int]$matches[1]
    $top = [int]$matches[2]
    $right = [int]$matches[3]
    $bottom = [int]$matches[4]
    $x = [int](($left + $right) / 2)
    $y = [int](($top + $bottom) / 2)
    Invoke-Adb @('shell', 'input', 'tap', "$x", "$y") | Out-Null
}

function Open-AndroidNavigation {
    $ui = Get-AndroidUi
    Invoke-AndroidNode (Find-AndroidNode $ui -ContentDescription 'Open navigation drawer')
    Start-Sleep -Milliseconds 700
}

function Select-AndroidNavigation([string]$Text) {
    $ui = Get-AndroidUi
    $item = $ui.SelectNodes('//node') |
        Where-Object { $_.text -eq $Text } |
        Select-Object -First 1
    if ($null -eq $item) {
        Open-AndroidNavigation
        $ui = Get-AndroidUi
        $item = Find-AndroidNode $ui -Text $Text
    }
    Invoke-AndroidNode $item
    Start-Sleep -Seconds 1
}

function Set-AndroidText([string]$AutomationId, [string]$Value) {
    $ui = Get-AndroidUi
    Invoke-AndroidNode (Find-AndroidNode $ui -AutomationId $AutomationId)
    Invoke-Adb @('shell', 'input', 'text', $Value) | Out-Null
}

function Scroll-AndroidForward {
    $ui = Get-AndroidUi
    $scrollable = $ui.SelectNodes('//node[@scrollable="true"]') |
        Select-Object -First 1
    if ($null -eq $scrollable -or
        $scrollable.bounds -notmatch '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$') {
        throw 'No scrollable Android region was found.'
    }

    $left = [int]$matches[1]
    $top = [int]$matches[2]
    $right = [int]$matches[3]
    $bottom = [int]$matches[4]
    $x = [int](($left + $right) / 2)
    $startY = [int]($top + (($bottom - $top) * 0.8))
    $endY = [int]($top + (($bottom - $top) * 0.25))
    Invoke-Adb @(
        'shell', 'input', 'swipe', "$x", "$startY", "$x", "$endY", '500'
    ) | Out-Null
    Start-Sleep -Milliseconds 700
}

function Assert-AndroidText([xml]$Ui, [string]$Text) {
    Find-AndroidNode $Ui -Text $Text | Out-Null
}

function Assert-AndroidCalendarGeometry([xml]$Ui, [DateTime]$Month) {
    $columnX = @{}
    $rowY = @{}
    $firstWeekday = [int]([DateTime]::new($Month.Year, $Month.Month, 1).DayOfWeek)

    for ($day = 1; $day -le [DateTime]::DaysInMonth($Month.Year, $Month.Month); $day++) {
        $date = [DateTime]::new($Month.Year, $Month.Month, $day)
        $node = Find-AndroidNode $Ui -AutomationId "HistoryDay$($date.ToString('yyyyMMdd'))"
        if ($node.bounds -notmatch '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$') {
            throw "Calendar day $day has invalid bounds."
        }
        $left = [int]$matches[1]
        $top = [int]$matches[2]
        $column = [int]$date.DayOfWeek
        $row = [int][Math]::Floor(($firstWeekday + $day - 1) / 7)

        if ($columnX.ContainsKey($column) -and [Math]::Abs($columnX[$column] - $left) -gt 3) {
            throw "Calendar day $day is not aligned in weekday column $column."
        }
        if ($rowY.ContainsKey($row) -and [Math]::Abs($rowY[$row] - $top) -gt 3) {
            throw "Calendar day $day is not aligned in calendar row $row."
        }
        $columnX[$column] = $left
        $rowY[$row] = $top
    }

    if ($columnX.Count -ne 7) {
        throw "Android calendar rendered $($columnX.Count) columns instead of 7."
    }
}

if (!(Test-Path $AdbPath)) {
    throw "adb was not found at '$AdbPath'."
}
$connectedDevices = (Invoke-Adb @('devices')) -join "`n"
if ($connectedDevices -notmatch '\sdevice(\s|$)') {
    throw 'No ready Android emulator or device was found.'
}

if (!$NoBuild) {
    & dotnet build (Join-Path $repoRoot 'HealthGoalsTracker.csproj') `
        -f net10.0-android --no-restore -p:EmbedAssembliesIntoApk=true
    if ($LASTEXITCODE -ne 0) {
        throw "Android build failed with exit code $LASTEXITCODE."
    }
}

$apkPath = Join-Path $repoRoot `
    'bin\Debug\net10.0-android\com.companyname.healthgoalstracker-Signed.apk'
Invoke-Adb @('install', '-r', $apkPath) | Out-Null
Invoke-Adb @('shell', 'pm', 'clear', $packageName) | Out-Null
Invoke-Adb @(
    'shell', 'pm', 'grant', $packageName, 'android.permission.POST_NOTIFICATIONS'
) | Out-Null
Invoke-Adb @('logcat', '-c') | Out-Null
Invoke-Adb @(
    'shell', 'monkey', '-p', $packageName,
    '-c', 'android.intent.category.LAUNCHER', '1'
) | Out-Null

$dailyScore = Wait-AndroidNode -AutomationId 'DailyScore' -TimeoutSeconds 30
if ($dailyScore.text -ne 'Today: 0 / 14') {
    throw "Unexpected initial Android score '$($dailyScore.text)'."
}
Complete-LiveTestFeature 'app.launch' 'Process launched and Home automation tree became ready.'

$ui = Get-AndroidUi
foreach ($text in @(
    'Slept at least 7 hours',
    'Ate less than 2200 Calories',
    'Ate at least 150g of Protein',
    'Movement'
)) {
    Assert-AndroidText $ui $text
}
Save-AndroidScreenshot '01-home'
Complete-LiveTestFeature 'home.initial-state' '01-home.png; default goals and Today: 0 / 14.'

Open-AndroidNavigation
$ui = Get-AndroidUi
foreach ($text in @(
    '🏠  Home',
    '📅  History',
    '📊  Measurements',
    '🔔  Notifications',
    '🔁  Reset Today',
    '📤  Export Data',
    '🩺  Export Diagnostics',
    'ℹ️  About'
)) {
    Assert-AndroidText $ui $text
}
Save-AndroidScreenshot '02-flyout'
Invoke-AndroidNode (Find-AndroidNode $ui -Text '🏠  Home')
Start-Sleep -Seconds 1
Complete-LiveTestFeature 'navigation.flyout' '02-flyout.png; all eight items present.'

$ui = Get-AndroidUi
Invoke-AndroidNode (Find-AndroidNode $ui -AutomationId 'ToggleGoal')
$completedScore = Wait-AndroidNode -AutomationId 'DailyScore'
if ($completedScore.text -ne 'Today: 3 / 14') {
    throw "Completing Sleep produced '$($completedScore.text)' instead of 'Today: 3 / 14'."
}
Open-AndroidNavigation
$ui = Get-AndroidUi
Invoke-AndroidNode (Find-AndroidNode $ui -Text '🔁  Reset Today')
Start-Sleep -Milliseconds 700
$ui = Get-AndroidUi
Invoke-AndroidNode (Find-AndroidNode $ui -Text 'Reset')
$resetScore = Wait-AndroidNode -AutomationId 'DailyScore'
if ($resetScore.text -ne 'Today: 0 / 14') {
    throw "Reset Today produced '$($resetScore.text)' instead of 'Today: 0 / 14'."
}
Select-AndroidNavigation '🏠  Home'
Save-AndroidScreenshot '03-reset-today'
Complete-LiveTestFeature 'goals.complete-and-reset' '03-reset-today.png; score changed 0/14 -> 3/14 -> 0/14.'

Select-AndroidNavigation '📊  Measurements'
Set-AndroidText 'MeasurementWeight' '180'
Set-AndroidText 'MeasurementBodyFat' '20'
$ui = Get-AndroidUi
Invoke-AndroidNode (Find-AndroidNode $ui -AutomationId 'SaveMeasurement')
Start-Sleep -Seconds 1
Scroll-AndroidForward
$ui = Get-AndroidUi
Assert-AndroidText $ui '180 lbs • 20% BF'
Save-AndroidScreenshot '04-measurements'
Complete-LiveTestFeature 'measurements.save-and-display' '04-measurements.png; saved values visible in recent history.'

Select-AndroidNavigation '📅  History'
$ui = Get-AndroidUi
foreach ($text in @('Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', '100%', '50–99%', '1–49%', '0%', 'No data')) {
    Assert-AndroidText $ui $text
}
Assert-AndroidCalendarGeometry $ui ([DateTime]::Today)
Save-AndroidScreenshot '05-history-calendar'
$todayId = "HistoryDay$([DateTime]::Today.ToString('yyyyMMdd'))"
Invoke-AndroidNode (Find-AndroidNode $ui -AutomationId $todayId)
Start-Sleep -Seconds 1
$ui = Get-AndroidUi
$weekSummary = Find-AndroidNode $ui -AutomationId 'SelectedWeekSummary'
if ($weekSummary.text -notmatch '^This week: \d+%') {
    throw "Android weekly summary was not visible: '$($weekSummary.text)'."
}
Save-AndroidScreenshot '06-history-detail'
Complete-LiveTestFeature 'history.calendar' '05-history-calendar.png and 06-history-detail.png; labels, geometry, and detail passed.'

Select-AndroidNavigation '🔔  Notifications'
$ui = Get-AndroidUi
foreach ($text in @(
    'Push Notifications',
    'Enable or disable all reminders',
    'Nudge — first reminder',
    'Nudge — second reminder',
    'Daily summary reminder',
    'Morning recap'
)) {
    Assert-AndroidText $ui $text
}
$packageDump = Invoke-Adb @('shell', 'dumpsys', 'package', $packageName)
if (($packageDump -join "`n") -notmatch 'POST_NOTIFICATIONS: granted=true') {
    throw 'Android notification permission was not granted.'
}
$alarmDump = Invoke-Adb @('shell', 'dumpsys', 'alarm')
$activeAlarmDump = (($alarmDump -join "`n") -split '  App Alarm history:')[0]
$alarmCount = [regex]::Matches(
    $activeAlarmDump,
    [regex]::Escape("$packageName/plugin.LocalNotification.ScheduledAlarmReceiver")
).Count
if ($alarmCount -ne 4) {
    throw "Expected four Android notification alarms, found $alarmCount."
}
Save-AndroidScreenshot '07-notifications'
Complete-LiveTestFeature 'notifications.configuration' '07-notifications.png; permission granted and four alarms scheduled.'

$diagnosticPath = Join-Path $outputPath 'diagnostics.log'
& $AdbPath exec-out run-as $packageName cat files/diagnostics/healthgoals.log > $diagnosticPath
if ($LASTEXITCODE -ne 0) {
    throw 'Could not retrieve Android application diagnostics.'
}
$diagnostics = Get-Content $diagnosticPath -Raw
foreach ($event in @(
    'Application started',
    'Main page loaded',
    'Goal completion state changed',
    "Today's goal completion state reset",
    'Reset Today completed',
    'New measurement saved',
    'History page loaded',
    'Notifications page loaded',
    'Notification schedules created'
)) {
    if ($diagnostics -notmatch [regex]::Escape($event)) {
        throw "Android diagnostic event '$event' was not recorded."
    }
}
if ($diagnostics -match '180 lbs|20% BF') {
    throw 'Android diagnostics contain synthetic health values.'
}

$appPid = (Invoke-Adb @('shell', 'pidof', $packageName)).Trim()
$appLogPath = Join-Path $outputPath 'app-log.txt'
& $AdbPath logcat -d -v brief "--pid=$appPid" '*:W' > $appLogPath
if ($LASTEXITCODE -ne 0) {
    throw 'Could not retrieve Android process logs.'
}
$fatalLogs = Select-String -Path $appLogPath `
    -Pattern 'FATAL EXCEPTION|Unhandled|System\.[A-Za-z]+Exception|SIGABRT|AndroidRuntime'
if ($fatalLogs) {
    throw "Android process log contains fatal or unhandled errors: $fatalLogs"
}
Complete-LiveTestFeature 'diagnostics.runtime' 'diagnostics.log and app-log.txt; expected events present and no fatal errors.'

Write-LiveTestReport (Join-Path $outputPath 'live-test-results.json') $requiredFeatures
Write-Host "Android live verification passed. Evidence: $outputPath"
