param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\backend-verification'),
    [int]$Port = 7073,
    [string]$FuncPath = '',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$outputPath = Join-Path $outputRoot (Get-Date -Format 'yyyyMMdd_HHmmss')
$stdoutPath = Join-Path $outputPath 'functions.stdout.log'
$stderrPath = Join-Path $outputPath 'functions.stderr.log'
$baseUri = "http://localhost:$Port/api/v1"
$hostProcess = $null
$listenerProcessId = $null
$requiredFeatures = @(
    'backend.health',
    'backend.identity',
    'backend.validation',
    'backend.sync',
    'backend.reads',
    'backend.diagnostics'
)

. (Join-Path $PSScriptRoot 'live-tests\common.ps1')
Initialize-LiveTestRun 'backend' $requiredFeatures
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

function Resolve-FunctionsExecutable {
    if (![string]::IsNullOrWhiteSpace($FuncPath)) {
        if (!(Test-Path $FuncPath)) {
            throw "Functions executable was not found at '$FuncPath'."
        }
        return (Resolve-Path $FuncPath).Path
    }

    $command = Get-Command func -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $installedPath = 'C:\Program Files\Microsoft\Azure Functions Core Tools\func.exe'
    if (Test-Path $installedPath) {
        return $installedPath
    }

    throw 'Azure Functions Core Tools 4 is required to run backend verification.'
}

function Stop-ProcessTree([int]$ProcessId) {
    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $ProcessId" `
        -ErrorAction SilentlyContinue
    foreach ($child in $children) {
        Stop-ProcessTree ([int]$child.ProcessId)
    }
    if ($null -ne (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $ProcessId
    }
}

function Invoke-Api {
    param(
        [string]$Path,
        [string]$Method = 'Get',
        [hashtable]$Headers = @{},
        [object]$Body
    )

    $parameters = @{
        Uri = "$baseUri/$Path"
        Method = $Method
        Headers = $Headers
        UseBasicParsing = $true
    }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = if ($Body -is [string]) {
            $Body
        }
        else {
            $Body | ConvertTo-Json -Depth 12
        }
    }
    return Invoke-WebRequest @parameters
}

function Assert-Status {
    param(
        [scriptblock]$Action,
        [int]$ExpectedStatus,
        [string]$ExpectedErrorCode = ''
    )

    try {
        $response = & $Action
        $status = [int]$response.StatusCode
        $content = $response.Content
    }
    catch {
        if ($null -eq $_.Exception.Response) {
            throw
        }
        $status = [int]$_.Exception.Response.StatusCode
        $content = $_.ErrorDetails.Message
    }

    if ($status -ne $ExpectedStatus) {
        throw "Expected HTTP $ExpectedStatus, received $status. Body: $content"
    }
    if ($ExpectedErrorCode -ne '') {
        $errorBody = $content | ConvertFrom-Json
        if ($errorBody.code -ne $ExpectedErrorCode) {
            throw "Expected error '$ExpectedErrorCode', received '$($errorBody.code)'."
        }
    }
}

function New-SyncRequest([string]$Cursor = '') {
    return @{
        deviceId = [Guid]::NewGuid().ToString()
        cursor = if ($Cursor -eq '') { $null } else { $Cursor }
        goals = @()
        dailyRecords = @()
        measurements = @()
    }
}

$functionsExecutable = Resolve-FunctionsExecutable
$previousDevelopmentIdentity = $env:AllowDevelopmentIdentity
$previousStorage = $env:AzureWebJobsStorage
$previousFunctionsEnvironment = $env:AZURE_FUNCTIONS_ENVIRONMENT
$previousCursorSigningKey = $env:CursorSigningKey
$previousApiScope = $env:ApiScope

