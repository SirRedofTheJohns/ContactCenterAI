#requires -Version 7.2
[CmdletBinding()]
param([string]$DotNetExe, [switch]$InitializeLocks)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskWorkspace = Split-Path -Parent (Split-Path -Parent $taskRepo)
if (-not $DotNetExe) { $DotNetExe = Join-Path $taskWorkspace 'work/runtimes/dotnet/dotnet.exe' }
if (-not (Test-Path -LiteralPath $DotNetExe)) { $DotNetExe = (Get-Command dotnet -ErrorAction Stop).Source }
$taskPreviousEnvironment = @{}
$taskEnvironment = @{
    DOTNET_CLI_HOME = (Join-Path $taskRepo '.local/cli')
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'; DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'; DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    NUGET_PACKAGES = (Join-Path $taskRepo '.local/packages')
    NUGET_HTTP_CACHE_PATH = (Join-Path $taskRepo '.local/nuget-http')
    CCAI_OPERATIONAL_DB_PASSWORD = $null; CCAI_BFF_CLIENT_SECRET = $null
    CCAI_TLS_CERTIFICATE_PATH = $null; CCAI_TLS_CERTIFICATE_PASSWORD = $null
}
if ($IsWindows) { $taskEnvironment.APPDATA = Join-Path $taskRepo '.local/appdata' }
try {
    foreach ($taskName in $taskEnvironment.Keys) {
        $taskPreviousEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
        [Environment]::SetEnvironmentVariable($taskName, $taskEnvironment[$taskName], 'Process')
    }
    $taskExpectedSdk = (Get-Content -LiteralPath (Join-Path $taskRepo 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if ((& $DotNetExe --version) -cne $taskExpectedSdk) { throw 'INGRESS_SDK_MISMATCH' }
    $taskArtifacts = Join-Path $taskRepo '.local/build-ingress'
    $taskRestore = @('restore', (Join-Path $taskRepo 'ContactCenterAI.slnx'), '--artifacts-path', $taskArtifacts, '--configfile', (Join-Path $taskRepo 'NuGet.Config'))
    if ($InitializeLocks) { $taskRestore += '--force-evaluate' } else { $taskRestore += '--locked-mode' }
    $taskFeed = Join-Path $taskWorkspace 'work/nuget-feed'
    if (Test-Path -LiteralPath $taskFeed) { $taskRestore += @('--source', $taskFeed) }
    & $DotNetExe @taskRestore
    if ($LASTEXITCODE -ne 0) { throw 'INGRESS_RESTORE_FAILED' }
    & $DotNetExe build (Join-Path $taskRepo 'ContactCenterAI.slnx') --artifacts-path $taskArtifacts --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw 'INGRESS_BUILD_FAILED' }
    & $DotNetExe (Join-Path $taskArtifacts 'bin/ContactCenterAI.IngressChecks/release/ContactCenterAI.IngressChecks.dll') $taskRepo
    if ($LASTEXITCODE -ne 0) { throw 'INGRESS_CHECKS_FAILED' }
}
finally {
    foreach ($taskName in $taskPreviousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskName, $taskPreviousEnvironment[$taskName], 'Process')
    }
}
