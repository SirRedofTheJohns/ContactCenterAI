#requires -Version 7.2
[CmdletBinding()]
param([string]$DotNetExe, [switch]$InitializeLocks)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskWorkspace = Split-Path -Parent (Split-Path -Parent $taskRepo)
if (-not $DotNetExe) { $DotNetExe = Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe' }
if (-not (Test-Path -LiteralPath $DotNetExe)) { $DotNetExe = (Get-Command dotnet -ErrorAction Stop).Source }
$taskDotnet = (Resolve-Path -LiteralPath $DotNetExe).Path
$taskPreviousLocation = Get-Location
$taskPreviousEnvironment = @{}
$taskEnvironment = @{
    DOTNET_CLI_HOME = (Join-Path $taskRepo '.local/cli')
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    DOTNET_NOLOGO = '1'
    NUGET_PACKAGES = (Join-Path $taskRepo '.local/packages')
    NUGET_HTTP_CACHE_PATH = (Join-Path $taskRepo '.local/nuget-http')
}
if ($IsWindows) { $taskEnvironment.APPDATA = Join-Path $taskRepo '.local/appdata' }
try {
    Set-Location -LiteralPath $taskRepo
    foreach ($taskName in $taskEnvironment.Keys) {
        $taskPreviousEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
        [Environment]::SetEnvironmentVariable($taskName, $taskEnvironment[$taskName], 'Process')
    }
    $taskExpectedSdk = (Get-Content -LiteralPath 'global.json' -Raw | ConvertFrom-Json).sdk.version
    if ((& $taskDotnet --version) -cne $taskExpectedSdk) { throw 'FOUNDATION_SDK_MISMATCH' }
    $taskArtifacts = Join-Path $taskRepo '.local/build-ingress'
    $taskRestore = @('restore', 'ContactCenterAI.slnx', '--artifacts-path', $taskArtifacts, '--configfile', (Join-Path $taskRepo 'NuGet.Config'))
    if ($InitializeLocks) { $taskRestore += '--force-evaluate' } else { $taskRestore += '--locked-mode' }
    $taskFeed = Join-Path $taskWorkspace 'work/nuget-feed'
    if (Test-Path -LiteralPath $taskFeed) { $taskRestore += @('--source', $taskFeed) }
    & $taskDotnet @taskRestore
    if ($LASTEXITCODE -ne 0) { throw 'FOUNDATION_RESTORE_FAILED' }
    & $taskDotnet build ContactCenterAI.slnx --artifacts-path $taskArtifacts --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw 'FOUNDATION_BUILD_FAILED' }
    $taskApiDll = Join-Path $taskArtifacts 'bin/ContactCenterAI.Api/release/ContactCenterAI.Api.dll'
    $taskChecks = @(& $taskDotnet (Join-Path $taskArtifacts 'bin/ContactCenterAI.FoundationChecks/release/ContactCenterAI.FoundationChecks.dll') $taskRepo $taskDotnet $taskApiDll)
    $taskCheckExitCode = $LASTEXITCODE
    $taskChecks | ForEach-Object { Write-Output $_ }
    if ($taskCheckExitCode -ne 0) { throw 'FOUNDATION_CHECKS_FAILED' }
    $taskSummary = @($taskChecks | Where-Object { $_ -match '^Foundation checks: ([0-9]+) passed\.' })
    if ($taskSummary.Count -ne 1) { throw 'FOUNDATION_CHECK_SUMMARY_MISSING' }
    $taskCount = [int]([regex]::Match($taskSummary[0], '^Foundation checks: ([0-9]+) passed\.').Groups[1].Value)
    & $taskDotnet (Join-Path $taskArtifacts 'bin/ContactCenterAI.Worker/release/ContactCenterAI.Worker.dll') --verify-startup
    if ($LASTEXITCODE -ne 0) { throw 'FOUNDATION_WORKER_START_FAILED' }
    $taskReport = [ordered]@{
        ticket = 'B02'; result = 'PASS'; observedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        sdk = $taskExpectedSdk; targetFramework = 'net10.0'; productProjects = 5
        releaseBuild = 'PASS'; warningsAsErrors = $true
        foundationChecksPassed = $taskCount; workerStartup = 'PASS'
        apiLiveness = 200; apiReadiness = 503; readinessReason = 'OPERATIONAL_STORE_NOT_CONFIGURED'
        businessRoute = 'Candidate rejects HTTP (426 HTTPS_REQUIRED)'; smokeProcessStopped = $true
        contractValidation = 'Local refs and bounded structural checks; not full OAS conformance'
        productAuthorizationTested = $false; aiEvaluationExecuted = $false; liveGenesysValidated = $false
        scope = 'Foundation only: references, host status, fail-closed readiness and passive worker startup'
    }
    $taskReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath 'docs/progress/b02-evidence.json' -Encoding utf8
    Write-Output 'PASS: B02 foundation evidence saved without credentials or arbitrary runtime logs.'
}
finally {
    foreach ($taskName in $taskPreviousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskName, $taskPreviousEnvironment[$taskName], 'Process')
    }
    Set-Location -LiteralPath $taskPreviousLocation.Path
}
