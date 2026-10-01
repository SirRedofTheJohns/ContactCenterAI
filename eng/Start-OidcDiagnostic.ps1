#requires -Version 7.2
[CmdletBinding()]
param([string]$DotNetExe = 'dotnet')
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskEnvFile = Join-Path $taskRepo 'deploy/local/.env'
if (-not (Test-Path -LiteralPath $taskEnvFile)) { throw 'LOCAL_ENV_MISSING' }
$taskSecretLine = Get-Content -LiteralPath $taskEnvFile | Where-Object { $_ -match '^CCAI_BFF_CLIENT_SECRET=' }
if (@($taskSecretLine).Count -ne 1) { throw 'CLIENT_SECRET_SETTING_INVALID' }
$taskPreviousSecret = [Environment]::GetEnvironmentVariable('CCAI_BFF_CLIENT_SECRET', 'Process')
$taskPreviousLocation = Get-Location
$taskPreviousEnvironment = @{}
$taskEnvironment = @{
    DOTNET_CLI_HOME = (Join-Path $taskRepo '.local/cli')
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    DOTNET_NOLOGO = '1'
}
try {
    foreach ($taskName in $taskEnvironment.Keys) {
        $taskPreviousEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
        [Environment]::SetEnvironmentVariable($taskName, $taskEnvironment[$taskName], 'Process')
    }
    [Environment]::SetEnvironmentVariable('CCAI_BFF_CLIENT_SECRET', $taskSecretLine.Substring('CCAI_BFF_CLIENT_SECRET='.Length), 'Process')
    Set-Location -LiteralPath $taskRepo
    & $DotNetExe run --project 'spikes/RuntimeCompatibility/RuntimeCompatibility.csproj' --no-build --no-restore -c Release -- --serve-login
    if ($LASTEXITCODE -ne 0) { throw 'OIDC_DIAGNOSTIC_START_FAILED' }
}
finally {
    foreach ($taskName in $taskPreviousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskName, $taskPreviousEnvironment[$taskName], 'Process')
    }
    [Environment]::SetEnvironmentVariable('CCAI_BFF_CLIENT_SECRET', $taskPreviousSecret, 'Process')
    Set-Location -LiteralPath $taskPreviousLocation.Path
}
