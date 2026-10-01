#requires -Version 7.2
[CmdletBinding()]
param(
    [switch]$AcceptSqlDeveloperLicense,
    [switch]$OfflineOnly,
    [switch]$CheckOnly,
    [switch]$ManagedNetworkDiagnostic,
    [switch]$CompareSqlNetworking,
    [string]$DockerEndpoint
)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskWorkspace = Split-Path -Parent (Split-Path -Parent $taskRepo)
$taskSdkExe = Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $taskSdkExe)) { $taskSdkExe = 'dotnet' }
$taskRunnerArgs = @{ DotNetExe = $taskSdkExe }
$taskFeed = Join-Path $taskWorkspace 'work/nuget-feed'
if (Test-Path -LiteralPath $taskFeed) { $taskRunnerArgs.PackageSource = $taskFeed }
if ($ManagedNetworkDiagnostic) { $taskRunnerArgs.ManagedNetworkDiagnostic = $true }

& (Join-Path $PSScriptRoot 'Initialize-LocalEnvironment.ps1')
if ($OfflineOnly) {
    & (Join-Path $PSScriptRoot 'Invoke-B01.ps1') @taskRunnerArgs -OfflineOnly
    return
}
if ($CheckOnly) {
    if ($CompareSqlNetworking) {
        if ($ManagedNetworkDiagnostic) { throw 'Choose comparison or managed-only diagnosis, not both.' }
        Write-Output '=== Native SQL networking (selected baseline) ==='
        try {
            & (Join-Path $PSScriptRoot 'Invoke-B01.ps1') @taskRunnerArgs
            return
        }
        catch { Write-Output 'Native connectivity failed; comparing the managed diagnostic with encryption still mandatory.' }
        $taskRunnerArgs.ManagedNetworkDiagnostic = $true
        Write-Output '=== Managed SQL networking (debug only) ==='
        try { & (Join-Path $PSScriptRoot 'Invoke-B01.ps1') @taskRunnerArgs }
        catch { Write-Output 'Managed diagnostic also failed; use the SQL_ERROR/SQL_DIAGNOSTIC/NATIVE_ERROR fields above.' }
        throw 'B01_BASELINE_CONNECTIVITY_FAILED: diagnostic comparison does not close the native baseline gate.'
    }
    & (Join-Path $PSScriptRoot 'Invoke-B01.ps1') @taskRunnerArgs
    return
}
if ($CompareSqlNetworking) { throw 'Use -CheckOnly with -CompareSqlNetworking.' }

$taskEnvFile = Join-Path $taskRepo 'deploy/local/.env'
$taskEnvLines = [IO.File]::ReadAllLines($taskEnvFile)
$taskLicenseIndexes = @(for ($taskIndex = 0; $taskIndex -lt $taskEnvLines.Length; $taskIndex++) {
    if ($taskEnvLines[$taskIndex].StartsWith('CCAI_ACCEPT_SQL_EULA=', [StringComparison]::Ordinal)) { $taskIndex }
})
if ($taskLicenseIndexes.Count -ne 1) { throw 'SQL_LICENSE_SETTING_INVALID' }
if ($taskEnvLines[$taskLicenseIndexes[0]] -cne 'CCAI_ACCEPT_SQL_EULA=Y') {
    if (-not $AcceptSqlDeveloperLicense) {
        throw 'SQL_DEVELOPER_TERMS: review https://go.microsoft.com/fwlink/?LinkId=746388 and rerun with -AcceptSqlDeveloperLicense if you accept. No containers started.'
    }
    $taskEnvLines[$taskLicenseIndexes[0]] = 'CCAI_ACCEPT_SQL_EULA=Y'
    [IO.File]::WriteAllLines($taskEnvFile, $taskEnvLines, [Text.UTF8Encoding]::new($false))
    Write-Output 'SQL Server Developer license acceptance recorded locally.'
}

Write-Output 'Starting the local SQL Server and Keycloak containers; image downloads may take several minutes.'
& (Join-Path $PSScriptRoot 'Start-LocalDependencies.ps1') -DockerEndpoint $DockerEndpoint
$taskDeadline = [DateTimeOffset]::UtcNow.AddMinutes(2)
$taskReady = $false
while ([DateTimeOffset]::UtcNow -lt $taskDeadline) {
    try {
        $taskDiscovery = Invoke-RestMethod -Uri 'http://localhost:8080/realms/contactcenterai-local/.well-known/openid-configuration' -TimeoutSec 5
        if ($taskDiscovery.issuer -ceq 'http://localhost:8080/realms/contactcenterai-local') { $taskReady = $true; break }
    }
    catch { } # Only retry fixed loopback discovery; do not print response bodies or credentials.
    Write-Output 'Waiting for the local login service...'
    Start-Sleep -Seconds 5
}
if (-not $taskReady) { throw 'KEYCLOAK_NOT_READY: local issuer discovery did not pass within two minutes.' }
& (Join-Path $PSScriptRoot 'Invoke-B01.ps1') @taskRunnerArgs
Write-Output 'SQL/OIDC connectivity checks passed. The interactive login test is still needed to close B01.'