try {
    if (!$NoBuild) {
        dotnet build `
            (Join-Path $repoRoot 'HealthGoalsTracker.Functions\HealthGoalsTracker.Functions.csproj') `
            --no-restore `
            -p:TreatWarningsAsErrors=true
        if ($LASTEXITCODE -ne 0) {
            throw "Functions build failed with exit code $LASTEXITCODE."
        }
    }

    $env:AllowDevelopmentIdentity = 'true'
    $env:AzureWebJobsStorage = ''
    $env:AZURE_FUNCTIONS_ENVIRONMENT = 'Development'
    $env:CursorSigningKey = 'backend-live-test-cursor-signing-key-1234567890'
    $env:ApiScope = 'health-goals.sync'
    if ($null -ne (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)) {
        throw "Port $Port is already in use."
    }
    $hostProcess = Start-Process $functionsExecutable `
        -PassThru `
        -WorkingDirectory $repoRoot `
        -ArgumentList @(
            'start',
            '--script-root', '.\HealthGoalsTracker.Functions',
            '--dotnet-isolated',
            '--port', "$Port"
        ) `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $deadline = (Get-Date).AddSeconds(90)
    do {
        if ($hostProcess.HasExited) {
            throw "Functions host exited with code $($hostProcess.ExitCode)."
        }
        try {
            $health = Invoke-Api 'health' -Headers @{ 'X-Correlation-Id' = 'backend-health' }
            if ($health.StatusCode -eq 200) {
                break
            }
        }
        catch {
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    if ($null -eq $health -or $health.StatusCode -ne 200) {
        throw 'Functions health endpoint did not become ready.'
    }
    $listenerProcessId = (
        Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop |
        Select-Object -First 1
    ).OwningProcess

    $healthBody = $health.Content | ConvertFrom-Json
    if ($healthBody.status -ne 'healthy' -or
        $healthBody.version -ne 'v1' -or
        $health.Headers['X-Correlation-Id'] -ne 'backend-health') {
        throw 'Health response did not satisfy the version and correlation contract.'
    }
    Complete-LiveTestFeature 'backend.health' 'Real Functions host returned healthy v1 with the supplied correlation ID.'

    Assert-Status { Invoke-Api 'goals' } 401 'unauthorized'
    $userAHeaders = @{
        'X-HealthGoals-Test-Subject' = 'backend-test-user-a'
        'X-Correlation-Id' = 'backend-sync'
    }
    $userBHeaders = @{
        'X-HealthGoals-Test-Subject' = 'backend-test-user-b'
    }

    $invalidRequest = New-SyncRequest
    $invalidRequest.goals = @(
        @{
            id = [Guid]::NewGuid().ToString()
            name = 'Atomic Validation Goal'
            iconEmoji = 'A'
            points = 1
            sortOrder = 0
            isDefault = $false
            isWeeklyOnly = $false
            isDeleted = $false
            updatedAt = '2026-08-31T12:00:00Z'
        }
    )
    $invalidRequest.measurements = @(
        @{
            id = 'invalid'
            date = '2026-08-31'
            weightLbs = 180
            updatedAt = '2026-08-31T12:00:00Z'
        }
    )
    Assert-Status {
        Invoke-Api 'sync' -Method Post -Headers $userAHeaders -Body $invalidRequest
    } 400 'validation_failed'
    $goalsAfterInvalid = (Invoke-Api 'goals' -Headers $userAHeaders).Content |
        ConvertFrom-Json
    if ($goalsAfterInvalid.Count -ne 0) {
        throw 'Validation failure partially applied a valid goal.'
    }
    Assert-Status {
        Invoke-Api 'sync' -Method Post -Headers $userAHeaders -Body '{'
    } 400 'invalid_json'
    foreach ($nullPayload in @(
        '{"deviceId":"00000000-0000-0000-0000-000000000001","goals":null,"dailyRecords":[],"measurements":[]}',
        '{"deviceId":"00000000-0000-0000-0000-000000000001","goals":[],"dailyRecords":[{"id":"00000000-0000-0000-0000-000000000002","date":"2026-08-31","updatedAt":"2026-08-31T12:00:00Z","entries":null}],"measurements":[]}',
        '{"deviceId":"00000000-0000-0000-0000-000000000001","goals":[],"dailyRecords":[{"id":"00000000-0000-0000-0000-000000000002","date":"2026-08-31","updatedAt":"2026-08-31T12:00:00Z","entries":[null]}],"measurements":[]}'
    )) {
        Assert-Status {
            Invoke-Api 'sync' -Method Post -Headers $userAHeaders -Body $nullPayload
        } 400 'validation_failed'
    }
    Complete-LiveTestFeature 'backend.validation' 'Malformed and mixed-validity requests returned stable 400 responses with no writes.'

    $goalId = [Guid]::NewGuid().ToString()
    $recordId = [Guid]::NewGuid().ToString()
    $measurementId = [Guid]::NewGuid().ToString()
    $syncRequest = New-SyncRequest
    $syncRequest.goals = @(
        @{
            id = $goalId
            name = 'Private Goal Marker'
            iconEmoji = 'G'
            points = 3
            sortOrder = 0
            isDefault = $false
            isWeeklyOnly = $false
            isDeleted = $false
            updatedAt = '2026-08-31T12:00:00Z'
        }
    )
    $syncRequest.dailyRecords = @(
        @{
            id = $recordId
            date = '2026-08-31'
            totalPointsEarned = 99
            totalPointsPossible = 99
            updatedAt = '2026-08-31T12:00:00Z'
            entries = @(
                @{
                    id = [Guid]::NewGuid().ToString()
                    goalId = $goalId
                    goalName = 'Private Goal Marker'
                    iconEmoji = 'G'
                    goalPoints = 3
                    isWeeklyOnly = $false
                    isCompleted = $true
                    updatedAt = '2026-08-31T12:00:00Z'
                }
            )
        }
    )
    $syncRequest.measurements = @(
        @{
            id = $measurementId
            date = '2026-08-31'
            weightLbs = 987.654321
            bodyFatPercent = 76.54321
            notes = 'Private Note Marker'
            updatedAt = '2026-08-31T12:00:00Z'
        }
    )

    $firstSyncResponse = Invoke-Api 'sync' -Method Post -Headers $userAHeaders -Body $syncRequest
    $firstSync = $firstSyncResponse.Content | ConvertFrom-Json
    if ($firstSync.dailyRecords[0].totalPointsEarned -ne 3 -or
        $firstSync.dailyRecords[0].totalPointsPossible -ne 3) {
        throw 'Server did not recalculate daily totals from snapshots.'
    }
    if ([string]::IsNullOrWhiteSpace($firstSync.cursor)) {
        throw 'Server did not return an opaque cursor.'
    }
    $tamperedCursor = $firstSync.cursor.Substring(0, $firstSync.cursor.Length - 1) +
        $(if ($firstSync.cursor.EndsWith('A')) { 'B' } else { 'A' })
    $tamperedRequest = New-SyncRequest $tamperedCursor
    Assert-Status {
        Invoke-Api 'sync' -Method Post -Headers $userAHeaders -Body $tamperedRequest
    } 400 'validation_failed'
    $crossUserRequest = New-SyncRequest $firstSync.cursor
    Assert-Status {
        Invoke-Api 'sync' -Method Post -Headers $userBHeaders -Body $crossUserRequest
    } 400 'validation_failed'

    $replay = (Invoke-Api 'sync' -Method Post -Headers $userAHeaders -Body $syncRequest).Content |
        ConvertFrom-Json
    if ($replay.cursor -ne $firstSync.cursor -or $replay.goals.Count -ne 1) {
        throw 'Replaying the same sync request was not idempotent.'
    }

    $olderRequest = New-SyncRequest $firstSync.cursor
    $olderRequest.goals = @(
        @{
            id = $goalId
            name = 'Older Goal'
            iconEmoji = 'O'
            points = 1
            sortOrder = 0
            isDefault = $false
            isWeeklyOnly = $false
            isDeleted = $false
            updatedAt = '2026-08-31T11:00:00Z'
        }
    )
    $older = (Invoke-Api 'sync' -Method Post -Headers $userAHeaders -Body $olderRequest).Content |
        ConvertFrom-Json
    if ($older.goals.Count -ne 1 -or
        $older.goals[0].name -ne 'Private Goal Marker' -or
        $older.cursor -ne $firstSync.cursor) {
        throw 'Older update was not reconciled to the current winner.'
    }
    Complete-LiveTestFeature 'backend.sync' 'Sync recalculated scores, returned an opaque cursor, replayed idempotently, and rejected an older update.'

    $userBGoals = (Invoke-Api 'goals' -Headers $userBHeaders).Content | ConvertFrom-Json
    if ($userBGoals.Count -ne 0) {
        throw 'User B received User A data.'
    }
    Complete-LiveTestFeature 'backend.identity' 'Missing identity returned 401 and a second subject received no first-subject data.'

    $goals = (Invoke-Api 'goals' -Headers $userAHeaders).Content | ConvertFrom-Json
    $records = (Invoke-Api 'records?from=2026-08-01&to=2026-08-31' -Headers $userAHeaders).Content |
        ConvertFrom-Json
    $measurements = (Invoke-Api 'measurements?from=2026-08-01&to=2026-08-31' -Headers $userAHeaders).Content |
        ConvertFrom-Json
    if ($goals.Count -ne 1 -or $records.Count -ne 1 -or $measurements.Count -ne 1) {
        throw 'Recovery reads did not return the synchronized entities.'
    }
    Assert-Status {
        Invoke-Api 'records?from=2026-09-01&to=2026-08-01' -Headers $userAHeaders
    } 400 'validation_failed'
    Complete-LiveTestFeature 'backend.reads' 'Authenticated bounded reads returned goals, records, and measurements; invalid range returned 400.'
}
finally {
    if ($null -ne $listenerProcessId) {
        Stop-ProcessTree $listenerProcessId
    }
    if ($null -ne $hostProcess -and !$hostProcess.HasExited) {
        Stop-ProcessTree $hostProcess.Id
    }
    $env:AllowDevelopmentIdentity = $previousDevelopmentIdentity
    $env:AzureWebJobsStorage = $previousStorage
    $env:AZURE_FUNCTIONS_ENVIRONMENT = $previousFunctionsEnvironment
    $env:CursorSigningKey = $previousCursorSigningKey
    $env:ApiScope = $previousApiScope
}

$logs = ((Get-Content $stdoutPath -Raw) + (Get-Content $stderrPath -Raw))
foreach ($privateValue in @(
    'backend-test-user-a',
    'backend-test-user-b',
    'Private Goal Marker',
    'Private Note Marker',
    '987.654321',
    '76.54321'
)) {
    if ($logs.Contains($privateValue, [StringComparison]::Ordinal)) {
        throw "Functions logs contain private value '$privateValue'."
    }
}
if ($logs -notmatch 'Sync requested: goals 1, records 1, measurements 1') {
    throw 'Functions logs did not record non-sensitive sync batch metadata.'
}
if ($logs -match 'Unhandled exception|ConsecutiveErrors=[1-9]\d*') {
    throw 'Functions host logs contain an unhandled or initialization failure.'
}
Complete-LiveTestFeature 'backend.diagnostics' 'Host logs contain batch metadata without identities, names, measurements, notes, or bodies.'

Write-LiveTestReport (Join-Path $outputPath 'live-test-results.json') $requiredFeatures
Write-Host "Backend live verification passed. Evidence: $outputPath"
