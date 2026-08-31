$script:LiveTestCatalog = $null
$script:LiveTestPlatform = $null
$script:LiveTestResults = [System.Collections.Generic.List[object]]::new()

function Initialize-LiveTestRun(
    [string]$Platform,
    [string[]]$RequiredFeatureIds
) {
    $catalogPath = Join-Path $PSScriptRoot 'features.json'
    $script:LiveTestCatalog = Get-Content $catalogPath -Raw | ConvertFrom-Json
    $script:LiveTestPlatform = $Platform
    $script:LiveTestResults.Clear()

    foreach ($featureId in $RequiredFeatureIds) {
        $feature = $script:LiveTestCatalog.features |
            Where-Object { $_.id -eq $featureId }
        if ($null -eq $feature) {
            throw "Live-test feature '$featureId' is not defined in '$catalogPath'."
        }
        if ($feature.platforms -notcontains $Platform) {
            throw "Live-test feature '$featureId' does not apply to '$Platform'."
        }
    }
}

function Complete-LiveTestFeature(
    [string]$FeatureId,
    [string]$Evidence
) {
    $feature = $script:LiveTestCatalog.features |
        Where-Object { $_.id -eq $FeatureId }
    if ($null -eq $feature) {
        throw "Cannot complete undefined live-test feature '$FeatureId'."
    }

    $script:LiveTestResults.Add([ordered]@{
        id = $feature.id
        title = $feature.title
        status = 'passed'
        evidence = $Evidence
    })
}

function Write-LiveTestReport(
    [string]$OutputPath,
    [string[]]$RequiredFeatureIds
) {
    $completedIds = @($script:LiveTestResults | ForEach-Object { $_.id })
    $missingIds = @($RequiredFeatureIds | Where-Object { $_ -notin $completedIds })
    if ($missingIds.Count -gt 0) {
        throw "Live-test report is missing required features: $($missingIds -join ', ')."
    }

    [ordered]@{
        schemaVersion = 1
        platform = $script:LiveTestPlatform
        completedAtUtc = [DateTime]::UtcNow.ToString('O')
        status = 'passed'
        features = $script:LiveTestResults
    } | ConvertTo-Json -Depth 5 | Set-Content $OutputPath
}
