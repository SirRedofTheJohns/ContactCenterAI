#requires -Version 7.2
[CmdletBinding()]
param(
    [string]$DotNetExe = 'dotnet',
    [switch]$OfflineOnly,
    [switch]$ManagedNetworkDiagnostic,
    [string]$PackageSource
)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskProject = Join-Path $taskRepo 'spikes/RuntimeCompatibility/RuntimeCompatibility.csproj'
$taskSdk = Get-Content -LiteralPath (Join-Path $taskRepo 'global.json') -Raw | ConvertFrom-Json
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
    NUGET_PLUGINS_CACHE_PATH = (Join-Path $taskRepo '.local/nuget-plugins')
}
if ($IsWindows) {
    # Child-process configuration only; no personal NuGet files or system settings changed.
    $taskEnvironment.APPDATA = Join-Path $taskRepo '.local/appdata'
    New-Item -ItemType Directory -Path (Join-Path $taskEnvironment.APPDATA 'NuGet') -Force | Out-Null
}
try {
    Set-Location -LiteralPath $taskRepo
    foreach ($taskName in $taskEnvironment.Keys) {
        $taskPreviousEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
        [Environment]::SetEnvironmentVariable($taskName, $taskEnvironment[$taskName], 'Process')
    }
    $taskActualSdk = & $DotNetExe --version
    if ($LASTEXITCODE -ne 0 -or $taskActualSdk -ne $taskSdk.sdk.version) {
        throw ('SDK_MISMATCH: install/use the pinned SDK ' + $taskSdk.sdk.version + '; see deploy/local/README.md.')
    }
    $taskRestoreArgs = @('restore', $taskProject, '--locked-mode', '--configfile', (Join-Path $taskRepo 'NuGet.Config'))
    if ($PackageSource) { $taskRestoreArgs += @('--source', $PackageSource) }
    & $DotNetExe @taskRestoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'RESTORE_FAILED: repair package-source access; do not disable TLS or signature verification.' }
    & $DotNetExe build $taskProject --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw 'SPIKE_BUILD_FAILED' }
    $taskMode = '--offline'
    if (-not $OfflineOnly) {
        $taskMode = '--live'
        if ($ManagedNetworkDiagnostic) { $taskMode = '--live-managed' }
        $taskEnvFile = Join-Path $taskRepo 'deploy/local/.env'
        if (-not (Test-Path -LiteralPath $taskEnvFile)) { throw 'LOCAL_ENV_MISSING' }
        $taskPasswordLine = Get-Content -LiteralPath $taskEnvFile | Where-Object { $_ -match '^CCAI_SQL_SA_PASSWORD=' }
        if (@($taskPasswordLine).Count -ne 1) { throw 'SQL_PASSWORD_SETTING_INVALID' }
        $taskPreviousEnvironment.CCAI_SQL_SA_PASSWORD = [Environment]::GetEnvironmentVariable('CCAI_SQL_SA_PASSWORD', 'Process')
        [Environment]::SetEnvironmentVariable('CCAI_SQL_SA_PASSWORD', $taskPasswordLine.Substring('CCAI_SQL_SA_PASSWORD='.Length), 'Process')
    }
    & $DotNetExe run --project $taskProject --no-build --no-restore -c Release -- $taskMode
    if ($LASTEXITCODE -ne 0) { throw 'B01_DEPENDENCY_CHECK_FAILED: live dependencies were not verified.' }
    Write-Output 'B01 checks completed for the selected mode. B01 closure also requires an interactive OIDC claim roundtrip.'
}
finally {
    foreach ($taskName in $taskPreviousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskName, $taskPreviousEnvironment[$taskName], 'Process')
    }
    Set-Location -LiteralPath $taskPreviousLocation.Path
}
